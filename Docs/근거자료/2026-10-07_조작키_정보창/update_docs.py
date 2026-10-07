from pathlib import Path
import re, hashlib

root = Path(r'C:\Users\MBC-501-19\Documents\ScrollHunter')
docs = root / 'Docs'
pending = {}
anchor = 'combat-info-controls-20261007'
ref = f'[10 A22 후속](10_결정사항_로그.md#{anchor})'
def load(name):
    p = docs / name
    raw = p.read_bytes()
    pending[p] = [raw, raw.decode('utf-8-sig').replace('\r\n', '\n')]
    return pending[p][1]
def put(name, text): pending[docs/name][1] = text
def once(text, before, after):
    assert text.count(before) == 1, before
    return text.replace(before, after)
def history(text, row):
    match = re.search(r'^## .*변경 이력[^\n]*\n', text, re.M)
    assert match
    start = match.end()
    sep = re.search(r'^\|[ :\-]+\|[^\n]*\n', text[start:], re.M)
    assert sep
    pos = start + sep.end()
    return text[:pos] + row + '\n' + text[pos:]

name='10_결정사항_로그.md'; text=load(name)
text=once(text, '- **현재 간이 UI:**', '- **초기 간이 UI(9/22 당시):**')
text=once(text, '- **배속 버튼:** 화면 상단 「속도」 좌클릭으로', '- **배속 버튼:** 화면 상단 「속도」 좌클릭 또는 S키로')
original='''<a id="combat-info-controls-20261007"></a>
#### 조작키·정보창 후속 (2026-10-07 사용자 확정)

- A키: 일시정지·재개. 기존 정지 버튼·카드 우클릭은 유지.
- S키: 1배속↔0.5배속. 버튼은 `속도 1배속` / `속도 0.5배속`으로 표시.
- 정지 중 S는 재개할 배속만 변경. 튜토리얼 지정 학습·카운트다운·시작/편성·보상/결과에서는 기존 제한 유지. 정지 중 스킬 사용 불가.
- 전투 정보창: 이름 27.5·주요 정보 22·효과/조건 18.7, 줄 간격 6(1920×1080 기준). 이름·수치는 굵게, 보조 설명은 작고 흐리게 구분. 자동 축소 없이 기존 청흑색 판·아이콘 유지. 적 대기·전환·오브도 같은 이름 크기 적용.
- 스킬 설명은 **총합 피해**로 표시. 1타 피해×타수, 적 1체에 전 타격 명중 기준. 크리티컬 제외, 방어력·방어도 적용 전. 광역도 적 수를 곱하지 않으며 채널링이 끊기면 실제 피해는 더 적을 수 있음.
- 일반 공격·피해 채널링·제압에 표시. 방어·침묵·생츄어리는 피해 합계 없음. 보상·편성도 같은 스킬 설명 사용. 카드 수치·피해 판정은 유지.

적용·검사는 [07 D-15](07_개발일지.md#d15-info-controls), 확인은 [09](09_프로토타입_제작절차.md#combat-info-checks-20261007) 참조.

'''
text=once(text, '<a id="reward-flow-20260922"></a>', original+'<a id="reward-flow-20260922"></a>')
text=once(text, '- 2026-09-30 설명창 가독성(사용자 요청):', '- 2026-09-30 설명창 가독성(당시 적용값, 현재 크기는 [10/7 후속](#combat-info-controls-20261007) 참조):')
text=history(text, f'| 2026-10-07 | 조작키·정보창 | A/S 추가, 설명 크기 구분·총합 피해 표시 | 사용자 확정. [A22 후속](#{anchor}), 07#d15-info-controls |')
put(name,text)

name='02_전투시스템_기획서.md'; text=load(name)
text=once(text, '| 동작 | 우클릭으로 정지/재개. 카드·적에 마우스를 올려 설명 확인. 키워드 중첩 팝업은 미구현 |',
    f'| 동작 | 카드·현재 타겟의 공격 정보 확인. 조작키·표시는 {ref} 참조. 키워드 중첩 팝업은 미구현 |')
text=once(text, '| 해제 | \\[ 재우클릭 \\] |', f'| 해제 | {ref} 참조 |')
text=history(text, f'| 2026-10-07 | 전투 조작·설명 | A/S 단축키·배속 문구·정보창 크기·총합 피해 적용 | {ref}, 07#d15-info-controls |')
put(name,text)

for name,row in [
('03_스킬_기획서.md', f'| 2026-10-07 | 스킬 설명 | 총합 피해·계산 기준 표시, 전투 정보창 글 크기 구분. SK 수치 유지 | {ref}, 07#d15-info-controls |'),
('04_적_패턴_기획서.md', f'| 2026-10-07 | 적 정보창 | 이름·공격 정보·조건 글 크기 구분. 적 수치 유지 | {ref}, 07#d15-info-controls |')]:
    text=load(name); put(name,history(text,row))

name='09_프로토타입_제작절차.md'; text=load(name)
text=once(text, '1. Play 후 상단 「속도 1×」 클릭 → 「속도 0.5×」 확인. 한 번 더 누르면 복귀. 우클릭 정지·재개 뒤에도 선택 배속인지 확인.',
    '1. Play 후 전투에 진입. S 또는 상단 「속도 1배속」 클릭 → 「속도 0.5배속」 확인. 한 번 더 누르면 복귀. A·우클릭 정지/재개 뒤에도 선택 배속인지 확인.')
text=once(text, '5. 함수 검사: Play 중 **Tools → Scroll Hunter → Check Combat Readability (Play)**. 전투를 초기화하므로 실측 도중 실행하지 않음. 현재 HP를 바꾸지 않고 첫 전투·원래 선택 배속으로 복귀. 실제 클릭 검증은 별도.',
    '5. `CombatReadabilityChecks.Run()`은 과거 B→C→B+C 전용. 현행 편성에서 바로 실행하지 말고 `TutorialChecks.RunCombatRegression()`의 임시 편성·복원 경로를 사용. 실측 도중 실행 금지. 실제 키보드·클릭 확인은 별도.')
check='''<a id="combat-info-checks-20261007"></a>
## A/S·정보창·총합 피해 확인 (2026-10-07)

원본은 [10 A22 후속](10_결정사항_로그.md#combat-info-controls-20261007), 적용·검사는 [07 D-15](07_개발일지.md#d15-info-controls). Battle 저장 완료, 추가 배치 없음.

1. 일반 전투에서 A 두 번으로 정지·재개. 정지 중 S로 배속 변경 후 A를 눌러 선택 배속으로 재개되는지 확인. 버튼·카드 우클릭도 확인.
2. 튜토리얼 지정 학습·카운트다운·편성·보상·결과에서 A/S가 시간 진행을 풀지 않는지 확인.
3. 아이스애로우 총합 175, 매직 스파크 40, 블리자드 80, 제압 30 확인. 광역도 적 1체 기준, 크리티컬 제외. 방어·침묵·생츄어리에 피해 합계가 없는지 확인.
4. 정보창·능력치 창을 함께 열어 글 크기와 겹침 확인. 보상·편성의 긴 설명도 확인. 1920×1080 기준, 작은 창은 1366×768로 재확인.
5. **Tools → Scroll Hunter → Check Information Text**로 저장된 14종 설명·영역 검사. 전투 초기화 없음. 실제 키 입력과 체감은 별도.

'''
text=once(text, '<a id="enemy-hp-trial-checks"></a>',check+'<a id="enemy-hp-trial-checks"></a>'); put(name,text)

name='07_개발일지.md'; text=load(name)
record='''<a id="d15-info-controls"></a>
#### A/S 조작키·정보창·총합 피해 (2026-10-07, Codex)

- A 정지·재개, S 배속 전환 연결. 버튼 문구 변경, 스킬·적 정보창 글 크기 구분. 원본 [10 A22 후속](10_결정사항_로그.md#combat-info-controls-20261007).
- 사용자 정정으로 ‘최대 피해’ 대신 ‘총합 피해’ 사용. 일반 공격·피해 채널링·제압 계산, 보상·편성도 공통 설명 반영. 제압은 칸을 넘지 않게 조건 문장만 줄임.
- 변경: CombatInfoUI·DamageChannelEffect·Battle 씬·피해 채널링 설명 에셋 2개. 기존 검사 파일에 정보 검사 추가. 전투 수치·판정 유지.
- 검사: 정보 77개(14종 합계 표기·비피해 제외·채널링/광역/제압·영역), 기존 가독성 33개 통과. 기존 검사는 B→C→B+C를 실행 중에만 적용하고 원래 편성 복원. 정지 실제 8프레임 코스트 유지·카드 거절, 배속 선택/재개·재시작 유지, 튜토리얼·카운트다운·편성 제한도 함수로 확인.
- 화면: [정보창 1920](근거자료/2026-10-07_조작키_정보창/01_정보창_1920.png)·[1366](근거자료/2026-10-07_조작키_정보창/02_정보창_1366.png), [편성 1366](근거자료/2026-10-07_조작키_정보창/03_편성_1366.png). 상태 지정·정지 캡처. 정보창/능력치 겹침·긴 설명 잘림 없음.
- 실제 키보드 A/S는 미확인. Game 뷰에 보낸 검사 이벤트는 구형 Input에 전달되지 않아 입력 통과로 세지 않음. 단축키 연결과 공통 정지·배속 함수 검사는 분리. 새 빌드·전체 런 체감은 미확인.
- 저장 과정에 LayoutGroup 손패 위치·TMP 스타일·이전 마법진 기본값이 직렬화됨. TMP 동적 글리프·기존 배경 재질의 동기화 차이도 남아 있음. 이번 요청으로 배경·마법진 값은 조정하지 않음.
- Play 종료·씬 미저장 표시 없음·마법사 직행 끔·콘솔 오류/경고0. 문서·AGENTS/CLAUDE 동기화. 스테이징·커밋·새 빌드 없음. 다음은 일반 전투에서 A/S 직접 입력과 작은 글자 체감 확인.

'''
text=once(text,'### D-16 ( )',record+'### D-16 ( )'); put(name,text)

handoff='2026-10-07 A/S·정보창·총합 피해 적용. 원본10#combat-info-controls-20261007, 검사·화면·한계07#d15-info-controls, 절차09#combat-info-checks-20261007. 정보77·가독성33 통과. 실제 키보드 입력·새 빌드 체감은 미확인. Play 종료·Battle 저장·마법사 직행 끔. 아래 기록은 당시 상태.\n\n'
for name in ['08_코드세션_인계사양.md','12_기획세션_인계프롬프트.md']:
    text=load(name); old='2026-10-07 튜토리얼 사망·우두머리 턱/차단 피격 보정 적용.'
    put(name,once(text,old,handoff+old))

name='00_문서맵_먼저읽기.md'; text=load(name)
text=once(text,'| 찾을 내용 | 위치 |\n|---|---|', '| 찾을 내용 | 위치 |\n|---|---|\n| A/S·정보창·총합 피해 | '+ref+', [07 적용·검사](07_개발일지.md#d15-info-controls), [09 확인](09_프로토타입_제작절차.md#combat-info-checks-20261007) |')
put(name,text)

for path,(old,new) in pending.items():
    assert path.read_bytes()==old, f'Concurrent change: {path}'
for path,(old,new) in pending.items():
    newline='\r\n' if b'\r\n' in old else '\n'
    bom=b'\xef\xbb\xbf' if old.startswith(b'\xef\xbb\xbf') else b''
    path.write_bytes(bom+new.replace('\n',newline).encode('utf-8'))
    print(path.name)
