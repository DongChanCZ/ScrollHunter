using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class RewardFlowChecks
{
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    [MenuItem("Tools/Scroll Hunter/Check Rewards (Play)")]
    public static void RunMenu() => Debug.Log(Run());

    public static string Run()
    {
        if (!Application.isPlaying) throw new InvalidOperationException("Battle 씬에서 Play 후 실행하세요.");
        var flow = UnityEngine.Object.FindFirstObjectByType<BattleFlow>();
        var deck = UnityEngine.Object.FindFirstObjectByType<DeckSystem>();
        var player = UnityEngine.Object.FindFirstObjectByType<Player>();
        float originalCriticalChance = (float)typeof(Player).GetField("criticalChance", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(player);
        var cost = UnityEngine.Object.FindFirstObjectByType<CostSystem>();
        var enemies = UnityEngine.Object.FindFirstObjectByType<EnemyManager>();
        var metrics = UnityEngine.Object.FindFirstObjectByType<CombatMetrics>();
        var ui = UnityEngine.Object.FindFirstObjectByType<BattleRewardUI>();
        var info = UnityEngine.Object.FindFirstObjectByType<CombatInfoUI>();
        var pool = (BattleRewardOption[])Get(flow, "rewardPool");
        var original = deck.CopyStartingDeck();
        var impact = pool[0].Card;
        var eruption = pool[1].Card;
        var suppression = pool[2].Card;
        var next = (Button)Get(flow, "nextButton");
        var choices = (Button[])Get(ui, "choiceButtons");
        var slots = (Button[])Get(ui, "deckButtons");
        var encounters = (BattleFlow.Encounter[])Get(flow, "encounters");
        Enemy b = encounters[0].enemies[0], c = encounters[1].enemies[0];
        int passed = 0;
        Action<bool, string> check = (ok, label) => { if (!ok) throw new Exception(label); passed++; };
        Action win = () => { foreach (var e in enemies.GetAliveEnemies()) e.TakeDamage(e.CurrentHp); enemies.NotifyEnemyDied(); Call(flow, "Update"); };
        Action<SkillData, bool> reset = (card, both) =>
        {
            flow.RestartRun();
            if (both) enemies.BeginBattle(new[] { b, c });
            var cards = new List<SkillData>(original); cards[0] = card; deck.SetDeck(cards);
            typeof(CostSystem).GetProperty("Current").SetValue(cost, 10f);
        };
        RewardProbeEffect probe = null;
        try
        {
            typeof(Player).GetField("criticalChance", Hidden).SetValue(player, 0f);
            Set(flow, "rewardPool", new[] { pool[0], pool[2] });
            flow.RestartRun();
            check(flow.DeckCount == 8 && Enumerable.Range(0, 8).All(i => flow.GetDeckCard(i) == original[i]), "initial deck");
            check(!flow.SelectReward(0) && !flow.ReplaceDeckCard(0) && !flow.SkipReward(), "no rewards during combat");
            flow.NextBattle(); check(flow.BattleNumber == 1, "cannot skip combat");
            player.TakeDamage(60); float carried = player.CurrentHp; player.AddShield(10); win();
            check(flow.State == BattleFlowState.BetweenBattles && Time.timeScale == 0 && player.Shield == 0, "victory stopped and cleaned");
            check(flow.RewardChoiceCount == 2 && choices.Take(2).All(x => x.gameObject.activeInHierarchy), "two card choices visible");
            check(!next.gameObject.activeInHierarchy && !deck.TryUseSlot(0), "no next/combat before reward");
            flow.NextBattle(); check(flow.BattleNumber == 1, "unresolved reward blocks next");
            Click(choices[Enumerable.Range(0, flow.RewardChoiceCount).First(i => flow.GetRewardChoice(i).Card == impact)]); check(flow.SelectedReward == impact && slots.All(x => x.gameObject.activeInHierarchy && x.interactable), "card opens eight replacement slots");
            check(!flow.ReplaceDeckCard(-1) && !flow.ReplaceDeckCard(8), "invalid replacement rejected");
            Click((Button)Get(ui, "backButton")); check(flow.SelectedReward == null && flow.GetDeckCard(0) == original[0], "back does not acquire");
            Click(choices[Enumerable.Range(0, flow.RewardChoiceCount).First(i => flow.GetRewardChoice(i).Card == impact)]); Click(slots[0]);
            check(flow.RewardResolved && flow.SelectedReward == null && flow.GetDeckCard(0) == impact, "replacement resolves once");
            check(flow.DeckCount == 8 && Enumerable.Range(1, 7).All(i => flow.GetDeckCard(i) == original[i]), "same index, other order preserved");
            check(!flow.SkipReward() && !flow.ReplaceDeckCard(1) && !flow.SelectReward(1), "double acquisition blocked");
            check(next.gameObject.activeInHierarchy && slots.All(x => !x.interactable), "resolved review and next button");
            check(deck.CopyStartingDeck().SequenceEqual(original), "serialized starting deck unchanged");
            Click(next);
            check(flow.BattleNumber == 2 && deck.GetHandCard(0) == impact && player.CurrentHp == carried && cost.Current == 3, "new deck and HP enter C");
            int hp = c.CurrentHp;
            check(deck.TryUseSlot(0) && c.CurrentHp == hp - 120 && cost.Current == 0 && metrics.RecordedHits == 3, "acquired impact immediately playable");
            Set(flow, "rewardPool", pool.Take(3).ToArray()); win();
            check(flow.RewardChoiceCount == 2 && Enumerable.Range(0, 2).All(i => flow.GetRewardChoice(i).Card != impact), "owned reward excluded next time");
            check(!choices[2].gameObject.activeSelf, "unused third choice hidden");
            Click(choices[Enumerable.Range(0, flow.RewardChoiceCount).First(i => flow.GetRewardChoice(i).Card == suppression)]); Click(slots[2]); Click(next);
            check(flow.BattleNumber == 3 && deck.GetHandCard(0) == impact && deck.GetHandCard(2) == suppression, "two acquired cards retained in BC");
            win(); check(flow.State == BattleFlowState.Victory && !((GameObject)Get(ui, "panel")).activeSelf && !flow.SelectReward(0), "final victory has no reward");
            flow.RestartRun(); check(player.CurrentHp == player.MaxHp && Enumerable.Range(0, 8).All(i => flow.GetDeckCard(i) == original[i]), "restart resets original deck and HP");
            win(); flow.SelectReward(0); Click((Button)Get(ui, "skipButton")); Click(next); win();
            check(flow.RewardChoiceCount == 2 && flow.GetDeckCard(0) == original[0], "skip pending selection keeps deck and all choices");
            flow.SkipReward(); flow.NextBattle(); player.TakeDamage(100000); Call(flow, "Update");
            check(flow.State == BattleFlowState.Defeat && !flow.SelectReward(0), "defeat has no reward");
            flow.RestartRun(); Set(flow, "rewardPool", new BattleRewardOption[0]); win();
            check(flow.RewardChoiceCount == 0 && flow.SkipReward(), "empty pool can skip");
            flow.NextBattle(); check(flow.BattleNumber == 2, "empty pool no dead end"); Set(flow, "rewardPool", pool);

            check(impact.Cost == 3 && impact.CastTime == 0 && impact.Damage == 40 && impact.HitCount == 3 && !impact.IsAreaOfEffect, "SK07 values");
            check(eruption.Cost == 2 && eruption.CastTime == 0.5f && eruption.Damage == 40 && eruption.HitCount == 1 && eruption.IsAreaOfEffect, "SK10 values");
            check(suppression.Cost == 2 && suppression.CastTime == 0.2f && suppression.Damage == 30 && suppression.Category == SkillCategory.Interrupt, "SK12 values");
            reset(impact, true); typeof(Enemy).GetProperty("CurrentHp").SetValue(b, 50); hp = c.CurrentHp;
            check(deck.TryUseSlot(0) && !b.IsAlive && c.CurrentHp == hp && metrics.DamageApplied == 50 && metrics.OverkillDamage == 70 && metrics.RecordedHits == 3, "impact lethal per-hit accounting");
            reset(eruption, true); int bhp = b.CurrentHp; int chp = c.CurrentHp;
            check(deck.TryUseSlot(0) && cost.Current == 8 && b.CurrentHp == bhp && c.CurrentHp == chp, "eruption delayed and paid immediately");
            Call(deck, "AdvanceCast", 0.49f); check(b.CurrentHp == bhp && c.CurrentHp == chp, "eruption no early damage");
            Call(deck, "AdvanceCast", 0.02f); check(b.CurrentHp == bhp - 40 && c.CurrentHp == chp - 40 && metrics.RecordedHits == 2 && (int)Get(metrics, "totalUses") == 1, "eruption hits both once");
            reset(eruption, true); deck.TryUseSlot(0); b.TakeDamage(b.CurrentHp); enemies.NotifyEnemyDied(); chp = c.CurrentHp;
            Call(deck, "AdvanceCast", 0.51f); check(c.CurrentHp == chp - 40 && metrics.RecordedHits == 1, "eruption excludes dead enemy");
            reset(eruption, true); deck.TryUseSlot(0); player.ApplyStun(2); Call(deck, "AdvanceCast", 0.51f);
            check(metrics.RecordedHits == 0 && cost.Current == 8 && deck.GetHandCard(0) == original[4] && !deck.IsCasting, "eruption stun cancels without refund or extra rotation");
            reset(suppression, false); bhp = b.CurrentHp;
            check(deck.TryUseSlot(0), "orange suppression accepted"); Call(deck, "AdvanceCast", 0.21f);
            check(b.CurrentHp == bhp - 30 && b.IsStaggered && (float)Get(b, "staggerTimer") == 2.5f, "suppression damage then 2.5 stagger");
            check((int)Get(metrics, "interruptAttempts") == 1 && (int)Get(metrics, "interruptSuccesses") == 1, "suppression attempt and success metrics");
            reset(suppression, false); b.TryInterrupt(2.5f);
            check(!deck.TryUseSlot(0) && cost.Current == 10 && deck.GetHandCard(0) == suppression, "noncasting suppression spends nothing");
            reset(suppression, true); typeof(EnemyManager).GetProperty("CurrentTarget").SetValue(enemies, c); chp = c.CurrentHp;
            check(deck.TryUseSlot(0), "red suppression accepted"); Call(deck, "AdvanceCast", 0.21f);
            check(c.CurrentHp == chp - 30 && c.IsCasting && !c.IsStaggered && (int)Get(metrics, "interruptSuccesses") == 0 && cost.Current == 8, "red takes damage without interrupt");
            reset(suppression, false); Set(b, "current", b.Data.Find(CastColor.Green)); bhp = b.CurrentHp;
            check(deck.TryUseSlot(0), "late green suppression accepted"); Call(b, "Fire"); Call(deck, "AdvanceCast", 0.21f);
            check(b.CurrentHp == bhp - 30 && !b.IsStaggered && (int)Get(metrics, "interruptSuccesses") == 0, "cast ended still takes damage without interrupt");
            reset(suppression, true); bhp = b.CurrentHp; chp = c.CurrentHp; deck.TryUseSlot(0);
            typeof(EnemyManager).GetProperty("CurrentTarget").SetValue(enemies, c); Call(deck, "AdvanceCast", 0.21f);
            check(b.CurrentHp == bhp && c.CurrentHp == chp - 30 && !c.IsStaggered, "effect-time target used");
            reset(suppression, false); deck.TryUseSlot(0); player.ApplyStun(2); Call(deck, "AdvanceCast", 0.21f);
            check(metrics.RecordedHits == 0 && cost.Current == 8 && deck.GetHandCard(0) == original[4], "suppression stun cancellation");
            reset(suppression, false); typeof(Enemy).GetProperty("CurrentHp").SetValue(b, 30);
            var logs = new List<string>(); Application.LogCallback capture = (m, s, t) => { if (t == LogType.Log) logs.Add(m); };
            Application.logMessageReceived += capture;
            try { deck.TryUseSlot(0); Call(deck, "AdvanceCast", 0.21f); Call(flow, "Update"); }
            finally { Application.logMessageReceived -= capture; }
            check(!b.IsAlive && metrics.DamageApplied == 30 && (int)Get(metrics, "interruptSuccesses") == 0, "lethal suppression has damage but no interrupt success");
            check(logs.Last().Contains("전투 종료 요약") && logs.Last().Contains("실피해 30"), "lethal suppression summary last and complete");
            check(info.DescribeCard(suppression).Contains("피해 후") && info.DescribeCard(suppression).Contains("30"), "damaging interrupt tooltip");

            flow.RestartRun(); probe = ScriptableObject.CreateInstance<RewardProbeEffect>();
            RewardProbeEffect.Applied = RewardProbeEffect.Removed = 0;
            var option = new BattleRewardOption(); Set(option, "effect", probe); Set(flow, "rewardPool", new[] { option }); win();
            check(flow.RewardChoiceCount == 1 && flow.SelectReward(0), "non-card reward accepted");
            check(RewardProbeEffect.Applied == 1 && RewardProbeEffect.Received != probe && flow.RewardResolved && flow.SelectedReward == null, "effect cloned and immediate, no replacement");
            check(Enumerable.Range(0, 8).All(i => flow.GetDeckCard(i) == original[i]), "effect preserves deck");
            flow.NextBattle(); check(RewardProbeEffect.Removed == 0 && flow.BattleNumber == 2, "effect retained between battles");
            flow.RestartRun(); check(RewardProbeEffect.Removed == 1, "restart removes acquired effect");
            flow.RestartRun(); check(RewardProbeEffect.Removed == 1, "effect cleanup not duplicated");
            return "Reward checks passed: " + passed + ". Function/UI event checks; not physical input or balance validation.";
        }
        finally
        {
            typeof(Player).GetField("criticalChance", Hidden).SetValue(player, originalCriticalChance);
            Set(flow, "rewardPool", pool); flow.RestartRun();
            if (probe != null) UnityEngine.Object.DestroyImmediate(probe);
            RewardProbeEffect.Received = null;
        }
    }
    private static object Get(object target, string name) => target.GetType().GetField(name, Hidden).GetValue(target);
    private static void Set(object target, string name, object value) => target.GetType().GetField(name, Hidden).SetValue(target, value);
    private static void Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, Hidden).Invoke(target, args);
    private static void Click(Button button) => ExecuteEvents.Execute(button.gameObject,
        new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left }, ExecuteEvents.pointerClickHandler);
}
