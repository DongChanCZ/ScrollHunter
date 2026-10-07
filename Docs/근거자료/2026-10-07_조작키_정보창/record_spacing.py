from pathlib import Path
import hashlib, sys
root=Path(r'C:\Users\MBC-501-19\Documents\ScrollHunter')
def edit(path, pairs):
    raw=path.read_bytes(); text=raw.decode('utf-8-sig').replace('\r\n','\n')
    for old,new in pairs:
        assert text.count(old)==1, old
        text=text.replace(old,new)
    assert path.read_bytes()==raw
    newline='\r\n' if b'\r\n' in raw else '\n'
    bom=b'\xef\xbb\xbf' if raw.startswith(b'\xef\xbb\xbf') else b''
    path.write_bytes(bom+text.replace('\n',newline).encode('utf-8'))
    print(path.name)
if '--root' in sys.argv:
    paths=[root/'AGENTS.md',root/'CLAUDE.md']
    assert all(hashlib.sha256(p.read_bytes()).hexdigest().upper()=='0C2C1B4DA38E786AF80A87C4A3C4824F83423225D17BC70CE5DF6DCA937310A7' for p in paths)
    for p in paths:
        edit(p,[('A 정지·재개, S 배속, 설명 크기 구분·총합 피해 표시 적용.',
            'A 정지·재개, S 배속, 설명 크기 구분·총합 피해 표시 적용. 후속으로 글자 크기는 유지하고 정보창 단락 간격 확대, 정보77 재검사·두 해상도 화면 확인.')])
else:
    d=root/'Docs'
    edit(d/'10_결정사항_로그.md', [('적 대기·전환·오브도 같은 이름 크기 적용.',
        '적 대기·전환·오브도 같은 이름 크기 적용. 후속: 글자 크기·단락 안 줄 간격은 유지, 이름 아래 빈줄 추가·정보/효과 사이 빈줄은 본문 40%→80% 높이로 확대.')])
    edit(d/'02_전투시스템_기획서.md', [('A/S 단축키·배속 문구·정보창 크기·총합 피해 적용 |',
        'A/S 단축키·배속 문구·정보창 크기/단락 간격·총합 피해 적용 |')])
    edit(d/'03_스킬_기획서.md', [('총합 피해·계산 기준 표시, 전투 정보창 글 크기 구분. SK 수치 유지 |',
        '총합 피해·계산 기준 표시, 전투 정보창 글 크기 구분·단락 간격 확대. SK 수치 유지 |')])
    edit(d/'04_적_패턴_기획서.md', [('이름·공격 정보·조건 글 크기 구분. 적 수치 유지 |',
        '이름·공격 정보·조건 글 크기 구분·단락 간격 확대. 적 수치 유지 |')])
    edit(d/'09_프로토타입_제작절차.md', [('4. 정보창·능력치 창을 함께 열어 글 크기와 겹침 확인.',
        '4. 정보창·능력치 창을 함께 열어 글 크기·단락 간격과 겹침 확인.')])
    edit(d/'07_개발일지.md', [('### D-16 ( )',
        '- 후속(단락 간격): 사용자 확인으로 글자 크기는 유지. 이름 아래 빈줄 추가·주요 정보/효과 사이 빈줄 확대. CombatInfoUI 기본값·Battle 저장 동기화, 보상/편성 배치 유지. 정보77 재검사 통과. [1920](근거자료/2026-10-07_조작키_정보창/04_단락간격_1920.png)·[긴 설명 1366](근거자료/2026-10-07_조작키_정보창/06_단락간격_긴설명_1366.png)에서 겹침·잘림 없음(정지 상태 지정 캡처). Play 종료·콘솔 오류/경고0, 새 빌드 없음.\n\n### D-16 ( )')])
    for name in ['08_코드세션_인계사양.md','12_기획세션_인계프롬프트.md']:
        edit(d/name,[('2026-10-07 A/S·정보창·총합 피해 적용.',
            '2026-10-07 A/S·정보창·총합 피해 적용. 후속 정보창 단락 간격 확대·글자 크기 유지.')])
