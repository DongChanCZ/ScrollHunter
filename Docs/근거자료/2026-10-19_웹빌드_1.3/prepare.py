from pathlib import Path
import shutil, hashlib, json, os

evidence=Path(__file__).resolve().parent
root=evidence.parents[2]
backup=Path(os.environ['TEMP'])/'ScrollHunter_Web13_20261019'
backup.mkdir(exist_ok=True)
files=[root/'Assets/_Project/Scenes/Battle.unity',
       *list((root/'Assets/_Project/Fonts').rglob('*SDF.asset')),
       *list((root/'Assets/Settings').glob('*.asset')),
       root/'ProjectSettings/ProjectSettings.asset',
       root/'Assets/_Project/Scripts/CombatUI.cs',
       root/'Assets/_Project/Scripts/CombatInfoUI.cs',
       root/'Assets/_Project/Scripts/Player.cs',
       *list(Path('C:/Users/MBC-501-19/AppData/LocalLow/DefaultCompany/ScrollHunter').glob('*'))]
records=[]
for i,p in enumerate(files):
    if not p.is_file():continue
    dest=backup/f'{i}_{p.name}'
    assert not dest.exists(),dest
    shutil.copy2(p,dest)
    records.append(dict(path=str(p),backup=str(dest),sha256=hashlib.sha256(p.read_bytes()).hexdigest()))
desktop=Path('C:/Users/MBC-501-19/Desktop')
build=desktop/'ScrollHunter_Web_1.3'
oldzip=desktop/'ScrollHunter_Web_1.3.zip'
shutil.copytree(build,backup/'PreviousBuild')
shutil.copy2(oldzip,backup/oldzip.name)
assert hashlib.sha256(oldzip.read_bytes()).digest()==hashlib.sha256((backup/oldzip.name).read_bytes()).digest()
(evidence/'before.json').write_text(json.dumps(records,indent=2),encoding='utf8')
(evidence/'previous-build.json').write_text(json.dumps(dict(folder=str(backup/'PreviousBuild'),zip=str(backup/oldzip.name),bytes=oldzip.stat().st_size,sha256=hashlib.sha256(oldzip.read_bytes()).hexdigest()),indent=2),encoding='utf8')
print('Backed up',len(records),'project/save files and the previous desktop folder/ZIP.')
