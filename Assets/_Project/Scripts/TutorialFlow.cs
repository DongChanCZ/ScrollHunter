using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>04의 7개 학습. 일반 전투 판정은 유지하고 안내 정지·지정 입력만 예외로 처리한다.</summary>
[DefaultExecutionOrder(-200)]
public class TutorialFlow : MonoBehaviour
{
    [SerializeField] private Enemy tutorialEnemy;
    [SerializeField] private RectTransform canvasRoot;
    [SerializeField] private TMP_FontAsset font;
    [SerializeField] private RectTransform costHighlight;
    [SerializeField] private RectTransform targetHighlight;
    [SerializeField] private RectTransform enemyInfoHighlight;
    [SerializeField] private RectTransform[] slotHighlights;
    private BattleFlow flow;
    private DeckSystem deck;
    private Player player;
    private CostSystem cost;
    private CombatInfoUI information;
    private GameObject overlay;
    private RectTransform guide;
    private TMP_Text guideText;
    private TMP_Text continueHint;
    [SerializeField] private string continueHintText = "Space";
    [SerializeField, TextArea] private string enemyInfoPrompt = "해당 구역은 타겟팅 된 적의 공격에 대한 정보를 보여줍니다";
    private string guideSource;
    private bool showingEnemyInfo;
    private bool showingCombatTip;
    [SerializeField, TextArea] private string targetingPrompt = "이 표시는 현재 타겟팅한 적을 보여줍니다.\n적이 여러 명이면 좌우 방향키 또는 다른 적 클릭으로 타겟을 바꿀 수 있습니다.";
    [SerializeField, TextArea] private string interruptExplanationPrompt = "주황색 공격은 기절을 일으킵니다.\n발동 전에 차단하면 공격을 취소하고 적을 2.7초 동안 경직시킵니다.";
    [SerializeField, TextArea] private string interruptTimingPrompt = "공격 발동 직전에 차단하면\n적이 시전에 쓴 시간까지 최대한 벌 수 있습니다.\n침묵도 시전에 0.2초가 필요하니 너무 늦게 누르지 마세요.";
    [SerializeField, TextArea] private string shieldExplanationPrompt = "빨간색 공격은 차단할 수 없습니다.\n맞는 순간 방어도가 남아 있으면 피해가 절반으로 줄어듭니다.";
    [SerializeField, TextArea] private string shieldConceptPrompt = "방어도는 미리 쌓아둘 수 있으며, 여러 번 사용하면 누적됩니다.\n피해를 받으면 체력보다 방어도가 먼저 줄어듭니다.\n남은 방어도는 전투가 끝나면 사라집니다.";
    private int tooltipGuideStep;
    [SerializeField] private Vector2 tooltipGuidePosition = new Vector2(-250f, 255f);
    [SerializeField] private float tooltipGuideWidth = 900f;
    private Vector2 defaultGuidePosition;
    private float defaultGuideWidth;
    public bool WaitingForTooltip => isActiveAndEnabled && ShowingGuide && tooltipGuideStep > 0;
    [SerializeField, TextArea] private string tooltipPrompt = "Space를 누른 채 Q를 눌러 파이어볼의 설명을 확인해 보세요.\nSpace를 누르는 동안에는 스킬이 사용되지 않습니다.";
    [SerializeField, TextArea] private string tooltipReleasePrompt = "Space를 놓으면 설명이 닫힙니다.";
    private bool pendingCycleGuide;
    private int cycleGuideStep;
    [SerializeField, TextArea] private string defeatPrompt = "이제 공격스킬을 써서 적을 처치하세요";
    [SerializeField, TextArea] private string completionPrompt = "<size=36><b>튜토리얼 완료</b></size>\nHP와 포션이 회복된 상태로 다음 전투를 시작합니다.\n스킬 순서를 확인하고 전투를 준비하세요.";
    [Header("외형 (10 A37) — 비우면 단색 사각형")]
    [SerializeField] private Sprite panelSprite;
    [SerializeField] private Color panelColor = new Color(0.035f, 0.025f, 0.025f, 0.94f);
    [SerializeField] private Sprite frameSprite;
    [SerializeField] private Color frameColor = new Color(0.86f, 0.74f, 0.5f, 0.9f);
    [SerializeField] private Sprite buttonSprite;
    [SerializeField] private Color textColor = Color.white;
    [SerializeField] private float guideLineSpacing = 72f;
    [SerializeField] private float guideMinimumHeight = 260f;
    private bool showingDefeatPrompt;
    private RectTransform skipRect;
    private RectTransform[] shades = new RectTransform[4];
    private RectTransform highlight;
    private int consumedFrame = -1;
    public Enemy Enemy => tutorialEnemy;
    public bool Active { get; private set; }
    public int CompletedLessons { get; private set; }
    public bool ShowingGuide { get; private set; }
    public bool WaitingForSkill { get; private set; }
    public int Lesson { get; private set; }
    public bool BlocksInputThisFrame => isActiveAndEnabled && Time.frameCount == consumedFrame;
    public bool BlocksOtherControls => isActiveAndEnabled && (ShowingGuide || WaitingForSkill || BlocksInputThisFrame);
    private int RequiredSlot => Lesson == 3 ? 0 : Lesson == 4 ? 2 : Lesson == 5 ? 3 : -1;

    private void Awake()
    {
        flow = FindFirstObjectByType<BattleFlow>();
        deck = FindFirstObjectByType<DeckSystem>();
        player = FindFirstObjectByType<Player>();
        cost = FindFirstObjectByType<CostSystem>();
        information = FindFirstObjectByType<CombatInfoUI>();
        BuildUI();
        defaultGuidePosition = guide.anchoredPosition;
        defaultGuideWidth = guide.sizeDelta.x;
        GameText.Changed += RefreshLanguage;
    }

    private void OnEnable()
    {
        if (tutorialEnemy == null) return;
        tutorialEnemy.CastAdvanced += OnCastAdvanced;
        tutorialEnemy.AttackFired += OnAttackFired;
    }

    private void OnDisable()
    {
        if (tutorialEnemy != null)
        {
            tutorialEnemy.CastAdvanced -= OnCastAdvanced;
            tutorialEnemy.AttackFired -= OnAttackFired;
            tutorialEnemy.HoldTutorialDefeat = false;
        }
        Active = ShowingGuide = WaitingForSkill = showingEnemyInfo = showingCombatTip = pendingCycleGuide = false;
        cycleGuideStep = tooltipGuideStep = 0;
        if (overlay != null) overlay.SetActive(false);
        if (skipRect != null) skipRect.gameObject.SetActive(false);
    }

    public void Begin(int hp, bool retry)
    {
        pendingCycleGuide = false;
        cycleGuideStep = tooltipGuideStep = 0;
        if (!retry) CompletedLessons = 0;
        Active = true;
        ShowingGuide = WaitingForSkill = showingCombatTip = false;
        Lesson = CompletedLessons + 1;
        int patternIndex = Lesson == 4 ? 1 : Lesson == 5 || Lesson == 6 ? 2 : 0;
        tutorialEnemy.BeginTutorial(hp, patternIndex);
        skipRect.gameObject.SetActive(true);
        // 시작 버튼을 누른 클릭이 첫 안내까지 넘기지 않게 한다.
        consumedFrame = Time.frameCount;
        Debug.Log("[튜토리얼] " + (retry ? "재도전" : "시작") + " / 완료 학습 " + CompletedLessons + " / 적 HP " + hp, this);
        if (CompletedLessons >= 6)
        {
            HideGuide();
            if (hp == 0) Complete();
            else ResumeTime();
        }
        else if (retry || CompletedLessons < 3) ShowLesson(Lesson);
    }

    private void Update()
    {
        if (flow != null && flow.MenuBlocksInput) return;
        if (!Active && !ShowingGuide && consumedFrame == Time.frameCount - 1) flow.RefreshTutorialUI();
        if (!ShowingGuide || WaitingForSkill || WaitingForTooltip || BlocksInputThisFrame) return;
        bool click = Input.GetMouseButtonDown(0);
        if (click && Active && RectTransformUtility.RectangleContainsScreenPoint(skipRect, Input.mousePosition)) return;
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)
            || Input.GetKeyDown(KeyCode.Space) || click) AdvanceGuide();
    }

    public void TickCombat()
    {
        if (!Active) return;
        if (!player.IsAlive) { flow.BeginTutorial(true); return; }
        if (pendingCycleGuide && !deck.IsLocked)
        {
            pendingCycleGuide = false;
            cycleGuideStep = 1;
            ShowGuide("사용한 카드는 대기열 맨 뒤로 이동합니다.\n대기열의 첫 카드가 방금 사용한 칸에 들어왔습니다.", slotHighlights[0]);
            consumedFrame = Time.frameCount;
        }
        if (CompletedLessons >= 6 && tutorialEnemy.CurrentHp <= 0) Complete();
    }

    private void OnCastAdvanced()
    {
        if (!Active || pendingCycleGuide || ShowingGuide || WaitingForSkill || tutorialEnemy.CastProgress01 < 0.5f) return;
        if (CompletedLessons == 3 && tutorialEnemy.CastColor == CastColor.Orange) ShowLesson(4);
        else if (CompletedLessons == 4 && tutorialEnemy.CastColor == CastColor.Red) ShowLesson(5);
    }

    private void OnAttackFired(CastColor color)
    {
        if (Active && player.IsAlive && CompletedLessons == 5 && color == CastColor.Red) ShowLesson(6);
    }

    private string Explanation(int lesson)
    {
        switch (lesson)
        {
            case 1: return "스킬에 맞는 코스트가 모이면 스킬을 사용할 수 있습니다.";
            case 2: return "스킬은 사용 방법에 따라 즉발, 캐스팅, 채널링으로 나뉘며 그 중에서 공격 스킬은 단일기와 광역기로 나뉩니다.";
            case 3: return targetingPrompt;
            case 4: return interruptExplanationPrompt;
            case 5: return shieldExplanationPrompt;
            case 6: return "체력이 떨어졌을 때, 게임 시간이 흐르는 중 Shift 또는 물약 아이콘 좌클릭으로 물약을 사용할 수 있습니다.";
            default: return "순서 변경 버튼을 누르고 스킬을 드래그하면 해당 위치의 순번으로 배치할 수 있습니다. 저장하면 적용되고 취소하면 편집 전 순서로 돌아갑니다.";
        }
    }

    private void SetGuideText(string value) { guideSource = value; guideText.text = GameText.T(value); }
    private void RefreshLanguage()
    {
        if (guideText != null) guideText.text = GameText.T(guideSource);
        if (skipRect != null) skipRect.GetComponentInChildren<TMP_Text>(true).text = GameText.T("튜토리얼 스킵");
    }
    private void OnDestroy() { GameText.Changed -= RefreshLanguage; }

    private void ShowLesson(int lesson)
    {
        Lesson = lesson;
        ShowGuide(Explanation(lesson), lesson == 1 ? costHighlight : lesson == 3 ? targetHighlight : null);
    }

    private void ShowGuide(string text, RectTransform focus)
    {
        ShowingGuide = true;
        tooltipGuideStep = 0;
        WaitingForSkill = showingDefeatPrompt = showingEnemyInfo = showingCombatTip = false;
        Time.timeScale = 0f;
        if (information != null) information.BeginBattle();
        highlight = focus;
        SetGuideText(text);
        continueHint.gameObject.SetActive(true);
        overlay.SetActive(true);
    }

    public bool AdvanceGuide()
    {
        if (flow.MenuBlocksInput || !ShowingGuide || WaitingForSkill || WaitingForTooltip || BlocksInputThisFrame) return false;
        consumedFrame = Time.frameCount;
        if (flow.State == BattleFlowState.TutorialComplete)
        {
            ShowLesson(7);
            flow.OpenTutorialPreparation();
            return true;
        }
        if (cycleGuideStep > 0)
        {
            if (cycleGuideStep == 1)
            {
                cycleGuideStep = 2;
                var combat = FindFirstObjectByType<CombatUI>();
                ShowGuide("이 카드는 다음에 손패로 들어올 카드입니다.\n쓰지 않은 카드는 남아 있으니, 차단과 방어가 필요할 때까지 아껴두세요.",
                    combat != null && combat.NextCardIcon != null ? combat.NextCardIcon.rectTransform : null);
            }
            else { HideGuide(); ResumeTime(); }
            return true;
        }
        // 3단계 안에서 마커 → 적 정보 → Space+Q 실습 → 파이어볼 사용 순서로 안내한다.
        if (Lesson == 3 && !showingEnemyInfo)
        {
            ShowGuide(enemyInfoPrompt, enemyInfoHighlight);
            showingEnemyInfo = true;
            return true;
        }
        if (Lesson == 3)
        {
            ShowGuide(tooltipPrompt, slotHighlights[0]);
            tooltipGuideStep = 1;
            continueHint.gameObject.SetActive(false);
            return true;
        }
        if (Lesson >= 4 && Lesson <= 5)
        {
            if (!showingCombatTip)
            {
                ShowGuide(Lesson == 4 ? interruptTimingPrompt : shieldConceptPrompt, null);
                showingCombatTip = true;
                return true;
            }
            WaitingForSkill = true;
            continueHint.gameObject.SetActive(false);
            highlight = slotHighlights[RequiredSlot];
            cost.EnsureTutorialCost(deck.GetHandCard(RequiredSlot).Cost);
            SetGuideText(Lesson == 3 ? "Q 또는 아이콘을 눌러 단일 스킬인 파이어볼을 사용하세요."
                : Lesson == 4 ? "E 또는 아이콘을 눌러 차단 스킬인 침묵으로 적의 공격을 차단하세요."
                : "R 또는 아이콘을 눌러 방어 스킬인 매직아머로 방어도를 쌓아 체력을 보존하세요.");
            return true;
        }
        CompletedLessons = Mathf.Max(CompletedLessons, Lesson);
        // 포션 설명 후 다음 목표를 안내한다. 이미 처치 조건을 채웠으면 완료 안내로 간다.
        if (Lesson == 6 && !showingDefeatPrompt && tutorialEnemy.CurrentHp > 0)
        {
            showingDefeatPrompt = true;
            SetGuideText(defeatPrompt);
            return true;
        }
        if (Lesson < 3) ShowLesson(Lesson + 1);
        else
        {
            HideGuide();
            if (Active && tutorialEnemy.CurrentHp == 0) Complete();
            else if (Active) ResumeTime();
            flow.RefreshTutorialUI();
        }
        return true;
    }

    internal void ObserveTooltip(bool held, int slot)
    {
        if (!WaitingForTooltip || BlocksInputThisFrame || flow.MenuBlocksInput) return;
        if (tooltipGuideStep == 1 && held && slot == 0)
        {
            tooltipGuideStep = 2;
            SetGuideText(tooltipReleasePrompt);
            highlight = information.CardInfoRect;
        }
        else if (tooltipGuideStep == 2 && !held)
        {
            tooltipGuideStep = 0;
            showingEnemyInfo = true;
            WaitingForSkill = true;
            highlight = slotHighlights[0];
            cost.EnsureTutorialCost(deck.GetHandCard(0).Cost);
            SetGuideText("Q 또는 아이콘을 눌러 단일 스킬인 파이어볼을 사용하세요.");
            consumedFrame = Time.frameCount;
        }
    }

    public bool AllowsSlot(int slot, SkillData card)
    {
        if (!isActiveAndEnabled) return true;
        if (BlocksInputThisFrame || ShowingGuide && !WaitingForSkill) return false;
        if (!Active) return true;
        if (CompletedLessons >= 5) return true;
        return WaitingForSkill && slot == RequiredSlot && card == deck.CopyStartingDeck()[slot];
    }

    // DeckSystem에서 코스트 지불에 성공한 뒤, 순환·시전 시작 전에만 호출한다.
    public void AcceptSkill(int slot, SkillData card)
    {
        if (!Active || !WaitingForSkill || !AllowsSlot(slot, card)) return;
        CompletedLessons = Lesson;
        Debug.Log("[튜토리얼] 학습 " + Lesson + " 입력 수락 / " + card.DisplayName, this);
        HideGuide();
        pendingCycleGuide = Lesson == 3;
        ResumeTime();
    }

    private void ResumeTime() => Time.timeScale = information != null ? information.BattleSpeed : 1f;
    private void HideGuide()
    {
        ShowingGuide = WaitingForSkill = showingDefeatPrompt = showingEnemyInfo = showingCombatTip = pendingCycleGuide = false;
        cycleGuideStep = tooltipGuideStep = 0;
        if (information != null) information.BeginBattle();
        overlay.SetActive(false);
    }

    private void Complete()
    {
        tutorialEnemy.ReleaseTutorialDefeat();
        Active = false;
        skipRect.gameObject.SetActive(false);
        HideGuide();
        consumedFrame = Time.frameCount;
        flow.FinishTutorial(false);
    }

    internal void ShowCompletionGuide()
    {
        ShowGuide(completionPrompt, null);
        consumedFrame = Time.frameCount;
    }

    public void Skip()
    {
        if (flow.MenuBlocksInput || !Active) return;
        Active = false;
        tutorialEnemy.HoldTutorialDefeat = false;
        HideGuide();
        skipRect.gameObject.SetActive(false);
        consumedFrame = Time.frameCount;
        flow.FinishTutorial(true);
    }

    public void Cancel()
    {
        Active = false;
        tutorialEnemy.HoldTutorialDefeat = false;
        HideGuide();
        skipRect.gameObject.SetActive(false);
        consumedFrame = -1;
    }

    private RectTransform UiRect(string name, Transform parent, Color color)
    {
        var obj = new GameObject(name, typeof(RectTransform), typeof(Image));
        obj.transform.SetParent(parent, false);
        var image = obj.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return (RectTransform)obj.transform;
    }

    private static void Dress(RectTransform rect, Sprite sprite)
    {
        if (sprite == null) return;
        var image = rect.GetComponent<Image>();
        image.sprite = sprite;
        image.type = sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
    }

    private TMP_Text Label(Transform parent, string text, float size)
    {
        var obj = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        obj.transform.SetParent(parent, false);
        var label = obj.GetComponent<TextMeshProUGUI>();
        label.font = font;
        label.fontSize = size;
        label.color = textColor;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        label.text = GameText.T(text);
        var rect = label.rectTransform;
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(20, 12); rect.offsetMax = new Vector2(-20, -12);
        return label;
    }

    private void BuildUI()
    {
        overlay = new GameObject("TutorialOverlay", typeof(RectTransform));
        overlay.transform.SetParent(canvasRoot, false);
        var root = (RectTransform)overlay.transform;
        root.anchorMin = Vector2.zero; root.anchorMax = Vector2.one;
        root.offsetMin = root.offsetMax = Vector2.zero;
        for (int i = 0; i < shades.Length; i++) shades[i] = UiRect("Shade" + i, root, new Color(0, 0, 0, 0.85f));
        guide = UiRect("TutorialGuide", root, panelColor);
        Dress(guide, panelSprite);
        if (frameSprite != null)
        {
            RectTransform frame = UiRect("Frame", guide, frameColor);
            Dress(frame, frameSprite);
            frame.anchorMin = Vector2.zero; frame.anchorMax = Vector2.one;
            frame.offsetMin = frame.offsetMax = Vector2.zero;
        }
        guide.anchorMin = guide.anchorMax = new Vector2(0.5f, 0);
        guide.pivot = new Vector2(0.5f, 0);
        guide.anchoredPosition = new Vector2(0, 255);
        guide.sizeDelta = new Vector2(1050, 225);
        guideText = Label(guide, "", 29);
        guideText.lineSpacing = guideLineSpacing;
        guideText.rectTransform.offsetMin = new Vector2(30, 60);
        guideText.rectTransform.offsetMax = new Vector2(-30, -60);
        continueHint = Label(guide, continueHintText, 25);
        continueHint.alignment = TextAlignmentOptions.BottomRight;
        continueHint.color = new Color32(231, 203, 135, 255);
        continueHint.rectTransform.anchorMax = new Vector2(1, 0);
        continueHint.rectTransform.offsetMin = new Vector2(20, 18);
        continueHint.rectTransform.offsetMax = new Vector2(-44, 52);
        skipRect = UiRect("SkipTutorial", canvasRoot, buttonSprite != null ? Color.white : new Color(0.12f, 0.10f, 0.10f, 0.95f));
        Dress(skipRect, buttonSprite);
        skipRect.anchorMin = skipRect.anchorMax = new Vector2(1, 1);
        skipRect.pivot = new Vector2(1, 1);
        skipRect.anchoredPosition = new Vector2(-25, -25);
        skipRect.sizeDelta = new Vector2(buttonSprite != null ? 228 : 210, 55);
        skipRect.GetComponent<Image>().raycastTarget = true;
        var button = skipRect.gameObject.AddComponent<Button>();
        button.targetGraphic = skipRect.GetComponent<Image>();
        button.onClick.AddListener(Skip);
        Label(skipRect, "튜토리얼 스킵", buttonSprite != null ? 23 : 25);
        overlay.SetActive(false);
        skipRect.gameObject.SetActive(false);
    }

    private void LateUpdate()
    {
        if (!ShowingGuide) return;
        // 실습 중 오른쪽 스킬 설명을 가리지 않도록 안내는 왼쪽에 둔다.
        guide.anchoredPosition = WaitingForTooltip ? tooltipGuidePosition : defaultGuidePosition;
        guide.sizeDelta = new Vector2(WaitingForTooltip ? tooltipGuideWidth : defaultGuideWidth, guide.sizeDelta.y);
        // 위아래 여백을 같게 두어 본문을 박스 중앙에 맞추고 Space 공간을 확보한다.
        guide.sizeDelta = new Vector2(guide.sizeDelta.x, Mathf.Max(guideMinimumHeight,
            guideText.GetPreferredValues(guideText.text, guide.sizeDelta.x - 60f, Mathf.Infinity).y
            + guideText.rectTransform.offsetMin.y - guideText.rectTransform.offsetMax.y));
        // 대상 UI를 복제하거나 확대하지 않고 네 사각형으로 주변만 어둡게 한다.
        Rect bounds = canvasRoot.rect;
        Rect hole = new Rect(bounds.center, Vector2.zero);
        if (highlight != null && highlight.gameObject.activeInHierarchy)
        {
            var corners = new Vector3[4]; highlight.GetWorldCorners(corners);
            var lo = canvasRoot.InverseTransformPoint(corners[0]);
            var hi = canvasRoot.InverseTransformPoint(corners[2]);
            hole = Rect.MinMaxRect(Mathf.Clamp(lo.x - 12, bounds.xMin, bounds.xMax), Mathf.Clamp(lo.y - 12, bounds.yMin, bounds.yMax),
                Mathf.Clamp(hi.x + 12, bounds.xMin, bounds.xMax), Mathf.Clamp(hi.y + 12, bounds.yMin, bounds.yMax));
        }
        Place(shades[0], bounds.xMin, bounds.yMin, hole.xMin, bounds.yMax);
        Place(shades[1], hole.xMax, bounds.yMin, bounds.xMax, bounds.yMax);
        Place(shades[2], hole.xMin, bounds.yMin, hole.xMax, hole.yMin);
        Place(shades[3], hole.xMin, hole.yMax, hole.xMax, bounds.yMax);
    }

    private static void Place(RectTransform rect, float left, float bottom, float right, float top)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = Vector2.zero;
        rect.anchoredPosition = new Vector2(left, bottom);
        rect.sizeDelta = new Vector2(right - left, top - bottom);
    }
}
