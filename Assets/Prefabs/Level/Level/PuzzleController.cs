using UnityEngine;
using TMPro;
using System;

public class PuzzleController : MonoBehaviour
{
    [Header("Refs")]
    public LevelData level;
    public LevelUIRenderer ui;
    public RectTransform gridRoot;

    [Header("Cell colors")]
    public Color colUnknown = new Color(0.08f,0.08f,0.08f);
    public Color colFill    = new Color(0f,1f,0.2f);
    public Color colEmpty   = new Color(0.02f,0.02f,0.02f);
    public Color colLock    = new Color(0.8f,0.1f,0.2f);
    public Color colError   = new Color(1f,0.25f,0.25f);

    [Header("Clue visuals")]
    public Color clueOK     = Color.white;
    public Color clueBad    = new Color(1f,0.5f,0.5f);
    public Color clueSolved = new Color(0.8f,1f,0.8f);
    public bool  hideSolvedClues = true;

    [Header("Strikes / Synchronisation")]
    [Min(1)] public int maxStrikes = 3;
    public float strikeCooldown = 0.2f;
    public bool  lockInputOnLose = true;

    public int  Strikes  { get; private set; }
    public bool IsLocked { get; private set; }
    public float Sync01  => Mathf.Clamp01(1f - (float)Strikes / Mathf.Max(1, maxStrikes));

    // ── Сохранения ───────────────────────────────────────────────────────────────
    [Header("Progress / Saves")]
    [Tooltip("Опционально: ссылка на LevelSwitcher, чтобы мы знали текущий индекс уровня.")]
    public LevelSwitcher levelSwitcher;
    [Tooltip("Сохранять следующий индекс уровня при победе.")]
    public bool saveOnWin = true;
    [Tooltip("Копить общий счётчик ошибок в сохранении.")]
    public bool saveStrikes = true;

    // Events (оставлены твоими, чтобы UI мог подписываться)
    public event Action<int,int,float> OnStrike;        // (strikes,max,sync01)
    public event Action<int,int>       OnStrikesChanged;
    public event Action<float>         OnSyncChanged;
    public event Action                OnLoseEvent;
    public event Action                OnWinEvent;

    int N;
    Cell[,] cells;
    TextMeshProUGUI[,] clueTexts;
    float _lastStrikeTime = -999f;

    void Start()
    {
        // Инициализация (без автозагрузки шага — это делает внешний менеджер/LevelSwitcher)
        if (ui && level && ui.level != level) ui.SetLevel(level);
        Rebind();
        ValidateAll();
        RaiseStrikesAndSync();
    }

    public void SetLevel(LevelData data)
    {
        level = data;
        if (ui) ui.SetLevel(data);
        Strikes = 0; IsLocked = false; _lastStrikeTime = -999f;
        Rebind();
        ValidateAll();
        RaiseStrikesAndSync();
    }

    void Rebind()
    {
        if (!ui || !level) return;

        N = level.size;
        cells = new Cell[N, N];
        clueTexts = new TextMeshProUGUI[N, N];

        int i = 0;
        foreach (Transform t in ui.transform)
        {
            int r = i / N, c = i % N; i++;

            var cell = t.GetComponent<Cell>();
            if (!cell) cell = t.gameObject.AddComponent<Cell>();
            cells[r,c] = cell;

            bool isLock = (level.locks != null && level.locks[r*N+c] == 1);
            cell.Setup(r, c, isLock ? CellState.Lock : CellState.Unknown, this);

            clueTexts[r,c] = t.GetComponentInChildren<TextMeshProUGUI>(true);
        }
    }

    // --- ЛКМ: попытка закрасить ---
    public void TryLeft(int r, int c, Cell cell)
    {
        if (IsLocked) return;

        int idx = r * N + c;
        bool shouldFill = (level.target != null && idx < level.target.Length && level.target[idx] == 1);
        var  prev       = cell.state;

        // защита от перезаписи верных состояний
        if (shouldFill && prev == CellState.Fill)  return;
        if (!shouldFill && prev == CellState.Empty) return;

        if (shouldFill)
        {
            cell.SetState(CellState.Fill);
        }
        else
        {
            if (prev == CellState.Error) return; // не дублируем штраф
            cell.SetState(CellState.Error);
            Punish($"LEFT on empty ({r},{c})");
        }

        ValidateNeighborsAround(r, c);
        if (IsWin()) OnWin();
    }

    // --- ПКМ: попытка пометить пусто ---
    public void TryRight(int r, int c, Cell cell)
    {
        if (IsLocked) return;

        int idx = r * N + c;
        bool shouldFill = (level.target != null && idx < level.target.Length && level.target[idx] == 1);
        var  prev       = cell.state;

        if (shouldFill && prev == CellState.Fill)   return;
        if (!shouldFill && prev == CellState.Empty) return;

        if (shouldFill)
        {
            if (prev == CellState.Error) return;
            cell.SetState(CellState.Error);
            Punish($"RIGHT on filled ({r},{c})");
        }
        else
        {
            cell.SetState(CellState.Empty);
        }

        ValidateNeighborsAround(r, c);
        if (IsWin()) OnWin();
    }

    // ---- Штраф → страйк ----
    void Punish(string reason)
    {
        if (Time.unscaledTime - _lastStrikeTime < strikeCooldown) return; // анти-дабл при драге
        _lastStrikeTime = Time.unscaledTime;

        Strikes++;
        Debug.Log($"[Penalty] {reason}. Strikes: {Strikes}/{maxStrikes}");

        // Сохраняем общий счётчик ошибок (не обязателен для прогресса по уровням)
        if (saveStrikes)
        {
            SaveManager.Load(); // гарантируем, что есть актуальная структура
            SaveManager.Data.totalStrikes++;
            SaveManager.Save();
        }

        OnStrike?.Invoke(Strikes, maxStrikes, Sync01);
        RaiseStrikesAndSync();

        if (Strikes >= maxStrikes)
            OnLose();
    }

    void RaiseStrikesAndSync()
    {
        OnStrikesChanged?.Invoke(Strikes, maxStrikes);
        OnSyncChanged?.Invoke(Sync01);
    }

    void OnLose()
    {
        if (lockInputOnLose) IsLocked = true;
        Debug.Log("[LOSE] Materialization failed.");
        OnLoseEvent?.Invoke();
        // НИЧЕГО не сохраняем про шаг — остаёмся на том же индексе.
    }

    void OnWin()
    {
        IsLocked = true;
        Debug.Log("WIN!");

        // ── Сохранить ТОЛЬКО номер уровня (следующий индекс) ───────────────
        if (saveOnWin)
        {
            SaveManager.Load();
            int nextIndex = ComputeNextIndex();
            if (nextIndex >= 0)
            {
                SaveManager.Data.stepIndex = nextIndex;
                SaveManager.Save();
            }
        }
        // сигналы наружу (камера/анимация/переключение сцены)
        OnWinEvent?.Invoke();
    }

    // Считаем индекс текущего уровня и «next» по LevelSwitcher (если задан)
    int ComputeNextIndex()
    {
        if (levelSwitcher == null || levelSwitcher.levels == null || levelSwitcher.levels.Length == 0)
            return -1;

        int cur = -1;
        for (int i = 0; i < levelSwitcher.levels.Length; i++)
        {
            if (levelSwitcher.levels[i] == level) { cur = i; break; }
        }
        if (cur < 0) cur = Mathf.Clamp(levelSwitcher.index, 0, levelSwitcher.levels.Length - 1);

        int next = Mathf.Min(cur + 1, levelSwitcher.levels.Length - 1);
        return next;
    }

    public void OnCellChanged(int r, int c) { /* reserved for undo/redo */ }

    // ---------- Подсказки ----------
    void ValidateAll()
    {
        if (level == null) return;
        for (int r=0; r<N; r++)
            for (int c=0; c<N; c++)
                ValidateClue(r, c);
    }

    void ValidateNeighborsAround(int r, int c)
    {
        for (int dr=-1; dr<=1; dr++)
            for (int dc=-1; dc<=1; dc++)
                ValidateClue(r+dr, c+dc);
    }

    void ValidateClue(int r, int c)
    {
        if (r<0||c<0||r>=N||c>=N) return;

        int clue = (level.clues != null && level.clues.Length == N*N) ? level.clues[r*N+c] : -1;
        var tmp  = clueTexts[r,c];
        if (!tmp || clue < 0) return;

        int filled=0, unknown=0;
        for (int dr=-1; dr<=1; dr++)
            for (int dc=-1; dc<=1; dc++)
            {
                int rr=r+dr, cc=c+dc;
                if (rr<0||cc<0||rr>=N||cc>=N) continue;
                var s = cells[rr,cc].state;
                if (s==CellState.Fill) filled++;
                else if (s==CellState.Unknown) unknown++;
            }

        bool solved = (filled == clue && unknown == 0);
        bool over   = (filled > clue);

        if (!hideSolvedClues)
        {
            if      (over)   tmp.color = clueBad;
            else if (solved) tmp.color = clueSolved;
            else             tmp.color = clueOK;
            tmp.gameObject.SetActive(true);
        }
        else
        {
            tmp.color = over ? clueBad : clueOK;
            tmp.gameObject.SetActive(!solved);
        }
    }

    // ---------- Победа ----------
    bool IsWin()
    {
        for (int r=0; r<N; r++)
        for (int c=0; c<N; c++)
        {
            int idx = r*N+c;
            if (cells[r,c].state == CellState.Lock) continue;

            var st = cells[r,c].state;
            if (st == CellState.Unknown || st == CellState.Error) return false;

            bool shouldBeFill = level.target[idx] == 1;
            if (shouldBeFill != (st == CellState.Fill)) return false;
        }
        return true;
    }
}
