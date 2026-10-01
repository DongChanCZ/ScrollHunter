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
    [SerializeField, TextArea] private string defeatPrompt = "이제 공격스킬을 써서 적을 처치하세요";
    [SerializeField, TextArea] private string completionPrompt = "<size=36><b>튜토리얼 완료</b></size>\nHP와 포션이 회복된 상태로 다음 전투를 시작합니다.\n스킬 순서를 확인하고 전투를 준비하세요.";
    [Header("외형 (10 A37) — 비우면 단색 사각형")]
    [SerializeField] private Sprite panelSprite;
    [SerializeField] private Color panelColor = new Color(0.035f, 0.025f, 0.025f, 0.94f);
    [SerializeField] private Sprite frameSprite;
    [SerializeField] private Color frameColor = new Color(0.86f, 0.74f, 0.5f, 0.9f);
    [SerializeField] private Sprite buttonSprite;
    [SerializeField] private Color textColor = Color.white;
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
        Active = ShowingGuide = WaitingForSkill = false;
        if (overlay != null) overlay.SetActive(false);
        if (skipRect != null) skipRect.gameObject.SetActive(false);
    }

    public void Begin(int hp, bool retry)
    {
        if (!retry) CompletedLessons = 0;
        Active = true;
        ShowingGuide = WaitingForSkill = false;
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
        if (!Active && !ShowingGuide && consumedFrame == Time.frameCount - 1) flow.RefreshTutorialUI();
        if (!ShowingGuide || WaitingForSkill || BlocksInputThisFrame) return;
        bool click = Input.GetMouseButtonDown(0);
        if (click && Active && RectTransformUtility.RectangleContainsScreenPoint(skipRect, Input.mousePosition)) return;
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)
            || Input.GetKeyDown(KeyCode.Space) || click) AdvanceGuide();
    }

    public void TickCombat()
    {
        if (!Active) return;
        if (!player.IsAlive) { flow.BeginTutorial(true); return; }
        if (CompletedLessons >= 6 && tutorialEnemy.CurrentHp <= 0) Complete();
    }

    private void OnCastAdvanced()
    {
        if (!Active || ShowingGuide || WaitingForSkill || tutorialEnemy.CastProgress01 < 0.5f) return;
        if (CompletedLessons == 3 && tutorialEnemy.CastColor == CastColor.Orange) ShowLesson(4);
        else if (CompletedLessons == 4 && tutorialEnemy.CastColor == CastColor.Red) ShowLesson(5);
    }

    private void OnAttackFired(CastColor color)
    {
        if (Active && player.IsAlive && CompletedLessons == 5 && color == CastColor.Red) ShowLesson(6);
    }

    private static string Explanation(int lesson)
    {
        switch (lesson)
        {
            case 1: return "스킬에 맞는 코스트가 모이면 스킬을 사용할 수 있습니다.";
            case 2: return "스킬은 사용 방법에 따라 즉발, 캐스팅, 채널링으로 나뉘며 그 중에서 공격 스킬은 단일기와 광역기로 나뉩니다.";
            case 3: return "이 표시는 어떤 적을 타겟팅하고 있는지 보여줍니다.";
            case 4: return "적의 주황색 게이지의 공격은 상태이상을 일으키는 공격입니다.";
            case 5: return "적의 빨간색 게이지 공격은 차단이 불가능하고 방어도가 없을 시 치명적인 피해를 입힙니다.";
            case 6: return "체력이 떨어졌을 때, 게임 시간이 흐르는 중 Shift를 눌러 물약을 사용할 수 있습니다.";
            default: return "순서 변경 버튼을 누르고 스킬을 드래그하면 해당 위치의 순번으로 배치할 수 있습니다. 저장하면 적용되고 취소하면 편집 전 순서로 돌아갑니다.";
        }
    }

    private void ShowLesson(int lesson)
    {
        Lesson = lesson;
        ShowGuide(Explanation(lesson), lesson == 1 ? costHighlight : lesson == 3 ? targetHighlight : null);
    }

    private void ShowGuide(string text, RectTransform focus)
    {
        ShowingGuide = true;
        WaitingForSkill = showingDefeatPrompt = false;
        Time.timeScale = 0f;
        if (information != null) information.BeginBattle();
        highlight = focus;
        guideText.text = text;
        continueHint.gameObject.SetActive(true);
        overlay.SetActive(true);
    }

    public bool AdvanceGuide()
    {
        if (!ShowingGuide || WaitingForSkill || BlocksInputThisFrame) return false;
        consumedFrame = Time.frameCount;
        if (flow.State == BattleFlowState.TutorialComplete)
        {
            ShowLesson(7);
            flow.OpenTutorialPreparation();
            return true;
        }
        if (Lesson >= 3 && Lesson <= 5)
        {
            WaitingForSkill = true;
            continueHint.gameObject.SetActive(false);
            highlight = slotHighlights[RequiredSlot];
            cost.EnsureTutorialCost(deck.GetHandCard(RequiredSlot).Cost);
            guideText.text = Lesson == 3 ? "Q 또는 아이콘을 눌러 단일 스킬인 파이어볼을 사용하세요."
                : Lesson == 4 ? "E 또는 아이콘을 눌러 차단 스킬인 침묵으로 적의 공격을 차단하세요."
                : "R 또는 아이콘을 눌러 방어 스킬인 매직아머로 방어도를 쌓아 체력을 보존하세요.";
            return true;
        }
        CompletedLessons = Mathf.Max(CompletedLessons, Lesson);
        // 포션 설명 후 다음 목표를 안내한다. 이미 처치 조건을 채웠으면 완료 안내로 간다.
        if (Lesson == 6 && !showingDefeatPrompt && tutorialEnemy.CurrentHp > 0)
        {
            showingDefeatPrompt = true;
            guideText.text = defeatPrompt;
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
        ResumeTime();
    }

    private void ResumeTime() => Time.timeScale = information != null ? information.BattleSpeed : 1f;
    private void HideGuide()
    {
        ShowingGuide = WaitingForSkill = showingDefeatPrompt = false;
        overlay.SetActive(false);
    }

    private void Complete()
    {
        tutorialEnemy.ReleaseTutorialDefeat();
        Active = false;
        skipRect.gameObject.SetActive(false);
        ShowGuide(completionPrompt, null);
        consumedFrame = Time.frameCount;
        flow.FinishTutorial(false);
    }

    public void Skip()
    {
        if (!Active) return;
        Active = false;
        tutorialEnemy.HoldTutorialDefeat = false;
        HideGuide();
        skipRect.gameObject.SetActive(false);
        consumedFrame = Time.frameCount;
        flow.FinishTutorial(true);
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
        label.text = text;
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
        for (int i = 0; i < shades.Length; i++) shades[i] = UiRect("Shade" + i, root, new Color(0, 0, 0, 0.65f));
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
        guideText.rectTransform.offsetMin = new Vector2(30, 60);
        guideText.rectTransform.offsetMax = new Vector2(-30, -20);
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
