using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Battle 씬 Play 모드에서 진행 경계·버튼 경로를 검사하고 첫 전투로 되돌린다.</summary>
public static class BattleFlowChecks
{
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    private static int passed;

    [MenuItem("Tools/Scroll Hunter/Check Three Battle Flow (Play)")]
    public static void RunMenu() { Debug.Log(Run()); }

    public static string Run()
    {
        if (!EditorApplication.isPlaying) throw new InvalidOperationException("Battle 씬에서 Play 후 실행하세요.");
        var flow = UnityEngine.Object.FindFirstObjectByType<BattleFlow>();
        if (flow == null) throw new InvalidOperationException("BattleFlow가 없습니다.");
        var player = UnityEngine.Object.FindFirstObjectByType<Player>();
        var deck = UnityEngine.Object.FindFirstObjectByType<DeckSystem>();
        var cost = UnityEngine.Object.FindFirstObjectByType<CostSystem>();
        var enemies = UnityEngine.Object.FindFirstObjectByType<EnemyManager>();
        var metrics = UnityEngine.Object.FindFirstObjectByType<CombatMetrics>();
        var info = UnityEngine.Object.FindFirstObjectByType<CombatInfoUI>();
        var next = (Button)Get(flow, "nextButton");
        var restart = (Button)Get(flow, "restartButton");
        var originalEncounters = (BattleFlow.Encounter[])Get(flow, "encounters");
        passed = 0;
        try
        {
            flow.RestartRun();
            SkillData first = deck.GetHandCard(0);
            Check(flow.BattleCount == 3 && flow.BattleNumber == 1 && enemies.EnemyCount == 1
                && enemies.CurrentTarget.Data.name == "Enemy_B_Orange", "first encounter B");
            Check(enemies.CurrentTarget.transform.position == (Vector3)Get(enemies, "formationCenter"), "one enemy centered");
            Check(player.CurrentHp == player.MaxHp && cost.Current == 3f && Time.timeScale == 1f, "run initial resources");
            flow.NextBattle();
            Check(flow.BattleNumber == 1, "cannot skip active battle");
            player.TakeDamage(60);
            float carried = player.CurrentHp;
            Check(carried == player.MaxHp - 50, "HP loss before carry");
            Check(deck.TryUseSlot(0) && deck.IsCasting && deck.GetHandCard(0) != first, "accepted card and rotation");
            player.AddShield(55);
            player.ApplyStun(2f);
            info.InspectSlot(1);
            Check(info.IsInfoPaused, "pause before transition");
            metrics.RecordCostShortInput();
            metrics.RecordCostWasted(1f);
            Win(enemies, flow);
            Check(flow.State == BattleFlowState.BetweenBattles && Time.timeScale == 0f, "first victory waits");
            Check(!deck.IsCasting && !deck.IsLocked && player.Shield == 0 && !player.IsStunned, "end clears cast shield stun");
            Check(!deck.TryUseSlot(0), "ended battle rejects cards");
            player.TakeDamage(150);
            Check(player.CurrentHp == carried, "ended battle rejects damage");
            Click(next);
            Check(flow.BattleNumber == 2 && enemies.EnemyCount == 1
                && enemies.CurrentTarget.Data.name == "Enemy_C_Red", "next button opens C");
            Check(enemies.CurrentTarget.transform.position == (Vector3)Get(enemies, "formationCenter"), "C also centered");
            Check(player.CurrentHp == carried && cost.Current == 3f && deck.GetHandCard(0) == first, "carry HP and reset cost hand");
            Check(!info.IsInfoPaused && !metrics.Ended && metrics.BattleNumber == 2 && metrics.Elapsed == 0f
                && (int)Get(metrics, "totalUses") == 0 && (int)Get(metrics, "nextUseId") == 1
                && (int)Get(metrics, "costShortInputs") == 0 && (float)Get(metrics, "costWasted") == 0f, "pause and metrics reset");
            Check(enemies.CurrentTarget.CurrentHp == 300 && enemies.CurrentTarget.CastColor == CastColor.Red
                && enemies.CurrentTarget.CastProgress01 == 0f, "enemy HP pattern and cast reset");
            Call(deck, "AdvanceCast", 5f);
            Check(player.Shield == 0 && enemies.CurrentTarget.CurrentHp == 300, "old cast does not leak");
            Win(enemies, flow);
            Click(next);
            Check(flow.BattleNumber == 3 && enemies.EnemyCount == 2 && enemies.DeadCount == 0, "third encounter revives B and C");
            Check(player.CurrentHp == carried && enemies.CurrentTarget.Data.name == "Enemy_B_Orange", "second HP carry and live target");
            float centerX = ((Vector3)Get(enemies, "formationCenter")).x;
            float offset = (float)Get(enemies, "sideOffset");
            Check(enemies.Enemies[0].transform.position.x == centerX - offset
                && enemies.Enemies[1].transform.position.x == centerX + offset, "two enemies on left and right");
            Check(enemies.TryFire(enemies.CurrentTarget), "enemy fire gap resets");
            Win(enemies, flow);
            Check(flow.State == BattleFlowState.Victory && !next.gameObject.activeSelf
                && restart.gameObject.activeInHierarchy && Time.timeScale == 0f, "final victory UI");
            flow.NextBattle();
            Check(flow.BattleNumber == 3, "no fourth encounter");
            Click(restart);
            Check(flow.BattleNumber == 1 && player.CurrentHp == player.MaxHp && enemies.EnemyCount == 1, "final restart button");
            Check(deck.TryUseSlot(0), "card usable after restart");
            player.AddShield(60);
            player.ApplyStun(2f);
            info.InspectSlot(1);
            typeof(Player).GetField("currentHp", Hidden).SetValue(player, 0f);
            Call(player, "Update");
            Call(flow, "Update");
            Check(flow.State == BattleFlowState.Defeat && metrics.Ended && Time.timeScale == 0f, "defeat while paused");
            Check(player.Shield == 0 && !player.IsStunned && !deck.IsCasting && !deck.IsLocked, "forced defeat cleanup");
            Check(!deck.TryUseSlot(0), "defeat input blocked");
            float endedCost = cost.Current;
            Call(cost, "Update");
            Check(cost.Current == endedCost, "no cost charge after result");
            flow.NextBattle();
            Check(flow.BattleNumber == 1 && flow.State == BattleFlowState.Defeat, "defeat cannot advance");
            Click(restart);
            Check(flow.State == BattleFlowState.Fighting && flow.BattleNumber == 1 && player.CurrentHp == player.MaxHp
                && player.Shield == 0 && !player.IsStunned && !deck.IsLocked && deck.GetHandCard(0) == first
                && cost.Current == 3f && !info.IsInfoPaused && !metrics.Ended, "defeat restart fully resets");
            Check(enemies.CurrentTarget.CurrentHp == 300 && enemies.CurrentTarget.CastColor == CastColor.Orange
                && !enemies.CurrentTarget.IsStaggered && enemies.CurrentTarget.CastProgress01 == 0f, "B revived and pattern restored");
            Enemy a = null, b = null, c = null;
            foreach (var enemy in UnityEngine.Object.FindObjectsByType<Enemy>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (enemy.Data.name == "Enemy_A_Green") a = enemy;
                if (enemy.Data.name == "Enemy_B_Orange") b = enemy;
                if (enemy.Data.name == "Enemy_C_Red") c = enemy;
            }
            typeof(BattleFlow).GetField("encounters", Hidden).SetValue(flow, new[] {
                new BattleFlow.Encounter { label = "Layout check", enemies = new[] { a, b, c }, centerEnemy = c }
            });
            flow.RestartRun();
            Check(enemies.EnemyCount == 3 && enemies.Enemies[0] == a && enemies.Enemies[1] == c
                && enemies.Enemies[2] == b && c.transform.position.x == centerX, "three enemies strongest in center");
            Call(enemies, "StepTarget", 1);
            Check(enemies.CurrentTarget == c, "target step follows new left to right order");
            foreach (var enemy in enemies.Enemies)
            {
                Call(enemy, "LateUpdate");
                var bar = (RectTransform)Get(enemy, "castBarRoot");
                Vector3 head = enemy.transform.position + Vector3.up * (float)Get(enemy, "barWorldHeight");
                Check(Vector3.Distance(bar.position, Camera.main.WorldToScreenPoint(head)) < 1f, "bar follows " + enemy.name);
            }
            c.TakeDamage(int.MaxValue);
            enemies.NotifyEnemyDied();
            Check(enemies.CurrentTarget == a && a.transform.position.x == centerX - offset
                && b.transform.position.x == centerX + offset, "death skips target without moving survivors");
            return "BattleFlow checks passed: " + passed + ". Function/event verification; not a manual-input or balance test.";
        }
        finally
        {
            typeof(BattleFlow).GetField("encounters", Hidden).SetValue(flow, originalEncounters);
            flow.RestartRun();
        }
    }

    private static void Win(EnemyManager enemies, BattleFlow flow)
    {
        foreach (Enemy enemy in enemies.GetAliveEnemies()) enemy.TakeDamage(int.MaxValue);
        enemies.NotifyEnemyDied();
        Call(flow, "Update");
    }

    private static void Click(Button button)
    {
        Check(button != null && button.gameObject.activeInHierarchy && button.interactable, "result button is available");
        ExecuteEvents.Execute(button.gameObject, new PointerEventData(EventSystem.current)
            { button = PointerEventData.InputButton.Left }, ExecuteEvents.pointerClickHandler);
    }

    private static object Get(object target, string field) => target.GetType().GetField(field, Hidden).GetValue(target);
    private static void Call(object target, string method, params object[] args)
        => target.GetType().GetMethod(method, Hidden).Invoke(target, args);
    private static void Check(bool condition, string name)
    {
        if (!condition) throw new InvalidOperationException("BattleFlow check failed: " + name);
        passed++;
    }
}
