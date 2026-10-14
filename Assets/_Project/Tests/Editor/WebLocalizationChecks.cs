using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class WebLocalizationChecks
{
    private const BindingFlags Hidden = BindingFlags.NonPublic | BindingFlags.Instance;
    private static object Get(object o,string n) => o.GetType().GetField(n,Hidden).GetValue(o);
    private static void Set(object o,string n,object v) => o.GetType().GetField(n,Hidden).SetValue(o,v);
    private static object Call(object o,string n,params object[] args) => o.GetType().GetMethod(n,Hidden).Invoke(o,args);
    private static bool Hangul(string s) => Regex.IsMatch(s ?? "", "[가-힣]");

    [MenuItem("Tools/Scroll Hunter/Check Web Localization (Fresh Play)")]
    public static void Run()
    {
        if(!Application.isPlaying) throw new Exception("Fresh Play required");
        var flow=UnityEngine.Object.FindFirstObjectByType<BattleFlow>();
        if(flow.State!=BattleFlowState.Title) throw new Exception("Start at title");
        var menu=flow.GetComponent<MainMenuUI>();var story=flow.GetComponent<StoryUI>();
        var info=UnityEngine.Object.FindFirstObjectByType<CombatInfoUI>();
        var tutorial=UnityEngine.Object.FindFirstObjectByType<TutorialFlow>(FindObjectsInactive.Include);
        var save=flow.Inheritance;
        bool language=GameText.English,hadPreference=PlayerPrefs.HasKey(GameText.PreferenceKey);
        int preference=PlayerPrefs.GetInt(GameText.PreferenceKey);
        int count=0; var warnings=new List<string>();
        Action<bool,string> check=(ok,message)=>{if(!ok)throw new Exception(message);count++;};
        Action<TMP_Text,string> textCheck=(text,context)=>{
            text.ForceMeshUpdate(true);
            check(!Hangul(text.text),context+": untranslated "+text.text);
            if(text.isTextOverflowing) warnings.Add(context+": overflow "+text.text);
        };
        try
        {
            menu.ClosePanels();
            GameText.SetEnglish(true);
            var entries=Newtonsoft.Json.Linq.JObject.Parse(Resources.Load<TextAsset>("GameText").text)["entries"];
            foreach(var entry in entries)
            {
                var ko=(string)entry["ko"];var en=(string)entry["en"];
                check(GameText.T(ko)==en,"catalog: "+ko);
                check(!Hangul(en),"English contains Hangul: "+ko);
            }
            foreach(bool english in new[]{false,true})
            {
                GameText.SetEnglish(english);
                foreach(var title in new[]{"Scroll Hunter","FOREST STAGE CLEAR","DESERT STAGE","COMING SOON...?","Space","Shift"})
                    check(GameText.T(title)==title,"Original English changed: "+title);
                check(GameText.T("게임 시작")== (english?"Start Game":"게임 시작"),"two-way menu text");
            }
            foreach(var guid in AssetDatabase.FindAssets("t:SkillData",new[]{"Assets/_Project/Data"}))
            {
                var card=AssetDatabase.LoadAssetAtPath<SkillData>(AssetDatabase.GUIDToAssetPath(guid));
                check(!Hangul(card.DisplayName),"skill name "+card.name);
                check(!Hangul(info.DescribeCard(card)),"skill description "+card.name+" "+info.DescribeCard(card));
            }
            foreach(var guid in AssetDatabase.FindAssets("t:EnemyData",new[]{"Assets/_Project/Data"}))
            {
                var enemy=AssetDatabase.LoadAssetAtPath<EnemyData>(AssetDatabase.GUIDToAssetPath(guid));
                check(!Hangul(enemy.DisplayName+enemy.PassiveDescription),"enemy "+enemy.name);
                foreach(var attack in enemy.Attacks) check(!Hangul(attack.SkillName),"attack "+enemy.name);
            }
            foreach(var guid in AssetDatabase.FindAssets("t:RunRewardEffect",new[]{"Assets/_Project/Data"}))
            {
                var effect=AssetDatabase.LoadAssetAtPath<RunRewardEffect>(AssetDatabase.GUIDToAssetPath(guid));
                check(!Hangul(effect.DisplayName+effect.Description),"passive "+effect.name);
                check(!Hangul((string)Call(UnityEngine.Object.FindFirstObjectByType<BattleRewardUI>(FindObjectsInactive.Include),"DescribePassiveEffect",effect)),"passive details "+effect.name);
            }
            story.ShowIntro(()=>{});
            for(int i=0;i<story.Content.intro.Length;i++)
            {
                Set(story,"<PageIndex>k__BackingField",i);Call(story,"DrawPage");
                textCheck((TMP_Text)Get(story,"body"),"intro "+i);
            }
            story.Cancel();
            var sets=new[]{story.Content.tutorialDialogue,story.Content.forestDialogue,story.Content.leaderDialogue,story.Content.banditDialogue,story.Content.bossDialogue};
            for(int section=0;section<sets.Length;section++)
            {
                if(section==0)story.ShowTutorialDialogue(()=>{});else story.ShowBattleDialogue(section-1,()=>{});
                for(int i=0;i<sets[section].Length;i++)
                {
                    Set(story,"<PageIndex>k__BackingField",i);Call(story,"DrawPage");
                    textCheck((TMP_Text)Get(story,"body"),"dialogue "+section+"/"+i);
                    textCheck((TMP_Text)Get(story,"speakerName"),"speaker "+section+"/"+i);
                    check(story.IsTyping==(sets[section][i].speaker!=StoryContent.Speaker.Narrator),"narration/typewriter "+section+"/"+i);
                }
                GameText.SetEnglish(false);GameText.SetEnglish(true);
                check(!story.IsTyping,"language change reveals current dialogue");
                story.Cancel();
            }
            if(tutorial!=null)
            {
                for(int lesson=1;lesson<=7;lesson++)
                {
                    Call(tutorial,"ShowLesson",lesson);Call(tutorial,"LateUpdate");
                    textCheck((TMP_Text)Get(tutorial,"guideText"),"tutorial "+lesson);
                    GameText.SetEnglish(false);check(Hangul(((TMP_Text)Get(tutorial,"guideText")).text),"tutorial restores Korean");
                    GameText.SetEnglish(true);
                }
                tutorial.Cancel();
            }
            var isolated=new InheritanceSave(Path.Combine(Path.GetTempPath(),"ScrollHunter_Web_20261014","test-save-"+Guid.NewGuid().ToString("N")+".json"),
                (BattleRewardOption[])Get(flow,"rewardPool"),UnityEngine.Object.FindFirstObjectByType<DeckSystem>().CopyStartingDeck());
            check(isolated.TryRecordIntro()&&isolated.TryRecordTutorial(),"isolated progress write");
            Set(flow,"inheritance",isolated);flow.StartRun();
            check(flow.State==BattleFlowState.Preparing,"preparation after tutorial record");
            foreach(var field in new[]{"deckSlotLabels","deckNameLabels","deckInfoLabels","deckLabels"})
                foreach(var text in (TMP_Text[])Get(UnityEngine.Object.FindFirstObjectByType<BattleRewardUI>(FindObjectsInactive.Include),field)) textCheck(text,"deck "+field);
            GameText.SetEnglish(false);check(Hangul(((TMP_Text[])Get(UnityEngine.Object.FindFirstObjectByType<BattleRewardUI>(FindObjectsInactive.Include),"deckNameLabels"))[0].text),"deck restores Korean");
            GameText.SetEnglish(true);
            flow.ReturnToTitle();
            menu.OpenOptions();GameText.SetEnglish(true);
            check(!((Button)Get(menu,"quitButton")).gameObject.activeSelf,"web quit hidden");
            foreach(var field in new[]{"modeButton","resolutionDropdown","vSyncToggle","frameLimitDropdown"})
                check(!((Component)Get(menu,field)).gameObject.activeSelf,"web control hidden: "+field);
            check(Application.targetFrameRate==60 && QualitySettings.vSyncCount==0,"web internal 60 fps cap");
            ((TMP_Dropdown)Get(menu,"languageDropdown")).SetValueWithoutNotify(1);
            PlayerPrefs.SetInt(GameText.PreferenceKey,0);menu.ClosePanels();
            check(!GameText.English,"cancel restores saved language");
            GameText.SetEnglish(true);GameText.Save();GameText.SetEnglish(false);GameText.LoadSaved();
            check(GameText.English,"language save/load");
            var result=new{passed=count,warnings=warnings.ToArray(),platform=EditorUserBuildSettings.activeBuildTarget.ToString()};
            File.WriteAllText("Docs/근거자료/2026-10-14_웹빌드_언어/localization-checks.json",Newtonsoft.Json.JsonConvert.SerializeObject(result,Newtonsoft.Json.Formatting.Indented));
            Debug.Log("[Web Localization] "+count+" passed; layout warnings "+warnings.Count);
        }
        finally
        {
            story.Cancel();if(tutorial!=null)tutorial.Cancel();flow.ReturnToTitle();Set(flow,"inheritance",save);
            if(hadPreference)PlayerPrefs.SetInt(GameText.PreferenceKey,preference);else PlayerPrefs.DeleteKey(GameText.PreferenceKey);
            PlayerPrefs.Save();GameText.SetEnglish(language);
        }
    }
}
