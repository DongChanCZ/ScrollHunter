from pathlib import Path
import hashlib

root = Path(r'C:\Users\MBC-501-19\Documents\ScrollHunter')
paths = [root/'AGENTS.md', root/'CLAUDE.md']
note = '> **2026-10-07 맵 이름(Codex / Unity MCP):** 상단 표시를 4종 맵 이름으로 변경. 원본 `Docs/10#backgrounds-20261007`(A45), 이름·재시작 6상태 검사와 화면은 `Docs/07#d15-map-names`, 확인은 `Docs/09#background-checks`. BattleFlow·Battle 저장. 전투 구성·계측 번호·배경 유지, Play 종료·콘솔 오류/경고0. 전체 런·새 빌드 미실시.\n\n'
prepared = []
for p in paths:
    raw = p.read_bytes()
    assert hashlib.sha256(raw).hexdigest().upper() == 'F3B5595E1DC7248EA700B162A5538BE957518278D363BF681B4450A3704663CC', 'Concurrent edit'
    text = raw.decode('utf-8-sig').replace('\r\n','\n')
    marker = '## 현재 단계\n\n'
    assert text.count(marker) == 1
    text = text.replace(marker, marker + note)
    newline = '\r\n' if b'\r\n' in raw else '\n'
    bom = b'\xef\xbb\xbf' if raw.startswith(b'\xef\xbb\xbf') else b''
    prepared.append((p, raw, bom+text.replace('\n',newline).encode('utf-8')))
for p, raw, updated in prepared:
    assert p.read_bytes() == raw
for p, raw, updated in prepared:
    p.write_bytes(updated)
    print(p.name)
