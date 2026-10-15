using TMPro;
using UnityEngine;

/// <summary>언어별 본문 폰트. 비어 있는 칸은 기본 폰트를 그대로 쓴다. Resources/LanguageFonts에 둔다.</summary>
[CreateAssetMenu(menuName = "Scroll Hunter/Language Font Set")]
public class LanguageFontSet : ScriptableObject
{
    [SerializeField] private TMP_FontAsset baseFont;
    [Tooltip("GameLanguage 순서(한국어, English, 日本語, 简体中文, Español)")]
    [SerializeField] private TMP_FontAsset[] languageFonts = new TMP_FontAsset[5];

    public TMP_FontAsset BaseFont => baseFont;
    public TMP_FontAsset FontFor(GameLanguage language)
    {
        int index = (int)language;
        var font = index >= 0 && index < languageFonts.Length ? languageFonts[index] : null;
        return font != null ? font : baseFont;
    }
    public bool Manages(TMP_FontAsset font)
    {
        if (font == null) return false;
        if (font == baseFont) return true;
        foreach (var candidate in languageFonts) if (candidate == font) return true;
        return false;
    }
    public bool IsLanguageDefaultMaterial(Material material)
    {
        foreach (var candidate in languageFonts) if (candidate != null && candidate.material == material) return true;
        return false;
    }
}
