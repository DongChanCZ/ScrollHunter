from pathlib import Path
import re, json
root=Path(__file__).resolve().parents[3]
docs=root/'Docs'
changed=[]
def update(path,fn):
 raw=path.read_bytes();text=raw.decode('utf-8-sig').replace('\r\n','\n');new=fn(text)
 assert new!=text,path
 assert path.read_bytes()==raw,'Concurrent edit: '+str(path)
 path.write_bytes(new.replace('\n','\r\n').encode('utf-8'));changed.append(path.name)
def replace(s,a,b):
 assert s.count(a)==1,(a[:100],s.count(a))
 return s.replace(a,b)
def history(s,row):
 start=re.search(r'^## .*변경 이력',s,re.M).start()
 lines=s[start:].splitlines(True)
 for i,line in enumerate(lines):
  if re.match(r'^\|[ :\-]+\|',line):
   lines.insert(i+1,row+'\n');return s[:start]+''.join(lines)
 raise AssertionError('No history table')

def decisions(s):
 s=replace(s,'- 처치 후 편성을 바로 띄우지 않고 완료 안내를 먼저 표시한다. 게임 시간은 정지.',
 '- 처치 확정 후 게임 시간·입력을 멈추고 쓰러지는 모션을 끝까지 재생한다. 이후 완료 안내를 표시한다. 안내 전 편성은 숨긴다.')
 s=replace(s,'- 사용자 개선 요청 반영. 구현·확인은 [07 D-11](07_개발일지.md#d11-tutorial-completion), 재확인은 [09](09_프로토타입_제작절차.md#tutorial-checks).',
 '- 사용자 개선 요청 반영. 최초 구현은 [07 D-11](07_개발일지.md#d11-tutorial-completion), 10/7 사망 모션 누락 보정은 [07 D-15](07_개발일지.md#d15-tutorial-leader-fix), 재확인은 [09](09_프로토타입_제작절차.md#tutorial-checks).')
 marker='<a id="leader-free-motion-20261006"></a>'
 start=s.index(marker);end=s.index('\n<a id=',start+len(marker))
 block=s[start:end]
 block=replace(block,'왼손 HandIK·손가락 쥠·사망 때 놓기는 유지한다.', '손가락 쥠 유지. 왼손 HandIK는 차단 피격·사망 때 풀고, 복귀 때 다시 적용한다.')
 block+='\n<a id="leader-face-hit-20261007"></a>\n- **10/7 우두머리 보정:** 턱·수염이 목과 가슴에 끌리지 않게 머리 가중치를 보정한다. 메시 형태·텍스처·뼈 구성은 유지. 차단 피격 때 왼손을 손잡이에 강제로 붙이지 않는다. 모션·검·전투 수치는 유지한다. 적용·검사 [07](07_개발일지.md#d15-tutorial-leader-fix), 작업본 [06](06_에셋_파이프라인.md#enemy-model-pipeline).\n'
 return s[:start]+block+s[end:]
update(docs/'10_결정사항_로그.md',decisions)
row='| 2026-10-07 | 튜토리얼 사망·우두머리 | 사망 모션 뒤 완료 안내, 턱 가중치·차단 피격 손 보정. 수치 유지 | [10 A32](10_결정사항_로그.md#tutorial-completion-20260930)·[A40](10_결정사항_로그.md#leader-face-hit-20261007), [07 검사](07_개발일지.md#d15-tutorial-leader-fix) |'
update(docs/'04_적_패턴_기획서.md',lambda s:history(replace(s,'처치 확정 → 완료 안내·확인 → 편성 학습 7','처치 확정 → 사망 모션 → 완료 안내·확인 → 편성 학습 7'),row))
def assets(s):
 marker='<a id="enemy-model-pipeline"></a>'
 i=s.index('\n',s.index('\n',s.index(marker))+1)+1
 s=s[:i]+'\n- **10/7 우두머리 후속:** 턱 가중치 작업본은 `근거자료/2026-10-07_튜토리얼사망_우두머리얼굴/Leader_face_weights.fbx`. Unity 동일 FBX·기존 meta 유지. 원본 10/2 blend·FBX 보존, 텍스처는 기존 것 사용. `fix_leader_weights.py`로 재생성. 원본 [10 A40](10_결정사항_로그.md#leader-face-hit-20261007), 검사 [07](07_개발일지.md#d15-tutorial-leader-fix).\n'+s[i:]
 return history(s,'| 2026-10-07 | 우두머리 보정 | 턱 가중치 작업본·차단 피격 보정 연결 | [10 A40](10_결정사항_로그.md#leader-face-hit-20261007), [07](07_개발일지.md#d15-tutorial-leader-fix) |')
update(docs/'06_에셋_파이프라인.md',assets)
record='''<a id="d15-tutorial-leader-fix"></a>
#### 튜토리얼 사망·우두머리 턱과 피격 보정 (2026-10-07, Codex)

- 사용자 발견: 튜토리얼 적의 쓰러짐이 안 보임, 우두머리 하관 비틀림, 차단 피격 때 팔이 몸 안으로 들어감.
- 튜토리얼은 적을 바로 숨기던 처리를 사망 모션 뒤로 이동. 기존 승리 연출 대기를 공유하고 이후 완료 안내를 연다. 스킵은 즉시 편성, 재시작은 이전 연출 취소. 원본 [10 A32](10_결정사항_로그.md#tutorial-completion-20260930).
- 우두머리 턱·수염의 목/가슴 가중치를 머리 쪽으로 보정(원본 메시 236개 정점). 형태·UV·텍스처·뼈 구성 유지, 기존 원본 보존. Unity FBX만 작업본으로 교체하고 meta 유지.
- 차단 피격 때 왼손 HandIK를 풀고 복귀 때 다시 적용. 팔이 손잡이를 따라 몸통을 가로지르던 원인 보정. 프리팹·빌더 동기화, 기존 피격 모션·대검·전투 수치 유지. 원본 [10 A40](10_결정사항_로그.md#leader-face-hit-20261007).
- 함수 검사: 튜토리얼108·사망 전환44·타이밍359·시선225·연결188·모델68 통과. 재시작·스킵·입력 차단·양손 보정 복귀 포함.
- 실제 프레임: 튜토리얼 사망 대기 2.7초, 1배속/0.5배속 처치 각각136프레임. 대기 중 모션 표시·입력 금지, 종료 후 안내·모델 정리 확인. 우두머리 초록 차단 성공 2회, 피격 중 손 보정0·복귀 후1 확인.
- 검사 보완: 모델 검사는 예전 원본 해시를 비교해 실패→새 작업본 경로로 정정. 연결 검사는 오브 도입 전 시전 기대값으로 실패→현재 시전 시작 스냅샷의 오브 배율 반영. 시선 검사 직후 연결 검사도 준비 자세에서 실패했으며 새 Play 단독 재실행은 통과. 초기 프레임 시도는 시작 초기화 전 호출·비활성 창 진행 문제로 제외, 새 Play 초기화 후 프레임 진행으로 다시 확인했다.
- 근거: `근거자료/2026-10-07_튜토리얼사망_우두머리얼굴/`. [턱 전](근거자료/2026-10-07_튜토리얼사망_우두머리얼굴/01_leader_face_before.png)·[후](근거자료/2026-10-07_튜토리얼사망_우두머리얼굴/02_leader_face_after.png)는 확대 자세 검사. [쓰러짐](근거자료/2026-10-07_튜토리얼사망_우두머리얼굴/tutorial_falling_mid.png)·[차단 피격](근거자료/2026-10-07_튜토리얼사망_우두머리얼굴/leader_interrupt_mid.png)은 실제 Game 뷰. 전체 런·직접 손 조작·새 빌드 체감은 미확인.
- Play 종료·씬 미저장 표시 없음·마법사 직행 끔(시작 상태 유지)·콘솔 오류/경고0. Battle 씬·전투 수치·다른 적 모델 유지. 문서·AGENTS/CLAUDE 갱신, 스테이징·커밋 없음.

'''
update(docs/'07_개발일지.md',lambda s:replace(s,'### D-16 ( )',record+'### D-16 ( )'))
def procedure(s):
 s=replace(s,'5. 적 처치 → 완료 안내. 편성은 숨겨지고 기다려도 전환되지 않아야 한다.',
 '5. 적 처치 → 쓰러짐 끝까지 재생 → 완료 안내. 사망 모션 중 카드·포션·안내 넘김 불가. 편성은 숨겨지고 완료 안내에서 기다려도 전환되지 않아야 한다.')
 marker='## 적 모델 Unity 적용·동작 확인 (2026-10-02)\n'
 s=replace(s,marker,marker+'\n10/7 후속: 우두머리 대기·시전에서 턱이 얼굴과 함께 도는지 확인. 초록 차단 피격 중 왼손이 몸통을 가로지르지 않고, 복귀 때 다시 검을 잡아야 한다. `Check Enemy Death Ending (Play)`는 튜토리얼 사망·스킵·도중 재시작 포함. 작업본·검사 한계는 [07](07_개발일지.md#d15-tutorial-leader-fix).\n')
 return s
update(docs/'09_프로토타입_제작절차.md',procedure)
update(docs/'00_문서맵_먼저읽기.md',lambda s:replace(s,'| 최신 진행·다음 작업 |', '| 튜토리얼 사망·우두머리 턱/피격 | [10 A32](10_결정사항_로그.md#tutorial-completion-20260930)·[A40](10_결정사항_로그.md#leader-face-hit-20261007), [07 수정·검사](07_개발일지.md#d15-tutorial-leader-fix). 직접 체감·새 빌드 확인 남음 |\n| 최신 진행·다음 작업 |'))
handoff='2026-10-07 튜토리얼 사망·우두머리 턱/차단 피격 보정 적용. 원본10 A32·A40, 변경·검사·한계07#d15-tutorial-leader-fix. Play 종료·씬 미저장 표시 없음·마법사 직행 끔(이번 시작 상태 유지). 직접 체감·새 빌드는 미확인. 아래 기록은 당시 상태.\n\n'
for name in ['08_코드세션_인계사양.md','12_기획세션_인계프롬프트.md']:
 update(docs/name,lambda s:replace(s,'2026-10-07 화면 전체 흔들림·마법진 후속 적용.',handoff+'2026-10-07 화면 전체 흔들림·마법진 후속 적용.'))
note='> **2026-10-07 튜토리얼 사망·우두머리 턱/차단 피격(Codex / Unity MCP):** 원본 `Docs/10` A32·A40, 변경·검사·화면·한계 `Docs/07#d15-tutorial-leader-fix`, 절차 `Docs/09#tutorial-checks`·`#enemy-model-unity-checks`. 튜토리얼 사망 뒤 완료 안내, 우두머리 턱 가중치 작업본·피격 중 왼손 보정 해제 적용. 튜토리얼108·사망44·타이밍359·시선225·연결188·모델68 통과, 실제 튜토리얼 사망2회·우두머리 차단2회 확인. 검사 구 기대값·연속 실행 실패/재확인은 07 참조. Play 종료·씬 미저장 표시 없음·직행 끔(시작 상태 유지)·콘솔 오류/경고0. 전투 수치·Battle 씬·다른 적 모델 유지. 직접 체감·새 빌드 미확인, 스테이징·커밋 없음.\n\n'
assert (root/'AGENTS.md').read_bytes()==(root/'CLAUDE.md').read_bytes()
for name in ['AGENTS.md','CLAUDE.md']:
 update(root/name,lambda s:replace(s,'## 현재 단계\n\n','## 현재 단계\n\n'+note))
assert (root/'AGENTS.md').read_bytes()==(root/'CLAUDE.md').read_bytes()
print(json.dumps(changed,ensure_ascii=False))
