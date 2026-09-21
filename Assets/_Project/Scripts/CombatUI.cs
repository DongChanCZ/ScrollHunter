using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// HP / 방어도 / 코스트 / 손패 4칸 / 대기열 다음 1장 / 타겟 마커 / 기절 표시 / 플레이어 시전 표시. 읽기 전용.
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

        [Tooltip("공격/방어/차단 유형 이름")]
        public TMP_Text typeText;

        [Tooltip("유형별 도형(사각·마름모·원). 색이 안 보여도 유형을 구분하기 위함")]
        public Image typeShape;

        [Tooltip("Q/W/E/R 단축키 표시(정적 텍스트). 행동 잠금·기절 중 대비를 맞추려고 참조한다")]
        public TMP_Text keyLabel;
    }

    [SerializeField] private CostSystem costSystem;
    [SerializeField] private DeckSystem deckSystem;
    [SerializeField] private EnemyManager enemyManager;
    [SerializeField] private Player player;

    [Tooltip("전투 종료(승리·패배) 감지용. 비워두면 씬에서 자동으로 찾는다")]
    [SerializeField] private CombatMetrics metrics;

    [Header("좌하단 상태")]
    [SerializeField] private TMP_Text costText;
    [SerializeField] private TMP_Text hpText;

    [Tooltip("방어도. 0일 때는 숨긴다.")]
    [SerializeField] private TMP_Text shieldText;

    [Header("기절 표시 (화면 중앙)")]
    [Tooltip("기절 중에만 켜진다. 슬롯 색만으로는 손패를 안 볼 때 놓친다. (02 §6.5.4)")]
    [SerializeField] private TMP_Text stunText;

    [Tooltip("{0}=남은 시간")]
    [SerializeField] private string stunFormat = "기절 {0:0.0}";

    [Header("손패")]
    [SerializeField] private HandSlotView[] handSlots = new HandSlotView[DeckSystem.HandSize];
    [SerializeField] private TMP_Text nextCardText;

    [Header("시전 표시 (손패 위) — 시전 시간만 쓴다. 손패 슬롯의 행동 잠금 숫자와는 다른 타이머다")]
    [Tooltip("시전 중과 취소 표시 동안에만 켜진다. 비워두면 시전 표시를 하지 않는다")]
    [SerializeField] private GameObject castRoot;

    [Tooltip("진행 바. 오른쪽 앵커를 움직여 채운다 (Image.fillAmount는 쓰지 않는다)")]
    [SerializeField] private Image castFill;

    [SerializeField] private TMP_Text castNameText;
    [SerializeField] private TMP_Text castTimeText;

    [Tooltip("{0}=시전 중인 스킬 이름")]
    [SerializeField] private string castFormat = "시전 {0}";
    [SerializeField] private string channelFormat = "채널링 {0}";

    [Tooltip("{0}=남은 시전 시간(초)")]
    [SerializeField] private string castTimeFormat = "{0:0.0}";

    [Tooltip("{0}=끊긴 스킬 이름. 기절로 취소됐을 때만 잠깐 표시한다")]
    [SerializeField] private string castCancelledFormat = "{0} 시전 취소";

    [Tooltip("취소 표시를 띄워 두는 시간(초). 기절 2초보다 짧게 두어 다음 시전과 겹치지 않게 한다")]
    [SerializeField] private float cancelNoticeSeconds = 0.8f;

    [Tooltip("진행 바 색. 적 캐스팅 3색·슬롯 상태색·카드 유형색과 겹치지 않는 색만 쓸 것")]
    [SerializeField] private Color castFillColor = new Color(0.92f, 0.94f, 0.88f, 1f);

    [SerializeField] private Color castTextColor = Color.white;

    [Tooltip("취소 표시 글자색. 진행 중과 한눈에 달라 보여야 한다")]
    [SerializeField] private Color castCancelledTextColor = new Color(0.62f, 0.64f, 0.70f, 1f);

    [Header("타겟 마커 — 화면 고정 (02 문서 §2.2)")]
    [Tooltip("Canvas 아래에 둔다. 현재 타겟 위로 이동하고, 타겟이 없으면 숨는다.")]
    [SerializeField] private RectTransform targetMarker;

    [Tooltip("기준점 높이. 적 바와 같은 값을 쓴다 (캡슐 머리 = 1.05)")]
    [SerializeField] private float markerWorldHeight = 1.05f;

    [Tooltip("적 바 위로 띄울 화면 픽셀. 바 높이(84)보다 커야 겹치지 않는다")]
    [SerializeField] private float markerScreenOffsetY = 96f;

    [Tooltip("맥동 세기. 0이면 맥동 없음")]
    [SerializeField] private float markerPulse = 0.15f;

    [SerializeField] private float markerPulseSpeed = 3f;

    [Header("슬롯 색 — 비활성 사유별로 달라야 한다")]
    [SerializeField] private Color slotNormalColor = Color.white;

    [Tooltip("행동 잠금 중")]
    [SerializeField] private Color slotLockedColor = new Color(0.45f, 0.45f, 0.45f, 1f);

    [Tooltip("기절 중. 행동 잠금(회색)과 반드시 구분되어야 한다")]
    [SerializeField] private Color slotStunnedColor = new Color(0.42f, 0.22f, 0.55f, 1f);

    [Tooltip("차단 카드인데 타겟이 캐스팅 중이 아님")]
    [SerializeField] private Color slotNoTargetColor = new Color(0.30f, 0.32f, 0.40f, 1f);

    [Tooltip("코스트 부족")]
    [SerializeField] private Color slotPoorColor = new Color(0.62f, 0.62f, 0.62f, 1f);

    [Header("글자 색")]
    [SerializeField] private Color costNormalColor = new Color(0.1f, 0.3f, 0.8f);
    [SerializeField] private Color costShortColor = new Color(0.9f, 0.15f, 0.15f);

    [Header("카드 유형 표시 — 적 캐스팅 초록·주황·빨강, 슬롯 상태색(회색·보라)과 겹치지 않는 색만 쓸 것")]
    [SerializeField] private string dealTypeLabel = "공격";
    [SerializeField] private string shieldTypeLabel = "방어";
    [SerializeField] private string interruptTypeLabel = "차단";

    [SerializeField] private Color dealTypeColor = new Color(0.30f, 0.45f, 0.90f);
    [SerializeField] private Color shieldTypeColor = new Color(0.15f, 0.65f, 0.80f);
    [SerializeField] private Color interruptTypeColor = new Color(0.85f, 0.25f, 0.60f);

    [Tooltip("방어 유형 도형(원). 비워두면 사각형으로 대체된다")]
    [SerializeField] private Sprite shieldShapeSprite;

    [Header("행동 잠금·기절 — 배경(회색·보라)은 그대로 두고 글자만 밝혀서 대비를 높인다")]
    [Tooltip("행동 잠금·기절 중 이름·유형·코스트·단축키·남은 시간에 공통으로 쓰는 고대비 글자색. 코스트 부족의 빨강과 헷갈리지 않아야 한다")]
    [SerializeField] private Color lockedOrStunnedTextColor = Color.white;

    [Header("표시 문자열")]
    [SerializeField] private string emptySlotLabel = "-";

    [Tooltip("타겟이 캐스팅 중이 아닐 때 차단 슬롯에 표시")]
    [SerializeField] private string noTargetLabel = "잠김";
    [SerializeField] private string invalidSkillLabel = "설정 필요";

    [Tooltip("{0}=현재 HP, {1}=최대 HP")]
    [SerializeField] private string hpFormat = "{0} / {1}";

    [Tooltip("{0}=방어도")]
    [SerializeField] private string shieldFormat = "방어도 {0}";

    private Vector3 markerBaseScale = Vector3.one;

    // 행동 잠금·기절이 아닐 때 되돌릴 원래 글자색. 4슬롯 스타일이 같으므로 하나만 캐싱한다.
    private Color defaultNameColor = Color.black;
    private Color defaultKeyLabelColor = Color.black;
    private Color defaultLockTextColor = Color.black;

    private void Awake()
    {
        if (costSystem == null) costSystem = FindFirstObjectByType<CostSystem>();
        if (deckSystem == null) deckSystem = FindFirstObjectByType<DeckSystem>();
        if (enemyManager == null) enemyManager = FindFirstObjectByType<EnemyManager>();
        if (player == null) player = FindFirstObjectByType<Player>();
        if (metrics == null) metrics = FindFirstObjectByType<CombatMetrics>();

        if (targetMarker != null) markerBaseScale = targetMarker.localScale;

        CacheDefaultTextColors();
    }

    private void CacheDefaultTextColors()
    {
        for (int i = 0; i < handSlots.Length; i++)
        {
            HandSlotView view = handSlots[i];
            if (view == null) continue;

            if (view.nameText != null) defaultNameColor = view.nameText.color;
            if (view.keyLabel != null) defaultKeyLabelColor = view.keyLabel.color;
            if (view.lockText != null) defaultLockTextColor = view.lockText.color;
            break; // 4슬롯 모두 같은 스타일이라 첫 슬롯만 보면 된다.
        }
    }

    // 같은 프레임의 입력 결과까지 반영하려고 LateUpdate에서 갱신한다.
    private void LateUpdate()
    {
        UpdateStatus();
        UpdateHand();
        UpdateCast();
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

        if (stunText != null)
        {
            bool stunned = player.IsStunned;
            stunText.enabled = stunned;
            if (stunned) stunText.text = string.Format(stunFormat, player.StunRemaining);
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

            // 행동 잠금·기절은 배경(회색·보라)을 그대로 두는 대신 글자를 전부 밝혀서 대비를 확보한다.
            bool highContrast = state == SlotState.ActionLocked || state == SlotState.Stunned;

            if (view.nameText != null)
            {
                view.nameText.text = card != null ? card.DisplayName : emptySlotLabel;
                view.nameText.color = highContrast ? lockedOrStunnedTextColor : defaultNameColor;
            }

            if (view.costText != null)
            {
                view.costText.text = card != null ? card.Cost.ToString("0.#") : string.Empty;
                // 코스트 부족은 숫자를 빨갛게 해서 다른 비활성 사유와 구분한다.
                view.costText.color = highContrast ? lockedOrStunnedTextColor
                    : (state == SlotState.NotEnoughCost ? costShortColor : costNormalColor);
            }

            if (view.lockText != null)
            {
                view.lockText.text = LockLabel(state);
                // 남은 시간도 밝게 — 코스트 부족의 빨간 숫자와 헷갈리지 않게 한다.
                view.lockText.color = highContrast ? lockedOrStunnedTextColor : defaultLockTextColor;
            }

            if (view.keyLabel != null)
                view.keyLabel.color = highContrast ? lockedOrStunnedTextColor : defaultKeyLabelColor;

            if (view.background != null)
                view.background.color = SlotColor(state);

            // 유형 표시는 카드 이름표와 같은 이유로 매 프레임 카드 기준으로 다시 그린다 — 순환하면 즉시 바뀐다.
            if (view.typeText != null)
            {
                view.typeText.text = card != null ? TypeLabel(card.Category) : string.Empty;
                view.typeText.color = card == null ? defaultNameColor
                    : (highContrast ? lockedOrStunnedTextColor : TypeColor(card.Category));
            }

            if (view.typeShape != null)
            {
                view.typeShape.enabled = card != null;
                if (card != null)
                {
                    view.typeShape.color = highContrast ? lockedOrStunnedTextColor : TypeColor(card.Category);
                    // 방어만 원(Knob), 나머지는 사각형 — 차단은 45도 회전한 마름모로 사각형(공격)과 구분한다.
                    view.typeShape.sprite = card.Category == SkillCategory.Shield ? shieldShapeSprite : null;
                    float rotation = card.Category == SkillCategory.Interrupt ? 45f : 0f;
                    view.typeShape.rectTransform.localRotation = Quaternion.Euler(0f, 0f, rotation);
                }
            }
        }

        if (nextCardText != null)
        {
            SkillData next = deckSystem.PeekNext();
            nextCardText.text = next != null ? next.DisplayName : emptySlotLabel;
        }
    }

    /// <summary>
    /// 지금 시전 중인 카드를 손패 위에 보여준다.
    ///
    /// 손패 슬롯은 입력 수락 즉시 다음 카드로 바뀌므로(10 A14) 슬롯이 아니라
    /// <see cref="DeckSystem.CastingCard"/>를 읽는다. 진행률 분모도 그 카드의 캐스팅 시간이다 —
    /// 행동 잠금 MAX(캐스팅, 최소 GCD)로 나누면 0.2초짜리 카드가 절반만 찬 것처럼 보인다.
    /// 남은 시간도 <see cref="DeckSystem.CastRemaining"/>만 쓴다. 손패 슬롯의 잠금 숫자와는 다른 타이머다.
    ///
    /// 시간을 여기서 따로 적분하지 않는 이유: 캐스팅 진행은 DeckSystem이 Time.deltaTime으로 돌리므로
    /// 일시정지 중 정지(02 E13)와 2배속 비례(E14)가 저절로 지켜진다.
    /// </summary>
    private void UpdateCast()
    {
        if (castRoot == null) return;

        // 전투가 끝나면 남기지 않는다. 특히 패배는 timeScale이 0이 되어 시전 타이머가 멈추므로
        // 그대로 두면 남은 시간을 띄운 채 영구히 박힌다.
        bool combatOver = (metrics != null && metrics.Ended)
                          || (enemyManager != null && enemyManager.CombatEnded);

        SkillData casting = !combatOver && deckSystem != null ? deckSystem.CastingCard : null;
        if (casting != null)
        {
            ShowCast(casting, deckSystem.CastRemaining);
            return;
        }

        // 기절로 끊긴 직후에만 잠깐 남긴다. 정상 완료는 조용히 끄기만 한다 —
        // 완료가 곧 성공은 아니기 때문이다 (10 T10 빨강 대상 차단은 완료지만 실패).
        bool showCancelled = !combatOver
                             && deckSystem != null
                             && deckSystem.LastCastCancelled
                             && deckSystem.LastCastCard != null
                             && Time.time - deckSystem.LastCastEndTime < cancelNoticeSeconds;

        if (showCancelled)
        {
            ShowCastCancelled(deckSystem.LastCastCard);
            return;
        }

        if (castRoot.activeSelf) castRoot.SetActive(false);
    }

    private void ShowCast(SkillData card, float remaining)
    {
        if (!castRoot.activeSelf) castRoot.SetActive(true);

        float total = card.CastTime;
        SetFill(castFill, total > 0f ? 1f - remaining / total : 1f);
        if (castFill != null) castFill.color = castFillColor;

        if (castNameText != null)
        {
            castNameText.text = string.Format(card.IsChanneling ? channelFormat : castFormat, card.DisplayName);
            castNameText.color = castTextColor;
        }

        if (castTimeText != null)
        {
            castTimeText.text = string.Format(castTimeFormat, remaining);
            castTimeText.color = castTextColor;
        }
    }

    /// <summary>기절로 끊긴 표시. 바를 비워서 진행 중과 한눈에 구분되게 한다.</summary>
    private void ShowCastCancelled(SkillData card)
    {
        if (!castRoot.activeSelf) castRoot.SetActive(true);

        SetFill(castFill, 0f);

        if (castNameText != null)
        {
            castNameText.text = string.Format(castCancelledFormat, card.DisplayName);
            castNameText.color = castCancelledTextColor;
        }

        if (castTimeText != null) castTimeText.text = string.Empty;
    }

    /// <summary>
    /// 바를 t(0~1)만큼 채운다. Image.fillAmount를 쓰지 않는 이유는 Enemy 쪽 바와 같다 —
    /// 스프라이트가 있어야 동작하는데 스프라이트를 쓰면 모서리가 둥글어진다.
    /// Enemy.SetFill은 private이라 호출할 수 없어 같은 방식을 여기에 둔다.
    /// </summary>
    private static void SetFill(Image fill, float t)
    {
        if (fill == null) return;

        Vector2 max = fill.rectTransform.anchorMax;
        max.x = Mathf.Clamp01(t);
        fill.rectTransform.anchorMax = max;
    }

    private string LockLabel(SlotState state)
    {
        if (state == SlotState.Stunned)
            return player != null ? player.StunRemaining.ToString("F1") : string.Empty;
        if (state == SlotState.ActionLocked) return deckSystem.LockRemaining.ToString("F1");
        if (state == SlotState.NoValidTarget) return noTargetLabel;
        if (state == SlotState.InvalidConfiguration) return invalidSkillLabel;
        return string.Empty;
    }

    private string TypeLabel(SkillCategory category)
    {
        if (category == SkillCategory.Shield) return shieldTypeLabel;
        if (category == SkillCategory.Interrupt) return interruptTypeLabel;
        return dealTypeLabel;
    }

    private Color TypeColor(SkillCategory category)
    {
        if (category == SkillCategory.Shield) return shieldTypeColor;
        if (category == SkillCategory.Interrupt) return interruptTypeColor;
        return dealTypeColor;
    }

    private Color SlotColor(SlotState state)
    {
        if (state == SlotState.Stunned) return slotStunnedColor;
        if (state == SlotState.ActionLocked) return slotLockedColor;
        if (state == SlotState.NoValidTarget || state == SlotState.InvalidConfiguration) return slotNoTargetColor;
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

        Camera cam = Camera.main;
        if (cam == null) return;

        // 적 바와 같은 기준점을 써서 화면 좌표로 옮긴다. 바 위에 얹히도록 픽셀만큼 더 올린다.
        Vector3 head = target.transform.position + Vector3.up * markerWorldHeight;
        Vector3 screen = cam.WorldToScreenPoint(head);
        screen.y += markerScreenOffsetY;
        targetMarker.position = screen;

        if (markerPulse > 0f)
        {
            float t = (Mathf.Sin(Time.unscaledTime * markerPulseSpeed) + 1f) * 0.5f;
            targetMarker.localScale = markerBaseScale * (1f + markerPulse * t);
        }
    }
}
