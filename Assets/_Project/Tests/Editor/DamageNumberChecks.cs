using System;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;

/// <summary>Play 모드에서 숫자 표시·피해 보존·정리 확인 후 첫 전투로 복귀.</summary>
public static class DamageNumberChecks
{
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;

    [MenuItem("Tools/Scroll Hunter/Check Damage Numbers (Play)")]
    public static void RunMenu() => Debug.Log(Run());

    public static string Run()
    {
        if (!Application.isPlaying) throw new InvalidOperationException("Battle 씬에서 Play 후 실행하세요.");
        var flow = UnityEngine.Object.FindFirstObjectByType<BattleFlow>();
        var ui = UnityEngine.Object.FindFirstObjectByType<DamageNumberUI>();
        var enemies = UnityEngine.Object.FindFirstObjectByType<EnemyManager>();
        var info = UnityEngine.Object.FindFirstObjectByType<CombatInfoUI>();
        int passed = 0;
        Action<bool, string> check = (ok, label) => { if (!ok) throw new Exception(label); passed++; };
        try
        {
            flow.RestartRun();
            Enemy target = enemies.CurrentTarget;
            int hp = target.CurrentHp;
            target.TakeDamage(30);
            check(target.CurrentHp == hp - 30 && ui.ActiveCount == 1, "normal damage unchanged, one number");
            TMP_Text normal = ui.GetComponentsInChildren<TMP_Text>().Single();
            check(normal.text == "30" && normal.color == (Color)Get(ui, "normalColor"), "normal text and yellow");
            check(normal.font.name.Contains("Maplestory") && normal.outlineWidth > 0f
                && normal.fontSharedMaterial.IsKeywordEnabled("OUTLINE_ON")
                && normal.outlineColor.r == 0 && normal.outlineColor.g == 0 && normal.outlineColor.b == 0, "Maplestory and black outline");
            check(!normal.raycastTarget, "number does not block target clicks");
            Vector2 initial = normal.rectTransform.anchoredPosition;
            float duration = (float)Get(ui, "duration");
            Call(ui, "Advance", duration * 0.6f);
            check(normal.rectTransform.anchoredPosition.y > initial.y && normal.color.a > 0f
                && normal.color.a < 1f, "rises and fades");
            info.InspectSlot(0);
            Vector2 paused = normal.rectTransform.anchoredPosition;
            Color pausedColor = normal.color;
            Call(ui, "Advance", 0f);
            check(info.IsInfoPaused && normal.rectTransform.anchoredPosition == paused
                && normal.color == pausedColor, "pause freezes animation");
            info.Resume();
            Call(ui, "Advance", duration);
            check(ui.ActiveCount == 0, "expired number cleaned");
            target.TakeDamage(18, true); // 이미 계산된 피해와 치명타 표시만 전달. 추가 추첨·배율 변경 없음.
            TMP_Text critical = ui.GetComponentsInChildren<TMP_Text>().Single();
            check(target.CurrentHp == hp - 48 && critical.text == "18"
                && critical.color == (Color)Get(ui, "criticalColor"), "critical flag only changes style");
            target.TakeDamage(0);
            check(ui.ActiveCount == 1, "zero damage ignored");
            target.TakeDamage(11);
            check(ui.ActiveCount == 2, "successive damage coexists");
            flow.RestartRun();
            check(ui.ActiveCount == 0, "restart clears old numbers");
            target = enemies.CurrentTarget;
            target.TakeDamage(999);
            check(!target.IsAlive && ui.ActiveCount == 1, "lethal number survives enemy deactivation");
            target.TakeDamage(10);
            check(ui.ActiveCount == 1, "dead target emits nothing");
            enemies.NotifyEnemyDied();
            Call(flow, "Update");
            check(flow.State == BattleFlowState.BetweenBattles, "normal victory still works");
            Call(ui, "Advance", duration);
            check(ui.ActiveCount == 0, "final damage can finish after victory");
            flow.NextBattle();
            check(flow.BattleNumber == 2 && ui.ActiveCount == 0, "next battle clean");
            return "Damage number checks passed: " + passed;
        }
        finally { flow.RestartRun(); }
    }

    private static object Get(object target, string name) => target.GetType().GetField(name, Hidden).GetValue(target);
    private static void Call(object target, string name, params object[] args)
        => target.GetType().GetMethod(name, Hidden).Invoke(target, args);
}
