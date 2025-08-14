using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Рисует прямоугольную рамку(и) для UI (CanvasRenderer) без спрайтов.
/// Две обводки: внешняя и внутренняя с зазором.
/// </summary>
[RequireComponent(typeof(CanvasRenderer))]
public class UICrtBorder : MaskableGraphic
{
    [Min(1)] public float thickness = 2f;
    [Min(0)] public float innerGap = 6f;
    public bool doubleBorder = true;
    public Color borderColorOuter = new(0.45f, 1f, 0.47f, 1f);
    public Color borderColorInner = new(0.24f, 0.63f, 0.27f, 1f);

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        DrawRect(vh, rectTransform.rect, thickness, borderColorOuter);

        if (doubleBorder)
        {
            var r = rectTransform.rect;
            r.xMin += innerGap; r.yMin += innerGap;
            r.xMax -= innerGap; r.yMax -= innerGap;
            DrawRect(vh, r, thickness, borderColorInner);
        }
    }

    private static void DrawRect(VertexHelper vh, Rect r, float t, Color col)
    {
        // 4 стороны как 4 тонких прямоугольника
        AddQuad(vh, new Rect(r.xMin, r.yMax - t, r.width, t), col);           // top
        AddQuad(vh, new Rect(r.xMin, r.yMin, r.width, t), col);                // bottom
        AddQuad(vh, new Rect(r.xMin, r.yMin, t, r.height), col);               // left
        AddQuad(vh, new Rect(r.xMax - t, r.yMin, t, r.height), col);           // right
    }

    private static void AddQuad(VertexHelper vh, Rect r, Color col)
    {
        int i = vh.currentVertCount;
        vh.AddVert(new Vector3(r.xMin, r.yMin), col, Vector2.zero);
        vh.AddVert(new Vector3(r.xMin, r.yMax), col, Vector2.zero);
        vh.AddVert(new Vector3(r.xMax, r.yMax), col, Vector2.zero);
        vh.AddVert(new Vector3(r.xMax, r.yMin), col, Vector2.zero);
        vh.AddTriangle(i, i+1, i+2);
        vh.AddTriangle(i, i+2, i+3);
    }
}
