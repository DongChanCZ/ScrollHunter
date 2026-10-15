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
    private static object Call(object o,string n,params object[] args) => o.GetType().GetMethod(n,Hidden|BindingFlags.Public).Invoke(o,args);
    private static bool Hangul(string s) => Regex.IsMatch(s ?? "", "[가-힣]");
    private static readonly GameLanguage[] Translations={GameLanguage.English,GameLanguage.Japanese,GameLanguage.ChineseSimplified,GameLanguage.Spanish};
    private static readonly string[] Fields={"ko","en","ja","zh","es"};
    private static string Signature(string s,string pattern)=>string.Join("|",Regex.Matches(s??"",pattern).Cast<Match>().Select(m=>m.Value).OrderBy(v=>v,StringComparer.Ordinal));
    private const string Placeholder=@"\{\d+(:[^}]*)?\}", Tag=@"<[^>]+>";

    [MenuItem("Tools/Scroll Hunter/Check Web Localization (Fresh Play)")]
    public static void Run()
    {
        if(!Application.isPlaying) throw new Exception("Fresh Play required");
        var flow=UnityEngine.Object.FindFirstObjectByType<BattleFlow>();
        if(flow.State!=BattleFlowState.Title) throw new Exception("Start at title");
        var menu=flow.GetComponent<MainMenuUI>();var story=flow.GetComponent<StoryUI>();
        var info=UnityEngine.Object.FindFirstObjectByType<CombatInfoUI>();
        var tutorial=UnityEngine.Object.FindFirstObjectByType<TutorialFlow>(FindObjectsInactive.Include);
        var rewardUI=UnityEngine.Object.FindFirstObjectByType<BattleRewardUI>(FindObjectsInactive.Include);
        var save=flow.Inheritance;
        var language=GameText.Current; bool hadPreference=PlayerPrefs.HasKey(GameText.PreferenceKey);
        int preference=PlayerPrefs.GetInt(GameText.PreferenceKey);
        int count=0; var warnings=new List<string>(); var missingGlyphs=new List<string>();
        Application.LogCallback glyphLog=(message,trace,type)=>{ if(message.Contains("was not found in the")) missingGlyphs.Add(message); };
        Application.logMessageReceived+=glyphLog;
        Action<bool,string> check=(ok,message)=>{if(!ok)throw new Exception(message);count++;};
        Action<TMP_Text,string> textCheck=(text,context)=>{
            text.ForceMeshUpdate(true);
            context=GameText.Current+" "+context;
            check(!Hangul(text.text),context+": untranslated "+text.text);
            check(text.font==LanguageFonts.Current||!new[]{"GmarketSansMedium SDF","NotoSansJP-Medium SDF","NotoSansSC-Medium SDF"}.Contains(text.font.name),context+": font "+text.font.name);
            for(int i=0;i<text.textInfo.characterCount;i++)
                check(text.textInfo.characterInfo[i].character!='□',context+": missing glyph in "+text.text);
            if(text.isTextOverflowing) warnings.Add(context+": overflow "+text.text);
        };
        try
        {
            menu.ClosePanels();
            // 1) 문구표: 5개 언어 모두 채움, 서식 인자·TMP 태그·줄바꿈·앞뒤 공백 유지, 각 언어 폰트에 글자 존재
            var entries=Newtonsoft.Json.Linq.JObject.Parse(Resources.Load<TextAsset>("GameText").text)["entries"];
            var fonts=Resources.Load<LanguageFontSet>("LanguageFonts");
            check(fonts!=null&&fonts.FontFor(GameLanguage.Korean)==fonts.BaseFont&&fonts.FontFor(GameLanguage.English)==fonts.BaseFont,"Korean/English keep base font");
            foreach(var entry in entries)
            {
                var ko=(string)entry["ko"];
                foreach(var lang in Translations)
                {
                    var value=(string)entry[Fields[(int)lang]];
                    check(!string.IsNullOrEmpty(value),lang+" missing: "+ko);
                    check(GameText.T(ko,lang)==value,"catalog lookup "+lang+": "+ko);
                    check(!Hangul(value),lang+" contains Hangul: "+ko);
                    if(lang==GameLanguage.English) continue; // 기존 영어는 2026-10-14 확정본을 유지한다.
                    check(Signature(value,Placeholder)==Signature(ko,Placeholder),lang+" placeholders: "+ko+" => "+value);
                    check(Signature(value,Tag)==Signature(ko,Tag),lang+" tags: "+ko+" => "+value);
                    check(value.Count(c=>c=='\n')==ko.Count(c=>c=='\n'),lang+" line breaks: "+ko);
                    check(value.Length-value.TrimStart(' ').Length==ko.Length-ko.TrimStart(' ').Length&&value.EndsWith(" ")==ko.EndsWith(" "),lang+" edge spaces: "+ko);
                    try{string.Format(System.Globalization.CultureInfo.InvariantCulture,value,1,2,3,4,5,6,7,8);count++;}
                    catch(FormatException){throw new Exception(lang+" format: "+value);}
                    var font=fonts.FontFor(lang);
                    foreach(char c in Regex.Replace(value,Tag,""))
                        if(!char.IsControl(c)) check(font.characterLookupTable.ContainsKey(c),lang+" glyph U+"+((int)c).ToString("X4")+" '"+c+"' in "+font.name);
                }
            }
            foreach(var lang in Enum.GetValues(typeof(GameLanguage)).Cast<GameLanguage>())
            {
                GameText.SetLanguage(lang);
                check(GameText.Current==lang,"set language "+lang);
                foreach(var title in new[]{"Scroll Hunter","FOREST STAGE CLEAR","DESERT STAGE","COMING SOON...?","Space","Shift"})
                    check(GameText.T(title)==title,"Original English changed: "+lang+" "+title);
                check(GameText.T("게임 시작")==(lang==GameLanguage.Korean?"게임 시작":(string)entries.First(e=>(string)e["ko"]=="게임 시작")[Fields[(int)lang]]),"menu text "+lang);
                var start=((Button)Get(menu,"optionsOpen")).GetComponentInChildren<TMP_Text>(true);
                check(start.font==fonts.FontFor(lang),"menu font swap "+lang+": "+start.font.name);
                foreach(var c in GameText.NativeNames) foreach(char ch in c)
                    check(fonts.FontFor(lang).HasCharacter(ch,true,true)||TMP_Settings.fallbackFontAssets.Any(f=>f.HasCharacter(ch,true,true)),"native name glyph "+lang+" "+ch);
            }
            // 2) 데이터 문구(스킬·적·패시브)
            foreach(var lang in Translations)
            {
                GameText.SetLanguage(lang);
                foreach(var guid in AssetDatabase.FindAssets("t:SkillData",new[]{"Assets/_Project/Data"}))
                {
                    var card=AssetDatabase.LoadAssetAtPath<SkillData>(AssetDatabase.GUIDToAssetPath(guid));
                    check(!Hangul(card.DisplayName),lang+" skill name "+card.name);
                    check(!Hangul(info.DescribeCard(card)),lang+" skill description "+card.name+" "+info.DescribeCard(card));
                }
                foreach(var guid in AssetDatabase.FindAssets("t:EnemyData",new[]{"Assets/_Project/Data"}))
                {
                    var enemy=AssetDatabase.LoadAssetAtPath<EnemyData>(AssetDatabase.GUIDToAssetPath(guid));
                    check(!Hangul(enemy.DisplayName+enemy.PassiveDescription),lang+" enemy "+enemy.name);
                    foreach(var attack in enemy.Attacks) check(!Hangul(attack.SkillName),lang+" attack "+enemy.name);
                }
                foreach(var guid in AssetDatabase.FindAssets("t:RunRewardEffect",new[]{"Assets/_Project/Data"}))
                {
                    var effect=AssetDatabase.LoadAssetAtPath<RunRewardEffect>(AssetDatabase.GUIDToAssetPath(guid));
                    check(!Hangul(effect.DisplayName+effect.Description),lang+" passive "+effect.name);
                    check(!Hangul((string)Call(rewardUI,"DescribePassiveEffect",effect)),lang+" passive details "+effect.name);
                }
            }
            // 3) 인트로·대화·튜토리얼 화면 (4개 번역 언어)
            var sets=new[]{story.Content.tutorialDialogue,story.Content.forestDialogue,story.Content.leaderDialogue,story.Content.banditDialogue,story.Content.bossDialogue};
            foreach(var lang in Translations)
            {
                GameText.SetLanguage(lang);
                story.ShowIntro(()=>{});
                for(int i=0;i<story.Content.intro.Length;i++)
                {
                    Set(story,"<PageIndex>k__BackingField",i);Call(story,"DrawPage");
                    textCheck((TMP_Text)Get(story,"body"),"intro "+i);
                }
                story.Cancel();
                for(int section=0;section<sets.Length;section++)
                {
                    if(section==0)story.ShowTutorialDialogue(()=>{});else story.ShowBattleDialogue(section-1,()=>{});
                    for(int i=0;i<sets[section].Length;i++)
                    {
                        Set(story,"<PageIndex>k__BackingField",i);Call(story,"DrawPage");
                        var body=(TMP_Text)Get(story,"body");
                        textCheck(body,"dialogue "+section+"/"+i);
                        textCheck((TMP_Text)Get(story,"speakerName"),"speaker "+section+"/"+i);
                        bool narration=sets[section][i].speaker==StoryContent.Speaker.Narrator;
                        check(story.IsTyping==!narration,"narration/typewriter "+section+"/"+i);
                        if(narration) check(body.text.StartsWith(lang==GameLanguage.Japanese||lang==GameLanguage.ChineseSimplified?"（":"(",StringComparison.Ordinal),"narration brackets "+lang);
                    }
                    GameText.SetLanguage(GameLanguage.Korean);GameText.SetLanguage(lang);
                    check(!story.IsTyping,"language change reveals current dialogue");
                    story.Cancel();
                }
                if(tutorial!=null)
                {
                    for(int lesson=1;lesson<=7;lesson++)
                    {
                        Call(tutorial,"ShowLesson",lesson);Call(tutorial,"LateUpdate");
                        textCheck((TMP_Text)Get(tutorial,"guideText"),"tutorial "+lesson);
                        GameText.SetLanguage(GameLanguage.Korean);check(Hangul(((TMP_Text)Get(tutorial,"guideText")).text),"tutorial restores Korean");
                        GameText.SetLanguage(lang);
                    }
                    tutorial.Cancel();
                }
            }
            // 4) 편성 화면
            var isolated=new InheritanceSave(Path.Combine(Path.GetTempPath(),"ScrollHunter_Lang5_20261015","test-save-"+Guid.NewGuid().ToString("N")+".json"),
                (BattleRewardOption[])Get(flow,"rewardPool"),UnityEngine.Object.FindFirstObjectByType<DeckSystem>().CopyStartingDeck());
            check(isolated.TryRecordIntro()&&isolated.TryRecordTutorial(),"isolated progress write");
            Set(flow,"inheritance",isolated);flow.StartRun();
            check(flow.State==BattleFlowState.Preparing,"preparation after tutorial record");
            foreach(var lang in Translations)
            {
                GameText.SetLanguage(lang);
                foreach(var field in new[]{"deckSlotLabels","deckNameLabels","deckInfoLabels","deckLabels"})
                    foreach(var text in (TMP_Text[])Get(rewardUI,field)) textCheck(text,"deck "+field);
            }
            GameText.SetLanguage(GameLanguage.Korean);check(Hangul(((TMP_Text[])Get(rewardUI,"deckNameLabels"))[0].text),"deck restores Korean");
            // 5) 차단 성공 문구: 모든 언어 한 줄
            var numbers=UnityEngine.Object.FindFirstObjectByType<DamageNumberUI>(FindObjectsInactive.Include);
            var cam=(Camera)Get(numbers,"worldCamera");
            foreach(var lang in Enum.GetValues(typeof(GameLanguage)).Cast<GameLanguage>())
            {
                GameText.SetLanguage(lang);
                Call(numbers,"ShowInterrupt",cam.transform.position+cam.transform.forward*8f);
                var list=(System.Collections.IList)Get(numbers,"numbers");
                var label=(TMP_Text)list[list.Count-1].GetType().GetField("text").GetValue(list[list.Count-1]);
                label.ForceMeshUpdate(true);
                check(label.textWrappingMode==TextWrappingModes.NoWrap&&label.textInfo.lineCount==1,"interrupt one line "+lang+": "+label.text);
                for(int i=0;i<label.textInfo.characterCount;i++) check(label.textInfo.characterInfo[i].character!='□',"interrupt glyph "+lang);
                Call(numbers,"Clear");
            }
            flow.ReturnToTitle();
            // 6) 옵션: 웹 표시, 선택 즉시 반영·취소 복귀·적용 저장, 기존 0/1 값 호환
            menu.OpenOptions();
            check(!((Button)Get(menu,"quitButton")).gameObject.activeSelf,"web quit hidden");
            foreach(var field in new[]{"modeButton","resolutionDropdown","vSyncToggle","frameLimitDropdown"})
                check(!((Component)Get(menu,field)).gameObject.activeSelf,"web control hidden: "+field);
            check(Application.targetFrameRate==60 && QualitySettings.vSyncCount==0,"web internal 60 fps cap");
            var dropdown=(TMP_Dropdown)Get(menu,"languageDropdown");
            check(dropdown.options.Count==5&&dropdown.options.Select(o=>o.text).SequenceEqual(GameText.NativeNames),"five language options");
            foreach(var saved in Enum.GetValues(typeof(GameLanguage)).Cast<GameLanguage>())
            {
                PlayerPrefs.SetInt(GameText.PreferenceKey,(int)saved);menu.ClosePanels();menu.OpenOptions();
                check(GameText.Current==saved&&dropdown.value==(int)saved,"saved language shown "+saved);
                var next=(GameLanguage)(((int)saved+1)%5);
                dropdown.value=(int)next;
                check(GameText.Current==next,"dropdown applies immediately "+next);
                menu.ClosePanels();
                check(GameText.Current==saved&&PlayerPrefs.GetInt(GameText.PreferenceKey)==(int)saved,"cancel restores "+saved);
                menu.OpenOptions();dropdown.value=(int)next;Call(menu,"ApplyOptions");
                check(PlayerPrefs.GetInt(GameText.PreferenceKey)==(int)next&&GameText.Current==next,"apply saves "+next);
                GameText.SetLanguage(saved);GameText.LoadSaved();
                check(GameText.Current==next,"save/load "+next);
                menu.OpenOptions();
            }
            menu.ClosePanels();
            PlayerPrefs.SetInt(GameText.PreferenceKey,0);GameText.LoadSaved();check(GameText.Current==GameLanguage.Korean,"legacy 0 = Korean");
            PlayerPrefs.SetInt(GameText.PreferenceKey,1);GameText.LoadSaved();check(GameText.Current==GameLanguage.English,"legacy 1 = English");
            PlayerPrefs.DeleteKey(GameText.PreferenceKey);GameText.LoadSaved();check(GameText.Current==GameLanguage.English,"web first launch defaults to English");
            PlayerPrefs.SetInt(GameText.PreferenceKey,99);GameText.LoadSaved();check(GameText.Current==GameLanguage.English,"invalid web value falls back to English");
            check(missingGlyphs.Count==0,"TMP missing glyph warnings: "+string.Join(" / ",missingGlyphs.Take(3)));
            var result=new{passed=count,warnings=warnings.ToArray(),platform=EditorUserBuildSettings.activeBuildTarget.ToString()};
            Directory.CreateDirectory("Docs/근거자료/2026-10-15_다국어");
            File.WriteAllText("Docs/근거자료/2026-10-15_다국어/localization-checks.json",Newtonsoft.Json.JsonConvert.SerializeObject(result,Newtonsoft.Json.Formatting.Indented));
            Debug.Log("[Web Localization] "+count+" passed; layout warnings "+warnings.Count);
        }
        finally
        {
            Application.logMessageReceived-=glyphLog;
            story.Cancel();if(tutorial!=null)tutorial.Cancel();flow.ReturnToTitle();Set(flow,"inheritance",save);
            menu.ClosePanels();
            if(hadPreference)PlayerPrefs.SetInt(GameText.PreferenceKey,preference);else PlayerPrefs.DeleteKey(GameText.PreferenceKey);
            PlayerPrefs.Save();GameText.SetLanguage(language);
        }
    }
}
