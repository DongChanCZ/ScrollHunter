#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

// 임시 촬영 전용. 실제 전투 입력 경로를 사용하며 에셋/수치/실제 저장을 변경하지 않는다.
public sealed class TrailerHonestCapture : MonoBehaviour
{
    const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
    public static string Status="idle";
    [Serializable] public class ActionEvent { public int frame,arg; public string action,target,skill; public float hp,cost,lockBefore,castBefore; }
    [Serializable] public class SoundEvent { public int frame; public float volume; public string path; }
    [Serializable] public class TraceMark { public int frame,battle,phase,hp; public string name; }
    [Serializable] public class Report { public int frames; public List<ActionEvent> actions=new List<ActionEvent>(); public List<SoundEvent> sounds=new List<SoundEvent>(); public List<TraceMark> marks=new List<TraceMark>(); public List<string> errors=new List<string>(); }
    BattleFlow flow; DeckSystem deck; EnemyManager enemies; CostSystem cost; Player player; GameAudio gameAudio;
    InheritanceSave saved; GameLanguage languageBefore; bool shakeBefore; MainMenuUI menu;
    List<SkillData> startDeck; Report report=new Report(), replay; int replayIndex;
    string folder,ffmpeg; int frame,limit,nextAction,rewardStart=-1,bossStart=-1,previousPhase;
    System.Diagnostics.Process encoder;
    static object Get(object o,string n)=>o.GetType().GetField(n,F).GetValue(o);
    static void Set(object o,string n,object v)=>o.GetType().GetField(n,F).SetValue(o,v);
    static object Call(object o,string n,params object[] args)=>o.GetType().GetMethod(n,F).Invoke(o,args);
    public void Begin(string output,string executable,int language,string replayFile)
    {
        folder=output;ffmpeg=executable;Directory.CreateDirectory(folder);
        if(!string.IsNullOrEmpty(replayFile))replay=JsonUtility.FromJson<Report>(File.ReadAllText(replayFile));
        limit=replay!=null?replay.frames:7200;
        flow=FindFirstObjectByType<BattleFlow>();deck=FindFirstObjectByType<DeckSystem>();enemies=FindFirstObjectByType<EnemyManager>();
        cost=FindFirstObjectByType<CostSystem>();player=FindFirstObjectByType<Player>();gameAudio=FindFirstObjectByType<GameAudio>();
        saved=flow.Inheritance;languageBefore=GameText.Current;startDeck=deck.CopyStartingDeck();flow.ReturnToTitle();
        Set(flow,"inheritance",new InheritanceSave(Path.Combine(folder,"temporary-save.json"),(BattleRewardOption[])Get(flow,"rewardPool"),startDeck));
        Set(flow,"rewardRandom",new System.Random(1931));
        flow.GetComponent<TutorialFlow>().enabled=false;flow.GetComponent<StoryUI>().enabled=false;
        GameText.SetLanguage((GameLanguage)language);LanguageFonts.ApplyAll();
        menu=FindFirstObjectByType<MainMenuUI>();shakeBefore=menu.ScreenShakeEnabled;
        Set(menu,"<ScreenShakeEnabled>k__BackingField",false);
        gameAudio.Played+=OnSound;
        Assembly.Load("Assembly-CSharp-Editor").GetType("UiCapture").GetMethod("SetGameViewSize").Invoke(null,new object[]{1920,1080});
        EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView")).Focus();
        Application.runInBackground=true;Time.captureFramerate=30;EditorApplication.isPaused=false;
        StartCoroutine(Capture());
    }
    void OnSound(SoundCue cue,AudioClip clip,UnityEngine.Audio.AudioMixerGroup group)
    {report.sounds.Add(new SoundEvent{frame=frame,volume=cue.volume,path=AssetDatabase.GetAssetPath(clip)});}
    void Mark(string name,int phase=0)
    {report.marks.Add(new TraceMark{frame=frame,name=name,battle=flow.BattleNumber,phase=phase,hp=(int)player.CurrentHp});}
    void Perform(string action,int arg=0,string target="")
    {
        var ev=new ActionEvent{frame=frame,action=action,arg=arg,target=target,hp=player.CurrentHp,cost=cost.Current,lockBefore=deck.LockRemaining,castBefore=deck.CastRemaining};
        bool ok=true;
        switch(action)
        {
            case "begin":
                if(arg==0){Call(flow,"ClearRunRewards");Set(flow,"runDeck",new List<SkillData>(startDeck));}
                UnityEngine.Random.InitState(915+arg);
                Call(flow,"BeginBattle",arg,true,null);Time.timeScale=1f;
                Mark(arg==0?"forest-start":"boss-start");if(arg==3)bossStart=frame;break;
            case "target":
                var enemy=enemies.GetAliveEnemies().Find(e=>e.name==target);Call(enemies,"SelectTarget",enemy);ok=enemies.CurrentTarget==enemy;break;
            case "skill":
                ev.skill=deck.GetHandCard(arg)!=null?deck.GetHandCard(arg).name:"null";
                ok=deck.TryUseSlot(arg);
                if(ok && (ev.lockBefore>0.001f||ev.castBefore>0.001f))throw new Exception("Accepted skill while locked");
                break;
            case "potion":ok=player.TryUsePotion();break;
            case "reward":ok=flow.SelectReward(arg);Mark("reward-comparison");break;
            case "replace":ok=flow.SelectReplacementSlot(arg);break;
            case "confirm":ok=flow.ConfirmReplacement();break;
            case "skip":ok=flow.SkipReward();break;
        }
        if(!ok)report.errors.Add(frame+": rejected "+action+" "+arg+" "+target);
        report.actions.Add(ev);
    }
    void Decide()
    {
        if(frame==0){Perform("begin",0);nextAction=36;}
        if(flow.State==BattleFlowState.BetweenBattles && rewardStart<0 && !flow.ResultPanelHeld)
        {rewardStart=frame;Mark("reward-candidates");}
        if(rewardStart>=0 && bossStart<0)
        {
            int t=frame-rewardStart;
            if(t==90){int choice=0;for(int i=0;i<flow.RewardChoiceCount;i++)if(flow.GetRewardChoice(i).Card!=null){choice=i;break;}Perform("reward",choice);}
            if(t==270 && flow.SelectedReward!=null){Perform("replace",4);Perform("confirm");}
            if(t==360){Perform("begin",3);nextAction=frame+45;}
            return;
        }
        if(bossStart<0 && (frame==3300 || flow.State==BattleFlowState.Defeat)){Perform("begin",3);nextAction=frame+45;}
        if(bossStart>=0 && (frame-bossStart>=3300 || flow.State==BattleFlowState.Defeat || flow.State==BattleFlowState.Victory))
        {if(frame-bossStart>150)limit=Math.Min(limit,frame+1);}
        if(flow.State!=BattleFlowState.Fighting || frame<nextAction || deck.IsLocked || player.IsStunned)return;
        if(frame%3!=0)return;
        if(player.CurrentHp<player.MaxHp*.55f && player.CanUsePotion){Perform("potion");nextAction=frame+18;return;}
        var alive=enemies.GetAliveEnemies();
        Enemy danger=null;
        foreach(var enemy in alive)
            if(!enemy.IsBossOrb && enemy.IsCasting && enemy.CastColor!=CastColor.Red && enemy.CastProgress01>.65f
                && enemy.CurrentCastTime*(1-enemy.CastProgress01)>.28f
                && (danger==null || enemy.CastColor==CastColor.Orange))danger=enemy;
        int slot=-1;
        if(danger!=null)
            for(int i=0;i<4;i++){var s=deck.GetHandCard(i);if(s!=null && s.Category==SkillCategory.Interrupt && cost.CanAfford(s.Cost)){slot=i;break;}}
        if(slot>=0)
        {if(enemies.CurrentTarget!=danger)Perform("target",0,danger.name);Use(slot);return;}
        // 방어도와 차단을 실제 비용으로 확보하며, 시전/채널링 중 입력을 추가하지 않는다.
        for(int i=0;i<4;i++)
        {var s=deck.GetHandCard(i);if(s!=null && s.Category==SkillCategory.Shield && player.Shield<75 && deck.GetSlotState(i)==SlotState.Ready){Use(i);return;}}
        var focus=enemies.CurrentTarget;
        if(focus==null||focus.IsBossOrb||!focus.IsAlive)
            foreach(var e in alive)if(!e.IsBossOrb && (focus==null||focus.IsBossOrb||e.CurrentHp<focus.CurrentHp))focus=e;
        if(focus!=null && focus!=enemies.CurrentTarget)Perform("target",0,focus.name);
        float best=-1;
        for(int i=0;i<4;i++)
        {
            var s=deck.GetHandCard(i);if(s==null||s.Category!=SkillCategory.Deal||deck.GetSlotState(i)!=SlotState.Ready)continue;
            bool soon=false;foreach(var e in alive)if(!e.IsBossOrb && e.IsCasting && e.CastColor==CastColor.Orange && e.CastProgress01>.3f)soon=true;
            if(soon && cost.Current-s.Cost<2 && cost.Current<6)continue;
            float score=s.TotalDamage*(s.IsAreaOfEffect?Math.Min(alive.Count,3):1)/Mathf.Max(1,s.Cost);
            if(score>best){best=score;slot=i;}
        }
        if(slot>=0)Use(slot);
    }
    void Use(int slot)
    {var s=deck.GetHandCard(slot);Perform("skill",slot);nextAction=frame+Mathf.CeilToInt(Mathf.Max(.65f,s.CastTime+.2f)*30f);}
    IEnumerator Capture()
    {
        yield return null;yield return new WaitForEndOfFrame();
        try
        {
            for(frame=0;frame<limit;frame++)
            {
                // 두 언어가 같은 확률 판정을 재현하도록 프레임별 난수 입력만 고정한다.
                // 치명타 확률·피해·시전·회복 수치는 게임 원본 그대로다.
                UnityEngine.Random.InitState(1931+frame*997);
                if(replay==null)Decide();
                else while(replayIndex<replay.actions.Count && replay.actions[replayIndex].frame==frame)
                {var a=replay.actions[replayIndex++];Perform(a.action,a.arg,a.target);}
                foreach(var e in enemies.GetAliveEnemies())if(e.HasPhases && e.PhaseNumber!=previousPhase){previousPhase=e.PhaseNumber;Mark("phase",previousPhase);}
                yield return new WaitForEndOfFrame();
                var tex=ScreenCapture.CaptureScreenshotAsTexture();
                if(encoder==null)
                {
                    string fmt=tex.format==TextureFormat.RGB24?"rgb24":"rgba";
                    var p=new System.Diagnostics.ProcessStartInfo(ffmpeg,"-y -loglevel error -f rawvideo -pixel_format "+fmt+" -video_size "+tex.width+"x"+tex.height+" -framerate 30 -i pipe:0 -vf vflip -an -c:v libx264 -threads 3 -preset ultrafast -crf 18 -pix_fmt yuv420p \""+Path.Combine(folder,"raw.mp4")+"\"");
                    p.UseShellExecute=false;p.CreateNoWindow=true;p.RedirectStandardInput=true;p.RedirectStandardError=true;
                    encoder=System.Diagnostics.Process.Start(p);encoder.ErrorDataReceived+=(s,e)=>{if(e.Data!=null)File.AppendAllText(Path.Combine(folder,"encode-errors.txt"),e.Data+"\n");};encoder.BeginErrorReadLine();
                }
                byte[] bytes=tex.GetRawTextureData();encoder.StandardInput.BaseStream.Write(bytes,0,bytes.Length);Destroy(tex);
                if(frame%60==0){Status="frame "+frame+"/"+limit+" "+flow.State+" HP "+player.CurrentHp+" battle "+flow.BattleNumber;File.WriteAllText(Path.Combine(folder,"progress.txt"),Status);}
            }
            Status="complete";
        }
        finally
        {
            if(encoder!=null){encoder.StandardInput.Close();encoder.WaitForExit(20000);}
            report.frames=frame;File.WriteAllText(Path.Combine(folder,"report.json"),JsonUtility.ToJson(report,true));File.WriteAllText(Path.Combine(folder,"progress.txt"),Status);
            gameAudio.Played-=OnSound;Time.captureFramerate=0;flow.ReturnToTitle();Set(flow,"inheritance",saved);
            Set(menu,"<ScreenShakeEnabled>k__BackingField",shakeBefore);GameText.SetLanguage(languageBefore);LanguageFonts.ApplyAll();
        }
    }
}
#endif
