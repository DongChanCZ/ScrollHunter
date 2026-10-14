using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>인트로·대화 표시. 진행과 저장은 BattleFlow에서 처리한다.</summary>
[DefaultExecutionOrder(-250)]
public sealed class StoryUI : MonoBehaviour
{
    [SerializeField] private StoryContent content;
    [SerializeField] private Texture2D dialogueBackground;
    [SerializeField] private Texture2D forestBackground;
    [SerializeField] private Texture2D caveEntranceBackground;
    [SerializeField] private Texture2D bossBackground;
    private StoryContent.DialogueLine[] currentDialogue;
    private Texture2D currentDialogueBackground;
    private RectTransform artArea;
    [SerializeField] private RectTransform canvasRoot;
    [SerializeField] private GameObject[] combatHud;
    private readonly Dictionary<CanvasGroup,float> hiddenHud = new Dictionary<CanvasGroup,float>();
    [SerializeField] private TMP_FontAsset font;
    [SerializeField] private Material fontMaterial;
    [SerializeField] private Sprite panelSprite, frameSprite;
    [SerializeField] private Button buttonTemplate;
    [SerializeField] private Color panelColor = new Color(.13f,.18f,.27f,1f);
    [SerializeField] private Color frameColor = new Color(.7f,.55f,.33f,.95f);
    [SerializeField] private Color textColor = new Color(.96f,.92f,.84f,1f);
    [SerializeField] private Color nameColor = new Color(.91f,.8f,.53f,1f);
    [SerializeField] private Vector2 boxSize = new Vector2(1680, 290);
    [SerializeField] private float bottomMargin = 42f, textInset = 44f;
    [SerializeField] private float bodyFontSize = 32f, nameFontSize = 38f, hintFontSize = 23f;
    [SerializeField] private float paragraphSpacing = 14f;
    [SerializeField] private float lineSpacing = 72f;
    [SerializeField] private Vector2 portraitSize = new Vector2(610, 700);
    [SerializeField] private float portraitRightMargin = 85f;
    [SerializeField] private Vector2 skipSize = new Vector2(280, 64);
    [SerializeField] private Vector2 skipMargin = new Vector2(42, 34);
    [SerializeField] private Color dialogueShade = new Color(.02f,.025f,.04f,.27f);
    [SerializeField] private KeyCode advanceKey = KeyCode.Space, alternateAdvanceKey = KeyCode.Return;

    private IntroPresentation introPresentation;
    private BattleFlow flow;
    private GameObject root;
    private RawImage backdrop, portrait, secondaryPortrait;
    private AspectRatioFitter imageFit;
    private Image veil;
    private TMP_Text body, speakerName, hint;
    private Button skip;
    private Action finished;
    private int consumedFrame = -1;
    private float revealedCharacters;
    private int dialogueCharacterCount;
    public bool IsTyping => Active && !ShowingIntro && body.maxVisibleCharacters < dialogueCharacterCount;
    public bool Active { get; private set; }
    public bool ShowingIntro { get; private set; }
    public int PageIndex { get; private set; }
    public bool BlocksInput => Active || Time.frameCount == consumedFrame;
    public StoryContent Content => content;
    public bool HasForestDialogue => content != null && content.forestDialogue != null && content.forestDialogue.Length > 0;

    private void Awake() { flow = GetComponent<BattleFlow>(); BuildUI(); GameText.Changed += RefreshLanguage; }
    private void Update()
    {
        if (!Active || flow.IsMenuPaused) return;
        introPresentation.Tick(Time.unscaledDeltaTime);
        if (!Active) return;
        if (consumedFrame != Time.frameCount)
        {
            bool click = Input.GetMouseButtonDown(0);
            bool skipClick = click && RectTransformUtility.RectangleContainsScreenPoint((RectTransform)skip.transform, Input.mousePosition);
            if (!skipClick && (click || Input.GetKeyDown(advanceKey) || Input.GetKeyDown(alternateAdvanceKey)
                || Input.GetKeyDown(KeyCode.KeypadEnter))) Advance();
        }
        // 입력 한 번으로 새 문장까지 넘어가지 않도록 표시보다 입력을 먼저 처리한다.
        if (consumedFrame != Time.frameCount) TickDialogue(Time.unscaledDeltaTime);
    }

    private void TickDialogue(float delta)
    {
        if (!IsTyping || flow.IsMenuPaused) return;
        revealedCharacters = Mathf.Min(dialogueCharacterCount,
            revealedCharacters + Mathf.Max(0f, delta) * Mathf.Max(1f, content.dialogueCharactersPerSecond));
        body.maxVisibleCharacters = Mathf.FloorToInt(revealedCharacters);
    }

    public void ShowIntro(Action onFinished) => Show(true, onFinished);
    public void ShowTutorialDialogue(Action onFinished) =>
        ShowDialogue(content != null ? content.tutorialDialogue : null, dialogueBackground, onFinished);
    public void ShowForestDialogue(Action onFinished) =>
        ShowDialogue(content != null ? content.forestDialogue : null, forestBackground, onFinished);
    private StoryContent.DialogueLine[] BattleDialogue(int index)
    {
        if (content == null) return null;
        switch (index)
        {
            case 0: return content.forestDialogue;
            case 1: return content.leaderDialogue;
            case 2: return content.banditDialogue;
            case 3: return content.bossDialogue;
            default: return null;
        }
    }
    public bool HasBattleDialogue(int index)
    {
        var lines = BattleDialogue(index);
        return lines != null && lines.Length > 0;
    }
    public void ShowBattleDialogue(int index, Action onFinished) =>
        ShowDialogue(BattleDialogue(index), index == 0 ? forestBackground : index == 3 ? bossBackground : caveEntranceBackground, onFinished);

    private void ShowDialogue(StoryContent.DialogueLine[] lines, Texture2D background, Action onFinished)
    {
        currentDialogue = lines;
        currentDialogueBackground = background;
        Show(false, onFinished);
    }
    private void Show(bool intro, Action onFinished)
    {
        ShowingIntro = intro;
        finished = onFinished;
        PageIndex = 0;
        if (content == null || (intro ? content.intro == null || content.intro.Length == 0
            : currentDialogue == null || currentDialogue.Length == 0))
        { Complete(); return; }
        Active = true;
        consumedFrame = Time.frameCount;
        Time.timeScale = 0f;
        HideCombatHud();
        root.SetActive(true);
        root.transform.SetAsLastSibling();
        ClearSelection();
        DrawPage();
    }

    public bool Advance()
    {
        if (!Active || flow.IsMenuPaused || consumedFrame == Time.frameCount) return false;
        consumedFrame = Time.frameCount;
        if (ShowingIntro)
            return introPresentation.Leave(() =>
            {
                if (++PageIndex >= content.intro.Length) Complete(); else DrawPage();
            });
        if (IsTyping)
        {
            revealedCharacters = dialogueCharacterCount;
            body.maxVisibleCharacters = dialogueCharacterCount;
            return true;
        }
        if (++PageIndex >= currentDialogue.Length) Complete(); else DrawPage();
        return true;
    }
    public void Skip()
    {
        if (!Active || flow.IsMenuPaused || consumedFrame == Time.frameCount) return;
        consumedFrame = Time.frameCount;
        Complete();
    }
    private void Complete()
    {
        var next = finished;
        Cancel();
        next?.Invoke();
    }
    public void Cancel()
    {
        finished = null;
        introPresentation?.Reset();
        revealedCharacters = 0f; dialogueCharacterCount = 0;
        if (body != null) body.maxVisibleCharacters = int.MaxValue;
        Active = false;
        consumedFrame = Time.frameCount;
        if (root != null) root.SetActive(false);
        foreach(var entry in hiddenHud) if(entry.Key != null) entry.Key.alpha=entry.Value;
        hiddenHud.Clear();
        ClearSelection();
    }
    private void HideCombatHud()
    {
        if(combatHud==null) return;
        foreach(var obj in combatHud)
        {
            if(obj==null) continue;
            var group=obj.GetComponent<CanvasGroup>();
            if(group==null) group=obj.AddComponent<CanvasGroup>();
            if(!hiddenHud.ContainsKey(group)) hiddenHud.Add(group,group.alpha);
            group.alpha=0f;
        }
    }
    private static void ClearSelection()
    {
        if (UnityEngine.EventSystems.EventSystem.current != null)
            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
    }

    private void RefreshLanguage()
    {
        if (!Active) return;
        DrawPage();
        if (!ShowingIntro) { revealedCharacters = dialogueCharacterCount; body.maxVisibleCharacters = dialogueCharacterCount; }
    }

    private void DrawPage()
    {
        if (!ShowingIntro) introPresentation.Reset();
        body.maxVisibleCharacters = int.MaxValue;
        backdrop.gameObject.SetActive(true);
        artArea.offsetMin=new Vector2(0,ShowingIntro?bottomMargin+boxSize.y:0);
        imageFit.aspectMode=ShowingIntro?AspectRatioFitter.AspectMode.FitInParent:AspectRatioFitter.AspectMode.EnvelopeParent;
        backdrop.color=ShowingIntro?Color.white:new Color(1f-dialogueShade.a,1f-dialogueShade.a,1f-dialogueShade.a,1f);
        veil.color = ShowingIntro ? Color.black : dialogueShade;
        speakerName.gameObject.SetActive(!ShowingIntro);
        portrait.gameObject.SetActive(false);
        secondaryPortrait.gameObject.SetActive(false);
        body.fontStyle = FontStyles.Normal;
        body.alignment = TextAlignmentOptions.TopLeft;
        body.rectTransform.offsetMax = new Vector2(-textInset, ShowingIntro ? -textInset : -84f);
        hint.text = GameText.T(content.continueText);
        skip.GetComponentInChildren<TMP_Text>().text = GameText.T(ShowingIntro ? content.introSkipText : content.dialogueSkipText);
        if (ShowingIntro)
        {
            var page = content.intro[PageIndex];
            backdrop.texture = page.image;
            backdrop.uvRect=page.visibleRegion;
            if (page.image != null) imageFit.aspectRatio = (page.image.width * page.visibleRegion.width) / (page.image.height * page.visibleRegion.height);
            body.text = GameText.T(page.text);
            introPresentation.Show(page);
            return;
        }
        backdrop.texture=currentDialogueBackground;
        backdrop.uvRect=new Rect(0,0,1,1);
        if(currentDialogueBackground!=null)imageFit.aspectRatio=(float)currentDialogueBackground.width/currentDialogueBackground.height;
        var line = currentDialogue[PageIndex];
        bool narration = line.speaker == StoryContent.Speaker.Narrator;
        bool enemy = line.speaker == StoryContent.Speaker.Enemy;
        speakerName.gameObject.SetActive(!narration);
        speakerName.alignment = enemy ? TextAlignmentOptions.TopRight : TextAlignmentOptions.TopLeft;
        speakerName.text = GameText.T(content.playerName);
        body.text = GameText.T(narration ? "(" + GameText.T(line.text).Trim().Trim('(', ')') + ")" : line.text);
        body.fontStyle = narration ? FontStyles.Italic : FontStyles.Normal;
        if (narration) body.rectTransform.offsetMax = new Vector2(-textInset, -textInset);
        if (enemy && content.portraits != null && line.portraitIndex >= 0 && line.portraitIndex < content.portraits.Length)
        {
            var actor = content.portraits[line.portraitIndex];
            speakerName.text = GameText.T(string.IsNullOrEmpty(line.speakerNameOverride) ? actor.displayName : line.speakerNameOverride);
            bool group = line.secondaryPortraitIndex >= 0 && line.secondaryPortraitIndex < content.portraits.Length
                && content.portraits[line.secondaryPortraitIndex].image != null;
            Vector2 size = group ? portraitSize * .82f : portraitSize;
            float right = portraitRightMargin;
            if (group) right += DrawPortrait(secondaryPortrait, content.portraits[line.secondaryPortraitIndex].image, size, right) + 24f;
            DrawPortrait(portrait, actor.image, size, right);
        }
        body.ForceMeshUpdate();
        dialogueCharacterCount = body.textInfo.characterCount;
        revealedCharacters = narration ? dialogueCharacterCount : 0f;
        body.maxVisibleCharacters = narration ? dialogueCharacterCount : 0;
    }

    private float DrawPortrait(RawImage image, Texture2D texture, Vector2 size, float right)
    {
        image.texture = texture;
        image.gameObject.SetActive(texture != null);
        if (texture == null) return 0f;
        float aspect = (float)texture.width / texture.height;
        float width = Mathf.Min(size.x, size.y * aspect);
        image.rectTransform.sizeDelta = new Vector2(width, width / aspect);
        image.rectTransform.anchoredPosition = new Vector2(-right, bottomMargin + boxSize.y - 8f);
        return width;
    }

    private void BuildUI()
    {
        root = new GameObject("StoryOverlay", typeof(RectTransform), typeof(Image));
        root.transform.SetParent(canvasRoot, false);
        Stretch((RectTransform)root.transform);
        veil = root.GetComponent<Image>();
        veil.color = Color.black;
        backdrop = new GameObject("IntroArtwork", typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter)).GetComponent<RawImage>();
        artArea = new GameObject("ArtworkArea",typeof(RectTransform),typeof(RectMask2D)).GetComponent<RectTransform>();
        artArea.SetParent(root.transform,false);
        Stretch(artArea); artArea.offsetMin=new Vector2(0,bottomMargin+boxSize.y);
        backdrop.transform.SetParent(artArea, false);
        backdrop.raycastTarget = false;
        imageFit = backdrop.GetComponent<AspectRatioFitter>();
        imageFit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        portrait = new GameObject("EnemyPortrait", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
        portrait.transform.SetParent(root.transform, false);
        portrait.raycastTarget = false;
        var pr = portrait.rectTransform;
        pr.anchorMin = pr.anchorMax = new Vector2(1, 0); pr.pivot = new Vector2(1, 0);
        pr.anchoredPosition = new Vector2(-portraitRightMargin, bottomMargin + boxSize.y - 8f);
        pr.sizeDelta = portraitSize;
        secondaryPortrait = Instantiate(portrait, root.transform, false);
        secondaryPortrait.name = "SecondaryEnemyPortrait";
        secondaryPortrait.gameObject.SetActive(false);
        var panel = new GameObject("DialogueBox", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(root.transform, false);
        var rt = (RectTransform)panel.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(.5f,0); rt.pivot = new Vector2(.5f,0);
        rt.anchoredPosition = new Vector2(0,bottomMargin); rt.sizeDelta = boxSize;
        var bg = panel.GetComponent<Image>(); bg.sprite = panelSprite; bg.type = Image.Type.Sliced; bg.color = panelColor; bg.raycastTarget = false;
        var border = new GameObject("Ornament", typeof(RectTransform), typeof(Image));
        border.transform.SetParent(rt, false); Stretch((RectTransform)border.transform);
        var frame = border.GetComponent<Image>(); frame.sprite = frameSprite; frame.type = Image.Type.Sliced; frame.color = frameColor; frame.raycastTarget = false;
        speakerName = Label("Speaker", rt, nameFontSize, nameColor);
        Stretch(speakerName.rectTransform);
        speakerName.rectTransform.offsetMin = new Vector2(textInset,boxSize.y-76f);
        speakerName.rectTransform.offsetMax = new Vector2(-textInset,-24f);
        body = Label("Body", rt, bodyFontSize, textColor);
        Stretch(body.rectTransform);
        body.rectTransform.offsetMin = new Vector2(textInset,80f);
        body.paragraphSpacing = paragraphSpacing;
        body.lineSpacing = lineSpacing;
        hint = Label("ContinueHint", rt, hintFontSize, nameColor);
        Stretch(hint.rectTransform);
        hint.alignment = TextAlignmentOptions.BottomRight;
        hint.rectTransform.offsetMin = new Vector2(textInset,20f);
        hint.rectTransform.offsetMax = new Vector2(-textInset,-boxSize.y+58f);
        skip = Instantiate(buttonTemplate,root.transform,false);
        skip.name = "SkipStory"; skip.onClick = new Button.ButtonClickedEvent(); skip.onClick.AddListener(Skip);
        var sr=(RectTransform)skip.transform;
        sr.anchorMin=sr.anchorMax=sr.pivot=new Vector2(1,1);
        sr.anchoredPosition=-skipMargin; sr.sizeDelta=skipSize; skip.gameObject.SetActive(true);
        var label=skip.GetComponentInChildren<TMP_Text>(); label.fontSize=hintFontSize;
        introPresentation = new IntroPresentation(backdrop, body, hint, content != null ? content.introPresentation : new IntroPresentation.Settings());
        root.SetActive(false);
    }
    private TMP_Text Label(string objectName, Transform parent, float size, Color color)
    {
        var label = new GameObject(objectName,typeof(RectTransform),typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        label.transform.SetParent(parent,false); label.font=font; label.fontSharedMaterial=fontMaterial;
        label.fontSize=size; label.color=color; label.raycastTarget=false;
        label.textWrappingMode=TextWrappingModes.Normal; label.overflowMode=TextOverflowModes.Overflow;
        return label;
    }
    private static void Stretch(RectTransform rect)
    { rect.anchorMin=Vector2.zero; rect.anchorMax=Vector2.one; rect.offsetMin=rect.offsetMax=Vector2.zero; }
    private void OnDisable() => Cancel();
    private void OnDestroy() { GameText.Changed -= RefreshLanguage; introPresentation?.Dispose(); if(root != null) Destroy(root); }
}
