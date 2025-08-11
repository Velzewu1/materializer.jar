using UnityEditor;
using UnityEngine;
using System;
using System.Collections.Generic;

public class FillapixEditorWindow : EditorWindow
{
    [Min(4)] public int gridSize = 16;                 // Текущий размер N×N
    readonly int[] presets = new[] { 8, 10, 12, 16, 20 };

    // Данные уровня (динамически пересоздаются под gridSize)
    int[,] target;      // 0/1
    int[,] clues;       // -1..9  (-1 = подсказка скрыта)
    int[,] locks;       // 0/1    (красные зоны = гарантированно пусто)
    bool[,] clueLock;   // замок конкретной подсказки
    bool[,] unknownAfterSolve;

    // Режимы рисования
    enum Mode { Paint, Lock, Clue }
    Mode mode = Mode.Paint;

    // UI флаги
    bool showClues = true;
    bool showMismatch = true;

    // Параметры «A: Random Prune + Validate»
    [Range(0.05f, 0.9f)] public float pruneMinFrac = 0.30f;
    [Range(0.05f, 0.9f)] public float pruneMaxFrac = 0.40f;
    public int pruneMaxAttempts = 200;
    public int pruneRandomSeed = 0;       // 0 = случайный
    public bool requireUniqueInA = false; // требовать уникальность при A

    [MenuItem("Tools/Fill-a-Pix/Editor (Variable)")]
    static void Open() => GetWindow<FillapixEditorWindow>("Fill-a-Pix");

    void OnEnable() => EnsureArrays(gridSize);

    void EnsureArrays(int N)
    {
        N = Mathf.Max(4, N);
        if (target != null && target.GetLength(0) == N) return;

        var oldT = target; var oldC = clues; var oldL = locks; var oldCL = clueLock; var oldU = unknownAfterSolve;

        target = new int[N, N];
        clues  = new int[N, N];
        locks  = new int[N, N];
        clueLock = new bool[N, N];
        unknownAfterSolve = new bool[N, N];

        if (oldT != null)
        {
            int m = Mathf.Min(N, oldT.GetLength(0));
            for (int r = 0; r < m; r++)
                for (int c = 0; c < m; c++)
                {
                    target[r, c] = oldT[r, c];
                    clues [r, c] = oldC[r, c];
                    locks [r, c] = oldL[r, c];
                    clueLock[r,c]= oldCL[r,c];
                    unknownAfterSolve[r, c] = oldU[r, c];
                }
        }
    }

    void OnGUI()
    {
        using (new EditorGUILayout.VerticalScope("box"))
        {
            EditorGUILayout.LabelField("Fill-a-Pix — редактор уровней", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Grid Size", GUILayout.Width(70));

                // пресеты
                int chosen = EditorGUILayout.IntPopup(gridSize,
                    Array.ConvertAll(presets, p => p.ToString()), presets, GUILayout.Width(120));

                // кастом
                int custom = EditorGUILayout.IntField(gridSize, GUILayout.Width(60));

                if (GUILayout.Button("Apply", GUILayout.Width(60)))
                {
                    gridSize = Mathf.Max(4, custom);
                    EnsureArrays(gridSize);
                    Repaint();
                }
                if (chosen != gridSize)
                {
                    gridSize = chosen;
                    EnsureArrays(gridSize);
                    Repaint();
                }

                GUILayout.FlexibleSpace();

                mode = (Mode)GUILayout.Toolbar((int)mode,
                    new[] { "PAINT (LMB/RMB)", "LOCK", "CLUE" }, GUILayout.Height(24));

                showClues    = GUILayout.Toggle(showClues, "Show clues", GUILayout.Width(110));
                showMismatch = GUILayout.Toggle(showMismatch, "Show mismatch", GUILayout.Width(120));

                if (GUILayout.Button("Clear", GUILayout.Width(80))) ClearAll();
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Generate clues (ALL)", GUILayout.Height(24))) GenerateCluesAll();
                if (GUILayout.Button("Recompute (unlocked)", GUILayout.Height(24))) RecomputeUnlocked();
                if (GUILayout.Button("Solve / Check", GUILayout.Height(24))) RunSolverReport();
                if (GUILayout.Button("Check Uniqueness", GUILayout.Height(24))) CheckUniquenessUI();
                if (GUILayout.Button("Save To Asset", GUILayout.Height(24))) SaveToAsset();
                if (GUILayout.Button("Load From Asset", GUILayout.Height(24))) LoadFromAsset();
            }
        }

        using (new EditorGUILayout.VerticalScope("box"))
        {
            EditorGUILayout.LabelField("Prune режим — Алгоритм A (Random + Validate)", EditorStyles.boldLabel);
            pruneMinFrac = EditorGUILayout.Slider("Min fraction", pruneMinFrac, 0.05f, 0.9f);
            pruneMaxFrac = EditorGUILayout.Slider("Max fraction", pruneMaxFrac, 0.05f, 0.9f);
            pruneMaxAttempts = EditorGUILayout.IntField("Max attempts", pruneMaxAttempts);
            pruneRandomSeed  = EditorGUILayout.IntField("Random seed (0 = random)", pruneRandomSeed);
            requireUniqueInA = EditorGUILayout.ToggleLeft("Require Uniqueness in A", requireUniqueInA);

            if (GUILayout.Button("Random Prune (Fast) — A", GUILayout.Height(24)))
                PruneRandomCluesWithValidation(pruneMinFrac, pruneMaxFrac, pruneMaxAttempts,
                    pruneRandomSeed == 0 ? (int?)null : pruneRandomSeed, requireUniqueInA);

            EditorGUILayout.HelpBox(
                "A: скрывает 30–40% незалоченных подсказок и валидирует решаемость без угадываний. " +
                "Опция уникальности добавляет ограниченный backtracking.",
                MessageType.Info);
        }

        DrawGrid();
    }

    void DrawGrid()
    {
        float cell = Mathf.Min(position.width - 40, position.height - 300) / gridSize;
        var rect = GUILayoutUtility.GetRect(cell * gridSize, cell * gridSize, GUILayout.ExpandWidth(false));
        EditorGUI.DrawRect(rect, Color.black);

        for (int r = 0; r < gridSize; r++)
        for (int c = 0; c < gridSize; c++)
        {
            var rc = new Rect(rect.x + c * cell, rect.y + r * cell, cell - 1, cell - 1);

            // фон
            Color bg = locks[r, c] == 1 ? new Color(0.8f, 0.1f, 0.2f) :
                       target[r, c] == 1 ? Color.green : Color.black;
            EditorGUI.DrawRect(rc, bg);

            // подсказки
            if (showClues && locks[r, c] == 0 && clues[r, c] >= 0)
            {
                EditorGUI.DrawRect(rc, new Color(0.6f, 1f, 0.6f, 0.35f));
                var st = new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleCenter };
                st.normal.textColor = Color.black;
                GUI.Label(rc, clues[r, c].ToString(), st);

                if (clueLock[r, c])
                {
                    var lockRect = new Rect(rc.x + 3, rc.y + 3, 8, 8);
                    EditorGUI.DrawRect(lockRect, Color.yellow);
                }

                if (showMismatch)
                {
                    int sum = Sum3x3At(target, r, c);
                    if (clues[r, c] != sum)
                        Handles.DrawSolidRectangleWithOutline(rc, new Color(0, 0, 0, 0), Color.red);
                }
            }
            else if (showClues && clueLock[r, c])
            {
                var lockRect = new Rect(rc.x + 3, rc.y + 3, 8, 8);
                EditorGUI.DrawRect(lockRect, Color.yellow);
            }

            if (unknownAfterSolve[r, c] && locks[r, c] == 0)
                EditorGUI.DrawRect(rc, new Color(1f, 1f, 0f, 0.35f));

            // ввод
            var e = Event.current;
            if (e.type == EventType.MouseDown && rc.Contains(e.mousePosition))
            {
                bool ctrl  = e.control || e.command;
                bool shift = e.shift;

                switch (mode)
                {
                    case Mode.Paint:
                        if (e.button == 0) { target[r, c] = 1; locks[r, c] = 0; }
                        else if (e.button == 1) { target[r, c] = 0; locks[r, c] = 0; }
                        Array.Clear(unknownAfterSolve, 0, unknownAfterSolve.Length);
                        Repaint(); e.Use(); break;

                    case Mode.Lock:
                        if (e.button == 0)
                        {
                            target[r, c] = 0; locks[r, c] = 1;
                            Array.Clear(unknownAfterSolve, 0, unknownAfterSolve.Length);
                            Repaint(); e.Use();
                        }
                        break;

                    case Mode.Clue:
                        if (ctrl && e.button == 0) { clueLock[r, c] = !clueLock[r, c]; Repaint(); e.Use(); break; }
                        if (e.button == 1) { clues[r, c] = -1; Repaint(); e.Use(); break; }
                        if (e.button == 0)
                        {
                            int val = clues[r, c];
                            if (val < 0) val = 0;
                            else
                            {
                                if (!shift) { val++; if (val > 9) val = -1; }
                                else        { val--; if (val < -1) val = 9; }
                            }
                            clues[r, c] = val; Repaint(); e.Use(); break;
                        }
                        break;
                }
            }
        }
    }

    void ClearAll()
    {
        for (int r=0;r<gridSize;r++)
        for (int c=0;c<gridSize;c++)
        {
            target[r,c]=0; locks[r,c]=0; clues[r,c]=-1; clueLock[r,c]=false;
        }
        Array.Clear(unknownAfterSolve, 0, unknownAfterSolve.Length);
        Repaint();
    }

    // --- Подсказки ---
    void GenerateCluesAll()
    {
        for (int r=0;r<gridSize;r++)
        for (int c=0;c<gridSize;c++)
            clues[r,c] = Sum3x3At(target, r, c);
        Array.Clear(unknownAfterSolve, 0, unknownAfterSolve.Length);
        Repaint();
    }

    void RecomputeUnlocked()
    {
        for (int r=0;r<gridSize;r++)
        for (int c=0;c<gridSize;c++)
            if (!clueLock[r,c]) clues[r,c] = Sum3x3At(target, r, c);
        Array.Clear(unknownAfterSolve, 0, unknownAfterSolve.Length);
        Repaint();
    }

    int Sum3x3At(int[,] A, int r, int c)
    {
        int s = 0;
        for (int dr=-1; dr<=1; dr++)
        for (int dc=-1; dc<=1; dc++)
        {
            int rr=r+dr, cc=c+dc;
            if (rr>=0 && rr<gridSize && cc>=0 && cc<gridSize) s += A[rr,cc];
        }
        return s;
    }

    // --- A: Random Prune + Validate ---
    void PruneRandomCluesWithValidation(float minFrac, float maxFrac, int maxAttempts, int? seed, bool needUnique)
    {
        var candidates = new List<Vector2Int>();
        for (int r=0;r<gridSize;r++)
        for (int c=0;c<gridSize;c++)
            if (locks[r,c]==0 && clues[r,c]>=0 && !clueLock[r,c])
                candidates.Add(new Vector2Int(r,c));

        if (candidates.Count == 0)
        {
            EditorUtility.DisplayDialog("Prune", "Нет доступных подсказок для удаления.", "OK");
            return;
        }

        int[,] backup = new int[gridSize, gridSize];
        Array.Copy(clues, backup, clues.Length);

        var rng = seed.HasValue ? new System.Random(seed.Value) : new System.Random();
        int attempts = 0;

        while (attempts++ < maxAttempts)
        {
            Array.Copy(backup, clues, clues.Length);

            float frac = Mathf.Lerp(minFrac, maxFrac, (float)rng.NextDouble());
            int toRemove = Mathf.Clamp(Mathf.RoundToInt(candidates.Count * frac), 1, candidates.Count);

            Shuffle(candidates, rng);
            for (int i=0;i<toRemove;i++)
            {
                var p = candidates[i];
                clues[p.x, p.y] = -1;
            }

            if (IsSolvableDeterministic(out _))
            {
                if (!needUnique || HasUniqueSolution(1) == UniqueResult.Unique)
                {
                    Array.Clear(unknownAfterSolve, 0, unknownAfterSolve.Length);
                    Repaint();
                    EditorUtility.DisplayDialog("Prune",
                        $"Удалено {toRemove} подсказок (~{(int)(100f * toRemove / Mathf.Max(1, candidates.Count))}%).\n" +
                        (needUnique ? "Решение уникально." : "Решаемо без угадываний."), "OK");
                    return;
                }
            }
        }

        Array.Copy(backup, clues, clues.Length);
        Array.Clear(unknownAfterSolve, 0, unknownAfterSolve.Length);
        Repaint();
        EditorUtility.DisplayDialog("Prune",
            $"Не удалось подобрать вариант после {maxAttempts} попыток.\nУменьши долю удаления или залочь важные подсказки.",
            "OK");
    }

    void Shuffle(List<Vector2Int> list, System.Random rng)
    {
        for (int i=list.Count-1; i>0; i--)
        {
            int j = rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    // --- Дет. валидатор/солвер/уникальность ---
    bool IsSolvableDeterministic(out int unknownCount)
    {
        int[,] state = InitStateFromLocks();
        if (!DeterministicSolveInPlace(state)) { unknownCount = -1; return false; }

        unknownCount = 0;
        for (int r=0;r<gridSize;r++)
        for (int c=0;c<gridSize;c++)
            if (locks[r,c]==0 && state[r,c]==-1) unknownCount++;

        return unknownCount == 0;
    }

    void RunSolverReport()
    {
        int[,] state = InitStateFromLocks();
        bool ok = DeterministicSolveInPlace(state);

        int unknownTotal = 0;
        Array.Clear(unknownAfterSolve, 0, unknownAfterSolve.Length);

        if (ok)
        {
            for (int r=0;r<gridSize;r++)
            for (int c=0;c<gridSize;c++)
            {
                bool u = (locks[r,c]==0 && state[r,c]==-1);
                unknownAfterSolve[r,c] = u;
                if (u) unknownTotal++;
            }
        }

        bool solved = ok && (unknownTotal == 0);
        EditorUtility.DisplayDialog(
            "Solver",
            solved ? "✅ Решаемо без угадываний."
                   : ok ? $"⚠️ Остались неопределённые клетки: {unknownTotal}\n(подсвечены жёлтым)"
                        : "❌ Противоречия с подсказками.",
            "OK");
        Repaint();
    }

    enum UniqueResult { Unique, Multiple, NoSolution, Inconclusive }

    void CheckUniquenessUI()
    {
        var res = HasUniqueSolution(1);
        string msg =
            res == UniqueResult.Unique     ? "✅ Решение уникально." :
            res == UniqueResult.Multiple   ? "⚠️ Более одного полного решения." :
            res == UniqueResult.NoSolution ? "❌ Решения не существует." :
                                             "ℹ️ Не удалось доказать уникальность.";
        EditorUtility.DisplayDialog("Uniqueness", msg, "OK");
    }

    UniqueResult HasUniqueSolution(int maxDepth)
    {
        int[,] baseState = InitStateFromLocks();
        if (!DeterministicSolveInPlace(baseState)) return UniqueResult.NoSolution;
        if (!CluesConsistent(baseState))          return UniqueResult.NoSolution;

        if (!FindFirstUnknown(baseState, out int ur, out int uc))
            return UniqueResult.Unique;

        // ветка 0
        var A = (int[,])baseState.Clone();
        A[ur, uc] = 0;
        var resA = Explore(A, maxDepth);

        // ветка 1
        var B = (int[,])baseState.Clone();
        B[ur, uc] = 1;
        var resB = Explore(B, maxDepth);

        if (resA == SolveKind.Solved && resB == SolveKind.Solved) return UniqueResult.Multiple;
        if (resA == SolveKind.Solved || resB == SolveKind.Solved) return UniqueResult.Unique;
        if (resA == SolveKind.Inconclusive || resB == SolveKind.Inconclusive) return UniqueResult.Inconclusive;
        return UniqueResult.NoSolution;
    }

    enum SolveKind { Solved, Impossible, Inconclusive }

    SolveKind Explore(int[,] s, int depthLeft)
    {
        if (!DeterministicSolveInPlace(s)) return SolveKind.Impossible;
        if (!CluesConsistent(s))           return SolveKind.Impossible;

        if (!FindFirstUnknown(s, out int ur, out int uc)) return SolveKind.Solved;
        if (depthLeft <= 0) return SolveKind.Inconclusive;

        var S0 = (int[,])s.Clone(); S0[ur, uc] = 0;
        var r0 = Explore(S0, depthLeft - 1);
        if (r0 == SolveKind.Solved)
        {
            var S1 = (int[,])s.Clone(); S1[ur, uc] = 1;
            var r1 = Explore(S1, depthLeft - 1);
            if (r1 == SolveKind.Solved) return SolveKind.Solved; // трактуется как Multiple выше
            return SolveKind.Solved;
        }
        else if (r0 == SolveKind.Impossible)
        {
            var S1 = (int[,])s.Clone(); S1[ur, uc] = 1;
            return Explore(S1, depthLeft - 1);
        }
        else
        {
            var S1 = (int[,])s.Clone(); S1[ur, uc] = 1;
            var r1 = Explore(S1, depthLeft - 1);
            if (r1 == SolveKind.Solved) return SolveKind.Solved;
            if (r0 == SolveKind.Inconclusive || r1 == SolveKind.Inconclusive) return SolveKind.Inconclusive;
            return SolveKind.Impossible;
        }
    }

    int[,] InitStateFromLocks()
    {
        var s = new int[gridSize, gridSize];
        for (int r=0;r<gridSize;r++)
        for (int c=0;c<gridSize;c++)
            s[r,c] = (locks[r,c]==1) ? 0 : -1;
        return s;
    }

    bool DeterministicSolveInPlace(int[,] s)
    {
        bool progress = true; int guard = 0;
        while (progress && guard++ < 400)
        {
            progress = false;

            for (int r=0;r<gridSize;r++)
            for (int c=0;c<gridSize;c++)
            {
                int clue = clues[r,c];
                if (clue < 0) continue;

                int filled=0, unknown=0;
                List<Vector2Int> unk = new List<Vector2Int>();

                for (int dr=-1; dr<=1; dr++)
                for (int dc=-1; dc<=1; dc++)
                {
                    int rr=r+dr, cc=c+dc;
                    if (rr<0||cc<0||rr>=gridSize||cc>=gridSize) continue;
                    int v = s[rr,cc];
                    if (v==1) filled++;
                    else if (v==-1) { unknown++; unk.Add(new Vector2Int(rr,cc)); }
                }

                int need = clue - filled;
                if (need < 0 || need > unknown) return false;

                if (need == 0 && unknown > 0)
                { foreach (var p in unk) { s[p.x,p.y] = 0; progress = true; } }
                else if (need == unknown && unknown > 0)
                { foreach (var p in unk) { s[p.x,p.y] = 1; progress = true; } }
            }
        }
        return true;
    }

    bool CluesConsistent(int[,] s)
    {
        for (int r=0;r<gridSize;r++)
        for (int c=0;c<gridSize;c++)
        {
            int clue = clues[r,c];
            if (clue < 0) continue;

            int minF=0, maxF=0;
            for (int dr=-1; dr<=1; dr++)
            for (int dc=-1; dc<=1; dc++)
            {
                int rr=r+dr, cc=c+dc;
                if (rr<0||cc<0||rr>=gridSize||cc>=gridSize) continue;
                if (s[rr,cc]==1) { minF++; maxF++; }
                else if (s[rr,cc]==-1) { maxF++; }
            }
            if (clue < minF || clue > maxF) return false;
        }
        return true;
    }

    bool FindFirstUnknown(int[,] s, out int ur, out int uc)
    {
        for (int r=0;r<gridSize;r++)
        for (int c=0;c<gridSize;c++)
            if (locks[r,c]==0 && s[r,c]==-1) { ur=r; uc=c; return true; }
        ur = uc = -1; return false;
    }

    // --- Save/Load ---
    void SaveToAsset()
    {
        var path = EditorUtility.SaveFilePanelInProject("Save LevelData",
            $"Level_{gridSize}x{gridSize}", "asset", "");
        if (string.IsNullOrEmpty(path)) return;

        var data = ScriptableObject.CreateInstance<LevelData>();
        data.size   = gridSize;
        data.target = new int[gridSize * gridSize];
        data.clues  = new int[gridSize * gridSize];
        data.locks  = new int[gridSize * gridSize];

        for (int r=0; r<gridSize; r++)
        for (int c=0; c<gridSize; c++)
        {
            data.Set(data.target, r, c, target[r, c]);
            data.Set(data.clues , r, c, Mathf.Clamp(clues[r, c], -1, 9));
            data.Set(data.locks , r, c, locks[r, c]);
        }

        AssetDatabase.CreateAsset(data, path);
        AssetDatabase.SaveAssets();
        EditorGUIUtility.PingObject(data);
    }

    void LoadFromAsset()
    {
        var path = EditorUtility.OpenFilePanel("Pick LevelData", "Assets", "asset");
        if (string.IsNullOrEmpty(path)) return;
        path = "Assets" + path.Replace(Application.dataPath, "");
        var data = AssetDatabase.LoadAssetAtPath<LevelData>(path);
        if (!data) { Debug.LogError("Can't load LevelData"); return; }

        gridSize = Mathf.Max(4, data.size);
        EnsureArrays(gridSize);

        for (int r=0; r<gridSize; r++)
        for (int c=0; c<gridSize; c++)
        {
            target[r,c] = data.Get(data.target, r, c);
            clues [r,c] = data.Get(data.clues , r, c);
            locks [r,c] = data.Get(data.locks , r, c);
            clueLock[r,c] = false;
        }

        Array.Clear(unknownAfterSolve, 0, unknownAfterSolve.Length);
        Repaint();
    }
}
