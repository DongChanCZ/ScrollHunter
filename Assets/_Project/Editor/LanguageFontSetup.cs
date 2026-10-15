using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

/// <summary>
/// GameText.json의 일본어·중국어·스페인어 글자만 담은 정적 SDF 폰트를 만든다(웹에서 원본 OTF 미포함·실행 중 생성 없음).
/// 줄 높이·기준선은 기존 본문 폰트 비율에 맞춰 같은 상자에서 높이가 달라지지 않게 한다.
/// </summary>
public static class LanguageFontSetup
{
    private const string Folder = "Assets/_Project/Fonts/NotoSansCJK";
    private const string BaseFontPath = "Assets/_Project/Fonts/GmarketSansMedium SDF.asset";
    private const string SetPath = "Assets/_Project/Resources/LanguageFonts.asset";
    private const int SamplingSize = 56, Padding = 6, AtlasWidth = 4096, AtlasHeight = 2048;
    private const string Ascii = " !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~";
    private const string CommonPunctuation = "–—‘’“”…·×→•※、。「」『』！？：；，（）・ー～《》【】";

    [MenuItem("Tools/Scroll Hunter/Build Language Fonts")]
    public static void Build()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Stop Play first.");
        var baseFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(BaseFontPath);
        var entries = Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText("Assets/_Project/Resources/GameText.json"))["entries"];
        Func<string, string> collect = field => string.Concat(entries.Select(e => (string)e[field] ?? ""));
        var latin1 = new string(Enumerable.Range(0xA1, 0xFF - 0xA1 + 1).Select(c => (char)c).ToArray());
        var japanese = Create("NotoSansJP-Medium", Ascii + latin1 + CommonPunctuation + collect("ja") + collect("es") + "日本語Español", baseFont);
        var chinese = Create("NotoSansSC-Medium", Ascii + CommonPunctuation + collect("zh") + "简体中文", baseFont);

        japanese.fallbackFontAssetTable = new List<TMP_FontAsset> { chinese, baseFont };
        chinese.fallbackFontAssetTable = new List<TMP_FontAsset> { japanese, baseFont };
        EditorUtility.SetDirty(japanese); EditorUtility.SetDirty(chinese);
        AssetDatabase.SaveAssetIfDirty(japanese); AssetDatabase.SaveAssetIfDirty(chinese);

        // 다른 폰트(피해 숫자 등)에서 빠진 글자용 공용 대체 폰트. 기존 항목 순서는 유지하고 뒤에 붙인다.
        var settings = Resources.Load<TMP_Settings>("TMP Settings");
        var fallbacks = new SerializedObject(settings).FindProperty("m_fallbackFontAssets");
        var list = Enumerable.Range(0, fallbacks.arraySize).Select(i => fallbacks.GetArrayElementAtIndex(i).objectReferenceValue as TMP_FontAsset)
            .Where(f => f != null && !f.name.StartsWith("NotoSans")).ToList();
        list.Add(japanese); list.Add(chinese);
        fallbacks.arraySize = list.Count;
        for (int i = 0; i < list.Count; i++) fallbacks.GetArrayElementAtIndex(i).objectReferenceValue = list[i];
        fallbacks.serializedObject.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssetIfDirty(settings);

        var set = AssetDatabase.LoadAssetAtPath<LanguageFontSet>(SetPath);
        if (set == null) { set = ScriptableObject.CreateInstance<LanguageFontSet>(); AssetDatabase.CreateAsset(set, SetPath); }
        var so = new SerializedObject(set);
        so.FindProperty("baseFont").objectReferenceValue = baseFont;
        var fonts = so.FindProperty("languageFonts");
        fonts.arraySize = GameText.NativeNames.Length;
        var byLanguage = new TMP_FontAsset[] { null, null, japanese, chinese, japanese };
        for (int i = 0; i < byLanguage.Length; i++) fonts.GetArrayElementAtIndex(i).objectReferenceValue = byLanguage[i];
        so.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssetIfDirty(set);
        Debug.Log("[Language Fonts] JP " + japanese.characterTable.Count + " / SC " + chinese.characterTable.Count + " glyphs, atlases "
            + japanese.atlasTextures.Length + "/" + chinese.atlasTextures.Length);
    }

    private static TMP_FontAsset Create(string name, string characters, TMP_FontAsset baseFont)
    {
        var unique = new string(characters.Where(c => !char.IsControl(c) && !char.IsSurrogate(c)).Distinct().OrderBy(c => c).ToArray());
        var source = AssetDatabase.LoadAssetAtPath<Font>(Folder + "/" + name + ".otf");
        var path = Folder + "/" + name + " SDF.asset";
        if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path) != null) AssetDatabase.DeleteAsset(path);
        var font = TMP_FontAsset.CreateFontAsset(source, SamplingSize, Padding, GlyphRenderMode.SDFAA, AtlasWidth, AtlasHeight, AtlasPopulationMode.Dynamic, true);
        font.name = name + " SDF";
        if (!font.TryAddCharacters(unique, out string missing) || !string.IsNullOrEmpty(missing))
            Debug.LogWarning("[Language Fonts] " + name + " missing: " + missing);

        // 줄 높이·상승·하강을 기존 본문 폰트와 같은 비율로 맞춘다. 글자 크기 기준(pointSize)은 원본 그대로다.
        var face = font.faceInfo; var reference = baseFont.faceInfo;
        float ratio = face.pointSize / reference.pointSize;
        face.lineHeight = reference.lineHeight * ratio;
        face.ascentLine = reference.ascentLine * ratio;
        face.descentLine = reference.descentLine * ratio;
        font.faceInfo = face;

        font.atlasPopulationMode = AtlasPopulationMode.Static;
        AssetDatabase.CreateAsset(font, path);
        for (int i = 0; i < font.atlasTextures.Length; i++)
        {
            font.atlasTextures[i].name = name + " Atlas" + (i == 0 ? "" : " " + i);
            AssetDatabase.AddObjectToAsset(font.atlasTextures[i], font);
        }
        font.material.name = name + " Atlas Material";
        AssetDatabase.AddObjectToAsset(font.material, font);
        EditorUtility.SetDirty(font);
        AssetDatabase.SaveAssetIfDirty(font);
        return font;
    }
}
