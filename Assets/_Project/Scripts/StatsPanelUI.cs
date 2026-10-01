using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 우측 능력치 창(10 A37 후속). 우측 상단 버튼으로 열고 닫기만 하며 게임을 정지시키지 않는다.
/// 열려 있는 동안 현재 적용된 최종값을 먼저, 기본값·증가분을 보조로 실제 상태에서 읽어 표시한다.
/// 플레이어에게 공격력 스탯은 없으므로 항목을 만들지 않는다.
/// </summary>
public class StatsPanelUI : MonoBehaviour
{
    [System.Serializable]
    private class StatRow
    {
        public TMP_Text value;
        [Tooltip("기본값·증가분 보조 설명. 증가분이 없으면 숨긴다")]
        public TMP_Text detail;
        public string valueFormat = "{0:0.#}";
        [Tooltip("{0} 기본값, {1} 증가분")]
        public string detailFormat = "기본 {0:0.#} · 패시브 +{1:0.#}";
    }

    [SerializeField] private Player player;
    [SerializeField] private CostSystem cost;
    [SerializeField] private DeckSystem deck;
    [SerializeField] private BattleFlow flow;
    [SerializeField] private TutorialFlow tutorial;
    [SerializeField] private Button toggleButton;
    [Tooltip("창이 열려 있는 동안 버튼에 켜지는 표시")]
    [SerializeField] private GameObject toggleOnMark;
    [SerializeField] private RectTransform panel;

    [Header("항목")]
    [SerializeField] private StatRow maxHp;
    [SerializeField] private StatRow defense;
    [SerializeField] private StatRow criticalChance;
    [SerializeField] private StatRow criticalMultiplier;
    [SerializeField] private StatRow costRegeneration;
    [SerializeField] private StatRow startingCost;
    [SerializeField] private StatRow maximumCost;
    [SerializeField] private StatRow shieldGain;
    [SerializeField] private StatRow interruptStagger;
    [Tooltip("패시브 이름(왼쪽)과 중첩(오른쪽). 같은 줄 순서로 채운다")]
    [SerializeField] private TMP_Text passiveNames;
    [SerializeField] private TMP_Text passiveStacks;

    [Header("표시 문자열")]
    [Tooltip("{0} 방어력에 따른 피해 감소율(%)")]
    [SerializeField] private string defenseDetailFormat = "받는 피해 {0:0.#}% 감소 · 방어도와 별개";
    [SerializeField] private string passiveStackFormat = "{0}/{1}";
    [SerializeField] private string unlimitedStackFormat = "{0}";
    [SerializeField] private string noPassivesText = "패시브 없음";

    private bool wantOpen;
    private readonly List<string> nameLines = new List<string>();
    private readonly List<string> stackLines = new List<string>();

    public bool IsOpen => panel != null && panel.gameObject.activeSelf;
    public float Width => panel != null ? panel.rect.width : 0f;
    /// <summary>버튼이 보이는 상태(일반 전투 중). 튜토리얼·편성·보상·결과 화면에서는 숨긴다.</summary>
    public bool Available => flow != null
        && (flow.State == BattleFlowState.Fighting || flow.State == BattleFlowState.CountingDown)
        && (tutorial == null || !tutorial.isActiveAndEnabled || !tutorial.Active);

    private void Awake()
    {
        if (player == null) player = FindFirstObjectByType<Player>();
        if (cost == null) cost = FindFirstObjectByType<CostSystem>();
        if (deck == null) deck = FindFirstObjectByType<DeckSystem>();
        if (flow == null) flow = FindFirstObjectByType<BattleFlow>();
        if (tutorial == null) tutorial = FindFirstObjectByType<TutorialFlow>();
        if (toggleButton != null) toggleButton.onClick.AddListener(Toggle);
    }

    private void OnDestroy()
    {
        if (toggleButton != null) toggleButton.onClick.RemoveListener(Toggle);
    }

    /// <summary>표시만 열고 닫는다. 시간·입력 상태는 바꾸지 않는다.</summary>
    public void Toggle() => SetOpen(!wantOpen);

    public void SetOpen(bool open)
    {
        wantOpen = open;
        Refresh();
    }

    private void LateUpdate() => Refresh();

    public void Refresh()
    {
        bool available = Available;
        if (toggleButton != null && toggleButton.gameObject.activeSelf != available) toggleButton.gameObject.SetActive(available);
        bool show = available && wantOpen;
        if (toggleOnMark != null && toggleOnMark.activeSelf != show) toggleOnMark.SetActive(show);
        if (panel != null && panel.gameObject.activeSelf != show) panel.gameObject.SetActive(show);
        if (!show || player == null || cost == null || deck == null) return;

        Show(maxHp, player.MaxHp, player.BaseMaxHp, player.MaxHpBonus);
        float reduction = player.Defense / (player.Defense + 100f) * 100f;
        Show(defense, player.Defense, 0f, 0f, string.Format(defenseDetailFormat, reduction));
        Show(criticalChance, player.CriticalChance, player.BaseCriticalChance, player.CriticalChanceBonus);
        Show(criticalMultiplier, player.CriticalMultiplier, 0f, 0f);
        Show(costRegeneration, cost.RegenerationPerSecond, cost.BaseRegeneration, cost.RegenerationBonus);
        Show(startingCost, cost.StartingCost, cost.BaseStartingCost, cost.StartingCostBonus);
        Show(maximumCost, cost.Max, cost.BaseMax, cost.MaximumCostBonus);
        Show(shieldGain, player.ShieldGainMultiplier, 1f, player.ShieldGainBonus);
        Show(interruptStagger, deck.InterruptStagger, deck.BaseInterruptStagger, player.InterruptStaggerBonus);
        FillPassives();
        if (passiveNames != null) passiveNames.text = string.Join("\n", nameLines);
        if (passiveStacks != null) passiveStacks.text = string.Join("\n", stackLines);
    }

    /// <summary>획득 패시브와 중첩 목록(이름 중첩 한 줄씩). 창이 닫혀 있어도 현재 상태로 만든다.</summary>
    public string DescribePassives()
    {
        FillPassives();
        var lines = new List<string>();
        for (int i = 0; i < nameLines.Count; i++)
            lines.Add(stackLines[i].Length > 0 ? nameLines[i] + " " + stackLines[i] : nameLines[i]);
        return string.Join("\n", lines);
    }

    private void FillPassives()
    {
        nameLines.Clear();
        stackLines.Clear();
        if (flow != null)
            foreach (KeyValuePair<RunRewardEffect, int> entry in flow.PassiveStacks)
            {
                nameLines.Add(entry.Key.DisplayName);
                stackLines.Add(entry.Key.MaxStacks > 0 ? string.Format(passiveStackFormat, entry.Value, entry.Key.MaxStacks)
                    : string.Format(unlimitedStackFormat, entry.Value));
            }
        if (nameLines.Count == 0) { nameLines.Add(noPassivesText); stackLines.Add(string.Empty); }
    }

    private static void Show(StatRow row, float value, float baseValue, float bonus, string fixedDetail = null)
    {
        if (row == null) return;
        if (row.value != null) row.value.text = string.Format(row.valueFormat, value);
        if (row.detail == null) return;
        string detail = fixedDetail ?? (Mathf.Abs(bonus) > 0.0001f ? string.Format(row.detailFormat, baseValue, bonus) : string.Empty);
        bool visible = detail.Length > 0;
        if (row.detail.gameObject.activeSelf != visible) row.detail.gameObject.SetActive(visible);
        row.detail.text = detail;
    }
}
