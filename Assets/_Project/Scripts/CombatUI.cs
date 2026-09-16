using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// HP / 방어도 / 코스트 / 손패 4칸 / 대기열 다음 1장 / 타겟 마커를 표시한다. 읽기 전용.
/// </summary>
public class CombatUI : MonoBehaviour
{
    [System.Serializable]
    public class HandSlotView
    {
        public Image background;
        public TMP_Text nameText;
        public TMP_Text costText;

        [Tooltip("잠금 시간 또는 비활성 사유 표시")]
        public TMP_Text lockText;
    }

    [SerializeField] private CostSystem costSystem;
    [SerializeField] private DeckSystem deckSystem;
    [SerializeField] private EnemyManager enemyManager;
    [SerializeField] private Player player;

    [Header("좌하단 상태")]
    [SerializeField] private TMP_Text costText;
    [SerializeField] private TMP_Text hpText;

    [Tooltip("방어도. 0일 때는 숨긴다.")]
    [SerializeField] private TMP_Text shieldText;

    [Header("손패")]
    [SerializeField] private HandSlotView[] handSlots = new HandSlotView[DeckSystem.HandSize];
    [SerializeField] private TMP_Text nextCardText;

    [Header("타겟 마커")]
    [Tooltip("현재 타겟 위로 이동한다. 타겟이 없으면 숨는다.")]
    [SerializeField] private Transform targetMarker;

    [SerializeField] private float markerHeight = 2.6f;

    [Tooltip("맥동 세기. 0이면 맥동 없음")]
    [SerializeField] private float markerPulse = 0.15f;

    [SerializeField] private float markerPulseSpeed = 3f;

    [Header("슬롯 색")]
    [SerializeField] private Color slotNormalColor = Color.white;

    [Tooltip("행동 잠금 중")]
    [SerializeField] private Color slotLockedColor = new Color(0.45f, 0.45f, 0.45f, 1f);

    [Tooltip("차단 카드인데 타겟이 캐스팅 중이 아님")]
    [SerializeField] private Color slotNoTargetColor = new Color(0.30f, 0.32f, 0.40f, 1f);

    [Tooltip("코스트 부족")]
    [SerializeField] private Color slotPoorColor = new Color(0.62f, 0.62f, 0.62f, 1f);

    [Header("글자 색")]
    [SerializeField] private Color costNormalColor = new Color(0.1f, 0.3f, 0.8f);
    [SerializeField] private Color costShortColor = new Color(0.9f, 0.15f, 0.15f);

    [Header("표시 문자열")]
    [SerializeField] private string emptySlotLabel = "-";

    [Tooltip("타겟이 캐스팅 중이 아닐 때 차단 슬롯에 표시")]
    [SerializeField] private string noTargetLabel = "잠김";

    [Tooltip("{0}=현재 HP, {1}=최대 HP")]
    [SerializeField] private string hpFormat = "{0} / {1}";

    [Tooltip("{0}=방어도")]
    [SerializeField] private string shieldFormat = "방어도 {0}";

    private Vector3 markerBaseScale = Vector3.one;

    private void Awake()
    {
        if (costSystem == null) costSystem = FindFirstObjectByType<CostSystem>();
        if (deckSystem == null) deckSystem = FindFirstObjectByType<DeckSystem>();
        if (enemyManager == null) enemyManager = FindFirstObjectByType<EnemyManager>();
        if (player == null) player = FindFirstObjectByType<Player>();

        if (targetMarker != null) markerBaseScale = targetMarker.localScale;
    }

    // 같은 프레임의 입력 결과까지 반영하려고 LateUpdate에서 갱신한다.
    private void LateUpdate()
    {
        UpdateStatus();
        UpdateHand();
        UpdateMarker();
    }

    private void UpdateStatus()
    {
        if (costSystem != null && costText != null)
        {
            costText.text = costSystem.Current.ToString("F1");
        }

        if (player == null) return;

        if (hpText != null)
        {
            hpText.text = string.Format(hpFormat,
                Mathf.CeilToInt(player.CurrentHp),
                Mathf.CeilToInt(player.MaxHp));
        }

        if (shieldText != null)
        {
            bool hasShield = player.Shield > 0;
            shieldText.enabled = hasShield;
            if (hasShield) shieldText.text = string.Format(shieldFormat, player.Shield);
        }
    }

    private void UpdateHand()
    {
        if (deckSystem == null) return;

        for (int i = 0; i < handSlots.Length; i++)
        {
            HandSlotView view = handSlots[i];
            if (view == null) continue;

            SkillData card = deckSystem.GetHandCard(i);
            SlotState state = deckSystem.GetSlotState(i);

            if (view.nameText != null)
                view.nameText.text = card != null ? card.DisplayName : emptySlotLabel;

            if (view.costText != null)
            {
                view.costText.text = card != null ? card.Cost.ToString("0.#") : string.Empty;
                // 코스트 부족은 숫자를 빨갛게 해서 다른 비활성 사유와 구분한다.
                view.costText.color = state == SlotState.NotEnoughCost ? costShortColor : costNormalColor;
            }

            if (view.lockText != null)
            {
                if (state == SlotState.ActionLocked) view.lockText.text = deckSystem.LockRemaining.ToString("F1");
                else if (state == SlotState.NoValidTarget) view.lockText.text = noTargetLabel;
                else view.lockText.text = string.Empty;
            }

            if (view.background != null)
                view.background.color = SlotColor(state);
        }

        if (nextCardText != null)
        {
            SkillData next = deckSystem.PeekNext();
            nextCardText.text = next != null ? next.DisplayName : emptySlotLabel;
        }
    }

    private Color SlotColor(SlotState state)
    {
        if (state == SlotState.ActionLocked) return slotLockedColor;
        if (state == SlotState.NoValidTarget) return slotNoTargetColor;
        if (state == SlotState.NotEnoughCost) return slotPoorColor;
        return slotNormalColor;
    }

    private void UpdateMarker()
    {
        if (targetMarker == null) return;

        Enemy target = enemyManager != null ? enemyManager.CurrentTarget : null;
        bool show = target != null && target.IsAlive;

        if (targetMarker.gameObject.activeSelf != show) targetMarker.gameObject.SetActive(show);
        if (!show) return;

        targetMarker.position = target.transform.position + Vector3.up * markerHeight;

        if (markerPulse > 0f)
        {
            float t = (Mathf.Sin(Time.unscaledTime * markerPulseSpeed) + 1f) * 0.5f;
            targetMarker.localScale = markerBaseScale * (1f + markerPulse * t);
        }

        Camera cam = Camera.main;
        if (cam != null) targetMarker.forward = cam.transform.forward;
    }
}
