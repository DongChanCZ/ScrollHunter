using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum BattleFlowState { Fighting, BetweenBattles, Victory, Defeat, Title, Preparing, TutorialComplete, CountingDown, Inheriting }

/// <summary>한 씬의 적을 재사용하는 시연 전투 진행. 승리 후 보상·덱 교체를 거쳐 다음 전투에 편성을 이월한다.</summary>
[DefaultExecutionOrder(-50)]
public class BattleFlow : MonoBehaviour
{
    [Serializable]
    public class Encounter
    {
        [Tooltip("상단에 표시할 맵 이름")]
        public string label;
        public Enemy[] enemies;
        [Tooltip("3체 구성에서 가장 강한 적을 지정한다. 이 적을 중앙에 배치한다.")]
        public Enemy centerEnemy;
    }

    [SerializeField] private Encounter[] encounters;
    [SerializeField] private TutorialFlow tutorial;
    private bool HasTutorial => tutorial != null && tutorial.isActiveAndEnabled;
    [SerializeField] private Player player;
    [SerializeField] private DeckSystem deck;
    [SerializeField] private CostSystem cost;
    [SerializeField] private EnemyManager enemies;
    [SerializeField] private CombatMetrics metrics;
    [SerializeField] private CombatInfoUI information;
    [SerializeField] private DamageNumberUI damageNumbers;
    [SerializeField] private SkillVfx skillVfx;

    [Header("전투 종료 연출")]
    [Tooltip("막타 스킬 연출의 기본 대기 시간(실제 초). 사망 모션이 길면 끝까지 기다린다. 게임은 멈춘 채 연출만 재생. 0이면 즉시 전환")]
    [SerializeField] private float finishingEffectHold = 0.6f;
    [Tooltip("패배 화면 최소 대기(실제 초). 남은 적 발동 연출이 있으면 끝까지 기다린다. 0이면 검사 시 즉시 전환")]
    [SerializeField, Min(0f)] private float defeatEffectHold = 0.6f;
    [SerializeField, Min(0f)] private float defeatEffectTail = 0.15f;
    private BossAttackVfx[] endingBossEffects;
    private EnemyAttackVfx[] endingEnemyEffects;
    private ArrowFlight[] endingArrows;

    [Header("전투 준비")]
    [SerializeField, Min(0)] private float countdownSeconds = 3f;
    [SerializeField, TextArea] private string countdownFormat = "전투 준비\n<size=140%>{0}</size>";
    private float countdownRemaining;
    private GameObject countdownPanel;
    private TMP_Text countdownText;
    public bool IsCountingDown => State == BattleFlowState.CountingDown;

    [Header("시작 화면")]
    [SerializeField] private GameObject startPanel;
    [SerializeField] private Button startButton;

    [Header("진행 화면")]
    [SerializeField] private TMP_Text progressText;
    [SerializeField] private GameObject resultPanel;
    [SerializeField] private TMP_Text resultText;
    [SerializeField] private Button nextButton;
    [SerializeField] private Button restartButton;
    [SerializeField] private string tutorialMapName = "오솔길 입구";
    [SerializeField] private string victoryFormat = "전투 {0} 승리\n남은 HP {1:0} / {2:0}\n이 HP로 다음 전투를 시작합니다.";
    [SerializeField] private string completeFormat = "모험 완료\n남은 HP {0:0} / {1:0}";
    [SerializeField] private string defeatFormat = "전투 {0} 패배\n처음부터 다시 도전할 수 있습니다.";

    [Header("계승")]
    [SerializeField] private string inheritanceFileName = "save.json";
    [SerializeField] private string inheritanceButtonText = "계승 선택";
    [SerializeField] private string restartButtonText = "처음부터 다시 시작";
    [SerializeField] private string inheritanceLogFormat = "[계승] {0} 저장 / 시작 덱 슬롯 {1}";
    private InheritanceSave inheritance;
    private readonly List<BattleRewardOption> earnedRewards = new List<BattleRewardOption>();
    public InheritanceSave Inheritance => inheritance;
    public bool InheritanceSaveFailed { get; private set; }

    [Header("시연 보상")]
    [SerializeField] private BattleRewardOption[] rewardPool;
    [SerializeField] private BattleRewardUI rewardUI;
    [SerializeField, Min(0)] private int cardChoiceLimit = 2;
    [SerializeField, Min(0)] private int passiveChoiceLimit = 1;
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
    [Header("편성 순서")]
    [SerializeField] private TMP_Text nextButtonLabel;
    [SerializeField] private string firstBattleText = "전투 시작";
    [SerializeField] private string nextBattleText = "다음 전투";
    [SerializeField] private string orderLogFormat = "[편성 전투 {0} 준비] 순서 저장: {1}";
    private List<SkillData> orderDraft;
    public bool IsEditingOrder => orderDraft != null;
    public bool CanEditOrder => !PanelHeld && (!HasTutorial || (!tutorial.ShowingGuide && !tutorial.BlocksInputThisFrame)) && (State == BattleFlowState.Preparing
        || (State == BattleFlowState.BetweenBattles && RewardResolved));
    public SkillData GetOrderCard(int index) => IsEditingOrder && index >= 0 && index < orderDraft.Count
        ? orderDraft[index] : GetDeckCard(index);

    private List<SkillData> runDeck = new List<SkillData>();
    private readonly List<BattleRewardOption> rewardChoices = new List<BattleRewardOption>();
    private readonly List<RunRewardEffect> acquiredEffects = new List<RunRewardEffect>();
    [SerializeField] private string effectLogFormat = "[보상 전투 {0}] {1} 획득 / {2}스택 / 충전 {3:0.0}/초 / 크리티컬 {4:0.#}%";
    public SkillData SelectedReward { get; private set; }
    public int SelectedReplacementIndex { get; private set; } = -1;
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

    /// <summary>획득한 패시브와 중첩(획득 순서). 능력치 창 표시용.</summary>
    public IEnumerable<KeyValuePair<RunRewardEffect, int>> PassiveStacks => effectStacks;

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
        if (tutorial == null) tutorial = FindFirstObjectByType<TutorialFlow>();
        if (player == null) player = FindFirstObjectByType<Player>();
        if (deck == null) deck = FindFirstObjectByType<DeckSystem>();
        if (cost == null) cost = FindFirstObjectByType<CostSystem>();
        if (enemies == null) enemies = FindFirstObjectByType<EnemyManager>();
        if (metrics == null) metrics = FindFirstObjectByType<CombatMetrics>();
        if (information == null) information = FindFirstObjectByType<CombatInfoUI>();
        if (damageNumbers == null) damageNumbers = FindFirstObjectByType<DamageNumberUI>();
        if (skillVfx == null) skillVfx = FindFirstObjectByType<SkillVfx>();
        if (startButton != null) startButton.onClick.AddListener(StartRun);
        if (nextButton != null) nextButton.onClick.AddListener(NextBattle);
        if (restartButton != null) restartButton.onClick.AddListener(ContinueAfterResult);
        inheritance = new InheritanceSave(System.IO.Path.Combine(Application.persistentDataPath, inheritanceFileName),
            rewardPool, deck.CopyStartingDeck());
        BuildCountdownUI();
        if (Camera.main != null && Camera.main.GetComponent<RedHitShake>() == null)
            Camera.main.gameObject.AddComponent<RedHitShake>();
    }

    private void Start()
    {
        if (!IsConfigured()) return;
#if UNITY_EDITOR
        if (TryBeginMagePreview()) return;
#endif
        // 모든 Awake 이후 대기 상태로 전환한다. 전투 계측은 시작 버튼에서 연다.
        metrics.WaitForBattle();
        enemies.WaitForBattle();
        Time.timeScale = 0f;
        RefreshUI();
    }

#if UNITY_EDITOR
    // 임시 모델 확인용. 씬·빌드에는 저장하지 않고 Unity를 종료하면 해제된다.
    private const string MagePreviewMenu = "Tools/Scroll Hunter/Mage Model Preview on Play";

    [UnityEditor.MenuItem(MagePreviewMenu)]
    private static void ToggleMagePreview()
    {
        bool enabled = !UnityEditor.SessionState.GetBool(MagePreviewMenu, false);
        UnityEditor.SessionState.SetBool(MagePreviewMenu, enabled);
        Debug.Log("[마법사 모델 테스트] Play 직행 " + (enabled ? "켜짐" : "꺼짐"));
    }

    [UnityEditor.MenuItem(MagePreviewMenu, true)]
    private static bool ValidateMagePreview()
    {
        UnityEditor.Menu.SetChecked(MagePreviewMenu, UnityEditor.SessionState.GetBool(MagePreviewMenu, false));
        return !UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode;
    }

    private bool TryBeginMagePreview()
    {
        if (!UnityEditor.SessionState.GetBool(MagePreviewMenu, false)) return false;
        if (tutorial != null) tutorial.enabled = false;
        ClearRunRewards();
        runDeck = deck.CopyStartingDeck();
        BeginBattle(BattleCount - 1, true);
        Debug.Log("[마법사 모델 테스트] 기본 덱·HP·포션으로 마법사 전투 시작", this);
        return true;
    }
#endif

    public void StartRun()
    {
        if (State != BattleFlowState.Title || !IsConfigured()) return;
        ClearRunRewards();
        runDeck = deck.CopyStartingDeck();
        if (HasTutorial) { BeginTutorial(false); return; }
        ApplyInheritance();
        State = BattleFlowState.Preparing;
        Time.timeScale = 0f;
        RefreshUI();
    }

    public void RestartRun()
    {
        if (!IsConfigured()) return;
#if UNITY_EDITOR
        if (TryBeginMagePreview()) return;
#endif
        if (HasTutorial && tutorial.Active) { BeginTutorial(true); return; }
        ClearRunRewards();
        runDeck = deck.CopyStartingDeck();
        if (HasTutorial) BeginTutorial(false);
        else { ApplyInheritance(); BeginBattle(0, true); }
    }

    public void ContinueAfterResult()
    {
        if (PanelHeld) return;
        if (State == BattleFlowState.Victory) { RestartRun(); return; }
        if (State != BattleFlowState.Defeat) return;
        rewardChoices.Clear();
        foreach (var option in earnedRewards)
            if (inheritance.CanInherit(option) && !rewardChoices.Contains(option)) rewardChoices.Add(option);
        if (rewardChoices.Count == 0) { ReturnToTitle(); return; }
        // 후보를 보존한 뒤 이번 런 효과를 정리한다. 카드 설명도 다음 런의 계승 수치로 표시한다.
        ClearRunRewards();
        ApplyInheritance();
        SelectedReplacementIndex = -1;
        SelectedReward = null;
        RewardResolved = false;
        InheritanceSaveFailed = false;
        State = BattleFlowState.Inheriting;
        RefreshUI();
    }

    private void ApplyInheritance()
    {
        runDeck = inheritance.BuildDeck();
        // 튜토리얼은 기본 덱·기본 능력치. 종료 뒤 본편 준비에서 한 번만 적용한다.
        player.BeginBattle(true);
        foreach (var entry in inheritance.Passives)
        {
            RunRewardEffect source = inheritance.Find(entry.id).Effect;
            for (int i = 0; i < entry.stacks; i++) ApplyPassive(source);
        }
    }

    private bool ApplyPassive(RunRewardEffect source)
    {
        RunRewardEffect instance = Instantiate(source);
        try { instance.Apply(player, cost); }
        catch (Exception error) { ReleaseEffect(instance); Debug.LogException(error, this); return false; }
        acquiredEffects.Add(instance);
        effectStacks[source] = GetPassiveStacks(source) + 1;
        return true;
    }

    private bool CommitInheritance(BattleRewardOption option, int slot)
    {
        if (!inheritance.TryInherit(option, slot))
        {
            InheritanceSaveFailed = true;
            RefreshUI();
            return false;
        }
        Debug.Log(string.Format(inheritanceLogFormat, option.DisplayName, slot + 1), this);
        ReturnToTitle();
        return true;
    }

    public bool ResetInheritance()
    {
        if (State != BattleFlowState.Title) return false;
        InheritanceSaveFailed = !inheritance.TryReset();
        RefreshUI();
        return !InheritanceSaveFailed;
    }

    private void ReturnToTitle()
    {
        ClearRunRewards();
        SelectedReplacementIndex = -1;
        SelectedReward = null;
        rewardChoices.Clear();
        orderDraft = null;
        InheritanceSaveFailed = false;
        enemies.WaitForBattle();
        metrics.WaitForBattle();
        if (information != null) information.BeginBattle();
        BattleNumber = 0;
        State = BattleFlowState.Title;
        Time.timeScale = 0f;
        RefreshUI();
    }

    public void NextBattle()
    {
        if (IsEditingOrder || PanelHeld || (HasTutorial && (tutorial.ShowingGuide || tutorial.BlocksInputThisFrame))) return;
        if (State == BattleFlowState.Preparing) StartBattleCountdown(0, true);
        else if (State == BattleFlowState.BetweenBattles && BattleNumber < BattleCount && RewardResolved)
            StartBattleCountdown(BattleNumber, false);
    }

    public bool BeginOrderEdit()
    {
        if (!CanEditOrder || IsEditingOrder) return false;
        orderDraft = new List<SkillData>(runDeck);
        RefreshUI();
        return true;
    }

    public bool MoveOrderCard(int from, int to)
    {
        if (!CanEditOrder || !IsEditingOrder || from < 0 || to < 0
            || from >= orderDraft.Count || to >= orderDraft.Count) return false;
        SkillData card = orderDraft[from];
        orderDraft.RemoveAt(from);
        orderDraft.Insert(to, card);
        RefreshUI();
        return true;
    }

    public bool SaveOrder()
    {
        if (!CanEditOrder || !IsEditingOrder) return false;
        runDeck = orderDraft;
        orderDraft = null;
        Debug.Log(string.Format(orderLogFormat, State == BattleFlowState.Preparing ? 1 : BattleNumber + 1,
            string.Join(" → ", runDeck.ConvertAll(card => card.DisplayName))), this);
        RefreshUI();
        return true;
    }

    public void CancelOrderEdit()
    {
        orderDraft = null;
        RefreshUI();
    }

    public bool SelectReward(int index)
    {
        BattleRewardOption option = GetRewardChoice(index);
        if (State == BattleFlowState.Inheriting)
        {
            if (!inheritance.CanInherit(option)) return false;
            if (option.Card == null) return CommitInheritance(option, -1);
            SelectedReplacementIndex = -1;
            SelectedReward = option.Card;
            InheritanceSaveFailed = false;
            RefreshUI();
            return true;
        }
        if (State != BattleFlowState.BetweenBattles || RewardResolved || option == null
            || !CanOffer(option)) return false;
        SelectedReplacementIndex = -1;
        if (option.Card == null)
        {
            if (!ApplyPassive(option.Effect)) return false;
            earnedRewards.Add(option);
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
        if ((State != BattleFlowState.BetweenBattles && State != BattleFlowState.Inheriting) || RewardResolved) return;
        InheritanceSaveFailed = false;
        SelectedReplacementIndex = -1;
        SelectedReward = null;
        RefreshUI();
    }

    public bool SelectReplacementSlot(int index)
    {
        if ((State != BattleFlowState.BetweenBattles && State != BattleFlowState.Inheriting)
            || RewardResolved || SelectedReward == null || index < 0 || index >= runDeck.Count
            || runDeck.Contains(SelectedReward)) return false;
        SelectedReplacementIndex = index;
        InheritanceSaveFailed = false;
        RefreshUI();
        return true;
    }

    public bool ConfirmReplacement()
    {
        int index = SelectedReplacementIndex;
        if ((State != BattleFlowState.BetweenBattles && State != BattleFlowState.Inheriting)
            || RewardResolved || SelectedReward == null || index < 0 || index >= runDeck.Count
            || runDeck.Contains(SelectedReward)) return false;
        if (State == BattleFlowState.Inheriting)
        {
            var option = rewardChoices.Find(choice => choice.Card == SelectedReward);
            return CommitInheritance(option, index);
        }
        var earned = rewardChoices.Find(choice => choice.Card == SelectedReward);
        if (earned != null) earnedRewards.Add(earned);
        SkillData removed = runDeck[index];
        runDeck[index] = SelectedReward;
        Debug.Log(string.Format(replaceLogFormat, BattleNumber, index + 1, removed.DisplayName, SelectedReward.DisplayName), this);
        SelectedReplacementIndex = -1;
        SelectedReward = null;
        RewardResolved = true;
        RefreshUI();
        return true;
    }

    public bool SkipReward()
    {
        if (State == BattleFlowState.Inheriting) { ReturnToTitle(); return true; }
        if (State != BattleFlowState.BetweenBattles || RewardResolved) return false;
        SelectedReplacementIndex = -1;
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
        earnedRewards.Clear();
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

    public void BeginTutorial(bool retry)
    {
        int hp = retry ? tutorial.Enemy.CurrentHp : tutorial.Enemy.MaxHp;
        runDeck = deck.CopyStartingDeck();
        BeginBattle(0, true, new[] { tutorial.Enemy });
        tutorial.Begin(hp, retry);
        RefreshUI();
    }

    public void FinishTutorial(bool skipped)
    {
        Time.timeScale = 0f;
        deck.EndBattle();
        player.EndBattle();
        metrics.FlushPendingSummary();
        metrics.WaitForBattle();
        if (information != null) information.BeginBattle();
        panelHoldRemaining = 0f;
        ApplyInheritance();
        State = skipped ? BattleFlowState.Preparing : BattleFlowState.TutorialComplete;
        if (!skipped) HoldVictoryPresentation();
        if (PanelHeld)
        {
            if (skillVfx != null) skillVfx.PlayOutUnscaled();
        }
        else FinishTutorialPresentation();
        Debug.Log(skipped ? "[튜토리얼] 스킵 — 보상 없이 편성" : "[튜토리얼] 학습·처치 완료 — 완료 안내 확인 대기", this);
        RefreshUI();
    }

    private void FinishTutorialPresentation()
    {
        enemies.WaitForBattle();
        if (skillVfx != null) skillVfx.Clear();
        if (damageNumbers != null) damageNumbers.Clear();
        if (State == BattleFlowState.TutorialComplete) tutorial.ShowCompletionGuide();
    }

    internal void OpenTutorialPreparation()
    {
        if (State != BattleFlowState.TutorialComplete || PanelHeld) return;
        State = BattleFlowState.Preparing;
        Debug.Log("[튜토리얼] 완료 안내 확인 — 편성 학습", this);
        RefreshUI();
    }

    public void RefreshTutorialUI() => RefreshUI();

    private void StartBattleCountdown(int index, bool restoreHp)
    {
        BeginBattle(index, restoreHp);
        if (countdownSeconds <= 0f) return;
        countdownRemaining = countdownSeconds;
        State = BattleFlowState.CountingDown;
        Time.timeScale = 0f;
        countdownPanel.SetActive(true);
        countdownPanel.transform.SetAsLastSibling();
        countdownText.text = string.Format(countdownFormat, Mathf.CeilToInt(countdownRemaining));
        RefreshUI();
    }

    private void AdvanceCountdown(float unscaledDelta)
    {
        if (!IsCountingDown) return;
        countdownRemaining = Mathf.Max(0f, countdownRemaining - unscaledDelta);
        if (countdownRemaining > 0f)
        {
            countdownText.text = string.Format(countdownFormat, Mathf.CeilToInt(countdownRemaining));
            return;
        }
        countdownPanel.SetActive(false);
        State = BattleFlowState.Fighting;
        Time.timeScale = information != null ? information.BattleSpeed : 1f;
        RefreshUI();
    }

    private void BuildCountdownUI()
    {
        Canvas canvas = progressText != null ? progressText.GetComponentInParent<Canvas>() : FindFirstObjectByType<Canvas>();
        if (canvas == null) return;
        countdownPanel = new GameObject("BattleCountdown", typeof(RectTransform), typeof(Image));
        countdownPanel.transform.SetParent(canvas.transform, false);
        var root = (RectTransform)countdownPanel.transform;
        root.anchorMin = Vector2.zero; root.anchorMax = Vector2.one;
        root.offsetMin = root.offsetMax = Vector2.zero;
        countdownPanel.GetComponent<Image>().color = new Color(0, 0, 0, .35f);
        var label = new GameObject("CountdownText", typeof(RectTransform), typeof(TextMeshProUGUI));
        label.transform.SetParent(root, false);
        countdownText = label.GetComponent<TextMeshProUGUI>();
        if (progressText != null) { countdownText.font = progressText.font; countdownText.fontSharedMaterial = progressText.fontSharedMaterial; }
        countdownText.fontSize = 64f;
        countdownText.color = new Color32(231, 203, 135, 255);
        countdownText.alignment = TextAlignmentOptions.Center;
        countdownText.raycastTarget = false;
        countdownText.rectTransform.anchorMin = countdownText.rectTransform.anchorMax = new Vector2(.5f, .5f);
        countdownText.rectTransform.sizeDelta = new Vector2(800, 260);
        countdownPanel.SetActive(false);
    }

    private void BeginBattle(int index, bool restoreHp, Enemy[] tutorialEnemies = null)
    {
        countdownRemaining = 0f;
        if (countdownPanel != null) countdownPanel.SetActive(false);
        Time.timeScale = 0f;
        // Update의 종료 처리 전에 재시작해도 마지막 결과와 요약을 먼저 마감한다.
        if (metrics.Ended) deck.EndBattle();
        metrics.FlushPendingSummary();
        // 이전 효과의 종료 처리를 먼저 끝내고, 그 다음에 새 전투 계측을 연다.
        if (damageNumbers != null) damageNumbers.Clear();
        // 이전 전투 종료 때 멈춰 있던 스킬 연출이 새 전투에서 재생되지 않게 지운다.
        panelHoldRemaining = 0f;
        ClearDefeatEffects();
        if (skillVfx != null) skillVfx.Clear();
        orderDraft = null;
        deck.SetDeck(runDeck);
        SelectedReplacementIndex = -1;
        SelectedReward = null;
        RewardResolved = false;
        rewardChoices.Clear();
        player.BeginBattle(restoreHp);
        cost.BeginBattle();
        if (information != null) information.BeginBattle();
        BattleNumber = tutorialEnemies != null ? 0 : index + 1;
        metrics.BeginBattle(BattleNumber);
        metrics.RecordRunModifiers(cost.RegenerationPerSecond, player.CriticalChance, tutorialEnemies != null ? "튜토리얼 (일반 전투와 별도)" : PassiveSummary, ResourceStatus);
        enemies.BeginBattle(tutorialEnemies ?? encounters[index].enemies, tutorialEnemies != null ? null : encounters[index].centerEnemy);
        State = BattleFlowState.Fighting;
        RefreshUI();
        Time.timeScale = information != null ? information.BattleSpeed : 1f;
        metrics.RecordBattleSpeed(Time.timeScale);
    }

    private void Update()
    {
        if (IsCountingDown) { AdvanceCountdown(Time.unscaledDeltaTime); return; }
        if (HasTutorial && tutorial.Active) { tutorial.TickCombat(); return; }
        if (TickPanelHold()) return;
        if (BattleNumber == 0 || State != BattleFlowState.Fighting) return;
        if (!metrics.Ended && !enemies.CombatEnded && player.IsAlive) return;
        bool won = player.IsAlive && enemies.CombatEnded;
        enemies.ClearBossOrbs();
        State = !won ? BattleFlowState.Defeat
            : BattleNumber == BattleCount ? BattleFlowState.Victory : BattleFlowState.BetweenBattles;
        Time.timeScale = 0f;
        deck.EndBattle();
        player.EndBattle();
        if (information != null) information.BeginBattle();
        if (State == BattleFlowState.BetweenBattles) PrepareRewards();
        // 승패·보상·요약은 즉시 처리하고, 사망 모션과 막타 연출이 끝난 뒤 화면만 연다.
        panelHoldRemaining = 0f;
        if (won) HoldVictoryPresentation();
        if (!won && defeatEffectHold > 0f)
        {
            // 시간·입력·판정은 이미 종료. 발동한 적 연출만 실제 시간으로 마친다.
            if (skillVfx != null) skillVfx.Clear();
            endingBossEffects = FindObjectsByType<BossAttackVfx>(FindObjectsSortMode.None);
            endingEnemyEffects = FindObjectsByType<EnemyAttackVfx>(FindObjectsSortMode.None);
            endingArrows = FindObjectsByType<ArrowFlight>(FindObjectsSortMode.None);
            foreach (var fx in endingBossEffects) fx.PlayOutUnscaled();
            foreach (var fx in endingEnemyEffects) fx.PlayOutUnscaled();
            foreach (var arrow in endingArrows) arrow.PlayOutUnscaled();
            panelHoldRemaining = Mathf.Max(defeatEffectHold, defeatEffectTail);
        }
        if (PanelHeld)
        {
            if (skillVfx != null) skillVfx.PlayOutUnscaled();
        }
        // 결과 화면 뒤에서 멈춘 채 남을 연출을 지운다 (다음 전투에서 재생되는 잔여 연출 방지).
        else if (skillVfx != null) skillVfx.Clear();
        RefreshUI();
        metrics.FlushPendingSummary();
    }

    private void HoldVictoryPresentation()
    {
        if (finishingEffectHold <= 0f) return;
        if (skillVfx != null && skillVfx.HasFinishingEffect) panelHoldRemaining = finishingEffectHold;
        foreach (var model in FindObjectsByType<EnemyAnimationDriver>(FindObjectsSortMode.None))
            panelHoldRemaining = Mathf.Max(panelHoldRemaining, model.PlayDeathUnscaled());
    }

    // 막타 연출을 보여주는 동안 결과·보상 화면을 숨겨 두는 남은 시간(실제 초).
    private float panelHoldRemaining;
    private bool PanelHeld => panelHoldRemaining > 0f;

    /// <summary>화면 보류 시간을 줄이고, 끝나면 연출을 지우고 결과·보상 화면을 띄운다. 보류 중이면 true.</summary>
    private bool TickPanelHold()
    {
        if (!PanelHeld) return false;
        panelHoldRemaining -= Time.unscaledDeltaTime;
        if (State == BattleFlowState.Defeat && HasDefeatEffects())
            panelHoldRemaining = Mathf.Max(panelHoldRemaining, defeatEffectTail);
        if (PanelHeld) return true;
        panelHoldRemaining = 0f;
        if (skillVfx != null) skillVfx.Clear();
        ClearDefeatEffects();
        if (State == BattleFlowState.TutorialComplete) FinishTutorialPresentation();
        RefreshUI();
        return false;
    }

    private bool HasDefeatEffects()
    {
        if (endingBossEffects != null)
            foreach (var fx in endingBossEffects) if (fx != null && fx.HasFinishingEffect) return true;
        if (endingEnemyEffects != null)
            foreach (var fx in endingEnemyEffects) if (fx != null && fx.HasFinishingEffect) return true;
        if (endingArrows != null)
            foreach (var arrow in endingArrows) if (arrow != null && arrow.IsFlying) return true;
        return false;
    }

    private void ClearDefeatEffects()
    {
        if (endingBossEffects != null) foreach (var fx in endingBossEffects) if (fx != null) fx.Clear();
        if (endingEnemyEffects != null) foreach (var fx in endingEnemyEffects) if (fx != null) fx.Clear();
        if (endingArrows != null) foreach (var arrow in endingArrows) if (arrow != null) Destroy(arrow.gameObject);
        endingBossEffects = null; endingEnemyEffects = null; endingArrows = null;
    }

    private void RefreshUI()
    {
        bool title = State == BattleFlowState.Title;
        bool preparing = State == BattleFlowState.Preparing || State == BattleFlowState.TutorialComplete;
        if (startPanel != null) startPanel.SetActive(title);
        if (progressText != null)
        {
            progressText.gameObject.SetActive(!title && !preparing);
            if (HasTutorial && tutorial.Active) progressText.text = tutorialMapName;
            else if (BattleNumber > 0)
                progressText.text = encounters[BattleNumber - 1].label;
        }
        bool ended = State == BattleFlowState.Victory || State == BattleFlowState.Defeat;
        bool shown = !PanelHeld;
        if (resultPanel != null) resultPanel.SetActive(ended && shown);
        if (nextButton != null) nextButton.gameObject.SetActive(shown && CanEditOrder && !IsEditingOrder);
        if (nextButtonLabel != null) nextButtonLabel.text = preparing ? firstBattleText : nextBattleText;
        if (restartButton != null)
        {
            restartButton.gameObject.SetActive(shown && ended);
            var label = restartButton.GetComponentInChildren<TMP_Text>();
            if (label != null) label.text = State == BattleFlowState.Defeat ? inheritanceButtonText : restartButtonText;
        }
        if (rewardUI != null && shown) rewardUI.Refresh();
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
        if (restartButton != null) restartButton.onClick.RemoveListener(ContinueAfterResult);
    }
}
