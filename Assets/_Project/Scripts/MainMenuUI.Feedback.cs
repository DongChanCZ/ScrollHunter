using TMPro;
using UnityEngine;
using UnityEngine.UI;

public partial class MainMenuUI
{
    [Header("전투 화면 효과 옵션")]
    [SerializeField] private string screenFeedbackText = "피격·기절 화면 효과";
    [SerializeField] private string screenShakeText = "화면 흔들림";
    [SerializeField] private float feedbackOptionY = -332f;
    [SerializeField] private float feedbackWebOptionY = -255f;
    [SerializeField] private float feedbackOptionX = 335f;
    [SerializeField] private float feedbackOptionFontSize = 24f;
    [SerializeField] private string feedbackOptionsHint = "변경한 설정은 적용하면 저장되고, 취소하면 돌아갑니다.";
    private Toggle screenFeedbackToggle, screenShakeToggle;
    public bool ScreenFeedbackEnabled { get; private set; } = true;
    public bool ScreenShakeEnabled { get; private set; } = true;

    private void BuildFeedbackOptions()
    {
        if (vSyncToggle == null || optionsPanel == null) return;
        screenFeedbackToggle = FeedbackToggle("ScreenFeedback", screenFeedbackText, -feedbackOptionX);
        screenShakeToggle = FeedbackToggle("ScreenShake", screenShakeText, feedbackOptionX);
        screenFeedbackToggle.onValueChanged.AddListener(value => ScreenFeedbackEnabled = value);
        screenShakeToggle.onValueChanged.AddListener(value => ScreenShakeEnabled = value);
        var hintLabel = optionsPanel.transform.Find("Hint").GetComponent<TMP_Text>();
        hintLabel.text = feedbackOptionsHint;
        var hintLocalized = hintLabel.GetComponent<LocalizedLabel>();
        if (hintLocalized != null) hintLocalized.Initialize(feedbackOptionsHint);
        if (!GameText.IsWeb)
        {
            var hint = optionsPanel.transform.Find("Hint") as RectTransform;
            if (hint != null) hint.anchoredPosition = new Vector2(0, -454);
        }
    }

    private Toggle FeedbackToggle(string objectName, string labelText, float x)
    {
        var toggle = Instantiate(vSyncToggle, optionsPanel.transform, false);
        toggle.name = objectName;
        toggle.onValueChanged = new Toggle.ToggleEvent();
        toggle.group = null;
        toggle.interactable = true;
        var rect = (RectTransform)toggle.transform;
        rect.anchoredPosition = new Vector2(x, GameText.IsWeb ? feedbackWebOptionY : feedbackOptionY);
        rect.sizeDelta = new Vector2(650, 48);
        var label = toggle.GetComponentInChildren<TMP_Text>(true);
        label.rectTransform.anchoredPosition = new Vector2(10, 0);
        label.rectTransform.sizeDelta = new Vector2(530, 48);
        label.fontSize = feedbackOptionFontSize;
        label.enableAutoSizing = false;
        label.text = labelText;
        var localized = label.GetComponent<LocalizedLabel>();
        if (localized == null) localized = label.gameObject.AddComponent<LocalizedLabel>();
        localized.Initialize(labelText);
        toggle.gameObject.SetActive(true);
        return toggle;
    }

    private void ReadFeedbackOptions()
    {
        ScreenFeedbackEnabled = PlayerPrefs.GetInt(preferencesPrefix + "ScreenFeedback", 1) != 0;
        ScreenShakeEnabled = PlayerPrefs.GetInt(preferencesPrefix + "ScreenShake", 1) != 0;
        if (screenFeedbackToggle != null) screenFeedbackToggle.SetIsOnWithoutNotify(ScreenFeedbackEnabled);
        if (screenShakeToggle != null) screenShakeToggle.SetIsOnWithoutNotify(ScreenShakeEnabled);
    }

    private void SaveFeedbackOptions()
    {
        PlayerPrefs.SetInt(preferencesPrefix + "ScreenFeedback", ScreenFeedbackEnabled ? 1 : 0);
        PlayerPrefs.SetInt(preferencesPrefix + "ScreenShake", ScreenShakeEnabled ? 1 : 0);
    }
}
