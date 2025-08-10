using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class CanvasCursor : MonoBehaviour
{
    [Header("Refs")]
    public Canvas canvas;            // Screen Space - Camera
    public Camera uiCamera;          // та же камера, что в Canvas.RenderCamera
    public RectTransform cursorRT;   // Image курсора (RaycastTarget = false)

    [Header("Behaviour")]
    public bool hideSystemCursor = true;
    public bool confineToCanvas = true;
    public bool smooth = false;
    public float smoothTime = 0.03f;

    [Header("Debug (optional)")]
    public Text debugLabel;          // любой UI Text/TMP для вывода координат (необязательно)

    RectTransform canvasRT;
    Vector2 vel;

    void Awake()
    {
        if (!canvas) canvas = GetComponentInParent<Canvas>();
        canvasRT = canvas.GetComponent<RectTransform>();
        if (!uiCamera) uiCamera = canvas.worldCamera;

        // страховка по якорям и Raycast
        if (cursorRT)
        {
            cursorRT.anchorMin = cursorRT.anchorMax = new Vector2(0.5f, 0.5f);
            cursorRT.pivot     = new Vector2(0.5f, 0.5f);
            var img = cursorRT.GetComponent<Image>();
            if (img) img.raycastTarget = false;
            // курсор — прямой ребёнок Canvas
            if (cursorRT.parent != canvasRT) cursorRT.SetParent(canvasRT, worldPositionStays: false);
        }
    }

    void OnEnable()
    {
        if (hideSystemCursor)
        {
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Confined; // важно для оконного режима
        }
    }

    void OnDisable()
    {
        if (hideSystemCursor)
        {
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }
    }

    void Update()
    {
        if (!canvasRT || !uiCamera || !cursorRT) return;

        // 1) берём экранные координаты курсора
        Vector2 screenPos;
        #if ENABLE_INPUT_SYSTEM
        screenPos = Mouse.current != null ? Mouse.current.position.ReadValue() : (Vector2)Input.mousePosition;
        #else
        screenPos = Input.mousePosition;
        #endif

        // 2) конверсия: Screen -> local в системе Canvas
        Vector2 local;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRT, screenPos, uiCamera, out local);

        // 3) ограничение внутри прямоугольника Canvas (по желанию)
        if (confineToCanvas)
        {
            var r = canvasRT.rect; // координаты с центром в pivot
            local.x = Mathf.Clamp(local.x, r.xMin, r.xMax);
            local.y = Mathf.Clamp(local.y, r.yMin, r.yMax);
        }

        // 4) обновляем позицию курсора
        if (smooth)
            cursorRT.anchoredPosition = Vector2.SmoothDamp(cursorRT.anchoredPosition, local, ref vel, smoothTime);
        else
            cursorRT.anchoredPosition = local;

        // 5) отладка (видно, что читаем именно экранные пиксели и локальные координаты Canvas)
        if (debugLabel)
            debugLabel.text = $"Screen: {screenPos.x:0},{screenPos.y:0}\nLocal: {local.x:0.0},{local.y:0.0}";
    }
}
