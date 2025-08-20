using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.InputSystem;


public class Cell : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler
{
    public int r, c;
    public CellState state = CellState.Unknown;
    public Image bg;
    PuzzleController controller;

    // Статические флаги для зажатия кнопок
    private static bool isLeftHeld = false;
    private static bool isRightHeld = false;

    void Awake()
    {
        controller = GetComponentInParent<PuzzleController>();
        if (!bg) bg = GetComponent<Image>();
        if (bg) bg.raycastTarget = true;
    }

    public void Setup(int row, int col, CellState s, PuzzleController ctrl)
    {
        r = row; c = col; controller = ctrl; SetState(s, false);
    }

    public void SetState(CellState s, bool notify = true)
    {
        state = s;
        if (!controller || !bg) return;

        if      (s == CellState.Lock)  bg.color = controller.colLock;
        else if (s == CellState.Fill)  bg.color = controller.colFill;
        else if (s == CellState.Error) bg.color = controller.colError;
        else if (s == CellState.Empty) bg.color = controller.colEmpty;
        else                           bg.color = controller.colUnknown;

        if (notify) controller.OnCellChanged(r, c);
    }

    public void OnPointerClick(PointerEventData ev)
    {
        if (state == CellState.Lock || controller == null) return;

        if (ev.button == PointerEventData.InputButton.Left)
        {
            isLeftHeld = false; // сбросим, чтобы клик не считался как drag
            controller.TryLeft(r, c, this);
        }
        else if (ev.button == PointerEventData.InputButton.Right)
        {
            isRightHeld = false;
            controller.TryRight(r, c, this);
        }
    }

    public void OnPointerEnter(PointerEventData ev)
    {
        if (state == CellState.Lock || controller == null) return;

        if (isLeftHeld)
        {
            controller.TryLeft(r, c, this);
        }
        else if (isRightHeld)
        {
            controller.TryRight(r, c, this);
        }
    }

    void Update()
    {
        var mouse = Mouse.current;
        if (mouse != null)
        {
            if (mouse.leftButton.wasPressedThisFrame)   isLeftHeld  = true;
            if (mouse.leftButton.wasReleasedThisFrame)  isLeftHeld  = false;

            if (mouse.rightButton.wasPressedThisFrame)  isRightHeld = true;
            if (mouse.rightButton.wasReleasedThisFrame) isRightHeld = false;
        }
    }
}
