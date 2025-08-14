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

    // Events
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

        // ЗАЩИТА верных клеток: если клетка уже в корректном окончательном состоянии — игнор ввода
        if (shouldFill && prev == CellState.Fill)  return; // не даём «снять» верное заполнение
        if (!shouldFill && prev == CellState.Empty) return; // не даём «перекрасить» верную пустую

        if (shouldFill)
        {
            // Разрешаем исправить ошибку → Fill
            cell.SetState(CellState.Fill);
        }
        else
        {
            // Уже была ошибкой — не наказываем повторно
            if (prev == CellState.Error) return;
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

        // ЗАЩИТА верных клеток: если клетка уже в корректном окончательном состоянии — игнор ввода
        if (shouldFill && prev == CellState.Fill)   return; // не даём «снять» верное заполнение ПКМ
        if (!shouldFill && prev == CellState.Empty) return; // повторное empty — игнор

        if (shouldFill)
        {
            // Уже была ошибкой — не наказываем повторно
            if (prev == CellState.Error) return;
            cell.SetState(CellState.Error);
            Punish($"RIGHT on filled ({r},{c})");
        }
        else
        {
            // Разрешаем исправить ошибку → Empty
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
    }

    void OnWin()
    {
        IsLocked = true;
        Debug.Log("WIN!");
        OnWinEvent?.Invoke();
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

            // ни Unknown, ни Error
            var st = cells[r,c].state;
            if (st == CellState.Unknown || st == CellState.Error) return false;

            bool shouldBeFill = level.target[idx] == 1;
            if (shouldBeFill != (st == CellState.Fill)) return false;
        }
        return true;
    }
}
