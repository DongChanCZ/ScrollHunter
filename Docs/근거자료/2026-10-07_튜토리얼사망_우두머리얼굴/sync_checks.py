from pathlib import Path
root=Path(__file__).resolve().parents[3]
def edit(path,old,new):
 p=root/path;raw=p.read_bytes();text=raw.decode('utf-8-sig').replace('\r\n','\n');assert text.count(old)==1
 text=text.replace(old,new);assert p.read_bytes()==raw;p.write_bytes(text.replace('\n','\r\n').encode('utf-8'))
edit('Assets/_Project/Tests/Editor/EnemyModelChecks.cs',
 '("Leader/ENM_Leader_RED_v01", "2026-10-02_적모델링/05_우두머리_손보정_리깅", true, null)',
 '("Leader/ENM_Leader_RED_v01", "2026-10-07_튜토리얼사망_우두머리얼굴/Leader_face_weights", true, "2026-10-02_적모델링/05_우두머리_손보정_리깅")')
edit('Assets/_Project/Tests/Editor/EnemyAnimationLinkChecks.cs',
 'b.CurrentAttack.CastTime * b.Data.Phases[1].CastTimeMultiplier',
 'b.CurrentAttack.CastTime * (b.Data.Phases[1].CastTimeMultiplier - ((bool)Get(b, "cycleAtCastStart") ? b.Data.OrbCastTimeReduction : 0f))')
edit('Assets/_Project/Tests/Editor/TutorialChecks.cs',
 'else if(!probe.Active && !probe.ShowingGuide)',
 'else if(!probe.Active && !probe.ShowingGuide && flow.State!=BattleFlowState.TutorialComplete)')
print('Synced model source, orb cast multiplier and tutorial death hold checks')
