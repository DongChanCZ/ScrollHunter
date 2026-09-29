using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>저장된 Battle 씬의 실제 적 발동·피격 경로를 검사한다. 저장 에셋은 변경하지 않는다.</summary>
public static class RedShieldChecks
{
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;

    [MenuItem("Tools/Scroll Hunter/Check Red Shield (Play)")]
    public static void RunMenu() => Debug.Log(Run());

    public static string Run()
    {
        if (!Application.isPlaying) throw new InvalidOperationException("Battle 씬에서 Play 후 실행하세요.");
        var flow = UnityEngine.Object.FindFirstObjectByType<BattleFlow>();
        var player = UnityEngine.Object.FindFirstObjectByType<Player>();
        var metrics = UnityEngine.Object.FindFirstObjectByType<CombatMetrics>();
        var manager = UnityEngine.Object.FindFirstObjectByType<EnemyManager>();
        var info = UnityEngine.Object.FindFirstObjectByType<CombatInfoUI>();
        var all = UnityEngine.Object.FindObjectsByType<Enemy>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        var a = all.Single(e => e.Data.name == "Enemy_A_Green");
        var b = all.Single(e => e.Data.name == "Enemy_B_Orange");
        var c = all.Single(e => e.Data.name == "Enemy_C_Red");
        int passed = 0;
        Action<bool, string> check = (ok, label) => { if (!ok) throw new Exception(label); passed++; };
        Action<int> reset = shield =>
        {
            flow.RestartRun();
            manager.BeginBattle(new[] { a, b, c });
            player.AddShield(shield);
        };
        var logs = new List<string>();
        Application.LogCallback capture = (message, trace, type) => { if (type == LogType.Log) logs.Add(message); };
        Application.logMessageReceived += capture;
        try
        {
            reset(0);
            check(player.MaxHp == 500 && player.Defense == 20 && player.RedShieldDamageMultiplier == 0.5f, "saved player defaults");
            check(c.Data.Find(CastColor.Red).Damage == 240 && c.Data.Find(CastColor.Red).CastTime == 4f, "saved C red damage240 / cast4");
            check(a.Data.Find(CastColor.Green).Damage == 30 && b.Data.Find(CastColor.Orange).Damage == 40, "green and orange data unchanged");
            check(info.DescribeEnemy(c).Contains("240") && info.DescribeEnemy(c).Contains("50%")
                && info.DescribeEnemy(c).Contains("차단 불가"), "red information uses live data and reduction");
            check(!info.DescribeEnemy(b).Contains("50%"), "orange does not advertise reduction");

            int[] shields = { 0, 1, 120 };
            int[] expectedHp = { 300, 401, 500 };
            int[] expectedShield = { 0, 0, 20 };
            int[] expectedAbsorbed = { 0, 1, 100 };
            for (int i = 0; i < shields.Length; i++)
            {
                reset(shields[i]);
                Call(c, "Fire"); // Enemy.Fire가 색을 전달하는 실제 경로.
                check(player.CurrentHp == expectedHp[i] && player.Shield == expectedShield[i], "C HP / shield with " + shields[i]);
                check((int)Get(metrics, "shieldAbsorbed") == expectedAbsorbed[i], "actual absorption with " + shields[i]);
                check(metrics.RedShieldPrevented == (shields[i] > 0 ? 100 : 0), "reduction separated with " + shields[i]);
            }

            reset(1); Call(a, "Fire");
            check(player.CurrentHp == 476 && player.Shield == 0 && metrics.RedShieldPrevented == 0, "green no half");
            reset(1); Call(b, "Fire");
            check(player.CurrentHp == 468 && player.IsStunned && metrics.RedShieldPrevented == 0, "orange no half and stun retained");
            reset(25); Call(a, "Fire"); Call(c, "Fire");
            check(player.CurrentHp == 300 && metrics.RedShieldPrevented == 0, "green exhausted shield before red");
            reset(26); Call(a, "Fire"); Call(c, "Fire");
            check(player.CurrentHp == 401 && metrics.RedShieldPrevented == 100, "green left one before red");

            reset(120); player.TakeDamage(240, CastColor.Red); player.TakeDamage(240, CastColor.Red);
            check(player.CurrentHp == 420 && player.Shield == 0 && metrics.RedShieldPrevented == 200, "shield checked again each hit");
            player.TakeDamage(240, CastColor.Red);
            check(player.CurrentHp == 220 && metrics.RedShieldPrevented == 200, "next hit after exhaustion unhalved");
            reset(1); player.TakeDamage(3, CastColor.Red);
            check(player.CurrentHp == 500 && player.Shield == 0, "2.5 raw halves to1.25 then rounds once to1");
            reset(1); player.TakeDamage(0, CastColor.Red);
            check(player.CurrentHp == 500 && player.Shield == 1 && metrics.RedShieldPrevented == 0, "zero damage stays zero");
            player.TakeDamage(1, CastColor.Red);
            check(player.CurrentHp == 500 && player.Shield == 0, "positive raw retains minimum1");

            reset(120); player.SetSanctuary(true); Call(c, "Fire"); player.ApplyStun(2f);
            check(player.CurrentHp == 500 && player.Shield == 120 && !player.IsStunned, "sanctuary priority preserves resources");
            check(metrics.InvulnerabilityPrevented == 200 && metrics.RedShieldPrevented == 0
                && (int)Get(metrics, "shieldAbsorbed") == 0, "invulnerability not double counted");
            reset(120); metrics.ReportVictory(); player.TakeDamage(240, CastColor.Red);
            check(player.CurrentHp == 500 && player.Shield == 120 && metrics.RedShieldPrevented == 0, "ended rejects damage and metrics");
            reset(120);
            check(c.TryInterrupt(2.5f) == InterruptResult.FailedRed && c.IsCasting, "red still cannot be interrupted");

            reset(1); logs.Clear(); Call(c, "Fire"); metrics.ReportVictory(); metrics.FlushPendingSummary();
            check(logs.Any(s => s.Contains("피격 100 (원본 240)") && s.Contains("방어도 흡수 1")
                && s.Contains("빨강 반감 감소 100")), "hit log separates mitigation and absorption");
            check(logs.Last().Contains("전투 종료 요약") && logs.Last().Contains("실제 방어도 흡수 1")
                && logs.Last().Contains("빨강 반감 감소 피해 100"), "summary records separately and appears last");
            reset(1);
            check(metrics.RedShieldPrevented == 0 && metrics.InvulnerabilityPrevented == 0, "restart resets counters");
            typeof(Player).GetField("currentHp", Hidden).SetValue(player, 50f); logs.Clear(); Call(c, "Fire"); Call(flow, "Update");
            check(!player.IsAlive && metrics.Ended && metrics.RedShieldPrevented == 100, "fatal red reduction captured");
            check(logs.Last().Contains("전투 종료 요약") && logs.Last().Contains("빨강 반감 감소 피해 100"), "fatal summary last");
            return "Red shield checks passed: " + passed;
        }
        finally
        {
            Application.logMessageReceived -= capture;
            flow.RestartRun();
        }
    }

    private static object Get(object target, string name) => target.GetType().GetField(name, Hidden).GetValue(target);
    private static void Call(object target, string name) => target.GetType().GetMethod(name, Hidden).Invoke(target, null);
}
