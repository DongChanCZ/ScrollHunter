using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>상한은 현재 2회 보상보다 높으므로 추가 보상 기회만 함수 호출로 재현한다.</summary>
public static class PassiveRewardChecks
{
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    [MenuItem("Tools/Scroll Hunter/Check Passive Rewards (Play)")]
    public static void RunMenu() => Debug.Log(Run());

    public static string Run()
    {
        if (!Application.isPlaying) throw new InvalidOperationException("Battle 씬 Play에서 실행하세요.");
        var flow = UnityEngine.Object.FindFirstObjectByType<BattleFlow>();
        var player = UnityEngine.Object.FindFirstObjectByType<Player>();
        var cost = UnityEngine.Object.FindFirstObjectByType<CostSystem>();
        var deck = UnityEngine.Object.FindFirstObjectByType<DeckSystem>();
        var enemies = UnityEngine.Object.FindFirstObjectByType<EnemyManager>();
        var metrics = UnityEngine.Object.FindFirstObjectByType<CombatMetrics>();
        var ui = UnityEngine.Object.FindFirstObjectByType<BattleRewardUI>();
        var pool = (BattleRewardOption[])Get(flow, "rewardPool");
        var ps04 = pool.Single(x => x.Effect != null && x.Effect.name == "PS04_Circulation");
        var ps09 = pool.Single(x => x.Effect != null && x.Effect.name == "PS09_Precision");
        var choices = (Button[])Get(ui, "choiceButtons");
        var labels = (TMPro.TMP_Text[])Get(ui, "choiceLabels");
        var hud = (TMPro.TMP_Text)Get(flow, "passiveText");
        var starting = deck.CopyStartingDeck();
        var randomState = UnityEngine.Random.state;
        int passed = 0;
        Action<bool, string> check = (ok, label) => { if (!ok) throw new Exception(label); passed++; };
        Func<float, float, bool> near = (a, b) => Mathf.Abs(a - b) < .00001f;
        Action win = () => { foreach (var e in enemies.GetAliveEnemies()) e.TakeDamage(e.CurrentHp); enemies.NotifyEnemyDied(); Call(flow, "Update"); };
        Action reopen = () => {
            Property(flow, "State", BattleFlowState.BetweenBattles);
            Property(flow, "RewardResolved", false); Property(flow, "SelectedReward", null);
            Call(flow, "PrepareRewards"); Call(flow, "RefreshUI");
        };
        Func<BattleRewardOption[]> offered = () => Enumerable.Range(0, flow.RewardChoiceCount).Select(flow.GetRewardChoice).ToArray();
        StatRewardEffect independent = null;
        try
        {
            flow.RestartRun();
            check(pool.Length == 13 && pool.Count(x => x.Card != null) == 3 && pool.Count(x => x.Effect != null) == 10, "saved mixed pool");
            // 이하 기존 2종의 회귀 검사는 후보를 두 종으로 고정한다.
            Set(flow, "rewardPool", pool.Take(3).Concat(new[] { ps04, ps09 }).ToArray());
            check(ps04.Effect.MaxStacks == 4 && ps09.Effect.MaxStacks == 3, "per-PS serialized caps");
            check(near(cost.RegenerationPerSecond, .8f) && player.CriticalChance == 15, "base rates");
            check(flow.GetPassiveStacks(ps04.Effect) == 0 && flow.GetPassiveStacks(ps09.Effect) == 0, "fresh empty stacks");
            player.TakeDamage(60); float hp = player.CurrentHp; cost.TrySpend(1f); float current = cost.Current;
            UnityEngine.Random.InitState(31); var expectedRandom = UnityEngine.Random.value; UnityEngine.Random.InitState(31);
            win();
            check(UnityEngine.Random.value == expectedRandom, "reward sampling leaves critical RNG untouched");
            check(offered().Count(x => x.Card != null) == 2 && offered().Count(x => x.Effect != null) == 1, "two cards plus one passive");
            var snapshot = offered();
            check(flow.SelectReward(0), "card can open replacement");
            flow.CancelRewardSelection(); ui.Refresh();
            check(offered().SequenceEqual(snapshot) && !flow.RewardResolved, "back/refresh do not reroll");
            var selected = flow.GetRewardChoice(2).Effect;
            check(labels[2].text.Contains("0/") && labels[2].text.Contains("덱 유지"), "passive candidate states stacks and immediate application");
            Click(choices[2]);
            check(flow.RewardResolved && flow.SelectedReward == null && flow.GetPassiveStacks(selected) == 1, "passive button immediately resolves");
            check(Enumerable.Range(0, 8).All(i => flow.GetDeckCard(i) == starting[i]), "passive preserves all eight deck positions");
            check(cost.Current == current && player.CurrentHp == hp && player.MaxHp == 500, "acquisition does not refill resources or change HP");
            check(hud.gameObject.activeInHierarchy && hud.text.Contains(selected.DisplayName) && hud.text.Contains("1/"), "acquired stack HUD");
            check(!flow.SelectReward(2) && !flow.SkipReward() && !flow.ReplaceDeckCard(0), "resolved reward cannot be acquired twice");
            float regen = cost.RegenerationPerSecond, crit = player.CriticalChance;
            Click((Button)Get(flow, "nextButton"));
            check(flow.BattleNumber == 2 && player.CurrentHp == hp && cost.Current == 3, "next battle normal HP carry and start cost");
            check(near(cost.RegenerationPerSecond, regen) && player.CriticalChance == crit && flow.GetPassiveStacks(selected) == 1, "effects and stacks persist");
            check(((string)Get(metrics, "runModifiers")).Contains(selected.DisplayName), "next summary snapshots acquired passive");
            flow.RestartRun();
            check(near(cost.RegenerationPerSecond, .8f) && player.CriticalChance == 15 && flow.GetPassiveStacks(selected) == 0, "restart clears effects and stacks");

            Set(flow, "rewardPool", new[] { ps04 }); win();
            for (int i = 1; i <= 4; i++)
            {
                check(flow.RewardChoiceCount == 1 && flow.SelectReward(0), "PS04 acquisition " + i);
                check(near(cost.RegenerationPerSecond, .8f + .1f * i) && flow.GetPassiveStacks(ps04.Effect) == i, "PS04 cumulative rate " + i);
                reopen();
            }
            check(flow.RewardChoiceCount == 0 && !flow.SelectReward(0) && flow.SkipReward(), "PS04 cap excluded and empty pool can skip");
            check(near((float)Get(cost, "regenPerSecond"), .8f), "PS04 never mutates base serialized rate");
            flow.NextBattle();
            float before = cost.Current, dt = Time.deltaTime;
            Call(cost, "Update");
            check(near(cost.Current - before, 1.2f * dt), "CostSystem Update uses modified rate");
            flow.RestartRun();

            // 다른 PS가 같은 능력치를 올려도 PS04의 획득 상한은 별도로 센다.
            independent = UnityEngine.Object.Instantiate((StatRewardEffect)ps04.Effect);
            independent.Apply(player, cost); independent.Apply(player, cost);
            check(near(cost.RegenerationPerSecond, .9f) && flow.GetPassiveStacks(ps04.Effect) == 0, "independent contribution and idempotent Apply");
            reopen();
            for (int i = 0; i < 4; i++) { check(flow.SelectReward(0), "PS04 cap independent of total rate " + i); reopen(); }
            check(near(cost.RegenerationPerSecond, 1.3f) && flow.RewardChoiceCount == 0, "independent effect does not consume PS04 cap");
            flow.RestartRun();
            check(near(cost.RegenerationPerSecond, .9f), "restart removes only owned contributions");
            independent.Remove(); independent.Remove();
            check(near(cost.RegenerationPerSecond, .8f), "Remove is idempotent");

            Set(flow, "rewardPool", new[] { ps09 }); win();
            for (int i = 1; i <= 3; i++)
            {
                check(flow.SelectReward(0) && player.CriticalChance == 15 + 5 * i && flow.GetPassiveStacks(ps09.Effect) == i, "PS09 percentage points " + i);
                reopen();
            }
            check(flow.RewardChoiceCount == 0 && !flow.SelectReward(0), "PS09 capped exclusion");
            check((float)Get(player, "criticalChance") == 15 && player.CriticalMultiplier == 1.5f, "PS09 preserves base chance and multiplier");
            int seed = Enumerable.Range(0, 1000).First(i => { UnityEngine.Random.InitState(i); float v = UnityEngine.Random.value; return v >= .15f && v < .20f; });
            UnityEngine.Random.InitState(seed); check(player.RollCritical(), "RollCritical reads bonus");
            flow.RestartRun(); UnityEngine.Random.InitState(seed); check(!player.RollCritical(), "same roll below modified but above base threshold");

            // 선택 직후 상한에 도달한 오래된 후보로 다시 얻는 경로도 거절한다.
            reopen(); var stale = offered()[0];
            for (int i = 0; i < 3; i++) { check(flow.SelectReward(0), "PS09 repeated reward " + i); reopen(); }
            ((System.Collections.Generic.List<BattleRewardOption>)Get(flow, "rewardChoices")).Add(stale);
            check(!flow.SelectReward(0), "stale capped choice rechecked at selection");
            flow.RestartRun(); Set(flow, "rewardPool", new[] { ps04, ps04, ps09 }); reopen();
            check(flow.RewardChoiceCount == 1, "duplicate pool entries cannot create extra slots");
            var invalid = new BattleRewardOption(); Set(invalid, "card", pool[0].Card); Set(invalid, "effect", ps04.Effect);
            Set(flow, "rewardPool", new[] { invalid }); reopen();
            check(flow.RewardChoiceCount == 0 && flow.SkipReward(), "invalid mixed card/effect entry excluded");

            Set(flow, "rewardPool", pool); flow.RestartRun(); win();
            check(flow.SkipReward() && near(cost.RegenerationPerSecond, .8f) && player.CriticalChance == 15, "skip changes no stats");
            for (int i = 0; i < 20; i++)
            {
                Set(flow, "rewardPool", new[] { ps04 }); reopen(); flow.SelectReward(0); flow.RestartRun();
                check(near(cost.RegenerationPerSecond, .8f) && flow.GetPassiveStacks(ps04.Effect) == 0, "repeated restart cleanup " + i);
            }
            Set(flow, "rewardPool", new[] { ps04 }); reopen(); flow.SelectReward(0); flow.NextBattle();
            player.TakeDamage(100000); Call(flow, "Update");
            check(flow.State == BattleFlowState.Defeat && near(cost.RegenerationPerSecond, .9f) && !flow.SelectReward(0), "defeat retains run effects without granting reward");
            Click((Button)Get(flow, "restartButton"));
            check(near(cost.RegenerationPerSecond, .8f) && player.CriticalChance == 15 && player.CurrentHp == 500 && hud.text.Contains("패시브 없음"), "defeat restart button resets HUD and stats");
            return "Passive reward checks passed: " + passed + ". Function/UI event checks; extra reward opportunities are synthetic, not natural play.";
        }
        finally
        {
            if (independent != null) { independent.Remove(); UnityEngine.Object.DestroyImmediate(independent); }
            Set(flow, "rewardPool", pool); flow.RestartRun();
            UnityEngine.Random.state = randomState;
        }
    }

    private static object Get(object target, string name) => target.GetType().GetField(name, Hidden).GetValue(target);
    private static void Set(object target, string name, object value) => target.GetType().GetField(name, Hidden).SetValue(target, value);
    private static void Property(object target, string name, object value) => target.GetType().GetProperty(name).SetValue(target, value);
    private static void Call(object target, string name) => target.GetType().GetMethod(name, Hidden).Invoke(target, null);
    private static void Click(Button button) => ExecuteEvents.Execute(button.gameObject,
        new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left }, ExecuteEvents.pointerClickHandler);
}
