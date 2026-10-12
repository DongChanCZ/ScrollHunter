using System;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class StartEndingChecks
{
    private const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
    private static object Get(object o,string n)=>o.GetType().GetField(n,Hidden).GetValue(o);
    private static void Set(object o,string n,object v)=>o.GetType().GetField(n,Hidden).SetValue(o,v);
    private static object Call(object o,string n,params object[] args)=>o.GetType().GetMethod(n,Hidden).Invoke(o,args);
    [MenuItem("Tools/Scroll Hunter/Check Start Confirmation and Ending (Play)")]
    public static void Menu()=>Debug.Log(Run());
    public static string Run()
    {
        if(!Application.isPlaying)throw new Exception("Play required.");
        var f=UnityEngine.Object.FindFirstObjectByType<BattleFlow>();
        var menu=f.GetComponent<MainMenuUI>();
        var story=f.GetComponent<StoryUI>();
        var ending=f.GetComponent<StageClearUI>();
        var tutorial=UnityEngine.Object.FindFirstObjectByType<TutorialFlow>();
        var deck=UnityEngine.Object.FindFirstObjectByType<DeckSystem>();
        var real=f.Inheritance;
        string path=Path.Combine(Application.temporaryCachePath,"StartEnding-"+Guid.NewGuid().ToString("N")+".json");
        var pool=(BattleRewardOption[])Get(f,"rewardPool");
        Func<InheritanceSave> load=()=>new InheritanceSave(path,pool,deck.CopyStartingDeck());
        var save=load();
        int count=0;
        Action<bool,string> check=(ok,label)=>{if(!ok)throw new Exception(label);count++;};
        Action<string> click=name=>((Button)Get(menu,name)).onClick.Invoke();
        Action refresh=()=>Call(f,"RefreshUI");
        var start=(GameObject)Get(menu,"startDataPanel");
        var warning=(GameObject)Get(menu,"newGamePanel");
        var begin=(Button)Get(f,"startButton");
        string prefix=(string)Get(menu,"preferencesPrefix");
        string optionBefore=PlayerPrefs.GetString(prefix+"marker","unchanged");
        try
        {
            menu.BackToTitle();Set(f,"inheritance",save);
            check(!save.HasProgress,"absent save is fresh");
            begin.onClick.Invoke();
            check(story.ShowingIntro&&!start.activeSelf&&f.State==BattleFlowState.Story,"fresh button goes straight to intro");
            menu.BackToTitle();
            check(save.TryResetGame()&&!load().HasProgress,"empty save stays fresh");
            begin.onClick.Invoke();check(story.ShowingIntro&&!start.activeSelf,"empty file does not prompt");menu.BackToTitle();
            save.TryRecordIntro();
            begin.onClick.Invoke();check(start.activeSelf&&!warning.activeSelf&&f.State==BattleFlowState.Title,"intro record prompts");
            string prior=File.ReadAllText(path);
            click("startCancel");check(!start.activeSelf&&f.State==BattleFlowState.Title&&File.ReadAllText(path)==prior,"no returns to title, save unchanged");
            begin.onClick.Invoke();click("startNewGame");
            check(start.activeSelf&&warning.activeSelf&&File.ReadAllText(path)==prior,"new game only opens warning");
            click("newGameNo");check(start.activeSelf&&!warning.activeSelf,"warning no closes only warning");
            click("startNewGame");menu.HandleEscape();check(start.activeSelf&&!warning.activeSelf,"escape cancels top warning");
            menu.HandleEscape();check(!start.activeSelf&&f.State==BattleFlowState.Title,"escape cancels first prompt");

            save.TryResetGame();save.TryRecordTutorial();
            begin.onClick.Invoke();check(start.activeSelf,"tutorial record prompts");menu.ClosePanels();
            save.TryResetGame();save.TryRecordBattle(2);
            begin.onClick.Invoke();check(start.activeSelf,"clear record prompts");menu.ClosePanels();
            save.TryResetGame();
            var card=pool.First(x=>x.Card!=null&&save.CanInherit(x));
            check(save.TryInherit(card,0),"seed skill inheritance");
            begin.onClick.Invoke();check(start.activeSelf,"skill inheritance prompts");menu.ClosePanels();
            save.TryResetGame();
            var passive=pool.First(x=>x.Effect!=null&&save.CanInherit(x));
            check(save.TryInherit(passive,-1),"seed passive inheritance");
            begin.onClick.Invoke();check(start.activeSelf,"passive inheritance prompts");menu.ClosePanels();
            save.TryInherit(card,0);save.TryRecordIntro();save.TryRecordTutorial();save.TryRecordBattle(1);
            prior=File.ReadAllText(path);
            begin.onClick.Invoke();click("startExisting");
            check(f.State==BattleFlowState.Preparing&&!start.activeSelf,"yes goes to preparation");
            check(f.GetDeckCard(0)==card.Card&&f.GetPassiveStacks(passive.Effect)==1,"yes applies inheritance");
            check(File.ReadAllText(path)==prior,"existing start does not alter save");
            menu.BackToTitle();
            begin.onClick.Invoke();click("startNewGame");
            string validPath=(string)Get(save,"path");
            Directory.CreateDirectory(path+".blocked");Set(save,"path",path+".blocked");
            try {click("newGameYes");}
            finally {Set(save,"path",validPath);if(File.Exists(path+".blocked.tmp"))File.Delete(path+".blocked.tmp");Directory.Delete(path+".blocked");}
            check(start.activeSelf&&warning.activeSelf&&f.State==BattleFlowState.Title,"failed reset does not start");
            check(save.HasAny&&File.ReadAllText(path)==prior,"failed reset preserves inheritance");
            click("newGameNo");click("startNewGame");click("newGameYes");
            check(story.ShowingIntro&&f.State==BattleFlowState.Story&&!start.activeSelf&&!warning.activeSelf,"confirmed new game starts intro");
            check(!load().HasProgress&&!load().HasAny&&!File.Exists(path+".bak"),"new game clears all progress, inheritance, backup");
            check(PlayerPrefs.GetString(prefix+"marker","unchanged")==optionBefore,"settings untouched");
            menu.BackToTitle();

            var guide=(TMP_Text)Get(tutorial,"guideText");
            check(Mathf.Approximately(guide.lineSpacing,(float)Get(story,"lineSpacing")),"tutorial spacing matches intro");
            for(int n=1;n<=7;n++)
            {
                Call(tutorial,"ShowLesson",n);Call(tutorial,"LateUpdate");guide.ForceMeshUpdate();
                check(!guide.isTextOverflowing,"tutorial fits "+n);
                var hint=(TMP_Text)Get(tutorial,"continueHint");
                check(guide.rectTransform.offsetMin.y>=hint.rectTransform.offsetMax.y,"tutorial hint separated "+n);
            }
            tutorial.Cancel();
            var reward=(BattleRewardUI)Get(f,"rewardUI");
            string format=(string)Get(reward,"titleFormat");
            check(string.Format(format,3,200,500)=="전투 승리 · HP 200 / 500","reward title without battle number");

            Set(f,"<State>k__BackingField",BattleFlowState.BetweenBattles);refresh();
            check(!ending.Active,"ordinary victory has no ending");
            Set(f,"<State>k__BackingField",BattleFlowState.Defeat);refresh();
            check(!ending.Active&&((GameObject)Get(f,"resultPanel")).activeSelf,"defeat remains result panel");
            Set(f,"<State>k__BackingField",BattleFlowState.Victory);Set(f,"panelHoldRemaining",1f);refresh();
            check(!ending.Active,"boss death presentation finishes first");
            Set(f,"panelHoldRemaining",0f);refresh();
            check(ending.Active&&!ending.Ready&&!((GameObject)Get(f,"resultPanel")).activeSelf,"boss victory begins ending, old result hidden");
            check(!ending.Advance(),"early input does not skip forest");
            float fade=(float)Get(ending,"fadeSeconds"),hold=(float)Get(ending,"forestHoldSeconds");
            Call(ending,"Tick",fade);
            check(((CanvasGroup)Get(ending,"page")).alpha==1&&!ending.ShowingDesert,"forest fade in");
            check(((TMP_Text)Get(ending,"heading")).text=="FOREST STAGE CLEAR","forest label");
            Call(ending,"Tick",hold+fade*.5f);
            check(Mathf.Abs(((CanvasGroup)Get(ending,"page")).alpha-.5f)<.001f,"forest fade out");
            menu.HandleEscape();float time=(float)Get(ending,"elapsed");Call(ending,"Tick",20f);
            check((float)Get(ending,"elapsed")==time&&!ending.Advance(),"Esc pauses ending and blocks advance");menu.ResumeGame();
            Call(ending,"Tick",fade*.5f+fade*.5f);
            check(ending.ShowingDesert&&!ending.Ready&&Mathf.Abs(((CanvasGroup)Get(ending,"page")).alpha-.5f)<.001f,"desert fades in");
            check(((RawImage)Get(ending,"artwork")).texture==(Texture2D)Get(ending,"desert"),"desert asset connected");
            Call(ending,"Tick",fade);
            check(ending.Ready&&((TMP_Text)Get(ending,"heading")).text=="DESERT STAGE"&&((TMP_Text)Get(ending,"subtitle")).text=="COMING SOON...?","final screen text");
            check(!ending.Advance(),"ready frame cannot consume same input");
            Set(ending,"readyFrame",-1);Set(f,"menuInputFrame",-1);Set(story,"consumedFrame",-1);
            check(ending.Advance()&&!ending.Active&&f.State==BattleFlowState.Title,"advance returns to title");
            Set(f,"<State>k__BackingField",BattleFlowState.Victory);refresh();
            check(ending.Active&&!ending.Ready,"second clear starts fresh");
            menu.BackToTitle();check(!ending.Active,"return cancels ending");
            return "Start/end UI: "+count+" passed; isolated save; physical input and build excluded.";
        }
        finally
        {
            menu.BackToTitle();Set(f,"inheritance",real);tutorial.Cancel();
            foreach(string suffix in new[]{"",".bak",".tmp"})if(File.Exists(path+suffix))File.Delete(path+suffix);
        }
    }
}
