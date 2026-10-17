from pathlib import Path
import json
folder=Path(__file__).parent; root=folder.parents[2]
b=json.loads((folder/'build-result.json').read_text(encoding='utf-8-sig'))
z=json.loads((folder/'zip-checks.json').read_text(encoding='utf8'))
browser=json.loads((folder/'browser-result.json').read_text(encoding='utf8'))
assert b['result']=='Succeeded' and browser['loaded']
def edit(p,fn):
    raw=p.read_bytes(); bom=raw.startswith(b'\xef\xbb\xbf'); crlf=b'\r\n' in raw
    s=fn(raw.decode('utf-8-sig').replace('\r\n','\n'))
    p.write_bytes((b'\xef\xbb\xbf' if bom else b'')+s.replace('\n','\r\n' if crlf else '\n').encode())
spec='''<a id="web-1-3-20261017"></a>
#### 웹1.3 배포본 (2026-10-17)

- 내부 버전1.3, 출력 `Desktop/ScrollHunter_Web_1.3`·`ScrollHunter_Web_1.3.zip`. 기존1.1/1.2 보존.
- 현재 코스트 보석/정수/바운스·최대치, 재도전 또는 승리 기록 기준 대화 옵션, 일반 전투 로그 중단 포함. 웹 타이틀/가로 언어·Play와 웹 일반 로그 필터도 포함.
- 영어 기본·시작 전5언어·기존 플레이/저장 규칙 유지. 새 ZIP을 itch.io에 업로드해야 배포에 반영. [결과](07_개발일지.md#web-1-3-20261017)·[확인](09_프로토타입_제작절차.md#web-language-20261014).

'''
edit(root/'Docs/10_결정사항_로그.md',lambda s:s.replace('### 0.1 용어 정의',spec+'### 0.1 용어 정의',1).replace('- 기존 웹1.2 ZIP에는 미포함. 재빌드 후 적용. [검사]','- 웹1.3에 포함. 기존 웹1.2는 보존. [검사]',1).replace('| 2026-10-17 | 웹 시작 화면·로그 |','| 2026-10-17 | 웹1.3 | 바탕화면 웹 폴더·ZIP, 최신 소스 포함·기존1.2 보존 | [사양](#web-1-3-20261017) |\n| 2026-10-17 | 웹 시작 화면·로그 |',1))
record=f'''<a id="web-1-3-20261017"></a>
### 웹1.3 빌드·압축

- 바탕화면 `ScrollHunter_Web_1.3`·ZIP 생성. 내부1.3·최신 소스 포함, 기존1.2 유지. [사양](10_결정사항_로그.md#web-1-3-20261017).
- WebGL 일반 빌드 성공{b['seconds']:.2f}초·오류{b['errors']}·경고{b['warnings']}. ZIP {z['bytes']:,}바이트·{len(z['files'])}파일, 최상위index.html·`/`경로·무결성/파일 해시 일치. Cinzel 글꼴·OFL 동봉. [빌드](근거자료/2026-10-17_웹빌드_1.3/build-result.json)·[ZIP](근거자료/2026-10-17_웹빌드_1.3/zip-checks.json).
- 로컬 서버+Chrome 헤드리스 실제 WebGL 초기화·영어 기본·시작 화면 확인. 브라우저 오류0. 일반 로그 필터가 IL2CPP 변환물에 포함된 것 확인. [시작 전](근거자료/2026-10-17_웹빌드_1.3/web13-launch.png)·[게임](근거자료/2026-10-17_웹빌드_1.3/web13-title.png)·[로그](근거자료/2026-10-17_웹빌드_1.3/browser-result.json).
- 미저장 씬은 임시 사본 보관, 저장본과 차이는 현재 기본값과 일치. 빌드 중 저장 창 반복을 피하도록 미저장 표시만 잠시 해제 후 복구. 빌드가 다시 저장한 폰트/렌더 설정만 작업 직전 사본으로 복원. 씬·사용자 저장 유지, Play 종료. [보존](근거자료/2026-10-17_웹빌드_1.3/preservation.json).
- 폴더를 웹 서버로 실행하거나 ZIP을 itch.io HTML 게임으로 업로드해 확인. 수동 전체 런·itch.io 업로드·다른 PC 검사는 하지 않음. 스테이징·커밋 없음.

'''
edit(root/'Docs/07_개발일지.md',lambda s:s.replace('## 최종 제출 체크리스트',record+'## 최종 제출 체크리스트',1).replace('**최신 기록:** ','**최신 기록:** [웹1.3](#web-1-3-20261017) · ',1))
edit(root/'Docs/09_프로토타입_제작절차.md',lambda s:s.replace('최신 파일: 바탕화면 **ScrollHunter_Web_1.2.zip**. index.html이 ZIP 최상위에 있음. 기존1.1 폴더·ZIP은 보존. 크기·무결성·빌드 결과는 [07](07_개발일지.md#web-1-2-20261016).','최신 파일: 바탕화면 **ScrollHunter_Web_1.3.zip**. index.html이 ZIP 최상위에 있음. 기존1.1/1.2 폴더·ZIP은 보존. 크기·무결성·빌드 결과는 [07](07_개발일지.md#web-1-3-20261017).').replace('2. Play 종료 후 `Tools > Scroll Hunter > Build Web 1.2`. 바탕화면 `ScrollHunter_Web_1.2`에 생성.','2. Play 종료 후 `Tools > Scroll Hunter > Build Web 1.3`. 바탕화면 `ScrollHunter_Web_1.3`에 생성.').replace('10/16 카드 비교/설명·','10/17 현재 코스트·대화 옵션 조건 통합·웹 타이틀/가로 배치·일반 로그 필터와10/16 카드 비교/설명·',1).replace('[최신 빌드](07_개발일지.md#web-1-2-20261016)','[최신 빌드](07_개발일지.md#web-1-3-20261017)'))
edit(root/'README.md',lambda s:s.replace('Docs/07_개발일지.md#web-1-2-20261016','Docs/07_개발일지.md#web-1-3-20261017').replace('- 소스에는 현재 코스트 보석·재도전 또는 승리 기록 기준 대화 옵션·전투 로그 중단 적용. 기존 웹1.2 ZIP에는 미포함, 재빌드 필요.','- 웹1.3에 현재 코스트 보석·대화 옵션 조건 통합·웹 시작 화면 개선·일반 게임 로그 차단 포함. 바탕화면 `ScrollHunter_Web_1.3.zip`, 기존1.2 보존.'))
edit(root/'Docs/00_문서맵_먼저읽기.md',lambda s:s.replace('. 웹1.2에는 미포함','. 웹1.3에 포함').replace('| 10/16 최종·웹1.2 |','| 최신 웹1.3 | [07 빌드](07_개발일지.md#web-1-3-20261017)·[10 사양](10_결정사항_로그.md#web-1-3-20261017). 10/17 최신 소스 포함·기존1.2 보존 |\n| 10/16 최종·웹1.2 |',1))
for name in ['08_코드세션_인계사양.md','12_기획세션_인계프롬프트.md']:
    edit(root/'Docs'/name,lambda s:s.replace('기존 웹1.2는10/16 빌드여서10/17 변경은 미포함.','후속 웹1.3에10/17 변경 포함([07 빌드](07_개발일지.md#web-1-3-20261017)), 기존1.2 보존.',1))
summary=f'> **2026-10-17 웹1.3:** Desktop/ScrollHunter_Web_1.3·ZIP 생성, 내부1.3·최신 코스트/대화 옵션/웹 시작 화면/일반 로그 필터 포함. 빌드 오류{b["errors"]}·경고{b["warnings"]}, ZIP{len(z["files"])}파일 무결성/해시 일치·Chrome 초기화 확인. Docs/10#web-1-3-20261017·07 동일 앵커. 기존1.2·씬/사용자 저장 유지, Play 종료·기존 씬 미저장 표시 복구. 전체 런·itch.io 업로드 없음. 아래 미빌드는 당시 기록.\n\n'
edit(root/'AGENTS.md',lambda s:s.replace('## 현재 단계\n\n','## 현재 단계\n\n'+summary,1))
(root/'CLAUDE.md').write_bytes((root/'AGENTS.md').read_bytes())
print('Web 1.3 documentation synchronized.')
