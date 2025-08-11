using UnityEngine;
using TMPro;

public class PuzzleController : MonoBehaviour
{
    [Header("Refs")]
    public LevelData level;           // решение (target), locks, clues
    public LevelUIRenderer ui;        // создаёт сетку
    public RectTransform gridRoot;

    [Header("Colors")]
    public Color colUnknown = new Color(0.08f,0.08f,0.08f);
    public Color colFill    = new Color(0f,1f,0.2f);
    public Color colEmpty   = new Color(0.02f,0.02f,0.02f);
    public Color colLock    = new Color(0.8f,0.1f,0.2f);
    public Color colError   = new Color(1f,0.25f,0.25f);

    [Header("Clue highlight")]
    public Color clueOK     = Color.white;
    public Color clueBad    = new Color(1f,0.5f,0.5f);
    public Color clueSolved = new Color(0.8f,1f,0.8f);

    int N;
    Cell[,] cells;
    TextMeshProUGUI[,] clueTexts;

    void Start()
    {
        if (ui && level && ui.level != level) ui.SetLevel(level);
        Rebind();
        ValidateAll();
    }

    public void SetLevel(LevelData data)
    {
        level = data;
        if (ui) ui.SetLevel(data);
        Rebind();
        ValidateAll();
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

    // --- Новая логика кликов ---
    public void TryLeft(int r, int c, Cell cell)   // попытка закрасить
    {
        int idx = r * N + c;
        bool shouldFill = (level.target != null && idx < level.target.Length && level.target[idx] == 1);

        if (shouldFill)
        {
            cell.SetState(CellState.Fill);
        }
        else
        {
            cell.SetState(CellState.Error);
            Punish($"Mistake: LEFT on empty at ({r},{c})");
        }

        ValidateNeighborsAround(r, c);
        if (IsWin()) OnWin();
    }

    public void TryRight(int r, int c, Cell cell)  // попытка пометить пусто
    {
        int idx = r * N + c;
        bool shouldFill = (level.target != null && idx < level.target.Length && level.target[idx] == 1);

        if (shouldFill)
        {
            cell.SetState(CellState.Error);
            Punish($"Mistake: RIGHT on filled at ({r},{c})");
        }
        else
        {
            cell.SetState(CellState.Empty);
        }

        ValidateNeighborsAround(r, c);
        if (IsWin()) OnWin();
    }

    void Punish(string reason)
    {
        // Пока только лог. Здесь потом — уменьшение «жизней», глич экрана и т.п.
        Debug.Log($"[Penalty] {reason}");
    }

    public void OnCellChanged(int r, int c) { /* под undo/redo */ }

    // --- Подсветка клюзов ---
    void ValidateAll() {
        if (level == null) return;
        for (int r=0; r<N; r++)
            for (int c=0; c<N; c++)
                ValidateClue(r, c);
    }

    void ValidateNeighborsAround(int r, int c) {
        for (int dr=-1; dr<=1; dr++)
            for (int dc=-1; dc<=1; dc++)
                ValidateClue(r+dr, c+dc);
    }

    void ValidateClue(int r, int c) {
        if (r<0||c<0||r>=N||c>=N) return;
        int clue = (level.clues != null && level.clues.Length == N*N) ? level.clues[r*N+c] : -1;
        var tmp = clueTexts[r,c];
        if (!tmp || clue < 0) return;

        int filled=0, unknown=0;
        for (int dr=-1; dr<=1; dr++)
            for (int dc=-1; dc<=1; dc++) {
                int rr=r+dr, cc=c+dc;
                if (rr<0||cc<0||rr>=N||cc>=N) continue;
                var s = cells[rr,cc].state;
                if (s==CellState.Fill) filled++;
                else if (s==CellState.Unknown) unknown++;
            }

        if      (filled > clue)                tmp.color = clueBad;
        else if (filled == clue && unknown==0) tmp.color = clueSolved;
        else                                   tmp.color = clueOK;
    }

    bool IsWin() {
        for (int r=0; r<N; r++)
        for (int c=0; c<N; c++) {
            int idx = r*N+c;
            if (cells[r,c].state == CellState.Lock) continue;
            if (cells[r,c].state == CellState.Unknown || cells[r,c].state == CellState.Error)
                return false;
            bool shouldBeFill = level.target[idx]==1;
            if (shouldBeFill != (cells[r,c].state==CellState.Fill)) return false;
        }
        return true;
    }

    void OnWin() {
        Debug.Log("WIN!");
        // Триггер эффекта/продолжения сюжета позже
    }
}
