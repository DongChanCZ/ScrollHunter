from pathlib import Path
root=Path(__file__).resolve().parents[3]
docs=root/'Docs'
names=['00_문서맵_먼저읽기.md','06_에셋_파이프라인.md','07_개발일지.md','08_코드세션_인계사양.md','09_프로토타입_제작절차.md','10_결정사항_로그.md','12_기획세션_인계프롬프트.md']
paths=[docs/n for n in names]+[root/'AGENTS.md',root/'CLAUDE.md']
before={p:p.read_bytes() for p in paths}
texts={p:b.decode('utf-8-sig').replace('\r\n','\n') for p,b in before.items()}
def rep(p,a,b):
    assert texts[p].count(a)==1,(p,a)
    texts[p]=texts[p].replace(a,b,1)
rep(paths[5],'- 빨강 피격 직전 방어도가 없고 실제 HP가 줄면 카메라를 짧게 흔든다. 방어도가 있다가 이번 공격으로 0이 된 경우·무적·피해 0은 제외. HUD는 흔들지 않는다.',
    '- 빨강 피격 직전 방어도가 없고 실제 HP가 줄면 배경·적·HUD를 함께 짧게 흔든다. 방어도가 있다가 이번 공격으로 0이 된 경우·무적·피해 0은 제외.')
rep(paths[5],'- 흔들림 첫 값: 0.2초·이동 폭 0.06m·주기 24Hz, 점점 약해짐. 전투 중 정지·배속을 따르고, 패배 뒤에는 실제 시간으로 끝낸다. 재시작하면 원위치 복구.',
    '- 흔들림 후속값: 0.24초·1080 높이 기준 축별 최대 16px·24Hz, 점점 약해짐. 해상도에 비례해 이동. 전투 중 정지·배속을 따르고, 패배 뒤에는 실제 시간으로 끝낸다. 재시작하면 원위치 복구. 이전 카메라만 0.2초·0.06m 방식은 대체.\n- 화면 투영·HUD 표시 위치만 이동. 배경은 4% 여유를 두어 흔들릴 때 가장자리가 비지 않게 함. 후속 검사·화면은 [07](07_개발일지.md#d15-screen-shake-circle).')
needle='- 고정 카메라용 배치. 배경 전환은 전투 진행을 읽어 표시만 바꾸며'
rep(paths[5],needle,'- 보스 벽면 마법진은 화면 기준 시계방향 초당 8°(45초에 한 바퀴). 보라색 Additive 발광, 3초 주기로 밝기 ±15%. 페이즈 확대 중에도 이어서 회전. 정지·배속 적용, 새 전투·재시작 시 회전·맥동 초기화. 전역 조명은 유지. 확인은 [07](07_개발일지.md#d15-screen-shake-circle).\n'+needle)
rep(paths[4],'카메라만 흔들리고 HP·카드 UI는 고정되는지 확인.', '배경·적·HP·카드 UI가 함께 흔들리고 끝나면 원위치로 복구되는지 확인. 화면 가장자리 빈틈·게이지 위치 어긋남도 확인.')
rep(paths[4],'카메라 흔들림은 Play 때 자동 연결,', '화면·HUD 흔들림은 Play 때 자동 연결,')
rep(paths[4],'3. 보스 2·3페이즈에서 벽면 마법진 확대, 정지·0.5배속·재시작 복원 확인.', '3. 보스 벽면 마법진의 시계방향 회전·보라 발광과 2·3페이즈 확대 확인. 정지 시 회전·맥동이 멈추고 0.5배속은 절반 속도, 재시작은 초기 각도로 복원.')
rep(paths[1],'전투별 전환·마법진 확대.', '전투별 전환·마법진 확대·회전·보라 발광. Circle Degrees Per Second·Glow Period·Glow로 조절, 재질은 실행 중 PropertyBlock으로 표시하며 공유 원본을 바꾸지 않음.')
rep(paths[1],'2D 배경은 고정 카메라 후면, 화면비에 맞춰 빈틈 없이 확대.', '2D 배경은 고정 카메라 후면, 화면비에 맞춰 확대하고 화면 흔들림용 여유 4% 추가.')
entry='''<a id="d15-screen-shake-circle"></a>
#### 화면 전체 흔들림·보스 마법진 후속 (2026-10-07, Codex)

- 무방어 빨강 피격: 기존 카메라만 작은 이동 → 배경·적·HUD가 같은 화면 거리로 흔들림. 0.24초·1080 높이 기준 축별 최대16px·24Hz, 감쇠. 방어도 보유·이번 타격에 소진·무적·피해0은 제외. 원본 [10 A44](10_결정사항_로그.md#red-hit-defeat-feedback-20261007).
- `RedHitShake`는 화면 투영과 실행 중 HUD 부모만 이동. 카메라 Transform·전투 난수 유지, 정지·배속 적용·패배 후 실제 시간으로 종료·재시작 복구. 화면 끝 빈틈 방지로 2D 배경에 여유4% 추가.
- 보스 벽면 마법진: 시계방향8°/초·45초 한 바퀴, 보라 발광·3초 주기 밝기±15%. 페이즈 확대 중 회전 유지, 정지·배속·재시작 적용. `BattleEnvironment`에서 조절. 원본 [10 A45](10_결정사항_로그.md#backgrounds-20261007). 공유 재질·전역 조명·스킬 연출 유지.
- 검사: 피격/패배23·배경36·HUD75·튜토리얼108 통과. 화면/HUD 같은 픽셀 이동·복구, 회전 방향·맥동·정지·초기화 확인. 기존 검사 2개에 조건 추가.
- 실제 프레임: 회전 1배속60프레임(게임1.2초/9.6°), 정지60프레임(0°), 0.5배속60프레임(게임0.6초/4.8°). 빨강 피격30프레임 중12프레임 흔들림, 최대 HUD 이동13.74·화면과 일치·종료 후 원위치.
- 패배 실제 프레임345개: 디엔드1/0.5배속·매직미사일·우두머리·궁병 통과. 디엔드 대기1.68초 후 결과, 잔여 흔들림 없음.
- 초기 HUD 검사는 마법사 직행으로 단일 적인 상태라 타겟 변경에서 실패. 직행만 끈 뒤에도 비활성 튜토리얼 상태가 남아 HUD 마지막 항목·튜토리얼 검사 실패. 직행 끔·튜토리얼 활성으로 재실행해 통과. 검사 후 기존 직행 설정(켜짐) 복원. 게임 사양을 바꿔 통과시키지 않음.
- 근거: `근거자료/2026-10-07_화면흔들림_마법진/`. [1페이즈](근거자료/2026-10-07_화면흔들림_마법진/01_boss_glow.png)·[3페이즈](근거자료/2026-10-07_화면흔들림_마법진/02_boss_phase3.png)는 상태 지정 후 정지 촬영. 검사 결과는 `검사결과.txt`.
- Play 종료·씬 미저장 표시 없음·최종 콘솔 오류/경고0. 코드4개 변경, 씬·전투 수치·원본 이미지 변경 없음. 직접 손 조작·새 전체 런·새 빌드는 미확인. 스테이징·커밋 없음.

'''
rep(paths[2],'### D-16 ( )\n',entry+'### D-16 ( )\n')
brief='2026-10-07 화면 전체 흔들림·마법진 후속 적용. 빨강 무방어 피격에 배경·적·HUD 동시 흔들림, 보스 마법진 시계방향 회전·보라 발광. 원본10 A44·A45, 검사·화면·한계07#d15-screen-shake-circle. Play 종료·마법사 직행 켜짐(시작 상태 복원), 직접 체감·새 빌드는 미확인. 아래 기록은 당시 상태.\n\n'
rep(paths[3],'## 현재 목표와 확정 제작 범위\n\n','## 현재 목표와 확정 제작 범위\n\n'+brief)
rep(paths[6],'## 진행 상황\n\n','## 진행 상황\n\n'+brief)
rep(paths[0],'| 최신 진행·다음 작업 | [사용자 클리어](07_개발일지.md#d15-player-clear) 후 [일반 맵 가시성 보정](07_개발일지.md#d15-outdoor-readability) 적용. 후속 직접 체감·새 빌드 확인 남음 |',
    '| 최신 진행·다음 작업 | [일반 맵 가시성](07_개발일지.md#d15-outdoor-readability)·[화면 흔들림/마법진](07_개발일지.md#d15-screen-shake-circle) 적용. 직접 체감·새 빌드 확인 남음 |')
handoff='\n> **2026-10-07 화면 전체 흔들림·보스 마법진(Codex / Unity MCP):** 원본 `Docs/10` A44·A45, 변경·화면·한계 `Docs/07#d15-screen-shake-circle`, 절차 `Docs/09#hit-ending-checks`·`#background-checks`. RedHitShake 화면 투영+실행 중 HUD 부모 동시 이동, BattleEnvironment 마법진 시계방향 회전·보라 발광·배경 여유4%. 피격23·배경36·HUD75·튜토리얼108 통과, 실제 회전/흔들림210·패배345프레임 확인. 초기 직행/튜토리얼 비활성 조건으로 실패 후 정상 조건 재검사 통과(07 참조). Play 종료·씬 미저장 표시 없음·콘솔 오류/경고0, 직행은 시작 상태 켜짐으로 복원. 씬·전투 수치·원본 이미지 유지. 직접 체감·새 빌드 미확인, 스테이징·커밋 없음.\n'
assert before[paths[7]]==before[paths[8]],'AGENTS/CLAUDE differs'
for p in paths[7:]:rep(p,'## 현재 단계\n','## 현재 단계\n'+handoff)
for p in paths:assert p.read_bytes()==before[p],str(p)+' 동시 수정됨'
for p in paths:p.write_text(texts[p],encoding='utf-8',newline='\n')
(Path(__file__).parent/'검사결과.txt').write_text('''2026-10-07 화면 전체 흔들림·보스 마법진
Function checks: EndingFeedback 23 / BattleEnvironment 36 / HUD75 / Tutorial108 PASS
Actual frames: rotation 60@1x=9.6deg (1.2 game seconds), 60@pause=0deg, 60@0.5x=4.8deg (0.6 game seconds).
Red hit 30 frames: 12 shaken, peak HUD13.74 reference units, projection/HUD screen displacement matched (<0.08px), restored.
Ending frames: TheEnd1x88 hold1.68s / TheEnd0.5x89 hold1.68s / Fireball98 hold1.90s / Leader35 hold0.64s / Archer35 hold0.64s: PASS.
Earlier HUD/Tutorial failures: mage preview on caused single target; disabled TutorialFlow remained after preview. Preview off + TutorialFlow enabled passed. Failed runs excluded from totals.
Play stopped, scene clean, mage preview restored to true, final console errors/warnings0. Screenshots use forced phase state. No natural full run or build.
''',encoding='utf-8')
print('00·06·07·08·09·10·12·AGENTS·CLAUDE 반영')
