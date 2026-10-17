using UnityEngine;
using UnityEngine.UI;

// 손패 코스트 숫자를 담는 보석. 텍스처 없이 같은 도형으로 그린다.
[DisallowMultipleComponent]
[RequireComponent(typeof(CanvasRenderer))]
public class CostGemGraphic : MaskableGraphic
{
    [SerializeField] private Color rimColor = new Color32(10, 24, 43, 255);
    [SerializeField] private Color edgeColor = new Color32(126, 193, 237, 255);
    [SerializeField] private Color lightFacet = new Color32(65, 145, 212, 255);
    [SerializeField] private Color darkFacet = new Color32(17, 55, 111, 255);
    [SerializeField] private Color faceColor = new Color32(19, 59, 112, 255);
    [SerializeField, Range(0f, 1f)] private float edgeScale = .94f;
    [SerializeField, Range(0f, 1f)] private float facetScale = .87f;
    [SerializeField, Range(0f, 1f)] private float faceScale = .66f;

    private static readonly Vector2[] Shape = {
        new Vector2(0f, 1f), new Vector2(.72f, .65f), new Vector2(1f, 0f),
        new Vector2(.72f, -.65f), new Vector2(0f, -1f),
        new Vector2(-.72f, -.65f), new Vector2(-1f, 0f), new Vector2(-.72f, .65f)
    };

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        var rect = GetPixelAdjustedRect();
        var half = rect.size * .5f;
        var center = rect.center;
        for (int i = 0; i < Shape.Length; i++)
        {
            var a = Vector2.Scale(Shape[i], half);
            var b = Vector2.Scale(Shape[(i + 1) % Shape.Length], half);
            AddQuad(mesh, center + a, center + b, center + b * edgeScale, center + a * edgeScale, rimColor);
            AddQuad(mesh, center + a * edgeScale, center + b * edgeScale,
                center + b * facetScale, center + a * facetScale, edgeColor);
            var facet = Vector2.Dot((Shape[i] + Shape[(i + 1) % Shape.Length]) * .5f,
                new Vector2(-.5f, .5f)) * .5f + .5f;
            AddQuad(mesh, center + a * facetScale, center + b * facetScale,
                center + b * faceScale, center + a * faceScale, Color.Lerp(darkFacet, lightFacet, facet));
            int start = mesh.currentVertCount;
            mesh.AddVert(center, faceColor * color, Vector2.zero);
            mesh.AddVert(center + a * faceScale, faceColor * color, Vector2.zero);
            mesh.AddVert(center + b * faceScale, faceColor * color, Vector2.zero);
            mesh.AddTriangle(start, start + 1, start + 2);
        }
    }

    private void AddQuad(VertexHelper mesh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color tint)
    {
        int start = mesh.currentVertCount;
        tint *= color;
        mesh.AddVert(a, tint, Vector2.zero); mesh.AddVert(b, tint, Vector2.zero);
        mesh.AddVert(c, tint, Vector2.zero); mesh.AddVert(d, tint, Vector2.zero);
        mesh.AddTriangle(start, start + 1, start + 2);
        mesh.AddTriangle(start, start + 2, start + 3);
    }
}
