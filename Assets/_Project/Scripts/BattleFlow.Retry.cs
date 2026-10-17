using TMPro;
using UnityEngine;
using UnityEngine.UI;

public partial class BattleFlow
{
    [Header("패배·재도전")]
    [SerializeField] private string retryButtonText = "다시 도전";
    [SerializeField] private string skipDialoguePreference = "ScrollHunter.Options.SkipSeenDialogue";
    [SerializeField] private bool skipSeenDialogueByDefault = true;
    [SerializeField] private Vector2 resultBodySize = new Vector2(1280f, 620f);
    [SerializeField] private Vector2 resultMessageSize = new Vector2(1160f, 420f);
    [SerializeField] private Vector2 resultMessagePosition = new Vector2(0f, 60f);
    [SerializeField] private float resultButtonY = -218f;
    [SerializeField, Range(0f, 1f)] private float defeatDimAlpha = .8f;
    [SerializeField, Min(0f)] private float defeatFadeSeconds = .35f;
    private CanvasGroup resultFade;
    private float resultDimAlpha;
    public bool IsRetryPreparation { get; private set; }
    public bool SkipSeenDialogue => PlayerPrefs.GetInt(skipDialoguePreference, skipSeenDialogueByDefault ? 1 : 0) != 0;
    public bool HasInheritanceCandidates
    {
        get
        {
            if (inheritance == null) return false;
            foreach (var option in earnedRewards) if (inheritance.CanInherit(option)) return true;
            return false;
        }
    }
    public void SetSkipSeenDialogue(bool value)
    {
        PlayerPrefs.SetInt(skipDialoguePreference, value ? 1 : 0);
        PlayerPrefs.Save();
    }
    public void ConfirmRetry()
    {
        if (State != BattleFlowState.Preparing || !IsRetryPreparation || IsEditingOrder || IsMenuPaused
            || rewardUI == null || !rewardUI.RetryConfirmationVisible) return;
        rewardUI.CloseRetryConfirmation();
        if (CanSaveProgress && !inheritance.TryRecordRetry()) Debug.LogWarning(GameText.T(progressSaveError), this);
        StartBattleStory(0, true);
    }
    private void BuildRetryPresentation()
    {
        if (resultPanel == null || resultText == null) return;
        ((RectTransform)resultText.transform.parent).sizeDelta = resultBodySize;
        resultText.rectTransform.sizeDelta = resultMessageSize;
        resultText.rectTransform.anchoredPosition = resultMessagePosition;
        resultText.alignment = TextAlignmentOptions.Center;
        if (restartButton != null)
        {
            var rect = (RectTransform)restartButton.transform;
            rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, resultButtonY);
        }
        resultFade = resultPanel.GetComponent<CanvasGroup>();
        if (resultFade == null) resultFade = resultPanel.AddComponent<CanvasGroup>();
        var image = resultPanel.GetComponent<Image>();
        if (image != null) resultDimAlpha = image.color.a;
    }
    private void RefreshRetryPresentation(bool wasVisible)
    {
        if (resultPanel == null || resultFade == null) return;
        bool defeat = State == BattleFlowState.Defeat;
        var image = resultPanel.GetComponent<Image>();
        if (image != null) image.color = new Color(0f, 0f, 0f, defeat ? defeatDimAlpha : resultDimAlpha);
        if (!resultPanel.activeSelf || !defeat) resultFade.alpha = 1f;
        else if (!wasVisible) resultFade.alpha = defeatFadeSeconds > 0f ? 0f : 1f;
        resultFade.interactable = resultFade.alpha >= 1f;
    }
    private void LateUpdate()
    {
        if (IsMenuPaused || resultFade == null || !resultPanel.activeSelf || resultFade.alpha >= 1f) return;
        resultFade.alpha = defeatFadeSeconds > 0f
            ? Mathf.MoveTowards(resultFade.alpha, 1f, Time.unscaledDeltaTime / defeatFadeSeconds) : 1f;
        resultFade.interactable = resultFade.alpha >= 1f;
    }
}
