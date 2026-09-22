using System;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;

/// <summary>Play 모드에서 피해·차단 표시·피해 보존·정리 확인 후 첫 전투로 복귀.</summary>
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
            // 빨강 대상은 입력을 수락해도 성공 문구가 생기지 않는다.
            var deck = UnityEngine.Object.FindFirstObjectByType<DeckSystem>();
            target = enemies.CurrentTarget;
            check(target.CastColor == CastColor.Red && deck.TryUseSlot(3), "red silence input accepted");
            Call(deck, "AdvanceCast", 0.21f);
            check(ui.ActiveCount == 0 && target.IsCasting, "red failure emits no interrupt popup");

            flow.RestartRun();
            target = enemies.CurrentTarget;
            check(target.CastColor == CastColor.Orange && deck.TryUseSlot(3)
                && ui.ActiveCount == 0, "orange silence has no premature popup");
            Call(deck, "AdvanceCast", 0.21f);
            check(target.IsStaggered && ui.ActiveCount == 1, "actual orange interrupt emits once");
            TMP_Text interrupt = ui.GetComponentsInChildren<TMP_Text>().Single();
            TMP_Text template = (TMP_Text)Get(ui, "numberTemplate");
            check(interrupt.text == "차단!" && interrupt.color == (Color)Get(ui, "interruptColor")
                && interrupt.fontSize > template.fontSize, "large purple interrupt text");
            check(interrupt.font == template.font && "차단!".All(c => interrupt.font.HasCharacter(c))
                && interrupt.fontSharedMaterial == template.fontSharedMaterial
                && interrupt.fontSharedMaterial.IsKeywordEnabled("OUTLINE_ON")
                && !interrupt.raycastTarget, "same font and outline, glyphs present, no click blocking");
            Vector2 interruptStart = interrupt.rectTransform.anchoredPosition;
            Vector3 projected = Camera.main.WorldToScreenPoint(target.transform.position + (Vector3)Get(ui, "worldOffset"));
            Vector2 center;
            RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)ui.transform, projected, null, out center);
            center += (Vector2)Get(ui, "screenOffset");
            check(Mathf.Abs(interruptStart.x - center.x) <= (float)Get(ui, "horizontalSpread")
                && Mathf.Abs(interruptStart.y - center.y) < 0.01f, "same bounded random spawn");
            check(target.TryInterrupt(2.5f) == InterruptResult.NotCasting && ui.ActiveCount == 1,
                "noncasting emits no duplicate popup");
            Call(ui, "Advance", duration * 0.6f);
            check(interrupt.rectTransform.anchoredPosition.y > interruptStart.y
                && interrupt.rectTransform.anchoredPosition.x == interruptStart.x
                && interrupt.color.a > 0f && interrupt.color.a < 1f, "interrupt rises and fades with fixed X");
            Call(ui, "Advance", duration);
            check(ui.ActiveCount == 0, "interrupt expires");

            flow.RestartRun();
            target = enemies.CurrentTarget;
            typeof(Enemy).GetField("current", Hidden).SetValue(target, target.Data.Find(CastColor.Green));
            check(target.TryInterrupt(2.5f) == InterruptResult.Success && ui.ActiveCount == 1,
                "green success also emits");
            flow.RestartRun();
            check(ui.ActiveCount == 0, "restart clears interrupt");

            // 침묵 시전 중 대상의 초록 공격이 먼저 끝난 경우.
            target = enemies.CurrentTarget;
            typeof(Enemy).GetField("current", Hidden).SetValue(target, target.Data.Find(CastColor.Green));
            check(deck.TryUseSlot(3), "late silence input accepted");
            Call(target, "Fire");
            Call(deck, "AdvanceCast", 0.21f);
            check(ui.ActiveCount == 0 && !target.IsStaggered, "expired cast emits no success popup");
            return "Damage / interrupt display checks passed: " + passed;
        }
        finally { flow.RestartRun(); }
    }

    private static object Get(object target, string name) => target.GetType().GetField(name, Hidden).GetValue(target);
    private static void Call(object target, string name, params object[] args)
        => target.GetType().GetMethod(name, Hidden).Invoke(target, args);
}
