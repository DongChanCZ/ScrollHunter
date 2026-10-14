using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

/// <summary>Renders a real beveled mesh into the existing target UI, independent of map lighting.</summary>
[RequireComponent(typeof(RawImage))]
public sealed class WingedSwordMarker : MonoBehaviour
{
    [SerializeField] private Mesh mesh;
    [SerializeField] private Material surface;
    [SerializeField, Range(0f, 25f)] private float tiltDegrees = 12f;
    [SerializeField, Min(1f)] private float tiltPeriod = 4f;
    private RawImage image;
    private RenderTexture texture;
    private CommandBuffer commands;
    private float clock;

    private void OnEnable()
    {
        image = GetComponent<RawImage>();
        image.raycastTarget = false;
        image.uvRect = new Rect(0f, 0f, 1f, 1f);
        if (mesh == null || surface == null) return;
        texture = new RenderTexture(256, 256, 24, RenderTextureFormat.ARGB32)
        {
            name = "Winged Sword Marker", antiAliasing = 4,
            filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };
        texture.Create();
        commands = new CommandBuffer { name = "Winged Sword Marker" };
        image.texture = texture;
        clock = 0f;
        Render();
    }

    private void LateUpdate()
    {
        // Presentation follows the other target UI and stays readable while paused.
        clock += Time.unscaledDeltaTime;
        Render();
    }

    private void Render()
    {
        if (commands == null || texture == null || !texture.IsCreated()) return;
        float wave = clock * Mathf.PI * 2f / Mathf.Max(1f, tiltPeriod);
        Quaternion rotation = Quaternion.Euler(7f + Mathf.Sin(wave * .5f) * 3f,
            Mathf.Sin(wave) * tiltDegrees, 0f);
        // About 5 UI pixels up/down inside the 100px marker; keeps the tutorial focus bounds fixed.
        float hover = Mathf.Sin(clock * Mathf.PI * 2f / 3f) * .117f;
        Matrix4x4 model = Matrix4x4.TRS(new Vector3(0f, hover, -4f), rotation, Vector3.one);
        Matrix4x4 projection = GL.GetGPUProjectionMatrix(Matrix4x4.Ortho(-1.17f, 1.17f, -1.17f, 1.17f, .1f, 10f), true);
        commands.Clear();
        commands.SetRenderTarget(texture);
        commands.ClearRenderTarget(true, true, Color.clear);
        commands.SetGlobalMatrix("_MarkerMVP", projection * model);
        commands.SetGlobalMatrix("_MarkerRotation", Matrix4x4.Rotate(rotation));
        commands.DrawMesh(mesh, Matrix4x4.identity, surface, 0, 0);
        commands.DrawMesh(mesh, Matrix4x4.identity, surface, 0, 1);
        var previous = RenderTexture.active;
        Graphics.ExecuteCommandBuffer(commands);
        RenderTexture.active = previous;
    }

    private void OnDisable()
    {
        if (image != null) image.texture = null;
        if (commands != null) { commands.Release(); commands = null; }
        if (texture != null)
        {
            texture.Release();
            Destroy(texture);
            texture = null;
        }
    }
}
