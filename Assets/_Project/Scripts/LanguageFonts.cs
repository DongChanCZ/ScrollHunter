using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 기본 본문 폰트를 쓰는 글자를 현재 언어의 폰트로 바꾼다. 한국어·영어는 기존 폰트를 그대로 둔다.
/// 외곽선·그림자 재질은 같은 설정으로 언어 폰트 아틀라스에 옮기고, 실행 종료 시 공용 대체 폰트 순서를 되돌린다.
/// </summary>
public static class LanguageFonts
{
    private static LanguageFontSet set;
    private static readonly Dictionary<Material, Dictionary<TMP_FontAsset, Material>> variants = new Dictionary<Material, Dictionary<TMP_FontAsset, Material>>();
    private static readonly Dictionary<Material, Material> sources = new Dictionary<Material, Material>();
    private static readonly List<TMP_Text> pending = new List<TMP_Text>();
    private static List<TMP_FontAsset> savedFallbacks;

    public static TMP_FontAsset Current => set == null ? null : set.FontFor(GameText.Current);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        Shutdown();
        set = Resources.Load<LanguageFontSet>("LanguageFonts");
        if (set == null || set.BaseFont == null) return;
        var fallbacks = TMP_Settings.fallbackFontAssets;
        savedFallbacks = fallbacks != null ? new List<TMP_FontAsset>(fallbacks) : null;
        GameText.Changed += ApplyAll;
        TMPro_EventManager.TEXT_CHANGED_EVENT.Add(OnTextChanged);
        Canvas.preWillRenderCanvases += Flush;
        SceneManager.sceneLoaded += OnSceneLoaded;
        Application.quitting += Shutdown;
        ApplyAll();
    }

    private static void Shutdown()
    {
        GameText.Changed -= ApplyAll;
        TMPro_EventManager.TEXT_CHANGED_EVENT.Remove(OnTextChanged);
        Canvas.preWillRenderCanvases -= Flush;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        Application.quitting -= Shutdown;
        var fallbacks = TMP_Settings.fallbackFontAssets;
        if (savedFallbacks != null && fallbacks != null) { fallbacks.Clear(); fallbacks.AddRange(savedFallbacks); }
        savedFallbacks = null;
        pending.Clear();
        foreach (var variant in sources.Keys) if (variant != null) Object.Destroy(variant);
        variants.Clear(); sources.Clear();
        set = null;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => ApplyAll();

    /// <summary>씬의 모든 글자(비활성 포함)를 현재 언어 폰트로 맞춘다.</summary>
    public static void ApplyAll()
    {
        if (set == null) return;
        OrderFallbacks();
        var target = Current;
        foreach (var text in Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None)) Swap(text, target);
        pending.Clear();
    }

    // 새로 생성된 글자는 첫 생성 다음 화면 갱신 전에 바꾼다(생성 루프 안에서 바꾸지 않는다).
    private static void OnTextChanged(Object obj)
    {
        var text = obj as TMP_Text;
        if (text != null && set != null && text.font != Current && set.Manages(text.font)) pending.Add(text);
    }

    private static void Flush()
    {
        if (pending.Count == 0) return;
        var target = Current;
        var texts = pending.ToArray();
        pending.Clear();
        foreach (var text in texts) Swap(text, target);
    }

    private static void Swap(TMP_Text text, TMP_FontAsset target)
    {
        if (text == null || target == null || text.font == target || !set.Manages(text.font)) return;
        var material = text.fontSharedMaterial;
        text.font = target;
        text.fontSharedMaterial = MaterialFor(material, target);
    }

    private static Material MaterialFor(Material material, TMP_FontAsset target)
    {
        if (material == null) return target.material;
        var source = sources.TryGetValue(material, out var original) ? original : material;
        if (target == set.BaseFont) return set.IsLanguageDefaultMaterial(source) ? target.material : source;
        if (source == set.BaseFont.material || set.IsLanguageDefaultMaterial(source)) return target.material;
        if (!variants.TryGetValue(source, out var byFont)) variants[source] = byFont = new Dictionary<TMP_FontAsset, Material>();
        if (byFont.TryGetValue(target, out var variant) && variant != null) return variant;
        var reference = target.material;
        variant = new Material(source) { name = source.name + " (" + target.name + ")" };
        variant.SetTexture(ShaderUtilities.ID_MainTex, reference.GetTexture(ShaderUtilities.ID_MainTex));
        foreach (int id in new[] { ShaderUtilities.ID_GradientScale, ShaderUtilities.ID_TextureWidth, ShaderUtilities.ID_TextureHeight,
            ShaderUtilities.ID_WeightNormal, ShaderUtilities.ID_WeightBold })
            if (reference.HasProperty(id) && variant.HasProperty(id)) variant.SetFloat(id, reference.GetFloat(id));
        byFont[target] = variant; sources[variant] = source;
        return variant;
    }

    // 다른 폰트(피해 숫자 등)에서 빠진 글자는 현재 언어 폰트를 먼저 찾게 한다. 기존 대체 폰트의 앞 순서는 유지한다.
    private static void OrderFallbacks()
    {
        var fallbacks = TMP_Settings.fallbackFontAssets;
        if (savedFallbacks == null || fallbacks == null) return;
        fallbacks.Clear(); fallbacks.AddRange(savedFallbacks);
        var preferred = Current;
        if (preferred == set.BaseFont || !fallbacks.Remove(preferred)) return;
        int index = fallbacks.FindIndex(set.Manages);
        fallbacks.Insert(index < 0 ? fallbacks.Count : index, preferred);
    }
}
