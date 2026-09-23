using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>패시브 전체·포션의 함수/보상 경로 검사. 추가 보상 기회는 인위적이며 자연 계측이 아니다.</summary>
public static class AllPassiveChecks
{
    [MenuItem("Tools/Scroll Hunter/Check All Passives (Play)")]
    public static void RunMenu() => Debug.Log(Run());
    private const BindingFlags Hidden = BindingFlags.NonPublic | BindingFlags.Instance;
    private static FieldInfo Field(object obj, string name)
    {
        for (Type t = obj.GetType(); t != null; t = t.BaseType)
        {
            var field = t.GetField(name, Hidden | BindingFlags.DeclaredOnly);
            if (field != null) return field;
        }
        throw new Exception(name);
    }
    private static object Get(object obj, string name) => Field(obj, name).GetValue(obj);
    private static void Set(object obj, string name, object value) => Field(obj, name).SetValue(obj, value);
    private static void Call(object obj, string name) => obj.GetType().GetMethod(name, Hidden).Invoke(obj, null);
    private static void Property(object obj, string name, object value) => obj.GetType().GetProperty(name).SetValue(obj, value);
    private sealed class FixedRandom : System.Random
    {
        private readonly double value;
        public FixedRandom(double value) { this.value = value; }
        public override double NextDouble() => value;
        public override int Next(int maxValue) => 0;
    }

    public static string Run()
    {
        if (!Application.isPlaying) throw new InvalidOperationException("Battle 씬 Play에서 실행하세요.");
        var flow = UnityEngine.Object.FindFirstObjectByType<BattleFlow>();
        var player = UnityEngine.Object.FindFirstObjectByType<Player>();
        var cost = UnityEngine.Object.FindFirstObjectByType<CostSystem>();
        var deck = UnityEngine.Object.FindFirstObjectByType<DeckSystem>();
        var enemies = UnityEngine.Object.FindFirstObjectByType<EnemyManager>();
        var metrics = UnityEngine.Object.FindFirstObjectByType<CombatMetrics>();
        var info = UnityEngine.Object.FindFirstObjectByType<CombatInfoUI>();
        var ui = UnityEngine.Object.FindFirstObjectByType<CombatUI>();
        var pool = (BattleRewardOption[])Get(flow, "rewardPool");
        var random = Get(flow, "rewardRandom");
        var unityRandom = UnityEngine.Random.state;
        var ps = pool.Where(o => o.Effect != null).ToDictionary(o => o.Effect.name.Substring(0, 4));
        int passed = 0;
        Action<bool, string> check = (ok, name) => { if (!ok) throw new Exception("All passives: " + name); passed++; };
        Func<float, float, bool> near = (a, b) => Mathf.Abs(a - b) < .0001f;
        Action reopen = () => {
            Property(flow, "State", BattleFlowState.BetweenBattles);
            Property(flow, "RewardResolved", false); Property(flow, "SelectedReward", null);
            Call(flow, "PrepareRewards"); Call(flow, "RefreshUI");
        };
        Action<string> acquire = id => {
            Set(flow, "rewardPool", new[] { ps[id] });
            Set(flow, "rewardRandom", new FixedRandom(0));
            reopen();
            if (!flow.SelectReward(0)) throw new Exception("Acquire " + id);
        };
        Action fight = () => { Property(flow, "State", BattleFlowState.Fighting); Time.timeScale = 1; };
        StatRewardEffect instance = null;
        try
        {
            flow.RestartRun();
            check(ps.Count == 10 && pool.Count(o => o.Card != null) == 3, "all ten saved passives and three cards");
            check(ps["PS10"].Effect.IsRareReward && near(ps["PS10"].Effect.OfferChance, .01f), "serialized rare chance");
            check(ps.Where(p => p.Key != "PS10").All(p => !p.Value.Effect.IsRareReward), "other passives ordinary");
            check(player.PotionsRemaining == 3 && player.PotionCapacity == 3 && player.PotionHealAmount == 150, "potion base");

            // PS01 does not refill even when stacked; capacity is per run.
            Set(player, "currentHp", 100f); check(player.TryUsePotion() && player.PotionsRemaining == 2, "potion consumed before capacity reward");
            acquire("PS01"); check(player.PotionCapacity == 4 && player.PotionsRemaining == 2, "capacity only");
            acquire("PS01"); acquire("PS01"); reopen();
            check(player.PotionCapacity == 6 && player.PotionsRemaining == 2 && flow.RewardChoiceCount == 0, "PS01 stack cap");
            flow.RestartRun(); check(player.PotionsRemaining == 3 && player.PotionCapacity == 3, "potion reset after reward cleanup");

            Set(player, "currentHp", 350f); acquire("PS03");
            check(player.MaxHp == 550 && player.CurrentHp == 350 && player.PotionHealAmount == 165, "HP maximum only and live potion amount");
            acquire("PS02"); check(player.CurrentHp == 500, "heal 150");
            acquire("PS02"); check(player.CurrentHp == 550, "heal clamped");
            acquire("PS02"); check(player.CurrentHp == 550 && flow.GetPassiveStacks(ps["PS02"].Effect) == 3, "full HP reward accepted with no overflow");
            for (int i = 0; i < 5; i++) acquire("PS03");
            check(player.MaxHp == 800 && player.CurrentHp == 550 && flow.GetPassiveStacks(ps["PS03"].Effect) == 6, "uncapped HP stacks without healing");
            instance = UnityEngine.Object.Instantiate((StatRewardEffect)ps["PS02"].Effect);
            instance.Apply(player, cost); instance.Apply(player, cost);
            check(player.CurrentHp == 700, "instant heal apply idempotent");
            instance.Remove(); instance.Remove(); check(player.CurrentHp == 700, "instant heal not reversed on Remove");
            UnityEngine.Object.DestroyImmediate(instance); instance = null;
            flow.NextBattle(); check(player.CurrentHp == 700 && player.MaxHp == 800, "healed HP and increased max carry");
            flow.RestartRun(); check(player.CurrentHp == 500 && player.MaxHp == 500, "HP reset after all effects removed");

            cost.TrySpend(1); float resource = cost.Current;
            for (int i = 1; i <= 3; i++) { acquire("PS05"); check(cost.Current == resource && cost.StartingCost == 3 + i, "start cost only " + i); }
            reopen(); check(flow.RewardChoiceCount == 0, "PS05 cap excluded");
            for (int i = 1; i <= 2; i++) { acquire("PS06"); check(cost.Current == resource && cost.Max == 10 + 2 * i, "cost maximum only " + i); }
            reopen(); check(flow.RewardChoiceCount == 0, "PS06 cap excluded");
            flow.SkipReward(); flow.NextBattle();
            check(cost.Current == 6 && cost.Max == 14, "per battle modified start and cap");
            Property(cost, "Current", 13.99f); Set(cost, "regenPerSecond", 100000f); Call(cost, "Update"); Set(cost, "regenPerSecond", .8f);
            check(cost.Current <= 14 && cost.Current >= 13.99f, "regeneration honors raised cap");
            flow.RestartRun(); check(cost.Current == 3 && cost.Max == 10 && cost.StartingCost == 3, "cost reset");

            player.AddShield(10); acquire("PS07");
            check(player.Shield == 10 && player.GetShieldAmount(120) == 144, "shield bonus applies only to future grants");
            check(player.AddShield(120) == 144 && player.Shield == 154, "actual shield grant");
            check(player.GetShieldAmount(0) == 0 && player.GetShieldAmount(3) == 4, "shield zero and half-up rounding");
            for (int i = 0; i < 4; i++) acquire("PS07"); reopen();
            check(near(player.ShieldGainMultiplier, 2) && player.GetShieldAmount(120) == 240 && flow.RewardChoiceCount == 0, "additive shield cap");
            acquire("PS08"); acquire("PS08"); reopen();
            check(near(deck.InterruptStagger, 3.5f) && flow.RewardChoiceCount == 0, "stagger cap");
            flow.SkipReward(); flow.NextBattle();
            var cards = deck.CopyStartingDeck(); var armor = cards.Single(c => c.Category == SkillCategory.Shield);
            var silence = cards.Single(c => c.Category == SkillCategory.Interrupt);
            check(info.DescribeCard(armor).Contains("240") && info.DescribeCard(silence).Contains("3.5"), "modified tooltips");
            Property(cost, "Current", 10f); check(deck.TryUseSlot(3), "shield card starts");
            typeof(DeckSystem).GetMethod("AdvanceCast", Hidden).Invoke(deck, new object[] { .5f });
            check(player.Shield == 240, "deck uses enhanced shield");
            player.TakeDamage(150); check(player.Shield == 115 && player.CurrentHp == 500, "enhanced shield actually absorbs");
            var b = UnityEngine.Object.FindObjectsByType<Enemy>(FindObjectsInactive.Include, FindObjectsSortMode.None).Single(e => e.Data.name == "Enemy_B_Orange");
            enemies.BeginBattle(new[] { b }, null); deck.SetDeck(cards); Property(cost, "Current", 10f);
            check(deck.TryUseSlot(2), "silence input"); typeof(DeckSystem).GetMethod("AdvanceCast", Hidden).Invoke(deck, new object[] { .3f });
            check(b.IsStaggered && near((float)Get(b, "staggerTimer"), 3.5f), "enhanced actual stagger");
            player.ApplyStun(2); check(player.StunRemaining == 2, "player stun unchanged");
            flow.RestartRun(); check(player.GetShieldAmount(120) == 120 && near(deck.InterruptStagger, 2.5f), "response stats reset");

            // PS10 contributes independently even when the single-stat PS is capped.
            for (int i = 0; i < 4; i++) acquire("PS04");
            for (int i = 0; i < 3; i++) acquire("PS05");
            for (int i = 0; i < 2; i++) acquire("PS06");
            for (int i = 0; i < 5; i++) acquire("PS07");
            for (int i = 0; i < 2; i++) acquire("PS08");
            for (int i = 0; i < 3; i++) acquire("PS09");
            Set(player, "currentHp", 350f); resource = cost.Current;
            acquire("PS10");
            check(player.MaxHp == 550 && player.CurrentHp == 350 && cost.Current == resource, "PS10 maximum does not refill");
            check(near(cost.RegenerationPerSecond, 1.3f) && cost.StartingCost == 7 && cost.Max == 15, "PS10 adds beyond independent cost caps");
            check(player.GetShieldAmount(120) == 264 && near(deck.InterruptStagger, 4) && player.CriticalChance == 40, "PS10 response and critical contribution");
            check(flow.GetPassiveStacks(ps["PS04"].Effect) == 4 && flow.GetPassiveStacks(ps["PS09"].Effect) == 3, "PS10 does not consume other stacks");
            reopen(); check(flow.RewardChoiceCount == 0 && !flow.SelectReward(0), "PS10 one stack cap");
            flow.SkipReward(); flow.NextBattle();
            check(cost.Current == 7 && player.CurrentHp == 350 && player.MaxHp == 550, "composite carries into battle");
            check(((string)Get(metrics, "runModifiers")).Contains("시작 코스트 7 / 상한 15"), "full modifier snapshot in metrics");
            player.TakeDamage(100000); Call(flow, "Update"); flow.RestartRun();
            check(player.MaxHp == 500 && player.CurrentHp == 500 && player.CriticalChance == 15 && player.GetShieldAmount(120) == 120, "defeat restart clears player bonuses");
            check(near(cost.RegenerationPerSecond, .8f) && cost.Current == 3 && cost.Max == 10 && near(deck.InterruptStagger, 2.5f), "defeat restart clears cost and stagger");
            check(ps.Values.All(o => flow.GetPassiveStacks(o.Effect) == 0), "defeat restart clears all stacks");

            Set(flow, "rewardPool", new[] { ps["PS04"], ps["PS10"] });
            Set(flow, "rewardRandom", new FixedRandom(.009999)); reopen();
            check(flow.GetRewardChoice(0).Effect == ps["PS10"].Effect, "1 percent lower boundary admits rare");
            var choice = flow.GetRewardChoice(0); flow.CancelRewardSelection();
            check(flow.GetRewardChoice(0) == choice, "no reroll on back");
            Set(flow, "rewardRandom", new FixedRandom(.01)); reopen();
            check(flow.GetRewardChoice(0).Effect == ps["PS04"].Effect, "1 percent upper boundary uses ordinary");
            Set(flow, "rewardPool", new[] { ps["PS10"] }); reopen();
            check(flow.RewardChoiceCount == 0 && flow.SkipReward(), "rare miss not forced by empty regular pool");
            flow.RestartRun();

            // Potion is independent of deck lock, but never heals outside active combat.
            check(!player.TryUsePotion() && player.PotionsRemaining == 3, "full HP no consumption");
            Set(player, "currentHp", 100f); Time.timeScale = 0;
            check(!player.TryUsePotion() && player.CurrentHp == 100 && player.PotionsRemaining == 3, "pause no potion");
            Time.timeScale = 1; player.ApplyStun(2);
            check(!player.TryUsePotion() && player.PotionsRemaining == 3, "stun no potion");
            player.EndBattle();
            var ice = cards.Single(c => c.name == "Skill_SK02_IceArrow");
            cards[0] = ice; deck.SetDeck(cards); Property(cost, "Current", 10f);
            check(deck.TryUseSlot(0) && deck.IsChanneling, "channel starts for potion test");
            var slot = deck.GetHandCard(0); float remaining = deck.CastRemaining, currentCost = cost.Current;
            check(player.TryUsePotion() && player.CurrentHp == 250 && player.PotionsRemaining == 2, "potion during channel");
            check(deck.IsChanneling && deck.CastRemaining == remaining && deck.GetHandCard(0) == slot && cost.Current == currentCost, "potion leaves cast cost and deck unchanged");
            Set(player, "currentHp", 450f);
            check(player.TryUsePotion() && player.CurrentHp == 500 && metrics.PotionUses == 2 && metrics.PotionHealing == 200, "actual healing and overflow metrics");
            foreach (var enemy in enemies.GetAliveEnemies()) enemy.TakeDamage(enemy.CurrentHp);
            enemies.NotifyEnemyDied(); Call(flow, "Update"); Set(player, "currentHp", 400f);
            check(!player.TryUsePotion() && player.PotionsRemaining == 1, "reward screen cannot use potion");
            flow.SkipReward(); flow.NextBattle();
            check(player.PotionsRemaining == 1 && metrics.PotionUses == 0 && metrics.PotionHealing == 0, "potion charges carry but metrics reset");
            Set(player, "currentHp", 100f); check(player.TryUsePotion() && player.PotionsRemaining == 0, "last potion");
            check(!player.TryUsePotion() && player.CurrentHp == 250, "empty potion no heal");
            Call(ui, "UpdateStatus"); var potionText = (TMPro.TMP_Text)Get(ui, "potionText");
            check(potionText != null && potionText.text.Contains("0/3") && potionText.text.Contains("Shift"), "potion HUD count and shortcut");
            player.TakeDamage(100000); Call(flow, "Update"); check(!player.TryUsePotion(), "death no potion");
            flow.RestartRun(); check(player.PotionsRemaining == 3 && player.CurrentHp == 500 && cost.Current == 3, "restart restores base charges");
            metrics.WaitForBattle(); Set(player, "currentHp", 100f); Time.timeScale = 1;
            check(!player.TryUsePotion(), "title or ended metrics reject direct use");
            flow.RestartRun();
            return "All passive checks passed: " + passed + ". Synthetic function/UI state checks; physical input and natural balance are separate.";
        }
        finally
        {
            if (instance != null) { instance.Remove(); UnityEngine.Object.DestroyImmediate(instance); }
            Set(flow, "rewardPool", pool); Set(flow, "rewardRandom", random);
            Set(cost, "regenPerSecond", .8f);
            flow.RestartRun(); UnityEngine.Random.state = unityRandom;
        }
    }
}
