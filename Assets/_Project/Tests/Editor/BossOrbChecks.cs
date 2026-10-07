using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>A43 기능 검사. 함수 호출로 경계를 확인하며 직접 플레이·밸런스와 구분한다.</summary>
public static class BossOrbChecks
{
    const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    static object Get(object o, string n) => o.GetType().GetField(n, Hidden).GetValue(o);
    static void Set(object o, string n, object v) => o.GetType().GetField(n, Hidden).SetValue(o, v);
    static object Call(object o, string n, params object[] args) => o.GetType().GetMethod(n, Hidden).Invoke(o, args);
    static T Find<T>() where T : UnityEngine.Object => UnityEngine.Object.FindFirstObjectByType<T>();
    static Enemy Boss() => UnityEngine.Object.FindObjectsByType<Enemy>(FindObjectsInactive.Include, FindObjectsSortMode.None).Single(e => e.HasPhases);
    static void Fresh() => Call(Find<BattleFlow>(), "BeginBattle", 3, true, null);
    static void Tick(float dt) => Call(Boss(), "AdvanceCombat", dt);
    static BossOrbs Enter(int phase)
    {
        Fresh(); var boss = Boss(); boss.TakeDamage(phase == 2 ? 1600 : 3000); boss.TryInterrupt(2.5f); Tick(2.5f);
        return boss.GetComponent<BossOrbs>();
    }
    static void Attack(int index)
    {
        // 경계 검사에서 특정 공격을 선택한다. 실제 프레임 검사는 자연 순서도 확인한다.
        Call(Boss(), "BeginNextCast"); Set(Boss(), "current", Boss().Data.Attacks[index - 1]);
    }
    static bool Near(float a, float b) => Mathf.Abs(a-b) < .001f;

    [MenuItem("Tools/Scroll Hunter/Check Boss Orbs (Play)")]
    public static void Menu() => Debug.Log(Run());
    public static string Run()
    {
        if (!Application.isPlaying) throw new InvalidOperationException("Play required");
        var f=Find<BattleFlow>();var e=Boss();var p=Find<Player>();var c=Find<CostSystem>();
        var m=Find<EnemyManager>();var d=Find<DeckSystem>();var t=Find<TutorialFlow>();var info=Find<CombatInfoUI>();
        bool tutorial=t.enabled;var random=UnityEngine.Random.state;
        int passed=0;Action<bool,string> check=(ok,label)=>{if(!ok)throw new Exception("BossOrbs: "+label);passed++;};
        try
        {
            t.enabled=false;f.RestartRun();Fresh();var o=e.GetComponent<BossOrbs>();
            check(!o.RuinActive && !o.CycleActive && m.GetAliveEnemies().Count==1,"phase1 no orbs");
            e.TakeDamage(1600);check(!e.IsPhaseTransitioning,"threshold waits current attack");e.TryInterrupt(2.5f);
            check(e.IsPhaseTransitioning && !o.RuinActive && !o.CycleActive,"transition preview no aura");
            check(o.Ruin.gameObject.activeSelf && o.Cycle.gameObject.activeSelf,"preview visible");
            check(!o.Ruin.GetComponent<Collider>().enabled && m.GetAliveEnemies().Count==1,"preview untargetable");
            int hp=e.CurrentHp;o.Ruin.TakeDamage(1000);check(o.Ruin.CurrentHp==0 && e.CurrentHp==hp,"preview no damage");
            Call(m,"SelectTarget",o.Ruin);check(m.CurrentTarget==e,"preview not selected");
            Tick(1.99f);check(!o.RuinActive,"not early");Tick(.01f);
            check(o.RuinActive && o.CycleActive && e.IsStaggered && !e.IsPhaseTransitioning,"activation at transition end before stagger ends");
            foreach(var orb in new[]{o.Ruin,o.Cycle})
            {
                check(orb.MaxHp==400 && orb.CurrentHp==400 && orb.Data.Defense==0,"orb stats");
                check(!orb.IsCasting && orb.CurrentAttack==null && orb.TryInterrupt(2.5f)==InterruptResult.NotCasting,"no attacks or interrupt");
                check(orb.GetComponent<Collider>().enabled && orb.HpBarRect!=null,"click and HP bar");
                check(info.DescribeEnemy(orb).Contains("150"),"orb tooltip");
            }
            check(o.Ruin.transform.position.x<e.transform.position.x && o.Cycle.transform.position.x>e.transform.position.x,"left/right");
            check(m.GetAliveEnemies().Count==3 && m.EnemyCount==1,"targets 3, encounter enemy 1");
            Tick(.5f);check(Near(e.CurrentCastTime,1.2f) && Near(e.CurrentDamageMultiplier,1.1f),"phase2 snapshot 60% and 110%");
            hp=e.CurrentHp;o.Ruin.TakeDamage(400);check(e.CurrentHp==hp-150 && m.OrbsDestroyed==1,"fixed 150 once");
            o.Ruin.TakeDamage(400);check(e.CurrentHp==hp-150,"dead orb cannot retrigger");
            o.Cycle.TakeDamage(400);check(Near(e.CurrentCastTime,1.2f) && Near(e.CurrentDamageMultiplier,1.1f),"destroy during cast preserves snapshot");
            p.BeginBattle(true);Call(e,"Fire");check(p.CurrentHp==468,"35 x1.1 /1.2 rounded once to32");Tick(1);
            check(Near(e.CurrentCastTime,1.6f) && Near(e.CurrentDamageMultiplier,1),"next cast resamples missing orbs");

            o=Enter(2);o.Ruin.TakeDamage(50);o.Cycle.TakeDamage(90);e.TakeDamage(1400);e.TryInterrupt(2.5f);
            check(e.PhaseNumber==3 && o.Ruin.CurrentHp==350 && o.Cycle.CurrentHp==310,"phase3 keeps existing HP");
            hp=e.CurrentHp;o.Ruin.TakeDamage(400);check(e.CurrentHp==hp && !o.RuinActive,"transition invulnerability blocks destruction damage");
            Tick(2.5f);check(e.CurrentHp==hp && !o.RuinActive && o.Cycle.CurrentHp==310,"no deferred damage or refill");
            check(Near(e.CurrentCastTime,16),"phase3 only cycle reduction");
            o=Enter(3);check(o.Ruin.CurrentHp==400 && o.Cycle.CurrentHp==400,"phase2 skip summons both");

            for(int index=1;index<=5;index++)
            {
                o=Enter(3);Attack(index);p.BeginBattle(true);p.AddShield(10000);
                int expected=DamageFormula.Compute(e.CurrentAttack.Damage,1,p.Defense,1,1.1f*(e.CastColor==CastColor.Red?.5f:1));
                Call(e,"Fire");check(p.Shield==10000-expected,"all-pattern multiplier and red rounding "+index);
                check(p.IsCostRecoveryBlocked==(index==2||index==4),"only stun attacks block recovery "+index);
            }
            o=Enter(3);Attack(2);p.BeginBattle(true);Call(e,"Fire");float cost=c.Current;
            check(p.IsCostRecoveryBlocked && Near(p.StunRemaining,2),"empowered silence lock");
            Call(c,"AdvanceCost",1f);check(c.Current==cost,"blocked no regeneration");
            o.Ruin.TakeDamage(400);Call(c,"AdvanceCost",1f);check(c.Current==cost && p.IsCostRecoveryBlocked,"orb death does not clear lock");
            Set(p,"costRecoveryBlockedUntil",Time.time-.5f);Call(c,"AdvanceCost",1f);check(Near(c.Current,cost+.4f),"partial ending frame only charges remaining time");
            p.EndBattle();p.ApplyStun(2);cost=c.Current;Call(c,"AdvanceCost",.1f);check(Near(c.Current,cost+.08f),"ordinary stun still charges");
            p.EndBattle();p.SetSanctuary(true);p.ApplyCostBlockingStun(5);check(!p.IsStunned && !p.IsCostRecoveryBlocked,"immune stun never blocks recovery");
            p.SetSanctuary(false);p.ApplyCostBlockingStun(5);p.BeginBattle(true);check(!p.IsStunned && !p.IsCostRecoveryBlocked,"restart clears stun/lock");

            o=Enter(3);o.Ruin.TakeDamage(400);o.Cycle.TakeDamage(400);Attack(3);p.AddShield(10000);
            check(e.CurrentDamageMultiplier==1 && Near(e.CurrentCastTime,6),"dark hole begins unbuffed");
            check(e.TryInterrupt(2.5f)==InterruptResult.FailedRed && !o.RuinActive,"red interrupt failure does not summon");
            Call(e,"Fire");check(o.RuinActive && !o.CycleActive && o.Ruin.CurrentHp==400,"dark hole refills ruin first");
            check(e.CurrentDamageMultiplier==1,"new orb not retroactive");
            Attack(3);Call(e,"Fire");check(o.CycleActive,"next dark hole fills cycle");
            o.Ruin.TakeDamage(10);o.Cycle.TakeDamage(20);Attack(3);Call(e,"Fire");
            check(o.Ruin.CurrentHp==390 && o.Cycle.CurrentHp==380,"both alive no heal/replacement");
            o.Ruin.TakeDamage(400);Attack(3);Call(e,"Fire");check(o.Ruin.CurrentHp==400 && o.Cycle.CurrentHp==380,"only ruin missing refill ruin");
            o=Enter(2);o.Ruin.TakeDamage(400);o.Cycle.TakeDamage(400);Attack(3);p.AddShield(10000);Call(e,"Fire");
            check(!o.RuinActive && !o.CycleActive,"phase2 dark hole never restores");

            o=Enter(3);p.AddCriticalChanceBonus(-100);
            try
            {
                var cards=d.CopyStartingDeck();var area=cards.First(x=>x.IsAreaOfEffect && !x.IsChanneling);
                cards.Remove(area);cards.Insert(0,area);d.SetDeck(cards);c.EnsureTutorialCost(10);
                int before=o.Ruin.CurrentHp;hp=e.CurrentHp;check(d.TryUseSlot(0),"AoE accepted");Call(d,"AdvanceCast",area.CastTime+1);
                check(o.Ruin.CurrentHp<before && o.Cycle.CurrentHp<before && e.CurrentHp<hp,"AoE hits boss and both orbs");
                o=Enter(3);cards=d.CopyStartingDeck();var single=cards.First(x=>!x.IsAreaOfEffect && x.Damage>0 && !x.IsChanneling);
                cards.Remove(single);cards.Insert(0,single);d.SetDeck(cards);c.EnsureTutorialCost(10);Call(m,"SelectTarget",o.Ruin);hp=e.CurrentHp;
                check(d.TryUseSlot(0),"single accepted on orb");Call(d,"AdvanceCast",single.CastTime+1);
                check(o.Ruin.CurrentHp<400 && o.Cycle.CurrentHp==400 && e.CurrentHp==hp,"single targets selected orb");
                d.EndBattle();cards=d.CopyStartingDeck();var silence=cards.First(x=>x.Category==SkillCategory.Interrupt);cards.Remove(silence);cards.Insert(0,silence);d.SetDeck(cards);c.EnsureTutorialCost(10);cost=c.Current;
                check(!d.TryUseSlot(0) && c.Current==cost && d.GetHandCard(0)==silence,"interrupt input on orb spends/cycles nothing");
            }
            finally{p.AddCriticalChanceBonus(100);}
            o=Enter(3);Call(m,"SelectTarget",o.Ruin);o.Ruin.TakeDamage(400);m.NotifyEnemyDied();check(m.CurrentTarget!=o.Ruin && m.CurrentTarget.IsAlive,"orb death auto target");
            hp=e.CurrentHp;e.TakeDamage(hp);m.NotifyEnemyDied();check(m.CombatEnded && !o.CycleActive && m.OrbsDestroyed==1,"boss death cleans live orb without destruction");
            o=Enter(3);Set(e,"<CurrentHp>k__BackingField",150);o.Ruin.TakeDamage(400);m.NotifyEnemyDied();check(!e.IsAlive && !o.CycleActive && m.CombatEnded,"indirect 150 boss kill wins");
            o=Enter(3);o.Ruin.TakeDamage(400);o.Cycle.TakeDamage(400);Attack(3);Set(p,"currentHp",1f);Call(e,"Fire");Call(f,"Update");
            check(!o.RuinActive && !o.CycleActive && f.State==BattleFlowState.Defeat,"lethal dark hole no respawn");
            Fresh();check(!o.RuinActive && !o.CycleActive && m.GetAliveEnemies().Count==1 && m.OrbsDestroyed==0 && !p.IsCostRecoveryBlocked,"fresh run reset");
            o=Enter(2);p.TakeDamage(100000);Call(f,"Update");check(!o.RuinActive && !o.CycleActive && m.OrbsDestroyed==0,"defeat cleanup no destruction damage");
            p.AddCriticalChanceBonus(-100);
            try
            {
                // 시전 도중 새 오브: 일반 광역은 완료 시, 광역 채널은 다음 타격부터 편입.
                o=Enter(3);o.Ruin.TakeDamage(400);o.Cycle.TakeDamage(400);Attack(3);p.AddShield(10000);
                var cards=d.CopyStartingDeck();var area=cards.First(x=>x.IsAreaOfEffect && !x.IsChanneling && x.CastTime>0);
                cards.Remove(area);cards.Insert(0,area);d.SetDeck(cards);c.EnsureTutorialCost(10);
                check(d.TryUseSlot(0),"AoE begins without orb");Call(d,"AdvanceCast",area.CastTime*.5f);
                Call(e,"Fire");check(o.Ruin.CurrentHp==400,"orb spawns during ordinary AoE cast");
                Call(d,"AdvanceCast",area.CastTime*.5f+.001f);
                check(o.Ruin.CurrentHp==400-area.Damage*area.HitCount,"new orb included at AoE completion");

                o=Enter(3);o.Ruin.TakeDamage(400);o.Cycle.TakeDamage(400);Attack(3);p.AddShield(10000);
                cards=d.CopyStartingDeck();var channel=cards.First(x=>x.IsAreaOfEffect && x.IsChanneling);
                var timing=(DamageChannelEffect)channel.ChannelEffect;cards.Remove(channel);cards.Insert(0,channel);d.SetDeck(cards);c.EnsureTutorialCost(10);
                check(d.TryUseSlot(0),"AoE channel begins without orb");Call(d,"AdvanceCast",timing.FirstHitDelay);
                hp=e.CurrentHp;Call(e,"Fire");check(o.Ruin.CurrentHp==400,"old channel tick not applied to newly spawned orb");
                Call(d,"AdvanceCast",timing.HitInterval);
                check(o.Ruin.CurrentHp==400-channel.Damage && e.CurrentHp<hp,"next channel tick includes new orb once");

                Fresh();o=e.GetComponent<BossOrbs>();cards=d.CopyStartingDeck();cards.Remove(channel);cards.Insert(0,channel);d.SetDeck(cards);c.EnsureTutorialCost(10);
                check(d.TryUseSlot(0),"channel starts before phase transition");e.TakeDamage(1600);e.TryInterrupt(2.5f);hp=e.CurrentHp;
                Call(d,"AdvanceCast",timing.FirstHitDelay);
                check(e.CurrentHp==hp && !o.RuinActive && !o.CycleActive,"preview and invulnerable boss excluded from channel damage");
                Tick(2);check(o.Ruin.CurrentHp==400 && d.IsChanneling,"orbs activate while existing channel continues");
                Call(d,"AdvanceCast",timing.HitInterval);
                check(o.Ruin.CurrentHp==400-channel.Damage && o.Cycle.CurrentHp==400-channel.Damage,"next channel tick hits both activated orbs");

                Fresh();cards=d.CopyStartingDeck();cards.Remove(area);cards.Insert(0,area);d.SetDeck(cards);c.EnsureTutorialCost(10);
                check(d.TryUseSlot(0),"ordinary AoE starts before transition");e.TakeDamage(1600);e.TryInterrupt(2.5f);hp=e.CurrentHp;
                Call(d,"AdvanceCast",area.CastTime+.001f);
                check(e.CurrentHp==hp && !o.RuinActive && !o.CycleActive,"AoE completed during preview hits neither orb nor invulnerable boss");
                Tick(2);check(o.Ruin.CurrentHp==400 && o.Cycle.CurrentHp==400,"completed AoE not replayed on activation");
            }
            finally{p.AddCriticalChanceBonus(100);}
            return "Boss orb checks passed: "+passed;
        }
        finally{UnityEngine.Random.state=random;t.enabled=tutorial;f.RestartRun();}
    }

    static int mode, stage, frame, ticks, pauseFrames;
    static float started, lockedUntil, lockedCost, pausedCost, pausedTransition, savedCapture, savedSpeed;
    static bool savedPaused, savedTutorial;
    static string frameLines;
    public static string FrameResult { get; private set; }
    [MenuItem("Tools/Scroll Hunter/Check Boss Orbs Frames (Play)")]
    public static void StartFrames()
    {
        if(!Application.isPlaying)throw new InvalidOperationException("Play required");
        EditorApplication.update-=FrameStep;
        savedSpeed=Find<CombatInfoUI>().BattleSpeed;
        savedCapture=Time.captureDeltaTime;savedPaused=EditorApplication.isPaused;savedTutorial=Find<TutorialFlow>().enabled;
        Find<TutorialFlow>().enabled=false;Time.captureDeltaTime=.02f;EditorApplication.isPaused=true;
        mode=0;frameLines="";FrameResult="running";ConfigureFrames();EditorApplication.update+=FrameStep;
    }
    static void ConfigureFrames()
    {
        Fresh();var info=Find<CombatInfoUI>();if(!Near(info.BattleSpeed,mode==0?1f:.5f))info.ToggleSpeed();
        Boss().TakeDamage(1600);Boss().TryInterrupt(2.5f);
        stage=0;frame=-1;ticks=0;pauseFrames=0;started=Time.time;
    }
    static void FrameStep()
    {
        if(!Application.isPlaying){FinishFrames("stopped");return;}
        if(frame==Time.frameCount)return;frame=Time.frameCount;
        try
        {
            if(++ticks>2500)throw new Exception("timeout stage "+stage);
            var e=Boss();var o=e.GetComponent<BossOrbs>();var c=Find<CostSystem>();var p=Find<Player>();var info=Find<CombatInfoUI>();
            if(pauseFrames>0)
            {
                if(c.Current!=pausedCost || e.PhaseTransitionRemaining!=pausedTransition)throw new Exception("pause advanced cost/transition");
                if(++pauseFrames>12){info.Resume();pauseFrames=0;}
            }
            else if(stage==0)
            {
                if(Time.time-started>.4f){info.TogglePause();pausedCost=c.Current;pausedTransition=e.PhaseTransitionRemaining;pauseFrames=1;stage=1;}
            }
            else if(stage==1)
            {
                if(e.IsPhaseTransitioning && (o.RuinActive || o.CycleActive))throw new Exception("early orb activation");
                if(!e.IsPhaseTransitioning && (!o.RuinActive || !o.CycleActive))throw new Exception("activation missing at transition end");
                if(e.IsCasting)
                {
                    if(!Near(e.CurrentCastTime,1.2f) || Mathf.Abs(Time.time-started-2.5f)>.06f)throw new Exception("transition/stagger/cast timing");
                    Set(e,"phasePatternIndex",6);Call(e,"BeginNextCast"); // 실제 2페이즈 사일런스 시전부터 관측
                    c.TrySpend(c.Current);stage=2;
                }
            }
            else if(stage==2 && p.IsCostRecoveryBlocked)
            {
                lockedUntil=(float)Get(p,"costRecoveryBlockedUntil");lockedCost=c.Current;
                o.Ruin.TakeDamage(400);info.TogglePause();pausedCost=c.Current;pausedTransition=e.PhaseTransitionRemaining;pauseFrames=1;stage=3;
            }
            else if(stage==3)
            {
                if(Time.time<lockedUntil && !Near(c.Current,lockedCost))throw new Exception("regeneration during empowered stun after orb destruction");
                if(Time.time>=lockedUntil+.3f)
                {
                    if(p.IsStunned || p.IsCostRecoveryBlocked || c.Current<lockedCost+.2f)throw new Exception("recovery did not resume");
                    frameLines+=(mode==0?"1x":"0.5x")+": "+ticks+" frames; transition/stagger, active orbs, silence, paused lock, orb destruction, recovery PASS\n";
                    if(mode++==0)ConfigureFrames();else{FinishFrames("passed\n"+frameLines);return;}
                }
            }
            EditorApplication.Step();
        }
        catch(Exception error){FinishFrames("FAILED "+error);}
    }
    static void FinishFrames(string result)
    {
        EditorApplication.update-=FrameStep;Time.captureDeltaTime=savedCapture;FrameResult=result;
        if(Application.isPlaying){Set(Find<CombatInfoUI>(),"resumeScale",savedSpeed);Find<TutorialFlow>().enabled=savedTutorial;Find<BattleFlow>().RestartRun();}
        EditorApplication.isPaused=savedPaused;Debug.Log("Boss orb frames: "+result);
    }
}
