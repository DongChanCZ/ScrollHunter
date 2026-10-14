using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class WingedSwordMarkerChecks
{
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    [MenuItem("Tools/Scroll Hunter/Check Winged Sword Marker (Play)")]
    public static void Menu() => Debug.Log(Run());
    public static string Run()
    {
        if (!EditorApplication.isPlaying) throw new InvalidOperationException("Run during a battle.");
        var marker = UnityEngine.Object.FindFirstObjectByType<WingedSwordMarker>();
        if (marker == null) throw new InvalidOperationException("Select a living enemy first.");
        int passed = 0;
        Action<bool,string> check = (ok,label) => { if (!ok) throw new Exception(label); passed++; };
        var mesh = (Mesh)typeof(WingedSwordMarker).GetField("mesh",Flags).GetValue(marker);
        var mat = (Material)typeof(WingedSwordMarker).GetField("surface",Flags).GetValue(marker);
        var clock = typeof(WingedSwordMarker).GetField("clock",Flags);
        var render = typeof(WingedSwordMarker).GetMethod("Render",Flags);
        var image = marker.GetComponent<RawImage>();
        check(mesh != null && mesh.bounds.size.z > .09f, "Actual extruded 3D mesh");
        check(mesh.bounds.size.x < 2f && mesh.bounds.size.y < 2f, "No extrusion spikes");
        check(mesh.colors.Length == mesh.vertexCount && mesh.normals.Length == mesh.vertexCount, "Surface colors and normals");
        check(mat.shader.isSupported && !ShaderUtil.ShaderHasError(mat.shader), "Shader supported without errors");
        check(!image.raycastTarget && image.uvRect.height == 1f, "No click blocking or upside-down UV");
        check(!marker.transform.parent.Find("Emblem").gameObject.activeSelf
            && !marker.transform.parent.Find("Arrow").gameObject.activeSelf, "Legacy marker hidden");
        float previousClock = (float)clock.GetValue(marker);
        try
        {
            clock.SetValue(marker, 0f); render.Invoke(marker, null);
            var first = Pixels((RenderTexture)image.texture);
            clock.SetValue(marker, 1f); render.Invoke(marker, null);
            var second = Pixels((RenderTexture)image.texture);
            int visible = 0, bright = 0, changed = 0;
            for (int i=0;i<first.Length;i++)
            {
                if (first[i].a > 200) { visible++; if(first[i].r>170 && first[i].g>130) bright++; }
                if (!first[i].Equals(second[i])) changed++;
            }
            check(visible > 3000 && visible < first.Length/2, "Rendered shape with transparent background");
            check(bright > visible/3, "Readable surface brightness");
            check(changed > 500, "Tilt changes real geometry and lighting");
            check(first[0].a == 0 && first[first.Length-1].a == 0, "No opaque backdrop");
            var old = (RenderTexture)image.texture;
            marker.enabled = false;
            check(image.texture == null && !old.IsCreated(), "Disabled marker releases rendering resources");
            marker.enabled = true;
            check(image.texture is RenderTexture && ((RenderTexture)image.texture).IsCreated(), "Re-enable rebuilds render target");
        }
        finally { clock.SetValue(marker,previousClock); render.Invoke(marker,null); }
        return "Winged sword marker: "+passed+" checks passed";
    }

    static Color32[] Pixels(RenderTexture rt)
    {
        var previous = RenderTexture.active;
        var texture = new Texture2D(rt.width,rt.height,TextureFormat.RGBA32,false);
        try
        {
            RenderTexture.active = rt;
            texture.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0); texture.Apply();
            return texture.GetPixels32();
        }
        finally { RenderTexture.active = previous; UnityEngine.Object.DestroyImmediate(texture); }
    }
}
