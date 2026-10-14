using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

/// <summary>표시 문구만 번역한다. 저장 ID와 원본 영문 표기는 바꾸지 않는다.</summary>
public static class GameText
{
    [Serializable] public class Entry { public string ko, en; }
    [Serializable] private class Catalog { public Entry[] entries; }
    private static Dictionary<string, string> english;
    public const string PreferenceKey = "ScrollHunter.Options.Language";
    public static bool English { get; private set; }
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
        english = null;
        English = PlayerPrefs.GetInt(PreferenceKey, Application.systemLanguage == SystemLanguage.Korean ? 0 : 1) == 1;
    }

    public static void SetEnglish(bool value)
    {
        if (English == value) return;
        English = value;
        Changed?.Invoke();
    }

    public static void LoadSaved() => SetEnglish(PlayerPrefs.GetInt(PreferenceKey,
        Application.systemLanguage == SystemLanguage.Korean ? 0 : 1) == 1);
    public static void Save() { PlayerPrefs.SetInt(PreferenceKey, English ? 1 : 0); PlayerPrefs.Save(); }
    public static string T(string source)
    {
        if (!English || string.IsNullOrEmpty(source)) return source;
        if (english == null)
        {
            english = new Dictionary<string, string>(StringComparer.Ordinal);
            var asset = Resources.Load<TextAsset>("GameText");
            if (asset != null)
                foreach (var entry in JsonUtility.FromJson<Catalog>(asset.text).entries) english[entry.ko] = entry.en;
        }
        return english.TryGetValue(source, out var translated) ? translated : source;
    }

    public static string Format(string format, params object[] args)
    {
        if (English)
            for (int i = 0; i < args.Length; i++) if (args[i] is string text) args[i] = T(text);
        return string.Format(CultureInfo.InvariantCulture, T(format), args);
    }
}
