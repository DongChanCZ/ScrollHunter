from pathlib import Path
import hashlib, json
root=Path(__file__).resolve().parents[3]
changes=[]
def edit(relative, pairs):
    p=root/relative; raw=p.read_bytes(); text=raw.decode('utf-8-sig').replace('\r\n','\n')
    for old,new in pairs:
        assert text.count(old)==1,(relative,old[:80],text.count(old))
        text=text.replace(old,new)
    assert p.read_bytes()==raw, 'Concurrent change: '+relative
    p.write_bytes(text.replace('\n','\r\n').encode('utf-8'))
    changes.append({'path':relative,'before':hashlib.sha256(raw).hexdigest(),'after':hashlib.sha256(p.read_bytes()).hexdigest()})
edit('Assets/_Project/Scripts/TutorialFlow.cs',[(
'''        ShowGuide(completionPrompt, null);
        consumedFrame = Time.frameCount;
        flow.FinishTutorial(false);''',
'''        HideGuide();
        consumedFrame = Time.frameCount;
        flow.FinishTutorial(false);'''),(
'''    public void Skip()''',
'''    internal void ShowCompletionGuide()
    {
        ShowGuide(completionPrompt, null);
        consumedFrame = Time.frameCount;
    }

    public void Skip()''')])
edit('Assets/_Project/Scripts/BattleFlow.cs',[(
'''        enemies.WaitForBattle();
        if (information != null) information.BeginBattle();
        if (skillVfx != null) skillVfx.Clear();
        if (damageNumbers != null) damageNumbers.Clear();
        panelHoldRemaining = 0f;
        State = skipped ? BattleFlowState.Preparing : BattleFlowState.TutorialComplete;''',
'''        if (information != null) information.BeginBattle();
        panelHoldRemaining = 0f;
        State = skipped ? BattleFlowState.Preparing : BattleFlowState.TutorialComplete;
        if (!skipped) HoldVictoryPresentation();
        if (PanelHeld)
        {
            if (skillVfx != null) skillVfx.PlayOutUnscaled();
        }
        else FinishTutorialPresentation();'''),(
'''    internal void OpenTutorialPreparation()''',
'''    private void FinishTutorialPresentation()
    {
        enemies.WaitForBattle();
        if (skillVfx != null) skillVfx.Clear();
        if (damageNumbers != null) damageNumbers.Clear();
        if (State == BattleFlowState.TutorialComplete) tutorial.ShowCompletionGuide();
    }

    internal void OpenTutorialPreparation()'''),(
'''        if (State != BattleFlowState.TutorialComplete) return;''',
'''        if (State != BattleFlowState.TutorialComplete || PanelHeld) return;'''),(
'''        if (won && finishingEffectHold > 0f)
        {
            if (skillVfx != null && skillVfx.HasFinishingEffect) panelHoldRemaining = finishingEffectHold;
            foreach (var model in FindObjectsByType<EnemyAnimationDriver>(FindObjectsSortMode.None))
                panelHoldRemaining = Mathf.Max(panelHoldRemaining, model.PlayDeathUnscaled());
        }''',
'''        if (won) HoldVictoryPresentation();'''),(
'''    // 막타 연출을 보여주는 동안 결과·보상 화면을 숨겨 두는 남은 시간(실제 초).''',
'''    private void HoldVictoryPresentation()
    {
        if (finishingEffectHold <= 0f) return;
        if (skillVfx != null && skillVfx.HasFinishingEffect) panelHoldRemaining = finishingEffectHold;
        foreach (var model in FindObjectsByType<EnemyAnimationDriver>(FindObjectsSortMode.None))
            panelHoldRemaining = Mathf.Max(panelHoldRemaining, model.PlayDeathUnscaled());
    }

    // 막타 연출을 보여주는 동안 결과·보상 화면을 숨겨 두는 남은 시간(실제 초).'''),(
'''        ClearDefeatEffects();
        RefreshUI();
        return false;''',
'''        ClearDefeatEffects();
        if (State == BattleFlowState.TutorialComplete) FinishTutorialPresentation();
        RefreshUI();
        return false;''')])
edit('Assets/_Project/Editor/EnemyRigBuilder.cs',[(
'''new string[0], new[] { "Death" }''', '''new string[0], new[] { "Hit", "Death" }''')])
edit('Assets/_Project/Scripts/HandIK.cs',[(
'''사망에서는 끈다.''', '''차단 피격·사망에서는 끈다.''')])
Path(__file__).with_name('code_patch_hashes.json').write_text(json.dumps(changes,indent=2),encoding='utf-8')
print('Patched',len(changes),'files')
