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
            check(toggle.isOn && !toggle.interactable && hint.gameObject.activeSelf, "locked before retry, default checked");
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
                check(hint.text == GameText.T("첫 재도전을 시작하면 변경할 수 있습니다.") && !hint.isTextOverflowing, "locked hint " + language);
            }
            menu.ClosePanels(); check(save.TryRecordRetry(), "record actual retry");
            menu.OpenOptions();
            check(toggle.interactable && !hint.gameObject.activeSelf, "unlocked after retry");
            toggle.isOn = false;
            check(flow.SkipSeenDialogue, "unsaved change is not persisted");
            menu.ClosePanels(); menu.OpenOptions(); check(toggle.isOn, "cancel restores");
            toggle.isOn = false; menu.ApplyOptions();
            check(!flow.SkipSeenDialogue, "apply saves off");
            menu.OpenOptions(); check(!toggle.isOn, "reopen remembers off");
            menu.ClosePanels(); flow.SetSkipSeenDialogue(true); menu.OpenOptions();
            check(toggle.isOn, "retry checkbox preference shared");
            check(new InheritanceSave(path, pool, deck).HasRetried, "reload keeps unlock");
            check(save.TryResetGame(), "full reset"); menu.ClosePanels(); menu.OpenOptions();
            check(!toggle.interactable && hint.gameObject.activeSelf, "full reset locks option");
            check(scroll.verticalNormalizedPosition > .99f, "reopen resets scroll top");
            var blocked = Path.Combine(Application.temporaryCachePath, "blocked-retry-" + Guid.NewGuid()); Directory.CreateDirectory(blocked);
            var failed = new InheritanceSave(blocked, pool, deck);
            check(!failed.TryRecordRetry() && !failed.HasRetried, "write failure cannot unlock");
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
