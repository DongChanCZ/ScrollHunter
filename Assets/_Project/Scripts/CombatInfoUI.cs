using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Information only: card and enemy numbers always come from live combat data.
[DefaultExecutionOrder(-100)]
public class CombatInfoUI : MonoBehaviour
{
    [SerializeField] private DeckSystem deck;
    [SerializeField] private EnemyManager enemies;
    [SerializeField] private Player player;
    [SerializeField] private CombatMetrics metrics;
    [SerializeField] private RectTransform[] slots;
    [SerializeField] private RectTransform pauseButton;
    [SerializeField] private TMP_Text pauseLabel;
    [SerializeField] private GameObject cardPanel;
    [SerializeField] private TMP_Text cardText;
    [Tooltip("정보창 스킬 아이콘. 비워두면 표시하지 않는다")]
    [SerializeField] private Image cardIcon;
    [SerializeField] private GameObject enemyPanel;
    [SerializeField] private TMP_Text enemyText;
    [SerializeField] private TMP_Text hintText;

    [Header("표시 문자열")]
    [SerializeField] private string pauseText = "일시정지";
    [SerializeField] private string resumeText = "재개";
    [SerializeField] private string endedText = "전투 종료";
    [SerializeField] private string hint = "카드에 마우스: 정보 / 우클릭: 정지·재개";
    [SerializeField] private string pausedHint = "일시정지 — 정보 확인만 가능 / 우클릭 또는 재개";
    // 위계: 이름(145%·굵게) → 주요 정보(항목명 흐리게·수치 굵게) → 간격 → 상세 설명.
    // 색 대신 투명도로 흐리게 해 밝은 양피지·어두운 창·보상 카드 어디서든 같은 방식으로 읽힌다.
    [Tooltip("{0} 이름, {1} 주요 정보, {2} 간격, {3} 상세 설명")]
    [SerializeField] private string cardFormat = "<size=145%><b>{0}</b></size>\n{1}{2}\n{3}";
    [Tooltip("주요 정보. 편성 카드는 이 줄만 따로 표시한다. {0} 분류, {1} 대상, {2} 코스트, {3} 시간")]
    [SerializeField] private string cardInfoFormat = "<b>{0}</b><alpha=#CC> · <alpha=#FF>{1}\n<alpha=#CC>코스트 <alpha=#FF><b>{2:0.#}</b><alpha=#CC>   ·   시전 <alpha=#FF><b>{3:0.##}초</b>";
    [SerializeField] private string channelInfoFormat = "<b>{0}</b><alpha=#CC> · <alpha=#FF>{1}\n<alpha=#CC>코스트 <alpha=#FF><b>{2:0.#}</b><alpha=#CC>   ·   채널링 <alpha=#FF><b>{3:0.##}초</b>";
    [Tooltip("이름·주요 정보와 상세 설명 사이 간격")]
    [SerializeField] private string sectionBreak = "\n<size=40%> </size>";
    [SerializeField] private string channelUnconfiguredLabel = "채널링 효과 연결 필요";
    [SerializeField] private string damageFormat = "기본 피해 <b>{0} × {1}회</b>\n<alpha=#CC>방어력 적용 전 피해<alpha=#FF>";
    [SerializeField] private string shieldFormat = "방어도 <b>+{0}</b>";
    [SerializeField] private string interruptFormat = "캐스팅 중인 대상에게 사용\n효과 적용 때도 초록·주황\n시전 중이면 성공\n성공 경직 <b>{0:0.##}초</b>\n빨강·적 선발동: 실패, 코스트·카드 소모";
    [SerializeField] private string damagingInterruptFormat = "기본 피해 <b>{0} × {1}회</b>\n피해 후 생존 대상에게 차단 판정\n초록·주황: 성공 경직 <b>{2:0.##}초</b>\n빨강·시전 종료: 피해만 적용\n입력은 캐스팅 중인 대상에게만 가능";
    [SerializeField] private string enemyFormat = "<size=145%><b>{0}</b></size>\n<b>{1}</b>\n<alpha=#CC>기본 피해 <alpha=#FF><b>{2}</b><alpha=#CC> (방어력 적용 전)\n시전 <alpha=#FF><b>{3:0.##}초</b><alpha=#CC> · <alpha=#FF><b>{4}</b>{6}\n{5}";
    [SerializeField] private string stunFormat = "기절 <b>{0:0.##}초</b>";
    [SerializeField] private string restingFormat = "<size=145%><b>{0}</b></size>\n<alpha=#CC>현재 시전 중인 공격 없음";
    [SerializeField] private string dealLabel = "공격";
    [SerializeField] private string shieldLabel = "방어";
    [SerializeField] private string interruptLabel = "차단";
    [SerializeField] private string singleLabel = "적 1체";
    [SerializeField] private string allLabel = "적 전체";
    [SerializeField] private string selfLabel = "자신";
    [SerializeField] private string canInterruptLabel = "차단 가능";
    [SerializeField] private string cannotInterruptLabel = "차단 불가";
    [SerializeField] private string noEffectLabel = "추가 효과 없음";
    [SerializeField] private string redShieldEffectFormat = "피격 직전 방어도가 있으면 피해 <b>{0:0.#}%</b> 감소 후 흡수";

    [Header("설명창 높이")]
    [Tooltip("설명 글 길이에 맞춰 창 높이를 조절한다. 글이 짧아도 이보다 작아지지 않는다")]
    [SerializeField] private float cardPanelMinHeight = 260f;
    [SerializeField] private float enemyPanelMinHeight = 120f;
    [Tooltip("손패·시전 표시를 가리지 않도록 하는 최대 높이")]
    [SerializeField] private float panelMaxHeight = 640f;

    [Header("전투 배속")]
    [SerializeField] private RectTransform speedButton;
    [SerializeField] private TMP_Text speedLabel;
    [SerializeField] private float slowSpeed = 0.5f;
    [SerializeField] private float normalSpeed = 1f;
    [SerializeField] private string speedFormat = "속도 {0:0.#}×";

    public float BattleSpeed => resumeScale;
    private int selectedSlot = -1;
    private TutorialFlow tutorial;
    private BattleFlow flow;
    private float resumeScale = 1f;
    public bool IsInfoPaused { get; private set; }
    public bool BattleEnded => (metrics != null && metrics.Ended)
        || (enemies != null && enemies.CombatEnded) || (player != null && !player.IsAlive);

    private void Awake()
    {
        tutorial = FindFirstObjectByType<TutorialFlow>();
        flow = FindFirstObjectByType<BattleFlow>();
        if (deck == null) deck = FindFirstObjectByType<DeckSystem>();
        if (enemies == null) enemies = FindFirstObjectByType<EnemyManager>();
        if (player == null) player = FindFirstObjectByType<Player>();
        if (metrics == null) metrics = FindFirstObjectByType<CombatMetrics>();
    }

    private bool Contains(RectTransform rect)
    {
        if (rect == null) return false;
        Canvas canvas = rect.GetComponentInParent<Canvas>();
        Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        return RectTransformUtility.RectangleContainsScreenPoint(rect, Input.mousePosition, camera);
    }

    private void Update()
    {
        if (flow != null && flow.IsCountingDown) return;
        if (tutorial != null && tutorial.BlocksOtherControls)
        {
            selectedSlot = -1;
            if (tutorial.WaitingForSkill && !tutorial.BlocksInputThisFrame && Input.GetMouseButtonDown(0))
                for (int i = 0; i < slots.Length; i++) if (Contains(slots[i])) { deck.TryUseSlot(i); break; }
            return;
        }
        if (BattleEnded) { IsInfoPaused = false; Time.timeScale = 0f; return; }
        int hovered = -1;
        for (int i = 0; slots != null && i < slots.Length; i++)
            if (Contains(slots[i])) { hovered = i; break; }
        if (hovered >= 0) selectedSlot = hovered;
        else if (!IsInfoPaused) selectedSlot = -1;

        if (Input.GetMouseButtonDown(1))
        {
            if (IsInfoPaused) Resume();
            else if (hovered >= 0) InspectSlot(hovered);
        }
        else if (Input.GetMouseButtonDown(0) && Contains(pauseButton)) TogglePause();
        else if (Input.GetMouseButtonDown(0) && Contains(speedButton)) ToggleSpeed();
        else if (Input.GetMouseButtonDown(0) && hovered >= 0 && deck != null) deck.TryUseSlot(hovered);
    }

    public void BeginBattle()
    {
        IsInfoPaused = false;
        selectedSlot = -1;
        if (cardPanel != null) cardPanel.SetActive(false);
    }

    public void InspectSlot(int slot)
    {
        if (BattleEnded || (flow != null && flow.IsCountingDown) || deck == null || deck.GetHandCard(slot) == null) return;
        selectedSlot = slot;
        Pause();
    }

    public void ToggleSpeed()
    {
        if (BattleEnded || (flow != null && flow.IsCountingDown) || (tutorial != null && tutorial.BlocksOtherControls)) return;
        resumeScale = Mathf.Approximately(resumeScale, normalSpeed) ? slowSpeed : normalSpeed;
        // 정보 확인 정지를 해제하지 않고 재개할 배속만 바꾼다.
        if (!IsInfoPaused && Time.timeScale > 0f) Time.timeScale = resumeScale;
        if (metrics != null) metrics.RecordBattleSpeed(resumeScale);
    }

    public void TogglePause() { if (IsInfoPaused) Resume(); else Pause(); }
    private void Pause()
    {
        // Do not claim a pause owned by defeat or another system.
        if (BattleEnded || IsInfoPaused || Time.timeScale <= 0f || (tutorial != null && tutorial.BlocksOtherControls)) return;
        resumeScale = Time.timeScale;
        IsInfoPaused = true;
        Time.timeScale = 0f;
    }

    public void Resume()
    {
        if (!IsInfoPaused || BattleEnded || (flow != null && flow.IsCountingDown) || (tutorial != null && tutorial.BlocksOtherControls)) return;
        IsInfoPaused = false;
        selectedSlot = -1;
        Time.timeScale = resumeScale;
    }

    private void LateUpdate()
    {
        bool ended = BattleEnded;
        if (speedLabel != null) speedLabel.text = string.Format(speedFormat, BattleSpeed);
        pauseLabel.text = ended ? endedText : IsInfoPaused ? resumeText : pauseText;
        hintText.text = ended ? endedText : IsInfoPaused ? pausedHint : hint;
        SkillData card = !ended && selectedSlot >= 0 ? deck.GetHandCard(selectedSlot) : null;
        cardPanel.SetActive(card != null);
        if (card != null) { cardText.text = DescribeCard(card); FitHeight(cardPanel, cardText, cardPanelMinHeight); }
        ShowIcon(cardIcon, card);
        Enemy target = !ended && enemies != null ? enemies.CurrentTarget : null;
        enemyPanel.SetActive(target != null && target.IsAlive);
        if (target != null && target.IsAlive) { enemyText.text = DescribeEnemy(target); FitHeight(enemyPanel, enemyText, enemyPanelMinHeight); }
    }

    /// <summary>글 높이에 맞춰 창 높이를 조절한다. 글 영역은 창에 늘어나게 배치돼 있어 여백은 그대로 유지된다.</summary>
    private void FitHeight(GameObject panel, TMP_Text text, float minHeight)
    {
        var panelRect = panel.transform as RectTransform;
        RectTransform textRect = text.rectTransform;
        float chrome = panelRect.rect.height - textRect.rect.height;
        float needed = text.GetPreferredValues(text.text, textRect.rect.width, 0f).y + chrome;
        panelRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Clamp(needed, minHeight, panelMaxHeight));
    }

    /// <summary>카드 아이콘 표시. 카드나 아이콘이 없으면 테두리 자식까지 함께 숨긴다 (보상 화면과 공용).</summary>
    public static void ShowIcon(Image image, SkillData card)
    {
        if (image == null) return;
        image.sprite = card != null ? card.Icon : null;
        bool show = image.sprite != null;
        if (image.gameObject.activeSelf != show) image.gameObject.SetActive(show);
    }

    /// <summary>정보창·보상 후보용 전체 설명: 이름 → 주요 정보 → 상세 설명.</summary>
    public string DescribeCard(SkillData card)
        => string.Format(cardFormat, card.DisplayName, DescribeCardInfo(card), sectionBreak, DescribeCardEffect(card));

    /// <summary>분류·대상·코스트·시전(채널링) 시간. 편성 카드는 이 영역을 따로 표시한다.</summary>
    public string DescribeCardInfo(SkillData card)
    {
        string type = dealLabel;
        string target = card.IsAreaOfEffect ? allLabel : singleLabel;
        if (card.Category == SkillCategory.Shield) { type = shieldLabel; target = selfLabel; }
        else if (card.Category == SkillCategory.Interrupt) { type = interruptLabel; target = singleLabel; }
        return string.Format(card.IsChanneling ? channelInfoFormat : cardInfoFormat, type, target, card.Cost, card.CastTime);
    }

    /// <summary>효과·조건 등 상세 설명. 현재 능력치 보정(방어도·차단 경직)을 반영한다.</summary>
    public string DescribeCardEffect(SkillData card)
    {
        if (card.IsChanneling) return card.ChannelEffect != null ? card.ChannelEffect.Describe(card) : channelUnconfiguredLabel;
        if (card.Category == SkillCategory.Shield)
            return string.Format(shieldFormat, player != null ? player.GetShieldAmount(card.ShieldAmount) : card.ShieldAmount);
        if (card.Category == SkillCategory.Interrupt)
            return card.Damage > 0
                ? string.Format(damagingInterruptFormat, card.Damage, card.HitCount, deck.InterruptStagger)
                : string.Format(interruptFormat, deck.InterruptStagger);
        return string.Format(damageFormat, card.Damage, card.HitCount);
    }

    [SerializeField] private string phaseTransitionFormat = "<size=145%><b>{0}</b></size>\n<b>{1}페이즈 전환</b>\n무적 <b>{2:0.0}초</b> · 새 스킬 사용 불가";
    public string DescribeEnemy(Enemy target)
    {
        string name = target.Data.DisplayName;
        if (target.IsPhaseTransitioning) return string.Format(phaseTransitionFormat, name, target.PhaseNumber, target.PhaseTransitionRemaining);
        if (!target.IsCasting || target.CurrentAttack == null) return string.Format(restingFormat, name);
        EnemyAttack attack = target.CurrentAttack;
        string effect = attack.StunSeconds > 0f ? string.Format(stunFormat, attack.StunSeconds) : noEffectLabel;
        if (attack.CastColor == CastColor.Red && player != null)
        {
            string reduction = string.Format(redShieldEffectFormat, (1f - player.RedShieldDamageMultiplier) * 100f);
            effect = attack.StunSeconds > 0f ? effect + "\n" + reduction : reduction;
        }
        return string.Format(enemyFormat, name, attack.SkillName, attack.Damage, target.CurrentCastTime,
            attack.CastColor == CastColor.Red ? cannotInterruptLabel : canInterruptLabel,
            effect, sectionBreak);
    }

    private void OnDisable()
    {
        if (IsInfoPaused && !BattleEnded) Time.timeScale = resumeScale;
        IsInfoPaused = false;
    }
}
