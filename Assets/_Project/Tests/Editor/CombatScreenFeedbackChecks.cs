using System;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class CombatScreenFeedbackChecks
{
    const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    static object Get(object o, string n) => o.GetType().GetField(n, Hidden).GetValue(o);
    static void Set(object o, string n, object v) => o.GetType().GetField(n, Hidden).SetValue(o, v);
    static void Call(object o, string n, params object[] args) => o.GetType().GetMethod(n, Hidden).Invoke(o, args);
    static T Find<T>() where T : UnityEngine.Object => UnityEngine.Object.FindFirstObjectByType<T>();

    [MenuItem("Tools/Scroll Hunter/Check Combat Screen Feedback (Play)")]
    public static void Menu() => Debug.Log(Run());
    public static string Run()
    {
        if (!Application.isPlaying) throw new Exception("Play required");
        var flow = Find<BattleFlow>(); var info = Find<CombatInfoUI>(); var p = Find<Player>();
        var menu = Find<MainMenuUI>(); var shake = Find<RedHitShake>(); var tutorial = Find<TutorialFlow>(); var story = Find<StoryUI>();
        var fx = (Toggle)Get(menu, "screenFeedbackToggle"); var motion = (Toggle)Get(menu, "screenShakeToggle");
        var hit = (CombatScreenOverlay)Get(shake, "hitOverlay"); var stun = (CombatScreenOverlay)Get(shake, "stunOverlay");
        var keys = new[] { "ScrollHunter.Options.ScreenFeedback", "ScrollHunter.Options.ScreenShake" };
        var existed = new[] { PlayerPrefs.HasKey(keys[0]), PlayerPrefs.HasKey(keys[1]) };
        var saved = new[] { PlayerPrefs.GetInt(keys[0], 1), PlayerPrefs.GetInt(keys[1], 1) };
        bool oldTutorial = tutorial.enabled, oldStory = story.enabled;
        int passed = 0;
        Action<bool, string> check = (ok, name) => { if (!ok) throw new Exception(name); passed++; };
        Action fresh = () => { Call(flow, "BeginBattle", 2, true, null); Set(flow, "menuInputFrame", -1); };
        Action<float> draw = dt => Call(shake, "AdvanceScreenFeedback", dt);
        try
        {
            menu.ClosePanels(); tutorial.Cancel(); tutorial.enabled = false; story.enabled = false;
            Set(flow, "runDeck", Find<DeckSystem>().CopyStartingDeck());
            fx.isOn = true; motion.isOn = true;
            fresh();
            if (info.BattleSpeed != 1f) info.ToggleSpeed();
            info.ToggleSpeed(); check(info.BattleSpeed == 2f && Time.timeScale == 2f, "S selects 2x");
            info.TogglePause(); check(Time.timeScale == 0f, "A pauses 2x");
            info.ToggleSpeed(); check(info.BattleSpeed == 1f && Time.timeScale == 0f, "paused speed change stays paused");
            info.ToggleSpeed(); info.Resume(); check(Time.timeScale == 2f, "resume uses 2x");
            fresh(); check(Time.timeScale == 2f, "next battle retains 2x");
            menu.OpenPauseMenu(); check(Time.timeScale == 0f, "Esc pauses 2x");
            menu.ResumeGame(); Set(flow, "menuInputFrame", -1); check(Time.timeScale == 2f, "Esc resume restores 2x");
            Call(info, "LateUpdate"); check(((TMP_Text)Get(info, "speedLabel")).text.Contains("2"), "2x label");
            foreach (GameLanguage language in Enum.GetValues(typeof(GameLanguage)))
                check(!GameText.T(MainMenuUI.ControlsDescription, language).Contains("0.5"), "controls remove half speed " + language);
            check(hit.canvasRenderer != null && stun.canvasRenderer != null, "renderers included");
            check(!hit.raycastTarget && !stun.raycastTarget && hit.canvas.GetComponent<GraphicRaycaster>() == null, "overlay never consumes clicks");
            check(hit.canvas.sortingOrder > 0 && hit.rectTransform.anchorMin == Vector2.zero && hit.rectTransform.anchorMax == Vector2.one, "full screen overlay");
            p.TakeDamage(60); draw(0f); check(hit.color.r > hit.color.b && hit.color.a > .28f, "HP loss red screen");
            check(!shake.IsShaking, "ordinary hit never shakes");
            draw(.1f); float faded = hit.color.a;
            for (int i = 0; i < 12; i++) p.TakeDamage(1);
            draw(0f); check(Mathf.Approximately(hit.color.a, faded), "12 rapid hits do not restart or stack");
            draw(.25f); check(hit.color.a == 0f, "pulse fades completely");
            p.TakeDamage(10); draw(0f); check(hit.color.a > 0f, "pulse returns after cooldown");
            draw(1f); p.AddShield(100); p.TakeDamage(20); draw(0f);
            check(hit.EdgeOnly && hit.color.a == 0f && !shake.IsShaking, "absorb starts with transparent border");
            draw(.06f); float halfEdge = hit.color.a;
            check(halfEdge > 0f && halfEdge < .22f, "border fades in gently");
            draw(.06f); check(Mathf.Approximately(hit.color.a, .22f), "border maximum bounded");
            for (int i = 0; i < 12; i++) p.TakeDamage(1);
            draw(0f); check(Mathf.Approximately(hit.color.a, .22f), "absorbed rapid hits do not restart border");
            draw(.5f); check(hit.color.a == 0f, "border fades out fully");
            p.TakeDamage(10); draw(.12f); check(hit.EdgeOnly && hit.color.a > 0f, "next absorption can show border");
            p.TakeDamage(200); draw(0f); check(!hit.EdgeOnly && hit.color.r > hit.color.b, "HP spillover takes priority over absorb");
            fresh(); p.SetSanctuary(true); p.TakeDamage(60); p.ApplyStun(2f); draw(0f);
            check(hit.color.a == 0f && stun.color.a == 0f && !p.IsStunned, "immunity has no false feedback");
            p.SetSanctuary(false); p.TakeDamage(0); draw(0f); check(hit.color.a == 0f, "zero damage has no feedback");
            p.ApplyStun(3f); draw(.1f); check(stun.color.a > 0f && stun.color.b > stun.color.r, "stun purple haze");
            float opacity = stun.color.a, wave = stun.Wave; draw(0f);
            check(stun.color.a == opacity && stun.Wave == wave, "manual pause freezes haze");
            p.ApplyStun(1f); check(p.StunRemaining == 3f, "feedback does not change stun duration");
            p.ApplyStun(4f); draw(.1f); check(stun.color.a <= .38f, "repeated stun bounded");
            menu.OpenPauseMenu(); draw(0f); check(stun.color.a == 0f && hit.color.a == 0f, "menu stays unobscured");
            menu.ResumeGame(); Set(flow, "menuInputFrame", -1); draw(0f); check(stun.color.a > 0f, "stun resumes after menu");
            fx.isOn = false; draw(0f); check(stun.color.a == 0f && hit.color.a == 0f, "effects option immediately clears");
            check(p.IsStunned, "option cannot cancel gameplay stun");
            fx.isOn = true; draw(0f); check(stun.color.a > 0f, "enable restores active stun");
            p.EndBattle(); draw(0f); check(stun.color.a == 0f, "stun clears at battle end");
            fresh(); motion.isOn = false; p.TakeDamage(20, CastColor.Red); check(!shake.IsShaking, "shake disabled");
            draw(0f); check(hit.color.a > 0f, "color remains independently enabled");
            motion.isOn = true; p.TakeDamage(20, CastColor.Red); check(shake.IsShaking, "red shake enabled");
            Call(shake, "Advance", .1f); float remaining = (float)Get(shake, "remaining");
            p.TakeDamage(20, CastColor.Red); check((float)Get(shake, "remaining") == remaining, "rapid red hits cannot restart shake");
            fresh(); check(!shake.IsShaking && hit.color.a == 0f && stun.color.a == 0f, "restart clears all feedback");
            p.TakeDamage(10000); draw(0f); check(hit.color.a > 0f, "lethal hit still visible");
            draw(.4f); check(hit.color.a == 0f && stun.color.a == 0f, "death fades without game time");
            foreach (bool enabled in new[] { false, true })
            {
                fx.isOn = enabled; motion.isOn = enabled; Call(menu, "SaveFeedbackOptions");
                fx.isOn = !enabled; motion.isOn = !enabled; Call(menu, "ReadFeedbackOptions");
                check(menu.ScreenFeedbackEnabled == enabled && menu.ScreenShakeEnabled == enabled, "saved settings reload " + enabled);
            }
            fresh(); menu.OpenPauseMenu(); menu.OpenOptions();
            fx.isOn = false; motion.isOn = false; menu.ClosePanels();
            check(menu.ScreenFeedbackEnabled && menu.ScreenShakeEnabled, "cancel restores saved values");
            menu.ResumeGame(); Set(flow, "menuInputFrame", -1);
            return "Combat screen feedback: " + passed + " passed (function checks; human comfort and browser not measured)";
        }
        finally
        {
            for (int i = 0; i < keys.Length; i++) { if (existed[i]) PlayerPrefs.SetInt(keys[i], saved[i]); else PlayerPrefs.DeleteKey(keys[i]); }
            PlayerPrefs.Save(); Call(menu, "ReadFeedbackOptions");
            menu.ClosePanels(); if (menu.PauseMenuVisible) menu.ResumeGame();
            fresh(); if (info.BattleSpeed != 1f) info.ToggleSpeed();
            tutorial.enabled = oldTutorial; story.enabled = oldStory;
        }
    }
}
