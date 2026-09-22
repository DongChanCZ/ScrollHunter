using TMPro;
using UnityEngine;
using UnityEngine.UI;

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
    }

    public void Refresh()
    {
        bool between = flow != null && flow.State == BattleFlowState.BetweenBattles;
        panel.SetActive(between);
        if (!between) return;
        bool replacing = flow.SelectedReward != null && !flow.RewardResolved;
        bool choosing = !replacing && !flow.RewardResolved;
        title.text = string.Format(titleFormat, flow.BattleNumber, player.CurrentHp, player.MaxHp);
        instruction.text = flow.RewardResolved ? readyText
            : replacing ? string.Format(replaceFormat, flow.SelectedReward.DisplayName)
            : flow.RewardChoiceCount == 0 ? noChoicesText : chooseText;
        choicesRoot.SetActive(choosing);
        deckRoot.SetActive(!choosing);
        skipButton.gameObject.SetActive(!flow.RewardResolved);
        backButton.gameObject.SetActive(replacing);
        for (int i = 0; i < choiceButtons.Length; i++)
        {
            BattleRewardOption option = flow.GetRewardChoice(i);
            choiceButtons[i].gameObject.SetActive(option != null);
            choiceButtons[i].interactable = choosing;
            choiceLabels[i].text = option != null ? option.Describe(information) : string.Empty;
        }
        for (int i = 0; i < deckButtons.Length; i++)
        {
            SkillData card = flow.GetDeckCard(i);
            deckButtons[i].gameObject.SetActive(card != null);
            deckButtons[i].interactable = replacing;
            deckLabels[i].text = card == null ? string.Empty : string.Format(deckCardFormat, i + 1,
                i < DeckSystem.HandSize ? handLabel : queueLabel, information.DescribeCard(card));
        }
    }
}
