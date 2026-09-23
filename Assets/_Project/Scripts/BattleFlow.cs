using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum BattleFlowState { Fighting, BetweenBattles, Victory, Defeat, Title }

/// <summary>한 씬의 적을 재사용하는 시연 전투 진행. 승리 후 보상·덱 교체를 거쳐 다음 전투에 편성을 이월한다.</summary>
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
    [SerializeField] private DamageNumberUI damageNumbers;

    [Header("시작 화면")]
    [SerializeField] private GameObject startPanel;
    [SerializeField] private Button startButton;

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

    [Header("시연 보상")]
    [SerializeField] private BattleRewardOption[] rewardPool;
    [SerializeField] private BattleRewardUI rewardUI;
    [SerializeField, Min(0)] private int cardChoiceLimit = 2;
    [SerializeField, Min(0)] private int passiveChoiceLimit = 1;
    [SerializeField] private TMP_Text passiveText;
    [SerializeField] private string passiveStatusFormat = "충전 {0:0.0}/초 · 크리티컬 {1:0.#}%\n{2}";
    [SerializeField] private string noPassivesText = "패시브 없음";
    [SerializeField] private string passiveStackFormat = "{0} {1}/{2}";
    [SerializeField] private string passiveMaxStackFormat = "{0} {1}/{2} MAX";
    [SerializeField] private string unlimitedStackFormat = "{0} {1}";
    private readonly Dictionary<RunRewardEffect, int> effectStacks = new Dictionary<RunRewardEffect, int>();
    // 보상 추첨은 전투의 크리티컬 난수열을 소모하지 않는다.
    private readonly System.Random rewardRandom = new System.Random();
    [SerializeField] private string replaceLogFormat = "[보상 전투 {0}] 덱 {1}번: {2} → {3}";
    [SerializeField] private string skipLogFormat = "[보상 전투 {0}] 스킵 — 편성 유지";
    [SerializeField] private string resourceStatusFormat = "HP {0:0}/{1:0} · 포션 {2}/{3} · 시작 코스트 {4:0.#} / 상한 {5:0.#} · 방어도 배율 {6:0.##} · 차단 경직 {7:0.##}초";
    private string ResourceStatus => string.Format(resourceStatusFormat, player.CurrentHp, player.MaxHp,
        player.PotionsRemaining, player.PotionCapacity, cost.StartingCost, cost.Max, player.ShieldGainMultiplier, deck.InterruptStagger);
    private List<SkillData> runDeck = new List<SkillData>();
    private readonly List<BattleRewardOption> rewardChoices = new List<BattleRewardOption>();
    private readonly List<RunRewardEffect> acquiredEffects = new List<RunRewardEffect>();
    [SerializeField] private string effectLogFormat = "[보상 전투 {0}] {1} 획득 / {2}스택 / 충전 {3:0.0}/초 / 크리티컬 {4:0.#}%";
    public SkillData SelectedReward { get; private set; }
    public bool RewardResolved { get; private set; }
    public int RewardChoiceCount => rewardChoices.Count;
    public int DeckCount => runDeck.Count;
    public SkillData GetDeckCard(int index) => index >= 0 && index < runDeck.Count ? runDeck[index] : null;
    public BattleRewardOption GetRewardChoice(int index) => index >= 0 && index < rewardChoices.Count ? rewardChoices[index] : null;

    public int BattleNumber { get; private set; }
    public BattleFlowState State { get; private set; } = BattleFlowState.Title;
    public int BattleCount => encounters != null ? encounters.Length : 0;

    public int GetPassiveStacks(RunRewardEffect source)
    {
        int count;
        return source != null && effectStacks.TryGetValue(source, out count) ? count : 0;
    }

    public string PassiveSummary
    {
        get
        {
            var entries = new List<string>();
            foreach (var entry in effectStacks)
                entries.Add(string.Format(entry.Key.MaxStacks == 0 ? unlimitedStackFormat
                    : entry.Value >= entry.Key.MaxStacks ? passiveMaxStackFormat : passiveStackFormat,
                    entry.Key.DisplayName, entry.Value, entry.Key.MaxStacks));
            return entries.Count == 0 ? noPassivesText : string.Join(" · ", entries);
        }
    }

    private bool CanOffer(BattleRewardOption option)
    {
        return option != null && option.CanOffer(runDeck, player, cost)
            && (option.Effect == null || option.Effect.MaxStacks == 0
                || GetPassiveStacks(option.Effect) < option.Effect.MaxStacks);
    }

    private void Awake()
    {
        if (player == null) player = FindFirstObjectByType<Player>();
        if (deck == null) deck = FindFirstObjectByType<DeckSystem>();
        if (cost == null) cost = FindFirstObjectByType<CostSystem>();
        if (enemies == null) enemies = FindFirstObjectByType<EnemyManager>();
        if (metrics == null) metrics = FindFirstObjectByType<CombatMetrics>();
        if (information == null) information = FindFirstObjectByType<CombatInfoUI>();
        if (damageNumbers == null) damageNumbers = FindFirstObjectByType<DamageNumberUI>();
        if (startButton != null) startButton.onClick.AddListener(StartRun);
        if (nextButton != null) nextButton.onClick.AddListener(NextBattle);
        if (restartButton != null) restartButton.onClick.AddListener(RestartRun);
    }

    private void Start()
    {
        if (!IsConfigured()) return;
        // 모든 Awake 이후 대기 상태로 전환한다. 전투 계측은 시작 버튼에서 연다.
        metrics.WaitForBattle();
        enemies.WaitForBattle();
        Time.timeScale = 0f;
        RefreshUI();
    }

    public void StartRun()
    {
        if (State == BattleFlowState.Title) RestartRun();
    }

    public void RestartRun()
    {
        if (!IsConfigured()) return;
        ClearRunRewards();
        runDeck = deck.CopyStartingDeck();
        BeginBattle(0, true);
    }

    public void NextBattle()
    {
        if (State != BattleFlowState.BetweenBattles || BattleNumber >= BattleCount || !RewardResolved) return;
        BeginBattle(BattleNumber, false);
    }

    public bool SelectReward(int index)
    {
        BattleRewardOption option = GetRewardChoice(index);
        if (State != BattleFlowState.BetweenBattles || RewardResolved || option == null
            || !CanOffer(option)) return false;
        if (option.Card == null)
        {
            RunRewardEffect instance = Instantiate(option.Effect);
            try { instance.Apply(player, cost); }
            catch (Exception error) { ReleaseEffect(instance); Debug.LogException(error, this); return false; }
            acquiredEffects.Add(instance);
            effectStacks[option.Effect] = GetPassiveStacks(option.Effect) + 1;
            SelectedReward = null;
            RewardResolved = true;
            Debug.Log(string.Format(effectLogFormat, BattleNumber, option.DisplayName, GetPassiveStacks(option.Effect), cost.RegenerationPerSecond, player.CriticalChance) + " / " + ResourceStatus, this);
        }
        else SelectedReward = option.Card;
        RefreshUI();
        return true;
    }

    public void CancelRewardSelection()
    {
        if (State != BattleFlowState.BetweenBattles || RewardResolved) return;
        SelectedReward = null;
        RefreshUI();
    }

    public bool ReplaceDeckCard(int index)
    {
        if (State != BattleFlowState.BetweenBattles || RewardResolved || SelectedReward == null
            || index < 0 || index >= runDeck.Count || runDeck.Contains(SelectedReward)) return false;
        SkillData removed = runDeck[index];
        runDeck[index] = SelectedReward;
        Debug.Log(string.Format(replaceLogFormat, BattleNumber, index + 1, removed.DisplayName, SelectedReward.DisplayName), this);
        SelectedReward = null;
        RewardResolved = true;
        RefreshUI();
        return true;
    }

    public bool SkipReward()
    {
        if (State != BattleFlowState.BetweenBattles || RewardResolved) return false;
        SelectedReward = null;
        RewardResolved = true;
        Debug.Log(string.Format(skipLogFormat, BattleNumber), this);
        RefreshUI();
        return true;
    }

    private void PrepareRewards()
    {
        rewardChoices.Clear();
        if (rewardPool == null) return;
        var cards = new List<BattleRewardOption>();
        var passives = new List<BattleRewardOption>();
        var rare = new List<BattleRewardOption>();
        foreach (BattleRewardOption option in rewardPool)
        {
            if (!CanOffer(option)) continue;
            var candidates = option.Card != null ? cards : option.Effect.IsRareReward ? rare : passives;
            if (!candidates.Exists(other => other.Card == option.Card && other.Effect == option.Effect))
                candidates.Add(option);
        }
        TakeChoices(cards, cardChoiceLimit);
        for (int i = 0; i < passiveChoiceLimit; i++)
        {
            BattleRewardOption special = rare.Count > 0 ? rare[rewardRandom.Next(rare.Count)] : null;
            if (special != null && rewardRandom.NextDouble() < special.Effect.OfferChance)
            {
                rewardChoices.Add(special);
                rare.Remove(special);
            }
            else TakeChoices(passives, 1);
        }
    }

    private void TakeChoices(List<BattleRewardOption> candidates, int limit)
    {
        for (int i = 0; i < limit && candidates.Count > 0; i++)
        {
            int index = rewardRandom.Next(candidates.Count);
            rewardChoices.Add(candidates[index]);
            candidates.RemoveAt(index);
        }
    }

    private void ClearRunRewards()
    {
        for (int i = acquiredEffects.Count - 1; i >= 0; i--) ReleaseEffect(acquiredEffects[i]);
        acquiredEffects.Clear();
        effectStacks.Clear();
    }

    private void ReleaseEffect(RunRewardEffect effect)
    {
        if (effect == null) return;
        try { effect.Remove(); }
        catch (Exception error) { Debug.LogException(error, this); }
        finally { if (Application.isPlaying) Destroy(effect); else DestroyImmediate(effect); }
    }

    private bool IsConfigured()
    {
        if (player == null || deck == null || cost == null || enemies == null || metrics == null
            || BattleCount == 0) return ConfigurationError();
        List<SkillData> initial = deck.CopyStartingDeck();
        if (initial.Count != DeckSystem.DeckSize || initial.Contains(null) || new HashSet<SkillData>(initial).Count != initial.Count)
            return ConfigurationError();
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
        // Update의 종료 처리 전에 재시작해도 마지막 결과와 요약을 먼저 마감한다.
        if (metrics.Ended) deck.EndBattle();
        metrics.FlushPendingSummary();
        // 이전 효과의 종료 처리를 먼저 끝내고, 그 다음에 새 전투 계측을 연다.
        if (damageNumbers != null) damageNumbers.Clear();
        deck.SetDeck(runDeck);
        SelectedReward = null;
        RewardResolved = false;
        rewardChoices.Clear();
        player.BeginBattle(restoreHp);
        cost.BeginBattle();
        if (information != null) information.BeginBattle();
        BattleNumber = index + 1;
        metrics.BeginBattle(BattleNumber);
        metrics.RecordRunModifiers(cost.RegenerationPerSecond, player.CriticalChance, PassiveSummary, ResourceStatus);
        enemies.BeginBattle(encounters[index].enemies, encounters[index].centerEnemy);
        State = BattleFlowState.Fighting;
        RefreshUI();
        Time.timeScale = information != null ? information.BattleSpeed : 1f;
        metrics.RecordBattleSpeed(Time.timeScale);
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
        if (State == BattleFlowState.BetweenBattles) PrepareRewards();
        RefreshUI();
        metrics.FlushPendingSummary();
    }

    private void RefreshUI()
    {
        bool title = State == BattleFlowState.Title;
        if (startPanel != null) startPanel.SetActive(title);
        if (progressText != null)
        {
            progressText.gameObject.SetActive(!title);
            if (BattleNumber > 0)
                progressText.text = string.Format(progressFormat, BattleNumber, BattleCount, encounters[BattleNumber - 1].label);
        }
        if (passiveText != null)
        {
            passiveText.gameObject.SetActive(!title);
            passiveText.text = string.Format(passiveStatusFormat, cost.RegenerationPerSecond, player.CriticalChance, PassiveSummary);
        }
        bool ended = State == BattleFlowState.Victory || State == BattleFlowState.Defeat;
        if (resultPanel != null) resultPanel.SetActive(ended);
        if (nextButton != null) nextButton.gameObject.SetActive(State == BattleFlowState.BetweenBattles && RewardResolved);
        if (restartButton != null) restartButton.gameObject.SetActive(State == BattleFlowState.Victory || State == BattleFlowState.Defeat);
        if (rewardUI != null) rewardUI.Refresh();
        if (resultText == null || !ended) return;
        resultText.text = State == BattleFlowState.Defeat ? string.Format(defeatFormat, BattleNumber)
            : State == BattleFlowState.Victory ? string.Format(completeFormat, player.CurrentHp, player.MaxHp)
            : string.Format(victoryFormat, BattleNumber, player.CurrentHp, player.MaxHp);
    }

    private void OnDisable()
    {
        // 승패 확정 직후 Play를 멈춘 경우에도 대기 중 요약을 잃지 않는다.
        if (metrics == null || !metrics.Ended) return;
        if (deck != null) deck.EndBattle();
        metrics.FlushPendingSummary();
    }

    private void OnDestroy()
    {
        ClearRunRewards();
        if (startButton != null) startButton.onClick.RemoveListener(StartRun);
        if (nextButton != null) nextButton.onClick.RemoveListener(NextBattle);
        if (restartButton != null) restartButton.onClick.RemoveListener(RestartRun);
    }
}
