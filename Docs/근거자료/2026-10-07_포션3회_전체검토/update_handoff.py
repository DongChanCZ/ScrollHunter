from pathlib import Path
root=Path(__file__).resolve().parents[3]
a=root/'AGENTS.md';c=root/'CLAUDE.md'
ab=a.read_bytes();cb=c.read_bytes();assert ab==cb,'Handoff files differ; inspect before writing'
s=ab.decode('utf-8-sig').replace('\r\n','\n')
old='- 회복 포션: **런 시작 2/2**(10 A35)'
assert old in s;s=s.replace(old,'- 회복 포션: **런 시작 3/3**(10 A46)')
old='- 손익분기 **적 2체.** 같은 코스트 기준 광역 피해량 = 단일 피해량 × 0.5\n- 광역기의 대가는 코스트가 아니라 **캐스팅 시간** (0.8~1.2초)\n- 광역기 코스트를 올리면 피해량도 비례해 올릴 것'
assert old in s;s=s.replace(old,'- 적 2체·피해 절반은 초기 비교 기준. 현재 수치는 10 A20·A23·A29와 03 SK 표 적용.\n- 보스는 본체 1체+오브 최대 2체. 대상 수·코스트·시전 시간을 함께 비교(10 A43).')
old='## 현재 단계\n';assert old in s
entry='''
> **2026-10-07 포션 3회·문서 대조(Codex / Unity MCP):** 사용자 판단은 보스 난이도 상승에 따른 생존 여유 확보. 원본 `Docs/10#potion-three-20261007`(A46), `Player` 기본값·Battle 포션 3/3 저장. 회복 30%·PS01 최대치/현재 +1 유지. HUD75·보스315·전투 회귀756·튜토리얼108·오브79 통과. 튜토리얼 함수 검사는 결과 연출 대기를 검사 중에만 제외·복원하도록 보완. 실제 1배속 자동 입력 3런 모두 A+B+C 승리 후 보스 패배, 포션 3/4/3회·오류0. 사람 플레이·승률·밸런스 확정과 구분. 변경표·조건·한계 `Docs/07#d15-potion-audit`, 절차 `Docs/09#potion-three-checks`. 00~12·엑셀 7시트 대조·현행 표기 정정, 과거 실측 보존. Play 종료·씬 미저장 표시 없음·마법사 직행 끔·최종 콘솔 오류/경고0. 새 빌드·스테이징·커밋 없음.

> **2026-10-07 배경 색 후속(Codex):** 숲·입구는 청록/청회색, 보스 동굴 배경은 더 어둡게. 배경4·소품7 재질과 `BattleEnvironmentSetup` 색 배열 동기화. 적 재질14·전역 조명·카메라·마법진·UI 유지. 배경31 통과·화면 확인, 직접 체감·새 빌드는 별도. 원본 `Docs/10#backgrounds-20261007`(A45), 기록 `Docs/07#d15-background-color`, 조정 `Docs/06#background-assets-20261007`.
'''
s=s.replace(old,old+entry,1)
assert a.read_bytes()==ab and c.read_bytes()==cb
a.write_text(s,encoding='utf-8',newline='\n');c.write_text(s,encoding='utf-8',newline='\n')
print('AGENTS.md and CLAUDE.md updated identically')
