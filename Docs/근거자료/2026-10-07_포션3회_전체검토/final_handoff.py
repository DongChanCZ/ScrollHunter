from pathlib import Path

root = Path(__file__).resolve().parents[3]
a, c = root / 'AGENTS.md', root / 'CLAUDE.md'
original = a.read_bytes()
assert original == c.read_bytes(), '인계 문서가 달라짐. 재검토 필요'
s = original.decode('utf-8-sig').replace('\r\n', '\n')
pairs = [
    ('- 막타 보류는 화면 표시만 늦춘다. 승패 판정·보상 준비·요약 출력은 즉시 처리하며, 패배·스킬 외 처치는 보류하지 않는다.',
     '- 승패 판정은 즉시 처리. 승리는 막타·사망 연출 후, 패배는 발동 연출 종료 후 결과 화면 표시(10 A40·A44). 요약은 마지막 행동 로그 뒤에 출력(10 A21).'),
    ('| 🟢 Green | 일반 공격 | 불필요하나 차단 가능 |',
     '| 🟢 Green | 일반 공격 | 필요 시 차단 가능 |'),
    ('- **모든 적이 🟢 초록을 보유한다.** 주황·빨강은 그 위에 추가되는 공격이다',
     '- **공격하는 적은 모두 🟢 초록을 보유한다.** 오브는 공격하지 않음(10 A43). 주황·빨강은 추가 공격.'),
    ('- 비활성 사유 4가지를 **시각적으로 구분할 것.** 전부 회색이면 원인을 모른다',
     '- 기본 비활성 사유 4가지는 아래처럼 구분. 튜토리얼 지정 입력·보스 전환 중 새 스킬 제한은 10 A32·A34 적용.'),
]
for old, new in pairs:
    assert s.count(old) == 1, old
    s = s.replace(old, new)
assert a.read_bytes() == original and c.read_bytes() == original
for path in (a, c):
    path.write_text(s, encoding='utf-8', newline='\n')
print('AGENTS/CLAUDE: 현재 규칙 4곳 동일 정정')
