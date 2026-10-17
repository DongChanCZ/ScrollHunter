using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public partial class BattleRewardUI
{
    [Header("재도전 편성")]
    [SerializeField] private string retryPreparationTitle = "다시 도전 준비";
    [SerializeField] private string retryConfirmTitle = "다시 도전";
    [SerializeField] private string retryConfirmMessage = "스테이지의 맨 처음부터 다시 시작합니다.";
    [SerializeField] private string skipDialogueLabel = "이미 본 전투 대화 건너뛰기";
    [SerializeField] private string retryConfirmLabel = "확인";
    [SerializeField] private string retryCancelLabel = "취소";
    [SerializeField] private Vector2 retryPopupSize = new Vector2(1080f, 420f);
    [SerializeField] private float retryTitleY = 150f;
    [SerializeField] private Vector4 retryMessageRect = new Vector4(0f, 60f, 960f, 96f);
    [SerializeField] private float retryMessageFontSize = 30f;
    [SerializeField] private Vector2 retryToggleSize = new Vector2(880f, 44f);
    [SerializeField] private float retryToggleY = -36f;
    [SerializeField] private float retryToggleFontSize = 22f;
    [SerializeField] private float retryToggleBoxSize = 38f;
    [SerializeField] private float retryToggleLabelInset = 52f;
    [SerializeField] private Vector2 retryActionPosition = new Vector2(190f, -146f);
    private Toggle dialogueSkipToggle;
    private Button retryConfirmButton, retryCancelButton;
    private GameObject retryConfirmationPanel;

    public bool RetryConfirmationVisible => retryConfirmationPanel != null && retryConfirmationPanel.activeSelf;

    private void RefreshRetryConfirmation(bool preparing)
    {
        if ((!preparing || !flow.IsRetryPreparation || flow.IsEditingOrder) && retryConfirmationPanel != null)
            retryConfirmationPanel.SetActive(false);
    }

    private void BuildRetryConfirmation()
    {
        var menu = flow.GetComponent<MainMenuUI>();
        if (menu == null || menu.CheckBoxTemplate == null || rewardReplacementConfirmation == null) return;
        // 기존 확인창의 바탕·테두리·버튼을 그대로 사용한다.
        retryConfirmationPanel = new GameObject("RetryConfirmation", typeof(RectTransform), typeof(Image));
        retryConfirmationPanel.SetActive(false);
        retryConfirmationPanel.transform.SetParent(panel.transform.parent, false);
        var overlay = (RectTransform)retryConfirmationPanel.transform;
        overlay.anchorMin = Vector2.zero; overlay.anchorMax = Vector2.one;
        overlay.offsetMin = overlay.offsetMax = Vector2.zero;
        retryConfirmationPanel.GetComponent<Image>().color = new Color(0f, 0f, 0f, .8f);
        foreach (string name in new[] { "Body", "Frame", "Title" })
        {
            var copy = Instantiate(rewardReplacementConfirmation.transform.Find(name), overlay, false);
            copy.name = name;
            if (name != "Title")
            {
                var source = name == "Body" ? deckButtons[0].GetComponent<Image>() : deckButtons[0].transform.Find("Ornament").GetComponent<Image>();
                var image = copy.GetComponent<Image>();
                image.sprite = source.sprite; image.type = source.type;
                image.color = name == "Body" ? source.color * deckButtons[0].colors.normalColor : comparisonGold;
            }
            var rect = (RectTransform)copy;
            rect.anchoredPosition = name == "Title" ? new Vector2(0f, retryTitleY) : Vector2.zero;
            if (name != "Title") rect.sizeDelta = retryPopupSize + (name == "Frame" ? comparisonFramePadding : Vector2.zero);
            else
            {
                rect.sizeDelta = new Vector2(retryPopupSize.x - 100f, 60f);
                InitializeDialogueLabel(copy.GetComponent<TMP_Text>(), retryConfirmTitle);
            }
        }
        var message = Instantiate(retryConfirmationPanel.transform.Find("Title").GetComponent<TMP_Text>(), overlay, false);
        message.name = "Message";
        message.rectTransform.anchoredPosition = new Vector2(retryMessageRect.x, retryMessageRect.y);
        message.rectTransform.sizeDelta = new Vector2(retryMessageRect.z, retryMessageRect.w);
        message.fontSize = retryMessageFontSize;
        message.enableAutoSizing = false;
        message.alignment = TextAlignmentOptions.Center;
        message.textWrappingMode = TextWrappingModes.Normal;
        InitializeDialogueLabel(message, retryConfirmMessage);
        dialogueSkipToggle = Instantiate(menu.CheckBoxTemplate, overlay, false);
        dialogueSkipToggle.name = "SkipSeenDialogue";
        dialogueSkipToggle.group = null;
        dialogueSkipToggle.onValueChanged = new Toggle.ToggleEvent();
        dialogueSkipToggle.onValueChanged.AddListener(flow.SetSkipSeenDialogue);
        dialogueSkipToggle.interactable = true;
        dialogueSkipToggle.gameObject.SetActive(true);
        var toggleRect = (RectTransform)dialogueSkipToggle.transform;
        toggleRect.anchorMin = toggleRect.anchorMax = new Vector2(.5f, .5f);
        toggleRect.anchoredPosition = new Vector2(0f, retryToggleY);
        toggleRect.sizeDelta = retryToggleSize;
        var label = dialogueSkipToggle.GetComponentInChildren<TMP_Text>(true);
        label.fontSize = retryToggleFontSize;
        label.enableAutoSizing = false;
        label.alignment = TextAlignmentOptions.MidlineLeft;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        var border = (RectTransform)dialogueSkipToggle.transform.Find("Border");
        border.localScale = Vector3.one * (retryToggleBoxSize / border.sizeDelta.x);
        border.anchorMin = border.anchorMax = new Vector2(0f, .5f);
        border.anchoredPosition = new Vector2(retryToggleBoxSize * .5f, 0f);
        label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = Vector2.one;
        label.rectTransform.offsetMin = new Vector2(retryToggleLabelInset, 0f); label.rectTransform.offsetMax = Vector2.zero;
        InitializeDialogueLabel(label, skipDialogueLabel);

        retryConfirmButton = Instantiate(reorderButton, overlay, false);
        retryConfirmButton.name = "ConfirmRetry";
        retryConfirmButton.gameObject.SetActive(true);
        retryConfirmButton.onClick = new Button.ButtonClickedEvent();
        retryConfirmButton.onClick.AddListener(flow.ConfirmRetry);
        ((RectTransform)retryConfirmButton.transform).anchoredPosition = new Vector2(-retryActionPosition.x, retryActionPosition.y);
        InitializeDialogueLabel(retryConfirmButton.GetComponentInChildren<TMP_Text>(true), retryConfirmLabel);
        retryCancelButton = Instantiate(reorderButton, overlay, false);
        retryCancelButton.name = "CancelRetry";
        retryCancelButton.gameObject.SetActive(true);
        retryCancelButton.onClick = new Button.ButtonClickedEvent();
        retryCancelButton.onClick.AddListener(() => CloseRetryConfirmation());
        ((RectTransform)retryCancelButton.transform).anchoredPosition = retryActionPosition;
        InitializeDialogueLabel(retryCancelButton.GetComponentInChildren<TMP_Text>(true), retryCancelLabel);
        dialogueSkipToggle.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnDown = retryConfirmButton };
        retryConfirmButton.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnUp = dialogueSkipToggle, selectOnRight = retryCancelButton };
        retryCancelButton.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnUp = dialogueSkipToggle, selectOnLeft = retryConfirmButton };
    }

    private string dialogueSizingText;
    private TMP_FontAsset dialogueSizingFont;
    private void LateUpdate()
    {
        if (!RetryConfirmationVisible) return;
        var label = dialogueSkipToggle.GetComponentInChildren<TMP_Text>();
        if (dialogueSizingText == label.text && dialogueSizingFont == label.font) return;
        dialogueSizingText = label.text; dialogueSizingFont = label.font;
        float width = Mathf.Min(retryToggleSize.x, label.GetPreferredValues(label.text, Mathf.Infinity, Mathf.Infinity).x + retryToggleLabelInset);
        ((RectTransform)dialogueSkipToggle.transform).sizeDelta = new Vector2(width, retryToggleSize.y);
    }

    private static void InitializeDialogueLabel(TMP_Text label, string source)
    {
        label.text = source;
        var localized = label.GetComponent<LocalizedLabel>();
        if (localized == null) localized = label.gameObject.AddComponent<LocalizedLabel>();
        localized.Initialize(source);
    }

    public void OpenRetryConfirmation()
    {
        if (flow.State != BattleFlowState.Preparing || !flow.IsRetryPreparation || flow.IsEditingOrder || flow.IsMenuPaused) return;
        if (retryConfirmationPanel == null) BuildRetryConfirmation();
        if (retryConfirmationPanel == null) return;
        dialogueSkipToggle.SetIsOnWithoutNotify(flow.SkipSeenDialogue);
        retryConfirmationPanel.SetActive(true);
        retryConfirmationPanel.transform.SetAsLastSibling();
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(retryConfirmButton.gameObject);
    }

    public bool CloseRetryConfirmation()
    {
        if (!RetryConfirmationVisible) return false;
        retryConfirmationPanel.SetActive(false);
        flow.RefreshTutorialUI();
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        return true;
    }
}
