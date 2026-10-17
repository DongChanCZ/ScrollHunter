using TMPro;
using UnityEngine;
using UnityEngine.UI;

public partial class BattleRewardUI
{
    [Header("보상 교체 비교")]
    [SerializeField] private string rewardPreviewHeading = "획득할 스킬";
    [SerializeField] private string oldCardHeading = "교체할 기존 카드";
    [SerializeField] private string comparisonArrow = "→";
    [SerializeField] private Vector4 rewardPreviewRect = new Vector4(-748f, -45f, 350f, 440f);
    [SerializeField] private Vector2 replacementCardSize = new Vector2(350f, 410f);
    [SerializeField] private Vector2 replacementFirstCard = new Vector2(-365f, 175f);
    [SerializeField] private Vector2 replacementCardStep = new Vector2(366f, -426f);
    [SerializeField] private Vector4 rewardHeadingRect = new Vector4(-748f, 207f, 330f, 40f);
    [SerializeField] private Vector4 replacementDividerRect = new Vector4(-557f, -73f, 1f, 720f);
    [SerializeField] private float replacementBackY = -500f;
    [SerializeField] private float formationReorderX = 0f;
    [SerializeField] private float formationNextX = 600f;
    [SerializeField] private float comparisonHeadingSize = 25f;
    [SerializeField] private Color comparisonGold = new Color(0.91f, 0.80f, 0.53f, 1f);
    [SerializeField] private Color comparisonDividerColor = new Color(0.64f, 0.51f, 0.31f, 0.45f);
    [SerializeField] private Vector2 comparisonBodySize = new Vector2(1200f, 800f);
    [SerializeField] private Vector2 comparisonFramePadding = new Vector2(14f, 14f);
    [SerializeField] private Vector4 comparisonTitleRect = new Vector4(0f, 332f, 1100f, 60f);
    [SerializeField] private Vector4 comparisonPromptRect = new Vector4(0f, 263f, 1100f, 64f);
    [SerializeField] private Vector4 comparisonOldRect = new Vector4(-280f, -10f, 440f, 400f);
    [SerializeField] private Vector4 comparisonNewRect = new Vector4(280f, -10f, 440f, 400f);
    [SerializeField] private float comparisonButtonY = -320f;
    [SerializeField] private float comparisonCardHeadingY = 214f;
    [SerializeField] private float comparisonArrowSize = 48f;
    // x는 왼쪽 여백, y는 위 여백. 너비는 카드 폭에서 좌우 여백을 뺀다.
    [SerializeField] private Vector4 compactSlotLayout = new Vector4(22f, 18f, 22f, 24f);
    [SerializeField] private Vector4 compactNameLayout = new Vector4(18f, 140f, 18f, 44f);
    [SerializeField] private Vector4 compactInfoLayout = new Vector4(18f, 188f, 18f, 50f);
    [SerializeField] private Vector4 compactDetailLayout = new Vector4(22f, 246f, 22f, 22f);
    [SerializeField] private Vector4 compactIconLayout = new Vector4(0f, 54f, 80f, 80f);
    [SerializeField] private float compactLineSpacing = 8f;
    [SerializeField] private float compactGlyphPadding = 3f;

    [SerializeField] private float comparisonTitleOffset = 52f;
    [SerializeField] private float comparisonInstructionOffset = 50f;
    [SerializeField] private string comparisonSectionBreak = "\n<line-height=12> \n</line-height>";
    private Vector2 originalTitlePosition, originalInstructionPosition, originalTitleDividerPosition;
    private RectTransform comparisonTitleDivider;

    private GameObject replacementLayout;
    private ComparisonCard rewardPreview, comparisonOld, comparisonNew;
    private ComparisonCard[] replacementCards;
    private TMP_Text rewardHeading, oldHeading, newHeading;

    private sealed class ComparisonCard
    {
        public Button button;
        public TMP_Text slot, name, info, detail;
        public Image icon;
    }

    private void RefreshRewardComparison(bool replacing, bool confirming)
    {
        bool tall = replacing || deckRoot.activeSelf;
        title.rectTransform.anchoredPosition = originalTitlePosition + (tall ? Vector2.up * comparisonTitleOffset : Vector2.zero);
        instruction.rectTransform.anchoredPosition = originalInstructionPosition + (tall ? Vector2.up * comparisonInstructionOffset : Vector2.zero);
        if (comparisonTitleDivider != null)
            comparisonTitleDivider.anchoredPosition = originalTitleDividerPosition + (tall ? Vector2.up * comparisonTitleOffset : Vector2.zero);
        if (!replacing) return;
        if (replacementLayout == null) CreateRewardComparison();
        replacementLayout.SetActive(true);
        deckRoot.SetActive(false);
        if (dropIndicator != null) dropIndicator.gameObject.SetActive(false);
        var back = (RectTransform)backButton.transform;
        back.anchoredPosition = new Vector2(back.anchoredPosition.x, replacementBackY);
        SetLabel(rewardHeading, rewardPreviewHeading);
        FillComparisonCard(rewardPreview, flow.SelectedReward, string.Empty);
        for (int i = 0; i < replacementCards.Length; i++)
        {
            FillComparisonCard(replacementCards[i], flow.GetOrderCard(i), deckSlotLabels[i].text);
            replacementCards[i].button.interactable = !confirming;
        }
        if (!confirming) return;
        FillComparisonCard(comparisonOld, flow.GetDeckCard(flow.SelectedReplacementIndex), string.Empty);
        FillComparisonCard(comparisonNew, flow.SelectedReward, string.Empty);
        SetLabel(oldHeading, oldCardHeading);
        SetLabel(newHeading, rewardPreviewHeading);
        rewardReplacementConfirmation.transform.SetAsLastSibling();
    }

    private void CreateRewardComparison()
    {
        replacementLayout = new GameObject("RewardComparison", typeof(RectTransform));
        replacementLayout.SetActive(false);
        replacementLayout.transform.SetParent(panel.transform, false);
        rewardPreview = CopyComparisonCard(replacementLayout.transform, rewardPreviewRect, false);
        rewardHeading = ComparisonHeading(replacementLayout.transform, rewardHeadingRect);
        var divider = new GameObject("Divider", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        divider.transform.SetParent(replacementLayout.transform, false);
        PlaceComparisonRect(divider.rectTransform, replacementDividerRect);
        divider.color = comparisonDividerColor;
        divider.raycastTarget = false;
        replacementCards = new ComparisonCard[deckButtons.Length];
        for (int i = 0; i < replacementCards.Length; i++)
        {
            var position = replacementFirstCard + new Vector2((i % DeckSystem.HandSize) * replacementCardStep.x,
                (i / DeckSystem.HandSize) * replacementCardStep.y);
            replacementCards[i] = CopyComparisonCard(replacementLayout.transform,
                new Vector4(position.x, position.y, replacementCardSize.x, replacementCardSize.y), true);
            int index = i;
            replacementCards[i].button.onClick.AddListener(() => deckButtons[index].onClick.Invoke());
        }
        if (rewardReplacementConfirmation == null) return;
        var modal = rewardReplacementConfirmation.transform;
        var background = deckButtons[0].GetComponent<Image>();
        var sourceFrame = deckButtons[0].transform.Find("Ornament").GetComponent<Image>();
        var body = modal.Find("Body").GetComponent<Image>();
        body.sprite = background.sprite;
        body.type = background.type;
        body.color = background.color * deckButtons[0].colors.normalColor;
        var frame = modal.Find("Frame").GetComponent<Image>();
        frame.sprite = sourceFrame.sprite;
        frame.type = sourceFrame.type;
        frame.color = sourceFrame.color;
        ((RectTransform)modal.Find("Body")).sizeDelta = comparisonBodySize;
        ((RectTransform)modal.Find("Frame")).sizeDelta = comparisonBodySize + comparisonFramePadding;
        PlaceComparisonRect((RectTransform)modal.Find("Title"), comparisonTitleRect);
        PlaceComparisonRect(rewardReplacementPrompt.rectTransform, comparisonPromptRect);
        var yes = (RectTransform)rewardReplacementYes.transform;
        var no = (RectTransform)rewardReplacementNo.transform;
        yes.anchoredPosition = new Vector2(yes.anchoredPosition.x, comparisonButtonY);
        no.anchoredPosition = new Vector2(no.anchoredPosition.x, comparisonButtonY);
        comparisonOld = CopyComparisonCard(modal, comparisonOldRect, false);
        comparisonNew = CopyComparisonCard(modal, comparisonNewRect, false);
        oldHeading = ComparisonHeading(modal, new Vector4(comparisonOldRect.x, comparisonCardHeadingY, comparisonOldRect.z, comparisonHeadingSize * 2f));
        newHeading = ComparisonHeading(modal, new Vector4(comparisonNewRect.x, comparisonCardHeadingY, comparisonNewRect.z, comparisonHeadingSize * 2f));
        var arrow = ComparisonHeading(modal, new Vector4(0f, comparisonOldRect.y, comparisonArrowSize * 2f, comparisonArrowSize * 2f));
        arrow.fontSize = comparisonArrowSize;
        arrow.text = comparisonArrow;
    }

    private ComparisonCard CopyComparisonCard(Transform parent, Vector4 rect, bool selectable)
    {
        var copy = Instantiate(deckButtons[0], parent);
        copy.name = selectable ? "ReplacementCard" : "PreviewCard";
        copy.gameObject.SetActive(true);
        copy.onClick = new Button.ButtonClickedEvent();
        var drag = copy.GetComponent<DeckOrderSlot>();
        if (drag != null) { drag.enabled = false; Destroy(drag); }
        var card = new ComparisonCard
        {
            button = copy,
            slot = copy.transform.Find(deckSlotLabels[0].name).GetComponent<TMP_Text>(),
            name = copy.transform.Find(deckNameLabels[0].name).GetComponent<TMP_Text>(),
            info = copy.transform.Find(deckInfoLabels[0].name).GetComponent<TMP_Text>(),
            detail = copy.transform.Find(deckLabels[0].name).GetComponent<TMP_Text>(),
            icon = copy.transform.Find(deckIcons[0].name).GetComponent<Image>()
        };
        LayoutVerticalCard(card, rect);
        if (!selectable)
        {
            copy.enabled = false;
            // Selectable 비활성화는 틴트를 흰색으로 되돌리므로 바탕색 자체에 기존 카드 색을 적용한다.
            copy.targetGraphic.color *= copy.colors.normalColor;
            var tint = copy.GetComponent<SelectableTint>();
            if (tint != null) tint.enabled = false;
            foreach (var graphic in copy.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
            var frame = copy.transform.Find("Ornament").GetComponent<Image>();
            frame.color = deckButtons[0].transform.Find("Ornament").GetComponent<Image>().color;
        }
        return card;
    }

    [SerializeField] private Vector2 formationFirstCard = new Vector2(-549f, 175f);

    private void LayoutVerticalCard(ComparisonCard card, Vector4 rect)
    {
        PlaceComparisonRect((RectTransform)card.button.transform, rect);
        PlaceComparisonText(card.slot.rectTransform, compactSlotLayout);
        PlaceComparisonText(card.name.rectTransform, compactNameLayout);
        PlaceComparisonText(card.info.rectTransform, compactInfoLayout);
        var detail = compactDetailLayout;
        detail.w = rect.w - detail.y - detail.w;
        PlaceComparisonText(card.detail.rectTransform, detail);
        var icon = card.icon.rectTransform;
        icon.anchorMin = icon.anchorMax = new Vector2(0.5f, 1f);
        icon.pivot = new Vector2(0.5f, 1f);
        icon.anchoredPosition = new Vector2(compactIconLayout.x, -compactIconLayout.y);
        icon.sizeDelta = new Vector2(compactIconLayout.z, compactIconLayout.w);
        card.slot.alignment = TextAlignmentOptions.Center;
        card.name.alignment = TextAlignmentOptions.Center;
        card.info.alignment = TextAlignmentOptions.Top;
        card.detail.alignment = TextAlignmentOptions.Center;
        card.info.lineSpacing = card.detail.lineSpacing = compactLineSpacing;
        // 굵은 글자 외곽이 텍스트 영역 밖으로 번지지 않게 작은 내부 여백을 둔다.
        card.name.margin = card.info.margin = new Vector4(compactGlyphPadding, 0f, compactGlyphPadding, 0f);
    }

    private void LayoutFormationCards()
    {
        for (int i = 0; i < deckButtons.Length; i++)
        {
            var position = formationFirstCard + new Vector2((i % DeckSystem.HandSize) * replacementCardStep.x,
                (i / DeckSystem.HandSize) * replacementCardStep.y);
            LayoutVerticalCard(new ComparisonCard { button = deckButtons[i], slot = deckSlotLabels[i],
                name = deckNameLabels[i], info = deckInfoLabels[i], detail = deckLabels[i], icon = deckIcons[i] },
                new Vector4(position.x, position.y, replacementCardSize.x, replacementCardSize.y));
        }
        foreach (var button in new[] { reorderButton, saveOrderButton, cancelOrderButton })
        {
            if (button == null) continue;
            var rect = (RectTransform)button.transform;
            rect.anchoredPosition = new Vector2(button == reorderButton ? formationReorderX : rect.anchoredPosition.x, replacementBackY);
        }
    }

    public float CardNextX => formationNextX;
    public float CardFooterY => replacementBackY;

    private string CardEffectText(SkillData card)
    {
        string description = information.DescribeCardEffect(card);
        // 피해와 계산 기준은 묶고, 그 뒤의 대상·조건을 한 단락 띄운다.
        description = description.Replace("</size>\n", "</size>" + comparisonSectionBreak);
        if (card.Category == SkillCategory.Shield && card.IsChanneling)
        {
            int firstLine = description.IndexOf('\n');
            if (firstLine >= 0) description = description.Remove(firstLine, 1).Insert(firstLine, comparisonSectionBreak);
        }
        return description;
    }

    private void FillComparisonCard(ComparisonCard view, SkillData card, string slot)
    {
        view.button.gameObject.SetActive(card != null);
        if (card == null) return;
        SetLabel(view.slot, slot);
        SetLabel(view.name, card.DisplayName);
        SetLabel(view.info, information.DescribeCardInfo(card));
        SetLabel(view.detail, CardEffectText(card));
        CombatInfoUI.ShowIcon(view.icon, card);
    }

    private TMP_Text ComparisonHeading(Transform parent, Vector4 rect)
    {
        var label = Instantiate(instruction, parent);
        label.name = "ComparisonHeading";
        label.text = string.Empty;
        label.fontSize = comparisonHeadingSize;
        label.alignment = TextAlignmentOptions.Center;
        label.color = comparisonGold;
        label.raycastTarget = false;
        PlaceComparisonRect(label.rectTransform, rect);
        return label;
    }

    private static void PlaceComparisonRect(RectTransform rect, Vector4 layout)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(layout.x, layout.y);
        rect.sizeDelta = new Vector2(layout.z, layout.w);
    }

    private static void PlaceComparisonText(RectTransform rect, Vector4 layout)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.offsetMin = new Vector2(layout.x, -layout.y - layout.w);
        rect.offsetMax = new Vector2(-layout.z, -layout.y);
    }
}