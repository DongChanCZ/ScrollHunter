using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>보스 사망 연출 뒤 숲 클리어 → 사막 예고 → 시작 화면.</summary>
public class StageClearUI : MonoBehaviour
{
    [SerializeField] private RectTransform canvasRoot;
    [SerializeField] private Texture2D forest, desert;
    [SerializeField] private TMP_FontAsset titleFont, hintFont;
    [SerializeField] private Material titleMaterial, hintMaterial;
    [SerializeField] private string forestTitle = "FOREST STAGE CLEAR";
    [SerializeField] private string desertTitle = "DESERT STAGE";
    [SerializeField] private string desertSubtitle = "COMING SOON...?";
    [SerializeField] private string continueHint = "Space";
    [SerializeField, Min(.01f)] private float fadeSeconds = .8f;
    [SerializeField, Min(0f)] private float forestHoldSeconds = 2.4f;
    [SerializeField] private float titleSize = 82f, subtitleSize = 47f, hintSize = 25f;
    [SerializeField] private Color titleColor = new Color(.96f,.84f,.57f,1f);
    [SerializeField, Range(0f,1f)] private float shadeAlpha = .4f;
    private BattleFlow flow;
    private GameObject root;
    private CanvasGroup page;
    private RawImage artwork;
    private AspectRatioFitter fit;
    private TMP_Text heading, subtitle, hint;
    private float elapsed;
    private int readyFrame;
    public bool Active => root != null && root.activeSelf;
    public bool ShowingDesert => elapsed >= fadeSeconds * 2f + forestHoldSeconds;
    public bool Ready => Active && elapsed >= fadeSeconds * 3f + forestHoldSeconds;

    private void Awake()
    {
        flow = GetComponent<BattleFlow>();
        root = new GameObject("StageClearOverlay", typeof(RectTransform), typeof(Image));
        root.transform.SetParent(canvasRoot, false);
        Stretch((RectTransform)root.transform);
        root.GetComponent<Image>().color = Color.black;
        var scene = new GameObject("Page",typeof(RectTransform),typeof(CanvasGroup));
        scene.transform.SetParent(root.transform,false); Stretch((RectTransform)scene.transform);
        page = scene.GetComponent<CanvasGroup>();
        artwork = new GameObject("Background",typeof(RectTransform),typeof(RawImage),typeof(AspectRatioFitter)).GetComponent<RawImage>();
        artwork.transform.SetParent(scene.transform,false); artwork.raycastTarget=false;
        fit = artwork.GetComponent<AspectRatioFitter>();
        fit.aspectMode=AspectRatioFitter.AspectMode.EnvelopeParent;
        var shade = new GameObject("Shade",typeof(RectTransform),typeof(Image));
        shade.transform.SetParent(scene.transform,false); Stretch((RectTransform)shade.transform);
        shade.GetComponent<Image>().color=new Color(0,0,0,shadeAlpha);
        shade.GetComponent<Image>().raycastTarget=false;
        heading=Label("Title",scene.transform,titleFont,titleMaterial,titleSize);
        Place(heading.rectTransform,new Vector2(0,45),new Vector2(1760,150));
        subtitle=Label("Subtitle",scene.transform,titleFont,titleMaterial,subtitleSize);
        Place(subtitle.rectTransform,new Vector2(0,-90),new Vector2(1500,100));
        hint=Label("ContinueHint",scene.transform,hintFont,hintMaterial,hintSize);
        hint.rectTransform.anchorMin=hint.rectTransform.anchorMax=new Vector2(1,0);
        hint.rectTransform.pivot=new Vector2(1,0);
        hint.rectTransform.anchoredPosition=new Vector2(-60,44);
        hint.rectTransform.sizeDelta=new Vector2(180,50);
        hint.alignment=TextAlignmentOptions.BottomRight;
        hint.text=continueHint;
        root.SetActive(false);
    }

    public void Show()
    {
        if (Active || root == null) return;
        elapsed=0f;readyFrame=-1;
        root.SetActive(true);root.transform.SetAsLastSibling();
        if(UnityEngine.EventSystems.EventSystem.current!=null)
            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
        Draw();
    }

    private void Update()
    {
        if(!Active) return;
        if(flow.State!=BattleFlowState.Victory) { Cancel();return; }
        if(flow.IsMenuPaused) return;
        Tick(Time.unscaledDeltaTime);
        if(Input.GetMouseButtonDown(0)||Input.GetKeyDown(KeyCode.Space)
            ||Input.GetKeyDown(KeyCode.Return)||Input.GetKeyDown(KeyCode.KeypadEnter)) Advance();
    }

    private void Tick(float delta)
    {
        if(!Active||flow.IsMenuPaused) return;
        bool wasReady=Ready;
        elapsed+=Mathf.Max(0f,delta);
        if(!wasReady&&Ready) readyFrame=Time.frameCount;
        Draw();
    }

    private void Draw()
    {
        bool next=ShowingDesert;
        var image=next?desert:forest;
        artwork.texture=image;
        if(image!=null) fit.aspectRatio=(float)image.width/image.height;
        heading.text=next?desertTitle:forestTitle;
        subtitle.text=next?desertSubtitle:"";
        float start=fadeSeconds+forestHoldSeconds;
        page.alpha=next?Mathf.Clamp01((elapsed-start-fadeSeconds)/fadeSeconds)
            :elapsed<=start?Mathf.Clamp01(elapsed/fadeSeconds)
            :1f-Mathf.Clamp01((elapsed-start)/fadeSeconds);
        hint.gameObject.SetActive(Ready);
    }

    public bool Advance()
    {
        if(!Ready||flow.IsMenuPaused||flow.MenuBlocksInput||Time.frameCount==readyFrame) return false;
        Cancel();
        flow.ReturnToTitle();
        return true;
    }

    public void Cancel() { if(root!=null)root.SetActive(false);elapsed=0f;readyFrame=-1; }
    private void OnDisable()=>Cancel();
    private void OnDestroy(){if(root!=null)Destroy(root);}
    private TMP_Text Label(string name,Transform parent,TMP_FontAsset font,Material material,float size)
    {
        var t=new GameObject(name,typeof(RectTransform),typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        t.transform.SetParent(parent,false);t.font=font;t.fontSharedMaterial=material;t.fontSize=size;
        t.color=titleColor;t.alignment=TextAlignmentOptions.Center;t.raycastTarget=false;
        t.textWrappingMode=TextWrappingModes.Normal;return t;
    }
    private static void Stretch(RectTransform rt)
    {rt.anchorMin=Vector2.zero;rt.anchorMax=Vector2.one;rt.offsetMin=rt.offsetMax=Vector2.zero;}
    private static void Place(RectTransform rt,Vector2 position,Vector2 size)
    {rt.anchorMin=rt.anchorMax=rt.pivot=new Vector2(.5f,.5f);rt.anchoredPosition=position;rt.sizeDelta=size;}
}
