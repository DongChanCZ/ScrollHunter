from pathlib import Path
import shutil, hashlib, json, os
root=Path(__file__).resolve().parents[3]
backup=Path(os.environ['TEMP'])/'ScrollHunter_Web13_20261017'
backup.mkdir(exist_ok=True)
files=[root/'Assets/_Project/Scenes/Battle.unity',*list((root/'Assets/_Project/Fonts').rglob('*SDF.asset')),*list((root/'Assets/Settings').glob('*.asset')),root/'ProjectSettings/ProjectSettings.asset',*list(Path('C:/Users/MBC-501-19/AppData/LocalLow/DefaultCompany/ScrollHunter').glob('*'))]
result=[]
for i,p in enumerate(files):
    if not p.is_file(): continue
    dest=backup/f'{i}_{p.name}'
    assert not dest.exists(),dest
    shutil.copyfile(p,dest)
    result.append(dict(path=str(p),backup=str(dest),sha256=hashlib.sha256(p.read_bytes()).hexdigest()))
(Path(__file__).parent/'before.json').write_text(json.dumps(result,indent=2),encoding='utf8')
p=root/'Assets/_Project/Editor/WebBuildSetup.cs'
data=p.read_bytes(); assert b'1.3' not in data
p.write_bytes(data.replace(b'1.2',b'1.3').replace(b'2026-10-16_',b'2026-10-17_'))
print('Backed up',len(result),'files; build target set to Desktop/ScrollHunter_Web_1.3')
