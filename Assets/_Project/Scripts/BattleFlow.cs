using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum BattleFlowState { Fighting, BetweenBattles, Victory, Defeat }

/// <summary>한 씬의 적을 재사용하는 시연 전투 진행. 보상/덱 편집은 아직 연결하지 않는다.</summary>
[DefaultExecutionOrder(-50)]
public class BattleFlow : MonoBehaviour
{
    [Serializable]
    public class Encounter
    {
        public string label;
        public Enemy[] enemies;
        [Tooltip("3체 구성에서 가장 강한 적을 지정한다. 이 적을 중앙에 배치한다.")]
        public Enemy centerEnemy;
    }

    [SerializeField] private Encounter[] encounters;
    [SerializeField] private Player player;
    [SerializeField] private DeckSystem deck;
    [SerializeField] private CostSystem cost;
    [SerializeField] private EnemyManager enemies;
    [SerializeField] private CombatMetrics metrics;
    [SerializeField] private CombatInfoUI information;

    [Header("진행 화면")]
    [SerializeField] private TMP_Text progressText;
    [SerializeField] private GameObject resultPanel;
    [SerializeField] private TMP_Text resultText;
    [SerializeField] private Button nextButton;
    [SerializeField] private Button restartButton;
    [SerializeField] private string progressFormat = "전투 {0} / {1}  ·  {2}";
    [SerializeField] private string victoryFormat = "전투 {0} 승리\n남은 HP {1:0} / {2:0}\n이 HP로 다음 전투를 시작합니다.";
    [SerializeField] private string completeFormat = "3전투 완료\n남은 HP {0:0} / {1:0}";
    [SerializeField] private string defeatFormat = "전투 {0} 패배\n처음부터 다시 도전할 수 있습니다.";

    public int BattleNumber { get; private set; }
    public BattleFlowState State { get; private set; }
    public int BattleCount => encounters != null ? encounters.Length : 0;

    private void Awake()
    {
        if (player == null) player = FindFirstObjectByType<Player>();
        if (deck == null) deck = FindFirstObjectByType<DeckSystem>();
        if (cost == null) cost = FindFirstObjectByType<CostSystem>();
        if (enemies == null) enemies = FindFirstObjectByType<EnemyManager>();
        if (metrics == null) metrics = FindFirstObjectByType<CombatMetrics>();
        if (information == null) information = FindFirstObjectByType<CombatInfoUI>();
        if (nextButton != null) nextButton.onClick.AddListener(NextBattle);
        if (restartButton != null) restartButton.onClick.AddListener(RestartRun);
    }

    private void Start() => RestartRun();

    public void RestartRun()
    {
        if (!IsConfigured()) return;
        BeginBattle(0, true);
    }

    public void NextBattle()
    {
        if (State != BattleFlowState.BetweenBattles || BattleNumber >= BattleCount) return;
        BeginBattle(BattleNumber, false);
    }

    private bool IsConfigured()
    {
        if (player == null || deck == null || cost == null || enemies == null || metrics == null
            || BattleCount == 0) return ConfigurationError();
        foreach (Encounter encounter in encounters)
        {
            if (encounter == null || encounter.enemies == null || encounter.enemies.Length == 0 || encounter.enemies.Length > 3)
                return ConfigurationError();
            if (encounter.enemies.Length == 3
                && (encounter.centerEnemy == null || Array.IndexOf(encounter.enemies, encounter.centerEnemy) < 0))
                return ConfigurationError();
            for (int i = 0; i < encounter.enemies.Length; i++)
            {
                if (encounter.enemies[i] == null || encounter.enemies[i].Data == null)
                    return ConfigurationError();
                for (int j = 0; j < i; j++)
                    if (encounter.enemies[i] == encounter.enemies[j]) return ConfigurationError();
            }
        }
        return true;
    }

    private bool ConfigurationError()
    {
        Time.timeScale = 0f;
        Debug.LogError("BattleFlow: 전투 참조와 적 구성을 확인하세요.", this);
        return false;
    }

    private void BeginBattle(int index, bool restoreHp)
    {
        Time.timeScale = 0f;
        // 이전 효과의 종료 처리를 먼저 끝내고, 그 다음에 새 전투 계측을 연다.
        deck.ResetStartingDeck();
        player.BeginBattle(restoreHp);
        cost.BeginBattle();
        if (information != null) information.BeginBattle();
        BattleNumber = index + 1;
        metrics.BeginBattle(BattleNumber);
        enemies.BeginBattle(encounters[index].enemies, encounters[index].centerEnemy);
        State = BattleFlowState.Fighting;
        RefreshUI();
        Time.timeScale = 1f;
    }

    private void Update()
    {
        if (BattleNumber == 0 || State != BattleFlowState.Fighting) return;
        if (!metrics.Ended && !enemies.CombatEnded && player.IsAlive) return;
        bool won = player.IsAlive && enemies.CombatEnded;
        State = !won ? BattleFlowState.Defeat
            : BattleNumber == BattleCount ? BattleFlowState.Victory : BattleFlowState.BetweenBattles;
        Time.timeScale = 0f;
        deck.EndBattle();
        player.EndBattle();
        if (information != null) information.BeginBattle();
        RefreshUI();
    }

    private void RefreshUI()
    {
        if (progressText != null)
            progressText.text = string.Format(progressFormat, BattleNumber, BattleCount, encounters[BattleNumber - 1].label);
        bool ended = State != BattleFlowState.Fighting;
        if (resultPanel != null) resultPanel.SetActive(ended);
        if (nextButton != null) nextButton.gameObject.SetActive(State == BattleFlowState.BetweenBattles);
        if (restartButton != null) restartButton.gameObject.SetActive(State == BattleFlowState.Victory || State == BattleFlowState.Defeat);
        if (resultText == null || !ended) return;
        resultText.text = State == BattleFlowState.Defeat ? string.Format(defeatFormat, BattleNumber)
            : State == BattleFlowState.Victory ? string.Format(completeFormat, player.CurrentHp, player.MaxHp)
            : string.Format(victoryFormat, BattleNumber, player.CurrentHp, player.MaxHp);
    }

    private void OnDestroy()
    {
        if (nextButton != null) nextButton.onClick.RemoveListener(NextBattle);
        if (restartButton != null) restartButton.onClick.RemoveListener(RestartRun);
    }
}
