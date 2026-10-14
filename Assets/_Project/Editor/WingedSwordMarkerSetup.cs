using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class WingedSwordMarkerSetup
{
    public const string Folder = "Assets/_Project/UI/WingedSwordMarker";
    [Serializable] public class MeshData
    {
        public Vector3[] vertices, normals;
        public Color[] colors;
        public int[] triangles;
    }

    public static void ImportMesh(string jsonPath)
    {
        var data = JsonUtility.FromJson<MeshData>(System.IO.File.ReadAllText(jsonPath));
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(Folder + "/WingedSword.asset");
        if (mesh == null)
        {
            mesh = new Mesh { name = "WingedSword" };
            AssetDatabase.CreateAsset(mesh, Folder + "/WingedSword.asset");
        }
        mesh.Clear();
        mesh.vertices = data.vertices;
        mesh.normals = data.normals;
        mesh.colors = data.colors;
        mesh.triangles = data.triangles;
        mesh.RecalculateBounds();
        EditorUtility.SetDirty(mesh);
    }

    [MenuItem("Tools/Scroll Hunter/Apply Winged Sword Marker")]
    public static void Apply()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play first.");
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.name != "Battle") throw new InvalidOperationException("Open Battle scene.");
        var ui = new SerializedObject(UnityEngine.Object.FindFirstObjectByType<CombatUI>());
        var marker = (RectTransform)ui.FindProperty("targetMarker").objectReferenceValue;
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(Folder + "/WingedSword.asset");
        var shader = Shader.Find("ScrollHunter/Winged Sword Marker");
        if (mesh == null || shader == null) throw new InvalidOperationException("Missing marker mesh or shader.");
        var material = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/WingedSword.mat");
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, Folder + "/WingedSword.mat");
        }
        var child = marker.Find("WingedSword3D");
        if (child == null)
            child = new GameObject("WingedSword3D", typeof(RectTransform), typeof(RawImage)).transform;
        child.SetParent(marker, false);
        var rect = (RectTransform)child;
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one; rect.localRotation = Quaternion.identity;
        child.GetComponent<RawImage>().raycastTarget = false;
        var renderer = child.GetComponent<WingedSwordMarker>();
        if (renderer == null) renderer = child.gameObject.AddComponent<WingedSwordMarker>();
        var data = new SerializedObject(renderer);
        data.FindProperty("mesh").objectReferenceValue = mesh;
        data.FindProperty("surface").objectReferenceValue = material;
        data.ApplyModifiedPropertiesWithoutUndo();
        foreach (string name in new[] { "Emblem", "Arrow" })
        {
            var old = marker.Find(name);
            if (old != null) old.gameObject.SetActive(false);
        }
        marker.sizeDelta = new Vector2(100f, 100f);
        ui.FindProperty("markerScreenOffsetY").floatValue = 112f;
        ui.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(scene);
        AssetDatabase.SaveAssets();
    }
}
