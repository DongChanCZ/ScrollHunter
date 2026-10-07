from pathlib import Path
import hashlib
root=Path(r'C:\Users\MBC-501-19\Documents\ScrollHunter')
paths=[root/'AGENTS.md',root/'CLAUDE.md']
raw=paths[0].read_bytes()
assert all(p.read_bytes()==raw for p in paths)
assert hashlib.sha256(raw).hexdigest().upper()=='DB85427CB00AC6DC75B200B23F681056E2FA27A0220AE24C11706AD4ED168D99'
text=raw.decode('utf-8-sig').replace('\r\n','\n')
note='> **2026-10-07 A/S·정보창·총합 피해(Codex / Unity MCP):** A 정지·재개, S 배속, 설명 크기 구분·총합 피해 표시 적용. 원본 `Docs/10#combat-info-controls-20261007`(A22 후속), 검사·화면·한계 `Docs/07#d15-info-controls`, 절차 `Docs/09#combat-info-checks-20261007`. 정보77·가독성33 통과. 실제 키보드 A/S·새 빌드 체감 미확인. Play 종료·Battle 저장·마법사 직행 끔·콘솔 오류/경고0. 전투 수치·판정 유지, 스테이징·커밋 없음.\n\n'
assert text.count('## 현재 단계\n\n')==1
text=text.replace('## 현재 단계\n\n','## 현재 단계\n\n'+note)
old='- 현재 배속·정지 재개·충전 표시·유형 배지는 `Docs/10#combat-readability-20260922`를 따른다.'
assert text.count(old)==1
text=text.replace(old, old+' A/S 단축키·설명 UI·총합 피해는 `Docs/10#combat-info-controls-20261007` 참조.')
newline='\r\n' if b'\r\n' in raw else '\n'
bom=b'\xef\xbb\xbf' if raw.startswith(b'\xef\xbb\xbf') else b''
for p in paths:
    assert p.read_bytes()==raw
    p.write_bytes(bom+text.replace('\n',newline).encode('utf-8'))
    print(p.name)
