using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class WebBuildSetup
{
    [MenuItem("Tools/Scroll Hunter/Apply Web Language UI")]
    public static void Apply()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Stop Play first.");
        var menu = UnityEngine.Object.FindFirstObjectByType<MainMenuUI>();
        var so = new SerializedObject(menu);
        var panel = ((GameObject)so.FindProperty("optionsPanel").objectReferenceValue).transform;
        var source = (TMP_Dropdown)so.FindProperty("resolutionDropdown").objectReferenceValue;
        var found = panel.Find("LanguageDropdown");
        var language = found != null ? found.GetComponent<TMP_Dropdown>() : UnityEngine.Object.Instantiate(source, panel, false);
        language.name = "LanguageDropdown";
        language.onValueChanged = new TMP_Dropdown.DropdownEvent();
        language.ClearOptions(); language.AddOptions(GameText.NativeNames.ToList());
        language.SetValueWithoutNotify(0);
        var r = (RectTransform)language.transform;
        r.anchoredPosition = new Vector2(170, 280); r.sizeDelta = new Vector2(650, 60);
        language.gameObject.SetActive(true);
        so.FindProperty("languageDropdown").objectReferenceValue = language;
        var labelRoot = panel.Find("LanguageLabel");
        var label = labelRoot != null ? labelRoot.GetComponent<TMP_Text>() : UnityEngine.Object.Instantiate(panel.Find("Resolution").GetComponent<TMP_Text>(), panel, false);
        label.name = "LanguageLabel"; label.text = "언어";
        label.rectTransform.anchoredPosition = new Vector2(-440, 280);
        var hint = panel.Find("Hint").GetComponent<TMP_Text>();
        hint.text = "언어와 소리는 바로 반영됩니다. 적용하면 저장되고, 취소하면 돌아갑니다.";
        so.ApplyModifiedPropertiesWithoutUndo();
        int count = 0;
        foreach (var text in UnityEngine.Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!text.gameObject.scene.IsValid() || !Regex.IsMatch(text.text ?? "", "[가-힣]")) continue;
            if (text.transform.IsChildOf(language.transform)) continue;
            var local = text.GetComponent<LocalizedLabel>() ?? text.gameObject.AddComponent<LocalizedLabel>();
            var binding = new SerializedObject(local);
            binding.FindProperty("source").stringValue = text.text;
            binding.ApplyModifiedPropertiesWithoutUndo();
            count++;
        }
        var reward = UnityEngine.Object.FindFirstObjectByType<BattleRewardUI>(FindObjectsInactive.Include);
        var properties = new SerializedObject(reward).GetIterator();
        while (properties.Next(true))
        {
            if (properties.propertyType != SerializedPropertyType.ObjectReference) continue;
            var text = properties.objectReferenceValue as TMP_Text;
            if (text != null && text.GetComponent<LocalizedTextFit>() == null) text.gameObject.AddComponent<LocalizedTextFit>();
        }
        EditorSceneManager.MarkSceneDirty(menu.gameObject.scene);
        EditorSceneManager.SaveScene(menu.gameObject.scene);
        Debug.Log("[Web UI] Bound " + count + " original Korean labels. English titles left unchanged.");
    }

    [MenuItem("Tools/Scroll Hunter/Build Web 1.1")]
    public static void Build() => BuildTo("Docs/근거자료/2026-10-14_웹빌드_언어");

    private const string ScheduledKey = "ScrollHunter.WebBuildScheduled";
    public static string ScheduleState => SessionState.GetString(ScheduledKey, "");

    /// <summary>
    /// MCP 등에서 빌드를 1회만 예약하고 바로 돌아온다. 이미 예약·진행 중이면 다시 예약하지 않는다.
    /// 완료 여부는 근거 폴더의 build-result.json과 ScheduleState로 확인한다.
    /// </summary>
    public static string ScheduleOnce(string evidenceFolder)
    {
        if (Application.isPlaying) return "play";
        var state = ScheduleState;
        if (state == "scheduled" || state == "building") return "already " + state;
        SessionState.SetString(ScheduledKey, "scheduled");
        EditorApplication.CallbackFunction run = null;
        run = () =>
        {
            EditorApplication.update -= run;
            SessionState.SetString(ScheduledKey, "building");
            try { BuildTo(evidenceFolder); SessionState.SetString(ScheduledKey, "done"); }
            catch (Exception e) { SessionState.SetString(ScheduledKey, "failed: " + e.Message); Debug.LogException(e); }
        };
        EditorApplication.update += run;
        return "scheduled";
    }

    private static void BuildTo(string evidenceFolder)
    {
        if (Application.isPlaying) throw new InvalidOperationException("Stop Play first.");
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
        PlayerSettings.WebGL.decompressionFallback = false;
        PlayerSettings.WebGL.threadsSupport = false;
        PlayerSettings.WebGL.dataCaching = true;
        PlayerSettings.WebGL.initialMemorySize = 256;
        PlayerSettings.WebGL.maximumMemorySize = 2048;
        PlayerSettings.WebGL.memoryGrowthMode = WebGLMemoryGrowthMode.Geometric;
        PlayerSettings.WebGL.template = "PROJECT:ScrollHunter";
        PlayerSettings.WebGL.powerPreference = WebGLPowerPreference.HighPerformance;
        PlayerSettings.defaultWebScreenWidth = 1280;
        PlayerSettings.defaultWebScreenHeight = 720;
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "ScrollHunter_Web_1.1");
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
            scenes = new[] { "Assets/_Project/Scenes/Battle.unity" }, locationPathName = path,
            target = BuildTarget.WebGL, options = BuildOptions.None
        });
        var result = new { result = report.summary.result.ToString(), errors = report.summary.totalErrors,
            warnings = report.summary.totalWarnings, bytes = report.summary.totalSize, seconds = report.summary.totalTime.TotalSeconds, path };
        var evidence = Path.Combine(Directory.GetParent(Application.dataPath).FullName, evidenceFolder);
        Directory.CreateDirectory(evidence);
        File.WriteAllText(Path.Combine(evidence,"build-result.json"), Newtonsoft.Json.JsonConvert.SerializeObject(result, Newtonsoft.Json.Formatting.Indented));
        if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded) throw new Exception("Web build failed; see Console.");
        var credits=Path.Combine(Directory.GetParent(Application.dataPath).FullName,"Docs/CREDITS.md");
        if(File.Exists(credits)) File.Copy(credits,Path.Combine(path,"CREDITS.md"),true);
        var noto=Path.Combine(Application.dataPath,"_Project/Fonts/NotoSansCJK/OFL.txt");
        if(File.Exists(noto)) File.Copy(noto,Path.Combine(path,"OFL_NotoSansCJK.txt"),true);
        Debug.Log("[Web Build] " + result.path + " / " + result.bytes);
    }
}
