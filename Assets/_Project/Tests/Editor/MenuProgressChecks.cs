using System;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

/// <summary>옵션·진행 기록·Esc 회귀. 임시 저장 경로와 임시 옵션 키만 사용한다.</summary>
public static class MenuProgressChecks
{
    const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    static object Get(object o,string name) => o.GetType().GetField(name,Hidden).GetValue(o);
    static void Set(object o,string name,object value) => o.GetType().GetField(name,Hidden).SetValue(o,value);
    static object Call(object o,string name,params object[] args) => o.GetType().GetMethod(name,Hidden).Invoke(o,args);
    [MenuItem("Tools/Scroll Hunter/Check Options and Progress (Play)")]
    public static void Menu() => Debug.Log(Run());
    public static string Run()
    {
        if(!Application.isPlaying)throw new InvalidOperationException("Fresh Play required");
        var flow=UnityEngine.Object.FindFirstObjectByType<BattleFlow>();
        var menu=flow.GetComponent<MainMenuUI>();
        var tutorial=UnityEngine.Object.FindFirstObjectByType<TutorialFlow>();
        var deck=UnityEngine.Object.FindFirstObjectByType<DeckSystem>();
        var cost=UnityEngine.Object.FindFirstObjectByType<CostSystem>();
        var manager=UnityEngine.Object.FindFirstObjectByType<EnemyManager>();
        var info=UnityEngine.Object.FindFirstObjectByType<CombatInfoUI>();
        var pool=(BattleRewardOption[])Get(flow,"rewardPool");
        var realSave=Get(flow,"inheritance");
        bool tutorialOn=tutorial.enabled;
        float finish=(float)Get(flow,"finishingEffectHold"),defeat=(float)Get(flow,"defeatEffectHold"),speed=info.BattleSpeed;
        var oldPrefix=(string)Get(menu,"preferencesPrefix");
        var prefix="ScrollHunter.Options.MenuCheck-"+Guid.NewGuid().ToString("N")+".";
        var path=Path.Combine(Application.temporaryCachePath,"MenuProgressCheck-"+Guid.NewGuid().ToString("N"),"save.json");
        var save=new InheritanceSave(path,pool,deck.CopyStartingDeck());
        Func<InheritanceSave> load=()=>new InheritanceSave(path,pool,deck.CopyStartingDeck());
        var sliders=(Slider[])Get(menu,"volumeSliders");
        var parameters=(string[])Get(menu,"mixerParameters");
        var mixer=(AudioMixer)Get(menu,"mixer");
        var resetPanel=(GameObject)Get(menu,"resetPanel");
        Action nextFrame=()=>{Set(flow,"menuInputFrame",-1);Set(tutorial,"consumedFrame",-1);};
        int passed=0;Action<bool,string> check=(ok,label)=>{if(!ok)throw new Exception("Options/progress: "+label);passed++;};
        try
        {
            menu.BackToTitle();nextFrame();Set(flow,"inheritance",save);Set(menu,"preferencesPrefix",prefix);
            Set(flow,"finishingEffectHold",0f);Set(flow,"defeatEffectHold",0f);
            check(sliders.Length==4&&parameters.SequenceEqual(new[]{"MasterVolume","MusicVolume","SkillVolume","EnemyVolume"}),"four volume groups");
            var previous=(Button)Get(menu,"resolutionPrevious");var next=(Button)Get(menu,"resolutionNext");
            check(previous.GetComponentInChildren<TMP_Text>().text=="◀"&&next.GetComponentInChildren<TMP_Text>().text=="▶","resolution triangles");
            var suppression=AssetDatabase.LoadAssetAtPath<SkillData>("Assets/_Project/Data/ScrollHunter/SkillData/Skill_SK12_Suppression.asset");
            var description=info.DescribeCard(suppression);
            check(description.Contains("적 캐스팅 중에만")&&!description.Contains("생존 시"),"suppression wording");
            menu.OpenOptions();
            for(int i=0;i<sliders.Length;i++)
            {
                sliders[i].value=.2f+i*.15f;mixer.GetFloat(parameters[i],out float db);
                check(Mathf.Abs(db-20f*Mathf.Log10(sliders[i].value))<.01f,"live volume "+i);
                check(!PlayerPrefs.HasKey(prefix+parameters[i]),"preview not saved "+i);
            }
            menu.ClosePanels();
            for(int i=0;i<sliders.Length;i++){mixer.GetFloat(parameters[i],out float db);check(Mathf.Abs(db)<.01f,"cancel restores saved volume "+i);}
            menu.OpenOptions();for(int i=0;i<sliders.Length;i++)sliders[i].value=.3f+i*.1f;
            menu.ApplyOptions();
            for(int i=0;i<sliders.Length;i++)check(Mathf.Abs(PlayerPrefs.GetFloat(prefix+parameters[i])-sliders[i].value)<.001f,"apply persists "+i);
            menu.OpenOptions();sliders[0].value=0f;mixer.GetFloat("MasterVolume",out float muted);check(muted<=-79.9f,"master mute");
            menu.HandleEscape();mixer.GetFloat("MasterVolume",out float restored);check(!menu.OptionsVisible&&Mathf.Abs(restored-20f*Mathf.Log10(.3f))<.01f,"Escape discards preview");

            tutorial.enabled=true;flow.StartRun();
            check(tutorial.Active&&tutorial.ShowingGuide&&!save.TutorialDismissed,"first run tutorial");
            int lesson=tutorial.Lesson;menu.OpenPauseMenu();
            check(flow.IsMenuPaused&&Time.timeScale==0&&menu.PauseMenuVisible,"tutorial menu freeze");
            check(!tutorial.AdvanceGuide()&&!deck.TryUseSlot(0),"menu blocks guide and card");tutorial.Skip();
            check(tutorial.Active&&tutorial.Lesson==lesson,"skip blocked behind menu");
            menu.BackToTitle();nextFrame();
            check(flow.State==BattleFlowState.Title&&!tutorial.Active&&!tutorial.ShowingGuide&&!load().TutorialDismissed,"aborted tutorial not recorded and overlay cleared");
            flow.StartRun();nextFrame();tutorial.Skip();
            check(flow.State==BattleFlowState.Preparing&&load().TutorialDismissed,"skip persisted");
            flow.ReturnToTitle();Set(flow,"inheritance",load());flow.StartRun();
            check(flow.State==BattleFlowState.Preparing&&!tutorial.Active&&!tutorial.ShowingGuide,"reload skips tutorial");
            flow.NextBattle();check(flow.IsCountingDown,"preparation countdown");
            float remaining=(float)Get(flow,"countdownRemaining");menu.OpenPauseMenu();Call(flow,"Update");
            check(flow.IsCountingDown&&(float)Get(flow,"countdownRemaining")==remaining&&Time.timeScale==0,"menu freezes real-time countdown");
            menu.ResumeGame();check(Time.timeScale==0,"countdown stays frozen until countdown advances");nextFrame();Call(flow,"AdvanceCountdown",3f);
            check(flow.State==BattleFlowState.Fighting,"combat begins");
            Set(info,"resumeScale",.5f);Time.timeScale=.5f;
            menu.OpenPauseMenu();info.TogglePause();info.ToggleSpeed();
            check(flow.IsMenuPaused&&Time.timeScale==0&&info.BattleSpeed==.5f&&!deck.TryUseSlot(0),"A S skills blocked");
            menu.OpenOptions();sliders[1].value=.4f;menu.HandleEscape();
            check(!menu.OptionsVisible&&menu.PauseMenuVisible&&Time.timeScale==0,"options Esc stays paused");
            menu.HandleEscape();check(!menu.PauseMenuVisible&&!flow.IsMenuPaused&&Time.timeScale==.5f,"menu Esc restores speed");
            check(!deck.TryUseSlot(0),"closing click frame blocked");nextFrame();
            info.TogglePause();check(info.IsInfoPaused&&Time.timeScale==0,"manual pause before menu");
            menu.OpenPauseMenu();menu.ResumeGame();check(info.IsInfoPaused&&Time.timeScale==0,"manual pause preserved");nextFrame();info.Resume();
            check(Time.timeScale==.5f,"manual resume speed preserved");
            menu.OpenPauseMenu();menu.OpenOptions();sliders[2].value=.7f;menu.ApplyOptions();
            check(menu.PauseMenuVisible&&flow.IsMenuPaused&&Time.timeScale==0&&PlayerPrefs.GetFloat(prefix+parameters[2])==.7f,"in-game apply saves without resuming");
            menu.BackToTitle();nextFrame();
            check(flow.State==BattleFlowState.Title&&manager.CombatEnded&&!deck.IsCasting&&!menu.PauseMenuVisible,"title abort cleans battle");

            // 실제 승리 처리로 스테이지 기록을 저장한다.
            tutorial.enabled=false;flow.RestartRun();
            foreach(var enemy in manager.GetAliveEnemies().ToArray())enemy.TakeDamage(100000);
            Call(manager,"Update");Call(flow,"Update");
            check((load().ClearedBattles&1)!=0,"stage clear persisted");
            flow.ReturnToTitle();save=load();Set(flow,"inheritance",save);tutorial.enabled=true;flow.StartRun();
            check(flow.State==BattleFlowState.Preparing&&!tutorial.Active,"clear record skips tutorial");

            flow.ReturnToTitle();check(save.TryResetGame(),"reset setup");
            flow.StartRun();Set(tutorial,"<CompletedLessons>k__BackingField",6);Call(tutorial,"HideGuide");tutorial.Enemy.TakeDamage(100000);tutorial.TickCombat();
            check(load().TutorialDismissed&&flow.State==BattleFlowState.TutorialComplete,"tutorial clear persisted");
            flow.ReturnToTitle();save=load();Set(flow,"inheritance",save);
            check(save.TryInherit(pool.First(x=>x.Card!=null),0),"inherited card setup");
            check(save.TryInherit(pool.First(x=>x.Effect!=null&&x.Inheritable),-1),"inherited passive setup");
            check(save.TryRecordBattle(4),"boss clear setup");
            string before=File.ReadAllText(path);menu.OpenOptions();menu.AskReset();
            check(resetPanel.activeSelf&&((TMP_Text)Get(menu,"resetMessage")).text.Contains("정말 게임을 초기화 하시겠습니까?"),"reset confirmation");
            ((Button)Get(menu,"resetNo")).onClick.Invoke();
            check(!resetPanel.activeSelf&&File.ReadAllText(path)==before&&save.HasAny,"No preserves all progress");
            menu.AskReset();menu.HandleEscape();check(!resetPanel.activeSelf&&menu.OptionsVisible&&File.ReadAllText(path)==before,"Esc cancels confirmation only");
            menu.AskReset();((Button)Get(menu,"resetYes")).onClick.Invoke();
            var empty=load();check(!empty.HasAny&&!empty.TutorialDismissed&&empty.ClearedBattles==0&&!File.Exists(path+".bak"),"Yes resets all and stale backup");
            check(flow.State==BattleFlowState.Title&&!menu.OptionsVisible&&!resetPanel.activeSelf,"reset returns title");
            check(PlayerPrefs.GetFloat(prefix+parameters[2])==.7f&&PlayerPrefs.HasKey(prefix+"Width"),"reset preserves options");
            nextFrame();flow.StartRun();check(tutorial.Active&&tutorial.ShowingGuide,"reset restores first-run tutorial");
            // 기존 버전1 저장 파일에서 새 필드가 없어도 읽을 수 있다.
            flow.ReturnToTitle();File.WriteAllText(path,"{\"version\":1,\"cards\":[null,null,null,null,null,null,null,null],\"passives\":[]}");
            var legacy=load();check(!legacy.LoadFailed&&!legacy.TutorialDismissed&&legacy.ClearedBattles==0,"legacy save compatible");
            // 기록 저장 실패 시 메모리·기존 데이터도 성공으로 취급하지 않는다.
            var blocked=Path.Combine(Path.GetDirectoryName(path),"blocked");Directory.CreateDirectory(blocked);
            var failed=new InheritanceSave(blocked,pool,deck.CopyStartingDeck());
            check(!failed.TryRecordTutorial()&&!failed.TutorialDismissed&&!failed.TryResetGame(),"write failure leaves state intact");
            Set(flow,"inheritance",failed);menu.OpenOptions();menu.AskReset();menu.ConfirmReset();
            check(resetPanel.activeSelf&&menu.OptionsVisible&&((TMP_Text)Get(menu,"resetMessage")).text.Contains("초기화하지 못했습니다"),"reset failure visible");
            return "Options/progress: "+passed+" passed. Isolated save: "+path+". Physical Escape/drag/display switch separate.";
        }
        finally
        {
            menu.BackToTitle();nextFrame();Set(flow,"inheritance",realSave);tutorial.enabled=tutorialOn;
            Set(flow,"finishingEffectHold",finish);Set(flow,"defeatEffectHold",defeat);Set(info,"resumeScale",speed);
            foreach(var name in parameters.Concat(new[]{"Windowed","Width","Height"}))PlayerPrefs.DeleteKey(prefix+name);
            PlayerPrefs.Save();Set(menu,"preferencesPrefix",oldPrefix);Call(menu,"ReadSavedOptions");Call(menu,"ApplyAudio");
            flow.ReturnToTitle();
        }
    }
}
