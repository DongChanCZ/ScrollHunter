using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 주요 화면을 차례로 열어 각 화면에서 LocalizedLayoutAudit을 돌리고 일본어·중국어·스페인어 화면을 저장한다(Fresh Play·시작 화면).
/// 진행·계승 기록은 임시 저장 파일에만 쓴다. 끝나면 Play를 종료해 원래 저장 연결로 돌아간다.
/// </summary>
public static class LocalizedScreenTour
{
    private const BindingFlags Hidden = BindingFlags.NonPublic | BindingFlags.Instance;
    private const string Folder = "Docs/근거자료/2026-10-15_다국어";
    private static readonly GameLanguage[] Shots = { GameLanguage.Japanese, GameLanguage.ChineseSimplified, GameLanguage.Spanish };
    private class Step { public string name; public Func<bool> ready; public Action enter; public bool audit = true; }
    private static List<Step> steps;
    private static int index, shot, waitFrame;
    private static bool entered;
    private static readonly StringBuilder report = new StringBuilder();
    private static double deadline;
    public static string Status { get; private set; } = "idle";

    private static T Field<T>(object o, string name) => (T)o.GetType().GetField(name, Hidden).GetValue(o);

    [MenuItem("Tools/Scroll Hunter/Localized Screen Tour (Fresh Play)")]
    public static void Run()
    {
        if (!Application.isPlaying) throw new InvalidOperationException("Fresh Play required");
        var flow = UnityEngine.Object.FindFirstObjectByType<BattleFlow>();
        if (flow.State != BattleFlowState.Title) throw new InvalidOperationException("Start at title");
        Application.runInBackground = true;
        var menu = flow.GetComponent<MainMenuUI>();
        var story = flow.GetComponent<StoryUI>();
        var info = UnityEngine.Object.FindFirstObjectByType<CombatInfoUI>();
        var player = UnityEngine.Object.FindFirstObjectByType<Player>();
        Directory.CreateDirectory(Folder);
        steps = new List<Step>
        {
            new Step { name = "title", enter = () => menu.ClosePanels(), ready = () => true },
            new Step { name = "options", enter = () => menu.OpenOptions(), ready = () => menu.OptionsVisible },
            new Step { name = "controls", enter = () => { menu.ClosePanels(); menu.OpenControls(); }, ready = () => true },
            new Step { name = "inheritance-info", enter = () => { menu.ClosePanels(); Field<Button>(menu, "inheritanceOpen").onClick.Invoke(); }, ready = () => true },
            new Step { name = "deck", enter = () =>
            {
                var isolated = new InheritanceSave(Path.Combine(Path.GetTempPath(), "ScrollHunter_Lang5_20261015", "tour-" + Guid.NewGuid().ToString("N") + ".json"),
                    Field<BattleRewardOption[]>(flow, "rewardPool"), UnityEngine.Object.FindFirstObjectByType<DeckSystem>().CopyStartingDeck());
                isolated.TryRecordIntro(); isolated.TryRecordTutorial();
                flow.GetType().GetField("inheritance", Hidden).SetValue(flow, isolated);
                menu.ClosePanels(); flow.StartRun();
            }, ready = () => flow.State == BattleFlowState.Preparing },
            new Step { name = "dialogue", enter = () => flow.NextBattle(), ready = () => story.Active },
            new Step { name = "battle-card-info", enter = () => Field<Button>(story, "skip").onClick.Invoke(), ready = () =>
            {
                if (flow.State != BattleFlowState.Fighting) return false;
                if (!info.IsInfoPaused) info.TogglePause();
                info.GetType().GetField("selectedSlot", Hidden).SetValue(info, 2);
                return true;
            } },
            new Step { name = "battle-stats", enter = () => UnityEngine.Object.FindFirstObjectByType<StatsPanelUI>().SetOpen(true), ready = () => true },
            new Step { name = "reward", enter = () =>
            {
                if (info.IsInfoPaused) info.TogglePause();
                foreach (var enemy in UnityEngine.Object.FindObjectsByType<Enemy>(FindObjectsSortMode.None)) if (enemy.CurrentHp > 0) enemy.TakeDamage(99999);
            }, ready = () => flow.State == BattleFlowState.BetweenBattles && !flow.ResultPanelHeld },
            new Step { name = "replace-confirm", enter = () => { flow.SelectReward(0); flow.SelectReplacementSlot(0); }, ready = () => flow.SelectedReplacementIndex == 0 },
            new Step { name = "next-deck", enter = () => flow.ConfirmReplacement(), ready = () => flow.State == BattleFlowState.Preparing || flow.State == BattleFlowState.BetweenBattles },
            new Step { name = "defeat", audit = true, enter = () =>
            {
                flow.NextBattle();
                if (story.Active) Field<Button>(story, "skip").onClick.Invoke();
            }, ready = () =>
            {
                if (story.Active) { Field<Button>(story, "skip").onClick.Invoke(); return false; }
                if (flow.State == BattleFlowState.Fighting && player.CurrentHp > 0) player.TakeDamage(99999, CastColor.Red);
                return flow.State == BattleFlowState.Defeat && !flow.ResultPanelHeld;
            } },
            new Step { name = "inheritance", enter = () => flow.ContinueAfterResult(), ready = () => flow.State == BattleFlowState.Inheriting },
            new Step { name = "inheritance-confirm", enter = () => { flow.SelectReward(0); flow.SelectReplacementSlot(0); }, ready = () => true },
        };
        index = 0; shot = -1; waitFrame = 0; entered = false; report.Clear();
        deadline = EditorApplication.timeSinceStartup + 300;
        Status = "running";
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
    }

    private static void Tick()
    {
        try
        {
            if (!Application.isPlaying || EditorApplication.timeSinceStartup > deadline) { Finish("stopped at " + (index < steps.Count ? steps[index].name : "end")); return; }
            if (index >= steps.Count) { Finish("done"); return; }
            if (Time.frameCount < waitFrame) return;
            var step = steps[index];
            if (!entered) { step.enter(); entered = true; waitFrame = Time.frameCount + 3; return; }
            if (shot < 0)
            {
                if (!step.ready()) { waitFrame = Time.frameCount + 5; return; }
                if (step.audit) report.AppendLine("[" + step.name + "] " + LocalizedLayoutAudit.Audit());
                shot = 0;
            }
            if (shot < Shots.Length)
            {
                GameText.SetLanguage(Shots[shot]);
                ScreenCapture.CaptureScreenshot(Path.GetFullPath(Path.Combine(Folder, "tour_" + index.ToString("00") + "_" + step.name + "_" + Shots[shot] + ".png")));
                shot++; waitFrame = Time.frameCount + 3; return;
            }
            GameText.SetLanguage(GameLanguage.Korean);
            index++; shot = -1; entered = false;
        }
        catch (Exception e) { Finish("error at " + steps[index].name + ": " + (e.InnerException ?? e).Message); }
    }

    private static void Finish(string result)
    {
        EditorApplication.update -= Tick;
        Status = result;
        report.Insert(0, "Tour " + result + "\n");
        File.WriteAllText(Path.Combine(Folder, "screen-tour.txt"), report.ToString());
        Debug.Log("[Screen Tour] " + result);
    }
}
