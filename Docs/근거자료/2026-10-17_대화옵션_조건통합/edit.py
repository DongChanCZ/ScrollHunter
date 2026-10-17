from pathlib import Path
import json

root=Path(__file__).resolve().parents[3]
old='스테이지에서 승리하면 변경할 수 있습니다.'
new='재도전 또는 전투 승리 후 변경할 수 있습니다.'

def edit(rel, fn):
    p=root/rel
    raw=p.read_bytes()
    s=raw.decode('utf-8-sig').replace('\r\n','\n')
    t=fn(s)
    assert s!=t, rel
    p.write_bytes((b'\xef\xbb\xbf' if raw.startswith(b'\xef\xbb\xbf') else b'')+t.replace('\n','\r\n' if b'\r\n' in raw else '\n').encode('utf-8'))

edit('Assets/_Project/Scripts/MainMenuUI.ScrollOptions.cs',lambda s:s.replace(old,new).replace('저장된 스테이지 승리 기록이 있으면 조작 가능.', '재도전 시작 또는 본편 승리 기록이 있으면 조작 가능.').replace('&& flow.Inheritance.HasStageVictory;', '&& (flow.Inheritance.HasRetried || flow.Inheritance.HasStageVictory);'))
edit('Assets/_Project/Scripts/InheritanceSave.cs',lambda s:s.replace('// 재도전 기록은 이전 저장 호환·진행 여부 판단용으로 유지한다. 대화 스킵 옵션 잠금 해제에는 쓰지 않는다.', '// 재도전 기록은 진행 여부 판단과 대화 스킵 옵션 잠금 해제에 사용한다.'))

def strings(s):
    translations={old:new,'Available after you win a stage.':'Available after starting a retry or winning a battle.', 'ステージで勝利すると変更できます。':'再挑戦を開始するか、戦闘に勝利すると変更できます。','在关卡中获胜后即可更改。':'开始重新挑战或战斗胜利后可更改。','Disponible al ganar una etapa.':'Disponible al reintentar o ganar un combate.'}
    for a,b in translations.items():
        assert a in s,a
        s=s.replace(a,b)
    return s
edit('Assets/_Project/Resources/GameText.json',strings)

def checks(s):
    s=s.replace(old,new).replace('locked without stage victory, default checked','locked without retry or victory, default checked')
    s=s.replace('// 재도전 기록만으로는 열리지 않는다(기존 hasRetried 필드는 유지).','// 승리 없이 재도전한 경우도 옵션이 열리고 적용/재읽기가 가능하다.')
    s=s.replace('menu.OpenOptions(); check(!toggle.interactable && hint.gameObject.activeSelf, "retry alone stays locked");','''menu.OpenOptions(); check(toggle.interactable && !hint.gameObject.activeSelf, "retry alone unlocks");
            toggle.isOn = false; menu.ApplyOptions(); check(!flow.SkipSeenDialogue, "retry-only apply works");
            var retryOnly = new InheritanceSave(path, pool, deck);
            flow.GetType().GetField("inheritance", Hidden).SetValue(flow, retryOnly);
            menu.OpenOptions(); check(toggle.interactable && !toggle.isOn, "retry-only reload stays unlocked and remembers off");
            menu.ClosePanels(); flow.SetSkipSeenDialogue(true);
            flow.GetType().GetField("inheritance", Hidden).SetValue(flow, save);''')
    s=s.replace('// 이번 플레이에서 승리 기록이 생기면 같은 실행 안에서 바로 열린다.','''// 다시 새 저장으로 만들어 승리만으로도 같은 실행 안에서 열리는지 확인한다.
            check(save.TryResetGame() && !save.HasRetried, "clear retry before victory-only case");''')
    s=s.replace('reloaded.HasStageVictory && reloaded.HasRetried, "reload keeps victory unlock and retry field"','reloaded.HasStageVictory && !reloaded.HasRetried, "victory-only reload unlocks without retry"')
    s=s.replace('check(save.TryReset() && save.HasStageVictory, "inheritance reset keeps victory");','''check(save.TryRecordRetry() && save.HasRetried && save.HasStageVictory, "both conditions present");
            menu.OpenOptions(); check(toggle.interactable, "both conditions unlock"); menu.ClosePanels();
            check(save.TryReset() && save.HasStageVictory && save.HasRetried, "inheritance reset keeps both unlock records");''')
    return s
edit('Assets/_Project/Tests/Editor/DialogueOptionsChecks.cs',checks)
edit('Assets/_Project/Tests/Editor/RetryFlowChecks.cs',lambda s:s.replace('confirmed retry record persists but does not unlock option','confirmed retry record persists and qualifies for option unlock'))
print('Updated option condition, 5 languages and regression checks.')
