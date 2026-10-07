using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 10 A37 후속 전투 HUD 검사: HP 피격·흡수·무적·회복·연속 피격·초기화, 코스트·포션·방어도 표시값,
/// 능력치 창 값·열기/닫기·클릭 차단, 사용 피드백(수락/거절), 타겟 마커·강조선.
/// 함수 호출과 Tick(dt) 진행, 일부는 실제 프레임(EditorApplication.Step) 검사이며 직접 손 입력은 아니다.
/// 런을 초기화하므로 측정 플레이 중 실행하지 않는다. 직접 더한 능력치 증가분은 끝에 되돌린다.
/// </summary>
public static class HudChecks
{
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    private static int passed;
    private static object Get(object obj, string name) => obj.GetType().GetField(name, Hidden).GetValue(obj);
    private static void Set(object obj, string name, object value) => obj.GetType().GetField(name, Hidden).SetValue(obj, value);
    private static void Call(object obj, string name, params object[] args) => obj.GetType().GetMethod(name, Hidden).Invoke(obj, args);
    private static void Check(bool ok, string label)
    {
        if (!ok) throw new Exception("HUD: " + label);
        passed++;
    }
    private static bool Near(float a, float b, float eps = 0.002f) => Mathf.Abs(a - b) <= eps;

    [MenuItem("Tools/Scroll Hunter/Check HUD (Play)")]
    public static void Menu() => Debug.Log(Run());

    public static string Run()
    {
        if (!EditorApplication.isPlaying) throw new InvalidOperationException("Battle 씬 Play 후 실행하세요.");
        var flow = UnityEngine.Object.FindFirstObjectByType<BattleFlow>();
        var tutorial = UnityEngine.Object.FindFirstObjectByType<TutorialFlow>();
        var player = UnityEngine.Object.FindFirstObjectByType<Player>();
        var cost = UnityEngine.Object.FindFirstObjectByType<CostSystem>();
        var deck = UnityEngine.Object.FindFirstObjectByType<DeckSystem>();
        var enemies = UnityEngine.Object.FindFirstObjectByType<EnemyManager>();
        var ui = UnityEngine.Object.FindFirstObjectByType<CombatUI>();
        var info = UnityEngine.Object.FindFirstObjectByType<CombatInfoUI>();
        var gauge = UnityEngine.Object.FindFirstObjectByType<HpGaugeUI>();
        var feedback = UnityEngine.Object.FindFirstObjectByType<UseFeedbackUI>();
        var stats = UnityEngine.Object.FindFirstObjectByType<StatsPanelUI>();
        var costGauge = (SegmentedGaugeUI)Get(ui, "costGauge");
        bool wasPaused = EditorApplication.isPaused;
        passed = 0;
        float maxHpAdded = 0f, critAdded = 0f, regenAdded = 0f, startAdded = 0f, maxCostAdded = 0f, shieldAdded = 0f, staggerAdded = 0f;
        int potionCapacityAdded = 0;
        Action<float> tickGauge = dt => Call(gauge, "Tick", dt);
        Action<float> tickFeedback = dt => Call(feedback, "Tick", dt);
        Func<IList> numbers = () => (IList)Get(gauge, "numbers");
        Func<string> lastNumber = () => { IList list = numbers(); return list.Count == 0 ? string.Empty : ((TMP_Text)list[list.Count - 1].GetType().GetField("text").GetValue(list[list.Count - 1])).text; };
        Action freshBattle = () =>
        {
            flow.RestartRun();
            Set(tutorial, "consumedFrame", -1);
            tutorial.Skip();
            Set(tutorial, "consumedFrame", -1);
            flow.NextBattle();
            Call(flow, "AdvanceCountdown", 3f);
            tickGauge(0f);
        };
        try
        {
            EditorApplication.isPaused = true;
            freshBattle();
            Check(flow.State == BattleFlowState.Fighting && Near(gauge.FillRatio, 1f) && Near(gauge.GhostRatio, 1f), "fresh battle full gauge without ghost");

            // --- HP 직접 피해: 숫자·게이지 즉시 감소, 잔상 유지 후 추격, 테두리 점등, 실제 감소량
            player.TakeDamage(60); // 60 → 방어력 20 적용 50
            tickGauge(0f);
            Check(player.CurrentHp == 450 && Near(gauge.FillRatio, 0.9f), "fill drops immediately");
            Check(Near(gauge.GhostRatio, 1f) && gauge.FrameFlashing && !gauge.ShieldFlashing, "ghost keeps lost range and frame flashes");
            Check(numbers().Count == 1 && lastNumber() == "-50", "actual HP loss number");
            Call(ui, "UpdateStatus");
            Check(((TMP_Text)Get(ui, "hpText")).text == "450 / 500", "HP number drops immediately");
            float hold = (float)Get(gauge, "ghostHoldSeconds"), chase = (float)Get(gauge, "ghostChaseSeconds");
            Check(Near(hold, 0.2f) && Near(chase, 0.3f), "first trial ghost timing 0.2s hold + 0.3s chase");
            tickGauge(0.1f);
            Check(Near(gauge.GhostRatio, 1f), "ghost holds during 0.2s");
            tickGauge(0.15f);
            Check(gauge.GhostRatio < 1f && gauge.GhostRatio > 0.9f, "ghost starts chasing after hold");
            tickGauge(0.3f);
            Check(Near(gauge.GhostRatio, 0.9f) && !gauge.FrameFlashing, "ghost reaches current HP after chase");

            // --- 방어도로 전부 흡수: 방패 표시만 반응
            player.AddShield(120);
            int count = numbers().Count;
            player.TakeDamage(60);
            tickGauge(0f);
            Check(player.CurrentHp == 450 && player.Shield == 70, "full absorb keeps HP");
            Check(gauge.ShieldFlashing && !gauge.FrameFlashing && numbers().Count == count + 1 && lastNumber() == "-50" && Near(gauge.GhostRatio, 0.9f), "full absorb displays shield loss only");

            var emblem = (RectTransform)Get(gauge, "shieldIcon");
            var shieldValue = (TMP_Text)Get(ui, "shieldText");
            var absorbedEntry = numbers()[numbers().Count - 1];
            var absorbedText = (TMP_Text)absorbedEntry.GetType().GetField("text").GetValue(absorbedEntry);
            var hpTemplate = (TMP_Text)Get(gauge, "numberTemplate");
            Check(absorbedText.color == shieldValue.color && Near(absorbedText.fontSize, hpTemplate.fontSize), "shield color matches value and is smaller than HP hit");
            Check(absorbedText.font == hpTemplate.font && absorbedText.outlineWidth == hpTemplate.outlineWidth && !absorbedText.raycastTarget, "shared font/outline, no click blocking");
            Check(absorbedText.transform.parent == hpTemplate.transform.parent && absorbedText.transform.parent != emblem, "popup above HP artwork and independent of bouncing icon");
            var popupParent = (RectTransform)hpTemplate.transform.parent;
            float iconX = popupParent.InverseTransformPoint(emblem.position).x - popupParent.rect.center.x;
            float iconY = popupParent.InverseTransformPoint(emblem.position).y - popupParent.rect.center.y;
            Check(Near(absorbedText.rectTransform.anchoredPosition.x, iconX)
                && absorbedText.rectTransform.anchoredPosition.y > iconY
                && absorbedText.rectTransform.anchoredPosition.y < popupParent.rect.height * 0.5f,
                "popup starts just above shield, not above HP bar");
            Vector2 absorbedOrigin = absorbedText.rectTransform.anchoredPosition;
            tickGauge(.066f);
            Check(emblem.localScale.x > 1.9f, "full absorption produces strong bounce");
            Check(absorbedText.rectTransform.anchoredPosition.y > absorbedOrigin.y, "absorb number floats upward");
            Vector3 heldScale = emblem.localScale; Vector2 heldPosition = absorbedText.rectTransform.anchoredPosition;
            tickGauge(0f);
            Check(emblem.localScale == heldScale && absorbedText.rectTransform.anchoredPosition == heldPosition, "pause freezes bounce and popup");

            // --- 일부 흡수: 방패 바운스 없이 방어도와 HP 손실을 각각 표시
            player.TakeDamage(120); // 100, 방어도 70 흡수, HP 30
            tickGauge(0f);
            Check(player.CurrentHp == 420 && player.Shield == 0, "partial absorb splits damage");
            Check(numbers().Count == count + 3 && lastNumber() == "-30" && !gauge.ShieldFlashing && gauge.FrameFlashing, "partial absorb shows shield and HP loss without shield flash");
            Check(Near(gauge.GhostRatio, 0.9f) && Near(gauge.FillRatio, 0.84f), "ghost from HP before partial hit");
            Check(emblem.localScale == Vector3.one, "partial hit cancels existing bounce immediately");
            var partialShield = numbers()[numbers().Count - 2];
            var partialText = (TMP_Text)partialShield.GetType().GetField("text").GetValue(partialShield);
            Check(partialText.text == "-70" && (bool)partialShield.GetType().GetField("shield").GetValue(partialShield), "partial shows actual absorbed70");
            Vector2 shieldOrigin = (Vector2)partialShield.GetType().GetField("origin").GetValue(partialShield);
            var hpPopup = (TMP_Text)numbers()[numbers().Count - 1].GetType().GetField("text").GetValue(numbers()[numbers().Count - 1]);
            var hpValue = (TMP_Text)Get(ui, "hpText");
            Check(partialText.transform.parent == hpValue.transform.parent
                && partialText.transform.GetSiblingIndex() > hpValue.transform.GetSiblingIndex()
                && partialText.transform.GetSiblingIndex() > hpPopup.transform.GetSiblingIndex(), "absorption renders in front of HP text and later HP popup");

            // --- 연속 피격: 잔상은 첫 피격 전 HP를 유지, 숫자는 위로 쌓임
            IList list = numbers();
            Vector2 previousOrigin = (Vector2)list[list.Count - 1].GetType().GetField("origin").GetValue(list[list.Count - 1]);
            tickGauge(0.1f);
            player.TakeDamage(60);
            tickGauge(0f);
            list = numbers();
            Vector2 pushed = (Vector2)list[list.Count - 2].GetType().GetField("origin").GetValue(list[list.Count - 2]);
            Check(player.CurrentHp == 370 && Near(gauge.GhostRatio, 0.9f), "consecutive hit keeps earlier ghost");
            Check(pushed.y > previousOrigin.y && lastNumber() == "-50", "consecutive numbers stack upward");
            Check(shieldOrigin == (Vector2)partialShield.GetType().GetField("origin").GetValue(partialShield), "HP number does not move shield number lane");
            tickGauge(1.2f);
            Check(numbers().Count == 0 && Near(gauge.GhostRatio, gauge.FillRatio), "numbers and ghost settle");

            // --- 무적: 피해가 없으면 HP 피격 연출 없음
            player.SetSanctuary(true);
            player.TakeDamage(60);
            tickGauge(0f);
            Check(player.CurrentHp == 370 && numbers().Count == 0 && !gauge.FrameFlashing && !gauge.ShieldFlashing, "invulnerable hit shows nothing");
            player.SetSanctuary(false);

            // --- 실제 회복: 초록빛·회복량, 잔상 없음
            Check(player.TryUsePotion(), "potion accepted");
            tickGauge(0f);
            Check(player.CurrentHp == 500 && gauge.HealGlowing && lastNumber() == "+130" && Near(gauge.GhostRatio, 1f), "potion heal glow with actual restored amount");
            tickGauge(1.2f);

            // --- 최대 HP 증가는 회복으로 표시하지 않음
            player.AddMaxHpBonus(50); maxHpAdded += 50;
            tickGauge(0f);
            Check(!gauge.HealGlowing && numbers().Count == 0 && Near(gauge.FillRatio, 500f / 550f) && Near(gauge.GhostRatio, gauge.FillRatio), "max HP increase is not a heal");
            player.AddMaxHpBonus(-50); maxHpAdded -= 50;

            // --- 코스트 게이지: 소수 충전·1코스트 눈금·상한 변경
            typeof(CostSystem).GetProperty("Current").SetValue(cost, 3.5f);
            Call(ui, "UpdateStatus");
            Check(Near(costGauge.FillRatio, 0.35f) && costGauge.TickCount == 9, "fractional cost fill and 9 ticks at max 10");
            Check(((TMP_Text)Get(ui, "costText")).text == "3.5 / 10", "cost text current/max");
            cost.AddMaximumCostBonus(2); maxCostAdded += 2;
            Call(ui, "UpdateStatus");
            Check(costGauge.TickCount == 11 && Near(costGauge.FillRatio, 3.5f / 12f) && ((TMP_Text)Get(ui, "costText")).text == "3.5 / 12", "max cost change rebuilds ticks");
            cost.AddMaximumCostBonus(-2); maxCostAdded -= 2;
            Call(ui, "UpdateStatus");
            Check(costGauge.TickCount == 9, "ticks follow restored max");

            // --- 포션 표시: 실제 보유량·최대치(패시브 증가 포함)·Shift
            Call(ui, "UpdateStatus");
            var potionText = (TMP_Text)Get(ui, "potionText");
            var potionKey = (TMP_Text)Get(ui, "potionKeyText");
            var potionIcon = (Image)Get(ui, "potionIcon");
            Check(potionText.text == "2/3" && potionKey.text == "Shift" && potionIcon.color == Color.white, "potion count after one use");
            player.TakeDamage(300);
            Check(player.TryUsePotion() && player.TryUsePotion() && !player.TryUsePotion(), "remaining two potions then rejected");
            Call(ui, "UpdateStatus");
            Check(potionText.text == "0/3" && potionIcon.color == (Color)Get(ui, "potionEmptyIconColor") && potionKey.color == (Color)Get(ui, "potionUnavailableColor"), "empty potion dims");
            player.AddPotionCapacityBonus(1); potionCapacityAdded += 1;
            Call(ui, "UpdateStatus");
            Check(potionText.text == "0/4", "potion max follows passive bonus");
            player.AddPotionCapacityBonus(-1); potionCapacityAdded -= 1;

            // --- 방어도 표시: 방패 문양·숫자, 최대치 게이지 없음
            Call(ui, "UpdateStatus");
            var shieldText = (TMP_Text)Get(ui, "shieldText");
            var shieldIcon = (Image)Get(ui, "shieldIcon");
            Check(!shieldText.enabled && shieldIcon.color == (Color)Get(ui, "shieldEmptyIconColor"), "zero shield keeps dim emblem, no number");
            player.AddShield(120);
            Call(ui, "UpdateStatus");
            Check(shieldText.enabled && shieldText.text == "120" && shieldIcon.color == Color.white, "shield number and emblem");

            // --- 능력치 창: 정지 없이 열기/닫기, 현재 적용값·기본값·증가분
            float timeScale = Time.timeScale;
            stats.SetOpen(true);
            Check(stats.Available && stats.IsOpen && Time.timeScale == timeScale && !info.IsInfoPaused, "stats opens without pausing");
            const BindingFlags RowField = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            Func<string, string, TMP_Text> rowText = (row, field) => { object r = Get(stats, row); return (TMP_Text)r.GetType().GetField(field, RowField).GetValue(r); };
            Func<string, TMP_Text> value = row => rowText(row, "value");
            Func<string, TMP_Text> detail = row => rowText(row, "detail");
            Check(value("maxHp").text == "500" && value("defense").text == "20" && detail("defense").text.Contains("16.7"), "survival rows");
            Check(value("criticalChance").text == "15%" && value("criticalMultiplier").text == "×1.5", "critical rows");
            Check(value("costRegeneration").text == "0.8" && value("startingCost").text == "3" && value("maximumCost").text == "10", "cost rows");
            Check(value("shieldGain").text == "100%" && value("interruptStagger").text == "2.5초", "response rows");
            Check(!detail("maxHp").gameObject.activeSelf && !detail("criticalChance").gameObject.activeSelf, "no bonus detail without passives");
            Check(stats.DescribePassives().Contains("패시브 없음"), "no passives line");
            player.AddMaxHpBonus(50); maxHpAdded += 50;
            player.AddCriticalChanceBonus(5); critAdded += 5;
            cost.AddRegenerationBonus(0.1f); regenAdded += 0.1f;
            cost.AddStartingCostBonus(1); startAdded += 1;
            cost.AddMaximumCostBonus(2); maxCostAdded += 2;
            player.AddShieldGainBonus(0.2f); shieldAdded += 0.2f;
            player.AddInterruptStaggerBonus(0.5f); staggerAdded += 0.5f;
            stats.Refresh();
            Check(value("maxHp").text == "550" && detail("maxHp").gameObject.activeSelf && detail("maxHp").text == "기본 500 · 패시브 +50", "max HP final first, base and bonus second");
            Check(value("criticalChance").text == "20%" && detail("criticalChance").text.Contains("+5%p"), "crit chance with percentage points");
            Check(value("costRegeneration").text == "0.9" && value("startingCost").text == "4" && value("maximumCost").text == "12", "cost final values");
            Check(value("shieldGain").text == "120%" && value("interruptStagger").text == "3.0초" && detail("interruptStagger").text == "기본 2.5초 · 패시브 +0.5초", "response final values");
            // 카드 정보창은 능력치 창 왼쪽으로 비켜난다.
            var cardPanel = (GameObject)Get(info, "cardPanel");
            float baseX = (float)Get(info, "cardPanelBaseX");
            Set(info, "selectedSlot", 0);
            Call(info, "LateUpdate");
            Check(cardPanel.activeSelf && Near(((RectTransform)cardPanel.transform).anchoredPosition.x, baseX - stats.Width - (float)Get(info, "statsPanelGap"), 0.5f), "card info avoids stats panel");
            // 창 위 클릭은 창이 받는다(뒤의 적·스킬로 통과하지 않음).
            var panelRect = (RectTransform)Get(stats, "panel");
            EditorApplication.Step(); // 막 켠 그래픽은 한 번 그려져야(깊이 값) 레이캐스트 대상이 된다.
            Vector3[] corners = new Vector3[4];
            panelRect.GetWorldCorners(corners);
            var pointer = new PointerEventData(EventSystem.current) { position = (corners[0] + corners[2]) * 0.5f };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            Check(hits.Count > 0 && hits[0].gameObject.transform.IsChildOf(panelRect), "stats panel blocks clicks");
            stats.SetOpen(false);
            Set(info, "selectedSlot", 0); // 위에서 진행한 프레임에 마우스가 카드 밖이라 선택이 풀렸으므로 다시 지정
            Call(info, "LateUpdate");
            Check(!stats.IsOpen && cardPanel.activeSelf && Near(((RectTransform)cardPanel.transform).anchoredPosition.x, baseX, 0.5f), "closing restores card info position");
            Set(info, "selectedSlot", -1);
            player.AddMaxHpBonus(-50); maxHpAdded -= 50;
            player.AddCriticalChanceBonus(-5); critAdded -= 5;
            cost.AddRegenerationBonus(-0.1f); regenAdded -= 0.1f;
            cost.AddStartingCostBonus(-1); startAdded -= 1;
            cost.AddMaximumCostBonus(-2); maxCostAdded -= 2;
            player.AddShieldGainBonus(-0.2f); shieldAdded -= 0.2f;
            player.AddInterruptStaggerBonus(-0.5f); staggerAdded -= 0.5f;

            // --- 사용 피드백: 수락만 재생, 사용한 카드 아이콘 표시, 순환·잠금 불변
            freshBattle();
            typeof(CostSystem).GetProperty("Current").SetValue(cost, 10f);
            SkillData used = deck.GetHandCard(0);
            Check(deck.TryUseSlot(0), "slot 0 accepted");
            Check(feedback.ActiveSlotCount == 1 && feedback.UsedIconSprite(0) == used.Icon && deck.GetHandCard(0) != used, "feedback shows used card over new card");
            Check(Near(deck.LockRemaining, Mathf.Max(used.CastTime, 0.4f), 0.0001f), "feedback does not change lock");
            Check(!deck.TryUseSlot(1) && feedback.ActiveSlotCount == 1, "rejected locked input has no success effect");
            tickFeedback(0.5f);
            Check(feedback.ActiveSlotCount == 0, "feedback ends");
            Set(deck, "<LockRemaining>k__BackingField", 0f);
            typeof(CostSystem).GetProperty("Current").SetValue(cost, 0f);
            Check(!deck.TryUseSlot(1) && feedback.ActiveSlotCount == 0, "rejected cost-short input has no effect");
            Check(!player.TryUsePotion() && !feedback.PotionPlaying, "rejected potion at full HP has no effect");
            player.TakeDamage(200);
            Check(player.TryUsePotion() && feedback.PotionPlaying, "accepted potion plays green effect");
            tickFeedback(1f);
            Check(!feedback.PotionPlaying, "potion effect ends");

            // --- 타겟 마커·강조선(실제 프레임)
            var marker = (RectTransform)Get(ui, "targetMarker");
            var line = (RectTransform)Get(ui, "targetGaugeLine");
            var group = (CanvasGroup)Get(ui, "targetGaugeLineGroup");
            Vector3 baseScale = (Vector3)Get(ui, "markerBaseScale");
            for (int i = 0; i < 20; i++) EditorApplication.Step();
            Enemy first = enemies.CurrentTarget;
            Func<Enemy, bool> lineOn = target => line.gameObject.activeSelf
                && Vector2.Distance(line.position, target.HpBarRect.TransformPoint(target.HpBarRect.rect.center)) < 1f;
            Check(marker.gameObject.activeSelf && lineOn(first) && Near(marker.localScale.x, baseScale.x, 0.001f) && Near(group.alpha, (float)Get(ui, "targetGaugeLineIdleAlpha")), "steady marker and thin line on target");
            Call(enemies, "StepTarget", 1);
            EditorApplication.Step();
            Enemy second = enemies.CurrentTarget;
            Check(second != first && marker.localScale.x > baseScale.x * 1.05f && group.alpha > 0.9f && lineOn(second), "target change briefly emphasized");
            for (int i = 0; i < 20; i++) EditorApplication.Step();
            Check(Near(marker.localScale.x, baseScale.x, 0.001f) && lineOn(second), "emphasis settles");
            second.TakeDamage(second.CurrentHp);
            enemies.NotifyEnemyDied();
            EditorApplication.Step();
            Check(enemies.CurrentTarget == first && lineOn(first), "dead target auto-moves marker and line");

            // --- 전투 초기화: 잔상·숫자·점등 정리
            player.TakeDamage(60);
            tickGauge(0f);
            flow.RestartRun();
            tickGauge(0f);
            Check(numbers().Count == 0 && !gauge.FrameFlashing && Near(gauge.GhostRatio, gauge.FillRatio) && feedback.ActiveSlotCount == 0, "restart clears feedback");
            // --- 방어도가 정확히 소진되어도 HP가 유지되면 바운스. 종료·재시작 때 원래 크기로 복귀.
            freshBattle();
            player.AddShield(50);
            player.TakeDamage(60);
            tickGauge(.066f);
            Check(player.Shield == 0 && player.CurrentHp == 500 && emblem.localScale.x > 1.9f && lastNumber() == "-50", "exact depletion is full absorption");
            tickGauge(.24f);
            Check(emblem.localScale == Vector3.one && !gauge.ShieldFlashing, "bounce settles after duration");
            player.AddShield(50);
            player.TakeDamage(60);
            tickGauge(.066f);
            flow.RestartRun();
            Check(emblem.localScale == Vector3.one && numbers().Count == 0 && !gauge.ShieldFlashing, "restart clears active shield bounce and popups");
            // --- 남은 HP를 넘는 피해는 실제 감소량만 표시
            freshBattle();
            player.TakeDamage(99999);
            Check(lastNumber() == "-500", "overkill shows actual HP lost");
            flow.RestartRun();
            Check(!stats.Available && !((Button)Get(stats, "toggleButton")).gameObject.activeSelf || !stats.Available, "stats hidden outside normal battle");
            return "HUD checks passed: " + passed + ". Function/Tick and frame-step checks; physical input not covered.";
        }
        finally
        {
            if (maxHpAdded != 0f) player.AddMaxHpBonus(-maxHpAdded);
            if (critAdded != 0f) player.AddCriticalChanceBonus(-critAdded);
            if (regenAdded != 0f) cost.AddRegenerationBonus(-regenAdded);
            if (startAdded != 0f) cost.AddStartingCostBonus(-startAdded);
            if (maxCostAdded != 0f) cost.AddMaximumCostBonus(-maxCostAdded);
            if (shieldAdded != 0f) player.AddShieldGainBonus(-shieldAdded);
            if (staggerAdded != 0f) player.AddInterruptStaggerBonus(-staggerAdded);
            if (potionCapacityAdded != 0) player.AddPotionCapacityBonus(-potionCapacityAdded);
            player.SetSanctuary(false);
            stats.SetOpen(false);
            flow.RestartRun();
            EditorApplication.isPaused = wasPaused;
        }
    }
}
