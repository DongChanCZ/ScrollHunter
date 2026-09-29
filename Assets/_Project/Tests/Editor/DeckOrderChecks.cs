using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class DeckOrderChecks
{
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    [MenuItem("Tools/Scroll Hunter/Check Deck Order (Fresh Play)")]
    public static void RunMenu() => Debug.Log(Run());

    public static string Run()
    {
        if (!Application.isPlaying) throw new InvalidOperationException("새 Play의 시작 화면에서 실행하세요.");
        var flow = UnityEngine.Object.FindFirstObjectByType<BattleFlow>();
        if (flow.State != BattleFlowState.Title) throw new InvalidOperationException("새 Play가 필요합니다.");
        var ui = UnityEngine.Object.FindFirstObjectByType<BattleRewardUI>();
        var deck = UnityEngine.Object.FindFirstObjectByType<DeckSystem>();
        var player = UnityEngine.Object.FindFirstObjectByType<Player>();
        var cost = UnityEngine.Object.FindFirstObjectByType<CostSystem>();
        var metrics = UnityEngine.Object.FindFirstObjectByType<CombatMetrics>();
        var enemies = UnityEngine.Object.FindFirstObjectByType<EnemyManager>();
        var original = deck.CopyStartingDeck().ToArray();
        var slots = (Button[])Get(ui, "deckButtons");
        var next = (Button)Get(flow, "nextButton");
        var reorder = (Button)Get(ui, "reorderButton");
        var save = (Button)Get(ui, "saveOrderButton");
        var cancel = (Button)Get(ui, "cancelOrderButton");
        var pool = (BattleRewardOption[])Get(flow, "rewardPool");
        int passed = 0;
        Action<bool, string> check = (ok, label) => { if (!ok) throw new Exception(label); passed++; };
        Func<SkillData[]> committed = () => Enumerable.Range(0, 8).Select(flow.GetDeckCard).ToArray();
        Func<SkillData[]> draft = () => Enumerable.Range(0, 8).Select(flow.GetOrderCard).ToArray();
        Action win = () => { foreach (var e in enemies.GetAliveEnemies()) e.TakeDamage(e.CurrentHp); enemies.NotifyEnemyDied(); Call(flow, "Update"); };
        try
        {
            check(!flow.BeginOrderEdit() && !flow.SaveOrder(), "title cannot edit");
            Click((Button)Get(flow, "startButton"));
            check(flow.State == BattleFlowState.Preparing && metrics.Ended && metrics.BattleNumber == 0 && Time.timeScale == 0, "title click waits in preparation");
            check(slots.All(b => b.gameObject.activeInHierarchy) && reorder.gameObject.activeInHierarchy && next.gameObject.activeInHierarchy, "eight slots and actions visible");
            check(!((Button)Get(ui, "skipButton")).gameObject.activeSelf && !flow.SkipReward() && !flow.SelectReward(0), "no rewards before first fight");
            Drag(slots, 0, 5);
            check(committed().SequenceEqual(original) && !flow.IsEditingOrder, "drag disabled outside edit");
            Click(reorder);
            check(flow.IsEditingOrder && !next.gameObject.activeSelf && save.gameObject.activeInHierarchy && cancel.gameObject.activeInHierarchy, "edit gates next and shows save cancel");
            flow.NextBattle(); flow.StartRun();
            check(flow.State == BattleFlowState.Preparing && flow.IsEditingOrder && metrics.Ended && !deck.TryUseSlot(0), "edit cannot start combat or use cards");
            check(!flow.MoveOrderCard(-1, 2) && !flow.MoveOrderCard(1, 8) && !flow.BeginOrderEdit(), "invalid movement and nested editing rejected");
            Drag(slots, 0, 5);
            var moved = new[] { original[1], original[2], original[3], original[4], original[5], original[0], original[6], original[7] };
            check(draft().SequenceEqual(moved), "forward drag inserts and shifts across rows");
            check(committed().SequenceEqual(original), "draft does not modify run deck");
            check(((Image[])Get(ui, "deckIcons")).Select(x => x.sprite).SequenceEqual(moved.Select(x => x.Icon)), "icons follow draft");
            Drag(slots, 5, 0);
            check(draft().SequenceEqual(original), "reverse drag inserts and shifts");
            Drag(slots, 0, 5); Click(cancel);
            check(!flow.IsEditingOrder && committed().SequenceEqual(original) && draft().SequenceEqual(original), "cancel discards all moves");
            check(!((Image)Get(ui, "dragIcon")).gameObject.activeSelf && !((Image)Get(ui, "dropIndicator")).gameObject.activeSelf, "drag visuals cleared");
            Click(reorder);
            Drag(slots, 0, 5, PointerEventData.InputButton.Right);
            check(draft().SequenceEqual(original), "right drag ignored");
            Drag(slots, 0, -1);
            check(draft().SequenceEqual(original), "outside drop ignored");
            Drag(slots, 0, 0);
            check(draft().SequenceEqual(original), "same slot is a no-op");
            Drag(slots, 0, 5); Click(save);
            check(!flow.IsEditingOrder && committed().SequenceEqual(moved) && next.gameObject.activeInHierarchy, "save commits without starting battle");
            check(flow.State == BattleFlowState.Preparing && metrics.Ended && deck.CopyStartingDeck().SequenceEqual(original), "save is run-only and no metrics started");
            Click(reorder); Drag(slots, 1, 7); Click(cancel);
            check(committed().SequenceEqual(moved), "cancel restores last saved order");
            Click(next);
            check(flow.State == BattleFlowState.Fighting && flow.BattleNumber == 1 && metrics.BattleNumber == 1, "confirm begins first encounter once");
            check(Enumerable.Range(0, 4).All(i => deck.GetHandCard(i) == moved[i]) && deck.PeekNext() == moved[4], "saved order initializes hand and queue");
            check(!flow.BeginOrderEdit() && !flow.SaveOrder() && !flow.MoveOrderCard(0, 1), "combat cannot reorder");
            check(player.CurrentHp == player.MaxHp && cost.Current == cost.StartingCost && enemies.EnemyCount == 1, "starting resources and encounter unchanged");
            Set(flow, "rewardPool", pool.Where(x => x.Card != null).Take(1).ToArray());
            player.TakeDamage(60); float carriedHp = player.CurrentHp; win();
            check(!flow.BeginOrderEdit() && !reorder.gameObject.activeSelf, "unresolved reward cannot reorder");
            check(flow.SelectReward(0) && !flow.BeginOrderEdit(), "replacement selection cannot reorder");
            var replacement = flow.SelectedReward;
            Click(slots[3]);
            var replaced = committed();
            check(flow.RewardResolved && replaced[3] == replacement && reorder.gameObject.activeInHierarchy, "replace resolves before reorder");
            var choices = Enumerable.Range(0, flow.RewardChoiceCount).Select(flow.GetRewardChoice).ToArray();
            Click(reorder); Drag(slots, 3, 0); Click(cancel);
            check(committed().SequenceEqual(replaced) && flow.RewardResolved, "cancel preserves acquired card");
            Click(reorder); Drag(slots, 3, 0); Click(save);
            var reordered = committed();
            check(reordered[0] == replacement && reordered.Distinct().Count() == 8 && replaced.All(reordered.Contains), "save preserves exactly eight owned cards");
            check(choices.SequenceEqual(Enumerable.Range(0, flow.RewardChoiceCount).Select(flow.GetRewardChoice)) && !flow.SelectReward(0), "edit does not reroll or acquire reward twice");
            Click(next);
            check(flow.BattleNumber == 2 && player.CurrentHp == carriedHp && cost.Current == cost.StartingCost, "next encounter preserves HP and resets cost");
            check(Enumerable.Range(0, 4).All(i => deck.GetHandCard(i) == reordered[i]) && deck.PeekNext() == reordered[4], "reward reorder enters next combat");
            win(); Click((Button)Get(ui, "skipButton")); Click(reorder); Drag(slots, 7, 0); Click(save);
            check(flow.RewardResolved && committed()[0] == reordered[7], "skip also permits reorder");
            Click(next); win();
            check(flow.State == BattleFlowState.Victory && !flow.BeginOrderEdit(), "final result cannot edit");
            flow.RestartRun();
            check(committed().SequenceEqual(original) && deck.GetHandCard(0) == original[0] && player.CurrentHp == player.MaxHp, "restart resets saved order and HP");
            Set(flow, "rewardPool", pool.Where(x => x.Effect != null && !x.Effect.IsRareReward).Take(1).ToArray());
            win(); var passive = flow.GetRewardChoice(0).Effect; flow.SelectReward(0); int stacks = flow.GetPassiveStacks(passive);
            Click(reorder); Drag(slots, 7, 0); Click(cancel); Click(reorder); Drag(slots, 7, 0); Click(save);
            check(flow.GetPassiveStacks(passive) == stacks && stacks == 1 && flow.RewardResolved, "passive not removed or reapplied by editing");
            Click(reorder); Drag(slots, 0, 6); flow.RestartRun();
            check(!flow.IsEditingOrder && committed().SequenceEqual(original) && flow.GetPassiveStacks(passive) == 0, "restart discards draft and passives");
            check(!((Image)Get(ui, "dragIcon")).gameObject.activeSelf && !((GameObject)Get(ui, "panel")).activeSelf, "restart closes edit UI");
        }
        finally { Set(flow, "rewardPool", pool); flow.RestartRun(); }
        return "Deck order checks passed: " + passed + ". Function and pointer-event checks; not physical input.";
    }

    private static void Drag(Button[] slots, int from, int to, PointerEventData.InputButton button = PointerEventData.InputButton.Left)
    {
        Canvas.ForceUpdateCanvases();
        var data = new PointerEventData(EventSystem.current) { button = button,
            position = RectTransformUtility.WorldToScreenPoint(null, slots[from].transform.position) };
        ExecuteEvents.Execute(slots[from].gameObject, data, ExecuteEvents.beginDragHandler);
        data.position = to < 0 ? new Vector2(-10000, -10000) : RectTransformUtility.WorldToScreenPoint(null, slots[to].transform.position);
        ExecuteEvents.Execute(slots[from].gameObject, data, ExecuteEvents.dragHandler);
        ExecuteEvents.Execute(slots[from].gameObject, data, ExecuteEvents.endDragHandler);
    }
    private static void Click(Button button) => ExecuteEvents.Execute(button.gameObject,
        new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left }, ExecuteEvents.pointerClickHandler);
    private static object Get(object o, string name) => o.GetType().GetField(name, Hidden).GetValue(o);
    private static void Set(object o, string name, object value) => o.GetType().GetField(name, Hidden).SetValue(o, value);
    private static void Call(object o, string name) => o.GetType().GetMethod(name, Hidden).Invoke(o, null);
}
