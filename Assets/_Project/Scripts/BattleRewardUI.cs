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
    [SerializeField] private TMP_Text[] deckLabels;
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
    [SerializeField] private string deckCardFormat = "{0}번 · {1}\n{2}";
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

    [SerializeField] private string passiveChoiceFormat = "패시브 · {0}\n보유 {1}/{2}\n선택 즉시 적용 · 덱 유지";
    [SerializeField] private string unlimitedPassiveChoiceFormat = "패시브 · {0}\n보유 {1}\n선택 즉시 적용 · 덱 유지";

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
            choiceLabels[i].text = option == null ? string.Empty : option.Effect == null ? option.Describe(information)
                : string.Format(option.Effect.MaxStacks > 0 ? passiveChoiceFormat : unlimitedPassiveChoiceFormat,
                    option.Describe(information), flow.GetPassiveStacks(option.Effect), option.Effect.MaxStacks);
            if (choiceIcons != null && i < choiceIcons.Length)
                CombatInfoUI.ShowIcon(choiceIcons[i], option != null ? option.Card : null);
        }
        for (int i = 0; i < deckButtons.Length; i++)
        {
            SkillData card = flow.GetOrderCard(i);
            deckButtons[i].gameObject.SetActive(card != null);
            deckButtons[i].interactable = replacing || editing;
            deckLabels[i].text = card == null ? string.Empty : string.Format(deckCardFormat, i + 1,
                i < DeckSystem.HandSize ? handLabel : queueLabel, information.DescribeCard(card));
            if (deckIcons != null && i < deckIcons.Length) CombatInfoUI.ShowIcon(deckIcons[i], card);
        }
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
