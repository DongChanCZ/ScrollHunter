from pathlib import Path
root=Path(__file__).resolve().parents[3]
def edit(rel,fn):
    p=root/rel; raw=p.read_bytes(); s=raw.decode('utf-8-sig').replace('\r\n','\n'); t=fn(s)
    assert t!=s,rel
    p.write_bytes((b'\xef\xbb\xbf' if raw.startswith(b'\xef\xbb\xbf') else b'')+t.replace('\n','\r\n' if b'\r\n' in raw else '\n').encode('utf-8'))
def spec(s):
    s=s.replace('저장된 **스테이지 승리 기록**(`save.json`의 기존 `clearedBattles`, 본편 전투 승리 시 기록)이 하나 이상이면 조작 가능. 재도전 조건은 삭제.', '**재도전 시작 기록 또는 본편 전투 승리 기록** 중 하나라도 있으면 조작 가능(`hasRetried || clearedBattles != 0`). 승리 기록만 요구하던10/17 초기 변경을 후속 요청으로 보완.')
    s=s.replace('승리 기록이 없으면 재도전 기록만 있어도 비활성.', '승리 없이 재도전한 저장도 활성. 두 기록이 모두 없을 때만 비활성.')
    s=s.replace('이번 실행에서 승리 기록이 생기면 재실행 없이 활성. 게임 초기화로 승리 기록을 지우면 다시 비활성. 계승 초기화는 승리 기록 유지.', '이번 실행에서 재도전을 시작하거나 승리하면 재실행 없이 활성. 재도전 확인창 취소만으로는 활성화하지 않음. 게임 초기화는 두 기록 삭제·다시 잠금, 계승 초기화는 두 기록 유지.')
    s=s.replace('“스테이지에서 승리하면 변경할 수 있습니다.”', '“재도전 또는 전투 승리 후 변경할 수 있습니다.”')
    s=s.replace('`hasRetried`는 삭제하지 않고 재도전 기록·진행 여부 판단용으로 유지.', '`hasRetried`와 `clearedBattles`를 함께 활용하며 저장 형식 유지.')
    s=s.replace('[스테이지 승리 기록 기준](#dialogue-option-unlock-20261017)', '[재도전 또는 승리 기록 기준](#dialogue-option-unlock-20261017)')
    s=s.replace('검사·한계 [07](07_개발일지.md#dialogue-option-log-20261017)', '최종 조건 검사·한계 [07](07_개발일지.md#dialogue-option-either-20261017)')
    s=s.replace('| 2026-10-17 | 대화 스킵 옵션 |', '| 2026-10-17 후속 | 대화 스킵 옵션 | 재도전 시작 또는 본편 승리 중 하나면 활성. 재도전 창과 설정 공유 | [현행 사양](#dialogue-option-unlock-20261017) |\n| 2026-10-17 | 대화 스킵 옵션 |',1)
    return s
edit('Docs/10_결정사항_로그.md',spec)
for name in ['AGENTS.md','CLAUDE.md']:
    edit(name,lambda s:s.replace('## 현재 단계\n\n','## 현재 단계\n\n> **2026-10-17 대화 옵션 후속:** 옵션 체크박스는 재도전 시작 또는 본편 승리 기록 중 하나면 활성. 재도전 확인창은 기존처럼 조절 가능·같은 설정 공유. 두 기록 없는 새 게임만 잠금. 안내5언어·옵션207/재도전85·언어70,453 검사 통과. Docs/10#dialogue-option-unlock-20261017·07#dialogue-option-either-20261017. 새 웹 빌드 없음. 아래 승리만 기준은 후속 전 기록.\n\n',1))
for name in ['08_코드세션_인계사양.md','12_기획세션_인계프롬프트.md']:
    edit('Docs/'+name,lambda s:s.replace('대화 스킵 옵션은 본편 전투 승리 기록이 있으면 활성.', '대화 스킵 옵션은 재도전 시작 또는 본편 전투 승리 기록 중 하나면 활성.',1))
edit('README.md',lambda s:s.replace('승리 기록 기준 대화 옵션','재도전 또는 승리 기록 기준 대화 옵션'))
edit('Docs/00_문서맵_먼저읽기.md',lambda s:s.replace('저장된 스테이지 승리 기록이 있으면 체크박스 활성','재도전 시작 또는 본편 승리 기록 중 하나면 체크박스 활성'))
edit('Docs/05_VFX_연출_규칙.md',lambda s:s.replace('[승리 기록 기준]','[재도전 또는 승리 기록 기준]'))
edit('Docs/11_계승_시스템_기획서.md',lambda s:s.replace('저장된 스테이지 승리 기록이 있으면 조작 가능(당시 10-16에는 첫 재도전 확정 후).','재도전 시작 또는 본편 승리 기록 중 하나면 조작 가능.').replace('재도전 기록(`hasRetried`)은 저장만 유지.', '재도전 기록(`hasRetried`)도 옵션 활성화에 사용.'))
def checks(s):
    s=s.replace('새 저장(승리 기록 없음)', '새 저장(재도전·승리 기록 모두 없음)').replace('“스테이지에서 승리하면 변경할 수 있습니다.” 안내가 보이는지 확인. 패배→다시 도전만 해서는 계속 잠김.', '“재도전 또는 전투 승리 후 변경할 수 있습니다.” 안내가 보이는지 확인. 패배→재도전을 실제 시작하면 승리 없이도 활성. 재도전 확인창 취소는 잠금 유지.')
    s=s.replace('기존 승리 기록이 있는 저장은 처음부터 조작 가능.', '기존 재도전 또는 승리 기록이 있는 저장은 처음부터 조작 가능.')
    return s
edit('Docs/09_프로토타입_제작절차.md',checks)
print('Documentation synchronized.')
