using UnityEngine;
using UnityEngine.UI;

/// <summary>중앙을 옅게 유지하는 화면 잔광. UI 기본 재질·메시만 써 웹에서도 추가 셰이더가 필요 없다.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public class CombatScreenOverlay : MaskableGraphic
{
    [SerializeField, Range(4, 32)] private int subdivisions = 16;
    [SerializeField, Range(0f, 1f)] private float centerOpacity = .06f;
    [SerializeField, Min(1f)] private float edgePower = 4f;
    [SerializeField, Min(1f)] private float borderWidth = 18f;
    private bool edgeOnly;
    public bool EdgeOnly { get => edgeOnly; set { if (edgeOnly == value) return; edgeOnly = value; SetVerticesDirty(); } }
    private float wave;
    public float Wave { get => wave; set { if (Mathf.Approximately(wave, value)) return; wave = value; SetVerticesDirty(); } }

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        var rect = rectTransform.rect;
        if (edgeOnly) { DrawBorder(mesh, rect); return; }
        int count = Mathf.Max(4, subdivisions);
        for (int y = 0; y <= count; y++) for (int x = 0; x <= count; x++)
        {
            float u = (float)x / count, v = (float)y / count;
            float edge = Mathf.Max(Mathf.Abs(u * 2f - 1f), Mathf.Abs(v * 2f - 1f));
            float haze = Mathf.Lerp(centerOpacity, 1f, Mathf.Pow(edge, edgePower));
            if (wave > 0f) haze *= .88f + .12f * Mathf.Sin((edge * 2f - wave) * Mathf.PI * 2f);
            var tint = color; tint.a *= haze;
            mesh.AddVert(new Vector3(rect.xMin + rect.width * u, rect.yMin + rect.height * v), tint, new Vector2(u, v));
        }
        for (int y = 0; y < count; y++) for (int x = 0; x < count; x++)
        {
            int i = y * (count + 1) + x;
            mesh.AddTriangle(i, i + count + 1, i + 1);
            mesh.AddTriangle(i + 1, i + count + 1, i + count + 2);
        }
    }
    private void DrawBorder(VertexHelper mesh, Rect rect)
    {
        // 얇은 가장자리만 그려서 방어도 흡수가 화면·카드의 밝기를 바꾸지 않는다.
        float width = borderWidth * rect.height / 1080f;
        for (int ring = 0; ring < 3; ring++)
        {
            float inset = ring == 0 ? 0f : ring == 1 ? width * .25f : width;
            var tint = color;
            tint.a *= ring == 0 ? .5f : ring == 1 ? 1f : 0f;
            mesh.AddVert(new Vector3(rect.xMin + inset, rect.yMin + inset), tint, Vector2.zero);
            mesh.AddVert(new Vector3(rect.xMin + inset, rect.yMax - inset), tint, Vector2.zero);
            mesh.AddVert(new Vector3(rect.xMax - inset, rect.yMax - inset), tint, Vector2.zero);
            mesh.AddVert(new Vector3(rect.xMax - inset, rect.yMin + inset), tint, Vector2.zero);
        }
        for (int ring = 0; ring < 2; ring++) for (int side = 0; side < 4; side++)
        {
            int a = ring * 4 + side, b = ring * 4 + (side + 1) % 4;
            mesh.AddTriangle(a, b, a + 4);
            mesh.AddTriangle(b, b + 4, a + 4);
        }
    }

}
