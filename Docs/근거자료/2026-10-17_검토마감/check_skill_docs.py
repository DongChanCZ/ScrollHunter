import json
import re
from pathlib import Path

root = Path(__file__).resolve().parents[3]
folder = Path(__file__).parent
data = json.loads((folder/'current-values.json').read_text(encoding='utf-8-sig'))
skills = {Path(r['path']).stem: r['data']['MonoBehaviour'] for r in data if '/SkillData/Skill_' in r['path']}
text = (root/'Docs/03_스킬_기획서.md').read_text(encoding='utf-8-sig')
blocks = re.split(r'^### SK(\d{2})[^\n]*\n', text, flags=re.M)
checks = []
for i in range(1, len(blocks), 2):
    sid, block = blocks[i], blocks[i+1].split('\n## ')[0]
    if sid == '11':
        asset = skills['Skill_Silence']
    elif sid == '13':
        asset = skills['Skill_CrystalShield']
    else:
        asset = next(v for k, v in skills.items() if k.startswith('Skill_SK'+sid+'_'))
    for label, field in [('코스트','cost'), ('시전 / 채널링 시간','castTime'), ('피해량','damage'), ('타수','hitCount'), ('방어도 부여량','shieldAmount')]:
        m = re.search(r'^\| '+re.escape(label)+r' \|([^|]+)\|', block, re.M)
        if not m:
            continue
        number = re.search(r'\d+(?:\.\d+)?',m[1].replace('1타당', ''))
        if not number:
            continue
        doc = float(number[0])
        actual = asset[field]
        checks.append({'skill':sid,'field':field,'document':doc,'game':actual,'match':abs(doc-actual)<1e-5})
    if sid == '07':
        m = re.search(r'^\| 피해량 \|([^|]+)\|',block,re.M)
        nums = list(map(int,re.findall(r'\d+',m[1])))
        expected = [asset['damage'],asset['damage'],asset['damage']+asset['finalHitBonus'],asset['damage']*asset['hitCount']+asset['finalHitBonus']]
        checks.append({'skill':sid,'field':'hit distribution/total','document':nums,'game':expected,'match':nums==expected})
report={'skills':14,'checks':len(checks),'mismatches':[x for x in checks if not x['match']], 'details':checks}
(folder/'skill-document-comparison.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({k:v for k,v in report.items() if k!='details'},ensure_ascii=False))
