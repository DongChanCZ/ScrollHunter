import json
from pathlib import Path

root = Path(__file__).resolve().parents[3]
out = Path(__file__).parent
old = json.loads((root / 'Docs/근거자료/2026-10-16_웹빌드_1.2/current-values.json').read_text(encoding='utf-8-sig'))
new = {x['path']: x['data'] for x in json.loads((out / 'current-values.json').read_text(encoding='utf-8-sig'))}
count = 0
diffs = []

def walk(a, b, path):
    global count
    if isinstance(a, dict):
        if any(k in a for k in ('instanceID', 'fileID')):
            return
        for k, v in a.items():
            if k.startswith('m_'):
                continue
            walk(v, b.get(k) if isinstance(b, dict) else None, path + '.' + k)
    elif isinstance(a, list):
        if not isinstance(b, list) or len(a) != len(b):
            diffs.append({'path': path, 'issue': 'array length differs'})
            return
        for i, v in enumerate(a):
            walk(v, b[i], f'{path}[{i}]')
    elif isinstance(a, (int, float)) and not isinstance(a, bool):
        count += 1
        if not isinstance(b, (int, float)) or abs(a - b) > 1e-5:
            diffs.append({'path': path, 'old': a, 'current': b})

for row in old:
    walk(row['data'], new.get(row['path']), row['path'])
report = {'baseline': '2026-10-16 final Unity data snapshot', 'assets': len(old), 'numeric_fields': count, 'differences': diffs}
(out / 'numeric-comparison.json').write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding='utf-8')
print(json.dumps(report, ensure_ascii=False))
