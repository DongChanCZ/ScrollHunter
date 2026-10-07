using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>보스 사양·전환 경계·현재 5전투 연결 검사. 자연 플레이와 구분한다.</summary>
public static class BossChecks
{
    const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    static object Get(object o, string n) => o.GetType().GetField(n, Hidden).GetValue(o);
    static void Set(object o, string n, object v) => o.GetType().GetField(n, Hidden).SetValue(o, v);
    static object Call(object o, string n, params object[] args) => o.GetType().GetMethod(n, Hidden).Invoke(o, args);
    static T Find<T>() where T : UnityEngine.Object => UnityEngine.Object.FindFirstObjectByType<T>();
    static Enemy Boss() => UnityEngine.Object.FindObjectsByType<Enemy>(FindObjectsInactive.Include, FindObjectsSortMode.None).Single(e => e.Data.name == "Enemy_Boss");
    static bool Near(float a, float b) => Mathf.Abs(a - b) < .001f;
    static void FreshBoss() => Call(Find<BattleFlow>(), "BeginBattle", 3, true, null);
    static void Tick(Enemy e, float dt) => Call(e, "AdvanceCombat", dt);

    [MenuItem("Tools/Scroll Hunter/Check Boss (Play)")]
    public static void Menu() => Debug.Log(Run());
    public static string Run()
    {
        if (!Application.isPlaying) throw new InvalidOperationException("Play required");
        var f=Find<BattleFlow>();var t=Find<TutorialFlow>();var e=Boss();var p=Find<Player>();
        var d=Find<DeckSystem>();var c=Find<CostSystem>();var m=Find<EnemyManager>();
        var metrics=Find<CombatMetrics>();var info=Find<CombatInfoUI>();var ui=Find<CombatUI>();
        bool enabled=t.enabled;float hold=(float)Get(f,"finishingEffectHold");var random=UnityEngine.Random.state;
        int passed=0;Action<bool,string> check=(ok,msg)=>{if(!ok)throw new Exception("Boss: "+msg);passed++;};
        int[][] patterns={new[]{1,2,1,3},new[]{1,1,4,1,3,1,2},new[]{5,1,4,1,3,1}};
        try
        {
            t.enabled=false;Set(f,"finishingEffectHold",0f);f.RestartRun();FreshBoss();
            check(f.BattleCount==4 && f.BattleNumber==4 && m.EnemyCount==1 && m.CurrentTarget==e,"final encounter and target");
            check(e.transform.position.x==m.FormationCenter.x && e.MaxHp==4000 && e.Data.Defense==0,"HP defense centered");
            check(p.PotionCapacity == 3 && p.PotionsRemaining == 3 && p.PotionHealAmount==150,"base potion 3/3 and 30%");
            check(e.Data.Attacks.Select(a=>a.Damage).SequenceEqual(new[]{35,40,300,60,600}),"raw attack damage");
            check(e.Data.Attacks.Select(a=>a.CastTime).SequenceEqual(new float[]{2,3,6,5,20}),"base casts");
            check(e.Data.Attacks.Select(a=>a.StunSeconds).SequenceEqual(new float[]{0,2,0,5,0}),"nightmare exception");
            check(e.Data.Phases.Select(x=>x.HpThreshold).SequenceEqual(new[]{1f,.6f,.25f}),"thresholds");
            for(int phase=0;phase<3;phase++)
            {
                FreshBoss();
                if(phase>0){e.TakeDamage(phase==1?1600:3000);check(!e.IsPhaseTransitioning,"pending cast "+phase);e.TryInterrupt(2.5f);Tick(e,2.5f);}
                check(e.PhaseNumber==phase+1 && e.Data.Phases[phase].Pattern.SequenceEqual(patterns[phase]),"phase/order "+phase);
                // Two complete cycles check cursor wrap, cast multiplier and both same-color attacks.
                for(int i=0;i<patterns[phase].Length*2;i++)
                {
                    var a=e.Data.Attacks[patterns[phase][i%patterns[phase].Length]-1];
                    check(e.CurrentAttack==a && e.IsCasting,"sequence "+phase+"/"+i);
                    check(Near(e.CurrentCastTime,a.CastTime*(phase==0?1f:phase==1?.6f:.8f)),"effective cast "+phase+"/"+i);
                    check(info.DescribeEnemy(e).Contains(e.CurrentCastTime.ToString("0.0")) || info.DescribeEnemy(e).Contains(e.CurrentCastTime.ToString()),"tooltip effective cast");
                    p.BeginBattle(true);p.AddShield(10000);Call(e,"Fire");
                    check(Near(p.StunRemaining,a.StunSeconds),"stun duration");
                    p.BeginBattle(true);Tick(e,a.StaggerAfterCast-.01f);check(!e.IsCasting,"rest before boundary");
                    Tick(e,.011f);check(e.IsCasting,"rest ends");
                }
            }
            // 모든 페이즈에서 초록은 같은 순번, 주황은 다음 순번. 초록 반복 차단도 유지한다.
            for(int phase=0;phase<3;phase++)
            {
                for(int i=0;i<patterns[phase].Length;i++)
                {
                    FreshBoss();Set(e,"phaseIndex",phase);Set(e,"phasePatternIndex",i);
                    Set(e,"<CurrentHp>k__BackingField",phase==0?4000:phase==1?2000:900);Call(e,"BeginNextCast");
                    var attack=e.CurrentAttack;
                    if(attack.CastColor==CastColor.Red)continue;
                    check(e.TryInterrupt(2.5f)==InterruptResult.Success,"interrupt accepted "+phase+"/"+i);
                    Tick(e,2.49f);check(!e.IsCasting && e.IsStaggered,"full stagger "+phase+"/"+i);
                    Tick(e,.02f);
                    int expected=attack.CastColor==CastColor.Green?i:(i+1)%patterns[phase].Length;
                    check((int)Get(e,"phasePatternIndex")==expected && e.CurrentAttack==e.Data.Attacks[patterns[phase][expected]-1] && e.CastProgress01==0,"interrupt cursor "+phase+"/"+i);
                    if(attack.CastColor==CastColor.Green)
                    {
                        e.TryInterrupt(2.5f);Tick(e,2.5f);check(e.CurrentAttack==attack && (int)Get(e,"phasePatternIndex")==i,"repeat green interrupt");
                        p.AddShield(10000);Call(e,"Fire");Tick(e,attack.StaggerAfterCast);
                        check((int)Get(e,"phasePatternIndex")==((i+1)%patterns[phase].Length),"fired green advances once");
                    }
                }
            }
            FreshBoss();e.TakeDamage(1599);e.TryInterrupt(2.5f);check(e.PhaseNumber==1,"above sixty stays one");Tick(e,2.5f);
            e.TakeDamage(1);check(e.IsCasting && !e.IsInvulnerable,"exact sixty waits for attack");
            Call(e,"Fire");check(e.PhaseNumber==2 && e.IsInvulnerable && Near(e.PhaseTransitionRemaining,2),"fire starts transition");
            Tick(e,1.999f);check(e.IsInvulnerable && !e.IsCasting,"transition not early");Tick(e,.002f);check(e.IsCasting && !e.IsInvulnerable,"transition ends after rest");
            FreshBoss();e.TakeDamage(1600);e.TryInterrupt(2.5f);Call(e,"Start");check(e.IsStaggered && e.IsInvulnerable,"first Start preserves transition and stagger");Tick(e,2);
            check(!e.IsInvulnerable && !m.SkillInputBlocked && e.IsStaggered && !e.IsCasting,"2s unlock leaves .5s stagger");
            Tick(e,.499f);check(!e.IsCasting,"stagger not early");Tick(e,.002f);check(e.IsCasting && Near(e.CurrentCastTime,1.2f),"concurrent total 2.5 not 4.5");
            e.TakeDamage(1399);e.TryInterrupt(2.5f);check(e.PhaseNumber==2,"above twenty five stays two");Tick(e,2.5f);
            e.TakeDamage(1);e.TryInterrupt(2.5f);Tick(e,2.5f);check(e.PhaseNumber==3 && e.CurrentAttack==e.Data.Attacks[4] && e.CurrentCastTime==16,"phase3 starts five, cycle orb -20%");
            check(e.TryInterrupt(2.5f)==InterruptResult.FailedRed && e.IsCasting,"red cannot interrupt");
            FreshBoss();e.TakeDamage(3100);e.TryInterrupt(2.5f);check(e.PhaseNumber==3,"skip phase2");
            FreshBoss();Call(e,"Fire");e.TakeDamage(1600);check(e.IsInvulnerable,"threshold while resting starts now");Tick(e,2);check(e.IsCasting,"rest and transition parallel");

            FreshBoss();c.EnsureTutorialCost(10);var cards=d.CopyStartingDeck();var cast=cards.First(x=>!x.IsChanneling && x.Damage>0 && x.CastTime>0);
            cards[0]=cast;d.SetDeck(cards);check(d.TryUseSlot(0) && d.IsCasting,"ordinary cast active");
            e.TakeDamage(1600);e.TryInterrupt(2.5f);int hp=e.CurrentHp,hits=metrics.RecordedHits,crit=metrics.CriticalEligibleHits,damage=metrics.DamageApplied,overkill=metrics.OverkillDamage;
            float cost=c.Current;var hand=d.GetHandCard(0);
            check(Enumerable.Range(0,4).All(i=>!d.TryUseSlot(i)) && c.Current==cost && d.GetHandCard(0)==hand,"transition rejects all without cost/cycle");
            Call(ui,"UpdateHand");check(d.GetSlotState(0)==SlotState.PhaseTransition,"transition state distinct");
            int numbers=0;Action<Vector3,int,bool> onDamage=(v,n,k)=>numbers++;Enemy.DamageTaken+=onDamage;
            try {e.TakeDamage(300);Call(d,"AdvanceCast",cast.CastTime+.1f);}
            finally {Enemy.DamageTaken-=onDamage;}
            check(!d.IsCasting && e.CurrentHp==hp && numbers==0,"ordinary cast finishes, immunity no numbers");
            check(metrics.RecordedHits==hits && metrics.CriticalEligibleHits==crit && metrics.DamageApplied==damage && metrics.OverkillDamage==overkill,"blocked hit no metrics or crit");
            p.TakeDamage(100);check(p.CurrentHp<500 && p.TryUsePotion() && p.PotionsRemaining == 2,"only boss immune, potion usable");
            float transition=e.PhaseTransitionRemaining;info.TogglePause();Tick(e,1);
            check(Time.timeScale==0 && e.PhaseTransitionRemaining==transition && !p.TryUsePotion(),"manual pause freezes transition and potion");info.Resume();
            check(Time.timeScale>0,"manual resume");Tick(e,2.5f);c.EnsureTutorialCost(10);check(d.TryUseSlot(0),"new input accepted after transition");

            FreshBoss();cards=d.CopyStartingDeck();var channel=cards.First(x=>x.IsChanneling);cards[0]=channel;d.SetDeck(cards);c.EnsureTutorialCost(10);
            check(d.TryUseSlot(0) && d.IsChanneling,"channel starts");e.TakeDamage(1600);e.TryInterrupt(2.5f);hp=e.CurrentHp;hits=metrics.RecordedHits;
            Call(d,"AdvanceCast",1f);check(d.IsChanneling && e.CurrentHp==hp && metrics.RecordedHits==hits,"channel continues during immunity without fake hits");
            Tick(e,2.5f);Call(d,"AdvanceCast",2f);check(e.CurrentHp<hp,"remaining ticks damage after immunity");

            FreshBoss();e.TakeDamage(4000);m.NotifyEnemyDied();Call(f,"Update");
            check(!e.IsAlive && !e.IsInvulnerable && f.State==BattleFlowState.Victory && f.RewardChoiceCount==0,"lethal wins over phase and final has no reward");
            float finalHp=p.CurrentHp;Tick(e,100);f.NextBattle();check(p.CurrentHp==finalHp && f.BattleNumber==4,"no attacks or fifth ordinary battle after victory");
            f.RestartRun();p.TakeDamage(60);p.TryUsePotion();p.TakeDamage(30);float carry=p.CurrentHp;
            for(int stage=0;stage<3;stage++)
            {
                foreach(var enemy in m.GetAliveEnemies())enemy.TakeDamage(10000);m.NotifyEnemyDied();Call(f,"Update");
                check(f.State==BattleFlowState.BetweenBattles && f.RewardChoiceCount>0,"reward after ordinary battle "+stage);
                check(f.SkipReward(),"skip "+stage);f.NextBattle();Call(f,"AdvanceCountdown",3f);
                check(p.CurrentHp==carry && p.PotionsRemaining == 2,"HP/potion carry "+stage);
            }
            check(m.CurrentTarget==e && e.CurrentHp==4000 && e.PhaseNumber==1,"ABC to fresh boss");
            e.TakeDamage(3100);e.TryInterrupt(2.5f);p.TakeDamage(100000);Call(f,"Update");check(f.State==BattleFlowState.Defeat,"player defeat during transition");
            f.RestartRun();check(p.PotionsRemaining == 3 && p.PotionCapacity == 3 && !m.SkillInputBlocked,"restart clears transition, potion 3/3");FreshBoss();
            check(e.CurrentHp==4000 && e.PhaseNumber==1 && e.CurrentAttack==e.Data.Attacks[0],"boss restart resets phase cursor");
            return "Boss checks passed: "+passed;
        }
        finally{UnityEngine.Random.state=random;Set(f,"finishingEffectHold",hold);t.enabled=enabled;f.RestartRun();}
    }

    static int frame, ticks, mode, pausedFrames;
    static float elapsed, transitionEnded, savedCapture;
    static bool savedPaused, tutorialEnabled;
    static float pauseTimer, pauseCost;
    static string frameLines;
    public static string FrameResult { get; private set; }
    public static void StartFrameProbe()
    {
        if(!Application.isPlaying)throw new InvalidOperationException("Play required");
        EditorApplication.update-=FrameStep;
        savedCapture=Time.captureDeltaTime;savedPaused=EditorApplication.isPaused;
        tutorialEnabled=Find<TutorialFlow>().enabled;Find<TutorialFlow>().enabled=false;
        Time.captureDeltaTime=.05f;EditorApplication.isPaused=true;
        mode=0;frameLines="";FrameResult="running";ConfigureFrames();
        EditorApplication.update+=FrameStep;
    }
    static void ConfigureFrames()
    {
        FreshBoss();if(mode==1)Find<CombatInfoUI>().ToggleSpeed();
        Boss().TakeDamage(1600);Boss().TryInterrupt(2.5f);
        elapsed=0;transitionEnded=0;ticks=0;pausedFrames=0;frame=-1;
    }
    static void FrameStep()
    {
        if(!Application.isPlaying){FinishFrames("stopped");return;}
        if(Time.frameCount==frame)return;frame=Time.frameCount;
        try
        {
            var e=Boss();var c=Find<CostSystem>();var info=Find<CombatInfoUI>();
            if(++ticks>400)throw new Exception("frame timeout");
            if(ticks>1)elapsed+=Time.deltaTime;
            if(pausedFrames>0 && pausedFrames<=12)
            {
                if(Time.timeScale!=0 || e.PhaseTransitionRemaining!=pauseTimer || c.Current!=pauseCost)
                    throw new Exception("manual pause advanced time/cost");
                if(++pausedFrames==13)info.Resume();
            }
            if(pausedFrames==0 && elapsed>=.4f)
            {
                info.TogglePause();pauseTimer=e.PhaseTransitionRemaining;pauseCost=c.Current;pausedFrames=1;
            }
            if(!e.IsPhaseTransitioning && transitionEnded==0)transitionEnded=elapsed;
            if(e.IsCasting)
            {
                if(Mathf.Abs(transitionEnded-2)>.055f || Mathf.Abs(elapsed-2.5f)>.055f
                    || Mathf.Abs(c.Current-(3+.8f*elapsed))>.08f || !Near(e.CurrentCastTime,1.2f))
                    throw new Exception("timing mismatch: transition="+transitionEnded+" next="+elapsed+" cost="+c.Current);
                frameLines+=(mode==0?"1x":"0.5x")+": transition="+transitionEnded.ToString("0.000")+"s next="+elapsed.ToString("0.000")+"s cost="+c.Current.ToString("0.00")+", pause 12 frames frozen; "+ticks+" frames\n";
                if(mode++==0)ConfigureFrames();else{FinishFrames("passed\n"+frameLines);return;}
            }
            EditorApplication.Step();
        }
        catch(Exception ex){FinishFrames("FAILED: "+ex);}
    }
    static void FinishFrames(string result)
    {
        EditorApplication.update-=FrameStep;Time.captureDeltaTime=savedCapture;
        FrameResult=result;
        if(Application.isPlaying){Find<TutorialFlow>().enabled=tutorialEnabled;Find<BattleFlow>().RestartRun();}
        EditorApplication.isPaused=savedPaused;Debug.Log("Boss frames: "+result);
    }

}
