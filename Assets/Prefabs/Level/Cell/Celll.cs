using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class Cell : MonoBehaviour, IPointerClickHandler
{
    public int r, c;
    public CellState state = CellState.Unknown;
    public Image bg;
    PuzzleController controller;

    void Awake()
    {
        controller = GetComponentInParent<PuzzleController>();
        if (!bg) bg = GetComponent<Image>();
        if (bg) bg.raycastTarget = true;
    }

    public void Setup(int row, int col, CellState s, PuzzleController ctrl)
    {
        r=row; c=col; controller=ctrl; SetState(s, false);
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

    // ЛКМ: попытка закрасить; ПКМ: попытка пометить пусто
    public void OnPointerClick(PointerEventData ev)
    {
        if (state == CellState.Lock || controller == null) return;

        if (ev.button == PointerEventData.InputButton.Left)
        {
            controller.TryLeft(r, c, this);   // закрасить
        }
        else if (ev.button == PointerEventData.InputButton.Right)
        {
            controller.TryRight(r, c, this);  // пометить пусто
        }
    }
}
