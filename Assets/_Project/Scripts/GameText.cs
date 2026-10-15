using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

/// <summary>저장값은 순서 그대로 쓴다. 기존 0=한국어, 1=English는 바꾸지 않는다.</summary>
public enum GameLanguage { Korean = 0, English = 1, Japanese = 2, ChineseSimplified = 3, Spanish = 4 }

/// <summary>표시 문구만 번역한다. 저장 ID와 원본 영문 표기는 바꾸지 않는다.</summary>
public static class GameText
{
    [Serializable] public class Entry { public string ko, en, ja, zh, es; }
    [Serializable] private class Catalog { public Entry[] entries; }
    /// <summary>언어 선택 목록에 쓰는 각 언어의 자기 표기. 순서는 GameLanguage 값과 같다.</summary>
    public static readonly string[] NativeNames = { "한국어", "English", "日本語", "简体中文", "Español" };
    private static Dictionary<string, string>[] tables;
    public const string PreferenceKey = "ScrollHunter.Options.Language";
    public static GameLanguage Current { get; private set; }
    /// <summary>원문(한국어)이 아닌 언어. 번역문이 길어질 수 있는 영역의 맞춤 판단에 쓴다.</summary>
    public static bool Translated => Current != GameLanguage.Korean;
    public static event Action Changed;
    public static bool IsWeb
    {
        get
        {
#if UNITY_WEBGL
            return true;
#else
            return false;
#endif
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        Changed = null;
        tables = null;
        Current = Saved();
#if UNITY_WEBGL && !UNITY_EDITOR
        int selected = ScrollHunter_GetLaunchLanguage();
        if (IsValid(selected)) { Current = (GameLanguage)selected; Save(); }
#endif
    }

    public static bool IsValid(int value) => value >= 0 && value < NativeNames.Length;

    private static GameLanguage SystemDefault()
    {
        if (IsWeb) return GameLanguage.English;
        switch (Application.systemLanguage)
        {
            case SystemLanguage.Korean: return GameLanguage.Korean;
            case SystemLanguage.Japanese: return GameLanguage.Japanese;
            case SystemLanguage.Chinese:
            case SystemLanguage.ChineseSimplified: return GameLanguage.ChineseSimplified;
            case SystemLanguage.Spanish: return GameLanguage.Spanish;
            default: return GameLanguage.English;
        }
    }

    private static GameLanguage Saved()
    {
        int value = PlayerPrefs.GetInt(PreferenceKey, (int)SystemDefault());
        return IsValid(value) ? (GameLanguage)value : SystemDefault();
    }

    public static void SetLanguage(GameLanguage value)
    {
        if (!IsValid((int)value) || Current == value) return;
        Current = value;
        Changed?.Invoke();
    }

    public static void LoadSaved() => SetLanguage(Saved());
    public static void Save()
    {
        PlayerPrefs.SetInt(PreferenceKey, (int)Current); PlayerPrefs.Save();
#if UNITY_WEBGL && !UNITY_EDITOR
        ScrollHunter_RememberLanguage((int)Current);
#endif
    }

#if UNITY_WEBGL && !UNITY_EDITOR
    [System.Runtime.InteropServices.DllImport("__Internal")]
    private static extern int ScrollHunter_GetLaunchLanguage();
    [System.Runtime.InteropServices.DllImport("__Internal")]
    private static extern void ScrollHunter_RememberLanguage(int value);
#endif

    public static string T(string source) => T(source, Current);

    /// <summary>번역이 비어 있으면 영어, 영어도 없으면 원문을 보여 준다.</summary>
    public static string T(string source, GameLanguage language)
    {
        if (language == GameLanguage.Korean || string.IsNullOrEmpty(source)) return source;
        if (tables == null) Load();
        if (tables[(int)language].TryGetValue(source, out var translated)) return translated;
        return tables[(int)GameLanguage.English].TryGetValue(source, out translated) ? translated : source;
    }

    private static void Load()
    {
        tables = new Dictionary<string, string>[NativeNames.Length];
        for (int i = 0; i < tables.Length; i++) tables[i] = new Dictionary<string, string>(StringComparer.Ordinal);
        var asset = Resources.Load<TextAsset>("GameText");
        if (asset == null) return;
        foreach (var entry in JsonUtility.FromJson<Catalog>(asset.text).entries)
        {
            Add(GameLanguage.English, entry.ko, entry.en);
            Add(GameLanguage.Japanese, entry.ko, entry.ja);
            Add(GameLanguage.ChineseSimplified, entry.ko, entry.zh);
            Add(GameLanguage.Spanish, entry.ko, entry.es);
        }
    }

    private static void Add(GameLanguage language, string source, string translated)
    {
        if (!string.IsNullOrEmpty(translated)) tables[(int)language][source] = translated;
    }

    public static string Format(string format, params object[] args)
    {
        if (Translated)
            for (int i = 0; i < args.Length; i++) if (args[i] is string text) args[i] = T(text);
        return string.Format(CultureInfo.InvariantCulture, T(format), args);
    }
}
