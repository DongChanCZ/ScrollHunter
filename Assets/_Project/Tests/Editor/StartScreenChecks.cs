using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class StartScreenChecks
{
    [MenuItem("Tools/Scroll Hunter/Check Start Screen (Fresh Play)")]
    public static void RunMenu() => Debug.Log(Run());

    public static string Run()
    {
        if (!Application.isPlaying) throw new InvalidOperationException("새 Play의 시작 화면에서 실행하세요.");
        var flow = UnityEngine.Object.FindFirstObjectByType<BattleFlow>();
        var metrics = UnityEngine.Object.FindFirstObjectByType<CombatMetrics>();
        var deck = UnityEngine.Object.FindFirstObjectByType<DeckSystem>();
        var cost = UnityEngine.Object.FindFirstObjectByType<CostSystem>();
        var player = UnityEngine.Object.FindFirstObjectByType<Player>();
        var enemies = UnityEngine.Object.FindFirstObjectByType<EnemyManager>();
        var info = UnityEngine.Object.FindFirstObjectByType<CombatInfoUI>();
        Func<string, object> field = name => typeof(BattleFlow).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(flow);
        var start = (Button)field("startButton");
        var panel = (GameObject)field("startPanel");
        int passed = 0;
        Action<bool, string> check = (ok, label) => { if (!ok) throw new Exception(label); passed++; };
        Action<Button> click = button => ExecuteEvents.Execute(button.gameObject,
            new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left }, ExecuteEvents.pointerClickHandler);
        check(flow.State == BattleFlowState.Title && flow.BattleNumber == 0, "fresh play waits on title");
        check(panel.activeInHierarchy && start.IsInteractable(), "start button visible");
        check(Time.timeScale == 0 && metrics.Ended && metrics.BattleNumber == 0 && metrics.Elapsed == 0, "no battle time or metrics before start");
        check(enemies.CombatEnded && enemies.CurrentTarget == null && enemies.Enemies.All(e => !e.gameObject.activeSelf), "enemies inactive");
        float beforeCost = cost.Current;
        check(!deck.TryUseSlot(0) && !deck.IsCasting && cost.Current == beforeCost, "card input blocked");
        info.TogglePause(); info.ToggleSpeed(); info.Resume();
        check(Time.timeScale == 0 && !info.IsInfoPaused && info.BattleSpeed == 1, "pause and speed cannot start battle");
        check(!flow.SkipReward() && !flow.SelectReward(0), "rewards blocked on title");
        flow.NextBattle(); check(flow.State == BattleFlowState.Title, "next battle blocked on title");
        click(start);
        check(flow.State == BattleFlowState.Fighting && flow.BattleNumber == 1 && !panel.activeSelf, "start click opens first battle");
        check(player.CurrentHp == player.MaxHp && cost.Current == 3 && Time.timeScale == 1, "starting resources and speed");
        check(!metrics.Ended && metrics.BattleNumber == 1 && metrics.Elapsed == 0 && metrics.RecordedHits == 0, "fresh metrics after title wait");
        check(enemies.EnemyCount == 1 && enemies.CurrentTarget.Data.name.Contains("B"), "B encounter selected");
        var original = deck.CopyStartingDeck();
        check(Enumerable.Range(0, 8).All(i => flow.GetDeckCard(i) == original[i]), "original eight cards");
        check(deck.TryUseSlot(0), "card usable after start");
        float paid = cost.Current;
        flow.StartRun(); check(cost.Current == paid && deck.GetHandCard(0) == original[4], "duplicate start does not reset run");
        player.TakeDamage(100000);
        typeof(BattleFlow).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(flow, null);
        check(flow.State == BattleFlowState.Defeat && !panel.activeSelf, "defeat remains on result screen");
        click((Button)field("restartButton"));
        check(flow.State == BattleFlowState.Fighting && flow.BattleNumber == 1 && player.CurrentHp == player.MaxHp
            && deck.GetHandCard(0) == original[0] && !panel.activeSelf, "restart goes directly to clean B battle");
        return "Start screen checks passed: " + passed + ". Function/button event checks; not physical input.";
    }
}
