from pathlib import Path
import json,hashlib
p=Path(__file__).parent/'zip-checks.json'
r=json.loads(p.read_text(encoding='utf8')); old=Path(r['zip']); target=old.parent/'ScrollHunter_Web_1.3.zip'
assert old.name=='ScrollHunter_Web_1.zip' and not target.exists()
assert hashlib.sha256(old.read_bytes()).hexdigest()==r['sha256']
old.rename(target);r['zip']=str(target)
p.write_text(json.dumps(r,indent=2),encoding='utf8')
print(target)
