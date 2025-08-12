using UnityEngine;
using UnityEngine.UI;
using TMPro;

[RequireComponent(typeof(GridLayoutGroup), typeof(RectTransform))]
public class LevelUIRenderer : MonoBehaviour
{
    [Header("Data")]
    public LevelData level;

    [Header("UI")]
    public GameObject cellPrefab;
    public RectTransform container;
    public bool matchContainer = true;

    [Header("Grid look")]
    public Image gridImage;
    public Color gridColor = new Color(1f, 1f, 1f, 0.55f);

    [Header("Layout (px)")]
    [Min(0)] public float spacingPx = 2f;
    [Min(0)] public float outerBorderPx = 2f;

    [Header("Cell colors")]
    public Color colEmpty    = new Color(0.06f,0.06f,0.06f);
    public Color colFill     = new Color(0f, 1f, 0.2f);
    public Color colLock     = new Color(0.8f, 0.1f, 0.2f);
    public Color colClueText = Color.white;

    [Header("Hover 3x3 highlight")]
    public bool  hoverHighlightEnabled = true;
    public bool  highlightOnlyUnknown  = true;          // <<< ВАЖНО: подсвечивать только Unknown
    public Color hoverNeighbor = new Color(0f, 1f, 0.5f, 0.16f);
    public Color hoverCenter   = new Color(0f, 1f, 0.7f, 0.26f);

    GridLayoutGroup _grid;
    RectTransform   _rt;
    CanvasScaler    _scaler;

    Cell[]  _cells;
    Image[] _overlays;
    int _hiR = -1, _hiC = -1;
    bool _hoverSuppressed = false;

    float RefPPU => (_scaler ? _scaler.referencePixelsPerUnit : 100f);
    float Px2World(float px) => px / Mathf.Max(1f, RefPPU);

    void Awake()
    {
        _grid = GetComponent<GridLayoutGroup>();
        _rt   = GetComponent<RectTransform>();
        var canvas = GetComponentInParent<Canvas>();
        _scaler = canvas ? canvas.GetComponent<CanvasScaler>() : null;

        if (!container) container = _rt;
        if (!gridImage) gridImage = GetComponent<Image>();

        _rt.anchorMin = _rt.anchorMax = new Vector2(0.5f, 0.5f);
        _rt.pivot     = new Vector2(0.5f, 0.5f);

        _grid.startCorner    = GridLayoutGroup.Corner.UpperLeft;
        _grid.startAxis      = GridLayoutGroup.Axis.Horizontal;
        _grid.childAlignment = TextAnchor.UpperLeft;
        _grid.constraint     = GridLayoutGroup.Constraint.FixedColumnCount;
        _grid.padding        = new RectOffset(0,0,0,0);
        _grid.spacing        = Vector2.zero;

        if (level && cellPrefab) { Build(); FitCells(); }
    }

    void OnEnable()                        { FitCells(); }
    void OnRectTransformDimensionsChange() { FitCells(); }

    public void SetLevel(LevelData data) { level = data; Build(); FitCells(); }

    public void Build()
    {
        ClearHighlightInternal(true);
        if (!level || !cellPrefab) return;

        for (int i = transform.childCount - 1; i >= 0; --i)
            DestroyImmediate(transform.GetChild(i).gameObject);

        int N = Mathf.Max(1, level.size);
        _grid.constraintCount = N;
        _cells    = new Cell[N * N];
        _overlays = new Image[N * N];

        for (int r = 0; r < N; r++)
        for (int c = 0; c < N; c++)
        {
            int i = r * N + c;

            var go  = Instantiate(cellPrefab, transform);
            var rtC = go.transform as RectTransform;
            if (rtC)
            {
                rtC.localScale = Vector3.one;
                rtC.localRotation = Quaternion.identity;
                rtC.anchoredPosition3D = Vector3.zero;
            }

            var img = go.GetComponent<Image>();
            if (img)
            {
                if (SafeGet(level.locks, i) == 1) img.color = colLock;
                else img.color = colEmpty;
            }

            var tmp = go.GetComponentInChildren<TextMeshProUGUI>(true);
            if (tmp)
            {
                bool isLock  = (SafeGet(level.locks, i) == 1);
                int clue     = SafeGet(level.clues, i, -1);
                if (!isLock && clue >= 0)
                {
                    tmp.text = clue.ToString();
                    tmp.color = colClueText;
                    tmp.enableAutoSizing = true;
                    tmp.fontSizeMin = 4; tmp.fontSizeMax = 300;
                    tmp.alignment = TextAlignmentOptions.Center;
                    tmp.raycastTarget = false;
                    tmp.gameObject.SetActive(true);
                }
                else tmp.gameObject.SetActive(false);
            }

            var cell = go.GetComponent<Cell>();
            if (cell) _cells[i] = cell;

            // Overlay-слой для подсветки (поверх клетки, но raycast выключен)
            var hlGO = new GameObject("HL", typeof(RectTransform), typeof(Image));
            var hlRT = hlGO.GetComponent<RectTransform>();
            hlRT.SetParent(go.transform, false);
            hlRT.anchorMin = Vector2.zero;
            hlRT.anchorMax = Vector2.one;
            hlRT.offsetMin = Vector2.zero;
            hlRT.offsetMax = Vector2.zero;

            var hlImg = hlGO.GetComponent<Image>();
            hlImg.color = hoverNeighbor;
            hlImg.raycastTarget = false;
            hlImg.enabled = false;
            _overlays[i] = hlImg;
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(_rt);
    }

    public void FitCells()
    {
        if (!level) return;

        if (gridImage) { gridImage.color = gridColor; gridImage.raycastTarget = false; }

        var host = container ? container : _rt;
        float W = Mathf.Max(0.0001f, host.rect.width);
        float H = Mathf.Max(0.0001f, host.rect.height);

        float gapWU    = Mathf.Max(Px2World(Mathf.Max(1f, spacingPx)), 0.0001f);
        float borderWU = Mathf.Max(Px2World(Mathf.Max(1f, outerBorderPx)), 0.0001f);

        float availW = Mathf.Max(0, W - 2f * borderWU);
        float availH = Mathf.Max(0, H - 2f * borderWU);

        int   N     = Mathf.Max(1, level.size);
        float sideX = (availW - gapWU * (N - 1)) / N;
        float sideY = (availH - gapWU * (N - 1)) / N;
        float side  = Mathf.Max(0.0001f, Mathf.Min(sideX, sideY));

        _grid.cellSize = new Vector2(side, side);
        _grid.spacing  = new Vector2(gapWU, gapWU);

        if (matchContainer)
        {
            _rt.anchorMin = _rt.anchorMax = new Vector2(0.5f, 0.5f);
            _rt.pivot     = new Vector2(0.5f, 0.5f);
            _rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, availW);
            _rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,   availH);
            _rt.position = host.position;
            _rt.rotation = host.rotation;
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(_rt);
    }

    // External control (drag)
    public void SetHoverSuppressed(bool suppressed)
    {
        if (_hoverSuppressed == suppressed) return;
        _hoverSuppressed = suppressed;
        if (suppressed) ClearHighlight();
    }

    public void ClearHighlight() => ClearHighlightInternal(false);

    void ClearHighlightInternal(bool onRebuild)
    {
        if (_overlays == null) return;
        if (!onRebuild && _hiR >= 0 && _hiC >= 0)
        {
            int N = level.size;
            for (int dr = -1; dr <= 1; dr++)
            for (int dc = -1; dc <= 1; dc++)
            {
                int rr = _hiR + dr, cc = _hiC + dc;
                if (rr < 0 || cc < 0 || rr >= N || cc >= N) continue;
                int id = rr * N + cc;
                if (_overlays[id]) _overlays[id].enabled = false;
            }
        }
        _hiR = _hiC = -1;
    }

    public void Highlight3x3(int r, int c)
    {
        if (!hoverHighlightEnabled || _hoverSuppressed || level == null) return;

        ClearHighlight();
        int N = level.size;

        for (int dr = -1; dr <= 1; dr++)
        for (int dc = -1; dc <= 1; dc++)
        {
            int rr = r + dr, cc = c + dc;
            if (rr < 0 || cc < 0 || rr >= N || cc >= N) continue;
            int id = rr * N + cc;

            var img = (_overlays != null && id < _overlays.Length) ? _overlays[id] : null;
            if (!img) continue;

            // --- КЛЮЧЕВОЕ УСЛОВИЕ ---
            // Подсвечиваем ТОЛЬКО Unknown (если флаг включён).
            if (highlightOnlyUnknown && _cells != null && id < _cells.Length && _cells[id] != null)
            {
                var st = _cells[id].state;
                if (st != CellState.Unknown)
                {
                    img.enabled = false;
                    continue;
                }
            }

            img.color   = (dr == 0 && dc == 0) ? hoverCenter : hoverNeighbor;
            img.enabled = true;
        }

        _hiR = r; _hiC = c;
    }

    public void HighlightAtScreen(Vector2 screenPos, Camera cam)
    {
        if (!hoverHighlightEnabled || _hoverSuppressed || level == null) { ClearHighlight(); return; }

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_rt, screenPos, cam, out var local))
        { ClearHighlight(); return; }

        var rect = _rt.rect;
        float x0 = local.x - rect.xMin;
        float y0 = local.y - rect.yMin;
        float yTop = rect.height - y0;

        int N = level.size;
        var cs = _grid.cellSize; var sp = _grid.spacing;

        int c = Mathf.FloorToInt(x0 / (cs.x + sp.x));
        int r = Mathf.FloorToInt(yTop / (cs.y + sp.y));
        if (c < 0 || c >= N || r < 0 || r >= N) { ClearHighlight(); return; }

        float cx = c * (cs.x + sp.x);
        float cy = r * (cs.y + sp.y);
        if (x0 > cx + cs.x || yTop > cy + cs.y) { ClearHighlight(); return; }

        Highlight3x3(r, c);
    }

    // Hit-test
    public bool TryGetCellIndexAtScreen(Vector2 screenPos, Camera cam, out int idx, out int r, out int c)
    {
        idx = -1; r = -1; c = -1;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_rt, screenPos, cam, out var local))
            return false;

        var rect = _rt.rect;
        float x0 = local.x - rect.xMin;
        float y0 = local.y - rect.yMin;
        float yTop = rect.height - y0;

        int N = Mathf.Max(1, level.size);
        var cell = _grid.cellSize; var sp = _grid.spacing;

        c = Mathf.FloorToInt(x0 / (cell.x + sp.x));
        r = Mathf.FloorToInt(yTop / (cell.y + sp.y));
        if (c < 0 || c >= N || r < 0 || r >= N) return false;

        float cx = c * (cell.x + sp.x);
        float cyTop = r * (cell.y + sp.y);
        if (x0 > cx + cell.x || yTop > cyTop + cell.y) return false;

        idx = r * N + c;
        return (idx >= 0 && idx < transform.childCount);
    }

    // Click API
    public bool TryClickAtScreen(Vector2 screenPos, Camera cam)
        => TryClickAtScreen(screenPos, cam, ClickKind.Left);

    public bool TryClickAtScreen(Vector2 screenPos, Camera cam, ClickKind kind)
    {
        if (!TryGetCellIndexAtScreen(screenPos, cam, out int idx, out int r, out int c))
            return false;

        var cellComp = (_cells != null && idx < _cells.Length) ? _cells[idx]
                        : transform.GetChild(idx).GetComponent<Cell>();
        if (!cellComp || cellComp.state == CellState.Lock) return false;

        var ctrl = cellComp.GetComponentInParent<PuzzleController>();
        if (!ctrl) return false;

        if (kind == ClickKind.Left)  ctrl.TryLeft (r, c, cellComp);
        else                         ctrl.TryRight(r, c, cellComp);
        return true;
    }

    static int SafeGet(int[] a, int i, int def = 0)
        => (a != null && i >= 0 && i < a.Length) ? a[i] : def;
}
