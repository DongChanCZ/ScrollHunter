using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class DialogueOptionsChecks
{
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    private static object Get(object o, string n) => o.GetType().GetField(n, Hidden).GetValue(o);
    public static string Run()
    {
        if (!Application.isPlaying) throw new InvalidOperationException("Fresh Play required");
        var flow = UnityEngine.Object.FindFirstObjectByType<BattleFlow>();
        var menu = flow.GetComponent<MainMenuUI>();
        var real = flow.Inheritance;
        var lang = GameText.Current;
        var key = (string)Get(flow, "skipDialoguePreference");
        var prefix = (string)Get(menu, "preferencesPrefix");
        var keys = new List<string> { key, GameText.PreferenceKey };
        foreach (var n in new[] { "DisplayMode", "Windowed", "VSync", "FrameLimit", "Width", "Height", "ScreenFeedback", "ScreenShake" }) keys.Add(prefix + n);
        var floats = new List<string>();
        foreach (var n in (string[])Get(menu, "mixerParameters")) floats.Add(prefix + n);
        var exists = new Dictionary<string, bool>();
        var ints = new Dictionary<string, int>();
        var values = new Dictionary<string, float>();
        foreach (var k in keys) { exists[k] = PlayerPrefs.HasKey(k); ints[k] = PlayerPrefs.GetInt(k); }
        foreach (var k in floats) { exists[k] = PlayerPrefs.HasKey(k); values[k] = PlayerPrefs.GetFloat(k); }
        var path = Path.Combine(Application.temporaryCachePath, "dialogue-options-" + Guid.NewGuid(), "save.json");
        var deck = UnityEngine.Object.FindFirstObjectByType<DeckSystem>().CopyStartingDeck();
        var pool = (BattleRewardOption[])Get(flow, "rewardPool");
        var save = new InheritanceSave(path, pool, deck);
        int count = 0;
        Action<bool, string> check = (ok, message) => { if (!ok) throw new Exception(message); count++; };
        try
        {
            flow.ReturnToTitle(); flow.GetType().GetField("inheritance", Hidden).SetValue(flow, save);
            PlayerPrefs.DeleteKey(key);
            menu.OpenOptions(); Canvas.ForceUpdateCanvases();
            var toggle = (Toggle)Get(menu, "skipSeenDialogueToggle");
            var hint = (TMP_Text)Get(menu, "dialogueLockedLabel");
            var scroll = (ScrollRect)Get(menu, "optionsScroll");
            check(toggle.isOn && !toggle.interactable && hint.gameObject.activeSelf, "locked without retry or victory, default checked");
            check(scroll.content.rect.height > scroll.viewport.rect.height && !scroll.horizontal, "vertical scroll overflow");
            var before = scroll.content.anchoredPosition.y;
            scroll.OnScroll(new PointerEventData(EventSystem.current) { scrollDelta = new Vector2(0f, -4f) });
            check(scroll.content.anchoredPosition.y > before, "wheel scroll moves down");
            foreach (GameLanguage language in Enum.GetValues(typeof(GameLanguage)))
            {
                GameText.SetLanguage(language); LanguageFonts.ApplyAll();
                var label = toggle.GetComponentInChildren<TMP_Text>(); label.ForceMeshUpdate(true);
                hint.ForceMeshUpdate(true);
                check(label.text == GameText.T("이미 본 전투 대화 건너뛰기") && !label.isTextOverflowing, "checkbox language " + language);
                check(hint.text == GameText.T("재도전 또는 전투 승리 후 변경할 수 있습니다.") && !hint.isTextOverflowing, "locked hint " + language);
                if (language != GameLanguage.Korean) check(hint.text != "재도전 또는 전투 승리 후 변경할 수 있습니다.", "hint translated " + language);
                for (int i = 0; i < hint.textInfo.characterCount; i++) check(hint.textInfo.characterInfo[i].character != '□', "hint glyph " + language);
            }
            GameText.SetLanguage(GameLanguage.Korean); LanguageFonts.ApplyAll();
            // 잠긴 상태에서는 코드로 값을 바꿔도 적용 시 저장하지 않는다.
            toggle.isOn = false; menu.ApplyOptions();
            check(flow.SkipSeenDialogue && !PlayerPrefs.HasKey(key), "locked option is not saved on apply");
            // 승리 없이 재도전한 경우도 옵션이 열리고 적용/재읽기가 가능하다.
            check(save.TryRecordRetry() && save.HasRetried && !save.HasStageVictory, "retry record kept, no victory");
            menu.OpenOptions(); check(toggle.interactable && !hint.gameObject.activeSelf, "retry alone unlocks");
            toggle.isOn = false; menu.ApplyOptions(); check(!flow.SkipSeenDialogue, "retry-only apply works");
            var retryOnly = new InheritanceSave(path, pool, deck);
            flow.GetType().GetField("inheritance", Hidden).SetValue(flow, retryOnly);
            menu.OpenOptions(); check(toggle.interactable && !toggle.isOn, "retry-only reload stays unlocked and remembers off");
            menu.ClosePanels(); flow.SetSkipSeenDialogue(true);
            flow.GetType().GetField("inheritance", Hidden).SetValue(flow, save);
            menu.ClosePanels();
            // 이전 버전 저장: hasRetried 필드가 없고 승리 기록만 있는 파일.
            var legacyPath = Path.Combine(Application.temporaryCachePath, "dialogue-legacy-" + Guid.NewGuid(), "save.json");
            var legacy = new InheritanceSave(legacyPath, pool, deck);
            check(legacy.TryRecordBattle(1), "legacy victory setup");
            var json = File.ReadAllText(legacyPath);
            File.WriteAllText(legacyPath, System.Text.RegularExpressions.Regex.Replace(json, ",?\\s*\"hasRetried\"\\s*:\\s*(true|false)", ""));
            check(!File.ReadAllText(legacyPath).Contains("hasRetried"), "legacy file has no retry field");
            var legacyLoaded = new InheritanceSave(legacyPath, pool, deck);
            check(!legacyLoaded.LoadFailed && legacyLoaded.HasStageVictory && !legacyLoaded.HasRetried, "legacy victory loads");
            flow.GetType().GetField("inheritance", Hidden).SetValue(flow, legacyLoaded);
            menu.OpenOptions(); check(toggle.interactable && !hint.gameObject.activeSelf, "legacy victory unlocks without retry");
            menu.ClosePanels(); flow.GetType().GetField("inheritance", Hidden).SetValue(flow, save);
            // 다시 새 저장으로 만들어 승리만으로도 같은 실행 안에서 열리는지 확인한다.
            check(save.TryResetGame() && !save.HasRetried, "clear retry before victory-only case");
            check(save.TryRecordBattle(1) && save.HasStageVictory, "victory recorded this session");
            menu.OpenOptions();
            check(toggle.interactable && !hint.gameObject.activeSelf, "unlocked after victory without restart");
            toggle.isOn = false;
            check(flow.SkipSeenDialogue, "unsaved change is not persisted");
            menu.ClosePanels(); menu.OpenOptions(); check(toggle.isOn, "cancel restores");
            toggle.isOn = false; menu.ApplyOptions();
            check(!flow.SkipSeenDialogue, "apply saves off");
            menu.OpenOptions(); check(!toggle.isOn, "reopen remembers off");
            menu.ClosePanels(); flow.SetSkipSeenDialogue(true); menu.OpenOptions();
            check(toggle.isOn, "retry checkbox preference shared");
            // 재실행 대신 저장 파일을 새로 읽어 확인: 승리 기록과 옵션 값이 남는다.
            var reloaded = new InheritanceSave(path, pool, deck);
            check(reloaded.HasStageVictory && !reloaded.HasRetried, "victory-only reload unlocks without retry");
            flow.GetType().GetField("inheritance", Hidden).SetValue(flow, reloaded);
            menu.ClosePanels(); menu.OpenOptions(); check(toggle.interactable && toggle.isOn == flow.SkipSeenDialogue, "reloaded save unlocked with saved value");
            menu.ClosePanels(); flow.GetType().GetField("inheritance", Hidden).SetValue(flow, save);
            check(save.TryRecordRetry() && save.HasRetried && save.HasStageVictory, "both conditions present");
            menu.OpenOptions(); check(toggle.interactable, "both conditions unlock"); menu.ClosePanels();
            check(save.TryReset() && save.HasStageVictory && save.HasRetried, "inheritance reset keeps both unlock records");
            check(save.TryResetGame() && !save.HasStageVictory && !save.HasRetried, "full reset"); menu.ClosePanels(); menu.OpenOptions();
            check(!toggle.interactable && hint.gameObject.activeSelf, "full reset locks option");
            check(scroll.verticalNormalizedPosition > .99f, "reopen resets scroll top");
            var blocked = Path.Combine(Application.temporaryCachePath, "blocked-retry-" + Guid.NewGuid()); Directory.CreateDirectory(blocked);
            var failed = new InheritanceSave(blocked, pool, deck);
            check(!failed.TryRecordBattle(1) && !failed.HasStageVictory, "write failure cannot unlock");
        }
        finally
        {
            menu.ClosePanels(); flow.GetType().GetField("inheritance", Hidden).SetValue(flow, real);
            foreach (var k in keys) { if (exists[k]) PlayerPrefs.SetInt(k, ints[k]); else PlayerPrefs.DeleteKey(k); }
            foreach (var k in floats) { if (exists[k]) PlayerPrefs.SetFloat(k, values[k]); else PlayerPrefs.DeleteKey(k); }
            PlayerPrefs.Save(); GameText.SetLanguage(lang);
        }
        return "Dialogue options: " + count + " passed. Isolated progress and restored preferences.";
    }
}
