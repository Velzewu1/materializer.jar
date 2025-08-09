using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

public class FillapixEditorWindow : EditorWindow {
    const int SIZE = 15;

    // Режимы рисования
    enum Mode { Paint, Lock, Clue } // PAINT: LMB=Fill, RMB=Empty
    Mode mode = Mode.Paint;

    // Данные уровня
    int[,] target   = new int[SIZE, SIZE]; // 0/1
    int[,] clues    = new int[SIZE, SIZE]; // -1..9 (-1 = нет подсказки)
    int[,] locks    = new int[SIZE, SIZE]; // 0/1
    bool[,] clueLock= new bool[SIZE, SIZE]; // lock конкретной подсказки (не переписывать Recompute)

    // Подсветка результата Solve/Check
    bool[,] unknownAfterSolve = new bool[SIZE, SIZE];

    // UI
    bool showClues    = true;
    bool showMismatch = true;

    // Параметры Алгоритма A (Random Prune + Validation)
    [Range(0.05f, 0.9f)] public float pruneMinFrac = 0.30f;
    [Range(0.05f, 0.9f)] public float pruneMaxFrac = 0.40f;
    public int pruneMaxAttempts = 200;
    public int pruneRandomSeed = 0; // 0 = случайный

    [MenuItem("Tools/Fill-a-Pix/Editor 15x15")]
    static void Open() => GetWindow<FillapixEditorWindow>("Fill-a-Pix");

    void OnGUI() {
        using (new EditorGUILayout.VerticalScope("box")) {
            EditorGUILayout.LabelField("Fill-a-Pix 15×15 — редактор уровней", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope()) {
                mode = (Mode)GUILayout.Toolbar((int)mode,
                    new[] { "PAINT (LMB/RMB)", "LOCK", "CLUE" }, GUILayout.Height(24));

                showClues    = GUILayout.Toggle(showClues, "Show clues", GUILayout.Width(110));
                showMismatch = GUILayout.Toggle(showMismatch, "Show mismatch", GUILayout.Width(120));

                if (GUILayout.Button("Clear", GUILayout.Width(80))) ClearAll();
            }

            using (new EditorGUILayout.HorizontalScope()) {
                if (GUILayout.Button("Generate clues (ALL)", GUILayout.Height(24))) GenerateCluesAll();
                if (GUILayout.Button("Recompute (unlocked)", GUILayout.Height(24))) RecomputeUnlocked();
                if (GUILayout.Button("Solve / Check", GUILayout.Height(24))) RunSolverReport();
                if (GUILayout.Button("Save To Asset", GUILayout.Height(24))) SaveToAsset();
                if (GUILayout.Button("Load From Asset", GUILayout.Height(24))) LoadFromAsset();
            }
        }

        // Панель параметров Алгоритма A
        using (new EditorGUILayout.VerticalScope("box")) {
            EditorGUILayout.LabelField("Prune режим — Алгоритм A (Random + Validate)", EditorStyles.boldLabel);
            pruneMinFrac = EditorGUILayout.Slider("Min fraction", pruneMinFrac, 0.05f, 0.9f);
            pruneMaxFrac = EditorGUILayout.Slider("Max fraction", pruneMaxFrac, 0.05f, 0.9f);
            pruneMaxAttempts = EditorGUILayout.IntField("Max attempts", pruneMaxAttempts);
            pruneRandomSeed = EditorGUILayout.IntField("Random seed (0 = random)", pruneRandomSeed);

            if (GUILayout.Button("Random Prune (Fast) — A", GUILayout.Height(24)))
                PruneRandomCluesWithValidation(pruneMinFrac, pruneMaxFrac, pruneMaxAttempts,
                    pruneRandomSeed == 0 ? (int?)null : pruneRandomSeed);

            EditorGUILayout.HelpBox(
                "Алгоритм A: случайно скрывает 30–40% видимых незалоченных подсказок и проверяет решаемость без угадываний. " +
                "При неудаче откатывает и пробует заново до Max attempts.", MessageType.Info);
        }

        DrawGrid();
    }

    void DrawGrid() {
        float cell = Mathf.Min(position.width - 40, position.height - 260) / SIZE;
        var rect = GUILayoutUtility.GetRect(cell * SIZE, cell * SIZE, GUILayout.ExpandWidth(false));
        EditorGUI.DrawRect(rect, Color.black);

        for (int r = 0; r < SIZE; r++)
        for (int c = 0; c < SIZE; c++) {
            var rc = new Rect(rect.x + c * cell, rect.y + r * cell, cell - 1, cell - 1);

            // фон клетки (LOCK — просто красный)
            Color bg = locks[r, c] == 1 ? new Color(0.8f, 0.1f, 0.2f) :
                       target[r, c] == 1 ? Color.green : Color.black;
            EditorGUI.DrawRect(rc, bg);

            // Подсказки (не показываем на LOCK)
            if (showClues && locks[r, c] == 0 && clues[r, c] >= 0) {
                EditorGUI.DrawRect(rc, new Color(0.6f, 1f, 0.6f, 0.35f));
                var st = new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleCenter };
                st.normal.textColor = Color.black;
                GUI.Label(rc, clues[r, c].ToString(), st);

                // Иконка lock у подсказки
                if (clueLock[r, c]) {
                    var lockRect = new Rect(rc.x + 3, rc.y + 3, 8, 8);
                    EditorGUI.DrawRect(lockRect, Color.yellow);
                }

                // mismatch с target
                if (showMismatch) {
                    int sum = Sum3x3At(target, r, c);
                    if (clues[r, c] != sum)
                        Handles.DrawSolidRectangleWithOutline(rc, new Color(0,0,0,0), Color.red);
                }
            } else {
                // показываем только lock-иконку, если подсказка скрыта, но залочена
                if (showClues && clueLock[r, c]) {
                    var lockRect = new Rect(rc.x + 3, rc.y + 3, 8, 8);
                    EditorGUI.DrawRect(lockRect, Color.yellow);
                }
            }

            // Подсветка неопределённых после Solve/Check
            if (unknownAfterSolve[r, c] && locks[r, c] == 0) {
                EditorGUI.DrawRect(rc, new Color(1f, 1f, 0f, 0.35f));
            }

            // Обработка кликов
            var e = Event.current;
            if (e.type == EventType.MouseDown && rc.Contains(e.mousePosition)) {
                bool ctrl  = e.control || e.command;
                bool shift = e.shift;

                switch (mode) {
                    case Mode.Paint:
                        if (e.button == 0) {           // ЛКМ — Fill
                            target[r, c] = 1; locks[r, c] = 0;
                        } else if (e.button == 1) {    // ПКМ — Empty
                            target[r, c] = 0; locks[r, c] = 0;
                        }
                        System.Array.Clear(unknownAfterSolve, 0, unknownAfterSolve.Length);
                        Repaint(); e.Use(); break;

                    case Mode.Lock:
                        if (e.button == 0) {           // ЛКМ — LOCK (просто красный)
                            target[r, c] = 0; locks[r, c] = 1;
                            System.Array.Clear(unknownAfterSolve, 0, unknownAfterSolve.Length);
                            Repaint(); e.Use();
                        }
                        break;

                    case Mode.Clue:
                        if (ctrl && e.button == 0) {   // Ctrl+ЛКМ — lock/unlock подсказки
                            clueLock[r, c] = !clueLock[r, c];
                            Repaint(); e.Use(); break;
                        }
                        if (e.button == 1) {           // ПКМ — сразу -1 (удалить)
                            clues[r, c] = -1;
                            Repaint(); e.Use(); break;
                        }
                        if (e.button == 0) {           // ЛКМ — цикл; по пустой начать с 0
                            int val = clues[r, c];
                            if (val < 0) val = 0;
                            else {
                                if (!shift) {          // +1
                                    val++;
                                    if (val > 9) val = -1; // 0..9 -> -1
                                } else {               // -1
                                    val--;
                                    if (val < -1) val = 9; // -1 -> 9
                                }
                            }
                            clues[r, c] = val;
                            Repaint(); e.Use(); break;
                        }
                        break;
                }
            }
        }
    }

    void ClearAll() {
        for (int r = 0; r < SIZE; r++)
        for (int c = 0; c < SIZE; c++) {
            target[r, c] = 0; locks[r, c] = 0; clues[r, c] = -1; clueLock[r, c] = false;
        }
        System.Array.Clear(unknownAfterSolve, 0, unknownAfterSolve.Length);
        Repaint();
    }

    // --- Подсказки из target ---
    void GenerateCluesAll() {
        for (int r = 0; r < SIZE; r++)
        for (int c = 0; c < SIZE; c++)
            clues[r, c] = Sum3x3At(target, r, c); // 0..9
        System.Array.Clear(unknownAfterSolve, 0, unknownAfterSolve.Length);
        Repaint();
    }

    void RecomputeUnlocked() {
        for (int r = 0; r < SIZE; r++)
        for (int c = 0; c < SIZE; c++) {
            if (!clueLock[r, c]) clues[r, c] = Sum3x3At(target, r, c);
        }
        System.Array.Clear(unknownAfterSolve, 0, unknownAfterSolve.Length);
        Repaint();
    }

    int Sum3x3At(int[,] A, int r, int c) {
        int s = 0;
        for (int dr = -1; dr <= 1; dr++)
        for (int dc = -1; dc <= 1; dc++) {
            int rr = r + dr, cc = c + dc;
            if (rr >= 0 && rr < SIZE && cc >= 0 && cc < SIZE) s += A[rr, cc];
        }
        return s;
    }

    // --- Алгоритм A: Random prune с проверкой решаемости (детерминизм) ---
    void PruneRandomCluesWithValidation(float minFrac, float maxFrac, int maxAttempts, int? seed = null) {
        // кандидаты: видимые, не залоченные, не на LOCK
        var candidates = new List<Vector2Int>();
        for (int r = 0; r < SIZE; r++)
        for (int c = 0; c < SIZE; c++) {
            if (locks[r, c] == 0 && clues[r, c] >= 0 && !clueLock[r, c])
                candidates.Add(new Vector2Int(r, c));
        }

        if (candidates.Count == 0) {
            EditorUtility.DisplayDialog("Prune", "Нет доступных подсказок для удаления.", "OK");
            return;
        }

        // бэкап подсказок
        int[,] backup = new int[SIZE, SIZE];
        System.Array.Copy(clues, backup, clues.Length);

        var rng = seed.HasValue ? new System.Random(seed.Value) : new System.Random();
        int attempts = 0;

        while (attempts++ < maxAttempts) {
            // восстановить исходные подсказки
            System.Array.Copy(backup, clues, clues.Length);

            // выбрать долю удаления (между min и max)
            float frac = Mathf.Lerp(minFrac, maxFrac, (float)rng.NextDouble());
            int toRemove = Mathf.Clamp(Mathf.RoundToInt(candidates.Count * frac), 1, candidates.Count);

            // перетасовать и скрыть первые toRemove
            Shuffle(candidates, rng);
            for (int i = 0; i < toRemove; i++) {
                var p = candidates[i];
                clues[p.x, p.y] = -1;
            }

            // проверка решаемости детерминизмом
            if (IsSolvableDeterministic(out _)) {
                System.Array.Clear(unknownAfterSolve, 0, unknownAfterSolve.Length);
                Repaint();
                EditorUtility.DisplayDialog("Prune",
                    $"Удалено {toRemove} подсказок (~{(int)(100f * toRemove / Mathf.Max(1, candidates.Count))}%).\n" +
                    $"Поле решаемо без угадываний.", "OK");
                return;
            }
        }

        // Не нашли решаемый вариант — откат
        System.Array.Copy(backup, clues, clues.Length);
        System.Array.Clear(unknownAfterSolve, 0, unknownAfterSolve.Length);
        Repaint();
        EditorUtility.DisplayDialog("Prune",
            $"Не удалось подобрать решаемый вариант после {maxAttempts} попыток.\n" +
            $"Снизьте долю удаления или залочите важные подсказки.", "OK");
    }

    void Shuffle(List<Vector2Int> list, System.Random rng) {
        for (int i = list.Count - 1; i > 0; i--) {
            int j = rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    // --- Дет. валидатор для Algorithm A (без подсветки) ---
    bool IsSolvableDeterministic(out int unknownCount) {
        int[,] state = new int[SIZE, SIZE]; // -1 unknown, 0 empty, 1 fill
        for (int r = 0; r < SIZE; r++)
        for (int c = 0; c < SIZE; c++)
            state[r, c] = (locks[r, c] == 1) ? 0 : -1;

        bool progress = true;
        int iters = 0, maxIters = 300;

        while (progress && iters++ < maxIters) {
            progress = false;

            for (int r = 0; r < SIZE; r++)
            for (int c = 0; c < SIZE; c++) {
                int clue = clues[r, c];
                if (clue < 0) continue; // нет числа

                int filled = 0, unknown = 0;
                var unk = new List<Vector2Int>();

                for (int dr = -1; dr <= 1; dr++)
                for (int dc = -1; dc <= 1; dc++) {
                    int rr = r + dr, cc = c + dc;
                    if (rr < 0 || cc < 0 || rr >= SIZE || cc >= SIZE) continue;
                    int v = state[rr, cc];
                    if (v == 1) filled++;
                    else if (v == -1) { unknown++; unk.Add(new Vector2Int(rr, cc)); }
                }

                int need = clue - filled;
                if (need == 0 && unknown > 0) {
                    foreach (var p in unk) { state[p.x, p.y] = 0; progress = true; }
                } else if (need == unknown && unknown > 0) {
                    foreach (var p in unk) { state[p.x, p.y] = 1; progress = true; }
                }
            }
        }

        unknownCount = 0;
        for (int r = 0; r < SIZE; r++)
        for (int c = 0; c < SIZE; c++)
            if (locks[r, c] == 0 && state[r, c] == -1) unknownCount++;

        return unknownCount == 0;
    }

    // --- Solver (детерминированный, с подсветкой в окне) ---
    void RunSolverReport() {
        int[,] state = new int[SIZE, SIZE]; // -1 unknown, 0 empty, 1 fill
        for (int r = 0; r < SIZE; r++)
        for (int c = 0; c < SIZE; c++)
            state[r, c] = (locks[r, c] == 1) ? 0 : -1;

        bool progress = true;
        int iters = 0, maxIters = 300;

        while (progress && iters++ < maxIters) {
            progress = false;

            for (int r = 0; r < SIZE; r++)
            for (int c = 0; c < SIZE; c++) {
                int clue = clues[r, c];
                if (clue < 0) continue; // нет числа

                int filled = 0, unknown = 0;
                List<Vector2Int> unk = new List<Vector2Int>();

                for (int dr = -1; dr <= 1; dr++)
                for (int dc = -1; dc <= 1; dc++) {
                    int rr = r + dr, cc = c + dc;
                    if (rr < 0 || cc < 0 || rr >= SIZE || cc >= SIZE) continue;
                    int v = state[rr, cc];
                    if (v == 1) filled++;
                    else if (v == -1) { unknown++; unk.Add(new Vector2Int(rr, cc)); }
                }

                int need = clue - filled;
                if (need == 0 && unknown > 0) {
                    foreach (var p in unk) { state[p.x, p.y] = 0; progress = true; }
                } else if (need == unknown && unknown > 0) {
                    foreach (var p in unk) { state[p.x, p.y] = 1; progress = true; }
                }
            }
        }

        int unknownTotal = 0;
        System.Array.Clear(unknownAfterSolve, 0, unknownAfterSolve.Length);

        for (int r = 0; r < SIZE; r++)
        for (int c = 0; c < SIZE; c++) {
            bool u = (locks[r, c] == 0 && state[r, c] == -1);
            unknownAfterSolve[r, c] = u;
            if (u) unknownTotal++;
        }

        bool solved = (unknownTotal == 0);

        EditorUtility.DisplayDialog(
            "Solver",
            solved ? "✅ Решаемо без угадываний."
                   : $"⚠️ Остались неопределённые клетки: {unknownTotal}\nПодсвечены жёлтым на сетке.",
            "OK"
        );
        Repaint();
    }

    // --- Save/Load ---
    void SaveToAsset() {
        var path = EditorUtility.SaveFilePanelInProject("Save LevelData", "Level_15x15", "asset", "");
        if (string.IsNullOrEmpty(path)) return;

        var data = ScriptableObject.CreateInstance<LevelData>();
        data.size   = SIZE;
        data.target = new int[SIZE * SIZE];
        data.clues  = new int[SIZE * SIZE];
        data.locks  = new int[SIZE * SIZE];

        for (int r = 0; r < SIZE; r++)
        for (int c = 0; c < SIZE; c++) {
            data.Set(data.target, r, c, target[r, c]);
            data.Set(data.clues , r, c, Mathf.Clamp(clues[r, c], -1, 9));
            data.Set(data.locks , r, c, locks[r, c]);
        }
        AssetDatabase.CreateAsset(data, path);
        AssetDatabase.SaveAssets();
        EditorGUIUtility.PingObject(data);
    }

    void LoadFromAsset() {
        var path = EditorUtility.OpenFilePanel("Pick LevelData", "Assets", "asset");
        if (string.IsNullOrEmpty(path)) return;
        path = "Assets" + path.Replace(Application.dataPath, "");
        var data = AssetDatabase.LoadAssetAtPath<LevelData>(path);
        if (data == null) { Debug.LogError("Can't load LevelData"); return; }
        if (data.size != SIZE) { Debug.LogError("Wrong size"); return; }

        for (int r = 0; r < SIZE; r++)
        for (int c = 0; c < SIZE; c++) {
            target[r, c] = data.Get(data.target, r, c);
            clues [r, c] = data.Get(data.clues , r, c);
            locks [r, c] = data.Get(data.locks , r, c);
            clueLock[r, c] = false; // лочим заново по желанию
        }
        System.Array.Clear(unknownAfterSolve, 0, unknownAfterSolve.Length);
        Repaint();
    }
}
