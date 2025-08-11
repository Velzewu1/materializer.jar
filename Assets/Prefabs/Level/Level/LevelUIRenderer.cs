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
    public RectTransform container;          // SafeZone; если null — берём свой RectTransform
    public bool matchContainer = true;

    [Header("Grid look")]
    public Image gridImage;                   // ← назначается в инспекторе (Image на LevelRoot)
    public Color gridColor = new Color(1f,1f,1f,0.55f); // цвет «линий» сетки (белый с альфой)

    [Header("Layout (px)")]
    [Min(0)] public float paddingPx = 8f;
    [Min(0)] public float spacingPx = 2f;    // толщина линий сетки в ПИКСЕЛЯХ

    [Header("Cell colors")]
    public Color colEmpty    = new Color(0.06f,0.06f,0.06f);
    public Color colFill     = new Color(0f, 1f, 0.2f);
    public Color colLock     = new Color(0.8f, 0.1f, 0.2f);
    public Color colClueText = Color.white;

    GridLayoutGroup _grid;
    RectTransform   _rt;
    Canvas          _canvas;
    CanvasScaler    _scaler;
    Cell[]          _cells;

    float RefPPU => (_scaler ? _scaler.referencePixelsPerUnit : 100f);
    float Px2World(float px) => px / Mathf.Max(1f, RefPPU);

    void Awake()
    {
        _grid   = GetComponent<GridLayoutGroup>();
        _rt     = GetComponent<RectTransform>();
        _canvas = GetComponentInParent<Canvas>();
        _scaler = _canvas ? _canvas.GetComponent<CanvasScaler>() : null;

        if (!container) container = _rt;
        if (!gridImage) gridImage = GetComponent<Image>(); // НЕ добавляем, только пытаемся найти

        // базовые настройки
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

    void OnEnable()                         { FitCells(); }
    void OnRectTransformDimensionsChange()  { FitCells(); }

    public void SetLevel(LevelData data)
    {
        level = data;
        Build();
        FitCells();
    }

    public void Build()
    {
        if (!level || cellPrefab == null) return;

        // очистка
        for (int i = transform.childCount - 1; i >= 0; --i)
            DestroyImmediate(transform.GetChild(i).gameObject);

        int N = Mathf.Max(1, level.size);
        _grid.constraintCount = N;
        _cells = new Cell[N * N];

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

            // старт: Lock → красный, иначе — тёмный (Unknown/Empty)
            var img = go.GetComponent<Image>();
            if (img)
            {
                if (SafeGet(level.locks, i) == 1) img.color = colLock;
                else img.color = colEmpty;
            }

            // цифра (только если clue>=0 и не lock)
            var tmp = go.GetComponentInChildren<TextMeshProUGUI>(true);
            if (tmp)
            {
                bool isLock  = (SafeGet(level.locks, i) == 1);
                int clue     = SafeGet(level.clues, i, -1);
                bool hasClue = (clue >= 0);
                if (!isLock && hasClue)
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
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(_rt);
    }

    public void FitCells()
    {
        if (!level) return;

        // применяем цвет сетки, если фон назначен
        if (gridImage) { gridImage.color = gridColor; gridImage.raycastTarget = false; }

        var host = container ? container : _rt;
        float W = Mathf.Max(0.0001f, host.rect.width);
        float H = Mathf.Max(0.0001f, host.rect.height);

        float pad     = Px2World(paddingPx);
        float gapWU   = Mathf.Max(Px2World(Mathf.Max(1f, spacingPx)), 0.0001f); // ≥1 px экрана
        float availW  = Mathf.Max(0, W - 2f * pad);
        float availH  = Mathf.Max(0, H - 2f * pad);

        int   N       = Mathf.Max(1, level.size);
        float sideX   = (availW - gapWU * (N - 1)) / N;
        float sideY   = (availH - gapWU * (N - 1)) / N;
        float side    = Mathf.Max(0.0001f, Mathf.Min(sideX, sideY));

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

    // Click API для виртуального курсора
    public bool TryClickAtScreen(Vector2 screenPos, Camera cam)
        => TryClickAtScreen(screenPos, cam, ClickKind.Left);

    public bool TryClickAtScreen(Vector2 screenPos, Camera cam, ClickKind kind)
    {
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_rt, screenPos, cam, out var local))
            return false;

        var rect = _rt.rect;
        float x0 = local.x - rect.xMin;
        float y0 = local.y - rect.yMin;
        float yTop = rect.height - y0; // startCorner = UpperLeft

        int N = level.size;
        var cell = _grid.cellSize;
        var sp   = _grid.spacing;

        int c = Mathf.FloorToInt( x0   / (cell.x + sp.x) );
        int r = Mathf.FloorToInt( yTop / (cell.y + sp.y) );
        if (c < 0 || c >= N || r < 0 || r >= N) return false;

        // отсечь «щели»
        float cx = c * (cell.x + sp.x);
        float cyTop = r * (cell.y + sp.y);
        if (x0 > cx + cell.x || yTop > cyTop + cell.y) return false;

        int idx = r * N + c;
        if (idx < 0 || idx >= transform.childCount) return false;

        var cellComp = (_cells != null && idx < _cells.Length) ? _cells[idx]
                         : transform.GetChild(idx).GetComponent<Cell>();
        if (!cellComp || cellComp.state == CellState.Lock) return false;

        var ctrl = cellComp.GetComponentInParent<PuzzleController>();
        if (!ctrl) return false;

        if (kind == ClickKind.Left)  ctrl.TryLeft (r, c, cellComp);
        else                         ctrl.TryRight(r, c, cellComp);

        return true;
    }

    // helpers
    static int SafeGet(int[] a, int i, int def = 0)
        => (a != null && i >= 0 && i < a.Length) ? a[i] : def;
}
