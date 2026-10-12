using System;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;

public static class StoryChecks
{
    private const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
    private static object Get(object o,string n)=>o.GetType().GetField(n,Hidden).GetValue(o);
    private static void Set(object o,string n,object v)=>o.GetType().GetField(n,Hidden).SetValue(o,v);
    private static void Unlock(StoryUI s)=>Set(s,"consumedFrame",-1);
    [MenuItem("Tools/Scroll Hunter/Check Opening Story (Play)")]
    public static void Menu()=>Debug.Log(Run());
    public static string Run()
    {
        if(!Application.isPlaying)throw new InvalidOperationException("Enter Play first.");
        var f=UnityEngine.Object.FindFirstObjectByType<BattleFlow>();
        var s=f.GetComponent<StoryUI>();
        var t=UnityEngine.Object.FindFirstObjectByType<TutorialFlow>();
        var d=UnityEngine.Object.FindFirstObjectByType<DeckSystem>();
        var menu=f.GetComponent<MainMenuUI>();
        var real=Get(f,"inheritance");
        var pool=(BattleRewardOption[])Get(f,"rewardPool");
        var path=Path.Combine(Application.temporaryCachePath,"StoryChecks-"+Guid.NewGuid().ToString("N")+".json");
        Func<InheritanceSave> load=()=>new InheritanceSave(path,pool,d.CopyStartingDeck());
        int count=0;Action<bool,string> check=(ok,label)=>{if(!ok)throw new Exception("Story: "+label);count++;};
        Action fresh=()=>{menu.BackToTitle();var save=load();save.TryResetGame();Set(f,"inheritance",save);Unlock(s);};
        try
        {
            check(s!=null&&s.Content!=null,"content connected");
            check(s.Content.intro.Length==5&&s.Content.portraits.Length==5&&s.Content.tutorialDialogue.Length==11,"5 scenes, 5 actors, 11 lines");
            check(s.Content.intro.All(x=>x.image!=null&&!string.IsNullOrEmpty(x.text)),"intro complete");
            check(s.Content.portraits.All(x=>x.image!=null&&!string.IsNullOrEmpty(x.displayName)),"portraits complete");
            fresh();f.StartRun();
            check(f.State==BattleFlowState.Story&&s.Active&&s.ShowingIntro&&s.PageIndex==0,"new run intro");
            check(Time.timeScale==0&&f.MenuBlocksInput&&!t.Active,"story freezes combat");
            check(!s.Advance()&&s.PageIndex==0,"start input not reused");
            var body=(TMP_Text)Get(s,"body");
            for(int i=0;i<5;i++)
            {
                check(s.PageIndex==i&&body.text==s.Content.intro[i].text,"intro text "+i);
                body.ForceMeshUpdate();check(!body.isTextOverflowing,"intro fits "+i);
                ((IntroPresentation)Get(s,"introPresentation")).Tick(3f);
                Unlock(s);check(s.Advance(),"next intro "+i);
                check(!s.Advance(),"same frame rejected "+i);
                ((IntroPresentation)Get(s,"introPresentation")).Tick(1f);
            }
            check(s.Active&&!s.ShowingIntro&&f.State==BattleFlowState.Story,"intro to dialogue");
            check(load().IntroDismissed&&!load().TutorialDismissed,"only intro recorded");
            check(!t.Active&&Time.timeScale==0,"tutorial has not started behind dialogue");
            var name=(TMP_Text)Get(s,"speakerName");
            var portrait=(UnityEngine.UI.RawImage)Get(s,"portrait");
            for(int i=0;i<11;i++)
            {
                var line=s.Content.tutorialDialogue[i];
                check(s.PageIndex==i,"dialogue line "+i);
                bool enemy=line.speaker==StoryContent.Speaker.Enemy;
                bool narrator=line.speaker==StoryContent.Speaker.Narrator;
                check(portrait.gameObject.activeSelf==enemy,"portrait visibility "+i);
                if(narrator)check(body.text.StartsWith("(")&&body.text.EndsWith(")")&&!name.gameObject.activeSelf,"narration parentheses");
                else check(name.text==(enemy?"깡패":s.Content.playerName)&&name.alignment==(enemy?TextAlignmentOptions.TopRight:TextAlignmentOptions.TopLeft),"speaker position "+i);
                body.ForceMeshUpdate();check(!body.isTextOverflowing,"dialogue fits "+i);
                if(s.IsTyping){
                    Unlock(s);check(s.Advance()&&!s.IsTyping&&s.PageIndex==i,"reveal dialogue "+i);
                    check(!s.Advance(),"reveal does not advance twice "+i);
                }
                Unlock(s);s.Advance();
            }
            check(!s.Active&&t.Active&&t.ShowingGuide,"dialogue finishes into lesson one");
            check(t.BlocksInputThisFrame,"dialogue input cannot accept lesson");
            check(f.State==BattleFlowState.Fighting&&Time.timeScale==0,"lesson holds game");
            var hud=(GameObject[])Get(s,"combatHud");
            check(hud.All(x=>x==null||Mathf.Approximately(x.GetComponent<CanvasGroup>().alpha,1f)),"HUD restored");
            fresh();f.StartRun();Unlock(s);s.Skip();
            check(!s.ShowingIntro&&s.Active&&load().IntroDismissed,"intro skip enters dialogue");
            check(!load().TutorialDismissed,"intro skip is not tutorial skip");
            Unlock(s);menu.OpenPauseMenu();
            check(f.IsMenuPaused&&!s.Advance(),"pause blocks story");
            s.Skip();check(s.Active,"pause blocks skip");
            menu.ResumeGame();Unlock(s);check(s.Advance(),"resume story");
            Unlock(s);s.Skip();check(t.Active&&t.ShowingGuide,"dialogue skip starts tutorial");
            fresh();f.StartRun();menu.BackToTitle();
            check(!s.Active&&f.State==BattleFlowState.Title&&!load().IntroDismissed,"cancel does not complete intro");
            fresh();f.Inheritance.TryRecordIntro();f.Inheritance.TryRecordTutorial();f.StartRun();
            check(!s.Active&&!t.Active&&f.State==BattleFlowState.Preparing,"returning player bypasses story/tutorial");
            var saved=load();check(saved.IntroDismissed&&saved.TutorialDismissed,"disk reload progress");
            check(saved.TryReset()&&load().IntroDismissed&&load().TutorialDismissed,"inheritance-only reset preserves story");
            check(saved.TryResetGame()&&!load().IntroDismissed&&!load().TutorialDismissed,"full reset replays story");
            File.WriteAllText(path,"{\"version\":1,\"cards\":[null,null,null,null,null,null,null,null],\"passives\":[],\"tutorialDismissed\":true,\"clearedBattles\":1}");
            var legacy=load();check(!legacy.LoadFailed&&!legacy.IntroDismissed&&legacy.TutorialDismissed,"old save compatible");
            check(legacy.TryRecordIntro()&&load().ClearedBattles==1,"record intro preserves old clear");
            return "Opening Story: "+count+" checks passed (isolated save; function/UI checks).";
        }
        finally
        {
            menu.BackToTitle();Set(f,"inheritance",real);Unlock(s);
            Set(f,"menuInputFrame",-1);
        }
    }

    public static string FrameStatus="idle";
    public static void BeginFrames()
    {
        var f=UnityEngine.Object.FindFirstObjectByType<BattleFlow>();
var s=f.GetComponent<StoryUI>();var t=UnityEngine.Object.FindFirstObjectByType<TutorialFlow>();
var player=UnityEngine.Object.FindFirstObjectByType<Player>();var cost=UnityEngine.Object.FindFirstObjectByType<CostSystem>();var deck=UnityEngine.Object.FindFirstObjectByType<DeckSystem>();
var menu=f.GetComponent<MainMenuUI>();
var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
var real=typeof(BattleFlow).GetField("inheritance",flags).GetValue(f);
StorySetup.Preview();
string folder="Docs/근거자료/2026-10-12_인트로대화/";
int start=Time.frameCount,last=-1,ticks=0,checks=0,failures=0;float hp=player.CurrentHp,cp=cost.Current,cast=0;
Action<bool> check=ok=>{checks++;if(!ok)failures++;};
UnityEditor.EditorApplication.CallbackFunction tick=null;
tick=()=>{
if(!Application.isPlaying){UnityEditor.EditorApplication.update-=tick;return;}
if(Time.frameCount==last)return;last=Time.frameCount;ticks++;FrameStatus="running "+ticks+"/420";
if(ticks<360){
check(Time.timeScale==0f);check(f.State==BattleFlowState.Story);
check(Mathf.Approximately(player.CurrentHp,hp)&&Mathf.Approximately(cost.Current,cp));
if(ticks>180)check(Mathf.Approximately(t.Enemy.CastProgress01,cast));
}
if(ticks==40)ScreenCapture.CaptureScreenshot(folder+"01_인트로_최종.png");
if(ticks==60)s.Advance();
if(ticks==120)s.Advance();
if(ticks==140)ScreenCapture.CaptureScreenshot(folder+"03_스크롤쟁탈_최종.png");
if(ticks==180){s.Skip();hp=player.CurrentHp;cp=cost.Current;cast=t.Enemy.CastProgress01;}
if(ticks==210){check(!deck.TryUseSlot(0));check(!player.TryUsePotion());}
if(ticks==240)s.Advance();
if(ticks==270)ScreenCapture.CaptureScreenshot(folder+"06_깡패대화_최종.png");
if(ticks==300)s.Advance();
if(ticks==315)ScreenCapture.CaptureScreenshot(folder+"07_모험가대화_최종.png");
if(ticks==330){menu.OpenPauseMenu();check(!s.Advance());}
if(ticks==345)menu.ResumeGame();
if(ticks==360)s.Skip();
if(ticks>360){check(t.Active&&t.ShowingGuide&&t.Lesson==1);check(Time.timeScale==0f);}
if(ticks==400)ScreenCapture.CaptureScreenshot(folder+"08_대화후튜토리얼.png");
if(ticks>=420){
UnityEditor.EditorApplication.update-=tick;
var result=new {checks=checks,failures=failures,updates=ticks,actualFrames=Time.frameCount-start,mode="Real frames, programmatic controls; isolated save"};
System.IO.File.WriteAllText(folder+"실프레임검사.json",Newtonsoft.Json.JsonConvert.SerializeObject(result,Newtonsoft.Json.Formatting.Indented),new System.Text.UTF8Encoding(false));
FrameStatus=checks+" checks, failures "+failures+" / "+(Time.frameCount-start)+" frames";menu.BackToTitle();typeof(BattleFlow).GetField("inheritance",flags).SetValue(f,real);
Debug.Log("[Story frames] "+checks+" checks, failures "+failures);
}
};
UnityEditor.EditorApplication.update+=tick;

    }

    [MenuItem("Tools/Scroll Hunter/Check Forest Story (Play)")]
    public static void ForestMenu()=>Debug.Log(RunForest());

    public static string RunForest()
    {
        if(!Application.isPlaying)throw new InvalidOperationException("Enter Play first.");
        var f=UnityEngine.Object.FindFirstObjectByType<BattleFlow>();
        var s=f.GetComponent<StoryUI>();
        var t=UnityEngine.Object.FindFirstObjectByType<TutorialFlow>();
        var d=UnityEngine.Object.FindFirstObjectByType<DeckSystem>();
        var p=UnityEngine.Object.FindFirstObjectByType<Player>();
        var c=UnityEngine.Object.FindFirstObjectByType<CostSystem>();
        var m=UnityEngine.Object.FindFirstObjectByType<EnemyManager>();
        var menu=f.GetComponent<MainMenuUI>();
        var real=Get(f,"inheritance");
        var lines=s.Content.forestDialogue;
        var path=Path.Combine(Application.temporaryCachePath,"ForestChecks-"+Guid.NewGuid().ToString("N")+".json");
        int count=0;
        Action<bool,string> check=(ok,label)=>{if(!ok)throw new Exception("Forest story: "+label);count++;};
        Action unlock=()=>{Unlock(s);Set(f,"menuInputFrame",-1);Set(t,"consumedFrame",-1);};
        Action fresh=()=>{
            menu.BackToTitle();
            var save=new InheritanceSave(path,(BattleRewardOption[])Get(f,"rewardPool"),d.CopyStartingDeck());
            save.TryResetGame();save.TryRecordIntro();save.TryRecordTutorial();
            Set(f,"inheritance",save);unlock();f.StartRun();unlock();
        };
        try
        {
            check(s.HasForestDialogue&&lines.Length==17,"approved 17 lines");
            check(Get(s,"forestBackground")!=null,"forest background assigned");
            fresh();check(f.State==BattleFlowState.Preparing&&!s.Active,"order before dialogue");
            check(f.BeginOrderEdit()&&f.MoveOrderCard(0,5),"edit deck");
            f.NextBattle();check(f.State==BattleFlowState.Preparing&&!s.Active,"editing blocks start");
            check(f.SaveOrder(),"save order");
            var expected=Enumerable.Range(0,8).Select(f.GetDeckCard).ToArray();
            float hp=p.CurrentHp,cost=c.Current;int potions=p.PotionsRemaining;
            f.NextBattle();
            check(f.State==BattleFlowState.Story&&s.Active&&!s.ShowingIntro,"preparation starts forest");
            check(Time.timeScale==0f&&!f.IsCountingDown&&m.CombatEnded,"no battle before conversation");
            check(!d.TryUseSlot(0)&&!p.TryUsePotion()&&p.CurrentHp==hp&&c.Current==cost&&p.PotionsRemaining==potions,"combat input blocked without resource loss");
            check(!f.BeginOrderEdit(),"cannot reopen editor during story");
            f.NextBattle();check(s.PageIndex==0,"duplicate start ignored");
            check(!s.Advance(),"start click does not advance");
            menu.OpenPauseMenu();check(f.IsMenuPaused&&!s.Advance(),"Esc holds dialogue");
            s.Skip();check(s.Active,"Esc blocks skip");
            menu.ResumeGame();unlock();
            var body=(TMP_Text)Get(s,"body");var name=(TMP_Text)Get(s,"speakerName");
            var portrait=(UnityEngine.UI.RawImage)Get(s,"portrait");
            var backdrop=(UnityEngine.UI.RawImage)Get(s,"backdrop");
            check(backdrop.texture==(Texture2D)Get(s,"forestBackground"),"correct map artwork");
            for(int i=0;i<lines.Length;i++)
            {
                var line=lines[i];bool enemy=line.speaker==StoryContent.Speaker.Enemy;
                bool narration=line.speaker==StoryContent.Speaker.Narrator;
                check(s.PageIndex==i&&body.text==(narration?"("+line.text+")":line.text),"text "+i);
                check(portrait.gameObject.activeSelf==enemy,"portrait visibility "+i);
                if(enemy)check(name.text==s.Content.portraits[line.portraitIndex].displayName
                    &&portrait.texture==s.Content.portraits[line.portraitIndex].image
                    &&name.alignment==TextAlignmentOptions.TopRight,"raider/archer portrait "+i);
                else if(!narration)check(name.text==s.Content.playerName&&name.alignment==TextAlignmentOptions.TopLeft,"player name "+i);
                else check(!name.gameObject.activeSelf&&(body.fontStyle&FontStyles.Italic)!=0,"narrator "+i);
                body.ForceMeshUpdate();check(!body.isTextOverflowing,"text fits "+i);
                if(s.IsTyping){
                    unlock();check(s.Advance()&&!s.IsTyping&&s.PageIndex==i,"reveal line "+i);
                    check(!s.Advance(),"reveal input consumed "+i);
                }
                unlock();check(s.Advance(),"advance "+i);
                check(!s.Advance(),"one input one page "+i);
            }
            check(!s.Active&&f.IsCountingDown&&f.BattleNumber==1&&Time.timeScale==0f,"last line starts countdown");
            check(Enumerable.Range(0,4).All(i=>d.GetHandCard(i)==expected[i]),"edited order used");
            check(m.EnemyCount==2,"A+B encounter");
            typeof(BattleFlow).GetMethod("AdvanceCountdown",Hidden).Invoke(f,new object[]{2.9f});
            check(f.IsCountingDown,"countdown remains before 3 seconds");
            typeof(BattleFlow).GetMethod("AdvanceCountdown",Hidden).Invoke(f,new object[]{.2f});
            check(f.State==BattleFlowState.Fighting&&Time.timeScale>0,"fight after countdown");
            fresh();f.NextBattle();unlock();s.Skip();
            check(!s.Active&&f.IsCountingDown,"skip enters countdown");
            float remaining=(float)Get(f,"countdownRemaining");s.Skip();f.NextBattle();
            check((float)Get(f,"countdownRemaining")==remaining,"skip cannot repeat start");
            fresh();f.NextBattle();unlock();s.Advance();menu.BackToTitle();
            check(f.State==BattleFlowState.Title&&!s.Active&&!f.IsCountingDown,"cancel discards callback");
            unlock();f.StartRun();unlock();f.NextBattle();
            check(s.Active&&s.PageIndex==0,"reenter restarts dialogue");
            menu.BackToTitle();unlock();f.BeginTutorial(false);unlock();t.Skip();unlock();f.NextBattle();
            check(s.Active&&f.State==BattleFlowState.Story,"tutorial skip goes through preparation and story");
            menu.BackToTitle();unlock();
            typeof(BattleFlow).GetMethod("PrepareNextRun",Hidden).Invoke(f,null);unlock();f.NextBattle();
            check(s.Active&&f.State==BattleFlowState.Story,"inheritance next run goes through story");
            unlock();s.Skip();unlock();
            for(int battle=1;battle<4;battle++)
            {
                Set(f,"<State>k__BackingField",BattleFlowState.BetweenBattles);
                Set(f,"<BattleNumber>k__BackingField",battle);
                Set(f,"<RewardResolved>k__BackingField",true);
                hp=p.CurrentHp;potions=p.PotionsRemaining;unlock();f.NextBattle();
                check(s.Active&&f.State==BattleFlowState.Story,"later battle dialogue "+battle);unlock();s.Skip();
                check(!s.Active&&f.IsCountingDown&&f.BattleNumber==battle+1,"later battles proceed without replaying forest "+battle);
                check(p.CurrentHp==hp&&p.PotionsRemaining==potions,"carry resources "+battle);
            }
            fresh();s.Content.forestDialogue=new StoryContent.DialogueLine[0];f.NextBattle();
            check(f.IsCountingDown&&!s.Active,"missing optional dialogue falls back safely");
            return "Forest Story: "+count+" checks passed (isolated save).";
        }
        finally
        {
            s.Content.forestDialogue=lines;menu.BackToTitle();Set(f,"inheritance",real);unlock();
        }
    }


    [MenuItem("Tools/Scroll Hunter/Check Battle Stories (Play)")]
    public static void BattleMenu()=>Debug.Log(RunBattles());

    public static string RunBattles()
    {
        if(!Application.isPlaying)throw new InvalidOperationException("Enter Play first.");
        var f=UnityEngine.Object.FindFirstObjectByType<BattleFlow>();
        var s=f.GetComponent<StoryUI>();
        var d=UnityEngine.Object.FindFirstObjectByType<DeckSystem>();
        var p=UnityEngine.Object.FindFirstObjectByType<Player>();
        var c=UnityEngine.Object.FindFirstObjectByType<CostSystem>();
        var enemies=UnityEngine.Object.FindFirstObjectByType<EnemyManager>();
        var metrics=UnityEngine.Object.FindFirstObjectByType<CombatMetrics>();
        var menu=f.GetComponent<MainMenuUI>();
        var real=Get(f,"inheritance");
        var savedLeader=s.Content.leaderDialogue;var savedBandits=s.Content.banditDialogue;var savedBoss=s.Content.bossDialogue;
        var path=Path.Combine(Application.temporaryCachePath,"BanditStories-"+Guid.NewGuid().ToString("N")+".json");
        int checks=0;
        Action<bool,string> check=(ok,label)=>{if(!ok)throw new Exception("Bandit stories: "+label);checks++;};
        Action unlock=()=>{Unlock(s);Set(f,"menuInputFrame",-1);};
        Action<int> between=battle=>{
            menu.BackToTitle();
            var save=new InheritanceSave(path,(BattleRewardOption[])Get(f,"rewardPool"),d.CopyStartingDeck());
            save.TryResetGame();save.TryRecordIntro();save.TryRecordTutorial();
            Set(f,"inheritance",save);unlock();f.StartRun();unlock();f.NextBattle();unlock();s.Skip();
            typeof(BattleFlow).GetMethod("AdvanceCountdown",Hidden).Invoke(f,new object[]{3.1f});
            p.TakeDamage(120);check(p.TryUsePotion(),"potion baseline");p.TakeDamage(120);
            d.EndBattle();p.EndBattle();enemies.WaitForBattle();metrics.WaitForBattle();
            Set(f,"<State>k__BackingField",BattleFlowState.BetweenBattles);
            Set(f,"<BattleNumber>k__BackingField",battle);
            typeof(BattleFlow).GetMethod("PrepareRewards",Hidden).Invoke(f,null);
            Set(f,"panelHoldRemaining",0f);unlock();
        };
        try
        {
            check(savedLeader.Length==14&&savedBandits.Length==18&&savedBoss.Length==20,"approved dialogue lengths");
            check(Get(s,"bossBackground")!=null&&s.Content.portraits[4].image!=null,"boss artwork assigned");
            check(savedBoss[1].text=="……도적들이 이런 곳에 숨어 지낸다고?"&&savedBoss[18].text=="모험가가 마법사를 경계하며 자세를 잡는다.","revised opening and class-neutral ending");
            check(Get(s,"caveEntranceBackground")!=null,"cave artwork assigned");
            check(savedLeader.Concat(savedBandits).All(x=>!x.text.Contains("일러스트")),"no illustration directions in script");
            check(savedBandits.Count(x=>x.speakerNameOverride=="도적단 말단들"&&x.portraitIndex==1&&x.secondaryPortraitIndex==2)==2,"group assignments");
            foreach(int battle in new[]{1,2,3})
            {
                between(battle);
                f.NextBattle();check(!s.Active&&f.State==BattleFlowState.BetweenBattles,"unresolved reward blocks story");
                f.SkipReward();unlock();
                check(f.BeginOrderEdit()&&f.MoveOrderCard(0,5),"reorder before cave");
                f.NextBattle();check(!s.Active&&f.IsEditingOrder,"draft blocks story");
                check(f.SaveOrder(),"save order");
                var expected=Enumerable.Range(0,8).Select(f.GetDeckCard).ToArray();
                float hp=p.CurrentHp,cp=c.Current;int potions=p.PotionsRemaining;
                check(hp<p.MaxHp&&potions<p.PotionCapacity,"non-full carry baseline");
                f.NextBattle();
                check(f.State==BattleFlowState.Story&&s.Active&&f.BattleNumber==battle+1,"correct story before countdown");
                check(!s.Advance(),"start input not reused");f.NextBattle();check(s.PageIndex==0,"duplicate start ignored");
                check(Time.timeScale==0f&&!f.IsCountingDown,"story pauses time");
                check(!d.TryUseSlot(0)&&!p.TryUsePotion()&&p.CurrentHp==hp&&c.Current==cp&&p.PotionsRemaining==potions,"input blocked and resources retained");
                check(!f.BeginOrderEdit(),"story blocks deck editor");
                menu.OpenPauseMenu();check(f.IsMenuPaused&&!s.Advance(),"Esc pauses dialogue");s.Skip();check(s.Active,"Esc blocks skip");menu.ResumeGame();
                var lines=battle==1?savedLeader:battle==2?savedBandits:savedBoss;
                var body=(TMP_Text)Get(s,"body");var name=(TMP_Text)Get(s,"speakerName");
                var portrait=(UnityEngine.UI.RawImage)Get(s,"portrait");
                var second=(UnityEngine.UI.RawImage)Get(s,"secondaryPortrait");
                var backdrop=(UnityEngine.UI.RawImage)Get(s,"backdrop");
                check(backdrop.texture==(Texture2D)Get(s,battle==3?"bossBackground":"caveEntranceBackground"),"correct cave background");
                for(int i=0;i<lines.Length;i++)
                {
                    var line=lines[i];bool enemy=line.speaker==StoryContent.Speaker.Enemy;
                    bool narr=line.speaker==StoryContent.Speaker.Narrator;bool group=line.secondaryPortraitIndex==2;
                    check(s.PageIndex==i&&body.text==(narr?"("+line.text+")":line.text),"approved line "+battle+"/"+i);
                    check(portrait.gameObject.activeSelf==enemy&&second.gameObject.activeSelf==(enemy&&group),"portrait count "+battle+"/"+i);
                    if(enemy)
                    {
                        check(name.text==(group?"도적단 말단들":battle==3?"마법사":"우두머리")&&name.alignment==TextAlignmentOptions.TopRight,"enemy name");
                        check(portrait.texture==s.Content.portraits[line.portraitIndex].image,"primary image");
                        if(group)
                        {
                            check(second.texture==s.Content.portraits[2].image,"archer image");
                            check(portrait.rectTransform.anchoredPosition.x<second.rectTransform.anchoredPosition.x-second.rectTransform.rect.width,"portraits separated");
                        }
                        else check(Mathf.Approximately(portrait.rectTransform.anchoredPosition.x,-(float)Get(s,"portraitRightMargin")),"single portrait position restored");
                    }
                    else if(narr)check(!name.gameObject.activeSelf&&(body.fontStyle&FontStyles.Italic)!=0,"narration format");
                    else check(name.text==s.Content.playerName&&name.alignment==TextAlignmentOptions.TopLeft,"player label");
                    body.ForceMeshUpdate();check(!body.isTextOverflowing,"body fits");
                    if(s.IsTyping){unlock();check(s.Advance()&&!s.IsTyping&&s.PageIndex==i,"reveal line");check(!s.Advance(),"reveal input consumed");}
                    unlock();check(s.Advance(),"next page");check(!s.Advance(),"one input one page");
                }
                check(!s.Active&&f.IsCountingDown&&Time.timeScale==0f,"last line starts countdown");
                check(enemies.EnemyCount==(battle==2?3:1),"correct encounter");
                check(p.CurrentHp==hp&&p.PotionsRemaining==potions,"HP/potions carry");
                check(Enumerable.Range(0,4).All(i=>d.GetHandCard(i)==expected[i]),"saved hand order");
                typeof(BattleFlow).GetMethod("AdvanceCountdown",Hidden).Invoke(f,new object[]{2.9f});check(f.IsCountingDown,"3s preparation retained");
                typeof(BattleFlow).GetMethod("AdvanceCountdown",Hidden).Invoke(f,new object[]{.2f});check(f.State==BattleFlowState.Fighting,"combat after countdown");
                between(battle);f.SkipReward();unlock();f.NextBattle();unlock();s.Skip();
                check(f.IsCountingDown&&!s.Active,"skip starts battle");
                float remaining=(float)Get(f,"countdownRemaining");s.Skip();f.NextBattle();check((float)Get(f,"countdownRemaining")==remaining,"skip no duplicate start");
                between(battle);f.SkipReward();unlock();f.NextBattle();unlock();s.Advance();menu.BackToTitle();unlock();s.Skip();
                check(f.State==BattleFlowState.Title&&!s.Active&&!f.IsCountingDown,"return to title cancels callback");
            }
            between(1);f.SkipReward();s.Content.leaderDialogue=null;unlock();f.NextBattle();check(f.IsCountingDown&&!s.Active,"missing leader fallback");s.Content.leaderDialogue=savedLeader;
            between(2);f.SkipReward();s.Content.banditDialogue=null;unlock();f.NextBattle();check(f.IsCountingDown&&!s.Active,"missing group fallback");s.Content.banditDialogue=savedBandits;
            between(3);f.SkipReward();s.Content.bossDialogue=null;unlock();f.NextBattle();check(f.IsCountingDown&&!s.Active&&f.BattleNumber==4,"missing boss fallback");s.Content.bossDialogue=savedBoss;
            return "Battle Stories: "+checks+" checks passed (isolated save; function/UI checks).";
        }
        finally
        {
            s.Content.leaderDialogue=savedLeader;s.Content.banditDialogue=savedBandits;s.Content.bossDialogue=savedBoss;
            menu.BackToTitle();Set(f,"inheritance",real);unlock();
        }
    }

    [MenuItem("Tools/Scroll Hunter/Check Intro Presentation (Play)")]
    public static void PresentationMenu()=>Debug.Log(RunPresentation());

    public static string RunPresentation()
    {
        if(!Application.isPlaying)throw new InvalidOperationException("Enter Play first.");
        var flow=UnityEngine.Object.FindFirstObjectByType<BattleFlow>();
        var story=flow.GetComponent<StoryUI>();
        var menu=flow.GetComponent<MainMenuUI>();
        var player=UnityEngine.Object.FindFirstObjectByType<Player>();
        var cost=UnityEngine.Object.FindFirstObjectByType<CostSystem>();
        var real=Get(flow,"inheritance");
        int checks=0;
        Action<bool,string> check=(ok,label)=>{if(!ok)throw new Exception("Intro presentation: "+label);checks++;};
        Action unlock=()=>{Unlock(story);Set(flow,"menuInputFrame",-1);};
        try
        {
            StorySetup.Preview();
            var fx=(IntroPresentation)Get(story,"introPresentation");
            var art=(UnityEngine.UI.RawImage)Get(story,"backdrop");
            var body=(TMP_Text)Get(story,"body");
            var particles=(UnityEngine.UI.Image[])Get(fx,"particles");
            var config=story.Content.introPresentation;
            check(config.artworkShader!=null&&!ShaderUtil.ShaderHasError(config.artworkShader),"shader compiles");
            check(flow.State==BattleFlowState.Story&&Time.timeScale==0,"story pauses combat");
            float hp=player.CurrentHp,cp=cost.Current;
            Func<byte> finalAlpha=()=>{
                int last=body.text.LastIndexOf('\n')+1;
                var info=body.textInfo;
                for(int i=0;i<info.characterCount;i++){
                    var ch=info.characterInfo[i];
                    if(ch.isVisible&&ch.index>=last)return info.meshInfo[ch.materialReferenceIndex].colors32[ch.vertexIndex].a;
                }
                throw new Exception("Ending line missing");
            };
            for(int page=0;page<5;page++)
            {
                check(story.PageIndex==page,"page entered "+page);
                unlock();check(fx.Busy&&!story.Advance(),"entrance locks advance "+page);
                check(body.GetComponent<CanvasGroup>().alpha==0,"text starts hidden "+page);
                var original=art.uvRect;
                fx.Tick(.6f);
                if(page==4){
                    check(finalAlpha()==0&&fx.Busy,"last sentence hidden before delay");
                    fx.Tick(.9f);
                    byte partial=finalAlpha();check(partial>0&&partial<255,"last sentence fades separately");
                    fx.Tick(.3f);check(finalAlpha()==255&&!fx.Busy,"last sentence fully shown");
                }
                check(body.GetComponent<CanvasGroup>().alpha==1,"text readable "+page);
                check(!body.isTextOverflowing,"text fits "+page);
                check(!fx.Busy,"entrance completes "+page);
                fx.Tick(10f);check(art.uvRect!=original,"artwork moves "+page);
                var region=story.Content.intro[page].visibleRegion;var uv=art.uvRect;
                check(uv.xMin>=region.xMin&&uv.yMin>=region.yMin&&uv.xMax<=region.xMax+.0001f&&uv.yMax<=region.yMax+.0001f,"crop stays inside original "+page);
                check(particles.Count(x=>x.gameObject.activeSelf)==(page==0?config.emberCount:page==3?config.dustCount:0),"scene particles "+page);
                check(art.material.GetFloat("_Style")==page+1,"scene material "+page);
                if(page==1){
                    float before=art.material.GetFloat("_Pulse");fx.Tick(.4f);
                    check(!Mathf.Approximately(before,art.material.GetFloat("_Pulse")),"scroll glyph pulse");
                }
                if(page==2)check(art.material.GetFloat("_Shade")>0,"conflict edge darkening");
                if(page==0){
                    menu.OpenPauseMenu();
                    float before=(float)Get(fx,"age");var crop=art.uvRect;
                    typeof(StoryUI).GetMethod("Update",Hidden).Invoke(story,null);
                    unlock();check(!story.Advance(),"pause blocks advance");story.Skip();
                    check(story.Active&&story.PageIndex==0,"pause blocks skip");
                    check((float)Get(fx,"age")==before&&art.uvRect==crop,"pause freezes presentation");
                    menu.ResumeGame();unlock();
                }
                check(player.CurrentHp==hp&&cost.Current==cp,"intro spends no combat resources "+page);
                unlock();check(story.Advance(),"begin departure "+page);
                check(!story.Advance(),"same frame input blocked "+page);
                unlock();check(!story.Advance()&&story.PageIndex==page,"transition blocks repeated input "+page);
                fx.Tick(.13f);
                check(body.GetComponent<CanvasGroup>().alpha==0,"text hides first "+page);
                check(art.color.a>0&&art.color.a<1,"art fades after text "+page);
                if(page==2){
                    fx.Tick(.1f);check(art.color.a==0&&story.PageIndex==2,"conflict black hold");
                    fx.Tick(.16f);
                }else fx.Tick(.08f);
                if(page<4)check(story.PageIndex==page+1,"one next page "+page);
                if(page==3)check(art.color.a==0,"village uses normal fade into traveler");
            }
            check(story.Active&&!story.ShowingIntro&&story.PageIndex==0,"ending continues to tutorial dialogue");
            check(!((RectTransform)Get(fx,"particleRoot")).gameObject.activeSelf,"intro particles cleared");
            var shade=(Color)Get(story,"dialogueShade");
            check(Mathf.Approximately(art.color.r,1-shade.a),"dialogue background shading preserved");
            check(body.GetComponent<CanvasGroup>().alpha==1,"dialogue opacity restored");
            StorySetup.Preview();fx.Tick(1f);unlock();story.Advance();unlock();story.Skip();fx.Tick(3f);
            check(story.Active&&!story.ShowingIntro&&story.PageIndex==0,"skip cancels pending page change");
            StorySetup.Preview();fx.Tick(1f);unlock();story.Advance();menu.BackToTitle();fx.Tick(3f);
            check(!story.Active&&flow.State==BattleFlowState.Title,"title cancels pending callback");
            StorySetup.Preview();check(story.PageIndex==0&&fx.Busy&&art.color.a==0,"reopen resets presentation");
            return "Intro Presentation: "+checks+" checks passed (isolated save; deterministic animation ticks).";
        }
        finally {menu.BackToTitle();Set(flow,"inheritance",real);unlock();}
    }

    [MenuItem("Tools/Scroll Hunter/Check Dialogue Typing (Play)")]
    public static void TypingMenu()=>Debug.Log(RunTyping());

    public static string RunTyping()
    {
        if(!Application.isPlaying)throw new InvalidOperationException("Enter Play first.");
        var flow=UnityEngine.Object.FindFirstObjectByType<BattleFlow>();
        var story=flow.GetComponent<StoryUI>();var menu=flow.GetComponent<MainMenuUI>();
        var real=Get(flow,"inheritance");
        int checks=0,completed=0;
        Action<bool,string> check=(ok,label)=>{if(!ok)throw new Exception("Dialogue typing: "+label);checks++;};
        Action<float> tick=dt=>typeof(StoryUI).GetMethod("TickDialogue",Hidden).Invoke(story,new object[]{dt});
        Action unlock=()=>{Unlock(story);Set(flow,"menuInputFrame",-1);};
        try
        {
            StorySetup.Preview();unlock();story.Skip();
            var body=(TMP_Text)Get(story,"body");
            check(!story.ShowingIntro&&!story.IsTyping&&body.maxVisibleCharacters==body.textInfo.characterCount,"narration shown immediately");
            check(!story.Advance(),"entry input cannot advance");
            check(body.text.StartsWith("(")&&body.text.EndsWith(")"),"narration keeps parentheses");
            unlock();check(story.Advance()&&story.PageIndex==1&&story.IsTyping&&body.maxVisibleCharacters==0,"one input advances narration into typed speech");
            check(!story.Advance(),"new speech cannot reveal in same frame");
            string full=body.text;var font=body.fontSize;float spacing=body.lineSpacing;
            tick(.1f);body.ForceMeshUpdate();
            check(body.maxVisibleCharacters==Mathf.FloorToInt(.1f*story.Content.dialogueCharactersPerSecond),"natural reveal speed");
            check(story.IsTyping&&body.text==full&&body.maxVisibleCharacters<body.textInfo.characterCount,"keeps full text and Korean character boundaries");
            var chars=body.textInfo.characterInfo.Take(body.textInfo.characterCount).Select(x=>x.lineNumber).ToArray();
            menu.OpenPauseMenu();int visible=body.maxVisibleCharacters;tick(10f);unlock();
            check(!story.Advance()&&body.maxVisibleCharacters==visible&&story.IsTyping,"pause freezes reveal and input");
            story.Skip();check(story.Active,"pause blocks skip");
            menu.ResumeGame();unlock();check(story.Advance(),"reveal accepted");
            check(!story.IsTyping&&story.PageIndex==1&&body.maxVisibleCharacters==body.textInfo.characterCount,"first input only reveals");
            check(!story.Advance()&&story.PageIndex==1,"same input cannot skip");
            body.ForceMeshUpdate();
            check(chars.SequenceEqual(body.textInfo.characterInfo.Take(body.textInfo.characterCount).Select(x=>x.lineNumber)),"word wrapping stays fixed");
            check(body.fontSize==font&&body.lineSpacing==spacing,"readability settings unchanged");
            unlock();check(story.Advance()&&story.PageIndex==2&&story.IsTyping&&body.maxVisibleCharacters==0,"second input starts next line");
            tick(10f);check(!story.IsTyping,"natural completion");
            unlock();check(story.Advance()&&story.PageIndex==3,"completed line advances directly");
            story.Cancel();tick(10f);
            check(!story.Active&&!story.IsTyping&&body.maxVisibleCharacters==int.MaxValue,"cancel clears typing");
            story.ShowTutorialDialogue(()=>completed++);
            for(int i=0;i<story.Content.tutorialDialogue.Length;i++){
                tick(20f);check(!story.IsTyping&&story.PageIndex==i,"natural line completion "+i);
                unlock();check(story.Advance(),"next natural line "+i);
            }
            check(!story.Active&&completed==1,"last line completes once");
            unlock();story.Advance();story.Skip();check(completed==1,"completion cannot repeat");
            story.ShowTutorialDialogue(()=>completed++);unlock();story.Advance();tick(.1f);unlock();story.Skip();tick(10f);
            check(!story.Active&&completed==2,"skip bypasses unfinished dialogue");
            StorySetup.Preview();
            check(story.ShowingIntro&&!story.IsTyping&&body.maxVisibleCharacters==int.MaxValue,"intro retains whole-text presentation");
            unlock();story.Skip();unlock();story.Advance();tick(.1f);menu.BackToTitle();tick(10f);
            check(flow.State==BattleFlowState.Title&&!story.Active&&!story.IsTyping,"return to title cancels reveal");
            return "Dialogue Typing: "+checks+" checks passed (isolated save).";
        }
        finally {menu.BackToTitle();Set(flow,"inheritance",real);unlock();}
    }
}
