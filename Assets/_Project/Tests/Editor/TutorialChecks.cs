using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class TutorialChecks
{
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    private static object Get(object obj, string name) => obj.GetType().GetField(name, Hidden).GetValue(obj);
    private static void Set(object obj, string name, object value) => obj.GetType().GetField(name, Hidden).SetValue(obj, value);
    private static void Call(object obj, string name, params object[] args) => obj.GetType().GetMethod(name, Hidden).Invoke(obj, args);
    private static void NextFrame(TutorialFlow t) => Set(t, "consumedFrame", -1);
    private static void Advance(TutorialFlow t) { NextFrame(t); if (!t.AdvanceGuide()) throw new Exception("Guide advance rejected"); }
    private static void CastTime(DeckSystem d, float seconds) => Call(d, "AdvanceCast", seconds);

    [MenuItem("Tools/Scroll Hunter/Check Tutorial (Play)")]
    public static void Menu() => Debug.Log(Run());

    public static string Run()
    {
        if (!Application.isPlaying) throw new InvalidOperationException("Battle Play에서 실행하세요.");
        var f = UnityEngine.Object.FindFirstObjectByType<BattleFlow>();
        var t = UnityEngine.Object.FindFirstObjectByType<TutorialFlow>();
        var d = UnityEngine.Object.FindFirstObjectByType<DeckSystem>();
        var p = UnityEngine.Object.FindFirstObjectByType<Player>();
        var c = UnityEngine.Object.FindFirstObjectByType<CostSystem>();
        var m = UnityEngine.Object.FindFirstObjectByType<EnemyManager>();
        var metrics = UnityEngine.Object.FindFirstObjectByType<CombatMetrics>();
        var info = UnityEngine.Object.FindFirstObjectByType<CombatInfoUI>();
        var ui = UnityEngine.Object.FindFirstObjectByType<CombatUI>();
        var reward = UnityEngine.Object.FindFirstObjectByType<BattleRewardUI>();
        var e = t.Enemy;
        int passed = 0;
        Action<bool, string> check = (ok, text) => { if (!ok) throw new Exception("Tutorial: " + text); passed++; };
        Action fresh = () => { f.BeginTutorial(false); NextFrame(t); };
        Action qPrompt = () => { fresh(); Advance(t); Advance(t); Advance(t); NextFrame(t); };
        Action<int, CastColor> halfway = (lesson, color) =>
        {
            e.BeginTutorial(e.CurrentHp, color == CastColor.Orange ? 1 : 2);
            Set(e, "castTimer", e.CurrentAttack.CastTime / 2f);
            Call(t, "OnCastAdvanced");
            check(t.Lesson == lesson && t.ShowingGuide && !t.WaitingForSkill && Time.timeScale == 0, "halfway pauses lesson " + lesson);
            Advance(t); NextFrame(t);
        };
        try
        {
            fresh();
            check(t.Active && t.Lesson == 1 && t.CompletedLessons == 0 && f.BattleNumber == 0, "direct tutorial, separate battle zero");
            var body = (TMPro.TMP_Text)Get(t,"guideText");
            var hint = (TMPro.TMP_Text)Get(t,"continueHint");
            check(!body.text.Contains("학습") && !body.text.Contains("/ 7") && !body.text.Contains("Enter") && hint.text=="Space", "no lesson count; only Space hint");
            check(hint.alignment==TMPro.TextAlignmentOptions.BottomRight && hint.rectTransform.anchorMax.y==0, "hint anchored at guide bottom right");
            check(!f.CanEditOrder && !f.BeginOrderEdit() && !f.SkipReward() && f.RewardChoiceCount == 0, "no initial editor or reward");
            check(p.CurrentHp == 500 && p.PotionsRemaining == 2 && c.Current == 3 && e.CurrentHp == 300 && e.Data.Defense == 0, "saved starting values");
            check(m.EnemyCount == 1 && m.CurrentTarget == e && e.transform.position.x == m.FormationCenter.x, "single centered target");
            check(e.Data.Find(CastColor.Red).Damage == 350 && e.Data.Find(CastColor.Orange).StunSeconds == 2, "tutorial attacks, common stun");
            check(Enumerable.Range(0,4).All(i => !d.TryUseSlot(i)) && c.Current == 3, "explanation rejects all slots without spend");
            info.TogglePause(); info.Resume(); info.ToggleSpeed();
            check(Time.timeScale == 0 && !info.IsInfoPaused, "manual controls cannot release guide pause");
            Advance(t); check(t.Lesson == 2 && !t.AdvanceGuide(), "one advance per frame");
            Advance(t); check(t.Lesson == 3 && !t.WaitingForSkill, "target marker explanation precedes input");
            Advance(t);
            check(!hint.gameObject.activeSelf, "Space hint hidden when designated skill required");
            check(!d.TryUseSlot(0), "advance click cannot also use Q");
            NextFrame(t);
            check(d.GetSlotState(0) == SlotState.Ready && Enumerable.Range(1,3).All(i=>d.GetSlotState(i)==SlotState.TutorialLocked), "only Q enabled while frozen");
            Call(ui, "UpdateHand");
            var slots = (CombatUI.HandSlotView[])Get(ui, "handSlots");
            check(slots.Skip(1).All(x=>x.costCover.enabled && x.costCover.fillAmount==1), "entire other slots shaded");
            e.TakeDamage(10000); m.NotifyEnemyDied();
            check(e.CurrentHp == 0 && e.IsAlive && m.CurrentTarget == e && !metrics.Ended && e.IsCasting, "HP zero preserves attacks and target before lesson six");
            var q = d.GetHandCard(0); var next = d.PeekNext(); float before = c.Current;
            check(d.TryUseSlot(0) && t.CompletedLessons == 3 && Time.timeScale > 0, "Q accepted resumes time");
            check(d.GetHandCard(0)==next && c.Current==before-q.Cost && !d.TryUseSlot(0), "one cycle, cost, replacement locked");
            CastTime(d, 0.4f);
            check(Enumerable.Range(0,4).All(i=>d.GetSlotState(i)==SlotState.TutorialLocked), "all cards locked between lessons");
            halfway(4, CastColor.Orange);
            c.TrySpend(c.Current); // Retrying the lesson restores required cost before input.
            Set(t, "<WaitingForSkill>k__BackingField", false); Advance(t); NextFrame(t);
            check(c.Current == d.GetHandCard(2).Cost && !d.TryUseSlot(1), "only needed cost supplied, wrong slot rejected");
            check(d.TryUseSlot(2) && t.CompletedLessons==4 && !e.IsStaggered && d.IsCasting, "E acceptance precedes effect");
            CastTime(d, 0.19f); check(!e.IsStaggered, "no early silence effect");
            CastTime(d, 0.02f); check(e.IsStaggered && (float)Get(e,"staggerTimer")==2.5f, "normal silence effect at completion");
            Call(e, "BeginNextCast"); check(e.CastColor==CastColor.Red, "interrupted orange advances directly to red");
            CastTime(d,0.4f);
            halfway(5,CastColor.Red);
            check(d.TryUseSlot(3) && t.CompletedLessons==5 && p.Shield==0 && d.IsCasting, "R accepted, effect delayed");
            check(d.GetSlotState(0)==SlotState.ActionLocked, "learning lock removed but GCD remains");
            CastTime(d,0.21f); check(p.Shield==120, "magic armor applies normally");
            CastTime(d,0.4f);
            float hp=p.CurrentHp; Call(e,"Fire");
            check(p.CurrentHp==hp-26 && p.Shield==0 && t.Lesson==6 && Time.timeScale==0, "red350 half then shield, potion guide after impact");
            check(!p.TryUsePotion() && p.PotionsRemaining==2 && t.CompletedLessons==5, "Shift cannot use or finish frozen potion lesson");
            Advance(t);
            check(!t.Active && f.State==BattleFlowState.TutorialComplete && t.CompletedLessons==6 && t.ShowingGuide && !f.CanEditOrder, "HP zero plus six lessons waits at completion guide");
            var rewardPanel = (GameObject)Get(reward,"panel");
            check(!rewardPanel.activeSelf && body.text.Contains("튜토리얼 완료") && body.text.Contains("HP와 포션"), "completion explains next step without showing deck panel");
            check(f.RewardChoiceCount==0 && metrics.Ended, "tutorial completion gives no reward");
            check(!t.AdvanceGuide() && !p.TryUsePotion() && !d.TryUseSlot(0), "death-frame input cannot dismiss completion or spend resources");
            NextFrame(t); f.NextBattle(); Call(f,"AdvanceCountdown",3f); t.Skip(); Call(f,"Update"); info.TogglePause(); info.Resume();
            check(f.State==BattleFlowState.TutorialComplete && !rewardPanel.activeSelf && Time.timeScale==0, "no automatic transition, next battle, skip or pause bypass");
            check(!f.BeginOrderEdit() && !p.TryUsePotion() && !d.TryUseSlot(0), "completion blocks order edit, potion and card after next frame");
            Advance(t);
            check(f.State==BattleFlowState.Preparing && t.Lesson==7 && t.CompletedLessons==6 && t.ShowingGuide && rewardPanel.activeSelf && !f.CanEditOrder, "confirmation opens existing lesson seven over deck panel");
            check(!t.AdvanceGuide() && !f.BeginOrderEdit(), "confirmation cannot also dismiss lesson seven or edit");
            f.NextBattle(); Call(f,"AdvanceCountdown",3f); check(f.State==BattleFlowState.Preparing && m.CombatEnded, "lesson seven prevents battle start");
            Advance(t); NextFrame(t);
            check(f.CanEditOrder && !t.ShowingGuide && Time.timeScale==0, "lesson seven leaves normal preparation");
            var original = Enumerable.Range(0,8).Select(f.GetDeckCard).ToArray();
            check(f.BeginOrderEdit() && f.MoveOrderCard(0,5), "tutorial editor insertion available");
            f.CancelOrderEdit(); check(Enumerable.Range(0,8).Select(f.GetDeckCard).SequenceEqual(original), "cancel restores original");
            f.BeginOrderEdit(); f.MoveOrderCard(0,5); f.SaveOrder(); f.NextBattle(); Call(f,"AdvanceCountdown",3f);
            check(m.EnemyCount==2 && m.Enemies[0].Data.name=="Enemy_A_Green" && m.Enemies[1].Data.name=="Enemy_B_Orange", "next battle A+B");
            check(p.CurrentHp==500 && p.PotionsRemaining==2 && p.Shield==0 && c.Current==3 && d.GetHandCard(0)==original[1], "fresh HP/potion, saved order used");
            p.TakeDamage(30); p.TryUsePotion(); p.TakeDamage(30); float carry=p.CurrentHp;
            foreach(var enemy in m.GetAliveEnemies())enemy.TakeDamage(10000);
            m.NotifyEnemyDied(); Call(f,"Update");
            check(f.State==BattleFlowState.BetweenBattles && f.RewardChoiceCount>0, "normal victory offers rewards");
            f.SkipReward(); f.NextBattle(); Call(f,"AdvanceCountdown",3f);
            check(m.EnemyCount==1 && m.CurrentTarget.Data.name=="Enemy_C_Red" && p.CurrentHp==carry && p.PotionsRemaining==1, "normal C carries HP/potion");
            foreach(var enemy in m.GetAliveEnemies())enemy.TakeDamage(10000);m.NotifyEnemyDied();Call(f,"Update"); f.SkipReward();f.NextBattle(); Call(f,"AdvanceCountdown",3f);
            check(m.EnemyCount==3 && m.Enemies[1].Data.name=="Enemy_C_Red", "ABC center C");

            // 재도전: 완료 학습과 적 HP를 보존하고 미완료 단계에 필요한 상태를 복구한다.
            qPrompt(); check(d.TryUseSlot(0), "retry setup Q"); CastTime(d,.4f); e.TakeDamage(20);int kept=e.CurrentHp;
            p.AddShield(20);p.TakeDamage(10000);t.TickCombat();NextFrame(t);
            check(t.CompletedLessons==3 && t.Lesson==4 && t.ShowingGuide && e.CurrentHp==kept && e.CastColor==CastColor.Orange, "retry preserves progress and enemy HP, chooses orange");
            check(p.CurrentHp==500 && p.Shield==0 && !p.IsStunned && !d.IsCasting && !d.IsLocked && c.Current==3 && p.PotionsRemaining==2, "retry clears player states and restores resources");
            check(Enumerable.Range(0,4).All(i=>d.GetHandCard(i)==original[i]), "retry restores base hand");
            Advance(t);NextFrame(t);check(d.TryUseSlot(2), "retry prompt accepts E");
            p.TakeDamage(10000);t.TickCombat();NextFrame(t);
            check(t.CompletedLessons==4 && t.Lesson==5 && e.CastColor==CastColor.Red && !d.IsCasting, "retry during silence cast cleans cast and advances by accepted input");

            // 스킵은 승리 보고 없이 안내·시전·유지 효과를 정리한다.
            var logs=new System.Collections.Generic.List<string>();
            Application.LogCallback capture=(s,trace,type)=>{if(type==LogType.Log)logs.Add(s);};
            Application.logMessageReceived+=capture;
            try
            {
                Advance(t);NextFrame(t);d.TryUseSlot(3);p.SetSanctuary(true);p.ApplyStun(2);
                var skip=((RectTransform)Get(t,"skipRect")).GetComponent<Button>();
                ExecuteEvents.Execute(skip.gameObject,new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left},ExecuteEvents.pointerClickHandler);
                check(!t.Active && !t.ShowingGuide && f.State==BattleFlowState.Preparing && !f.CanEditOrder, "skip button opens optional order editor without lesson seven");
                check(!d.IsCasting && !d.IsLocked && !p.IsInvulnerable && !p.IsStunned && m.CombatEnded && metrics.Ended, "skip clears combat and maintained states");
                check(!logs.Any(s=>s.Contains("승패: 승리")) && f.RewardChoiceCount==0, "skip not victory or reward");
                NextFrame(t); f.NextBattle(); Call(f,"AdvanceCountdown",3f);check(p.CurrentHp==500 && p.PotionsRemaining==2 && m.EnemyCount==2, "skip starts fresh AB");
            }
            finally{Application.logMessageReceived-=capture;}

            fresh();
            var colors=new[]{CastColor.Green,CastColor.Orange,CastColor.Red,CastColor.Green,CastColor.Orange,CastColor.Red,CastColor.Green};
            for(int i=0;i<colors.Length;i++)
            {
                check(e.CastColor==colors[i],"fixed pattern " + i);
                if(i<colors.Length-1){Call(e,"Fire");Call(e,"BeginNextCast");}
            }
            fresh();check(e.TryInterrupt(2.5f)==InterruptResult.Success,"green interrupt allowed");Call(e,"BeginNextCast");check(e.CastColor==CastColor.Orange,"green interruption advances sequence");

            // 포션 안내는 생존 적에서도 사용 없이 완료하고, 닫은 프레임 Shift도 거절한다.
            fresh(); Set(t,"<CompletedLessons>k__BackingField",5); Call(t,"ShowLesson",6);
            p.TakeDamage(60); Advance(t);
            check(t.Active && t.CompletedLessons==6 && t.ShowingGuide && Time.timeScale==0 && body.text=="이제 공격스킬을 써서 적을 처치하세요", "potion guide leads to paused combat goal");
            check(hint.gameObject.activeSelf && !p.TryUsePotion() && !d.TryUseSlot(0) && !t.AdvanceGuide(), "combat goal blocks Shift, cards and same-frame advance");
            Advance(t);
            check(!t.ShowingGuide && Time.timeScale>0 && !p.TryUsePotion(), "goal closes to free combat, same-frame Shift blocked");
            NextFrame(t);check(p.TryUsePotion() && p.PotionsRemaining==1,"potion allowed once time runs");
            p.TakeDamage(10000);t.TickCombat();NextFrame(t);
            check(t.CompletedLessons==6 && !t.ShowingGuide && e.CastColor==CastColor.Green && p.PotionsRemaining==2,"completed tutorial retries as free combat from green");
            info.InspectSlot(0);check(info.IsInfoPaused && !d.TryUseSlot(0) && !p.TryUsePotion(),"normal manual pause remains restrictive");info.Resume();
            var sanctuary=AssetDatabase.LoadAssetAtPath<SkillData>(AssetDatabase.GUIDToAssetPath("a7b69bbb68255024fbcc381457f3346b"));
            var channelDeck=d.CopyStartingDeck();channelDeck[0]=sanctuary;d.SetDeck(channelDeck);c.EnsureTutorialCost(sanctuary.Cost);
            check(d.TryUseSlot(0) && d.IsChanneling && p.IsInvulnerable,"real sanctuary channel before skip");t.Skip();
            check(!p.IsInvulnerable && !d.IsChanneling,"skip ends actual maintained channel effect");
            fresh(); Set(t,"<CompletedLessons>k__BackingField",5); Call(t,"ShowLesson",6); Advance(t); t.Skip(); NextFrame(t); f.NextBattle(); Call(f,"AdvanceCountdown",3f);
            check(!t.ShowingGuide && !t.Active && m.EnemyCount==2, "skip from new combat goal reaches AB");
            fresh();e.TakeDamage(10000);p.TakeDamage(10000);t.TickCombat();NextFrame(t);
            check(e.CurrentHp==0 && e.IsAlive && m.CurrentTarget==e && t.CompletedLessons==0,"retry preserves zero-HP pending defeat");
            for(int completed=0;completed<=6;completed++)
            {
                fresh(); Set(t,"<CompletedLessons>k__BackingField",completed);
                e.TakeDamage(25);int remaining=e.CurrentHp;
                p.TakeDamage(10000);t.TickCombat();NextFrame(t);
                check(t.CompletedLessons==completed && e.CurrentHp==remaining && p.CurrentHp==500,"repeat retry preserves lesson "+completed);
                check(completed<6 ? t.ShowingGuide && t.Lesson==completed+1 : !t.ShowingGuide,"repeat retry resumes correct guide "+completed);
                t.Skip();check(!f.CanEditOrder,"skip click isolated at stage "+completed);
                NextFrame(t);f.NextBattle(); Call(f,"AdvanceCountdown",3f);check(m.EnemyCount==2 && !t.Active && p.PotionsRemaining==2,"skip from stage "+completed+" reaches AB");
            }
            // 자유 전투 처치도 완료 안내에서 기다리고, 안내 중 재시작은 처음 학습으로 돌아간다.
            fresh(); Set(t,"<CompletedLessons>k__BackingField",6); Call(t,"HideGuide");
            e.TakeDamage(10000); t.TickCombat(); NextFrame(t);
            check(f.State==BattleFlowState.TutorialComplete && !rewardPanel.activeSelf && t.ShowingGuide, "free combat kill also waits before preparation");
            f.RestartRun(); NextFrame(t);
            check(t.Active && t.CompletedLessons==0 && t.Lesson==1 && e.CurrentHp==300 && !rewardPanel.activeSelf, "restart at completion clears guide and restores tutorial");
            fresh();
            // 플레이어가 죽은직후 스킵해도 패배 요약과 새 전투가 섞이지 않는다.
            p.TakeDamage(10000);t.Skip();NextFrame(t);f.NextBattle(); Call(f,"AdvanceCountdown",3f);check(p.IsAlive && !metrics.Ended && m.EnemyCount==2,"skip after death safe");
        }
        finally { f.BeginTutorial(false); }
        return "Tutorial checks passed: " + passed + ". Function/pointer events; physical input and natural play not covered.";
    }

    [MenuItem("Tools/Scroll Hunter/Check Battle Ready (Play)")]
    public static void ReadyMenu() => Debug.Log(RunBattleReady());

    public static string RunBattleReady()
    {
        if (!Application.isPlaying) throw new InvalidOperationException("Play required");
        var f=UnityEngine.Object.FindFirstObjectByType<BattleFlow>();
        var t=UnityEngine.Object.FindFirstObjectByType<TutorialFlow>();
        var p=UnityEngine.Object.FindFirstObjectByType<Player>();
        var d=UnityEngine.Object.FindFirstObjectByType<DeckSystem>();
        var c=UnityEngine.Object.FindFirstObjectByType<CostSystem>();
        var m=UnityEngine.Object.FindFirstObjectByType<EnemyManager>();
        var metrics=UnityEngine.Object.FindFirstObjectByType<CombatMetrics>();
        var info=UnityEngine.Object.FindFirstObjectByType<CombatInfoUI>();
        var reward=UnityEngine.Object.FindFirstObjectByType<BattleRewardUI>();
        var panel=(GameObject)Get(f,"countdownPanel");var label=(TMPro.TMP_Text)Get(f,"countdownText");
        float speed=info.BattleSpeed,hold=(float)Get(f,"finishingEffectHold");
        int passed=0;Action<bool,string> check=(ok,msg)=>{if(!ok)throw new Exception("Ready: "+msg);passed++;};
        try
        {
            check((float)Get(f,"countdownSeconds")==3f,"saved duration three seconds");
            Set(f,"finishingEffectHold",0f);
            foreach(float battleSpeed in new[]{1f,.5f})
            {
                f.RestartRun();f.BeginTutorial(false);NextFrame(t);
                var skip=(RectTransform)Get(t,"skipRect");
                check(skip.anchorMin==Vector2.one && skip.anchorMax==Vector2.one && skip.pivot==Vector2.one && skip.anchoredPosition.x<0 && skip.anchoredPosition.y<0,"skip at top right");
                check(t.Active && t.ShowingGuide && !f.IsCountingDown && !panel.activeSelf,"tutorial begins with guide, not countdown");
                t.Skip();NextFrame(t);Set(info,"resumeScale",battleSpeed);f.NextBattle();
                check(f.IsCountingDown && panel.activeSelf && Time.timeScale==0 && label.text.Contains("3"),"three displayed, time frozen");
                check(!((GameObject)Get(reward,"panel")).activeSelf && !f.CanEditOrder && !f.BeginOrderEdit(),"preparation panel gone, editing blocked");
                check(panel.GetComponent<Image>().raycastTarget,"overlay blocks UI pointer input");
                var target=m.CurrentTarget;Call(m,"StepTarget",1);check(m.CurrentTarget==target,"keyboard target input blocked");
                info.ToggleSpeed();info.TogglePause();info.Resume();info.InspectSlot(0);
                check(info.BattleSpeed==battleSpeed && !info.IsInfoPaused && Time.timeScale==0,"pause/speed cannot skip or alter preparation");
                p.TakeDamage(60);float hp=p.CurrentHp;int potions=p.PotionsRemaining;
                var hand=Enumerable.Range(0,4).Select(d.GetHandCard).ToArray();
                check(!p.TryUsePotion() && Enumerable.Range(0,4).All(i=>!d.TryUseSlot(i)) && p.PotionsRemaining==potions && c.Current==3 && hand.SequenceEqual(Enumerable.Range(0,4).Select(d.GetHandCard)),"no card potion spend or cycle");
                foreach(var enemy in m.Enemies)Call(enemy,"AdvanceCombat",10f);
                check(p.CurrentHp==hp && m.Enemies.All(e=>e.CastProgress01==0) && metrics.Elapsed==0,"enemies and battle clock frozen");
                Call(f,"AdvanceCountdown",.5f);f.NextBattle();check((float)Get(f,"countdownRemaining")==2.5f,"repeated start cannot reset countdown");
                Call(f,"AdvanceCountdown",.5f);check(label.text.Contains("2") && f.IsCountingDown,"two after one real second");
                Call(f,"AdvanceCountdown",1f);check(label.text.Contains("1") && f.IsCountingDown,"one after two real seconds");
                Call(f,"AdvanceCountdown",.99f);check(f.IsCountingDown && Time.timeScale==0,"not before three seconds");
                Call(f,"AdvanceCountdown",.011f);
                check(f.State==BattleFlowState.Fighting && !panel.activeSelf && Time.timeScale==battleSpeed && metrics.Elapsed==0,"start once, restore selected speed, clock excludes preparation");
                check(d.TryUseSlot(0),"skill usable after countdown");d.EndBattle();
                for(int battle=2;battle<=4;battle++)
                {
                    foreach(var enemy in m.GetAliveEnemies())enemy.TakeDamage(10000);m.NotifyEnemyDied();Call(f,"Update");
                    check(f.SkipReward(),"resolve reward "+battle);hp=p.CurrentHp;potions=p.PotionsRemaining;
                    f.NextBattle();check(f.IsCountingDown && f.BattleNumber==battle && p.CurrentHp==hp && p.PotionsRemaining==potions && c.Current==3,"next encounter countdown preserves HP and potions "+battle);
                    Call(f,"AdvanceCountdown",3f);check(f.State==BattleFlowState.Fighting && Time.timeScale==battleSpeed,"next encounter starts "+battle);
                }
                f.RestartRun();t.Skip();NextFrame(t);f.NextBattle();Call(f,"AdvanceCountdown",1f);f.RestartRun();
                check(t.Active && t.Lesson==1 && !f.IsCountingDown && !panel.activeSelf && (float)Get(f,"countdownRemaining")==0,"restart cancels countdown");
                Call(f,"AdvanceCountdown",10f);check(t.ShowingGuide && Time.timeScale==0 && f.BattleNumber==0,"old countdown cannot start over tutorial");
            }
            return "Battle ready checks passed: "+passed+". Synthetic time and inputs; physical inputs separate.";
        }
        finally{Set(f,"finishingEffectHold",hold);Set(info,"resumeScale",speed);f.RestartRun();}
    }

    [MenuItem("Tools/Scroll Hunter/Check Tutorial Combat Regression (Play)")]
    public static void RegressionMenu() => Debug.Log(RunCombatRegression());

    public static string RunCombatRegression()
    {
        if(!Application.isPlaying)throw new InvalidOperationException("Play required");
        var f=UnityEngine.Object.FindFirstObjectByType<BattleFlow>();
        var t=UnityEngine.Object.FindFirstObjectByType<TutorialFlow>();
        var saved=(BattleFlow.Encounter[])Get(f,"encounters");
        var all=UnityEngine.Object.FindObjectsByType<Enemy>(FindObjectsInactive.Include,FindObjectsSortMode.None);
        var b=all.Single(e=>e.Data.name=="Enemy_B_Orange");var c=all.Single(e=>e.Data.name=="Enemy_C_Red");
        bool enabled=t.enabled; float hold=(float)Get(f,"finishingEffectHold"); float countdown=(float)Get(f,"countdownSeconds");
        try
        {
            // 과거 검사의 B/C/BC 전제는 이 런에만 격리한다. 새 저장 편성은 Run()에서 따로 검사한다.
            t.enabled=false; Set(f,"finishingEffectHold",0f); Set(f,"countdownSeconds",0f);
            Set(f,"encounters",new[]{new BattleFlow.Encounter{label="Legacy B",enemies=new[]{b}},new BattleFlow.Encounter{label="Legacy C",enemies=new[]{c}},new BattleFlow.Encounter{label="Legacy BC",enemies=new[]{b,c}}});
            var results=new System.Collections.Generic.List<string>();
            foreach(Func<string> run in new Func<string>[]{RedShieldChecks.Run,StartingDeckChecks.Run,CriticalChecks.Run,
                RemainingSkillChecks.Run,AllPassiveChecks.Run,BattleFlowChecks.Run,CombatReadabilityChecks.Run}) { var result=run(); results.Add(result); Debug.Log(result); }
            results.Add(RewardFlowChecks.Run());
            results.Add(PassiveRewardChecks.Run());
            results.Add(DamageNumberChecks.Run());
            foreach(Func<string> run in new Func<string>[]{StartScreenChecks.Run,DeckOrderChecks.Run})
            {
                Set(f,"<State>k__BackingField",BattleFlowState.Title);Set(f,"<BattleNumber>k__BackingField",0);
                var metrics=UnityEngine.Object.FindFirstObjectByType<CombatMetrics>();metrics.WaitForBattle();
                UnityEngine.Object.FindFirstObjectByType<EnemyManager>().WaitForBattle();
                Set(metrics,"<BattleNumber>k__BackingField",0);Call(f,"RefreshUI");Time.timeScale=0;
                results.Add(run());
            }
            return string.Join("\n",results);
        }
        finally{Set(f,"encounters",saved);Set(f,"finishingEffectHold",hold);Set(f,"countdownSeconds",countdown);t.enabled=enabled;f.RestartRun();}
    }

    private static int frame;
    private static int ticks;
    private static bool sawCompletion;
    private static bool sawCountdown;
    private static float countdownRealElapsed;
    private static float originalCapture;
    private static bool originalPaused;
    private static TutorialFlow probe;
    public static string FrameResult { get; private set; }

    [MenuItem("Tools/Scroll Hunter/Check Tutorial Frames (Play)")]
    public static void StartFrameProbe()
    {
        if(!Application.isPlaying)throw new InvalidOperationException("Play required");
        EditorApplication.update-=FrameStep;
        probe=UnityEngine.Object.FindFirstObjectByType<TutorialFlow>();
        UnityEngine.Object.FindFirstObjectByType<BattleFlow>().BeginTutorial(false);
        originalCapture=Time.captureDeltaTime;originalPaused=EditorApplication.isPaused;
        Time.captureDeltaTime=.05f; EditorApplication.isPaused=true;
        frame=-1;ticks=0;sawCompletion=sawCountdown=false;countdownRealElapsed=0;FrameResult="running";
        EditorApplication.update+=FrameStep;
    }

    private static void FrameStep()
    {
        if(!Application.isPlaying){FinishFrames("stopped");return;}
        if(Time.frameCount==frame)return;
        frame=Time.frameCount;
        try
        {
            if(++ticks>1600)throw new Exception("Frame probe timed out at lesson "+probe.Lesson);
            var d=UnityEngine.Object.FindFirstObjectByType<DeckSystem>();
            var flow=UnityEngine.Object.FindFirstObjectByType<BattleFlow>();
            if(flow.State==BattleFlowState.TutorialComplete)
            {
                var reward=UnityEngine.Object.FindFirstObjectByType<BattleRewardUI>();
                if(((GameObject)Get(reward,"panel")).activeSelf || Time.timeScale!=0)throw new Exception("Completion revealed deck or resumed time");
                sawCompletion=true;
            }
            if(probe.ShowingGuide && !probe.WaitingForSkill)probe.AdvanceGuide();
            else if(probe.WaitingForSkill)d.TryUseSlot(probe.Lesson==3?0:probe.Lesson==4?2:3);
            else if(probe.Active && probe.CompletedLessons>=6)
            {
                // 학습 6까지 실제 Update·시전·피격으로 진행, 이후 처치는 기능 호출로 마무리한다.
                probe.Enemy.TakeDamage(10000);probe.TickCombat();
            }
            else if(!probe.Active && !probe.ShowingGuide)
            {
                if(!sawCompletion)throw new Exception("Completion guide was skipped");
                flow.NextBattle();
                if(flow.IsCountingDown)
                {
                    if(sawCountdown)countdownRealElapsed+=Time.unscaledDeltaTime;
                    sawCountdown=true;
                    var c=UnityEngine.Object.FindFirstObjectByType<CostSystem>();
                    var m=UnityEngine.Object.FindFirstObjectByType<EnemyManager>();
                    var metrics=UnityEngine.Object.FindFirstObjectByType<CombatMetrics>();
                    if(Time.timeScale!=0 || c.Current!=3 || metrics.Elapsed!=0 || m.Enemies.Any(e=>e.CastProgress01!=0))throw new Exception("Countdown advanced combat");
                    EditorApplication.Step();return;
                }
                if(!sawCountdown)throw new Exception("Countdown skipped");
                countdownRealElapsed+=Time.unscaledDeltaTime;
                if(countdownRealElapsed<2.95f || countdownRealElapsed>3.15f)throw new Exception("Countdown duration "+countdownRealElapsed);
                if(flow.BattleNumber!=1 || flow.State!=BattleFlowState.Fighting)throw new Exception("AB did not begin");
                FinishFrames("passed: real Update reached all six combat lessons, normal .2s effects/red impact, hidden deck during completion, confirmation, lesson7, countdown "+countdownRealElapsed.ToString("0.000")+" real seconds and AB; "+ticks+" stepped frames (.05s capture delta)");return;
            }
            EditorApplication.Step();
        }
        catch(Exception ex){FinishFrames("FAILED: "+ex);}
    }

    private static void FinishFrames(string result)
    {
        EditorApplication.update-=FrameStep;Time.captureDeltaTime=originalCapture;
        EditorApplication.isPaused=originalPaused;FrameResult=result;Debug.Log("[Tutorial frames] "+result);
    }
}
