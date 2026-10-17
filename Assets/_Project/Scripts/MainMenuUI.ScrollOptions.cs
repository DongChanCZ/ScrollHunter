using TMPro;
using UnityEngine;
using UnityEngine.UI;

public partial class MainMenuUI
{
    [Header("옵션 스크롤·대화")]
    [SerializeField] private Vector2 optionsScrollSize = new Vector2(1360f, 610f);
    [SerializeField] private Vector2 optionsScrollPosition = new Vector2(0f, 14f);
    [SerializeField] private float optionsRowSpacing = 110f;
    [SerializeField] private string skipSeenDialogueText = "이미 본 전투 대화 건너뛰기";
    [SerializeField] private string dialogueLockedText = "첫 재도전을 시작하면 변경할 수 있습니다.";
    private ScrollRect optionsScroll;
    private Toggle skipSeenDialogueToggle;
    private TMP_Text dialogueLockedLabel;

    private void BuildScrollableOptions()
    {
        if (optionsPanel == null || languageDropdown == null || vSyncToggle == null) return;
        var root = new GameObject("OptionsScroll", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
        var rect = (RectTransform)root.transform;
        rect.SetParent(optionsPanel.transform, false);
        rect.sizeDelta = optionsScrollSize;
        rect.anchoredPosition = optionsScrollPosition;
        root.GetComponent<Image>().color = Color.clear;
        optionsScroll = root.GetComponent<ScrollRect>();
        optionsScroll.horizontal = false;
        optionsScroll.movementType = ScrollRect.MovementType.Clamped;
        optionsScroll.scrollSensitivity = 48f;
        var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D)).GetComponent<RectTransform>();
        viewport.SetParent(rect, false);
        viewport.anchorMin = Vector2.zero; viewport.anchorMax = Vector2.one;
        viewport.offsetMin = Vector2.zero; viewport.offsetMax = new Vector2(-32f, 0f);
        var content = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
        content.SetParent(viewport, false);
        content.anchorMin = new Vector2(0f, 1f); content.anchorMax = Vector2.one;
        content.pivot = new Vector2(.5f, 1f);
        optionsScroll.viewport = viewport; optionsScroll.content = content;
        var bar = Instantiate(languageDropdown.template.GetComponentInChildren<Scrollbar>(true), rect, false);
        bar.name = "Scrollbar";
        var br = (RectTransform)bar.transform;
        br.anchorMin = new Vector2(1f, 0f); br.anchorMax = Vector2.one;
        br.pivot = new Vector2(1f, .5f);
        br.sizeDelta = new Vector2(14f, 0f); br.anchoredPosition = Vector2.zero;
        bar.direction = Scrollbar.Direction.BottomToTop;
        bar.onValueChanged = new Scrollbar.ScrollEvent();
        bar.gameObject.SetActive(true);
        optionsScroll.verticalScrollbar = bar;
        optionsScroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;

        float y = -60f;
        MoveOption("LanguageLabel", content, y); MoveOption("LanguageDropdown", content, y);
        if (!GameText.IsWeb)
        {
            y -= optionsRowSpacing;
            MoveOption("Mode", content, y); MoveOption("ScreenMode", content, y);
            y -= optionsRowSpacing;
            foreach (var name in new[] { "Resolution", "ResolutionDropdown", "PreviousResolution", "NextResolution" }) MoveOption(name, content, y);
            y -= optionsRowSpacing;
            MoveOption("VSyncTitle", content, y); MoveOption("VSyncToggle", content, y);
            y -= optionsRowSpacing;
            MoveOption("FrameLimitTitle", content, y); MoveOption("FrameLimitDropdown", content, y);
            y -= 70f; MoveOption("DisplayHint", content, y);
            y -= 55f; MoveOption("PerformanceHint", content, y);
        }
        y -= 78f; MoveOption("DisplaySoundDivider", content, y);
        for (int i = 0; i < volumeSliders.Length; i++)
        {
            y -= optionsRowSpacing;
            foreach (var prefix in new[] { "SoundLabel", "Volume", "VolumeValue" }) MoveOption(prefix + i, content, y);
        }
        y -= 78f;
        var sourceLine = content.Find("DisplaySoundDivider");
        if (sourceLine != null)
        {
            var line = Instantiate(sourceLine, content, false); line.name = "FeedbackDivider";
            ((RectTransform)line).anchoredPosition = new Vector2(0f, y);
        }
        y -= optionsRowSpacing; MoveToggle(screenFeedbackToggle, content, y);
        y -= optionsRowSpacing; MoveToggle(screenShakeToggle, content, y);
        skipSeenDialogueToggle = FeedbackToggle("SkipSeenDialogue", skipSeenDialogueText, 0f);
        y -= optionsRowSpacing; MoveToggle(skipSeenDialogueToggle, content, y);
        dialogueLockedLabel = Instantiate(optionsPanel.transform.Find("Hint").GetComponent<TMP_Text>(), content, false);
        dialogueLockedLabel.name = "DialogueLockedHint";
        dialogueLockedLabel.rectTransform.anchorMin = dialogueLockedLabel.rectTransform.anchorMax = new Vector2(.5f, 1f);
        dialogueLockedLabel.rectTransform.anchoredPosition = new Vector2(0f, y - 60f);
        dialogueLockedLabel.rectTransform.sizeDelta = new Vector2(1240f, 50f);
        dialogueLockedLabel.fontSize = 23f;
        var local = dialogueLockedLabel.GetComponent<LocalizedLabel>();
        if (local == null) local = dialogueLockedLabel.gameObject.AddComponent<LocalizedLabel>();
        dialogueLockedLabel.text = dialogueLockedText;
        local.Initialize(dialogueLockedText);
        content.sizeDelta = new Vector2(0f, -y + 130f);
        var hint = (RectTransform)optionsPanel.transform.Find("Hint");
        hint.anchoredPosition = new Vector2(0f, -332f);
        ReadDialogueOption();
    }

    private void MoveOption(string name, RectTransform content, float y)
    {
        var row = optionsPanel.transform.Find(name) as RectTransform;
        if (row == null) return;
        float x = row.anchoredPosition.x;
        row.SetParent(content, false);
        row.anchorMin = row.anchorMax = new Vector2(.5f, 1f);
        row.anchoredPosition = new Vector2(x, y);
    }

    private static void MoveToggle(Toggle toggle, RectTransform content, float y)
    {
        if (toggle == null) return;
        var rect = (RectTransform)toggle.transform;
        rect.SetParent(content, false);
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, y); rect.sizeDelta = new Vector2(1240f, 56f);
        var border = rect.Find("Border") as RectTransform;
        if (border != null) border.anchoredPosition = new Vector2(-590f, 0f);
        var label = toggle.GetComponentInChildren<TMP_Text>(true);
        label.rectTransform.sizeDelta = new Vector2(1100f, 56f);
        label.rectTransform.anchoredPosition = new Vector2(24f, 0f);
    }

    private void ReadDialogueOption()
    {
        if (skipSeenDialogueToggle == null || flow == null) return;
        bool unlocked = flow.Inheritance != null && flow.Inheritance.HasRetried;
        skipSeenDialogueToggle.SetIsOnWithoutNotify(flow.SkipSeenDialogue);
        skipSeenDialogueToggle.interactable = unlocked;
        if (dialogueLockedLabel != null) dialogueLockedLabel.gameObject.SetActive(!unlocked);
    }

    private void SaveDialogueOption()
    {
        if (skipSeenDialogueToggle != null && skipSeenDialogueToggle.interactable)
            flow.SetSkipSeenDialogue(skipSeenDialogueToggle.isOn);
    }
}
