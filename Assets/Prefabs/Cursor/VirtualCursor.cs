using UnityEngine;
using UnityEngine.InputSystem;

public class VirtualCursor : MonoBehaviour
{
    [Header("Refs")]
    public RectTransform safeZone;    // область пазла
    public RectTransform cursorRT;    // иконка курсора
    public LevelUIRenderer grid;
    public Camera uiCamera;

    [Header("Tuning")]
    public float sensitivity = 1f;

    [Tooltip("Нормализованные координаты горячей точки в пределах Rect курсора. (0,0)=лево-низ, (1,1)=право-верх")]
    public Vector2 hotspotNormalized = new Vector2(0f, 1f); // левый-верх по умолчанию
    [Tooltip("Точная подстройка горячей точки, в пикселях экрана")]
    public Vector2 pixelOffset = Vector2.zero;
    [Tooltip("Клампить позицию так, чтобы hotspot всегда оставался внутри SafeZone")]
    public bool clampByHotspot = true;

    Vector2 _screenPos; // экранные координаты центра курсора

    void OnEnable()
    {
        if (!uiCamera) uiCamera = Camera.main;
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;

        // старт из центра SafeZone
        var corners = new Vector3[4];
        safeZone.GetWorldCorners(corners);
        var min = RectTransformUtility.WorldToScreenPoint(uiCamera, corners[0]);
        var max = RectTransformUtility.WorldToScreenPoint(uiCamera, corners[2]);
        _screenPos = (min + max) * 0.5f;

        UpdateCursorRT();
    }

    void OnDisable()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void Update()
    {
        var mouse = Mouse.current;
        if (mouse == null) return;

        _screenPos += mouse.delta.ReadValue() * sensitivity;

        // Кламп по hotspot
        var corners = new Vector3[4];
        safeZone.GetWorldCorners(corners);
        var min = RectTransformUtility.WorldToScreenPoint(uiCamera, corners[0]);
        var max = RectTransformUtility.WorldToScreenPoint(uiCamera, corners[2]);

        if (clampByHotspot && cursorRT)
        {
            UpdateCursorRT(); // чтобы GetHotspotScreen посчитал актуально
            var hs = GetHotspotScreen();
            var dx = 0f; var dy = 0f;
            if (hs.x < min.x) dx = min.x - hs.x;
            else if (hs.x > max.x) dx = max.x - hs.x;
            if (hs.y < min.y) dy = min.y - hs.y;
            else if (hs.y > max.y) dy = max.y - hs.y;
            _screenPos += new Vector2(dx, dy);
        }
        else
        {
            _screenPos.x = Mathf.Clamp(_screenPos.x, min.x, max.x);
            _screenPos.y = Mathf.Clamp(_screenPos.y, min.y, max.y);
        }

        UpdateCursorRT();

        // Клики из точки hotspot
        var clickPos = GetHotspotScreen();

        if (mouse.leftButton.wasPressedThisFrame)
            grid.TryClickAtScreen(clickPos, uiCamera, ClickKind.Left);

        if (mouse.rightButton.wasPressedThisFrame)
            grid.TryClickAtScreen(clickPos, uiCamera, ClickKind.Right);
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
