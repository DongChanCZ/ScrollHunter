using System;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;

/// <summary>현재 적 HP와 무관한 배속·표시 검사. 전투를 초기화하므로 측정 플레이 중 실행하지 않는다.</summary>
public static class CombatReadabilityChecks
{
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    private static int passed;
    private static object Get(object obj, string field) => obj.GetType().GetField(field, Hidden).GetValue(obj);
    private static void Set(object obj, string field, object value) => obj.GetType().GetField(field, Hidden).SetValue(obj, value);
    private static void Call(object obj, string method) => obj.GetType().GetMethod(method, Hidden).Invoke(obj, null);
    private static void Check(bool value, string label)
    {
        if (!value) throw new Exception("Readability: " + label);
        passed++;
    }

    [MenuItem("Tools/Scroll Hunter/Check Combat Readability (Play)")]
    public static void RunMenu() => Debug.Log(Run());

    [MenuItem("Tools/Scroll Hunter/Check Information Text")]
    public static void InformationMenu() => Debug.Log(RunInformation());

    // 저장된 14종 설명과 편성·보상 영역을 검사. 전투나 에셋은 바꾸지 않는다.
    public static string RunInformation()
    {
        var info = UnityEngine.Object.FindFirstObjectByType<CombatInfoUI>();
        var reward = UnityEngine.Object.FindFirstObjectByType<BattleRewardUI>();
        var cardText = (TMP_Text)Get(info, "cardText");
        var formation = ((TMP_Text[])Get(reward, "deckLabels"))[0];
        var candidates = (Array)Get(reward, "skillSlots");
        var candidate = (TMP_Text)candidates.GetValue(0).GetType().GetField("detail").GetValue(candidates.GetValue(0));
        passed = 0;
        Check((KeyCode)Get(info, "pauseKey") == KeyCode.A && (KeyCode)Get(info, "speedKey") == KeyCode.S, "A/S bindings");
        Check(string.Format((string)Get(info, "speedFormat"), 0.5f) == "속도 0.5배속"
            && string.Format((string)Get(info, "speedFormat"), 1f) == "속도 1배속", "speed labels");
        int cards = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:SkillData", new[] { "Assets/_Project/Data" }))
        {
            var card = AssetDatabase.LoadAssetAtPath<SkillData>(AssetDatabase.GUIDToAssetPath(guid));
            if (!card.name.Contains("SK") && card.name != "Skill_Silence" && card.name != "Skill_CrystalShield") continue;
            cards++;
            string effect = info.DescribeCardEffect(card);
            Check(effect.Contains("총합 피해") == (card.Damage > 0), card.DisplayName + " total only for damage");
            Check(!effect.Contains("최대 피해"), card.DisplayName + " no maximum label");
            foreach (var label in new[] { formation, candidate })
                Check(label.GetPreferredValues(effect, label.rectTransform.rect.width, 0).y <= label.rectTransform.rect.height,
                    card.DisplayName + " fits " + label.name);
            Check(cardText.GetPreferredValues(info.DescribeCard(card), cardText.rectTransform.rect.width, 0).y
                + ((GameObject)Get(info, "cardPanel")).GetComponent<RectTransform>().rect.height
                - cardText.rectTransform.rect.height <= (float)Get(info, "panelMaxHeight"), card.DisplayName + " fits battle panel");
        }
        Check(cards == 14, "current 14 cards");
        foreach (string file in new[] { "Skill_SK02_IceArrow", "Skill_SK06_MagicSpark", "Skill_SK12_Suppression", "Skill_SK09_Blizzard" })
        {
            var sample = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<SkillData>("Assets/_Project/Data/ScrollHunter/SkillData/" + file + ".asset"));
            try
            {
                Set(sample, "damage", 7); Set(sample, "hitCount", 3);
                string effect = info.DescribeCardEffect(sample);
                Check(effect.Contains("총합 피해 <b>21</b>") && effect.Contains("적 1체") && effect.Contains("크리티컬/방어 적용 전"), file + " 7 x 3 = 21 per enemy");
            }
            finally { UnityEngine.Object.DestroyImmediate(sample); }
        }
        return "Information text: " + passed + " passed / " + cards + " cards";
    }

    public static string Run()
    {
        if (!EditorApplication.isPlaying) throw new InvalidOperationException("Battle 씬 Play 후 실행하세요.");
        var flow = UnityEngine.Object.FindFirstObjectByType<BattleFlow>();
        var info = UnityEngine.Object.FindFirstObjectByType<CombatInfoUI>();
        var ui = UnityEngine.Object.FindFirstObjectByType<CombatUI>();
        var deck = UnityEngine.Object.FindFirstObjectByType<DeckSystem>();
        var cost = UnityEngine.Object.FindFirstObjectByType<CostSystem>();
        var player = UnityEngine.Object.FindFirstObjectByType<Player>();
        var enemies = UnityEngine.Object.FindFirstObjectByType<EnemyManager>();
        var metrics = UnityEngine.Object.FindFirstObjectByType<CombatMetrics>();
        var views = (CombatUI.HandSlotView[])Get(ui, "handSlots");
        float originalSpeed = info.BattleSpeed;
        passed = 0;
        try
        {
            flow.RestartRun();
            if (info.BattleSpeed != 1f) info.ToggleSpeed();
            info.ToggleSpeed();
            Check(info.BattleSpeed == 0.5f && Time.timeScale == 0.5f, "half speed");
            info.TogglePause();
            Check(info.IsInfoPaused && Time.timeScale == 0f && !deck.TryUseSlot(0), "pause blocks cards");
            info.ToggleSpeed();
            Check(info.BattleSpeed == 1f && Time.timeScale == 0f && info.IsInfoPaused, "paused selection stays paused");
            info.Resume();
            Check(Time.timeScale == 1f, "resume selected speed");
            info.ToggleSpeed();
            info.TogglePause();
            info.Resume();
            Check(Time.timeScale == 0.5f, "resume half speed");
            flow.RestartRun();
            Check(info.BattleSpeed == 0.5f && Time.timeScale == 0.5f, "restart retains speed");
            enemies.CurrentTarget.TakeDamage(enemies.CurrentTarget.CurrentHp);
            enemies.NotifyEnemyDied();
            Call(flow, "Update");
            Check(flow.State == BattleFlowState.BetweenBattles && Time.timeScale == 0f, "victory waits");
            info.ToggleSpeed();
            Check(info.BattleSpeed == 0.5f && Time.timeScale == 0f, "result cannot restart time");
            flow.SkipReward();
            flow.NextBattle();
            Check(flow.BattleNumber == 2 && Time.timeScale == 0.5f, "C retains speed");
            enemies.CurrentTarget.TakeDamage(enemies.CurrentTarget.CurrentHp);
            enemies.NotifyEnemyDied();
            Call(flow, "Update");
            flow.SkipReward();
            flow.NextBattle();
            Check(flow.BattleNumber == 3 && Time.timeScale == 0.5f, "BC retains speed");
            player.TakeDamage(100000);
            Call(flow, "Update");
            info.ToggleSpeed();
            info.Resume();
            Check(flow.State == BattleFlowState.Defeat && Time.timeScale == 0f, "defeat stays stopped");
            Check(((string)Get(metrics, "pendingSummary")) == null, "summary flushed at end");
            flow.RestartRun();
            Call(info, "LateUpdate");
            Check(((TMP_Text)Get(info, "speedLabel")).text.Contains("0.5"), "speed label");

            Set(cost, "<Current>k__BackingField", 0f);
            Call(ui, "LateUpdate");
            Check(views[0].costCover.enabled && views[0].costCover.fillAmount == 1f, "zero cost full cover");
            Set(cost, "<Current>k__BackingField", 0.5f);
            Call(ui, "LateUpdate");
            Check(views[0].costCover.fillAmount == 0.5f, "half charged");
            Check(views[0].costText.color == (Color)Get(ui, "costShortColor"), "red insufficient cost");
            Set(cost, "<Current>k__BackingField", 1f);
            Call(ui, "LateUpdate");
            Check(!views[0].costCover.enabled && views[1].costCover.fillAmount == 0.75f, "per-card cost denominator");
            Check(views[0].typeText.text == "단일" && views[1].typeText.text == "광역"
                && views[2].typeText.text == "차단" && views[3].typeText.text == "방어", "four badges");
            foreach (var view in views)
                Check(!view.costCover.raycastTarget && view.skillIcon != null && !view.skillIcon.raycastTarget
                    && view.costCover.transform.GetSiblingIndex() > view.skillIcon.transform.GetSiblingIndex()
                    && view.costCover.transform.GetSiblingIndex() < view.costText.transform.GetSiblingIndex()
                    && view.costCover.sprite != null && view.costCover.type == UnityEngine.UI.Image.Type.Filled
                    && view.costCover.fillMethod == UnityEngine.UI.Image.FillMethod.Radial360
                    && view.costCover.fillOrigin == (int)UnityEngine.UI.Image.Origin360.Top
                    && !view.costCover.fillClockwise, "clockwise reveal, behind labels and no input blocking");
            for (int i = 0; i < views.Length; i++)
                Check(views[i].skillIcon.sprite == deck.GetHandCard(i).Icon && views[i].skillIcon.enabled
                    && !views[i].nameText.enabled, "current hand icon replaces name");
            Sprite expectedIcon = deck.PeekNext().Icon;
            Set(cost, "<Current>k__BackingField", 3f);
            Check(deck.TryUseSlot(0), "input accepted");
            Call(ui, "LateUpdate");
            Check(views[0].typeText.text == "광역" && !views[0].costCover.enabled
                && views[0].background.color == (Color)Get(ui, "slotLockedColor")
                && !string.IsNullOrEmpty(views[0].lockText.text), "rotation updates badge and GCD timer");
            Check(views[0].skillIcon.sprite == expectedIcon, "input rotates icon with card");
            Check(((UnityEngine.UI.Image)Get(ui, "nextCardIcon")).sprite == deck.PeekNext().Icon, "next icon follows queue");
            player.ApplyStun(2f);
            Call(ui, "LateUpdate");
            Check(views[0].background.color == (Color)Get(ui, "slotStunnedColor") && !views[0].costCover.enabled
                && Array.TrueForAll(views, view => string.IsNullOrEmpty(view.lockText.text)), "purple stun without slot timer");
            Check(((TMP_Text)Get(ui, "stunText")).enabled, "central stun label");
            player.BeginBattle(false);
            deck.EndBattle();
            enemies.CurrentTarget.TryInterrupt(2.5f);
            Call(ui, "LateUpdate");
            Check(deck.GetSlotState(2) == SlotState.NoValidTarget && views[2].lockText.text.Length > 0
                && !views[2].costCover.enabled, "noncasting interrupt locked");
        }
        finally
        {
            flow.RestartRun();
            if (info.BattleSpeed != originalSpeed) info.ToggleSpeed();
            flow.RestartRun();
            Call(ui, "LateUpdate");
        }
        return "Combat readability checks: " + passed + " passed (current HP preserved)";
    }
}

