using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 덱 8장을 손패 4칸 + 대기열로 관리한다.
/// 셔플 없음. 등록 순서 그대로 결정론적으로 순환한다.
/// 손패 슬롯 위치는 고정이라 카드가 왼쪽으로 밀리지 않는다.
/// </summary>
public class DeckSystem : MonoBehaviour
{
    public const int HandSize = 4;

    [SerializeField] private CostSystem costSystem;

    [Tooltip("프로토타입용 기본 덱 8장. SetDeck으로 주입하면 이 값은 쓰이지 않는다.")]
    [SerializeField] private List<SkillData> startingDeck = new List<SkillData>();

    [Tooltip("손패 슬롯 입력 키. 순서대로 슬롯 0~3")]
    [SerializeField] private KeyCode[] handKeys = { KeyCode.Q, KeyCode.W, KeyCode.E, KeyCode.R };

    [Tooltip("즉발기에도 적용되는 행동 잠금 하한(초)")]
    [SerializeField] private float minGcd = 0.4f;

    private readonly SkillData[] hand = new SkillData[HandSize];
    private readonly Queue<SkillData> queue = new Queue<SkillData>();
    private bool initialized;

    /// <summary>남은 행동 잠금 시간(초).</summary>
    public float LockRemaining { get; private set; }

    public bool IsLocked => LockRemaining > 0f;

    private void Awake()
    {
        if (costSystem == null) costSystem = FindFirstObjectByType<CostSystem>();
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

    private void Update()
    {
        if (LockRemaining > 0f)
        {
            LockRemaining = Mathf.Max(0f, LockRemaining - Time.deltaTime);
        }

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
        if (IsLocked) return false;

        SkillData card = GetHandCard(slot);
        if (card == null) return false;

        if (costSystem == null)
        {
            Debug.LogError($"[{nameof(DeckSystem)}] CostSystem 참조가 없습니다.", this);
            return false;
        }

        if (!costSystem.TrySpend(card.Cost))
        {
            Debug.Log($"[{nameof(DeckSystem)}] 코스트 부족: {card.DisplayName} " +
                      $"(필요 {card.Cost}, 보유 {costSystem.Current:F1})", this);
            return false;
        }

        LockRemaining = Mathf.Max(card.CastTime, minGcd);

        // 쓴 카드는 덱 맨 아래로, 대기열 맨 앞이 그 슬롯에 들어온다.
        // 슬롯 인덱스는 건드리지 않으므로 카드가 왼쪽으로 밀리지 않는다.
        queue.Enqueue(card);
        hand[slot] = queue.Dequeue();

        return true;
    }
}
