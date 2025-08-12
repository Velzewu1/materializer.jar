using UnityEngine;
using UnityEngine.InputSystem;

public class VirtualCursor : MonoBehaviour
{
    [Header("Refs")]
    public RectTransform safeZone;    // область пазла (RectTransform контейнера)
    public RectTransform cursorRT;    // иконка курсора (UI)
    public LevelUIRenderer grid;      // рендерер сетки
    public Camera uiCamera;

    [Header("Tuning")]
    public float sensitivity = 1f;

    [Tooltip("Нормализованные координаты hot-spot внутри Rect курсора: (0,0)=лево-низ, (1,1)=право-верх")]
    public Vector2 hotspotNormalized = new Vector2(0f, 1f); // кончик слева-сверху
    [Tooltip("Точная подстройка hot-spot в пикселях экрана")]
    public Vector2 pixelOffset = Vector2.zero;
    [Tooltip("Держать hot-spot внутри SafeZone")]
    public bool clampByHotspot = true;

    Vector2 _screenPos;        // экранные координаты центра курсора
    int _lastIdxLeft  = -1;    // последняя обработанная клетка (ЛКМ)
    int _lastIdxRight = -1;    // последняя обработанная клетка (ПКМ)

    void OnEnable()
    {
        if (!uiCamera) uiCamera = Camera.main;
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;

        // старт из центра SafeZone
        var corners = new Vector3[4];
        safeZone.GetWorldCorners(corners);
        var min = RectTransformUtility.WorldToScreenPoint(uiCamera, corners[0]); // bottom-left
        var max = RectTransformUtility.WorldToScreenPoint(uiCamera, corners[2]); // top-right
        _screenPos = (min + max) * 0.5f;

        UpdateCursorRT();
    }

    void OnDisable()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        if (grid) grid.ClearHighlight(); // убрать подсветку при отключении
    }

    void Update()
    {
        var mouse = Mouse.current;
        if (mouse == null || grid == null || uiCamera == null || safeZone == null) return;

        // движение
        _screenPos += mouse.delta.ReadValue() * sensitivity;

        // кламп по hot-spot
        var corners = new Vector3[4];
        safeZone.GetWorldCorners(corners);
        var min = RectTransformUtility.WorldToScreenPoint(uiCamera, corners[0]);
        var max = RectTransformUtility.WorldToScreenPoint(uiCamera, corners[2]);

        if (clampByHotspot && cursorRT)
        {
            UpdateCursorRT(); // позиция курсора актуальна для подсчёта hot-spot
            var hs = GetHotspotScreen();
            var dx = 0f; var dy = 0f;
            if (hs.x < min.x) dx = min.x - hs.x; else if (hs.x > max.x) dx = max.x - hs.x;
            if (hs.y < min.y) dy = min.y - hs.y; else if (hs.y > max.y) dy = max.y - hs.y;
            _screenPos += new Vector2(dx, dy);
        }
        else
        {
            _screenPos.x = Mathf.Clamp(_screenPos.x, min.x, max.x);
            _screenPos.y = Mathf.Clamp(_screenPos.y, min.y, max.y);
        }

        UpdateCursorRT();

        // текущая точка «клика» — hot-spot
        var pos = GetHotspotScreen();

        // --- Подсветка 3×3: отключаем на время удержания любой кнопки ---
        bool anyHeld = mouse.leftButton.isPressed || mouse.rightButton.isPressed;
        grid.SetHoverSuppressed(anyHeld);
        if (!anyHeld)
            grid.HighlightAtScreen(pos, uiCamera);
        else
            grid.ClearHighlight();

        // hit-test индекса клетки
        if (grid.TryGetCellIndexAtScreen(pos, uiCamera, out int idx, out int r, out int c))
        {
            // ЛКМ: одиночный клик + рисование при удержании (по смене idx)
            if (mouse.leftButton.wasPressedThisFrame)
            {
                grid.TryClickAtScreen(pos, uiCamera, ClickKind.Left);
                _lastIdxLeft = idx;
            }
            else if (mouse.leftButton.isPressed && idx != _lastIdxLeft)
            {
                grid.TryClickAtScreen(pos, uiCamera, ClickKind.Left);
                _lastIdxLeft = idx;
            }

            // ПКМ: одиночный клик + рисование при удержании (по смене idx)
            if (mouse.rightButton.wasPressedThisFrame)
            {
                grid.TryClickAtScreen(pos, uiCamera, ClickKind.Right);
                _lastIdxRight = idx;
            }
            else if (mouse.rightButton.isPressed && idx != _lastIdxRight)
            {
                grid.TryClickAtScreen(pos, uiCamera, ClickKind.Right);
                _lastIdxRight = idx;
            }
        }

        // сбросы
        if (mouse.leftButton.wasReleasedThisFrame)  _lastIdxLeft = -1;
        if (mouse.rightButton.wasReleasedThisFrame) _lastIdxRight = -1;
    }

    void UpdateCursorRT()
    {
        if (!cursorRT) return;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(safeZone, _screenPos, uiCamera, out var local))
            return;
        cursorRT.anchoredPosition = local;
    }

    // Экранная позиция «горячей точки»
    Vector2 GetHotspotScreen()
    {
        if (!cursorRT) return _screenPos;

        var rect = cursorRT.rect;
        var localFromCenter = new Vector2(
            (hotspotNormalized.x - 0.5f) * rect.width,
            (hotspotNormalized.y - 0.5f) * rect.height
        );

        var world = cursorRT.TransformPoint(localFromCenter);
        var screen = RectTransformUtility.WorldToScreenPoint(uiCamera, world);
        return screen + pixelOffset;
    }
}
