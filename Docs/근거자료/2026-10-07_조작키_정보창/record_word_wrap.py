from pathlib import Path
import hashlib, sys

root = Path(r'C:\Users\MBC-501-19\Documents\ScrollHunter')
pending = []
def prepare(path, pairs):
    raw = path.read_bytes()
    text = raw.decode('utf-8-sig').replace('\r\n', '\n')
    for old, new in pairs:
        assert text.count(old) == 1, (path.name, old)
        text = text.replace(old, new)
    newline = '\r\n' if b'\r\n' in raw else '\n'
    bom = b'\xef\xbb\xbf' if raw.startswith(b'\xef\xbb\xbf') else b''
    pending.append((path, raw, bom + text.replace('\n', newline).encode('utf-8')))

if '--root' in sys.argv:
    paths = [root/'AGENTS.md', root/'CLAUDE.md']
    assert paths[0].read_bytes() == paths[1].read_bytes()
    for p in paths:
        assert hashlib.sha256(p.read_bytes()).hexdigest().upper() == '1EC471D090618090BBE35720EEFFBE0D8E2CE1B682BFC9936B88C315B120F7B7'
        prepare(p, [('후속으로 글자 크기는 유지하고 정보창 단락 간격 확대, 정보77 재검사·두 해상도 화면 확인.',
            '후속으로 단락 간격·몹 설명 좌우 여백 확대, 전체 TMP UI 단어 단위 줄바꿈 적용. 보스 페이즈는 캐스팅 바에서 빼고 정보창 이름에 표시. 정보77·단어 줄바꿈·1~3페이즈/대기/전환·일반 적/오브 제목과 두 해상도 화면 확인. 글자 크기 유지.')])
else:
    d = root/'Docs'
    prepare(d/'10_결정사항_로그.md', [
        ('- 스킬 설명은 **총합 피해**로 표시.', '- 몹 설명창 좌우 안쪽 여백: 24→30(공백 한 칸 정도 추가). 글자 크기는 유지.\n- 모든 UI는 한글도 단어 단위로 줄바꿈. 줄 끝에 단어가 걸리면 다음 줄로 이동. TMP 공통 설정 사용, 기존 한 줄 표시는 유지.\n- 보스 캐스팅 바는 공격명만 표시. 페이즈는 몹 설명창 이름에 `마법사 (1페이즈)`처럼 표시. 대기·전환 중에도 현재 페이즈 표시, 캐스팅 바의 전환·무적 안내는 유지.\n- 스킬 설명은 **총합 피해**로 표시.'),
        ('A/S 추가, 설명 크기 구분·총합 피해 표시 |', 'A/S 추가, 설명 크기·간격·단어 줄바꿈·총합 피해, 보스 페이즈 표시 이동 |'),
    ])
    prepare(d/'02_전투시스템_기획서.md', [('A/S 단축키·배속 문구·정보창 크기/단락 간격·총합 피해 적용 |', 'A/S 단축키·정보창 간격/단어 줄바꿈·총합 피해·보스 페이즈 표시 적용 |')])
    prepare(d/'03_스킬_기획서.md', [('전투 정보창 글 크기 구분·단락 간격 확대. SK 수치 유지 |', '전투 정보창 글 크기 구분·단락 간격·단어 줄바꿈 적용. SK 수치 유지 |')])
    prepare(d/'04_적_패턴_기획서.md', [('이름·공격 정보·조건 글 크기 구분·단락 간격 확대. 적 수치 유지 |', '글 크기 구분·단락/좌우 간격·단어 줄바꿈, 보스 페이즈를 정보창 이름에 표시. 적 수치 유지 |')])
    prepare(d/'09_프로토타입_제작절차.md', [
        ('5. **Tools → Scroll Hunter → Check Information Text**', '5. 몹 설명창 좌우 여유와 단어 줄바꿈 확인. 스킬·몹·보상·편성·튜토리얼 등 긴 설명이 줄 끝에서 단어 중간에 끊기지 않는지 확인.\n6. 보스 1~3페이즈에서 캐스팅 바는 공격명만, 정보창 이름은 `마법사 (N페이즈)`인지 확인. 대기·전환 중 제목, 전환/무적 안내와 일반 적·오브 이름도 확인.\n7. **Tools → Scroll Hunter → Check Information Text**'),
    ])
    prepare(d/'07_개발일지.md', [('### D-16 ( )',
        '- 후속(좌우 여백·줄바꿈·페이즈): 몹 설명창 좌우 여백 확대, TMP 공통 한글 단어 줄바꿈 적용. 보스 캐스팅의 페이즈는 정보창 이름으로 이동. Enemy·CombatInfoUI·TMP Settings·Battle 저장, 글자 크기·전투 수치 유지. 원본 10 A22 후속.\n'
        '- 확인: 정보77 통과. 실제 TMP에서 `방어도 크리티컬`을 좁게 표시해 두 단어가 각각 한 줄에 온전히 표시됨. 보스 1~3페이즈 시전·2/3 전환·대기 제목, 일반 적·오브 제목 확인. [1920](근거자료/2026-10-07_조작키_정보창/07_단어줄바꿈_보스_1920.png)·[1366](근거자료/2026-10-07_조작키_정보창/08_단어줄바꿈_보스_1366.png)에서 여백·잘림 확인(상태 지정·정지 캡처).\n'
        '- 검사 정정: 첫 줄바꿈 검사는 Canvas 없는 임시 텍스트라 표시 정보가 없어 제외, Play의 Canvas에서 재검사 통과. 오브 제목 검사는 본문의 기존 페이즈 설명까지 비교해 실패→제목 줄만 비교해 통과. 게임 오류와 구분. 전체 UI 화면별 직접 확인·새 빌드는 미실시. Play 종료·씬 미저장 표시 없음.\n\n### D-16 ( )')])
    for name in ['08_코드세션_인계사양.md', '12_기획세션_인계프롬프트.md']:
        prepare(d/name, [('후속 정보창 단락 간격 확대·글자 크기 유지.', '후속 단락/몹 설명 좌우 여백·전체 UI 단어 줄바꿈 적용, 보스 페이즈는 정보창 이름에 표시. 글자 크기 유지.')])

for path, raw, updated in pending:
    assert path.read_bytes() == raw, f'Changed concurrently: {path}'
for path, raw, updated in pending:
    path.write_bytes(updated)
    print(path.name)
