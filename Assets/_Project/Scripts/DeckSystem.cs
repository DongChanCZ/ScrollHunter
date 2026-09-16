using System.Collections.Generic;
using UnityEngine;

/// <summary>손패 슬롯이 왜 못 쓰이는지. UI가 사유별로 다르게 표시한다.</summary>
public enum SlotState
{
    Ready,
    Empty,
    /// <summary>행동 잠금(GCD) 중.</summary>
    ActionLocked,
    /// <summary>차단 카드인데 타겟이 캐스팅 중이 아님. 입력 자체가 불가.</summary>
    NoValidTarget,
    NotEnoughCost,
}

/// <summary>
/// 덱 8장을 손패 4칸 + 대기열로 관리하고, 카드 효과를 적용한다.
/// 셔플 없음. 등록 순서 그대로 결정론적으로 순환한다.
/// 손패 슬롯 위치는 고정이라 카드가 왼쪽으로 밀리지 않는다.
/// </summary>
public class DeckSystem : MonoBehaviour
{
    public const int HandSize = 4;

    [SerializeField] private CostSystem costSystem;
    [SerializeField] private EnemyManager enemyManager;
    [SerializeField] private Player player;

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

    /// <summary>남은 행동 잠금 시간(초).</summary>
    public float LockRemaining { get; private set; }

    public bool IsLocked => LockRemaining > 0f;

    private void Awake()
    {
        if (costSystem == null) costSystem = FindFirstObjectByType<CostSystem>();
        if (enemyManager == null) enemyManager = FindFirstObjectByType<EnemyManager>();
        if (player == null) player = FindFirstObjectByType<Player>();
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
        if (LockRemaining > 0f)
        {
            LockRemaining = Mathf.Max(0f, LockRemaining - Time.deltaTime);
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

    public bool TryUseSlot(int slot)
    {
        SkillData card = GetHandCard(slot);
        SlotState state = GetSlotState(slot);

        if (state != SlotState.Ready)
        {
            // 비활성 사유는 코스트를 쓰지 않는다.
            if (state != SlotState.Empty) LogUse(card, "-", StateToLabel(state));
            return false;
        }

        if (!costSystem.TrySpend(card.Cost))
        {
            LogUse(card, "-", "코스트 부족");
            return false;
        }

        string targetLabel;
        string resultLabel;
        ApplyCard(card, out targetLabel, out resultLabel);
        LogUse(card, targetLabel, resultLabel);

        LockRemaining = Mathf.Max(card.CastTime, minGcd);

        // 쓴 카드는 덱 맨 아래로, 대기열 맨 앞이 그 슬롯에 들어온다.
        // 슬롯 인덱스는 건드리지 않으므로 카드가 왼쪽으로 밀리지 않는다.
        queue.Enqueue(card);
        hand[slot] = queue.Dequeue();

        return true;
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

    private string StateToLabel(SlotState state)
    {
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
