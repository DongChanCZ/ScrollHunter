using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>저장 카드와 실제 피해 경로를 검사. 난수·플레이어 설정을 복원하고 첫 전투로 복귀.</summary>
public static class CriticalChecks
{
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    [MenuItem("Tools/Scroll Hunter/Check Critical Hits (Play)")]
    public static void RunMenu() => Debug.Log(Run());

    public static string Run()
    {
        if (!Application.isPlaying) throw new InvalidOperationException("Battle 씬에서 Play 후 실행하세요.");
        var flow = UnityEngine.Object.FindFirstObjectByType<BattleFlow>();
        var deck = UnityEngine.Object.FindFirstObjectByType<DeckSystem>();
        var player = UnityEngine.Object.FindFirstObjectByType<Player>();
        var cost = UnityEngine.Object.FindFirstObjectByType<CostSystem>();
        var enemies = UnityEngine.Object.FindFirstObjectByType<EnemyManager>();
        var metrics = UnityEngine.Object.FindFirstObjectByType<CombatMetrics>();
        var ui = UnityEngine.Object.FindFirstObjectByType<DamageNumberUI>();
        var original = deck.CopyStartingDeck();
        var pool = (BattleRewardOption[])Get(flow, "rewardPool");
        var cards = original.Concat(pool.Where(x => x.Card != null).Select(x => x.Card)).ToArray();
        var encounters = (BattleFlow.Encounter[])Get(flow, "encounters");
        var b = encounters[0].enemies[0]; var c = encounters[1].enemies[0];
        float chance = (float)Get(player, "criticalChance"), multiplier = (float)Get(player, "criticalMultiplier");
        bool logging = (bool)Get(metrics, "logEachInput");
        var randomState = Random.state;
        var amounts = new List<int>(); var flags = new List<bool>(); var logs = new List<string>();
        Action<Vector3, int, bool> capture = (pos, amount, critical) => { amounts.Add(amount); flags.Add(critical); };
        Application.LogCallback captureLog = (m, stack, type) => { if (type == LogType.Log) logs.Add(m); };
        Enemy.DamageTaken += capture; Application.logMessageReceived += captureLog;
        int passed = 0;
        Action<bool, string> check = (ok, label) => { if (!ok) throw new Exception(label); passed++; };
        Action<SkillData, bool> reset = (card, both) =>
        {
            flow.RestartRun();
            if (both) enemies.BeginBattle(new[] { b, c });
            var hand = new List<SkillData>(original); hand[0] = card; deck.SetDeck(hand);
            typeof(CostSystem).GetProperty("Current").SetValue(cost, 10f);
            ui.Clear(); amounts.Clear(); flags.Clear(); logs.Clear();
        };
        SkillData zero = null;
        try
        {
            check(chance == 15f && multiplier == 1.5f, "saved default 15% / 1.5x");
            Set(metrics, "logEachInput", true);
            check(DamageFormula.Compute(5, 1, 0, 1.5f) == 8
                && DamageFormula.Compute(135, 1, 0, 1.5f) == 203
                && DamageFormula.Compute(30, 1, 20, 1.5f) == 38
                && DamageFormula.Compute(0, 1, 0, 1.5f) == 0, "single final rounding / zero");
            Set(player, "criticalChance", 0f);
            var beforeRandom = Random.state;
            check(!player.RollCritical() && Random.state.Equals(beforeRandom), "zero chance never crits or rolls");
            Set(player, "criticalChance", 100f);
            check(player.RollCritical() && Random.state.Equals(beforeRandom), "100 percent always crits");

            foreach (var card in cards)
            {
                reset(card, card.IsAreaOfEffect);
                int hpB = b.CurrentHp, hpC = c.CurrentHp;
                check(deck.TryUseSlot(0), "accept " + card.DisplayName);
                if (card.CastTime > 0) check(amounts.Count == 0, "no damage on input " + card.DisplayName);
                Call(deck, "AdvanceCast", card.CastTime + 0.01f);
                int hits = card.Damage > 0 ? card.HitCount * (card.IsAreaOfEffect ? 2 : 1) : 0;
                int perHit = DamageFormula.Compute(card.Damage, 1, 0, 1.5f);
                check(amounts.Count == hits && flags.All(x => x) && amounts.All(x => x == perHit), "critical amounts and flags " + card.DisplayName);
                check(metrics.CriticalHits == hits && metrics.CriticalEligibleHits == hits, "critical metrics " + card.DisplayName);
                check(b.CurrentHp == hpB - perHit * (card.Damage > 0 ? card.HitCount : 0)
                    && (!card.IsAreaOfEffect || c.CurrentHp == hpC - perHit * card.HitCount), "actual HP " + card.DisplayName);
                if (card.Category == SkillCategory.Shield) check(player.Shield == card.ShieldAmount, "shield unaffected");
                if (card.Category == SkillCategory.Interrupt) check(b.IsStaggered && (float)Get(b, "staggerTimer") == 2.5f, "stagger unaffected");
            }

            var cutter = original[6]; var pillar = original[1]; var spark = original[4];
            // 15%에서 첫 두 결과가 다른 고정 시드. 카드 전체 한 번 추첨 버그를 잡는다.
            int seed = 0;
            for (; seed < 10000; seed++)
            {
                Random.InitState(seed);
                if (Random.value < 0.15f && Random.value >= 0.15f) break;
            }
            check(seed < 10000, "mixed seed found");
            Set(player, "criticalChance", 15f);
            foreach (var card in new[] { cutter, pillar, spark })
            {
                reset(card, card.IsAreaOfEffect);
                int count = card.HitCount * (card.IsAreaOfEffect ? 2 : 1);
                Random.InitState(seed);
                var expected = Enumerable.Range(0, count).Select(i => Random.value * 100f < 15f).ToArray();
                var expectedState = Random.state;
                Random.InitState(seed);
                deck.TryUseSlot(0); Call(deck, "AdvanceCast", card.CastTime + 0.01f);
                check(flags.SequenceEqual(expected), "independent hit/enemy rolls " + card.DisplayName);
                check(amounts.SequenceEqual(expected.Select(x => DamageFormula.Compute(card.Damage, 1, 0, x ? 1.5f : 1f))), "mixed damage " + card.DisplayName);
                check(Random.state.Equals(expectedState), "one draw per live hit; UI does not draw combat random");
                check(metrics.CriticalHits == expected.Count(x => x) && metrics.CriticalEligibleHits == count, "mixed count");
                if (card == cutter)
                {
                    var texts = ui.GetComponentsInChildren<TMP_Text>().Where(x => x.gameObject.activeInHierarchy).ToArray();
                    check(texts.Any(x => x.text == "45" && x.color == (Color)Get(ui, "criticalColor"))
                        && texts.Any(x => x.text == "30" && x.color == (Color)Get(ui, "normalColor")), "pink critical and yellow normal UI");
                }
            }

            reset(original[0], false); Set(player, "criticalChance", 0f); deck.TryUseSlot(0);
            check(amounts.SequenceEqual(new[] { 30 }) && !flags[0] && metrics.CriticalHits == 0, "normal damage unchanged");
            reset(original[1], false); Set(player, "criticalChance", 15f); beforeRandom = Random.state;
            deck.TryUseSlot(0); player.ApplyStun(2f); Call(deck, "AdvanceCast", 2f);
            check(amounts.Count == 0 && metrics.CriticalEligibleHits == 0 && Random.state.Equals(beforeRandom), "cancelled cast has no draw");
            reset(original[2], false); b.TryInterrupt(2.5f); beforeRandom = Random.state;
            check(!deck.TryUseSlot(0) && Random.state.Equals(beforeRandom), "rejected input has no draw");
            reset(original[7], false); deck.TryUseSlot(0); Time.timeScale = 0f; beforeRandom = Random.state;
            Call(deck, "AdvanceCast", 3f);
            check(amounts.Count == 0 && Random.state.Equals(beforeRandom), "pause has no hit or draw");
            Time.timeScale = 1f;

            zero = UnityEngine.Object.Instantiate(original[0]); Set(zero, "damage", 0);
            reset(zero, false); beforeRandom = Random.state; deck.TryUseSlot(0);
            check(amounts.Count == 0 && metrics.CriticalEligibleHits == 0 && Random.state.Equals(beforeRandom), "zero damage excluded");
            reset(cutter, false); Set(player, "criticalChance", 100f);
            typeof(Enemy).GetProperty("CurrentHp").SetValue(b, 35);
            deck.TryUseSlot(0); Call(flow, "Update");
            check(amounts.SequenceEqual(new[] { 45, 30 }) && flags.SequenceEqual(new[] { true, false }), "lethal crit then ordinary residual visual");
            check(metrics.CriticalHits == 1 && metrics.CriticalEligibleHits == 1 && metrics.RecordedHits == 2
                && metrics.DamageApplied == 35 && metrics.OverkillDamage == 40, "lethal/visual accounting");
            check(logs.Last().Contains("전투 종료 요약") && logs.Last().Contains("크리티컬 1회 / 유효 피해 타격 1회"), "critical included in final summary");
            beforeRandom = Random.state; int recorded = amounts.Count;
            check(!deck.TryUseSlot(0) && Random.state.Equals(beforeRandom) && amounts.Count == recorded, "ended battle cannot roll");
            flow.RestartRun();
            check(metrics.CriticalHits == 0 && metrics.CriticalEligibleHits == 0, "restart resets critical metrics");
            player.TakeDamage(150);
            check(player.CurrentHp == player.MaxHp - 125, "enemy damage not affected by player critical chance");
            return "Critical checks passed: " + passed + ". Function integration checks; not natural play or balance validation.";
        }
        finally
        {
            Enemy.DamageTaken -= capture; Application.logMessageReceived -= captureLog;
            Set(player, "criticalChance", chance); Set(player, "criticalMultiplier", multiplier);
            Set(metrics, "logEachInput", logging); Time.timeScale = 1f;
            flow.RestartRun(); ui.Clear(); Random.state = randomState;
            if (zero != null) UnityEngine.Object.DestroyImmediate(zero);
        }
    }
    private static object Get(object obj, string field) => obj.GetType().GetField(field, Hidden).GetValue(obj);
    private static void Set(object obj, string field, object value) => obj.GetType().GetField(field, Hidden).SetValue(obj, value);
    private static void Call(object obj, string method, params object[] args) => obj.GetType().GetMethod(method, Hidden).Invoke(obj, args);
}
