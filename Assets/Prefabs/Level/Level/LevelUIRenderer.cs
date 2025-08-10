using UnityEngine;
using UnityEngine.UI;
using TMPro;

[RequireComponent(typeof(GridLayoutGroup), typeof(RectTransform))]
public class LevelUIRenderer : MonoBehaviour
{
    public LevelData level;
    public GameObject cellPrefab;

    [Header("Layout")]
    public int padding = 8;
    public int spacing = 2;

    [Header("Colors")]
    public Color colEmpty = Color.black;
    public Color colFill  = new Color(0f, 1f, 0.2f);
    public Color colLock  = new Color(0.8f, 0.1f, 0.2f);
    public Color colClueText = Color.black;

    [Header("Clue text policy")]
    public bool removeTextOnLock = true;
    public bool removeTextWhenHidden = true;

    [Header("Container")]
    public RectTransform container;      // <- куда ВПИСЫВАТЬ уровень
    public bool matchContainer = true;   // <- подогнать свой RT под контейнер (якорями)

    GridLayoutGroup grid;
    RectTransform rt;

    void Awake()
    {
        grid = GetComponent<GridLayoutGroup>();
        rt   = GetComponent<RectTransform>();

        if (!level || level.size <= 0) { Debug.LogError("LevelUIRenderer: Level is null or size <= 0"); enabled=false; return; }
        if (!container) container = rt.parent as RectTransform;

        // Живём строго внутри контейнера: подгоняем якоря только если просили
        if (matchContainer && container)
        {
            // если висим не под контейнером — перевесим
            if (rt.parent != container) rt.SetParent(container, worldPositionStays:false);

            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot     = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(padding, padding);
            rt.offsetMax = new Vector2(-padding, -padding);
        }

        int N = level.size;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = N;
        grid.childAlignment = TextAnchor.UpperLeft;
        grid.spacing = new Vector2(spacing, spacing);
        grid.padding = new RectOffset(0, 0, 0, 0);

        Build();
    }

    void Start()  => FitCells();
    void OnEnable()=> FitCells();
    void OnRectTransformDimensionsChange()=> FitCells();

    void Build()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
#if UNITY_EDITOR
            if (!Application.isPlaying) DestroyImmediate(transform.GetChild(i).gameObject); else Destroy(transform.GetChild(i).gameObject);
#else
            Destroy(transform.GetChild(i).gameObject);
#endif

        int N = level.size;
        int total = N * N;

        for (int r = 0; r < N; r++)
        for (int c = 0; c < N; c++)
        {
            var go = Instantiate(cellPrefab, transform);
            go.SetActive(true);

            var img = go.GetComponent<Image>() ?? go.AddComponent<Image>();
            int i = r * N + c;

            bool isLock = (level.locks != null && i < level.locks.Length && level.locks[i] == 1);
            bool isFill = (!isLock && level.target != null && i < level.target.Length && level.target[i] == 1);

            img.color = isLock ? colLock : (isFill ? colFill : colEmpty);

            var tmp = go.GetComponentInChildren<TextMeshProUGUI>(true);
            int clue = (level.clues != null && level.clues.Length == total) ? level.clues[i] : -1;
            bool showClue = (!isLock && clue >= 0);

            if (tmp)
            {
                if (showClue)
                {
                    tmp.text = clue.ToString();
                    tmp.color = colClueText;
                    tmp.enableAutoSizing = true;
                    tmp.fontSizeMin = 8; tmp.fontSizeMax = 200;
                    tmp.alignment = TextAlignmentOptions.Center;
                    tmp.raycastTarget = false;
                    tmp.gameObject.SetActive(true);
                }
                else
                {
#if UNITY_EDITOR
                    if ((removeTextOnLock && isLock) || (removeTextWhenHidden && !showClue))
                        if (!Application.isPlaying) DestroyImmediate(tmp.gameObject); else Destroy(tmp.gameObject);
                    else tmp.gameObject.SetActive(false);
#else
                    if ((removeTextOnLock && isLock) || (removeTextWhenHidden && !showClue)) Destroy(tmp.gameObject);
                    else tmp.gameObject.SetActive(false);
#endif
                }
            }
        }

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
    }

    void FitCells()
    {
        if (!grid || !rt || !level || level.size <= 0) return;

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(rt);

        // размеры берем именно у НАШЕГО RT (который сидит внутри контейнера)
        float w = Mathf.Max(0, rt.rect.width);
        float h = Mathf.Max(0, rt.rect.height);

        int N = level.size;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = N;

        float freeW = w - (N - 1) * grid.spacing.x;
        float freeH = h - (N - 1) * grid.spacing.y;

        float side = Mathf.Floor(Mathf.Max(1f, Mathf.Min(freeW / N, freeH / N)));
        grid.cellSize = new Vector2(side, side);

        float usedW = N * side + (N - 1) * grid.spacing.x;
        float usedH = N * side + (N - 1) * grid.spacing.y;

        int padL = Mathf.Max(0, Mathf.FloorToInt((w - usedW) * 0.5f));
        int padR = Mathf.Max(0, Mathf.CeilToInt ((w - usedW) * 0.5f));
        int padT = Mathf.Max(0, Mathf.CeilToInt ((h - usedH) * 0.5f));
        int padB = Mathf.Max(0, Mathf.FloorToInt((h - usedH) * 0.5f));
        grid.padding = new RectOffset(padL, padR, padT, padB);
    }
}
