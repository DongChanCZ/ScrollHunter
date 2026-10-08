using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class MainMenuSetup
{
    private const string Folder = "Assets/_Project/UI/TitleMenu/";
    [MenuItem("Tools/Scroll Hunter/Apply Main Menu")]
    public static void Apply()
    {
        if (Application.isPlaying) throw new System.InvalidOperationException("Stop Play first.");
        var f = Object.FindFirstObjectByType<BattleFlow>();
        if (f.GetComponent<MainMenuUI>() != null) return;
        var flow = new SerializedObject(f);
        var start = (GameObject)flow.FindProperty("startPanel").objectReferenceValue;
        var button = (Button)flow.FindProperty("startButton").objectReferenceValue;
        var reward = new SerializedObject(flow.FindProperty("rewardUI").objectReferenceValue);
        var inherited = (Button)reward.FindProperty("inheritanceOpen").objectReferenceValue;
        var overview = (GameObject)reward.FindProperty("inheritancePanel").objectReferenceValue;
        var source = button.GetComponentInChildren<TMP_Text>(true);
        var parent = start.transform;
        var ui = f.gameObject.AddComponent<MainMenuUI>();
        var so = new SerializedObject(ui);
        Link(so,"titlePanel",start); Link(so,"inheritancePanel",overview); Link(so,"inheritanceOpen",inherited);

        var texturePath = Folder + "ScrollTable_Cartoon.png";
        var importer = (TextureImporter)AssetImporter.GetAtPath(texturePath);
        importer.textureType = TextureImporterType.Default; importer.mipmapEnabled = false;
        importer.wrapMode = TextureWrapMode.Clamp; importer.maxTextureSize = 2048;
        importer.textureCompression = TextureImporterCompression.Uncompressed; importer.SaveAndReimport();
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
        var image = new GameObject("ScrollBackground",typeof(RectTransform),typeof(RawImage),typeof(AspectRatioFitter)).GetComponent<RawImage>();
        image.transform.SetParent(parent,false); image.transform.SetAsFirstSibling();
        image.texture = texture; image.raycastTarget = false;
        var ratio = image.GetComponent<AspectRatioFitter>();ratio.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;ratio.aspectRatio = (float)texture.width/texture.height;
        var material = new Material(Shader.Find("ScrollHunter/UI/TitleRuneGlow"));
        AssetDatabase.CreateAsset(material,Folder+"TitleRuneGlow.mat");image.material=material;Link(so,"background",image);
        Link(so,"runeMaskShader",AssetDatabase.LoadAssetAtPath<Shader>(Folder+"TitleRuneMask.shader"));
        parent.Find("Vignette").gameObject.SetActive(false);
        Place((RectTransform)parent.Find("Title"),new Vector2(-510,330),new Vector2(860,110));
        Place((RectTransform)parent.Find("Subtitle"),new Vector2(-510,225),new Vector2(830,70));
        Place((RectTransform)parent.Find("TitleDivider"),new Vector2(-510,282),new Vector2(640,16));
        Place((RectTransform)button.transform,new Vector2(-510,110),new Vector2(440,86));
        SetButtonLabel(button,"게임 시작");
        var controls = CopyButton("ControlsOpen",parent,button,"조작 정보",new Vector2(-510,0),new Vector2(440,86));
        Place((RectTransform)inherited.transform,new Vector2(-510,-110),new Vector2(440,86));SetButtonLabel(inherited,"계승 정보");
        var options = CopyButton("OptionsOpen",parent,button,"옵     션",new Vector2(-510,-220),new Vector2(440,86));
        var quit = CopyButton("Quit",parent,button,"게임 종료",new Vector2(-510,-330),new Vector2(440,86));
        Link(so,"controlsOpen",controls);Link(so,"optionsOpen",options);Link(so,"quitButton",quit);

        var controlsModal = Modal("ControlsModal",parent);
        Link(so,"controlsPanel",controlsModal);
        Label("Title",controlsModal.transform,source,"조작 정보",new Vector2(0,360),new Vector2(1200,80),44,TextAlignmentOptions.Center);
        var oldControls = (RectTransform)parent.Find("ControlsPanel");
        oldControls.SetParent(controlsModal.transform,false);Place(oldControls,new Vector2(0,45),new Vector2(1400,465));
        var labels = oldControls.Find("Labels").GetComponent<TMP_Text>();
        var values = oldControls.Find("Values").GetComponent<TMP_Text>();
        Place(labels.rectTransform,new Vector2(-500,0),new Vector2(260,430));
        Place(values.rectTransform,new Vector2(180,0),new Vector2(1040,430));
        labels.alignment=TextAlignmentOptions.MidlineRight;values.alignment=TextAlignmentOptions.MidlineLeft;
        labels.fontSize=values.fontSize=28; labels.lineSpacing=values.lineSpacing=0;
        labels.text="<line-height=56>카드 사용\n대상 선택\n카드 정보\n일시정지·재개\n배속 변경\n포션 사용\n능력치";
        values.text="<line-height=56>Q / W / E / R 또는 카드 좌클릭\n적 좌클릭 또는 좌우 방향키\n카드에 마우스 올리기\nA 또는 우클릭 / 상단 정지·재개 버튼\nS 또는 상단 속도 버튼 (1배속 / 0.5배속)\nShift\n전투 중 우측 상단 버튼 (게임은 멈추지 않음)";
        var hint=parent.Find("FlowHint");
        if(hint!=null)Object.DestroyImmediate(hint.gameObject);
        Link(so,"controlsClose",CopyButton("Close",controlsModal.transform,button,"돌아가기",new Vector2(0,-365),new Vector2(360,76)));

        var optionsModal = Modal("OptionsModal",parent);Link(so,"optionsPanel",optionsModal);
        Label("Title",optionsModal.transform,source,"옵션",new Vector2(0,360),new Vector2(1000,80),44,TextAlignmentOptions.Center);
        Label("Mode",optionsModal.transform,source,"화면 모드",new Vector2(-435,240),new Vector2(270,70),30,TextAlignmentOptions.Left);
        var mode = CopyButton("ScreenMode",optionsModal.transform,button,"전체화면 (테두리 없음)",new Vector2(170,240),new Vector2(590,76));
        Link(so,"modeButton",mode);Link(so,"modeLabel",mode.GetComponentInChildren<TMP_Text>());
        Label("Resolution",optionsModal.transform,source,"해상도",new Vector2(-435,145),new Vector2(270,70),30,TextAlignmentOptions.Left);
        Link(so,"resolutionLabel",Label("ResolutionValue",optionsModal.transform,source,"",new Vector2(170,145),new Vector2(360,70),30,TextAlignmentOptions.Center));
        Link(so,"resolutionPrevious",CopyButton("PreviousResolution",optionsModal.transform,button,"이전",new Vector2(-70,145),new Vector2(115,70)));
        Link(so,"resolutionNext",CopyButton("NextResolution",optionsModal.transform,button,"다음",new Vector2(410,145),new Vector2(115,70)));
        Label("DisplayHint",optionsModal.transform,source,"전체화면은 모니터에 맞춰 표시됩니다.",new Vector2(0,70),new Vector2(1200,50),24,TextAlignmentOptions.Center);
        var names = new[]{"배경음", "스킬 사운드", "적 사운드"};
        var sliders = so.FindProperty("volumeSliders");var percentages = so.FindProperty("volumeLabels");sliders.arraySize=percentages.arraySize=names.Length;
        for(int i=0;i<names.Length;i++)
        {
            float y=-20-i*95;
            Label("SoundLabel"+i,optionsModal.transform,source,names[i],new Vector2(-435,y),new Vector2(270,70),30,TextAlignmentOptions.Left);
            var slider=MakeSlider(optionsModal.transform,"Volume"+i,new Vector2(130,y));
            sliders.GetArrayElementAtIndex(i).objectReferenceValue=slider;
            percentages.GetArrayElementAtIndex(i).objectReferenceValue=Label("VolumeValue"+i,optionsModal.transform,source,"100%",new Vector2(465,y),new Vector2(160,70),28,TextAlignmentOptions.Center);
        }
        Label("Hint",optionsModal.transform,source,"적용을 누르면 설정이 저장됩니다.",new Vector2(0,-290),new Vector2(1200,50),24,TextAlignmentOptions.Center);
        Link(so,"optionsCancel",CopyButton("Cancel",optionsModal.transform,button,"취소",new Vector2(-230,-365),new Vector2(360,76)));
        Link(so,"optionsApply",CopyButton("Apply",optionsModal.transform,button,"적용",new Vector2(230,-365),new Vector2(360,76)));
        Link(so,"mixer",CreateMixer());
        so.ApplyModifiedPropertiesWithoutUndo();controlsModal.SetActive(false);optionsModal.SetActive(false);
        overview.transform.SetAsLastSibling();
        AssetDatabase.SaveAssets();UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(f.gameObject.scene);
    }

    private static AudioMixer CreateMixer()
    {
        const string path="Assets/_Project/UI/TitleMenu/GameAudio.mixer";
        var existing=AssetDatabase.LoadAssetAtPath<AudioMixer>(path);if(existing!=null)return existing;
        var type=typeof(Editor).Assembly.GetType("UnityEditor.Audio.AudioMixerController");
        var flags=BindingFlags.Static|BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
        var mixer=type.GetMethod("CreateMixerControllerAtPath",flags).Invoke(null,new object[]{path});
        var master=type.GetProperty("masterGroup").GetValue(mixer);
        var exposedProperty=type.GetProperty("exposedParameters");
        var exposedType=exposedProperty.PropertyType.GetElementType();
        var parameters=System.Array.CreateInstance(exposedType,3);
        var names=new[]{"Music","Skill","Enemy"};
        for(int i=0;i<names.Length;i++)
        {
            var group=type.GetMethod("CreateNewGroup",flags).Invoke(mixer,new object[]{names[i],false});
            type.GetMethod("AddChildToParent",flags).Invoke(mixer,new[]{group,master});
            var guid=group.GetType().GetMethod("GetGUIDForVolume",flags).Invoke(group,null);
            var parameter=System.Activator.CreateInstance(exposedType);
            exposedType.GetField("guid").SetValue(parameter,guid);exposedType.GetField("name").SetValue(parameter,names[i]+"Volume");parameters.SetValue(parameter,i);
        }
        exposedProperty.SetValue(mixer,parameters);
        EditorUtility.SetDirty((Object)mixer);
        return (AudioMixer)mixer;
    }
    private static void Link(SerializedObject so,string name,Object value)=>so.FindProperty(name).objectReferenceValue=value;
    private static void Place(RectTransform rt,Vector2 position,Vector2 size)
    {
        rt.anchorMin=rt.anchorMax=rt.pivot=new Vector2(.5f,.5f);rt.anchoredPosition=position;rt.sizeDelta=size;
    }
    private static void SetButtonLabel(Button b,string text)
    {
        var t=b.GetComponentInChildren<TMP_Text>(true);t.text=text;t.fontSize=30;t.alignment=TextAlignmentOptions.Center;
        t.rectTransform.anchorMin=Vector2.zero;t.rectTransform.anchorMax=Vector2.one;
        t.rectTransform.offsetMin=new Vector2(14,4);t.rectTransform.offsetMax=new Vector2(-14,-4);
    }
    private static Button CopyButton(string name,Transform parent,Button source,string text,Vector2 pos,Vector2 size)
    {
        var b=Object.Instantiate(source,parent,false);b.name=name;b.onClick=new Button.ButtonClickedEvent();
        Place((RectTransform)b.transform,pos,size);SetButtonLabel(b,text);b.gameObject.SetActive(true);return b;
    }
    private static GameObject Modal(string name,Transform parent)
    {
        var bg=Box(name,parent,new Color(.02f,.025f,.04f,.88f));
        var rt=(RectTransform)bg.transform;rt.anchorMin=Vector2.zero;rt.anchorMax=Vector2.one;rt.offsetMin=rt.offsetMax=Vector2.zero;
        var frame=Box("Frame",bg.transform,new Color(.42f,.33f,.19f,1));Place((RectTransform)frame.transform,Vector2.zero,new Vector2(1544,924));
        var panel=Box("Body",bg.transform,new Color(.035f,.045f,.064f,1));Place((RectTransform)panel.transform,Vector2.zero,new Vector2(1536,916));
        return bg;
    }
    private static GameObject Box(string name,Transform parent,Color color)
    {
        var go=new GameObject(name,typeof(RectTransform),typeof(Image));go.transform.SetParent(parent,false);go.GetComponent<Image>().color=color;return go;
    }
    private static TMP_Text Label(string name,Transform parent,TMP_Text source,string text,Vector2 pos,Vector2 size,float fontSize,TextAlignmentOptions alignment)
    {
        var t=new GameObject(name,typeof(RectTransform),typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();t.transform.SetParent(parent,false);Place(t.rectTransform,pos,size);
        t.font=source.font;t.fontSharedMaterial=source.fontSharedMaterial;t.color=new Color32(234,221,195,255);t.fontSize=fontSize;t.text=text;t.alignment=alignment;t.raycastTarget=false;
        t.textWrappingMode=TextWrappingModes.Normal;return t;
    }
    private static Slider MakeSlider(Transform parent,string name,Vector2 pos)
    {
        var root=new GameObject(name,typeof(RectTransform),typeof(Slider));root.transform.SetParent(parent,false);Place((RectTransform)root.transform,pos,new Vector2(480,46));
        var background=Box("Track",root.transform,new Color(.12f,.15f,.2f,1));Place((RectTransform)background.transform,Vector2.zero,new Vector2(480,12));
        var fillArea=new GameObject("FillArea",typeof(RectTransform)).GetComponent<RectTransform>();fillArea.SetParent(root.transform,false);
        fillArea.anchorMin=new Vector2(0,.5f);fillArea.anchorMax=new Vector2(1,.5f);fillArea.sizeDelta=new Vector2(0,12);
        var fill=Box("Fill",fillArea,new Color(.64f,.51f,.3f,1));var fr=(RectTransform)fill.transform;fr.anchorMin=Vector2.zero;fr.anchorMax=Vector2.one;fr.offsetMin=fr.offsetMax=Vector2.zero;
        var handleArea=new GameObject("HandleArea",typeof(RectTransform)).GetComponent<RectTransform>();handleArea.SetParent(root.transform,false);
        handleArea.anchorMin=new Vector2(0,.5f);handleArea.anchorMax=new Vector2(1,.5f);handleArea.sizeDelta=Vector2.zero;
        var handle=Box("Handle",handleArea,new Color(.86f,.74f,.5f,1));Place((RectTransform)handle.transform,Vector2.zero,new Vector2(22,38));
        root.AddComponent<Image>().color=Color.clear;
        var slider=root.GetComponent<Slider>();slider.fillRect=fr;slider.handleRect=(RectTransform)handle.transform;slider.targetGraphic=handle.GetComponent<Image>();slider.minValue=0;slider.maxValue=1;slider.value=1;return slider;
    }
}
