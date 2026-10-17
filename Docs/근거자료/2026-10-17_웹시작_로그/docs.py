from pathlib import Path
root=Path(__file__).resolve().parents[3]
def edit(p,fn):
    raw=p.read_bytes(); bom=raw.startswith(b'\xef\xbb\xbf'); crlf=b'\r\n' in raw
    text=raw.decode('utf-8-sig').replace('\r\n','\n'); text=fn(text)
    p.write_bytes((b'\xef\xbb\xbf' if bom else b'')+text.replace('\n','\r\n' if crlf else '\n').encode())
anchor='web-launch-log-20261017'
spec='''<a id="web-launch-log-20261017"></a>
#### 웹 시작 화면·로그 (2026-10-17)

- 타이틀은 게임 시작 화면과 같은 `Scroll Hunter`·Cinzel Bold·금색. 고정 영문 유지. 웹 템플릿에 기존 글꼴과 OFL 동봉.
- 타이틀 아래 여백을 두고 언어 선택과 Play를 한 줄로 배치. 5언어·선택 저장·게임 전달·로딩 중 잠금 유지.
- WebGL 플레이어는 시작할 때 일반 게임 로그를 차단. 보상·계승·편성 기록도 포함. 경고·오류·Assert·예외는 유지하며 브라우저 콘솔 전체를 끄지 않음. 에디터/PC는 기존 로그 규칙 유지.
- 기존 웹1.2 ZIP에는 미포함. 재빌드 후 적용. [검사](07_개발일지.md#web-launch-log-20261017).

'''
edit(root/'Docs/10_결정사항_로그.md',lambda s:s.replace('### 0.1 용어 정의',spec+'### 0.1 용어 정의',1).replace('- 유지하는 일반 로그: 전투 밖의 보상 선택/교체/스킵·계승 저장·편성 순서 저장 기록, 에디터 전용 미리보기 안내.','- 에디터/PC에서 유지하는 일반 로그: 전투 밖의 보상 선택/교체/스킵·계승 저장·편성 순서 저장 기록, 에디터 전용 미리보기 안내. 웹은 [후속 규칙](#web-launch-log-20261017)에 따라 일반 게임 로그 전체 차단.').replace('보상 로그에 획득·스택·적용 후 능력치를 남기고','에디터/PC 보상 로그에 획득·스택·적용 후 능력치를 남기고'))
record='''<a id="web-launch-log-20261017"></a>
### 웹 시작 화면·일반 로그 (후속)

- 타이틀 글꼴/색을 시작 화면과 통일, 언어 선택+Play 한 줄·타이틀 간격 적용. WebGL 시작 시 일반 게임 로그만 필터. 기존 전투 로그 중단 유지. [사양](10_결정사항_로그.md#web-launch-log-20261017).
- Chrome 헤드리스에서 360/640/960/1920 너비×5언어20조합 정렬·잘림·글꼴 확인. 선택 저장/실행 언어·진행률·오류 안내·경고/오류 전달 검사 통과. Unity 로더는 모의 함수 사용, 실제 WebGL 실행 검사는 아님. [화면](근거자료/2026-10-17_웹시작_로그/web-start-960.png)·[결과](근거자료/2026-10-17_웹시작_로그/browser-checks.json).
- Unity 독립 Logger로 일반 로그 거절·Warning/Error/Assert/Exception 허용5종 확인, 에디터 logger 설정 유지. [결과](근거자료/2026-10-17_웹시작_로그/log-filter-checks.txt). 컴파일 오류/경고0. 추가 배치 없음·씬 저장/Play 전환 없음. 새 웹 빌드·업로드·전체 런·커밋 없음.

'''
edit(root/'Docs/07_개발일지.md',lambda s:s.replace('## 최종 제출 체크리스트',record+'## 최종 제출 체크리스트',1).replace('**최신 기록:** ','**최신 기록:** [D-23 웹 시작 화면·로그](#web-launch-log-20261017) · ',1))
edit(root/'Docs/09_프로토타입_제작절차.md',lambda s:s.replace('<a id="cost-resource-checks"></a>', '''<a id="web-launch-log-checks"></a>
## 웹 시작 화면·로그 확인 (2026-10-17)

1. 새 웹 빌드 후 로컬 서버/itch.io에서 열기. 별도 Unity 배치 없음. 기존 ZIP은 자동 갱신되지 않음.
2. 금색 Cinzel 타이틀 아래 언어 목록과 Play가 한 줄인지 확인. 언어5종·640 너비에서도 잘림 없이 표시.
3. 선택 언어로 시작하고 새로고침 후 선택 유지 확인. 개발자 도구 Console에서 전투·보상·계승·편성의 일반 로그가 없는지 확인. 경고·오류는 표시해야 함.
4. ZIP에 `Cinzel-Bold.ttf`·`OFL_Cinzel.txt`도 포함. 기존 7파일 개수로 제한하지 말고 빌드 폴더 전체 비교. [사양](10_결정사항_로그.md#web-launch-log-20261017).

<a id="cost-resource-checks"></a>''',1).replace('보상/편성 로그·경고/오류는 유지.','에디터/PC 보상/편성 로그와 경고/오류는 유지. 웹 일반 로그는 후속 규칙으로 차단.'))
edit(root/'Docs/06_에셋_파이프라인.md',lambda s:s.replace('## 변경 이력','## 변경 이력',1).replace('| 2026-10-17 |','| 2026-10-17 | 웹 시작 화면 | 기존 Cinzel-Bold.ttf·OFL을 웹 템플릿에 동봉, 가로 배치·웹 로그 필터 | [10](10_결정사항_로그.md#web-launch-log-20261017) |\n| 2026-10-17 |',1))
summary='> **2026-10-17 웹 시작 화면·로그:** 게임 시작 화면의 Cinzel Bold 금색 타이틀, 언어 선택/Play 한 줄·간격 적용. WebGL 일반 게임 로그 차단·경고/오류/예외 유지, 에디터/PC 유지. 브라우저20배치·모의 로더/저장/경고·Unity 로그5종 확인. Docs/10#web-launch-log-20261017·07 동일 앵커. 새 웹 빌드 없음·기존 ZIP 미포함.\n\n'
edit(root/'AGENTS.md',lambda s:s.replace('## 현재 단계\n\n','## 현재 단계\n\n'+summary,1))
(root/'CLAUDE.md').write_bytes((root/'AGENTS.md').read_bytes())
print('Documentation updated.')
