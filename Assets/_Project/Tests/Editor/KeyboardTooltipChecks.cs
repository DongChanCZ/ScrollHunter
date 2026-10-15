using System;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// Input routing is exercised directly; this does not synthesize physical keyboard events.
public static class KeyboardTooltipChecks
{
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    private static object Get(object o, string n) => o.GetType().GetField(n, Hidden).GetValue(o);
    private static void Set(object o, string n, object v) => o.GetType().GetField(n, Hidden).SetValue(o, v);
    private static void Call(object o, string n, params object[] a) => o.GetType().GetMethod(n, Hidden).Invoke(o, a);

    [MenuItem("Tools/Scroll Hunter/Check Keyboard Tooltips (Play)")]
    public static void Menu() => Debug.Log(Run());

    public static string Run()
    {
        if (!Application.isPlaying) throw new InvalidOperationException("Play required");
        var flow = UnityEngine.Object.FindFirstObjectByType<BattleFlow>();
        var info = UnityEngine.Object.FindFirstObjectByType<CombatInfoUI>();
        var tutorial = UnityEngine.Object.FindFirstObjectByType<TutorialFlow>();
        var story = UnityEngine.Object.FindFirstObjectByType<StoryUI>();
        var deck = UnityEngine.Object.FindFirstObjectByType<DeckSystem>();
        var cost = UnityEngine.Object.FindFirstObjectByType<CostSystem>();
        var hud = UnityEngine.Object.FindFirstObjectByType<CombatUI>();
        var menu = flow.GetComponent<MainMenuUI>();
        var panel = (GameObject)Get(info, "cardPanel");
        var label = (TMP_Text)Get(info, "cardText");
        var hint = (TMP_Text)Get(info, "hintText");
        var views = (CombatUI.HandSlotView[])Get(hud, "handSlots");
        var frames = (Image[])Get(hud, "inspectionFrames");
        var save = Get(flow, "inheritance");
        bool tutorialEnabled = tutorial.enabled, storyEnabled = story.enabled;
        GameLanguage language = GameText.Current;
        int passed = 0;
        Action<bool, string> check = (ok, name) => { if (!ok) throw new Exception("Keyboard tooltip: " + name); passed++; };
        Action<bool, int, int> inspect = (held, key, hover) =>
        {
            Call(info, "UpdateKeyboardInspection", held, key, hover);
            Call(info, "LateUpdate"); Call(hud, "UpdateHand");
        };
        Action nextFrame = () => { Set(info, "inspectionClosedFrame", -1); Set(tutorial, "consumedFrame", -1); Set(flow, "menuInputFrame", -1); };
        try
        {
            Set(flow, "inheritance", new InheritanceSave(System.IO.Path.Combine(Application.temporaryCachePath,
                "KeyboardTooltip-" + Guid.NewGuid() + ".json"), (BattleRewardOption[])Get(flow, "rewardPool"), deck.CopyStartingDeck()));
            story.enabled = false; tutorial.enabled = true;
            flow.BeginTutorial(false); tutorial.enabled = false; nextFrame();
            Time.timeScale = 1f; cost.EnsureTutorialCost(cost.Max);
            var originalCards = Enumerable.Range(0, 4).Select(deck.GetHandCard).ToArray();
            float originalCost = cost.Current;

            inspect(true, -1, 0);
            check(info.IsInspecting && !panel.activeSelf, "holding Space awaits a skill key, ignores mouse");
            foreach (int slot in new[] { 0, 1, 3, 2, 0 })
            {
                inspect(true, slot, (slot + 1) % 4);
                check(info.InspectedSlot == slot && panel.activeSelf && label.text.Contains(deck.GetHandCard(slot).DisplayName), "correct selected description");
                check(frames[slot] != null && frames[slot].enabled && frames.Count(x => x != null && x.enabled) == 1, "one gold frame");
                check(views.All(x => x.keyLabel.color == (Color)Get(hud, "inspectionColor")), "four key labels emphasized");
                check(Enumerable.Range(0, 4).All(i => !deck.TryUseSlot(i)), "all shared cast paths blocked");
                check(cost.Current == originalCost && Enumerable.Range(0, 4).All(i => deck.GetHandCard(i) == originalCards[i]), "no resource spend or cycle");
                check(Time.timeScale == 1f && !info.IsInfoPaused, "inspection does not pause combat");
                inspect(true, -1, -1); check(info.InspectedSlot == slot, "selection remains while Space is held");
            }
            inspect(false, -1, 0);
            check(!panel.activeSelf && !info.IsInspecting && frames.All(x => x == null || !x.enabled), "release closes and clears emphasis even over an icon");
            check(!deck.TryUseSlot(0), "release frame cannot cast"); nextFrame();
            inspect(false, -1, 1); check(panel.activeSelf && label.text.Contains(deck.GetHandCard(1).DisplayName), "mouse hover restored");
            inspect(false, -1, -1); check(!panel.activeSelf, "mouse exit closes");

            info.ToggleSpeed(); info.TogglePause();
            inspect(true, 0, -1); check(info.IsInfoPaused && Time.timeScale == 0f, "works during A pause");
            inspect(false, -1, -1); check(info.IsInfoPaused && Time.timeScale == 0f, "release preserves A pause");
            info.Resume(); nextFrame(); check(Time.timeScale == .5f, "pause resumes original speed");
            info.ToggleSpeed();
            check(deck.TryUseSlot(1) && deck.IsCasting, "normal cast works after inspection");
            float remaining = deck.CastRemaining;
            inspect(true, 0, -1); Call(deck, "AdvanceCast", .2f);
            check(deck.IsCasting && deck.CastRemaining < remaining, "existing cast keeps progressing while inspecting");
            menu.OpenPauseMenu(); Call(info, "Update"); Call(info, "LateUpdate");
            check(!info.IsInspecting && !panel.activeSelf && flow.IsMenuPaused && Time.timeScale == 0f, "Esc clears tooltip without resuming");
            menu.ResumeGame(); nextFrame();
            inspect(true, 2, -1); Call(info, "OnApplicationFocus", false); Call(info, "LateUpdate");
            check(!info.IsInspecting && !panel.activeSelf, "focus loss clears inspection");

            tutorial.enabled = true; flow.BeginTutorial(false); nextFrame();
            for (int i = 0; i < 4; i++) { check(tutorial.AdvanceGuide(), "advance to practice"); nextFrame(); }
            check(tutorial.WaitingForTooltip && !tutorial.WaitingForSkill, "tutorial awaits inspection");
            inspect(false, 0, -1); check(tutorial.WaitingForTooltip && !panel.activeSelf, "Q alone cannot complete practice");
            inspect(true, 1, -1); check((int)Get(tutorial, "tooltipGuideStep") == 1, "wrong skill does not complete practice");
            check(!tutorial.AdvanceGuide() && !deck.TryUseSlot(0), "click/Enter and cast cannot bypass practice");
            inspect(true, 0, -1);
            check((int)Get(tutorial, "tooltipGuideStep") == 2 && panel.activeSelf && Time.timeScale == 0f, "Q shows Fireball with tutorial paused");
            foreach (GameLanguage lang in Enum.GetValues(typeof(GameLanguage)))
            {
                GameText.SetLanguage(lang); Call(info, "LateUpdate"); Call(tutorial, "LateUpdate");
                hint.ForceMeshUpdate(true);
                check(!hint.isTextOverflowing, "battle hint fits " + lang);
                var guide = (TMP_Text)Get(tutorial, "guideText"); guide.ForceMeshUpdate(true);
                check(!guide.isTextOverflowing, "release guide fits " + lang);
                check(GameText.T((string)Get(tutorial, "tooltipPrompt"), lang) != (string)Get(tutorial, "tooltipPrompt") || lang == GameLanguage.Korean, "practice translated " + lang);
            }
            GameText.SetLanguage(language);
            inspect(false, -1, -1);
            check(tutorial.WaitingForSkill && !tutorial.WaitingForTooltip && !panel.activeSelf && !deck.TryUseSlot(0), "release closes and waits for a fresh fireball input");
            nextFrame(); check(deck.TryUseSlot(0) && tutorial.CompletedLessons == 3, "fresh Q uses Fireball after lesson");
            tutorial.Skip(); inspect(true, 0, -1);
            check(!info.IsInspecting && !panel.activeSelf, "preparation rejects combat inspection");
        }
        finally
        {
            GameText.SetLanguage(language); flow.ReturnToTitle();
            Set(flow, "inheritance", save); tutorial.enabled = tutorialEnabled; story.enabled = storyEnabled;
        }
        return "Keyboard tooltip checks passed: " + passed + ". Isolated save; routed inputs, not physical keyboard events.";
    }
}
