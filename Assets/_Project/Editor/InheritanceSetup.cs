using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class InheritanceSetup
{
    [MenuItem("Tools/Scroll Hunter/Apply Inheritance UI")]
    public static void Apply()
    {
        if (Application.isPlaying) throw new System.InvalidOperationException("Stop Play before applying inheritance UI.");
        var flow = Object.FindFirstObjectByType<BattleFlow>();
        var f = new SerializedObject(flow);
        var pool = f.FindProperty("rewardPool");
        for (int i = 0; i < pool.arraySize; i++)
        {
            var entry = pool.GetArrayElementAtIndex(i);
            var asset = entry.FindPropertyRelative("card").objectReferenceValue ?? entry.FindPropertyRelative("effect").objectReferenceValue;
            entry.FindPropertyRelative("inheritanceId").stringValue = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(asset));
            entry.FindPropertyRelative("inheritable").boolValue = !asset.name.StartsWith("PS02_");
        }
        f.FindProperty("defeatFormat").stringValue = "전투 {0} 패배\n이번 런에서 얻은 스킬·패시브 중 1개를 계승할 수 있습니다.";
        f.ApplyModifiedPropertiesWithoutUndo();
        var ui = f.FindProperty("rewardUI").objectReferenceValue as BattleRewardUI;
        var u = new SerializedObject(ui);
        var start = f.FindProperty("startPanel").objectReferenceValue as GameObject;
        var source = f.FindProperty("startButton").objectReferenceValue as Button;
        var sourceText = source.GetComponentInChildren<TMP_Text>(true);
        ApplyReplacementConfirmation();
        // 다시 실행해도 기존 UI와 사용자가 조정한 배치를 유지한다.
        if (u.FindProperty("inheritancePanel").objectReferenceValue != null) { ApplyOverviewStyle(); return; }
        var open = ButtonCopy("InheritanceOpen", start.transform, source, "계승 정보", new Vector2(0,-470));
        var panel = Panel("InheritanceOverview", start.transform, new Color32(12,15,23,255));
        Label("Title", panel.transform, sourceText, "계승 정보", new Vector2(0,430), new Vector2(1400,80), 44, TextAlignmentOptions.Center);
        Label("CardsTitle", panel.transform, sourceText, "시작 덱", new Vector2(-400,315), new Vector2(700,70), 32, TextAlignmentOptions.Left);
        Label("PassivesTitle", panel.transform, sourceText, "계승 패시브", new Vector2(400,315), new Vector2(700,70), 32, TextAlignmentOptions.Left);
        var cards = Label("Cards", panel.transform, sourceText, "", new Vector2(-400,10), new Vector2(700,510), 28, TextAlignmentOptions.TopLeft);
        var passives = Label("Passives", panel.transform, sourceText, "", new Vector2(400,10), new Vector2(700,510), 28, TextAlignmentOptions.TopLeft);
        Label("Guide", panel.transform, sourceText, "튜토리얼은 기본 편성으로 진행합니다. 계승은 본편부터 적용됩니다.\n시작 편성에서 바꾼 순서는 이번 런에만 적용됩니다.", new Vector2(0,-310), new Vector2(1600,100), 24, TextAlignmentOptions.Center);
        var status = Label("Status", panel.transform, sourceText, "", new Vector2(0,-390), new Vector2(1600,70), 24, TextAlignmentOptions.Center);
        var close = ButtonCopy("Close", panel.transform, source, "돌아가기", new Vector2(-250,-470));
        var reset = ButtonCopy("Reset", panel.transform, source, "계승 초기화", new Vector2(250,-470));
        var confirm = Panel("ResetConfirmation", panel.transform, new Color32(8,10,16,255));
        Label("Prompt", confirm.transform, sourceText, "계승한 스킬·패시브를 모두 지울까요?\n시작 덱과 능력치가 기본 상태로 돌아갑니다.", new Vector2(0,75), new Vector2(1400,220), 32, TextAlignmentOptions.Center);
        var cancel = ButtonCopy("Cancel", confirm.transform, source, "취소", new Vector2(-240,-125));
        var yes = ButtonCopy("Confirm", confirm.transform, source, "초기화", new Vector2(240,-125));
        Link(u,"inheritancePanel",panel); Link(u,"resetConfirmation",confirm);
        Link(u,"inheritanceOpen",open); Link(u,"inheritanceClose",close); Link(u,"inheritanceReset",reset);
        Link(u,"resetConfirm",yes); Link(u,"resetCancel",cancel);
        Link(u,"inheritedCards",cards); Link(u,"inheritedPassives",passives); Link(u,"inheritanceStatus",status);
        u.ApplyModifiedPropertiesWithoutUndo();
        confirm.SetActive(false); panel.SetActive(false);
        ApplyOverviewStyle();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(flow.gameObject.scene);
    }
    [MenuItem("Tools/Scroll Hunter/Apply Replacement Confirmation UI")]
    public static void ApplyReplacementConfirmation()
    {
        if (Application.isPlaying) throw new System.InvalidOperationException("Stop Play first.");
        var ui = Object.FindFirstObjectByType<BattleRewardUI>();
        var u = new SerializedObject(ui);
        var back = (Button)u.FindProperty("backButton").objectReferenceValue;
        var skip = (Button)u.FindProperty("skipButton").objectReferenceValue;
        var confirm = (Button)u.FindProperty("confirmReplacementButton").objectReferenceValue;
        if (confirm == null)
        {
            confirm = Object.Instantiate(back, back.transform.parent, false);
            confirm.name = "ConfirmReplacement";
            confirm.onClick = new Button.ButtonClickedEvent();
            confirm.GetComponentInChildren<TMP_Text>(true).text = "확인";
            Link(u, "confirmReplacementButton", confirm);
        }
        var visual = confirm.GetComponent<CanvasGroup>();
        if (visual == null) visual = confirm.gameObject.AddComponent<CanvasGroup>();
        Link(u, "confirmReplacementVisual", visual);
        u.FindProperty("replaceFormat").stringValue = "{0} 선택 · 교체할 카드를 고른 뒤 확인을 누르세요.";
        u.FindProperty("inheritanceReplaceFormat").stringValue = "{0} 계승 · 영구 시작 덱에서 교체할 자리를 고른 뒤 확인을 누르세요.";
        u.ApplyModifiedPropertiesWithoutUndo();
        var buttons = new[] { back, confirm, skip };
        for (int i = 0; i < buttons.Length; i++)
        {
            var b = buttons[i];
            var rt = (RectTransform)b.transform;
            rt.anchoredPosition = new Vector2((i - 1) * 510f, rt.anchoredPosition.y);
            var colors = b.colors;
            colors.normalColor = new Color(.26f, .32f, .42f);
            colors.highlightedColor = new Color(.35f, .43f, .55f);
            colors.pressedColor = new Color(.20f, .25f, .33f);
            colors.selectedColor = colors.normalColor;
            colors.disabledColor = new Color(.14f, .16f, .20f);
            b.colors = colors;
        }
        confirm.interactable = false;
        visual.alpha = u.FindProperty("disabledConfirmAlpha").floatValue;
        confirm.gameObject.SetActive(false);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(ui.gameObject.scene);
    }
    [MenuItem("Tools/Scroll Hunter/Style Inheritance Overview")]
    public static void ApplyOverviewStyle()
    {
        if (Application.isPlaying) throw new System.InvalidOperationException("Stop Play first.");
        var ui = Object.FindFirstObjectByType<BattleRewardUI>();
        var so = new SerializedObject(ui);
        var panel = (GameObject)so.FindProperty("inheritancePanel").objectReferenceValue;
        if (panel == null) return;
        var gold = new Color32(190,153,92,255);
        var ivory = new Color32(236,226,204,255);
        var muted = new Color32(172,172,177,255);
        panel.GetComponent<Image>().color = new Color(.02f,.025f,.04f,.9f);
        var body = Decoration(panel.transform,"OverviewBody",Vector2.zero,new Vector2(1536,916),new Color32(13,18,28,255));
        var frame = Decoration(panel.transform,"OverviewFrame",Vector2.zero,new Vector2(1550,930),gold);
        frame.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/UI/KenneyBorders/panel-border-002.png");
        frame.type = Image.Type.Sliced;
        body.transform.SetAsFirstSibling(); frame.transform.SetSiblingIndex(1);
        var title = StyleLabel(panel.transform,"Title",new Vector2(0,377),new Vector2(1300,72),46,gold);
        var divider = panel.transform.Find("TitleDivider");
        if (divider == null)
        {
            divider = Object.Instantiate(panel.transform.parent.Find("TitleDivider"),panel.transform,false);
            divider.name = "TitleDivider";
        }
        Place((RectTransform)divider,new Vector2(0,329),new Vector2(840,16));
        for (int i=0;i<2;i++)
        {
            float x = i==0 ? -370 : 370;
            var column=Decoration(panel.transform,"Column"+i,new Vector2(x,27),new Vector2(690,548),new Color32(20,28,41,255));
            column.transform.SetSiblingIndex(2);
            Decoration(panel.transform,"ColumnLine"+i,new Vector2(x,234),new Vector2(614,2),new Color32(98,80,51,255));
        }
        StyleLabel(panel.transform,"CardsTitle",new Vector2(-370,271),new Vector2(614,58),32,gold);
        StyleLabel(panel.transform,"PassivesTitle",new Vector2(370,271),new Vector2(614,58),32,gold);
        var cards=StyleLabel(panel.transform,"Cards",new Vector2(-370,-12),new Vector2(614,456),28,ivory);
        var passives=StyleLabel(panel.transform,"Passives",new Vector2(370,-12),new Vector2(614,456),28,ivory);
        cards.alignment=passives.alignment=TextAlignmentOptions.TopLeft;
        cards.lineSpacing=passives.lineSpacing=0;
        var guide=StyleLabel(panel.transform,"Guide",new Vector2(0,-298),new Vector2(1370,76),22,muted);
        guide.lineSpacing=8;
        StyleLabel(panel.transform,"Status",new Vector2(0,-352),new Vector2(1370,40),20,new Color32(233,163,129,255));
        Place((RectTransform)panel.transform.Find("Close"),new Vector2(-225,-405),new Vector2(360,72));
        Place((RectTransform)panel.transform.Find("Reset"),new Vector2(225,-405),new Vector2(360,72));
        so.FindProperty("inheritedCardFormat").stringValue="<line-height=54><size=20><color=#AAA294>{0:00}</color></size>  <size=28>{1}</size>{2}";
        so.FindProperty("inheritedCardMarker").stringValue="  <size=18><color=#E7CB87>계승</color></size>";
        so.FindProperty("inheritedPassiveFormat").stringValue="<line-height=48><size=28>{0}</size>  <size=20><color=#B9AF9E>{1}</color></size>";
        so.FindProperty("noInheritedPassives").stringValue="<size=24><color=#ABAAB0>계승한 패시브가 없습니다.</color></size>";
        so.ApplyModifiedPropertiesWithoutUndo();
        var confirm=(GameObject)so.FindProperty("resetConfirmation").objectReferenceValue;
        confirm.transform.SetAsLastSibling();
        confirm.GetComponent<Image>().color=new Color(.015f,.02f,.03f,.96f);
        var confirmBody=Decoration(confirm.transform,"DialogBody",Vector2.zero,new Vector2(1200,420),new Color32(13,18,28,255));
        var confirmFrame=Decoration(confirm.transform,"DialogFrame",Vector2.zero,new Vector2(1214,434),gold);
        confirmFrame.sprite=frame.sprite;confirmFrame.type=Image.Type.Sliced;
        confirmBody.transform.SetAsFirstSibling();confirmFrame.transform.SetSiblingIndex(1);
        var prompt=StyleLabel(confirm.transform,"Prompt",new Vector2(0,65),new Vector2(1100,150),32,ivory);
        prompt.text="<size=36>계승한 스킬·패시브를 모두 지울까요?</size>\n<size=24><color=#B9AF9E>시작 덱과 능력치가 기본 상태로 돌아갑니다.</color></size>";
        prompt.lineSpacing=16;
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(ui.gameObject.scene);
    }
    private static void Place(RectTransform rt,Vector2 position,Vector2 size)
    {
        rt.anchorMin=rt.anchorMax=rt.pivot=new Vector2(.5f,.5f);rt.anchoredPosition=position;rt.sizeDelta=size;
    }
    private static TMP_Text StyleLabel(Transform parent,string name,Vector2 position,Vector2 size,float fontSize,Color color)
    {
        var text=parent.Find(name).GetComponent<TMP_Text>();Place(text.rectTransform,position,size);
        text.fontSize=fontSize;text.enableAutoSizing=false;text.color=color;text.textWrappingMode=TextWrappingModes.Normal;
        return text;
    }
    private static Image Decoration(Transform parent,string name,Vector2 position,Vector2 size,Color color)
    {
        var child=parent.Find(name);
        var image=child!=null?child.GetComponent<Image>():new GameObject(name,typeof(RectTransform),typeof(Image)).GetComponent<Image>();
        image.transform.SetParent(parent,false);Place((RectTransform)image.transform,position,size);image.color=color;image.raycastTarget=false;
        return image;
    }

    private static void Link(SerializedObject target, string name, Object value) => target.FindProperty(name).objectReferenceValue = value;
    private static GameObject Panel(string name, Transform parent, Color color)
    {
        var go = new GameObject(name,typeof(RectTransform),typeof(Image));
        var rt = (RectTransform)go.transform; rt.SetParent(parent,false);
        rt.anchorMin=Vector2.zero; rt.anchorMax=Vector2.one; rt.offsetMin=rt.offsetMax=Vector2.zero;
        go.GetComponent<Image>().color=color;
        return go;
    }
    private static TMP_Text Label(string name, Transform parent, TMP_Text source, string text, Vector2 position, Vector2 size, float fontSize, TextAlignmentOptions alignment)
    {
        var go = new GameObject(name,typeof(RectTransform),typeof(TextMeshProUGUI));
        var t=go.GetComponent<TextMeshProUGUI>(); t.rectTransform.SetParent(parent,false);
        t.rectTransform.anchorMin=t.rectTransform.anchorMax=new Vector2(.5f,.5f);
        t.rectTransform.anchoredPosition=position; t.rectTransform.sizeDelta=size;
        t.font=source.font; t.fontSharedMaterial=source.fontSharedMaterial; t.fontSize=fontSize;
        t.color=new Color32(231,216,185,255); t.alignment=alignment; t.text=text;
        t.lineSpacing=12f; t.raycastTarget=false;
        return t;
    }
    private static Button ButtonCopy(string name, Transform parent, Button source, string text, Vector2 position)
    {
        var b=Object.Instantiate(source,parent,false); b.name=name; b.onClick=new Button.ButtonClickedEvent();
        var rt=(RectTransform)b.transform; rt.anchorMin=rt.anchorMax=new Vector2(.5f,.5f);
        rt.anchoredPosition=position; rt.sizeDelta=new Vector2(360,70);
        var label=b.GetComponentInChildren<TMP_Text>(); label.text=text; label.fontSize=28;
        b.gameObject.SetActive(true); return b;
    }
}
