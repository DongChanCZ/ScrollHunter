using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>임시 저장 경로만 사용한다. 사용자의 계승 파일은 읽거나 쓰지 않는다.</summary>
public static class InheritanceChecks
{
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    private static object Get(object o,string n) => o.GetType().GetField(n,Hidden).GetValue(o);
    private static void Set(object o,string n,object v) => o.GetType().GetField(n,Hidden).SetValue(o,v);
    private static void Prop(object o,string n,object v) => o.GetType().GetProperty(n).SetValue(o,v);
    private static object Call(object o,string n,params object[] v) => o.GetType().GetMethod(n,Hidden).Invoke(o,v);
    [MenuItem("Tools/Scroll Hunter/Check Inheritance (Play)")]
    public static void Menu() => Debug.Log(Run());
    public static string Run()
    {
        if(!Application.isPlaying)throw new InvalidOperationException("Battle Play required.");
        var f=UnityEngine.Object.FindFirstObjectByType<BattleFlow>();
        var d=UnityEngine.Object.FindFirstObjectByType<DeckSystem>();
        var p=UnityEngine.Object.FindFirstObjectByType<Player>();
        var c=UnityEngine.Object.FindFirstObjectByType<CostSystem>();
        var t=UnityEngine.Object.FindFirstObjectByType<TutorialFlow>();
        var ui=(BattleRewardUI)Get(f,"rewardUI");
        var pool=(BattleRewardOption[])Get(f,"rewardPool");
        var baseline=d.CopyStartingDeck();
        var realSave=Get(f,"inheritance");
        if(realSave==null)throw new InvalidOperationException("Wait for the first Play frame (Awake/Start) before running checks.");
        bool tutorialEnabled=t.enabled;
        float finish=(float)Get(f,"finishingEffectHold"),defeat=(float)Get(f,"defeatEffectHold");
        string dir=Path.Combine(Application.temporaryCachePath,"InheritanceCheck-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        string path=Path.Combine(dir,"save.json");
        int count=0;
        Action<bool,string> check=(ok,label)=>{if(!ok)throw new Exception("Inheritance: "+label);count++;};
        Func<InheritanceSave> load=()=>new InheritanceSave(path,pool,baseline);
        var card=pool.First(x=>x.Card!=null);
        var other=pool.First(x=>x.Card!=null&&x!=card);
        var ps04=pool.Single(x=>x.Effect!=null&&x.Effect.name.StartsWith("PS04_"));
        var ps01=pool.Single(x=>x.Effect!=null&&x.Effect.name.StartsWith("PS01_"));
        var ps02=pool.Single(x=>x.Effect!=null&&x.Effect.name.StartsWith("PS02_"));
        var ps03=pool.Single(x=>x.Effect!=null&&x.Effect.name.StartsWith("PS03_"));
        var ps10=pool.Single(x=>x.Effect!=null&&x.Effect.name.StartsWith("PS10_"));
        Action<BattleRewardOption> earn=option=>{
            Prop(f,"State",BattleFlowState.BetweenBattles);Prop(f,"RewardResolved",false);Prop(f,"SelectedReward",null);
            var choices=(List<BattleRewardOption>)Get(f,"rewardChoices");choices.Clear();choices.Add(option);
            check(f.SelectReward(0),"earn "+option.DisplayName);
            if(option.Card!=null)check(f.SelectReplacementSlot(0)&&f.ConfirmReplacement(),"commit reward card");
        };
        Action lose=()=>{ p.TakeDamage(100000);Call(f,"Update");check(f.State==BattleFlowState.Defeat,"actual defeat"); };
        try
        {
            check(pool.Select(x=>x.InheritanceId).Distinct().Count()==pool.Length&&pool.All(x=>!string.IsNullOrEmpty(x.InheritanceId)),"unique save IDs");
            var save=load();check(!save.HasAny&&save.BuildDeck().SequenceEqual(baseline),"empty save uses base deck");
            check(!save.CanInherit(ps02),"healing excluded");check(!save.TryInherit(card,-1),"invalid slot");
            check(save.TryInherit(card,0),"card save");check(load().BuildDeck()[0]==card.Card,"disk reload card");
            check(!save.TryInherit(card,1),"no duplicate starting card");
            check(save.TryInherit(other,0)&&load().BuildDeck()[0]==other.Card,"overwrite inherited slot");
            for(int i=0;i<ps04.Effect.MaxStacks;i++)check(save.TryInherit(ps04,-1),"stack passive");
            check(!save.TryInherit(ps04,-1)&&load().Stacks(ps04.Effect)==ps04.Effect.MaxStacks,"per-PS cap");
            check(save.TryInherit(ps10,-1)&&save.Stacks(ps04.Effect)==4,"PS10 independent cap");
            string prior=File.ReadAllText(path+".bak");File.WriteAllText(path,"broken");
            var recovered=load();check(recovered.UsedBackup&&!recovered.LoadFailed&&recovered.HasAny,"backup recovery");
            File.WriteAllText(path+".bak","broken");var corrupt=load();
            check(corrupt.LoadFailed&&!corrupt.HasAny&&File.ReadAllText(path)=="broken","corrupt file preserved");
            check(corrupt.TryReset()&&!load().HasAny&&!File.Exists(path+".bak"),"reset persisted without old backup");
            string blocked=Path.Combine(dir,"blocked");Directory.CreateDirectory(blocked);
            var failed=new InheritanceSave(blocked,pool,baseline);
            check(!failed.TryInherit(card,0)&&!failed.HasAny,"write failure preserves memory");
            File.WriteAllText(path,"{\"version\":99,\"cards\":[],\"passives\":[]}");
            File.WriteAllText(path+".bak","broken");check(load().LoadFailed,"unknown version rejected");
            File.WriteAllText(path,"{}");check(load().LoadFailed,"empty JSON is not a valid save");
            File.WriteAllText(path,"{\"version\":1,\"cards\":[null,null,null,null,null,null,null,null]}");
            check(load().LoadFailed,"missing passive list rejected");
            save=load();check(save.TryReset(),"fresh integration save");Set(f,"inheritance",save);
            t.enabled=false;Set(f,"finishingEffectHold",0f);Set(f,"defeatEffectHold",0f);
            f.RestartRun();earn(card);earn(other);earn(ps04);
            Prop(f,"State",BattleFlowState.Fighting);lose();
            Set(f,"panelHoldRemaining",1f);f.ContinueAfterResult();check(f.State==BattleFlowState.Defeat,"ending effect hold preserved");
            Set(f,"panelHoldRemaining",0f);
            ((Button)Get(f,"restartButton")).onClick.Invoke();
            check(f.State==BattleFlowState.Inheriting&&f.RewardChoiceCount==3,"defeat button opens earned choices");
            check(f.GetPassiveStacks(ps04.Effect)==0&&Mathf.Approximately(c.RegenerationPerSecond,.8f),"inheritance preview excludes transient effects");
            check(Enumerable.Range(0,3).Select(f.GetRewardChoice).Contains(card),"removed earned card stays eligible");
            check(!f.SelectReward(9)&&!f.SelectReplacementSlot(-1),"invalid selection ignored");
            var buttons=(Button[])Get(ui,"choiceButtons");buttons[0].onClick.Invoke();
            check(f.SelectedReward==card.Card&&!save.HasAny,"card choice alone not saved");
            var confirm=(Button)Get(ui,"confirmReplacementButton");
            var slots=(Button[])Get(ui,"deckButtons");
            check(!confirm.interactable&&!f.ConfirmReplacement(),"confirm disabled without slot");
            slots[1].onClick.Invoke();
            check(f.SelectedReplacementIndex==1&&confirm.interactable&&!save.HasAny&&!File.Exists(path+".tmp"),"slot selection does not save");
            ((Button)Get(ui,"backButton")).onClick.Invoke();check(f.SelectedReward==null&&f.SelectedReplacementIndex==-1&&f.RewardChoiceCount==3,"back clears slot and preserves choices");
            buttons[0].onClick.Invoke();check(!confirm.interactable,"new choice requires slot again");
            slots[1].onClick.Invoke();slots[2].onClick.Invoke();
            check(f.SelectedReplacementIndex==2&&f.GetDeckCard(2)==baseline[2]&&!load().HasAny,"reselect changes only pending slot");
            confirm.onClick.Invoke();
            check(f.State==BattleFlowState.Title&&load().BuildDeck()[2]==card.Card,"card commit returns title");
            check(!f.SelectReward(0)&&!f.SelectReplacementSlot(0),"cannot inherit twice");
            check(!f.ConfirmReplacement(),"double confirm rejected");
            f.StartRun();check(f.GetDeckCard(2)==card.Card&&Mathf.Approximately(c.RegenerationPerSecond,.8f),"new run card no transient passive");
            f.NextBattle();Call(f,"AdvanceCountdown",3f);check(f.State==BattleFlowState.Fighting,"new run countdown");
            earn(ps04);Prop(f,"State",BattleFlowState.Fighting);lose();f.ContinueAfterResult();buttons[0].onClick.Invoke();
            check(f.State==BattleFlowState.Title&&load().Stacks(ps04.Effect)==1,"passive saved from earned choice");
            check(save.TryInherit(ps01,-1)&&save.TryInherit(ps03,-1),"setup potion and HP inheritance");
            t.enabled=true;f.StartRun();
            check(t.Active&&f.GetDeckCard(2)==baseline[2]&&p.MaxHp==500&&p.PotionCapacity==3&&Mathf.Approximately(c.RegenerationPerSecond,.8f),"tutorial stays base");
            t.Skip();check(f.State==BattleFlowState.Preparing&&f.GetDeckCard(2)==card.Card,"tutorial skip enters inherited preparation");
            check(p.MaxHp==550&&p.PotionCapacity==4&&Mathf.Approximately(c.RegenerationPerSecond,.9f),"passives applied once");
            // 입력 소비 프레임을 지난 것과 같은 조건으로 준비 버튼을 검사한다.
            Set(t,"consumedFrame",-1);f.NextBattle();Call(f,"AdvanceCountdown",3f);
            check(p.CurrentHp==550&&p.PotionsRemaining==4&&f.GetPassiveStacks(ps04.Effect)==1,"full run resources with inherited maximum");
            Prop(f,"State",BattleFlowState.BetweenBattles);Prop(f,"RewardResolved",true);f.NextBattle();Call(f,"AdvanceCountdown",3f);
            check(f.GetPassiveStacks(ps04.Effect)==1&&Mathf.Approximately(c.RegenerationPerSecond,.9f),"next battle no double apply");
            earn(other);Prop(f,"State",BattleFlowState.Fighting);lose();f.ContinueAfterResult();
            check(f.RewardChoiceCount==1,"old inherited rewards not earned again");
            var before=File.ReadAllText(path);
            f.SelectReward(0);f.SelectReplacementSlot(1);
            ((Button)Get(ui,"skipButton")).onClick.Invoke();
            check(f.SelectedReplacementIndex==-1&&!f.ConfirmReplacement(),"skip clears pending slot");
            check(f.State==BattleFlowState.Title&&File.ReadAllText(path)==before,"skip preserves persistent data");
            ((Button)Get(ui,"inheritanceOpen")).onClick.Invoke();((Button)Get(ui,"inheritanceReset")).onClick.Invoke();
            check(((GameObject)Get(ui,"resetConfirmation")).activeSelf,"reset confirmation visible");
            ((Button)Get(ui,"resetCancel")).onClick.Invoke();check(save.HasAny,"reset cancel preserves");
            ((Button)Get(ui,"inheritanceReset")).onClick.Invoke();((Button)Get(ui,"resetConfirm")).onClick.Invoke();
            check(!load().HasAny,"confirmed reset persisted");
            t.enabled=false;f.RestartRun();lose();f.ContinueAfterResult();
            check(f.State==BattleFlowState.Title,"no rewards skips inheritance");
            f.StartRun();f.NextBattle();Call(f,"AdvanceCountdown",3f);earn(ps02);
            Prop(f,"State",BattleFlowState.Fighting);lose();f.ContinueAfterResult();
            check(f.State==BattleFlowState.Title,"only healing skips inheritance");
            check(p.PotionCapacity==3&&p.MaxHp==500&&Mathf.Approximately(c.RegenerationPerSecond,.8f),"reset base stats");
            return "Inheritance: "+count+" checks passed. Isolated store: "+dir;
        }
        finally
        {
            Call(f,"ClearRunRewards");Set(f,"inheritance",realSave);t.enabled=tutorialEnabled;
            Set(f,"finishingEffectHold",finish);Set(f,"defeatEffectHold",defeat);
            Call(f,"ReturnToTitle");
            // 파일은 재현 근거로 임시 캐시에 남긴다. 실제 사용자 저장 파일은 건드리지 않는다.
        }
    }
}
