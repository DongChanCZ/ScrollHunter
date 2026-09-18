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
}

/// <summary>
/// 덱 8장을 손패 4칸 + 대기열로 관리하고, 카드 효과를 적용한다.
/// 셔플 없음. 등록 순서 그대로 결정론적으로 순환한다.
/// 손패 슬롯 위치는 고정이라 카드가 왼쪽으로 밀리지 않는다.
///
/// **코스트 소모와 손패 순환은 입력을 수락한 즉시** 끝난다 (10 문서 A14).
/// **카드 효과만 캐스팅이 끝나야** 적용된다 (A11).
/// 그래야 「캐스팅 중 기절당하면 캐스팅 취소」(02 E02c)가 성립한다.
/// 끊겨도 코스트와 카드는 돌아오지 않는다. 효과만 발생하지 않는다.
/// </summary>
public class DeckSystem : MonoBehaviour
{
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
    [SerializeField] private float interruptStagger = 4f;

    private readonly SkillData[] hand = new SkillData[HandSize];
    private readonly Queue<SkillData> queue = new Queue<SkillData>();
    private bool initialized;

    // 진행 중인 캐스팅. 완료돼야 효과가 난다.
    // 손패 순환은 입력 수락 시 이미 끝났으므로 슬롯 번호를 들고 있을 필요가 없다.
    private SkillData castingCard;
    private float castRemaining;

    // 진행 중인 캐스팅의 계측 순서 번호. 효과 적용·기절 취소를 같은 번호로 잇는다.
    private int castingUseId;

    /// <summary>남은 행동 잠금 시간(초). MAX(캐스팅 시간, 최소 GCD)에서 줄어든다.</summary>
    public float LockRemaining { get; private set; }

    public bool IsLocked => LockRemaining > 0f;

    /// <summary>카드 캐스팅이 진행 중인지.</summary>
    public bool IsCasting => castingCard != null;

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

    /// <summary>덱을 외부에서 주입한다. 앞 4장이 손패, 나머지가 대기열.</summary>
    public void SetDeck(IList<SkillData> deck)
    {
        System.Array.Clear(hand, 0, HandSize);
        queue.Clear();
        LockRemaining = 0f;
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

        // 차단 카드는 타겟이 캐스팅 중이 아니면 아예 누를 수 없다.
        if (card.Category == SkillCategory.Interrupt && !HasInterruptableTarget())
            return SlotState.NoValidTarget;

        if (costSystem == null || !costSystem.CanAfford(card.Cost)) return SlotState.NotEnoughCost;

        return SlotState.Ready;
    }

    /// <summary>빨강도 캐스팅 중이므로 활성이다. 눌리면 실패하고 코스트만 나간다.</summary>
    private bool HasInterruptableTarget()
    {
        Enemy target = enemyManager != null ? enemyManager.CurrentTarget : null;
        return target != null && target.IsCasting;
    }

    private void Update()
    {
        // 기절이 최우선이다. 캐스팅 중이었다면 취소되고 코스트는 돌아오지 않는다.
        if (player != null && player.IsStunned)
        {
            if (IsCasting) CancelCast();
            LockRemaining = 0f;   // 기절이 행동 잠금을 대체한다
            return;
        }

        if (LockRemaining > 0f)
        {
            LockRemaining = Mathf.Max(0f, LockRemaining - Time.deltaTime);
        }

        if (IsCasting)
        {
            castRemaining = Mathf.Max(0f, castRemaining - Time.deltaTime);
            if (castRemaining <= 0f) CompleteCast();
        }

        // 일시정지 중에는 카드를 쓸 수 없다. (패배로 timeScale이 0이 된 경우 포함)
        if (Time.timeScale <= 0f) return;

        // 잠금 중 입력은 버퍼에 저장하지 않고 무시한다.
        if (IsLocked) return;

        int count = Mathf.Min(handKeys.Length, HandSize);
        for (int i = 0; i < count; i++)
        {
            if (!Input.GetKeyDown(handKeys[i])) continue;
            TryUseSlot(i);
            break;
        }
    }

    /// <summary>카드를 쓴다. 코스트는 즉시 나가고, 효과는 캐스팅이 끝나야 난다.</summary>
    public bool TryUseSlot(int slot)
    {
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
        // 효과는 캐스팅이 끝나야 나지만 손패 교체는 여기서 끝난다.
        // 새로 들어온 카드는 행동 잠금 때문에 바로 쓸 수 없다.
        CycleSlot(card, slot);

        // 사용 1회는 여기서 센다. 효과 적용 때 다시 세지 않는다 (09 문서 4단계 ②).
        castingUseId = metrics != null
            ? metrics.RecordUseAccepted(SlotLabel(slot), card, DescribeTarget(card), costBefore, costSystem.Current)
            : 0;

        castingCard = card;
        castRemaining = card.CastTime;
        LockRemaining = Mathf.Max(card.CastTime, minGcd);

        // 캐스팅 시간이 0이면 그 자리에서 발동한다.
        if (castRemaining <= 0f) CompleteCast();

        return true;
    }

    /// <summary>캐스팅 완료. 여기서 처음으로 효과가 난다. 순환은 이미 끝났다.</summary>
    private void CompleteCast()
    {
        SkillData card = castingCard;
        int useId = castingUseId;

        castingCard = null;
        castRemaining = 0f;
        castingUseId = 0;

        if (card == null) return;

        string targetLabel;
        string resultLabel;
        ApplyCard(card, out targetLabel, out resultLabel);

        if (metrics != null) metrics.RecordUseResult(useId, card.DisplayName + " / " + targetLabel + " / " + resultLabel);
        else LogUse(card, targetLabel, resultLabel);

        RecordCastEnd(card, false);
    }

    /// <summary>
    /// 기절로 캐스팅이 끊겼다. 코스트는 돌아오지 않는다 (02 E02c).
    /// 카드는 입력 수락 시 이미 순환했으므로 **추가로 순환시키지 않는다** (10 문서 A12).
    /// 효과만 발생하지 않는다.
    /// </summary>
    private void CancelCast()
    {
        SkillData card = castingCard;
        int useId = castingUseId;

        castingCard = null;
        castRemaining = 0f;
        castingUseId = 0;

        if (card == null) return;

        const string reason = "캐스팅 취소 — 기절 (코스트 반환 없음, 카드는 입력 시 이미 순환)";

        // 사용 횟수에는 이미 들어가 있다. 결과만 취소로 구분한다.
        if (metrics != null) metrics.RecordUseCancelled(useId, card.DisplayName + " / " + reason);
        else LogUse(card, "-", reason);

        RecordCastEnd(card, true);
    }

    /// <summary>
    /// 시전이 어떻게 끝났는지만 남긴다. UI가 정상 완료와 기절 취소를 구분할 유일한 근거다.
    /// CompleteCast와 CancelCast는 바깥에서 보면 상태를 똑같이 비우기 때문에 이 기록이 없으면 구분할 수 없다.
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

    private void ApplyCard(SkillData card, out string targetLabel, out string resultLabel)
    {
        targetLabel = "-";
        resultLabel = "-";

        switch (card.Category)
        {
            case SkillCategory.Shield:
            {
                if (player != null) player.AddShield(card.ShieldAmount);
                int stack = player != null ? player.Shield : 0;
                targetLabel = "자신";
                resultLabel = "방어도 +" + card.ShieldAmount + " (누적 " + stack + ")";
                break;
            }

            case SkillCategory.Interrupt:
            {
                Enemy target = enemyManager != null ? enemyManager.CurrentTarget : null;
                targetLabel = target != null ? target.name : "-";

                InterruptResult r = target != null
                    ? target.TryInterrupt(interruptStagger)
                    : InterruptResult.NotCasting;

                if (r == InterruptResult.Success && metrics != null) metrics.RecordInterruptSuccess();

                if (r == InterruptResult.Success)
                    resultLabel = "차단 성공 (경직 " + interruptStagger.ToString("0.#") + "초)";
                else if (r == InterruptResult.FailedRed)
                    resultLabel = "차단 실패 — 빨강 캐스팅";
                else
                    resultLabel = "차단 실패 — 캐스팅 중 아님";
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
                    for (int i = 0; i < all.Count; i++) total += DealTo(all[i], card);
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
                        int dealt = DealTo(target, card);
                        resultLabel = "피해 " + dealt + " → HP " + target.CurrentHp + "/" + target.MaxHp;
                    }
                }

                enemyManager.NotifyEnemyDied();
                break;
            }
        }
    }

    private int DealTo(Enemy target, SkillData card)
    {
        if (target == null) return 0;
        int dealt = DamageFormula.Compute(card.Damage, card.HitCount, target.Data.Defense);
        target.TakeDamage(dealt);
        return dealt;
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
