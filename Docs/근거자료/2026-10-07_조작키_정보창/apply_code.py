from pathlib import Path
import hashlib, re

root = Path(r'C:\Users\MBC-501-19\Documents\ScrollHunter')
changes = {}

def read(relative, expected):
    path = root / relative
    raw = path.read_bytes()
    assert hashlib.sha256(raw).hexdigest().upper() == expected, f'Concurrent change: {path}'
    return path, raw.decode('utf-8-sig').replace('\r\n', '\n')

def replace(text, before, after):
    assert text.count(before) == 1, before
    return text.replace(before, after)

path, text = read('Assets/_Project/Scripts/CombatInfoUI.cs', '8360B24B86A767034971223388894CCCC003F1CC47DC5992D904E0C538B5A89B')
text = replace(text, '    [Header("표시 문자열")]', '    [Header("조작키")]\n    [SerializeField] private KeyCode pauseKey = KeyCode.A;\n    [SerializeField] private KeyCode speedKey = KeyCode.S;\n\n    [Header("표시 문자열")]')
text = replace(text, '카드에 마우스: 정보 / 우클릭: 정지·재개', 'A 정지·재개 · S 배속 · 카드에 마우스: 정보')
text = replace(text, '일시정지 — 정보 확인만 가능 / 우클릭 또는 재개', '일시정지 · A/우클릭 재개 · S 배속')
text = text.replace('이름(145%·굵게·아이보리)', '이름(125%·굵게·아이보리)').replace('<size=145%>', '<size=125%>')
text = replace(text, '{1}{2}\\n{3}";', '{1}{2}\\n<size=85%>{3}</size>";')
text = replace(text, '기본 피해 <b>{0} × {1}회</b>\\n<alpha=#CC>방어력 적용 전 피해<alpha=#FF>', '피해 <b>{0}×{1}회</b> · 총합 피해 <b>{2}</b>\\n<size=80%><alpha=#CC>적 1체·전 타격·크리티컬/방어 적용 전<alpha=#FF></size>')
text = replace(text, '기본 피해 <b>{0} × {1}회</b>\\n피해 후 생존 대상에게 차단 판정', '피해 <b>{0}×{1}회</b> · 총합 피해 <b>{3}</b>\\n<size=80%><alpha=#CC>적 1체·전 타격·크리티컬/방어 적용 전<alpha=#FF></size>\\n피해 후 생존 대상에게 차단 판정')
text = replace(text, '<color=#F5EBD6>{1}</color>\\n<alpha=#CC>기본 피해', '<b><color=#F5EBD6>{1}</color></b>\\n<alpha=#CC>기본 피해')
text = replace(text, '{6}\\n{5}";', '{6}\\n<size=85%>{5}</size>";')
text = replace(text, '속도 {0:0.#}×', '속도 {0:0.#}배속')
text = replace(text, '        if (Input.GetMouseButtonDown(1))', '        if (Input.GetKeyDown(pauseKey)) TogglePause();\n        else if (Input.GetKeyDown(speedKey)) ToggleSpeed();\n        else if (Input.GetMouseButtonDown(1))')
text = replace(text, 'card.HitCount, deck.InterruptStagger)', 'card.HitCount, deck.InterruptStagger, (long)card.Damage * card.HitCount)')
text = replace(text, 'string.Format(damageFormat, card.Damage, card.HitCount)', 'string.Format(damageFormat, card.Damage, card.HitCount, (long)card.Damage * card.HitCount)')
changes[path] = text

path, text = read('Assets/_Project/Scripts/DamageChannelEffect.cs', '78EB8A379FF34F0774FED4A7F364E7A4DD33ADC659396110D3721DC2009CEDFF')
text = replace(text, '기본 피해 {0} × {1}회\\n첫 타격', '피해 <b>{0}×{1}회</b> · 총합 피해 <b>{6}</b>\\n<size=80%><alpha=#CC>적 1체·전 타격·크리티컬/방어 적용 전<alpha=#FF></size>\\n첫 타격')
text = replace(text, 'string.Format(singleTargetFormat, targetDeathRecovery));', 'string.Format(singleTargetFormat, targetDeathRecovery), (long)skill.Damage * skill.HitCount);')
changes[path] = text

for path, text in changes.items():
    path.write_bytes(text.replace('\n', '\r\n').encode('utf-8'))
    print(path.relative_to(root))
