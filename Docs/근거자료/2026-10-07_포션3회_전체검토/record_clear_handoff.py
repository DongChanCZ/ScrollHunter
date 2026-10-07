from pathlib import Path

root = Path(__file__).resolve().parents[3]
a, c = root / 'AGENTS.md', root / 'CLAUDE.md'
before = a.read_bytes()
assert before == c.read_bytes(), '인계 문서 불일치'
s = before.decode('utf-8-sig').replace('\r\n', '\n')
entry = '\n> **2026-10-07 사용자 클리어 로그(Codex 분석):** 1배속·수동 정지 사용, 튜토리얼부터 보스까지 승리. 기록 원본 `Docs/07#d15-player-clear`. 포션 3개 사용·오브 3개 파괴·2/3페이즈 진행 확인. 생츄어리·패시브 없이 완주한 제출 1런이며 무정지 난이도·반복 승률과 구분. 오브 처리 이유는 체력 압박 예상, 오솔길 적이 배경에 묻히는 문제는 남음(사용자). 다음은 오솔길 가시성 보정·새 빌드 확인. 이번 작업은 로그 대조·문서 기록만, 게임·수치·배경·Unity 실행·빌드·커밋 변경 없음.\n'
assert '사용자 클리어 로그(Codex 분석)' not in s
assert '## 현재 단계\n' in s
s = s.replace('## 현재 단계\n', '## 현재 단계\n' + entry, 1)
assert a.read_bytes() == before and c.read_bytes() == before
for p in (a, c):
    p.write_text(s, encoding='utf-8', newline='\n')
print('AGENTS.md·CLAUDE.md 동일 기록 완료')
