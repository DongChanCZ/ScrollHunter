using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object=UnityEngine.Object;

public static class StorySetup
{
    private const string Folder="Assets/_Project/UI/Story/";
    private const string DataPath="Assets/_Project/Data/Story/OpeningStory.asset";
    [MenuItem("Tools/Scroll Hunter/Apply Opening Story")]
    public static void Apply()
    {
        if(Application.isPlaying) throw new InvalidOperationException("Stop Play first.");
        string[] names=new[]{"Intro01_War","Intro02_Scroll","Intro03_Conflict","Intro04_Peace","Intro05_Traveler","Portrait_Thug","Portrait_Raider","Portrait_Archer","Portrait_Leader","Portrait_Mage"};
        var textures=new Texture2D[names.Length];
        for(int i=0;i<names.Length;i++)
        {
            string path=Folder+names[i]+".png";
            var imp=(TextureImporter)AssetImporter.GetAtPath(path);
            if(imp==null) throw new InvalidOperationException("Missing story art: "+path);
            imp.textureType=TextureImporterType.Default;
            imp.alphaSource=TextureImporterAlphaSource.FromInput;
            imp.alphaIsTransparency=i>=5;
            imp.npotScale=TextureImporterNPOTScale.None; imp.mipmapEnabled=false; imp.wrapMode=TextureWrapMode.Clamp; imp.maxTextureSize=2048;
            imp.textureCompression=TextureImporterCompression.CompressedHQ;
            imp.SaveAndReimport();
            textures[i]=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
        var data=AssetDatabase.LoadAssetAtPath<StoryContent>(DataPath);
        if(data==null)
        {
            System.IO.Directory.CreateDirectory("Assets/_Project/Data/Story");
            data=ScriptableObject.CreateInstance<StoryContent>();
            data.intro=new StoryContent.IntroPage[]{
                new StoryContent.IntroPage { text="고대의 대전쟁 당시, 한 제국은 병사들을 강화하기 위해 대마법사에게 새로운 기술을 개발하도록 명했다. 그렇게 탄생한 것이 바로 ‘비전 스크롤’이었다." },
                new StoryContent.IntroPage { text="비전 스크롤에는 마법뿐만 아니라 신체 능력과 기술, 지식까지 담아 다른 이에게 전할 수 있었다. 병사들은 스크롤에 담긴 힘과 기술을 익혀 전장에서 활약했고, 제국은 마침내 전쟁에서 승리했다." },
                new StoryContent.IntroPage { text="하지만 누구나 강력한 힘을 얻을 수 있다는 사실은 사람들의 탐욕을 자극했다. 스크롤을 차지하려는 다툼은 제국 내부로 번졌고, 끝없는 분열 속에서 제국은 결국 몰락했다." },
                new StoryContent.IntroPage { text="오랜 세월이 흐른 지금도 비전 스크롤은 남아 있다. 그러나 이제는 새로운 기술이나 마법의 기초를 쉽게 익히는 수단으로 여겨질 뿐이다. 한때 제국의 병사들에게 막강한 힘을 부여했다는 이야기는 고대의 전설로 남았다." },
                new StoryContent.IntroPage { text="그럼에도 전설 속 힘을 찾아 스크롤을 모으고, 싸우며 모험을 이어가는 이들이 있다.\n사람들은 그들을 스크롤 헌터라 부른다." }
            };
            data.portraits=new StoryContent.Portrait[]{
                new StoryContent.Portrait{displayName="깡패"},new StoryContent.Portrait{displayName="약탈자"},
                new StoryContent.Portrait{displayName="궁병"},new StoryContent.Portrait{displayName="우두머리"},
                new StoryContent.Portrait{displayName="마법사"}
            };
            data.tutorialDialogue=new StoryContent.DialogueLine[]{
                new StoryContent.DialogueLine{speaker=(StoryContent.Speaker)0,portraitIndex=0,text="숲으로 향하는 길목에서 깡패가 앞을 가로막는다."},
                new StoryContent.DialogueLine{speaker=(StoryContent.Speaker)2,portraitIndex=0,text="어이, 거기 멈춰. 이 숲에 들어가려면 통행세부터 내야지."},
                new StoryContent.DialogueLine{speaker=(StoryContent.Speaker)1,portraitIndex=0,text="통행세? 여기 오는 동안 그런 얘기는 못 들었는데."},
                new StoryContent.DialogueLine{speaker=(StoryContent.Speaker)2,portraitIndex=0,text="그럼 지금 알았으면 됐잖아. 돈 내놔."},
                new StoryContent.DialogueLine{speaker=(StoryContent.Speaker)1,portraitIndex=0,text="길을 막고 돈을 뜯는 걸 통행세라고 하나? 낼 생각 없으니까 비켜."},
                new StoryContent.DialogueLine{speaker=(StoryContent.Speaker)2,portraitIndex=0,text="좋게 말하니까 우습게 보이나? 그럼 쓰러뜨리고 직접 챙겨 주지. 가진 건 전부 두고 가게 될 거다."},
                new StoryContent.DialogueLine{speaker=(StoryContent.Speaker)0,portraitIndex=0,text="모험가가 가볍게 웃으며 어깨를 푼다."},
                new StoryContent.DialogueLine{speaker=(StoryContent.Speaker)1,portraitIndex=0,text="마침 잘됐네. 한동안 싸울 일이 없어서 몸이 굳어 있었거든."},
                new StoryContent.DialogueLine{speaker=(StoryContent.Speaker)2,portraitIndex=0,text="뭐? 지금 웃음이 나와?"},
                new StoryContent.DialogueLine{speaker=(StoryContent.Speaker)1,portraitIndex=0,text="덕분에 몸 좀 풀고 가겠어. 덤벼."},
                new StoryContent.DialogueLine{speaker=(StoryContent.Speaker)0,portraitIndex=0,text="깡패가 주먹을 쥐고 달려든다."}
            };
            AssetDatabase.CreateAsset(data,DataPath);
        }
        if(data.forestDialogue==null || data.forestDialogue.Length==0)
        {
            data.forestDialogue=new StoryContent.DialogueLine[]{
                new StoryContent.DialogueLine{speaker=(StoryContent.Speaker)0,portraitIndex=0,text="숲속을 한참 헤매던 모험가가 갈림길 앞에서 걸음을 멈춘다."},
                new StoryContent.DialogueLine{speaker=(StoryContent.Speaker)1,portraitIndex=0,text="이 바위… 아까도 지나온 것 같은데. 길을 잘못 들었나?"},
                new StoryContent.DialogueLine{speaker=(StoryContent.Speaker)0,portraitIndex=0,text="그때, 나무 사이에서 약탈자와 궁병이 나타나 길을 가로막는다."},
                new StoryContent.DialogueLine{speaker=(StoryContent.Speaker)2,portraitIndex=1,text="거기, 멈춰. 여기서 뭘 하고 있지?"},
                new StoryContent.DialogueLine{speaker=(StoryContent.Speaker)1,portraitIndex=0,text="길을 좀 찾고 있었어. 마침 잘 만났네."},
                new StoryContent.DialogueLine{speaker=(StoryContent.Speaker)2,portraitIndex=2,text="길을 잃었으면 돌아가. 이 앞은 우리 구역이야."},
                new StoryContent.DialogueLine{speaker=(StoryContent.Speaker)0,portraitIndex=0,text="모험가는 두 사람의 차림새를 살핀다. 도적단이라면 빼앗아 모은 물건도 적지 않을 터였다. 그중에 비전 스크롤이 있을지도 모른다는 생각이 든다."},
                new StoryContent.DialogueLine{speaker=(StoryContent.Speaker)1,portraitIndex=0,text="너희 구역이라… 그럼 안쪽에 동료들도 있겠네?"},
                new StoryContent.DialogueLine{speaker=(StoryContent.Speaker)2,portraitIndex=1,text="그걸 네가 알아서 뭘 하게? 좋게 말할 때 돌아가."},
                new StoryContent.DialogueLine{speaker=(StoryContent.Speaker)1,portraitIndex=0,text="헛걸음한 줄 알았는데, 오히려 잘 찾아온 것 같아서."},
                new StoryContent.DialogueLine{speaker=(StoryContent.Speaker)2,portraitIndex=2,text="이 자식, 돌아갈 생각이 없군."},
                new StoryContent.DialogueLine{speaker=(StoryContent.Speaker)0,portraitIndex=0,text="궁병이 활에 화살을 건다. 약탈자도 단검을 뽑아 모험가를 겨눈다."},
                new StoryContent.DialogueLine{speaker=(StoryContent.Speaker)2,portraitIndex=1,text="계속 얼쩡거리면 무사히 돌아가진 못할 거다."},
                new StoryContent.DialogueLine{speaker=(StoryContent.Speaker)1,portraitIndex=0,text="그렇게 막으니까 더 궁금해지는데."},
                new StoryContent.DialogueLine{speaker=(StoryContent.Speaker)2,portraitIndex=2,text="말로 해선 안 되겠어. 처리하자!"},
                new StoryContent.DialogueLine{speaker=(StoryContent.Speaker)0,portraitIndex=0,text="모험가는 날아올 화살을 경계하며 자세를 잡는다."},
                new StoryContent.DialogueLine{speaker=(StoryContent.Speaker)1,portraitIndex=0,text="좋아. 그럼 길은 직접 열어야겠네."}
            };
        }
        if(data.leaderDialogue==null || data.leaderDialogue.Length==0)
            data.leaderDialogue=new StoryContent.DialogueLine[]{
                new StoryContent.DialogueLine{speaker=(StoryContent.Speaker)0,portraitIndex=0,text="숲길을 지나자 커다란 동굴 입구가 모습을 드러낸다. 대검을 든 남자가 그 앞을 지키고 있다."},
                new StoryContent.DialogueLine{speaker=(StoryContent.Speaker)1,portraitIndex=0,text="드디어 찾았네. 꽤 깊숙이도 숨어 있었군."},
                new StoryContent.DialogueLine{speaker=(StoryContent.Speaker)2,portraitIndex=3,text="못 보던 얼굴인데. 여기엔 무슨 용무냐?"},
                new StoryContent.DialogueLine{speaker=(StoryContent.Speaker)1,portraitIndex=0,text="물건 하나 찾으러 왔어."},
                new StoryContent.DialogueLine{speaker=(StoryContent.Speaker)2,portraitIndex=3,text="장사하는 곳 아니다. 귀찮게 하지 말고 당장 꺼져."},
                new StoryContent.DialogueLine{speaker=(StoryContent.Speaker)1,portraitIndex=0,text="여기에 꽤 값나가는 스크롤이 있다는 얘길 들었거든. 그것만 넘겨주면 조용히 떠날게."},
                new StoryContent.DialogueLine{speaker=(StoryContent.Speaker)0,portraitIndex=0,text="우두머리가 잠시 모험가를 노려보다 코웃음을 친다."},
                new StoryContent.DialogueLine{speaker=(StoryContent.Speaker)2,portraitIndex=3,text="어디서 헛소리를 주워들었는지 모르겠군. 여기엔 그런 거 없다."},
                new StoryContent.DialogueLine{speaker=(StoryContent.Speaker)1,portraitIndex=0,text="그럼 잠깐 안을 둘러봐도 괜찮겠네?"},
                new StoryContent.DialogueLine{speaker=(StoryContent.Speaker)2,portraitIndex=3,text="하! 설령 있다고 한들, 네놈한테 순순히 내줄 것 같으냐?"},
                new StoryContent.DialogueLine{speaker=(StoryContent.Speaker)0,portraitIndex=0,text="우두머리가 대검을 뽑아 들고 모험가에게 다가온다."},
                new StoryContent.DialogueLine{speaker=(StoryContent.Speaker)2,portraitIndex=3,text="좋게 보내주려고 했더니, 기어이 칼을 보겠다는 거군."},
                new StoryContent.DialogueLine{speaker=(StoryContent.Speaker)1,portraitIndex=0,text="역시 말로는 안 되겠네."},
                new StoryContent.DialogueLine{speaker=(StoryContent.Speaker)0,portraitIndex=0,text="모험가가 거리를 벌리며 전투 자세를 잡는다. 우두머리가 대검을 치켜든다."}
            };
        if(data.banditDialogue==null || data.banditDialogue.Length==0)
            data.banditDialogue=new StoryContent.DialogueLine[]{
                new StoryContent.DialogueLine{speaker=(StoryContent.Speaker)0,portraitIndex=0,text="우두머리가 거칠게 숨을 몰아쉬며 뒤로 물러선다. 여유롭던 얼굴에 당혹감이 번진다."},
                new StoryContent.DialogueLine{speaker=(StoryContent.Speaker)2,portraitIndex=3,text="크윽… 어디서 이런 놈이 굴러들어 와서는…!"},
                new StoryContent.DialogueLine{speaker=(StoryContent.Speaker)1,portraitIndex=0,text="이쯤 했으면 비켜주는 게 어때? 나도 더 싸우고 싶진 않은데."},
                new StoryContent.DialogueLine{speaker=(StoryContent.Speaker)2,portraitIndex=3,text="입 다물어!"},
                new StoryContent.DialogueLine{speaker=(StoryContent.Speaker)0,portraitIndex=0,text="우두머리가 동굴 쪽을 향해 고함을 지른다."},
                new StoryContent.DialogueLine{speaker=(StoryContent.Speaker)2,portraitIndex=3,text="전부 나와라! 당장!"},
                new StoryContent.DialogueLine{speaker=(StoryContent.Speaker)0,portraitIndex=0,text="동굴에서 도적단 말단들이 달려 나온다."},
                new StoryContent.DialogueLine{speaker=(StoryContent.Speaker)2,portraitIndex=1,secondaryPortraitIndex=2,speakerNameOverride="도적단 말단들",text="두목! 무슨 일입니까?"},
                new StoryContent.DialogueLine{speaker=(StoryContent.Speaker)2,portraitIndex=3,text="저놈을 막아라. 무슨 일이 있어도 동굴 안으로 들여보내선 안 된다!"},
                new StoryContent.DialogueLine{speaker=(StoryContent.Speaker)2,portraitIndex=1,secondaryPortraitIndex=2,speakerNameOverride="도적단 말단들",text="고작 혼자 온 놈 아닙니까? 저희가 처리하겠습니다."},
                new StoryContent.DialogueLine{speaker=(StoryContent.Speaker)2,portraitIndex=3,text="방심하지 마라! 처음부터 전력을 다해!"},
                new StoryContent.DialogueLine{speaker=(StoryContent.Speaker)0,portraitIndex=0,text="말단들이 무기를 들고 길을 막는다. 모험가는 적들을 번갈아 살피며 한 걸음 물러선다."},
                new StoryContent.DialogueLine{speaker=(StoryContent.Speaker)1,portraitIndex=0,text="이건 좀 곤란한데. 한꺼번에 상대하는 건 생각 안 했거든."},
                new StoryContent.DialogueLine{speaker=(StoryContent.Speaker)2,portraitIndex=3,text="이제야 상황을 알겠나? 도망칠 길도 없다!"},
                new StoryContent.DialogueLine{speaker=(StoryContent.Speaker)0,portraitIndex=0,text="모험가가 짧게 숨을 고르고 다시 자세를 잡는다."},
                new StoryContent.DialogueLine{speaker=(StoryContent.Speaker)1,portraitIndex=0,text="여기까지 와서 빈손으로 돌아갈 순 없지."},
                new StoryContent.DialogueLine{speaker=(StoryContent.Speaker)2,portraitIndex=3,text="쳐라!"},
                new StoryContent.DialogueLine{speaker=(StoryContent.Speaker)1,portraitIndex=0,text="좋아. 끝까지 해보자!"}
            };
        if(data.bossDialogue==null || data.bossDialogue.Length==0)
            data.bossDialogue=new StoryContent.DialogueLine[]{
                new StoryContent.DialogueLine{speaker=(StoryContent.Speaker)0,portraitIndex=0,text="동굴 안으로 들어서자 차갑고 무거운 기운이 온몸을 감싼다. 모험가는 걸음을 늦추고 어둠 속을 살핀다."},
                new StoryContent.DialogueLine{speaker=(StoryContent.Speaker)1,portraitIndex=0,text="……도적들이 이런 곳에 숨어 지낸다고?"},
                new StoryContent.DialogueLine{speaker=(StoryContent.Speaker)0,portraitIndex=0,text="동굴 깊숙한 곳에서 지팡이를 든 마법사가 혀를 찬다."},
                new StoryContent.DialogueLine{speaker=(StoryContent.Speaker)2,portraitIndex=4,text="쯧. 한때는 이름난 가문의 호위병이었다기에 기대했건만…… 침입자 하나 막지 못하다니."},
                new StoryContent.DialogueLine{speaker=(StoryContent.Speaker)1,portraitIndex=0,text="입구에 있던 덩치를 말하는 건가? 나름 애는 쓰던데."},
                new StoryContent.DialogueLine{speaker=(StoryContent.Speaker)2,portraitIndex=4,text="그래서, 네놈은 누구지? 이런 곳까지 기어들어 온 이유가 뭐냐."},
                new StoryContent.DialogueLine{speaker=(StoryContent.Speaker)1,portraitIndex=0,text="여기에 값나가는 비전 스크롤이 있다는 소문을 듣고 왔어. 당신이 가지고 있나?"},
                new StoryContent.DialogueLine{speaker=(StoryContent.Speaker)0,portraitIndex=0,text="마법사가 모험가를 훑어보더니 비웃음을 흘린다."},
                new StoryContent.DialogueLine{speaker=(StoryContent.Speaker)2,portraitIndex=4,text="스크롤의 힘을 좇아 여기까지 왔다는 건가. 헛수고했군."},
                new StoryContent.DialogueLine{speaker=(StoryContent.Speaker)1,portraitIndex=0,text="헛수고라니?"},
                new StoryContent.DialogueLine{speaker=(StoryContent.Speaker)2,portraitIndex=4,text="그 스크롤이라면 이미 내가 사용했다. 그 안에 담긴 지식은 내 연구에 제법 도움이 되었지."},
                new StoryContent.DialogueLine{speaker=(StoryContent.Speaker)1,portraitIndex=0,text="……한발 늦었네. 그럼 밖에 있던 녀석들이 지키던 건 스크롤이 아니라 당신이었군."},
                new StoryContent.DialogueLine{speaker=(StoryContent.Speaker)2,portraitIndex=4,text="쓸데없는 것들이 몰려들지 않도록 맡겨 두었을 뿐이다. 그조차 제대로 해내지 못했지만."},
                new StoryContent.DialogueLine{speaker=(StoryContent.Speaker)1,portraitIndex=0,text="그 쓸데없는 것에 나도 포함되는 모양이네."},
                new StoryContent.DialogueLine{speaker=(StoryContent.Speaker)0,portraitIndex=0,text="마법사가 지팡이를 들어 올리자 보라색 보석에서 불길한 빛이 새어 나온다."},
                new StoryContent.DialogueLine{speaker=(StoryContent.Speaker)2,portraitIndex=4,text="네놈 때문에 중요한 연구가 중단됐다. 아무 대가도 치르지 않고 돌아갈 수 있을 거라 생각했나?"},
                new StoryContent.DialogueLine{speaker=(StoryContent.Speaker)1,portraitIndex=0,text="빈손으로 돌아가는 것도 억울한데, 목숨까지 내놓으라고?"},
                new StoryContent.DialogueLine{speaker=(StoryContent.Speaker)2,portraitIndex=4,text="마침 새로 익힌 마법을 시험할 상대가 필요했지. 네놈이라도 쓸모를 보여 봐라."},
                new StoryContent.DialogueLine{speaker=(StoryContent.Speaker)0,portraitIndex=0,text="모험가가 마법사를 경계하며 자세를 잡는다."},
                new StoryContent.DialogueLine{speaker=(StoryContent.Speaker)1,portraitIndex=0,text="좋아. 그 스크롤로 얼마나 대단해졌는지 한번 보자고."}
            };
        for(int i=0;i<5;i++){data.intro[i].image=textures[i];data.portraits[i].image=textures[5+i];}
        ConfigureIntroPresentation(data);
        EditorUtility.SetDirty(data);AssetDatabase.SaveAssetIfDirty(data);
        var flow=Object.FindFirstObjectByType<BattleFlow>();
        var fs=new SerializedObject(flow);
        var button=(Button)fs.FindProperty("startButton").objectReferenceValue;
        var canvas=button.GetComponentInParent<Canvas>().transform;
        var source=button.GetComponentInChildren<TMP_Text>(true);
        var ui=flow.GetComponent<StoryUI>();if(ui==null)ui=flow.gameObject.AddComponent<StoryUI>();
        var so=new SerializedObject(ui);
        Link(so,"bossBackground",AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Environment/Materials/04_CaveInterior.mat").mainTexture);
        Link(so,"caveEntranceBackground",AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Environment/Materials/03_CaveEntrance.mat").mainTexture);
        Link(so,"forestBackground",AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Environment/Materials/02_ForestTrail.mat").mainTexture);
        Link(so,"dialogueBackground",AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Environment/Materials/01_ForestEdge.mat").mainTexture);
        Link(so,"content",data); Link(so,"canvasRoot",canvas);Link(so,"buttonTemplate",button);
        Link(so,"font",source.font);Link(so,"fontMaterial",source.fontSharedMaterial);
        Link(so,"panelSprite",AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/UI/FantasyTrial/Button.asset"));
        Link(so,"frameSprite",AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/UI/KenneyBorders/panel-border-002.png"));
        string[] hudNames={"EnemyBars","TargetGaugeLine","SanctuaryFrame","MagicArmorVfx","HandRoot","PlayerHud","NextCardText","StunText","TargetMarker","CastBar","CombatInformation","RunProgress","StatsWindow","DamageNumbers"};
        var hud=so.FindProperty("combatHud");hud.arraySize=hudNames.Length;
        for(int i=0;i<hudNames.Length;i++){var obj=canvas.Find(hudNames[i]);hud.GetArrayElementAtIndex(i).objectReferenceValue=obj!=null?obj.gameObject:null;}
        so.ApplyModifiedPropertiesWithoutUndo();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(flow.gameObject.scene);
    }
    public static void ConfigureIntroPresentation(StoryContent data)
    {
        if (data.introPresentation == null) data.introPresentation = new IntroPresentation.Settings();
        if (data.introPresentation.artworkShader != null) return;
        data.introPresentation.artworkShader = AssetDatabase.LoadAssetAtPath<Shader>(Folder + "IntroArtwork.shader");
        var styles = new[] { IntroPresentation.Style.War, IntroPresentation.Style.Scroll, IntroPresentation.Style.Conflict,
            IntroPresentation.Style.Peace, IntroPresentation.Style.Traveler };
        for (int i = 0; i < data.intro.Length && i < styles.Length; i++)
        {
            var page = data.intro[i]; page.style = styles[i];
            page.startZoom = i == 3 ? 1.04f : 1f; page.endZoom = 1.04f;
            page.focusFrom = i == 3 ? new Vector2(.2f,.5f) : new Vector2(.5f,.5f);
            page.focusTo = i == 3 ? new Vector2(.8f,.5f) : i == 4 ? new Vector2(.65f,.55f) : new Vector2(.5f,.55f);
            page.blackHold = i == 2 ? .18f : 0f;
            page.emphasizeLastLine = i == 4;
        }
    }

    private static void Link(SerializedObject so,string name,Object value)=>so.FindProperty(name).objectReferenceValue=value;

    // 한 번의 Play에서만 임시 저장을 사용한다. 종료 후 실제 저장으로 돌아간다.
    [MenuItem("Tools/Scroll Hunter/Preview Opening Story (Play)")]
    public static void Preview()=>Preview(false);
    [MenuItem("Tools/Scroll Hunter/Preview Forest Story (Play)")]
    public static void PreviewForest()=>Preview(true);
    private static void Preview(bool forest)
    {
        if(!Application.isPlaying) throw new InvalidOperationException("Enter Play first.");
        var flow=Object.FindFirstObjectByType<BattleFlow>();
        flow.GetComponent<MainMenuUI>().BackToTitle();
        var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
        var pool=(BattleRewardOption[])typeof(BattleFlow).GetField("rewardPool",flags).GetValue(flow);
        var deck=Object.FindFirstObjectByType<DeckSystem>();
        string path=System.IO.Path.Combine(Application.temporaryCachePath,"StoryPreview-"+Guid.NewGuid().ToString("N")+".json");
        typeof(BattleFlow).GetField("inheritance",flags).SetValue(flow,new InheritanceSave(path,pool,deck.CopyStartingDeck()));
        if(forest){flow.Inheritance.TryRecordIntro();flow.Inheritance.TryRecordTutorial();}
        flow.StartRun();
        Debug.Log("[인트로 미리보기] 이 Play는 임시 저장을 사용합니다. 종료하면 실제 진행 기록으로 돌아갑니다.");
    }
}