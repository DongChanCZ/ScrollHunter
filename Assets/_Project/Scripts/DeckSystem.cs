using System.Collections.Generic;
using UnityEngine;

/// <summary>손패 슬롯이 왜 못 쓰이는지. UI가 사유별로 다르게 표시한다. (02 문서 §6.5.4)</summary>
public enum SlotState
{
    Ready,
    Empty,
    /// <summary>기절 중. 손패 전체가 막힌다.</summary>
    Stunned,
    /// <summary>행동 잠금(GCD) 중. 손패 전체가 막힌다.</summary>
    ActionLocked,
    /// <summary>차단 카드인데 타겟이 캐스팅 중이 아님. 그 슬롯만 막힌다.</summary>
    NoValidTarget,
    /// <summary>코스트 부족. 그 슬롯만 막힌다.</summary>
    NotEnoughCost,
    /// <summary>채널링 시간 또는 효과 연결이 유효하지 않음.</summary>
    InvalidConfiguration,
}

/// <summary>
/// 덱 8장을 손패 4칸 + 대기열로 관리하고, 카드 효과를 적용한다.
/// 셔플 없음. 등록 순서 그대로 결정론적으로 순환한다.
/// 손패 슬롯 위치는 고정이라 카드가 왼쪽으로 밀리지 않는다.
///
/// **코스트 소모와 손패 순환은 입력을 수락한 즉시** 끝난다 (10 문서 A14).
/// 일반 시전은 완료 시 효과 적용(A11), 채널링은 진행 중 효과 유지/발생(A18).
/// 기절로 끊겨도 코스트와 카드는 돌아오지 않는다.
/// 채널링 중 이미 적용된 결과는 유지하고, 진행 중인 유지 효과만 정리한다.
/// </summary>
public class DeckSystem : MonoBehaviour
{
    public const int DeckSize = 8;
    public const int HandSize = 4;

    [SerializeField] private CostSystem costSystem;
    [SerializeField] private EnemyManager enemyManager;
    [SerializeField] private Player player;

    [Tooltip("4단계 계측. 비워두면 씬에서 자동으로 찾는다.")]
    [SerializeField] private CombatMetrics metrics;

    [Tooltip("프로토타입용 기본 덱 8장. SetDeck으로 주입하면 이 값은 쓰이지 않는다.")]
    [SerializeField] private List<SkillData> startingDeck = new List<SkillData>();

    [Tooltip("손패 슬롯 입력 키. 순서대로 슬롯 0~3")]
    [SerializeField] private KeyCode[] handKeys = { KeyCode.Q, KeyCode.W, KeyCode.E, KeyCode.R };

    [Tooltip("즉발기에도 적용되는 행동 잠금 하한(초)")]
    [SerializeField] private float minGcd = 0.4f;

    [Tooltip("차단 성공 시 적이 경직되는 시간(초). 10 문서 M9")]
    [SerializeField] private float interruptStagger = 2.5f;

    private readonly SkillData[] hand = new SkillData[HandSize];
    private readonly Queue<SkillData> queue = new Queue<SkillData>();
    private bool initialized;

    // 일반 시전과 채널링이 공유하는 진행 상태.
    // 손패 순환은 입력 수락 시 이미 끝났으므로 슬롯 번호를 들고 있을 필요가 없다.
    private SkillData castingCard;
    private float castRemaining;
    private float minimumLockRemaining;
    private ChannelEffect activeChannel;
    private ChannelContext channelContext;

    // 진행 중인 캐스팅의 계측 순서 번호. 효과 적용·기절 취소를 같은 번호로 잇는다.
    private int castingUseId;

    /// <summary>남은 행동 잠금 시간(초). MAX(캐스팅 시간, 최소 GCD)에서 줄어든다.</summary>
    public float LockRemaining { get; private set; }

    public bool IsLocked => LockRemaining > 0f || IsCasting;
    public float InterruptStagger => Mathf.Max(0f, interruptStagger + (player != null ? player.InterruptStaggerBonus : 0f));

    /// <summary>카드 캐스팅이 진행 중인지.</summary>
    public bool IsCasting => castingCard != null;
    public bool IsChanneling => castingCard != null && castingCard.IsChanneling;

    private bool BattleEnded => (metrics != null && metrics.Ended)
        || (enemyManager != null && enemyManager.CombatEnded) || (player != null && !player.IsAlive);

    /// <summary>캐스팅 완료까지 남은 시간(초).</summary>
    public float CastRemaining => castRemaining;

    /// <summary>
    /// 지금 시전 중인 카드. 손패에는 입력 수락 시점에 이미 다음 카드가 들어와 있으므로(10 A14),
    /// 시전 표시는 손패 슬롯이 아니라 이 값을 봐야 한다.
    /// </summary>
    public SkillData CastingCard => castingCard;

    /// <summary>마지막으로 끝난 시전의 카드. 아직 없으면 null.</summary>
    public SkillData LastCastCard { get; private set; }

    /// <summary>마지막 시전이 기절로 끊겼는지. 정상 완료면 false (10 A12).</summary>
    public bool LastCastCancelled { get; private set; }

    /// <summary>마지막 시전이 끝난 시각(Time.time). 취소 표시를 얼마나 띄울지 재는 데만 쓴다.</summary>
    public float LastCastEndTime { get; private set; }

    private void Awake()
    {
        if (costSystem == null) costSystem = FindFirstObjectByType<CostSystem>();
        if (enemyManager == null) enemyManager = FindFirstObjectByType<EnemyManager>();
        if (player == null) player = FindFirstObjectByType<Player>();
        if (metrics == null) metrics = FindFirstObjectByType<CombatMetrics>();
    }

    private void Start()
    {
        if (!initialized) SetDeck(startingDeck);
    }

    public List<SkillData> CopyStartingDeck() => new List<SkillData>(startingDeck);
    public void ResetStartingDeck() => SetDeck(startingDeck);

    public void EndBattle()
    {
        FinishCast(ChannelEndReason.CombatEnded);
        LockRemaining = minimumLockRemaining = 0f;
    }

    /// <summary>덱을 외부에서 주입한다. 앞 4장이 손패, 나머지가 대기열.</summary>
    public void SetDeck(IList<SkillData> deck)
    {
        FinishCast(ChannelEndReason.DeckReset);
        System.Array.Clear(hand, 0, HandSize);
        queue.Clear();
        LockRemaining = minimumLockRemaining = 0f;
        castingCard = null;
        castRemaining = 0f;
        castingUseId = 0;
        LastCastCard = null;
        LastCastCancelled = false;
        LastCastEndTime = 0f;
        initialized = true;
        if (deck == null) return;

        int index = 0;
        foreach (SkillData card in deck)
        {
            if (card == null) continue;
            if (index < HandSize) hand[index] = card;
            else queue.Enqueue(card);
            index++;
        }
    }

    public SkillData GetHandCard(int slot)
    {
        if (slot < 0 || slot >= HandSize) return null;
        return hand[slot];
    }

    /// <summary>대기열 맨 앞 카드. 없으면 null.</summary>
    public SkillData PeekNext() => queue.Count > 0 ? queue.Peek() : null;

    /// <summary>슬롯이 왜 못 쓰이는지. UI 표시용이자 입력 차단 기준이다.</summary>
    public SlotState GetSlotState(int slot)
    {
        SkillData card = GetHandCard(slot);
        if (card == null) return SlotState.Empty;

        // 손패 전체를 막는 사유가 먼저.
        if (player != null && player.IsStunned) return SlotState.Stunned;
        if (IsLocked) return SlotState.ActionLocked;

        if (!card.HasValidChannel) return SlotState.InvalidConfiguration;

        // 차단 카드는 타겟이 캐스팅 중이 아니면 아예 누를 수 없다.
        if (card.Category == SkillCategory.Interrupt && !HasInterruptableTarget())
            return SlotState.NoValidTarget;

        if (costSystem == null || !costSystem.CanAfford(card.Cost)) return SlotState.NotEnoughCost;

        return SlotState.Ready;
    }

    /// <summary>빨강도 입력 가능. 코스트·카드는 소모하고 차단은 실패한다. 피해가 있는 카드는 피해만 적용.</summary>
    private bool HasInterruptableTarget()
    {
        Enemy target = enemyManager != null ? enemyManager.CurrentTarget : null;
        return target != null && target.IsCasting;
    }

    private void Update()
    {
        AdvanceCast(Time.deltaTime);
        if (Time.timeScale <= 0f || BattleEnded || IsLocked || (player != null && player.IsStunned)) return;

        int count = Mathf.Min(handKeys.Length, HandSize);
        for (int i = 0; i < count; i++)
        {
            if (!Input.GetKeyDown(handKeys[i])) continue;
            TryUseSlot(i);
            break;
        }
    }

    private void AdvanceCast(float deltaTime)
    {
        // 종료/기절 정리는 timeScale 0에서도 실행한다. 정지 중에는 효과 진행만 멈춘다.
        if (BattleEnded) { FinishCast(ChannelEndReason.CombatEnded); LockRemaining = 0f; return; }
        if (player != null && player.IsStunned)
        {
            FinishCast(ChannelEndReason.Stunned);
            LockRemaining = 0f;
            return;
        }
        if (Time.timeScale <= 0f || deltaTime <= 0f) return;

        LockRemaining = Mathf.Max(0f, LockRemaining - deltaTime);
        minimumLockRemaining = Mathf.Max(0f, minimumLockRemaining - deltaTime);
        if (!IsCasting) return;

        float elapsed = Mathf.Min(deltaTime, castRemaining);
        castRemaining = Mathf.Max(0f, castRemaining - deltaTime);
        if (activeChannel != null)
        {
            try { activeChannel.Tick(channelContext, elapsed); }
            catch (System.Exception exception)
            {
                Debug.LogException(exception, this);
                FinishCast(ChannelEndReason.Error);
                return;
            }
        }

        // 효과 자체가 마지막 적을 죽이거나 플레이어에게 기절을 줄 수도 있다.
        if (BattleEnded) FinishCast(ChannelEndReason.CombatEnded);
        else if (player != null && player.IsStunned) FinishCast(ChannelEndReason.Stunned);
        else if (activeChannel != null && activeChannel.TargetLost)
        {
            float recovery = activeChannel.TargetLossRecoveryRemaining;
            FinishCast(ChannelEndReason.TargetDied);
            LockRemaining = Mathf.Max(minimumLockRemaining, recovery);
        }
        else if (castRemaining <= 0f) FinishCast(ChannelEndReason.Completed);
    }

    private void OnDisable()
    {
        FinishCast(ChannelEndReason.Disabled);
        LockRemaining = 0f;
    }

    /// <summary>입력 수락 시 코스트·카드 소모. 발동 방식에 따라 효과를 시작한다.</summary>
    public bool TryUseSlot(int slot)
    {
        if (Time.timeScale <= 0f || BattleEnded) return false;
        SkillData card = GetHandCard(slot);
        SlotState state = GetSlotState(slot);

        if (state != SlotState.Ready)
        {
            // 잠금·기절·대상 조건을 모두 통과하고 코스트만 모자란 경우만 센다 (09 문서 4단계 ②).
            if (state == SlotState.NotEnoughCost && metrics != null) metrics.RecordCostShortInput();

            // 비활성 사유는 코스트를 쓰지 않는다.
            if (state != SlotState.Empty) LogUse(card, "-", StateToLabel(state));
            return false;
        }

        float costBefore = costSystem.Current;

        if (!costSystem.TrySpend(card.Cost))
        {
            if (metrics != null) metrics.RecordCostShortInput();
            LogUse(card, "-", "코스트 부족");
            return false;
        }

        // 유효한 입력을 수락한 즉시 카드를 순환시킨다 (10 문서 A14).
        // 효과 적용 시점과 무관하게 손패 교체는 여기서 끝난다.
        // 새로 들어온 카드는 행동 잠금 때문에 바로 쓸 수 없다.
        CycleSlot(card, slot);

        // 사용 1회는 여기서 센다. 효과 적용 때 다시 세지 않는다 (09 문서 4단계 ②).
        castingUseId = metrics != null
            ? metrics.RecordUseAccepted(SlotLabel(slot), card, DescribeTarget(card), costBefore, costSystem.Current)
            : 0;

        castingCard = card;
        castRemaining = card.CastTime;
        LockRemaining = Mathf.Max(card.CastTime, minGcd);
        minimumLockRemaining = minGcd;

        if (card.IsChanneling)
        {
            int acceptedUseId = castingUseId;
            channelContext = new ChannelContext(card, player, enemyManager,
                (target, hitIndex) => DealHit(target, card, acceptedUseId, hitIndex, false));
            activeChannel = Instantiate(card.ChannelEffect);
            try { activeChannel.Begin(channelContext); }
            catch (System.Exception exception)
            {
                Debug.LogException(exception, this);
                FinishCast(ChannelEndReason.Error);
            }
            if (BattleEnded) FinishCast(ChannelEndReason.CombatEnded);
            else if (player != null && player.IsStunned) FinishCast(ChannelEndReason.Stunned);
        }
        else if (castRemaining <= 0f) FinishCast(ChannelEndReason.Completed);

        return true;
    }

    private void FinishCast(ChannelEndReason reason)
    {
        SkillData card = castingCard;
        if (card == null) return;
        int useId = castingUseId;
        ChannelEffect effect = activeChannel;
        ChannelContext context = channelContext;

        // End에서 정리가 다시 요청되더라도 중복 호출하지 않는다.
        castingCard = null;
        castRemaining = 0f;
        castingUseId = 0;
        activeChannel = null;
        channelContext = null;
        if (reason != ChannelEndReason.Completed) LockRemaining = 0f;

        if (effect != null)
        {
            try { effect.End(context, reason); }
            catch (System.Exception exception) { Debug.LogException(exception, this); }
            finally
            {
                if (Application.isPlaying) Destroy(effect);
                else DestroyImmediate(effect);
            }
        }

        string targetLabel = "-";
        string resultLabel;
        if (reason == ChannelEndReason.Completed && !card.IsChanneling)
            ApplyCard(card, useId, out targetLabel, out resultLabel);
        else
            resultLabel = (card.IsChanneling ? "채널링 종료 — " : "시전 종료 — ") + reason;

        // 완료된 채널링에는 기존 ApplyCard를 덧붙이지 않는다.
        if (metrics != null)
        {
            string label = card.DisplayName + " / " + targetLabel + " / " + resultLabel;
            if (reason == ChannelEndReason.Stunned) metrics.RecordUseCancelled(useId, label);
            else metrics.RecordUseResult(useId, label);
        }
        else LogUse(card, targetLabel, resultLabel);
        RecordCastEnd(card, reason == ChannelEndReason.Stunned);
    }

    /// <summary>
    /// 시전이 어떻게 끝났는지만 남긴다. UI가 정상 완료와 기절 취소를 구분할 유일한 근거다.
    /// 정상 완료와 기절 취소 모두 진행 상태를 비우므로 종료 이유를 따로 보관한다.
    /// 코스트·카드 순환·판정에는 관여하지 않는다.
    /// </summary>
    private void RecordCastEnd(SkillData card, bool cancelled)
    {
        LastCastCard = card;
        LastCastCancelled = cancelled;
        LastCastEndTime = Time.time;
    }

    /// <summary>
    /// 쓴 카드는 덱 맨 아래로, 대기열 맨 앞이 그 슬롯에 들어온다.
    /// 슬롯 인덱스는 건드리지 않으므로 카드가 왼쪽으로 밀리지 않는다.
    /// </summary>
    private void CycleSlot(SkillData card, int slot)
    {
        if (slot < 0 || slot >= HandSize) return;

        queue.Enqueue(card);
        hand[slot] = queue.Dequeue();
    }

    private void ApplyCard(SkillData card, int useId, out string targetLabel, out string resultLabel)
    {
        targetLabel = "-";
        resultLabel = "-";

        switch (card.Category)
        {
            case SkillCategory.Shield:
            {
                int granted = player != null ? player.AddShield(card.ShieldAmount) : 0;
                int stack = player != null ? player.Shield : 0;
                targetLabel = "자신";
                resultLabel = "방어도 +" + granted + " (누적 " + stack + ")";
                break;
            }

            case SkillCategory.Interrupt:
            {
                Enemy target = enemyManager != null ? enemyManager.CurrentTarget : null;
                targetLabel = target != null ? target.name : "-";

                if (target == null || !target.IsAlive) { resultLabel = "대상 없음"; break; }
                // 제압: 피해를 먼저 처리. 처치한 대상에는 차단 성공을 부여하지 않는다.
                int dealt = card.Damage > 0 ? DealTo(target, card, useId) : 0;
                if (!target.IsAlive)
                {
                    resultLabel = "피해 " + dealt + " / 대상 처치 — 차단 판정 없음";
                    enemyManager.NotifyEnemyDied();
                    break;
                }
                InterruptResult r = target.TryInterrupt(InterruptStagger);

                if (r == InterruptResult.Success && metrics != null) metrics.RecordInterruptSuccess();

                if (r == InterruptResult.Success)
                    resultLabel = "차단 성공 (경직 " + InterruptStagger.ToString("0.#") + "초)";
                else if (r == InterruptResult.FailedRed)
                    resultLabel = "차단 실패 — 빨강 캐스팅";
                else
                    resultLabel = "차단 실패 — 캐스팅 중 아님";
                if (card.Damage > 0) resultLabel = "피해 " + dealt + " / " + resultLabel;
                break;
            }

            case SkillCategory.Deal:
            {
                if (enemyManager == null) { resultLabel = "대상 없음"; break; }

                if (card.IsAreaOfEffect)
                {
                    List<Enemy> all = enemyManager.GetAliveEnemies();
                    targetLabel = "적 전체 " + all.Count + "체";
                    int total = 0;
                    for (int i = 0; i < all.Count; i++) total += DealTo(all[i], card, useId);
                    resultLabel = "피해 " + total;
                }
                else
                {
                    Enemy target = enemyManager.CurrentTarget;
                    targetLabel = target != null ? target.name : "-";
                    if (target == null)
                    {
                        resultLabel = "대상 없음";
                    }
                    else
                    {
                        int dealt = DealTo(target, card, useId);
                        resultLabel = "피해 " + dealt + " → HP " + target.CurrentHp + "/" + target.MaxHp;
                    }
                }

                enemyManager.NotifyEnemyDied();
                break;
            }
        }
    }

    private int DealTo(Enemy target, SkillData card, int useId)
    {
        if (target == null) return 0;
        // 일반 시전도 채널링과 같은 타격별 판정·반올림·계측 경로를 사용한다.
        int total = 0;
        for (int hit = 1; hit <= card.HitCount; hit++) total += DealHit(target, card, useId, hit, true);
        return total;
    }

    // 일반 개별 타격과 채널링이 공유한다. 사용 횟수는 여기서 늘리지 않는다.
    private int DealHit(Enemy target, SkillData card, int useId, int hitIndex, bool allowRemainingVisual)
    {
        if (target == null || card.Damage <= 0 || BattleEnded) return 0;
        bool visualOnly = !target.IsAlive;
        if (visualOnly && !allowRemainingVisual) return 0;
        // 사망 후 잔여 숫자는 기존 일반 피해로 표시하며 추첨·치명타 집계에 넣지 않는다.
        bool critical = !visualOnly && player != null && player.RollCritical();
        int amount = DamageFormula.Compute(card.Damage, 1, target.Data.Defense,
            critical ? player.CriticalMultiplier : 1f);
        int before = target.CurrentHp;
        if (visualOnly) target.ShowOverkill(amount);
        else target.TakeDamage(amount, critical);
        int actual = Mathf.Max(0, before - target.CurrentHp);
        if (metrics != null) metrics.RecordHit(useId, card, target.name, hitIndex,
            amount, actual, amount - actual, visualOnly, critical);
        return amount;
    }

    /// <summary>입력 로그용 슬롯 이름. 예: 슬롯2(W)</summary>
    private string SlotLabel(int slot)
    {
        if (slot < 0 || slot >= handKeys.Length) return "슬롯" + slot;
        return "슬롯" + (slot + 1) + "(" + handKeys[slot] + ")";
    }

    /// <summary>입력 시점의 대상. 효과 적용 시점에는 달라질 수 있다.</summary>
    private string DescribeTarget(SkillData card)
    {
        if (card == null) return "-";
        if (card.Category == SkillCategory.Shield) return "자신";
        if (card.IsAreaOfEffect) return "적 전체";
        Enemy target = enemyManager != null ? enemyManager.CurrentTarget : null;
        return target != null ? target.name : "-";
    }

    private string StateToLabel(SlotState state)
    {
        if (state == SlotState.Stunned)
            return "사용 불가 — 기절 " + (player != null ? player.StunRemaining.ToString("0.0") : "?") + "초";
        if (state == SlotState.ActionLocked) return "행동 잠금 " + LockRemaining.ToString("0.0") + "초";
        if (state == SlotState.NoValidTarget) return "사용 불가 — 타겟이 캐스팅 중 아님";
        if (state == SlotState.NotEnoughCost) return "코스트 부족";
        if (state == SlotState.InvalidConfiguration) return "채널링 설정 확인 — 양수 시간과 효과 연결 필요";
        return state.ToString();
    }

    private void LogUse(SkillData card, string targetLabel, string resultLabel)
    {
        string cardName = card != null ? card.DisplayName : "-";
        string cost = card != null ? card.Cost.ToString("0.#") : "-";
        Debug.Log("[" + Time.time.ToString("F2") + "s / " + cardName + " / " + cost
                  + " / " + targetLabel + " / " + resultLabel + "]", this);
    }
}
