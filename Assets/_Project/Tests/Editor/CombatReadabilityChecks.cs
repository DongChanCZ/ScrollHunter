using System;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;

/// <summary>현재 적 HP와 무관한 배속·표시 검사. 전투를 초기화하므로 측정 플레이 중 실행하지 않는다.</summary>
public static class CombatReadabilityChecks
{
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    private static int passed;
    private static object Get(object obj, string field) => obj.GetType().GetField(field, Hidden).GetValue(obj);
    private static void Set(object obj, string field, object value) => obj.GetType().GetField(field, Hidden).SetValue(obj, value);
    private static void Call(object obj, string method) => obj.GetType().GetMethod(method, Hidden).Invoke(obj, null);
    private static void Check(bool value, string label)
    {
        if (!value) throw new Exception("Readability: " + label);
        passed++;
    }

    [MenuItem("Tools/Scroll Hunter/Check Combat Readability (Play)")]
    public static void RunMenu() => Debug.Log(Run());

    public static string Run()
    {
        if (!EditorApplication.isPlaying) throw new InvalidOperationException("Battle 씬 Play 후 실행하세요.");
        var flow = UnityEngine.Object.FindFirstObjectByType<BattleFlow>();
        var info = UnityEngine.Object.FindFirstObjectByType<CombatInfoUI>();
        var ui = UnityEngine.Object.FindFirstObjectByType<CombatUI>();
        var deck = UnityEngine.Object.FindFirstObjectByType<DeckSystem>();
        var cost = UnityEngine.Object.FindFirstObjectByType<CostSystem>();
        var player = UnityEngine.Object.FindFirstObjectByType<Player>();
        var enemies = UnityEngine.Object.FindFirstObjectByType<EnemyManager>();
        var metrics = UnityEngine.Object.FindFirstObjectByType<CombatMetrics>();
        var views = (CombatUI.HandSlotView[])Get(ui, "handSlots");
        float originalSpeed = info.BattleSpeed;
        passed = 0;
        try
        {
            flow.RestartRun();
            if (info.BattleSpeed != 1f) info.ToggleSpeed();
            info.ToggleSpeed();
            Check(info.BattleSpeed == 0.5f && Time.timeScale == 0.5f, "half speed");
            info.TogglePause();
            Check(info.IsInfoPaused && Time.timeScale == 0f && !deck.TryUseSlot(0), "pause blocks cards");
            info.ToggleSpeed();
            Check(info.BattleSpeed == 1f && Time.timeScale == 0f && info.IsInfoPaused, "paused selection stays paused");
            info.Resume();
            Check(Time.timeScale == 1f, "resume selected speed");
            info.ToggleSpeed();
            info.TogglePause();
            info.Resume();
            Check(Time.timeScale == 0.5f, "resume half speed");
            flow.RestartRun();
            Check(info.BattleSpeed == 0.5f && Time.timeScale == 0.5f, "restart retains speed");
            enemies.CurrentTarget.TakeDamage(enemies.CurrentTarget.CurrentHp);
            enemies.NotifyEnemyDied();
            Call(flow, "Update");
            Check(flow.State == BattleFlowState.BetweenBattles && Time.timeScale == 0f, "victory waits");
            info.ToggleSpeed();
            Check(info.BattleSpeed == 0.5f && Time.timeScale == 0f, "result cannot restart time");
            flow.SkipReward();
            flow.NextBattle();
            Check(flow.BattleNumber == 2 && Time.timeScale == 0.5f, "C retains speed");
            enemies.CurrentTarget.TakeDamage(enemies.CurrentTarget.CurrentHp);
            enemies.NotifyEnemyDied();
            Call(flow, "Update");
            flow.SkipReward();
            flow.NextBattle();
            Check(flow.BattleNumber == 3 && Time.timeScale == 0.5f, "BC retains speed");
            player.TakeDamage(100000);
            Call(flow, "Update");
            info.ToggleSpeed();
            info.Resume();
            Check(flow.State == BattleFlowState.Defeat && Time.timeScale == 0f, "defeat stays stopped");
            Check(((string)Get(metrics, "pendingSummary")) == null, "summary flushed at end");
            flow.RestartRun();
            Call(info, "LateUpdate");
            Check(((TMP_Text)Get(info, "speedLabel")).text.Contains("0.5"), "speed label");

            Set(cost, "<Current>k__BackingField", 0f);
            Call(ui, "LateUpdate");
            Check(views[0].costCover.enabled && views[0].costCover.fillAmount == 1f, "zero cost full cover");
            Set(cost, "<Current>k__BackingField", 0.5f);
            Call(ui, "LateUpdate");
            Check(views[0].costCover.fillAmount == 0.5f, "half charged");
            Check(views[0].costText.color == (Color)Get(ui, "costShortColor"), "red insufficient cost");
            Set(cost, "<Current>k__BackingField", 1f);
            Call(ui, "LateUpdate");
            Check(!views[0].costCover.enabled && views[1].costCover.fillAmount == 0.75f, "per-card cost denominator");
            Check(views[0].typeText.text == "단일" && views[1].typeText.text == "광역"
                && views[2].typeText.text == "차단" && views[3].typeText.text == "방어", "four badges");
            foreach (var view in views)
                Check(!view.costCover.raycastTarget && view.costCover.transform.GetSiblingIndex() == 0
                    && view.costCover.sprite != null && view.costCover.type == UnityEngine.UI.Image.Type.Filled
                    && view.costCover.fillMethod == UnityEngine.UI.Image.FillMethod.Radial360
                    && view.costCover.fillOrigin == (int)UnityEngine.UI.Image.Origin360.Top
                    && !view.costCover.fillClockwise, "clockwise reveal, behind labels and no input blocking");
            Set(cost, "<Current>k__BackingField", 3f);
            Check(deck.TryUseSlot(0), "input accepted");
            Call(ui, "LateUpdate");
            Check(views[0].typeText.text == "광역" && !views[0].costCover.enabled
                && views[0].background.color == (Color)Get(ui, "slotLockedColor")
                && !string.IsNullOrEmpty(views[0].lockText.text), "rotation updates badge and GCD timer");
            player.ApplyStun(2f);
            Call(ui, "LateUpdate");
            Check(views[0].background.color == (Color)Get(ui, "slotStunnedColor") && !views[0].costCover.enabled
                && Array.TrueForAll(views, view => string.IsNullOrEmpty(view.lockText.text)), "purple stun without slot timer");
            Check(((TMP_Text)Get(ui, "stunText")).enabled, "central stun label");
            player.BeginBattle(false);
            deck.EndBattle();
            enemies.CurrentTarget.TryInterrupt(2.5f);
            Call(ui, "LateUpdate");
            Check(deck.GetSlotState(2) == SlotState.NoValidTarget && views[2].lockText.text.Length > 0
                && !views[2].costCover.enabled, "noncasting interrupt locked");
        }
        finally
        {
            flow.RestartRun();
            if (info.BattleSpeed != originalSpeed) info.ToggleSpeed();
            flow.RestartRun();
            Call(ui, "LateUpdate");
        }
        return "Combat readability checks: " + passed + " passed (current HP preserved)";
    }
}
