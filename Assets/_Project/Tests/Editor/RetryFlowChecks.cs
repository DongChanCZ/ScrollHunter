using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class RetryFlowChecks
{
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    private static object Get(object o, string n) => o.GetType().GetField(n, Hidden).GetValue(o);
    private static void Set(object o, string n, object v) => o.GetType().GetField(n, Hidden).SetValue(o, v);
    private static object Call(object o, string n, params object[] args) => o.GetType().GetMethod(n, Hidden).Invoke(o, args);

    [MenuItem("Tools/Scroll Hunter/Check Retry Flow (Play)")]
    public static void Menu() => Debug.Log(Run());

    public static string Run()
    {
        if (!Application.isPlaying) throw new InvalidOperationException("Play required");
        var f = UnityEngine.Object.FindFirstObjectByType<BattleFlow>();
        var d = UnityEngine.Object.FindFirstObjectByType<DeckSystem>();
        var p = UnityEngine.Object.FindFirstObjectByType<Player>();
        var t = UnityEngine.Object.FindFirstObjectByType<TutorialFlow>();
        var story = f.GetComponent<StoryUI>();
        var ui = (BattleRewardUI)Get(f, "rewardUI");
        var real = f.Inheritance; var lang = GameText.Current;
        bool tutorialEnabled = t.enabled, storyEnabled = story.enabled;
        float hold = (float)Get(f, "defeatEffectHold");
        string key = (string)Get(f, "skipDialoguePreference");
        bool had = PlayerPrefs.HasKey(key); int preference = PlayerPrefs.GetInt(key);
        string dir = Path.Combine(Application.temporaryCachePath, "RetryChecks-" + Guid.NewGuid());
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, "save.json");
        var pool = (BattleRewardOption[])Get(f, "rewardPool");
        var deck = d.CopyStartingDeck();
        Func<InheritanceSave> load = () => new InheritanceSave(path, pool, deck);
        int count = 0;
        Action<bool, string> check = (ok, label) => { if (!ok) throw new Exception("Retry: " + label); count++; };
        Action unlock = () => { Set(story, "consumedFrame", -1); Set(f, "menuInputFrame", -1); };
        Action lose = () => { p.TakeDamage(100000); Call(f, "Update"); check(f.State == BattleFlowState.Defeat, "actual defeat"); };
        try
        {
            GameText.SetLanguage(GameLanguage.Korean); t.enabled = false; story.enabled = true;
            PlayerPrefs.DeleteKey(key); check(f.SkipSeenDialogue, "default skip on");
            f.SetSkipSeenDialogue(false); check(!f.SkipSeenDialogue && PlayerPrefs.GetInt(key) == 0, "off persisted");
            f.SetSkipSeenDialogue(true); check(f.SkipSeenDialogue && PlayerPrefs.GetInt(key) == 1, "on persisted");
            File.WriteAllText(path, "{\"version\":1,\"cards\":[null,null,null,null,null,null,null,null],\"passives\":[],\"clearedBattles\":2}");
            var old = load();
            check(!old.HasRetried, "legacy retry option locked");
            check(!old.LoadFailed && old.HasSeenDialogue(1) && !old.HasSeenDialogue(0), "old cleared record only");
            check(old.TryRecordDialogue(3) && load().HasSeenDialogue(3) && load().ClearedBattles == 2, "dialogue saved independently");
            check(old.TryReset() && load().HasSeenDialogue(3), "inheritance reset preserves dialogue");
            check(old.TryResetGame() && !load().HasSeenDialogue(1) && !load().HasSeenDialogue(3), "full reset clears dialogue");
            check(!old.TryRecordDialogue(-1) && !old.TryRecordDialogue(30), "invalid indices rejected");
            var blocked = Path.Combine(dir, "blocked"); Directory.CreateDirectory(blocked);
            var failure = new InheritanceSave(blocked, pool, deck);
            check(!failure.TryRecordDialogue(0) && !failure.HasSeenDialogue(0), "failed write does not mark seen");
            var save = load(); save.TryRecordIntro(); save.TryRecordTutorial();
            Set(f, "inheritance", save); f.ReturnToTitle(); f.StartRun();
            check(f.State == BattleFlowState.Preparing && !f.IsRetryPreparation, "normal initial preparation");
            unlock(); f.NextBattle(); check(story.Active, "unseen dialogue shown");
            f.ReturnToTitle(); check(!load().HasSeenDialogue(0), "cancel does not mark seen");
            for (int i = 0; i < f.BattleCount; i++)
            {
                f.ReturnToTitle(); f.StartRun(); unlock(); Call(f, "StartBattleStory", i, true);
                check(story.Active, "unseen battle " + i);
                if (i % 2 == 0) { unlock(); story.Skip(); } else Call(story, "Complete");
                check(!story.Active && f.IsCountingDown && load().HasSeenDialogue(i), "finish/skip records " + i);
                check(load().ClearedBattles == 0, "not a clear " + i);
                f.ReturnToTitle(); f.StartRun(); unlock(); Call(f, "StartBattleStory", i, true);
                check(!story.Active && f.IsCountingDown, "seen dialogue skips " + i);
                f.ReturnToTitle(); f.SetSkipSeenDialogue(false); f.StartRun(); unlock(); Call(f, "StartBattleStory", i, true);
                check(story.Active, "off replays " + i); f.ReturnToTitle(); f.SetSkipSeenDialogue(true);
            }
            Set(f, "defeatEffectHold", 0f);
            unlock(); f.RestartRun(); lose();
            check(!f.HasInheritanceCandidates && ((TMP_Text)Get(f, "resultText")).text.Contains("처음부터 다시 도전"), "no reward defeat notice");
            check(((Button)Get(f, "restartButton")).GetComponentInChildren<TMP_Text>().text == "다시 도전", "no reward button");
            f.ContinueAfterResult();
            check(f.State == BattleFlowState.Preparing && f.IsRetryPreparation, "no reward retry preparation");
            check(((RectTransform)((Button)Get(ui, "reorderButton")).transform).anchoredPosition.x == 0f
                && ((RectTransform)((Button)Get(f, "nextButton")).transform).anchoredPosition.x == 600f, "center reorder and right progress");
            check(!ui.RetryConfirmationVisible && ui.transform.Find("DialogueSettingsButton") == null, "no top-right settings button");
            unlock(); ((Button)Get(f, "nextButton")).onClick.Invoke();
            var toggle = (Toggle)Get(ui, "dialogueSkipToggle");
            check(ui.RetryConfirmationVisible && toggle.gameObject.activeInHierarchy && toggle.isOn, "retry opens checked confirmation before starting");
            f.NextBattle(); check(f.State == BattleFlowState.Preparing && !f.BeginOrderEdit() && !f.IsCountingDown, "popup blocks background actions");
            toggle.isOn = false; check(!f.SkipSeenDialogue, "checkbox saves choice"); toggle.isOn = true;
            foreach (GameLanguage language in Enum.GetValues(typeof(GameLanguage)))
            {
                GameText.SetLanguage(language); LanguageFonts.ApplyAll(); Call(ui, "LateUpdate"); Canvas.ForceUpdateCanvases();
                var popup = (GameObject)Get(ui, "retryConfirmationPanel");
                check(popup.transform.Find("Title").GetComponent<TMP_Text>().text == GameText.T("다시 도전"), "popup title " + language);
                var message = popup.transform.Find("Message").GetComponent<TMP_Text>(); message.ForceMeshUpdate();
                check(message.text == GameText.T("스테이지의 맨 처음부터 다시 시작합니다.") && !message.isTextOverflowing, "restart notice " + language);
                var label = toggle.GetComponentInChildren<TMP_Text>(); label.ForceMeshUpdate();
                check(label.text == GameText.T("이미 본 전투 대화 건너뛰기") && !label.isTextOverflowing && label.fontSize < message.fontSize, "small checkbox label " + language);
                check(((Button)Get(ui, "retryConfirmButton")).GetComponentInChildren<TMP_Text>().text == GameText.T("확인")
                    && ((Button)Get(ui, "retryCancelButton")).GetComponentInChildren<TMP_Text>().text == GameText.T("취소"), "popup actions " + language);
            }
            GameText.SetLanguage(GameLanguage.Korean);
            f.GetComponent<MainMenuUI>().HandleEscape();
            check(!ui.RetryConfirmationVisible && !f.IsMenuPaused && f.State == BattleFlowState.Preparing, "escape cancels retry");
            ui.OpenRetryConfirmation();
            ((Button)Get(ui, "retryCancelButton")).onClick.Invoke();
            check(!ui.RetryConfirmationVisible && !toggle.gameObject.activeInHierarchy && !f.IsCountingDown, "cancel keeps preparation");
            check(!save.HasRetried && !load().HasRetried, "cancel does not unlock dialogue option");
            f.ConfirmRetry(); check(!f.IsCountingDown, "closed popup cannot confirm");
            check(((TMP_Text)Get(ui, "title")).text == "다시 도전 준비", "retry title");
            check(((TMP_Text)Get(f, "nextButtonLabel")).text == "다시 도전", "retry action");
            check(f.BeginOrderEdit(), "can edit after cancel"); ui.OpenRetryConfirmation();
            check(!ui.RetryConfirmationVisible, "no confirmation during order edit"); f.CancelOrderEdit();
            unlock(); f.NextBattle(); ((Button)Get(ui, "retryConfirmButton")).onClick.Invoke();
            check(f.IsCountingDown && f.BattleNumber == 1 && !story.Active, "confirmation starts first battle with skip");
            check(save.HasRetried && load().HasRetried, "confirmed retry unlock persists");
            f.ConfirmRetry(); check(f.IsCountingDown && f.BattleNumber == 1, "double confirmation ignored");
            var card = pool.First(x => x.Card != null && save.CanInherit(x));
            unlock(); f.RestartRun(); ((List<BattleRewardOption>)Get(f, "earnedRewards")).Add(card); lose();
            check(f.HasInheritanceCandidates && ((TMP_Text)Get(f, "resultText")).text.Contains("계승을 마치면"), "inheritance defeat notice");
            f.ContinueAfterResult(); check(f.State == BattleFlowState.Inheriting, "candidate screen");
            check(f.SelectReward(0) && f.SelectReplacementSlot(0) && f.ConfirmReplacement(), "card inheritance confirmed");
            check(f.State == BattleFlowState.Preparing && f.IsRetryPreparation && load().BuildDeck()[0] == card.Card, "card save then retry");
            var passive = pool.First(x => x.Effect != null && save.CanInherit(x));
            unlock(); f.RestartRun(); ((List<BattleRewardOption>)Get(f, "earnedRewards")).Add(passive); lose(); f.ContinueAfterResult();
            check(f.SelectReward(0) && f.State == BattleFlowState.Preparing && f.IsRetryPreparation, "passive bypasses replacement");
            unlock(); f.RestartRun(); ((List<BattleRewardOption>)Get(f, "earnedRewards")).Add(pool.First(x => x.Card != null && save.CanInherit(x))); lose(); f.ContinueAfterResult();
            check(f.SkipReward() && f.State == BattleFlowState.Preparing && f.IsRetryPreparation, "skip then retry");
            f.ReturnToTitle(); check(!f.IsRetryPreparation, "title clears retry label");
            check(save.TryReset() && load().HasRetried, "inheritance reset preserves retry unlock");
            check(save.TryResetGame() && !load().HasRetried, "full reset locks dialogue option");
        }
        finally
        {
            story.Cancel(); f.ReturnToTitle(); Set(f, "inheritance", real); Set(f, "defeatEffectHold", hold);
            t.enabled = tutorialEnabled; story.enabled = storyEnabled;
            if (had) PlayerPrefs.SetInt(key, preference); else PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save(); GameText.SetLanguage(lang);
        }
        return "Retry checks passed: " + count + ". Isolated save; preference restored. Not a manual playthrough.";
    }
}
