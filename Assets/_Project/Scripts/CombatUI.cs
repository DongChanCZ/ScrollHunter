using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 코스트 / 손패 4칸 / 대기열 다음 1장을 표시한다. 읽기 전용.
/// </summary>
public class CombatUI : MonoBehaviour
{
    [System.Serializable]
    public class HandSlotView
    {
        public Image background;
        public TMP_Text nameText;
        public TMP_Text costText;

        [Tooltip("남은 행동 잠금 시간 표시")]
        public TMP_Text lockText;
    }

    [SerializeField] private CostSystem costSystem;
    [SerializeField] private DeckSystem deckSystem;

    [Tooltip("현재 코스트 (좌하단)")]
    [SerializeField] private TMP_Text costText;

    [SerializeField] private HandSlotView[] handSlots = new HandSlotView[DeckSystem.HandSize];

    [Tooltip("대기열 다음 1장")]
    [SerializeField] private TMP_Text nextCardText;

    [SerializeField] private Color slotNormalColor = Color.white;

    [Tooltip("행동 잠금 중 슬롯 색")]
    [SerializeField] private Color slotLockedColor = new Color(0.45f, 0.45f, 0.45f, 1f);

    [Tooltip("카드가 없는 칸에 표시할 문자열")]
    [SerializeField] private string emptySlotLabel = "-";

    private void Awake()
    {
        if (costSystem == null) costSystem = FindFirstObjectByType<CostSystem>();
        if (deckSystem == null) deckSystem = FindFirstObjectByType<DeckSystem>();
    }

    // 같은 프레임의 입력 결과까지 반영하려고 LateUpdate에서 갱신한다.
    private void LateUpdate()
    {
        if (costSystem != null && costText != null)
        {
            costText.text = costSystem.Current.ToString("F1");
        }

        if (deckSystem == null) return;

        bool locked = deckSystem.IsLocked;
        string lockLabel = locked ? deckSystem.LockRemaining.ToString("F1") : string.Empty;

        for (int i = 0; i < handSlots.Length; i++)
        {
            HandSlotView view = handSlots[i];
            if (view == null) continue;

            SkillData card = deckSystem.GetHandCard(i);

            if (view.nameText != null)
                view.nameText.text = card != null ? card.DisplayName : emptySlotLabel;

            if (view.costText != null)
                view.costText.text = card != null ? card.Cost.ToString("0.#") : string.Empty;

            if (view.lockText != null)
                view.lockText.text = lockLabel;

            if (view.background != null)
                view.background.color = locked ? slotLockedColor : slotNormalColor;
        }

        if (nextCardText != null)
        {
            SkillData next = deckSystem.PeekNext();
            nextCardText.text = next != null ? next.DisplayName : emptySlotLabel;
        }
    }
}
