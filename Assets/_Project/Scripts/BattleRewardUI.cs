using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>전투 사이 보상/교체 화면. 편성의 변경과 진행 조건은 BattleFlow에서 처리한다.</summary>
public class BattleRewardUI : MonoBehaviour
{
    [SerializeField] private BattleFlow flow;
    [SerializeField] private CombatInfoUI information;
    [SerializeField] private Player player;
    [SerializeField] private GameObject panel;
    [SerializeField] private GameObject choicesRoot;
    [SerializeField] private GameObject deckRoot;
    [SerializeField] private TMP_Text title;
    [SerializeField] private TMP_Text instruction;
    [SerializeField] private Button[] choiceButtons;
    [SerializeField] private TMP_Text[] choiceLabels;
    [SerializeField] private Button[] deckButtons;
    [Tooltip("편성 카드의 상세 설명(효과·조건). 정해진 위치에서 아래로 늘어난다")]
    [SerializeField] private TMP_Text[] deckLabels;
    [Tooltip("편성 카드 영역. 모든 카드가 같은 위치·크기를 쓰므로 설명 길이와 관계없이 순번·이름·주요 정보가 정렬된다")]
    [SerializeField] private TMP_Text[] deckSlotLabels;
    [SerializeField] private TMP_Text[] deckNameLabels;
    [SerializeField] private TMP_Text[] deckInfoLabels;
    [Tooltip("보상 후보 스킬 아이콘. 패시브·빈 칸은 숨긴다")]
    [SerializeField] private Image[] choiceIcons;
    [Tooltip("교체 화면의 덱 카드 아이콘")]
    [SerializeField] private Image[] deckIcons;
    [SerializeField] private Button skipButton;
    [SerializeField] private Button backButton;
    [SerializeField] private string titleFormat = "전투 {0} 승리 · HP {1:0} / {2:0}";
    [SerializeField] private string chooseText = "보상 1개를 선택하세요. 필요 없으면 스킵할 수 있습니다.";
    [SerializeField] private string noChoicesText = "받을 수 있는 보상이 없습니다. 스킵 후 진행하세요.";
    [SerializeField] private string replaceFormat = "{0} 선택 · 교체할 카드 1장을 누르세요.";
    [SerializeField] private string readyText = "편성을 확인하고 다음 전투로 진행하세요.";
    [Tooltip("{0} 순번, {1} 시작 손패·대기열")]
    [SerializeField] private string deckSlotFormat = "{0}번 · {1}";
    [SerializeField] private string handLabel = "시작 손패";
    [SerializeField] private string queueLabel = "대기열";

    [Header("편성 순서")]
    [SerializeField] private Button reorderButton;
    [SerializeField] private Button saveOrderButton;
    [SerializeField] private Button cancelOrderButton;
    [SerializeField] private Image dragIcon;
    [SerializeField] private Image dropIndicator;
    [SerializeField] private string preparationTitle = "시작 편성";
    [SerializeField] private string preparationText = "편성을 확인하고 전투를 시작하세요.";
    [SerializeField] private string orderText = "카드를 원하는 자리에 드래그하세요. 1~4번은 시작 손패, 5~8번은 대기열입니다.";
    private int dragFrom = -1;

    private void Awake()
    {
        if (flow == null) flow = FindFirstObjectByType<BattleFlow>();
        if (information == null) information = FindFirstObjectByType<CombatInfoUI>();
        if (player == null) player = FindFirstObjectByType<Player>();
        for (int i = 0; i < choiceButtons.Length; i++)
        {
            int index = i;
            choiceButtons[i].onClick.AddListener(() => flow.SelectReward(index));
        }
        for (int i = 0; i < deckButtons.Length; i++)
        {
            int index = i;
            deckButtons[i].onClick.AddListener(() => flow.ReplaceDeckCard(index));
        }
        skipButton.onClick.AddListener(() => flow.SkipReward());
        backButton.onClick.AddListener(flow.CancelRewardSelection);
        if (reorderButton != null) reorderButton.onClick.AddListener(() => flow.BeginOrderEdit());
        if (saveOrderButton != null) saveOrderButton.onClick.AddListener(() => flow.SaveOrder());
        if (cancelOrderButton != null) cancelOrderButton.onClick.AddListener(flow.CancelOrderEdit);
    }

    /// <summary>후보 슬롯의 패시브 전용 영역. 스킬 후보일 때는 꺼진다.</summary>
    [System.Serializable]
    private class PassiveSlot
    {
        public GameObject root;
        public TMP_Text category;
        public TMP_Text title;
        public TMP_Text effect;
        public TMP_Text stacks;
        public TMP_Text guide;
    }

    [System.Serializable]
    private struct StatText
    {
        public RewardStat stat;
        [Tooltip("{0} 패시브 데이터의 수치. 핵심 수치만 <b>로 강조")]
        public string format;
    }

    [Header("패시브 후보")]
    [Tooltip("choiceButtons와 같은 순서. 분류·이름·효과는 위에서 고정 위치, 보유·안내는 아래 고정 영역")]
    [SerializeField] private PassiveSlot[] passiveSlots;
    [SerializeField] private string passiveCategoryText = "패시브";
    [SerializeField] private string passiveStackFormat = "보유 <b>{0}</b> / 최대 {1}";
    [SerializeField] private string unlimitedPassiveStackFormat = "보유 <b>{0}</b>";
    [SerializeField] private string passiveGuideText = "선택 즉시 적용 · 덱 유지";
    [Tooltip("패시브 설명(조건·주의)을 효과 줄 아래에 흐리게 표시")]
    [SerializeField] private string passiveNoteFormat = "<alpha=#CC>{0}<alpha=#FF>";
    [Tooltip("능력치별 효과 문구. 수치·단위는 패시브 데이터(능력치 종류·수치)에서 정해진다")]
    [SerializeField] private StatText[] passiveStatFormats =
    {
        new StatText { stat = RewardStat.PotionCapacity, format = "포션 최대 보유량 <b>+{0:0}</b>" },
        new StatText { stat = RewardStat.Potions, format = "현재 포션 <b>{0:0}개</b> 보충" },
        new StatText { stat = RewardStat.Healing, format = "즉시 HP <b>{0:0}</b> 회복" },
        new StatText { stat = RewardStat.MaximumHp, format = "최대 HP <b>+{0:0}</b>" },
        new StatText { stat = RewardStat.CostRegeneration, format = "초당 코스트 충전 <b>+{0:0.##}</b>" },
        new StatText { stat = RewardStat.StartingCost, format = "매 전투 시작 코스트 <b>+{0:0.#}</b>" },
        new StatText { stat = RewardStat.MaximumCost, format = "코스트 상한 <b>+{0:0.#}</b>" },
        new StatText { stat = RewardStat.ShieldGain, format = "방어도 부여량 <b>+{0:0%}</b>" },
        new StatText { stat = RewardStat.InterruptStagger, format = "차단 성공 경직 <b>+{0:0.##}초</b>" },
        new StatText { stat = RewardStat.CriticalChance, format = "크리티컬 확률 <b>+{0:0.#}%p</b>" },
    };

    public void Refresh()
    {
        bool preparing = flow != null && flow.State == BattleFlowState.Preparing;
        bool between = flow != null && flow.State == BattleFlowState.BetweenBattles;
        panel.SetActive(between || preparing);
        if ((!between && !preparing) || !flow.IsEditingOrder) EndOrderDrag();
        if (!between && !preparing) return;
        bool editing = flow.IsEditingOrder;
        bool replacing = between && flow.SelectedReward != null && !flow.RewardResolved;
        bool choosing = between && !replacing && !flow.RewardResolved;
        title.text = preparing ? preparationTitle : string.Format(titleFormat, flow.BattleNumber, player.CurrentHp, player.MaxHp);
        instruction.text = editing ? orderText : preparing ? preparationText : flow.RewardResolved ? readyText
            : replacing ? string.Format(replaceFormat, flow.SelectedReward.DisplayName)
            : flow.RewardChoiceCount == 0 ? noChoicesText : chooseText;
        choicesRoot.SetActive(choosing);
        deckRoot.SetActive(!choosing);
        skipButton.gameObject.SetActive(between && !flow.RewardResolved);
        if (reorderButton != null) reorderButton.gameObject.SetActive(flow.CanEditOrder && !editing);
        if (saveOrderButton != null) saveOrderButton.gameObject.SetActive(editing);
        if (cancelOrderButton != null) cancelOrderButton.gameObject.SetActive(editing);
        backButton.gameObject.SetActive(replacing);
        for (int i = 0; i < choiceButtons.Length; i++)
        {
            BattleRewardOption option = flow.GetRewardChoice(i);
            choiceButtons[i].gameObject.SetActive(option != null);
            choiceButtons[i].interactable = choosing;
            RunRewardEffect passive = option != null ? option.Effect : null;
            PassiveSlot slot = passiveSlots != null && i < passiveSlots.Length ? passiveSlots[i] : null;
            bool split = passive != null && slot != null && slot.root != null;
            // 스킬과 패시브가 같은 슬롯을 번갈아 쓰므로 쓰지 않는 쪽의 글자·아이콘을 비우고 끈다.
            choiceLabels[i].text = option == null || split ? string.Empty
                : passive == null ? option.Describe(information) : DescribePassive(passive);
            if (choiceLabels[i].gameObject.activeSelf == split) choiceLabels[i].gameObject.SetActive(!split);
            ShowPassive(slot, split ? passive : null);
            if (choiceIcons != null && i < choiceIcons.Length)
                CombatInfoUI.ShowIcon(choiceIcons[i], option != null ? option.Card : null);
        }
        for (int i = 0; i < deckButtons.Length; i++)
        {
            SkillData card = flow.GetOrderCard(i);
            deckButtons[i].gameObject.SetActive(card != null);
            deckButtons[i].interactable = replacing || editing;
            SetText(deckSlotLabels, i, card == null ? string.Empty
                : string.Format(deckSlotFormat, i + 1, i < DeckSystem.HandSize ? handLabel : queueLabel));
            SetText(deckNameLabels, i, card == null ? string.Empty : card.DisplayName);
            SetText(deckInfoLabels, i, card == null ? string.Empty : information.DescribeCardInfo(card));
            SetText(deckLabels, i, card == null ? string.Empty : information.DescribeCardEffect(card));
            if (deckIcons != null && i < deckIcons.Length) CombatInfoUI.ShowIcon(deckIcons[i], card);
        }
    }

    private static void SetText(TMP_Text[] labels, int index, string text)
    {
        if (labels != null && index < labels.Length && labels[index] != null) labels[index].text = text;
    }

    private static void SetLabel(TMP_Text label, string text)
    {
        if (label != null) label.text = text;
    }

    /// <summary>패시브 영역 표시. effect가 null이면 영역을 끄고 글자를 비운다.</summary>
    private void ShowPassive(PassiveSlot slot, RunRewardEffect effect)
    {
        if (slot == null || slot.root == null) return;
        bool show = effect != null;
        if (slot.root.activeSelf != show) slot.root.SetActive(show);
        SetLabel(slot.category, show ? passiveCategoryText : string.Empty);
        SetLabel(slot.title, show ? effect.DisplayName : string.Empty);
        SetLabel(slot.effect, show ? DescribePassiveEffect(effect) : string.Empty);
        SetLabel(slot.stacks, show ? DescribePassiveStacks(effect) : string.Empty);
        SetLabel(slot.guide, show ? passiveGuideText : string.Empty);
    }

    /// <summary>패시브 영역이 연결되지 않은 슬롯용 한 덩어리 설명.</summary>
    private string DescribePassive(RunRewardEffect effect) => string.Join("\n", passiveCategoryText, effect.DisplayName,
        DescribePassiveEffect(effect), DescribePassiveStacks(effect), passiveGuideText);

    private string DescribePassiveEffect(RunRewardEffect effect)
    {
        string text = effect.DescribeEffect(FormatStat, "\n");
        if (string.IsNullOrEmpty(effect.Description)) return text;
        return (text.Length > 0 ? text + "\n" : string.Empty) + string.Format(passiveNoteFormat, effect.Description);
    }

    private string DescribePassiveStacks(RunRewardEffect effect) => effect.MaxStacks > 0
        ? string.Format(passiveStackFormat, flow.GetPassiveStacks(effect), effect.MaxStacks)
        : string.Format(unlimitedPassiveStackFormat, flow.GetPassiveStacks(effect));

    private string FormatStat(RewardStat stat, float amount)
    {
        if (passiveStatFormats != null)
            foreach (StatText entry in passiveStatFormats)
                if (entry.stat == stat && !string.IsNullOrEmpty(entry.format)) return string.Format(entry.format, amount);
        Debug.LogWarning($"[{nameof(BattleRewardUI)}] 패시브 문구 없음: {stat}", this);
        return string.Format("{0} {1:0.##}", stat, amount);
    }

    public void BeginOrderDrag(int index, PointerEventData data)
    {
        if (data.button != PointerEventData.InputButton.Left || !flow.IsEditingOrder
            || !flow.CanEditOrder || index < 0 || index >= deckButtons.Length) return;
        dragFrom = index;
        if (dragIcon != null)
        {
            CombatInfoUI.ShowIcon(dragIcon, flow.GetOrderCard(index));
            dragIcon.transform.SetAsLastSibling();
        }
        UpdateOrderDrag(data);
    }

    public void UpdateOrderDrag(PointerEventData data)
    {
        if (dragFrom < 0) return;
        if (!flow.IsEditingOrder || !flow.CanEditOrder) { EndOrderDrag(); return; }
        Vector2 local;
        if (dragIcon != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(
            (RectTransform)dragIcon.transform.parent, data.position, data.pressEventCamera, out local))
            dragIcon.rectTransform.anchoredPosition = local;
        int target = DropIndex(data);
        if (dropIndicator == null) return;
        dropIndicator.gameObject.SetActive(target >= 0);
        if (target < 0) return;
        var slot = (RectTransform)deckButtons[target].transform;
        dropIndicator.rectTransform.position = slot.position;
        dropIndicator.rectTransform.sizeDelta = slot.rect.size;
    }

    public void FinishOrderDrag(PointerEventData data)
    {
        int from = dragFrom;
        int to = DropIndex(data);
        EndOrderDrag();
        if (from >= 0 && to >= 0) flow.MoveOrderCard(from, to);
    }

    private int DropIndex(PointerEventData data)
    {
        for (int i = 0; i < deckButtons.Length; i++)
            if (deckButtons[i].gameObject.activeInHierarchy && RectTransformUtility.RectangleContainsScreenPoint(
                (RectTransform)deckButtons[i].transform, data.position, data.pressEventCamera)) return i;
        return -1;
    }

    private void EndOrderDrag()
    {
        dragFrom = -1;
        if (dragIcon != null) dragIcon.gameObject.SetActive(false);
        if (dropIndicator != null) dropIndicator.gameObject.SetActive(false);
    }

    private void OnDisable() => EndOrderDrag();
}
