from pathlib import Path
root=Path(__file__).resolve().parents[3]
p=root/'Assets/_Project/Tests/Editor/EnemyAnimationLinkChecks.cs'
raw=p.read_bytes(); text=raw.decode('utf-8-sig').replace('\r\n','\n')
old='''            check(result.activeSelf, "boss result opens after presentation");'''
new=old+'''
            // 튜토리얼도 사망 모션을 먼저 마치고 완료 안내를 연다.
            t.enabled = true; f.RestartRun();
            typeof(TutorialFlow).GetProperty("CompletedLessons").SetValue(t, 6);
            t.Begin(t.Enemy.MaxHp, true);
            var tutorialDriver = Driver(t.Enemy); var ta = tutorialDriver.GetComponent<Animator>();
            t.Enemy.TakeDamage(t.Enemy.CurrentHp); t.TickCombat();
            check(f.State == BattleFlowState.TutorialComplete && !t.Active && !t.ShowingGuide, "tutorial guide waits for death");
            check(tutorialDriver.IsDead && tutorialDriver.IsDetached && tutorialDriver.gameObject.activeInHierarchy, "tutorial corpse remains visible");
            check(Time.timeScale == 0f && ta.updateMode == AnimatorUpdateMode.UnscaledTime, "tutorial death uses real time only");
            check(!t.AdvanceGuide() && !f.CanEditOrder && !deck.TryUseSlot(0), "tutorial hold cannot be skipped by combat/guide input");
            float duration = (float)Get(f, "panelHoldRemaining");
            check(duration > .1f, "tutorial hold includes death clip");
            for (int i = 0; i < Mathf.CeilToInt(duration * 60) + 1; i++) ta.Update(1f / 60);
            check(ta.GetCurrentAnimatorStateInfo(0).IsName("Death") && ta.GetCurrentAnimatorStateInfo(0).normalizedTime >= 1, "tutorial fall completes");
            Set(f, "panelHoldRemaining", float.Epsilon); Call(f, "TickPanelHold");
            check(t.ShowingGuide && !tutorialDriver.IsDetached && !tutorialDriver.gameObject.activeInHierarchy, "guide opens and corpse is cleaned after death");
            Set(t, "consumedFrame", -1); check(t.AdvanceGuide() && f.State == BattleFlowState.Preparing, "completion still opens formation lesson");
            f.RestartRun();
            check(t.Active && !tutorialDriver.IsDead && ta.updateMode == AnimatorUpdateMode.Normal, "tutorial restart restores living model");
            t.Skip(); check(f.State == BattleFlowState.Preparing && !t.ShowingGuide && (float)Get(f, "panelHoldRemaining") == 0, "tutorial skip has no death hold");
            f.RestartRun(); typeof(TutorialFlow).GetProperty("CompletedLessons").SetValue(t, 6); t.Begin(t.Enemy.MaxHp, true);
            t.Enemy.TakeDamage(t.Enemy.CurrentHp); t.TickCombat(); f.RestartRun();
            check(t.Active && !tutorialDriver.IsDead && !tutorialDriver.IsDetached && (float)Get(f, "panelHoldRemaining") == 0, "restart during tutorial death cancels old presentation");'''
assert text.count(old)==1;text=text.replace(old,new)
old='''                    foreach (string state in new[] { a.HasState(0, Animator.StringToHash("Fire0")) ? "Fire0" : "Release", "Hit", "Death" })'''
new='''                    if (prefab.name.Contains("Leader"))
                    {
                        var ik = go.GetComponent<HandIK>();
                        foreach (string state in new[] { "Idle", "Cast", "Hit", "Death" })
                        {
                            a.Play(state, 0, .4f); a.Update(0f); ik.Snap();
                            Check(Mathf.Abs(ik.Weight - (state == "Hit" || state == "Death" ? 0f : 1f)) < .001f, "leader grip releases only Hit/Death: " + state);
                        }
                    }
'''+old
assert text.count(old)==1;text=text.replace(old,new)
assert p.read_bytes()==raw
p.write_bytes(text.replace('\n','\r\n').encode('utf-8'))
print('Added tutorial death and leader grip regressions')
