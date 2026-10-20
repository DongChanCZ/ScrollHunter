using System;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.UI;

/// <summary>SK15 전용 룬 방패. 전투 판정은 기존 ShieldApplied/selfEffect 경로를 사용한다.</summary>
public static class ArcaneBarrierVfxSetup
{
    const string Folder = "Assets/_Project/UI/ArcaneBarrier";
    const string SkillPath = "Assets/_Project/Data/ScrollHunter/SkillData/Skill_SK15_ArcaneBarrier.asset";
    static readonly Color Gold = new Color(1f, .73f, .24f);
    static readonly Color Ivory = new Color(1f, .94f, .72f);

    [MenuItem("Tools/Scroll Hunter/Apply Arcane Barrier VFX")]
    public static void Apply()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play before applying.");
        var vfx = UnityEngine.Object.FindFirstObjectByType<SkillVfx>();
        var skill = AssetDatabase.LoadAssetAtPath<SkillData>(SkillPath);
        if (vfx == null || skill == null) throw new InvalidOperationException("Open Battle scene.");
        var serialized = new SerializedObject(vfx);
        var entries = serialized.FindProperty("entries");
        SerializedProperty entry = null;
        for (int i = 0; i < entries.arraySize; i++)
            if (entries.GetArrayElementAtIndex(i).FindPropertyRelative("skill").objectReferenceValue == skill)
                entry = entries.GetArrayElementAtIndex(i);
        if (entry == null) throw new InvalidOperationException("SK15 VFX entry missing.");
        var previous = (GameObject)entry.FindPropertyRelative("selfEffect").objectReferenceValue;
        Transform parent = previous.transform.parent;
        var armor = parent.Find("MagicArmorVfx");
        int sibling = armor != null ? armor.GetSiblingIndex() : previous.transform.GetSiblingIndex();
        var existing = parent.Find("ArcaneBarrierVfx");
        if (existing != null) UnityEngine.Object.DestroyImmediate(existing.gameObject);

        // 같은 원화로 알파·아이콘을 미리 만들어 실행 중 이미지 처리를 피한다.
        var source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        source.LoadImage(File.ReadAllBytes(Folder + "/circle5a-source.png"));
        int size = source.width;
        var outer = new Color[size * size];
        var inner = new Color[size * size];
        var membrane = new Color[size * size];
        var icon = new Color[size * size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            int index = y * size + x;
            float px = (x + .5f) / size - .5f, py = (y + .5f) / size - .5f;
            float radius = Mathf.Sqrt(px * px + py * py);
            Color original = source.GetPixel(x, y);
            float ink = Mathf.Pow(Mathf.Max(original.r, original.g, original.b), 1.25f);
            Color gold = Color.Lerp(Gold, Ivory, SmoothBand(.25f, 1f, original.g));
            float ringMask = SmoothBand(.355f, .375f, radius);
            outer[index] = new Color(gold.r, gold.g, gold.b, ink * ringMask);
            inner[index] = new Color(gold.r, gold.g, gold.b, ink * (1f - ringMask));
            float disc = (1f - SmoothBand(.34f, .405f, radius))
                * (.035f + .095f * Mathf.Clamp01(radius / .4f));
            membrane[index] = new Color(Gold.r, Gold.g, Gold.b, disc);
            float haze = Mathf.Exp(-radius * radius * 9f);
            Color backdrop = Color.Lerp(new Color(.025f, .035f, .065f), new Color(.16f, .105f, .035f), haze);
            icon[index] = Color.Lerp(backdrop, gold, ink);
            icon[index].a = 1f;
        }
        Sprite outerSprite = SaveSprite("RuneOuter", size, outer);
        Sprite innerSprite = SaveSprite("RuneInner", size, inner);
        Sprite membraneSprite = SaveSprite("Membrane", size, membrane);
        Sprite iconSprite = SaveSprite("ArcaneBarrierIcon", size, icon);
        UnityEngine.Object.DestroyImmediate(source);

        var root = new GameObject("ArcaneBarrierVfx", typeof(RectTransform), typeof(Animator));
        root.SetActive(false);
        var rect = (RectTransform)root.transform;
        rect.SetParent(parent, false);
        rect.SetSiblingIndex(sibling + 1);
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
        rect.anchoredPosition = new Vector2(0f, 45f);
        rect.sizeDelta = new Vector2(600f, 600f);
        Image film = Layer(rect, "Membrane", membraneSprite);
        Image center = Layer(rect, "Inner", innerSprite);
        Image rim = Layer(rect, "Outer", outerSprite);
        rim.type = Image.Type.Filled;
        rim.fillMethod = Image.FillMethod.Radial360;
        rim.fillOrigin = (int)Image.Origin360.Top;
        rim.fillClockwise = true;

        string clipPath = Folder + "/ArcaneBarrier.anim";
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
        if (clip == null) { clip = new AnimationClip(); AssetDatabase.CreateAsset(clip, clipPath); }
        clip.ClearCurves();
        clip.frameRate = 60f;
        foreach (string layer in new[] { "Membrane", "Inner", "Outer" })
        {
            Curve(clip, layer, typeof(RectTransform), "m_LocalScale.x", 0f, .74f, .22f, 1f, .65f, 1.015f, 1.08f, 1.035f);
            Curve(clip, layer, typeof(RectTransform), "m_LocalScale.y", 0f, .74f, .22f, 1f, .65f, 1.015f, 1.08f, 1.035f);
        }
        Curve(clip, "Outer", typeof(Image), "m_FillAmount", 0f, .02f, .24f, 1f, 1.08f, 1f);
        Curve(clip, "Outer", typeof(Image), "m_Color.a", 0f, 0f, .06f, .78f, .24f, .88f, .55f, .76f, 1.08f, 0f);
        Curve(clip, "Inner", typeof(Image), "m_Color.a", 0f, 0f, .1f, 0f, .28f, .32f, .55f, .27f, 1.02f, 0f);
        Curve(clip, "Membrane", typeof(Image), "m_Color.a", 0f, 0f, .14f, 0f, .3f, 1f, .6f, .8f, 1.08f, 0f);
        var clipSettings = AnimationUtility.GetAnimationClipSettings(clip);
        clipSettings.loopTime = false;
        AnimationUtility.SetAnimationClipSettings(clip, clipSettings);
        EditorUtility.SetDirty(clip);
        AssetDatabase.SaveAssetIfDirty(clip);
        string controllerPath = Folder + "/ArcaneBarrier.controller";
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
        var machine = controller.layers[0].stateMachine;
        var state = machine.defaultState;
        if (state == null) { state = machine.AddState("Form shield"); machine.defaultState = state; }
        state.motion = clip;
        EditorUtility.SetDirty(controller);
        EditorUtility.SetDirty(state);
        AssetDatabase.SaveAssetIfDirty(controller);
        var animator = root.GetComponent<Animator>();
        animator.runtimeAnimatorController = controller;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        entry.FindPropertyRelative("selfEffect").objectReferenceValue = root;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        var skillObject = new SerializedObject(skill);
        skillObject.FindProperty("icon").objectReferenceValue = iconSprite;
        skillObject.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssetIfDirty(skill);
        PrefabUtility.SaveAsPrefabAsset(root, Folder + "/ArcaneBarrierVfx.prefab");
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(root.scene);
    }

    static Sprite SaveSprite(string name, int size, Color[] pixels)
    {
        if (name != "ArcaneBarrierIcon" &&
            (pixels[0].a > .001f || pixels[size - 1].a > .001f || pixels[pixels.Length - 1].a > .001f))
            throw new InvalidOperationException(name + ": texture corners must be transparent.");
        string path = Folder + "/" + name + ".png";
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.SetPixels(pixels); texture.Apply();
        File.WriteAllBytes(path, texture.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    static float SmoothBand(float from, float to, float value)
        => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(from, to, value));

    static Image Layer(RectTransform parent, string name, Sprite sprite)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        var image = go.GetComponent<Image>();
        image.sprite = sprite; image.raycastTarget = false;
        image.color = new Color(1f, 1f, 1f, 0f);
        return image;
    }

    static void Curve(AnimationClip clip, string path, Type type, string property, params float[] values)
    {
        var curve = new AnimationCurve();
        for (int i = 0; i < values.Length; i += 2) curve.AddKey(new Keyframe(values[i], values[i + 1]));
        for (int i = 0; i < curve.length; i++)
        {
            AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto);
            AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto);
        }
        AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, type, property), curve);
    }
}
