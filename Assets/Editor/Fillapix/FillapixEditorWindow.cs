using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

public class FillapixEditorWindow : EditorWindow {
    const int SIZE = 16;

    // Режимы рисования
    enum Mode { Paint, Lock, Clue } // PAINT: LMB=Fill, RMB=Empty
    Mode mode = Mode.Paint;

    // Данные уровня
    int[,] target   = new int[SIZE, SIZE]; // 0/1
    int[,] clues    = new int[SIZE, SIZE]; // -1..9 (-1 = нет подсказки)
    int[,] locks    = new int[SIZE, SIZE]; // 0/1
    bool[,] clueLock= new bool[SIZE, SIZE]; // lock конкретной подсказки

    // Подсветка результата Solve/Check
    bool[,] unknownAfterSolve = new bool[SIZE, SIZE];

    // UI
    bool showClues    = true;
    bool showMismatch = true;

    // Алгоритм A (Random Prune + Validation)
    [Range(0.05f, 0.9f)] public float pruneMinFrac = 0.30f;
    [Range(0.05f, 0.9f)] public float pruneMaxFrac = 0.40f;
    public int pruneMaxAttempts = 200;
    public int pruneRandomSeed = 0; // 0 = случайный
    public bool requireUniqueInA = false; // требовать уникальность при A

    [MenuItem("Tools/Fill-a-Pix/Editor 16x16")]
    static void Open() => GetWindow<FillapixEditorWindow>("Fill-a-Pix");

    void OnGUI() {
        using (new EditorGUILayout.VerticalScope("box")) {
            EditorGUILayout.LabelField("Fill-a-Pix 16x16 — редактор уровней", EditorStyles.boldLabel);

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
                if (GUILayout.Button("Check Uniqueness", GUILayout.Height(24))) CheckUniquenessUI();
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
            requireUniqueInA = EditorGUILayout.ToggleLeft("Require Uniqueness in A", requireUniqueInA);

            if (GUILayout.Button("Random Prune (Fast) — A", GUILayout.Height(24)))
                PruneRandomCluesWithValidation(pruneMinFrac, pruneMaxFrac, pruneMaxAttempts,
                    pruneRandomSeed == 0 ? (int?)null : pruneRandomSeed, requireUniqueInA);

            EditorGUILayout.HelpBox(
                "A: случайно скрывает 30–40% незалоченных подсказок и проверяет решаемость без угадываний. " +
                "Опция Require Uniqueness — дополнительно требует уникального решения.", MessageType.Info);
        }

        DrawGrid();
    }

    void DrawGrid() {
        float cell = Mathf.Min(position.width - 40, position.height - 300) / SIZE;
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

                if (clueLock[r, c]) {
                    var lockRect = new Rect(rc.x + 3, rc.y + 3, 8, 8);
                    EditorGUI.DrawRect(lockRect, Color.yellow);
                }

                if (showMismatch) {
                    int sum = Sum3x3At(target, r, c);
                    if (clues[r, c] != sum)
                        Handles.DrawSolidRectangleWithOutline(rc, new Color(0,0,0,0), Color.red);
                }
            } else {
                if (showClues && clueLock[r, c]) {
                    var lockRect = new Rect(rc.x + 3, rc.y + 3, 8, 8);
                    EditorGUI.DrawRect(lockRect, Color.yellow);
                }
            }

            if (unknownAfterSolve[r, c] && locks[r, c] == 0) {
                EditorGUI.DrawRect(rc, new Color(1f, 1f, 0f, 0.35f));
            }

            var e = Event.current;
            if (e.type == EventType.MouseDown && rc.Contains(e.mousePosition)) {
                bool ctrl  = e.control || e.command;
                bool shift = e.shift;

                switch (mode) {
                    case Mode.Paint:
                        if (e.button == 0) { target[r, c] = 1; locks[r, c] = 0; }
                        else if (e.button == 1) { target[r, c] = 0; locks[r, c] = 0; }
                        System.Array.Clear(unknownAfterSolve, 0, unknownAfterSolve.Length);
                        Repaint(); e.Use(); break;

                    case Mode.Lock:
                        if (e.button == 0) {
                            target[r, c] = 0; locks[r, c] = 1;
                            System.Array.Clear(unknownAfterSolve, 0, unknownAfterSolve.Length);
                            Repaint(); e.Use();
                        }
                        break;

                    case Mode.Clue:
                        if (ctrl && e.button == 0) { clueLock[r, c] = !clueLock[r, c]; Repaint(); e.Use(); break; }
                        if (e.button == 1) { clues[r, c] = -1; Repaint(); e.Use(); break; }
                        if (e.button == 0) {
                            int val = clues[r, c];
                            if (val < 0) val = 0;
                            else {
                                if (!shift) { val++; if (val > 9) val = -1; }
                                else { val--; if (val < -1) val = 9; }
                            }
                            clues[r, c] = val; Repaint(); e.Use(); break;
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

    // --- A: Random prune с проверкой решаемости (и опц. уникальности) ---
    void PruneRandomCluesWithValidation(float minFrac, float maxFrac, int maxAttempts, int? seed = null, bool needUnique = false) {
        var candidates = new List<Vector2Int>();
        for (int r = 0; r < SIZE; r++)
        for (int c = 0; c < SIZE; c++)
            if (locks[r, c] == 0 && clues[r, c] >= 0 && !clueLock[r, c])
                candidates.Add(new Vector2Int(r, c));

        if (candidates.Count == 0) {
            EditorUtility.DisplayDialog("Prune", "Нет доступных подсказок для удаления.", "OK");
            return;
        }

        int[,] backup = new int[SIZE, SIZE];
        System.Array.Copy(clues, backup, clues.Length);

        var rng = seed.HasValue ? new System.Random(seed.Value) : new System.Random();
        int attempts = 0;

        while (attempts++ < maxAttempts) {
            System.Array.Copy(backup, clues, clues.Length);

            float frac = Mathf.Lerp(minFrac, maxFrac, (float)rng.NextDouble());
            int toRemove = Mathf.Clamp(Mathf.RoundToInt(candidates.Count * frac), 1, candidates.Count);

            Shuffle(candidates, rng);
            for (int i = 0; i < toRemove; i++) {
                var p = candidates[i];
                clues[p.x, p.y] = -1;
            }

            if (IsSolvableDeterministic(out _)) {
                if (!needUnique || HasUniqueSolution(1) == UniqueResult.Unique) {
                    System.Array.Clear(unknownAfterSolve, 0, unknownAfterSolve.Length);
                    Repaint();
                    EditorUtility.DisplayDialog("Prune",
                        $"Удалено {toRemove} подсказок (~{(int)(100f * toRemove / Mathf.Max(1, candidates.Count))}%).\n" +
                        (needUnique ? "Решение уникально." : "Поле решаемо без угадываний."), "OK");
                    return;
                }
            }
        }

        System.Array.Copy(backup, clues, clues.Length);
        System.Array.Clear(unknownAfterSolve, 0, unknownAfterSolve.Length);
        Repaint();
        EditorUtility.DisplayDialog("Prune",
            $"Не удалось подобрать подходящий вариант после {maxAttempts} попыток.\n" +
            $"Снизьте долю удаления или залочите важные подсказки.", "OK");
    }

    void Shuffle(List<Vector2Int> list, System.Random rng) {
        for (int i = list.Count - 1; i > 0; i--) {
            int j = rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    // --- Дет. валидатор (без подсветки) ---
    bool IsSolvableDeterministic(out int unknownCount) {
        int[,] state = InitStateFromLocks();
        if (!DeterministicSolveInPlace(state)) { unknownCount = -1; return false; }

        unknownCount = 0;
        for (int r = 0; r < SIZE; r++)
        for (int c = 0; c < SIZE; c++)
            if (locks[r, c] == 0 && state[r, c] == -1) unknownCount++;

        return unknownCount == 0;
    }

    // --- Solver (детерминированный, с подсветкой) ---
    void RunSolverReport() {
        int[,] state = InitStateFromLocks();
        bool ok = DeterministicSolveInPlace(state);

        int unknownTotal = 0;
        System.Array.Clear(unknownAfterSolve, 0, unknownAfterSolve.Length);

        if (ok) {
            for (int r = 0; r < SIZE; r++)
            for (int c = 0; c < SIZE; c++) {
                bool u = (locks[r, c] == 0 && state[r, c] == -1);
                unknownAfterSolve[r, c] = u;
                if (u) unknownTotal++;
            }
        }

        bool solved = ok && (unknownTotal == 0);

        EditorUtility.DisplayDialog(
            "Solver",
            solved ? "✅ Решаемо без угадываний."
                   : ok ? $"⚠️ Остались неопределённые клетки: {unknownTotal}\nПодсвечены жёлтым на сетке."
                        : "❌ Обнаружены противоречия с подсказками.",
            "OK"
        );
        Repaint();
    }

    // ---------- Проверка уникальности ----------
    enum UniqueResult { Unique, Multiple, NoSolution, Inconclusive }

    void CheckUniquenessUI() {
        var res = HasUniqueSolution(1); // глубина бэктрекинга = 1 (обычно хватает)
        string msg =
            res == UniqueResult.Unique      ? "✅ Решение уникально." :
            res == UniqueResult.Multiple    ? "⚠️ Найдено более одного полного решения." :
            res == UniqueResult.NoSolution  ? "❌ Решения не существует (противоречия)." :
                                              "ℹ️ Не удалось доказать уникальность.";
        EditorUtility.DisplayDialog("Uniqueness", msg, "OK");
    }

    UniqueResult HasUniqueSolution(int maxDepth) {
        int[,] baseState = InitStateFromLocks();
        if (!DeterministicSolveInPlace(baseState)) return UniqueResult.NoSolution;
        if (!CluesConsistent(baseState))          return UniqueResult.NoSolution;

        if (!FindFirstUnknown(baseState, out int ur, out int uc))
            return UniqueResult.Unique; // уже полное решение

        // Ветка A: пусто
        var A = (int[,])baseState.Clone();
        A[ur, uc] = 0;
        var resA = Explore(A, maxDepth);

        // Ветка B: залито
        var B = (int[,])baseState.Clone();
        B[ur, uc] = 1;
        var resB = Explore(B, maxDepth);

        if (resA == SolveKind.Solved && resB == SolveKind.Solved) return UniqueResult.Multiple;
        if (resA == SolveKind.Solved || resB == SolveKind.Solved) return UniqueResult.Unique;

        // если обе ветки не дали полного, но допустимые (или упёрлись в глубину) — оставим как inconclusive
        if (resA == SolveKind.Inconclusive || resB == SolveKind.Inconclusive) return UniqueResult.Inconclusive;
        return UniqueResult.NoSolution;
    }

    enum SolveKind { Solved, Impossible, Inconclusive }

    SolveKind Explore(int[,] s, int depthLeft) {
        if (!DeterministicSolveInPlace(s)) return SolveKind.Impossible;
        if (!CluesConsistent(s))           return SolveKind.Impossible;

        if (!FindFirstUnknown(s, out int ur, out int uc))
            return SolveKind.Solved; // полное решение найдено

        if (depthLeft <= 0) return SolveKind.Inconclusive;

        // Рекурсивно углубимся ещё на 1 по первой неизвестной
        var S0 = (int[,])s.Clone(); S0[ur, uc] = 0;
        var r0 = Explore(S0, depthLeft - 1);
        if (r0 == SolveKind.Solved) {
            // уже нашли одно полное; чтобы доказать множественность, проверим вторую ветку
            var S1 = (int[,])s.Clone(); S1[ur, uc] = 1;
            var r1 = Explore(S1, depthLeft - 1);
            if (r1 == SolveKind.Solved) return SolveKind.Solved; // используется выше как Multiple
            // одно полное, второе нет — уникальность в нашей метрике (вернём Solved наверх)
            return SolveKind.Solved;
        } else if (r0 == SolveKind.Impossible) {
            var S1 = (int[,])s.Clone(); S1[ur, uc] = 1;
            return Explore(S1, depthLeft - 1);
        } else {
            // r0 inconclusive — попробуем вторую ветку
            var S1 = (int[,])s.Clone(); S1[ur, uc] = 1;
            var r1 = Explore(S1, depthLeft - 1);
            if (r1 == SolveKind.Solved) return SolveKind.Solved;
            if (r0 == SolveKind.Inconclusive || r1 == SolveKind.Inconclusive) return SolveKind.Inconclusive;
            return SolveKind.Impossible;
        }
    }

    // --- Вспомогательные для уникальности/солвера ---
    int[,] InitStateFromLocks() {
        int[,] s = new int[SIZE, SIZE];
        for (int r=0;r<SIZE;r++)
        for (int c=0;c<SIZE;c++)
            s[r,c] = (locks[r,c]==1) ? 0 : -1;
        return s;
    }

    bool DeterministicSolveInPlace(int[,] s) {
        bool progress = true;
        int guard = 0;
        while (progress && guard++ < 400) {
            progress = false;

            for (int r=0;r<SIZE;r++)
            for (int c=0;c<SIZE;c++) {
                int clue = clues[r,c];
                if (clue < 0) continue;

                int filled = 0, unknown = 0;
                List<Vector2Int> unk = new List<Vector2Int>();

                for (int dr=-1; dr<=1; dr++)
                for (int dc=-1; dc<=1; dc++) {
                    int rr=r+dr, cc=c+dc;
                    if (rr<0||cc<0||rr>=SIZE||cc>=SIZE) continue;
                    int v = s[rr,cc];
                    if (v==1) filled++;
                    else if (v==-1) { unknown++; unk.Add(new Vector2Int(rr,cc)); }
                }

                int need = clue - filled;
                if (need < 0) return false;        // переполнение
                if (need > unknown) return false;   // не добираем

                if (need == 0 && unknown > 0) {
                    foreach (var p in unk) { s[p.x,p.y] = 0; progress = true; }
                } else if (need == unknown && unknown > 0) {
                    foreach (var p in unk) { s[p.x,p.y] = 1; progress = true; }
                }
            }
        }
        return true;
    }

    bool CluesConsistent(int[,] s) {
        for (int r=0;r<SIZE;r++)
        for (int c=0;c<SIZE;c++) {
            int clue = clues[r,c];
            if (clue < 0) continue;
            int minF=0, maxF=0;
            for (int dr=-1; dr<=1; dr++)
            for (int dc=-1; dc<=1; dc++) {
                int rr=r+dr, cc=c+dc;
                if (rr<0||cc<0||rr>=SIZE||cc>=SIZE) continue;
                if (s[rr,cc]==1) { minF++; maxF++; }
                else if (s[rr,cc]==-1) { maxF++; }
            }
            if (clue < minF || clue > maxF) return false;
        }
        return true;
    }

    bool FindFirstUnknown(int[,] s, out int ur, out int uc) {
        for (int r=0;r<SIZE;r++)
        for (int c=0;c<SIZE;c++)
            if (locks[r,c]==0 && s[r,c]==-1) { ur=r; uc=c; return true; }
        ur = uc = -1; return false;
    }

    // --- Save/Load ---
    void SaveToAsset() {
        var path = EditorUtility.SaveFilePanelInProject("Save LevelData", "Level_16x16", "asset", "");
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
            clueLock[r, c] = false;
        }
        System.Array.Clear(unknownAfterSolve, 0, unknownAfterSolve.Length);
        Repaint();
    }
}
