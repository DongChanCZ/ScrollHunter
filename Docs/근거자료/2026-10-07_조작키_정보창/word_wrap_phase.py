from pathlib import Path
import hashlib

root = Path(r'C:\Users\MBC-501-19\Documents\ScrollHunter')
changes = {
    'Assets/_Project/Scripts/Enemy.cs': (
        'A6AB085AB7E6B718CB67378495A6F6F5A75FC7C6E9C7CBF60596E4DD29BADEB9', [
            ('    [SerializeField] private string phaseAttackFormat = "{0}페이즈 · {1}";\n', ''),
            (': HasPhases ? string.Format(phaseAttackFormat, PhaseNumber, current.SkillName) : current.SkillName;', ': current.SkillName;'),
        ]),
    'Assets/_Project/Scripts/CombatInfoUI.cs': (
        '435606E0766E98B30D3649F9C2C42AA1C95988CF5DFBF78B230B2897F63F1327', [
            ('    [SerializeField] private string phaseTransitionFormat = ', '    [SerializeField] private string bossNameFormat = "{0} ({1}페이즈)";\n    [SerializeField] private string phaseTransitionFormat = '),
            ('<b>{1}</b>페이즈 전환</color>\\n무적 <b>{2:0.0}초</b>', '전환 중</color>\\n무적 <b>{1:0.0}초</b>'),
            ('        string name = target.Data.DisplayName;\n', '        string name = target.Data.DisplayName;\n        if (target.HasPhases) name = string.Format(bossNameFormat, name, target.PhaseNumber);\n'),
            ('string.Format(phaseTransitionFormat, name, target.PhaseNumber, target.PhaseTransitionRemaining)', 'string.Format(phaseTransitionFormat, name, target.PhaseTransitionRemaining)'),
        ]),
}
prepared = []
for rel, (expected, replacements) in changes.items():
    path = root / rel
    raw = path.read_bytes()
    assert hashlib.sha256(raw).hexdigest().upper() == expected, f'Changed concurrently: {rel}'
    text = raw.decode('utf-8').replace('\r\n', '\n')
    for old, new in replacements:
        assert text.count(old) == 1, (rel, old)
        text = text.replace(old, new)
    prepared.append((path, text.replace('\n', '\r\n').encode('utf-8')))
for path, data in prepared:
    path.write_bytes(data)
    print(path.name)
