from pathlib import Path
import sys

root = Path(__file__).resolve().parents[4]
docs = root / 'Docs'
if '--handoff' in sys.argv:
    paths = [root/'AGENTS.md', root/'CLAUDE.md']
    before = {p:p.read_bytes() for p in paths}
    assert before[paths[0]] == before[paths[1]]
    s = before[paths[0]].decode('utf-8-sig').replace('\r\n','\n')
    entry = '\n> **2026-10-07 일반 맵 적 가시성(Codex / Unity MCP):** 깡패~우두머리 구간의 배경색·소품 배치 보정. 원본 `Docs/10#outdoor-readability-20261007`(A45 후속), 변경·화면·한계 `Docs/07#d15-outdoor-readability`, 조정 위치 `Docs/06#background-assets-20261007`. 배치 도구·재질·Battle 저장 동기화. 배경31 통과·5편성 화면 확인. 저장 씬 차이는 배경 Transform7·바위 재질 연결6, 적 재질·전투 데이터·보스 배경 유지. 검사 종료 때 콘솔 오류/경고0, 후속 직접 체감·새 빌드는 미확인. 검사 후 Play 종료했고 문서 정리 중 Play 재진입이 확인되어 추가 조작하지 않음. 수치·새 빌드·커밋 변경 없음.\n'
    assert '일반 맵 적 가시성(Codex / Unity MCP)' not in s
    s=s.replace('## 현재 단계\n','## 현재 단계\n'+entry,1)
    for p in paths: assert p.read_bytes()==before[p]
    for p in paths: p.write_text(s,encoding='utf-8',newline='\n')
    print('AGENTS·CLAUDE 동일 인계 완료');sys.exit()

names=['00_문서맵_먼저읽기.md','06_에셋_파이프라인.md','07_개발일지.md','09_프로토타입_제작절차.md','10_결정사항_로그.md','12_기획세션_인계프롬프트.md']
before={n:(docs/n).read_bytes() for n in names}
texts={n:b.decode('utf-8-sig').replace('\r\n','\n') for n,b in before.items()}
def replace(n,old,new):
    assert texts[n].count(old)==1,(n,old)
    texts[n]=texts[n].replace(old,new,1)

spec='''<a id="outdoor-readability-20261007"></a>
#### A45 후속. 일반 맵 적 가시성 (2026-10-07)

- 범위는 숲 초입·오솔길·동굴 입구. 깡패·약탈자·궁병·우두머리가 배경과 구분되도록 배경의 갈색기와 밝기를 낮춤.
- 오솔길 나무 6개를 양옆으로 이동. 동굴 입구는 폭만 1.22배, 높이·깊이는 유지. 일반 맵 바위는 별도 재질 사용.
- 적 재질·전역 조명·카메라·보스 동굴은 유지. 안개판·새 셰이더·전투 기능 추가 없음.
- 배치 시험값은 `BattleEnvironmentSetup` 색 배열·Prop 좌표/폭에 반영. 적용값·변경 전후 화면은 [07](07_개발일지.md#d15-outdoor-readability), 조정 위치는 [06](06_에셋_파이프라인.md#background-assets-20261007).

'''
replace(names[4],'<a id="potion-three-20261007"></a>',spec+'<a id="potion-three-20261007"></a>')
entry='''<a id="d15-outdoor-readability"></a>
#### 일반 맵 적 가시성 보정 (2026-10-07, Codex)

- 사용자 요청: 오솔길에서 적이 배경에 묻히는 문제를 깡패~우두머리 맵까지 보정. 원본 [10 A45 후속](10_결정사항_로그.md#outdoor-readability-20261007).
- 숲 초입·오솔길·동굴 입구의 배경 3개·나무/관목/입구 재질 4개를 더 차갑고 낮은 밝기로 조정. 일반 맵 바위는 `P04_MossRocks_Outdoor`로 분리해 6곳에 연결. 보스가 쓰는 기존 바위 재질 유지.
- 오솔길 나무 6개를 바깥으로 이동해 적 뒤 줄기 겹침을 줄임. 동굴 입구는 가로 1.22배. `BattleEnvironmentSetup`·재질·Battle 저장값 동기화, 전체 배경 빌더 재실행 없음.
- 확인: 배경 함수 검사 31개 통과(전환·화면비·재시작·마법진·입력 충돌체·조명 유지). 1920×1080에서 깡패·A+B·C·A+B+C·보스 화면 확인. 전투를 함수로 열고 정지해 촬영했으며 새 전체 런 계측은 아님.
- 저장본 차이는 배경 Transform 7곳·일반 바위 재질 연결 6곳. 적 재질·전투 데이터·보스 배경 파일 해시 유지. 새 셰이더·전투 코드·스킬 연출 변경 없음.
- 근거 폴더: `근거자료/2026-10-07_배경제작/일반맵가시성/`. `10~13`은 변경 전, `20~24`는 최종 화면. `01~04`는 임시 비교안, 안개판 시험은 적용하지 않음. `applied_values.txt` 전후 값, `checks.txt` 검사, `scope_check.txt` 저장 범위 대조.
- [숲 초입](근거자료/2026-10-07_배경제작/일반맵가시성/20_edge_final.png) · [오솔길](근거자료/2026-10-07_배경제작/일반맵가시성/21_forest_final.png) · [동굴 입구 3체](근거자료/2026-10-07_배경제작/일반맵가시성/23_ABC_final.png).
- 검사 후 Play 종료·콘솔 오류/경고0. 문서 정리 중 Play 재진입 확인, 추가 조작하지 않음. 마법사 직행 끔·저장 씬 미저장 표시 없음. 후속 직접 체감·새 빌드는 미확인, 스테이징·커밋 없음.

다음: 수정된 일반 맵에서 적 몸·무기·공격이 잘 구분되는지 직접 확인 후 새 빌드 점검.

'''
replace(names[2],'### D-16 ( )\n',entry+'### D-16 ( )\n')
old='10/7 색 보정: 숲·입구의 갈색을 청록·청회색으로 낮추고 보스 동굴은 더 어둡게 조정. `Environment/Materials`의 배경 4개·소품 7개 `_BaseColor`와 `BattleEnvironmentSetup` 색 배열을 함께 유지. 적 재질·전역 조명은 유지. 전후 값·화면은 [07](07_개발일지.md#d15-background-color).'
new=old+'\n\n후속 일반 맵 가시성은 [10 A45](10_결정사항_로그.md#outdoor-readability-20261007). 일반 바위는 `P04_MossRocks_Outdoor`, 보스 바위는 기존 `P04_MossRocks`. `BattleEnvironmentSetup`의 PropTints·BackplateTints·OutdoorRockTint와 씬 나무 좌표·입구 폭을 같이 유지. 결과는 [07](07_개발일지.md#d15-outdoor-readability).'
replace(names[1],old,new)
replace(names[1],'화면·기능 확인 완료, 직접 플레이·빌드 성능은 미확인.','화면·기능 확인 완료. 가시성 후속 보정의 직접 체감·빌드 성능은 미확인.')
old='5. 위치는 Battle의 `BattleEnvironment` 자식에서 조정. 전투 코드·조명·원본 FBX를 함께 바꾸지 않음. 새 빌드 성능·직접 플레이는 별도 기록.'
new=old+'\n6. [일반 맵 가시성 후속](10_결정사항_로그.md#outdoor-readability-20261007): 깡패부터 3체 전투까지 몸·무기 뒤 나무/바위가 겹치는지 확인. 일반 바위만 Outdoor 재질 사용, 보스 배경은 기존값 유지. 후속 검사·화면은 [07](07_개발일지.md#d15-outdoor-readability).'
replace(names[3],old,new)
old='| 최신 진행·다음 작업 | [07 D-15 클리어](07_개발일지.md#d15-player-clear). 1배속·수동 정지 사용, 보스까지 승리. 오솔길 적 식별 보정·새 빌드 확인 남음 |'
new='| 최신 진행·다음 작업 | [사용자 클리어](07_개발일지.md#d15-player-clear) 후 [일반 맵 가시성 보정](07_개발일지.md#d15-outdoor-readability) 적용. 후속 직접 체감·새 빌드 확인 남음 |'
replace(names[0],old,new)
replace(names[5],'## 진행 상황\n','## 진행 상황\n\n2026-10-07 일반 맵 적 가시성 보정 적용. 원본 10#outdoor-readability-20261007, 변경·화면·검사는 07#d15-outdoor-readability. 숲 초입·오솔길·동굴 입구만 조정, 보스 배경·전투 수치 유지. 다음은 직접 체감·새 빌드 확인. 아래 클리어는 보정 전 기록.\n')
for n in names: assert (docs/n).read_bytes()==before[n],n+' 동시 수정됨'
for n in names: (docs/n).write_text(texts[n],encoding='utf-8',newline='\n')
print('00·06·07·09·10·12 기록 완료')
