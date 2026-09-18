using TMPro;
using UnityEngine;

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
    [SerializeField] private GameObject enemyPanel;
    [SerializeField] private TMP_Text enemyText;
    [SerializeField] private TMP_Text hintText;

    [Header("표시 문자열")]
    [SerializeField] private string pauseText = "일시정지";
    [SerializeField] private string resumeText = "재개";
    [SerializeField] private string endedText = "전투 종료";
    [SerializeField] private string hint = "카드에 마우스: 정보 / 우클릭: 정지·재개";
    [SerializeField] private string pausedHint = "일시정지 — 정보 확인만 가능 / 우클릭 또는 재개";
    [SerializeField] private string cardFormat = "{0}\n{1} · {2}\n코스트 {3:0.#} / 시전 {4:0.##}초\n{5}";
    [SerializeField] private string damageFormat = "기본 피해 {0} × {1}회\n방어력 적용 전 피해";
    [SerializeField] private string shieldFormat = "방어도 +{0}";
    [SerializeField] private string interruptFormat = "캐스팅 중인 대상에게 사용\n효과 적용 시 초록·주황이면 차단\n성공 경직 {0:0.##}초\n빨강·적 선발동: 실패, 코스트·카드 소모";
    [SerializeField] private string enemyFormat = "{0}\n{1}\n기본 피해 {2} (방어력 적용 전)\n시전 {3:0.##}초 / {4}\n{5}";
    [SerializeField] private string stunFormat = "기절 {0:0.##}초";
    [SerializeField] private string restingFormat = "{0}\n현재 시전 중인 공격 없음";
    [SerializeField] private string dealLabel = "공격";
    [SerializeField] private string shieldLabel = "방어";
    [SerializeField] private string interruptLabel = "차단";
    [SerializeField] private string singleLabel = "적 1체";
    [SerializeField] private string allLabel = "적 전체";
    [SerializeField] private string selfLabel = "자신";
    [SerializeField] private string canInterruptLabel = "차단 가능";
    [SerializeField] private string cannotInterruptLabel = "차단 불가";
    [SerializeField] private string noEffectLabel = "추가 효과 없음";

    private int selectedSlot = -1;
    private float resumeScale = 1f;
    public bool IsInfoPaused { get; private set; }
    public bool BattleEnded => (metrics != null && metrics.Ended)
        || (enemies != null && enemies.CombatEnded) || (player != null && !player.IsAlive);

    private void Awake()
    {
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
        else if (Input.GetMouseButtonDown(0) && hovered >= 0 && deck != null) deck.TryUseSlot(hovered);
    }

    public void InspectSlot(int slot)
    {
        if (BattleEnded || deck == null || deck.GetHandCard(slot) == null) return;
        selectedSlot = slot;
        Pause();
    }

    public void TogglePause() { if (IsInfoPaused) Resume(); else Pause(); }
    private void Pause()
    {
        // Do not claim a pause owned by defeat or another system.
        if (BattleEnded || IsInfoPaused || Time.timeScale <= 0f) return;
        resumeScale = Time.timeScale;
        IsInfoPaused = true;
        Time.timeScale = 0f;
    }

    public void Resume()
    {
        if (!IsInfoPaused || BattleEnded) return;
        IsInfoPaused = false;
        selectedSlot = -1;
        Time.timeScale = resumeScale;
    }

    private void LateUpdate()
    {
        bool ended = BattleEnded;
        pauseLabel.text = ended ? endedText : IsInfoPaused ? resumeText : pauseText;
        hintText.text = ended ? endedText : IsInfoPaused ? pausedHint : hint;
        SkillData card = !ended && selectedSlot >= 0 ? deck.GetHandCard(selectedSlot) : null;
        cardPanel.SetActive(card != null);
        if (card != null) cardText.text = DescribeCard(card);
        Enemy target = !ended && enemies != null ? enemies.CurrentTarget : null;
        enemyPanel.SetActive(target != null && target.IsAlive);
        if (target != null && target.IsAlive) enemyText.text = DescribeEnemy(target);
    }

    public string DescribeCard(SkillData card)
    {
        string type = dealLabel;
        string target = card.IsAreaOfEffect ? allLabel : singleLabel;
        string effect = string.Format(damageFormat, card.Damage, card.HitCount);
        if (card.Category == SkillCategory.Shield)
        { type = shieldLabel; target = selfLabel; effect = string.Format(shieldFormat, card.ShieldAmount); }
        else if (card.Category == SkillCategory.Interrupt)
        { type = interruptLabel; target = singleLabel; effect = string.Format(interruptFormat, deck.InterruptStagger); }
        return string.Format(cardFormat, card.DisplayName, type, target, card.Cost, card.CastTime, effect);
    }

    public string DescribeEnemy(Enemy target)
    {
        string name = target.Data.DisplayName;
        if (!target.IsCasting || target.CurrentAttack == null) return string.Format(restingFormat, name);
        EnemyAttack attack = target.CurrentAttack;
        return string.Format(enemyFormat, name, attack.SkillName, attack.Damage, attack.CastTime,
            attack.CastColor == CastColor.Red ? cannotInterruptLabel : canInterruptLabel,
            attack.StunSeconds > 0f ? string.Format(stunFormat, attack.StunSeconds) : noEffectLabel);
    }

    private void OnDisable()
    {
        if (IsInfoPaused && !BattleEnded) Time.timeScale = resumeScale;
        IsInfoPaused = false;
    }
}
