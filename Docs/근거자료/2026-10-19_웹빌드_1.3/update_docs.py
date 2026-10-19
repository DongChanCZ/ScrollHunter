from pathlib import Path
import json

e=Path(__file__).resolve().parent
docs=e.parents[1]
root=docs.parent
b=json.loads((e/'build-result.json').read_text(encoding='utf8'))
z=json.loads((e/'zip-checks.json').read_text(encoding='utf8'))
browser=json.loads((e/'browser-result.json').read_text(encoding='utf8'))
preserved=json.loads((e/'preservation.json').read_text(encoding='utf8'))
assert b['result']=='Succeeded' and b['errors']==0
assert browser['loaded'] and not browser['errors'] and not any(x['type']=='error' for x in browser['logs'])
assert all(x['unchanged'] for x in preserved)
tag='web-1-3-rebuild-20261019'

def edit(path,old,new):
    content=path.read_text(encoding='utf8')
    assert content.count(old)==1,(path,old)
    path.write_text(content.replace(old,new),encoding='utf8')

status=f'> **2026-10-19 웹1.3 재빌드:** 최신 HUD 외곽선 포함 Desktop/ScrollHunter_Web_1.3·ZIP 교체. 일반 WebGL 빌드 성공·오류{b["errors"]}·경고{b["warnings"]}, ZIP{len(z["files"])}파일 무결성/해시 일치·Chrome 영어 시작 확인. 기존1.3은 TEMP/ScrollHunter_Web13_20261019 보관. 씬/폰트/사용자 저장{len(preserved)}파일 유지·Play 종료·씬 저장 상태 유지. Docs/10#web-1-3-20261017·07#{tag}. 전체 런·itch.io 업로드 없음. 아래 미빌드는 당시 기록.\n\n'
assert (root/'AGENTS.md').read_bytes()==(root/'CLAUDE.md').read_bytes()
for name in ('AGENTS.md','CLAUDE.md'):
    edit(root/name,'## 현재 단계\n\n','## 현재 단계\n\n'+status)

edit(docs/'10_결정사항_로그.md',
     '- 내부 버전1.3, 출력 `Desktop/ScrollHunter_Web_1.3`·`ScrollHunter_Web_1.3.zip`. 기존1.1/1.2 보존.',
     '- 내부 버전1.3, 출력 `Desktop/ScrollHunter_Web_1.3`·`ScrollHunter_Web_1.3.zip`. 기존1.1/1.2 보존.\n- 10/19 최신 HUD 글자 외곽선(최종 두께·정지/배속 버튼 내부 문구 제외)을 포함해 같은 경로로 재빌드·ZIP 교체. 직전1.3은 TEMP/ScrollHunter_Web13_20261019에 백업. [재빌드 결과](07_개발일지.md#'+tag+').')
edit(docs/'09_프로토타입_제작절차.md',
     '크기·무결성·빌드 결과는 [07](07_개발일지.md#web-1-3-20261017).',
     '10/19 HUD 외곽선 수정까지 포함한 최신 재빌드의 크기·무결성·결과는 [07](07_개발일지.md#'+tag+').')
entry=f'''### 웹1.3 재빌드 — HUD 외곽선 포함·바탕화면 ZIP 교체

<a id="{tag}"></a>

- 최신 저장 씬과 소스로 일반 WebGL 빌드. 아이콘·물약·코스트·A/S 외곽선 최종 수정 포함, 내부 버전1.3·영어 기본·5언어·시작 전 언어 선택·일반 게임 로그 필터 유지. 게임 수치/트레일러 촬영 코드 변경 없음.
- 빌드 성공 {b['seconds']:.2f}초·오류{b['errors']}·경고{b['warnings']}. 작업 전 콘솔의 Gmarket 폰트 반입 경고1건은 빌드 결과와 별도. [빌드 결과](근거자료/2026-10-19_웹빌드_1.3/build-result.json).
- 바탕화면 `ScrollHunter_Web_1.3`와 ZIP 갱신. ZIP {z['bytes']:,}바이트·{len(z['files'])}파일, `index.html` 최상위·슬래시 경로·CRC 무결성 및 압축 전후 파일 해시 일치. 직전 폴더는 TEMP/ScrollHunter_Web13_20261019/PreviousBuild, ZIP은 같은 임시 폴더에 보관. [ZIP 검사](근거자료/2026-10-19_웹빌드_1.3/zip-checks.json) · [이전본](근거자료/2026-10-19_웹빌드_1.3/previous-build.json).
- 새 Chrome 브라우저 저장 환경에서 영어 기본 언어·웹 로더·게임 시작 화면 초기화 확인, 브라우저 오류0. [실행 검사](근거자료/2026-10-19_웹빌드_1.3/browser-result.json) · [시작 화면](근거자료/2026-10-19_웹빌드_1.3/web13-title.png).
- 빌드가 다시 저장한 폰트는 이번 작업 직전 사본으로 복원. 씬·폰트·렌더 설정·소스·사용자 저장 {len(preserved)}파일 해시 유지. Play 종료·씬 저장 상태 유지. [보존 검사](근거자료/2026-10-19_웹빌드_1.3/preservation.json). 직접 전체 런·itch.io 재업로드·커밋 미실시.

'''
edit(docs/'07_개발일지.md','## 최종 제출 체크리스트',entry+'## 최종 제출 체크리스트')
edit(docs/'07_개발일지.md','**최신 기록:** [웹1.3](#web-1-3-20261017)',f'**최신 기록:** [10/19 웹1.3 재빌드](#{tag}) · [웹1.3](#web-1-3-20261017)')
print('Updated build record, distribution specification, build instructions, and matching AGENTS/CLAUDE status.')
