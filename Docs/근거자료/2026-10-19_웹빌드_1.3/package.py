from pathlib import Path
import json, hashlib, shutil, zipfile, os

evidence=Path(__file__).resolve().parent
result=json.loads((evidence/'build-result.json').read_text(encoding='utf8'))
assert result['result']=='Succeeded' and result['errors']==0,result
build=Path(result['path'])
assert build.resolve()==Path('C:/Users/MBC-501-19/Desktop/ScrollHunter_Web_1.3').resolve()
archive=build.parent/(build.name+'.zip')
previous=json.loads((evidence/'previous-build.json').read_text(encoding='utf8'))
assert hashlib.sha256(archive.read_bytes()).hexdigest()==previous['sha256'],'Desktop ZIP changed during build'
assert hashlib.sha256(Path(previous['zip']).read_bytes()).hexdigest()==previous['sha256'],'Backup differs'
assert (build/'index.html').is_file() and (build/'Cinzel-Bold.ttf').is_file()
assert 'launch-controls' in (build/'index.html').read_text(encoding='utf8')
assert '1.3' in (build/'index.html').read_text(encoding='utf8')
files=sorted(p for p in build.rglob('*') if p.is_file())
assert not any('DoNotShip' in str(p) or p.suffix=='.meta' for p in files)
pending=Path(previous['zip']).parent/'ScrollHunter_Web_1.3_new.zip'
assert not pending.exists(),pending
with zipfile.ZipFile(pending,'x',zipfile.ZIP_DEFLATED,compresslevel=6) as z:
    for p in files:z.write(p,p.relative_to(build).as_posix())
checks=[]
with zipfile.ZipFile(pending) as z:
    assert z.testzip() is None and 'index.html' in z.namelist()
    for p in files:
        name=p.relative_to(build).as_posix()
        digest=hashlib.sha256(p.read_bytes()).hexdigest()
        assert hashlib.sha256(z.read(name)).hexdigest()==digest and '\\' not in name
        checks.append(dict(file=name,bytes=p.stat().st_size,sha256=digest))
preservation=[]
for row in json.loads((evidence/'before.json').read_text(encoding='utf8')):
    p=Path(row['path']);changed=hashlib.sha256(p.read_bytes()).hexdigest()!=row['sha256']
    restored=False
    if changed and ('/Fonts/' in p.as_posix() or '/Assets/Settings/' in p.as_posix()):
        assert hashlib.sha256(Path(row['backup']).read_bytes()).hexdigest()==row['sha256']
        shutil.copyfile(row['backup'],p);restored=True
    assert hashlib.sha256(p.read_bytes()).hexdigest()==row['sha256'],('Unexpected modified file',str(p))
    preservation.append(dict(path=str(p),restoredBuildResave=restored,unchanged=True))
(evidence/'preservation.json').write_text(json.dumps(preservation,indent=2),encoding='utf8')
newhash=hashlib.sha256(pending.read_bytes()).hexdigest()
os.replace(pending,archive)
assert hashlib.sha256(archive.read_bytes()).hexdigest()==newhash
report=dict(zip=str(archive),bytes=archive.stat().st_size,sha256=newhash,files=checks,previousZip=previous['zip'])
(evidence/'zip-checks.json').write_text(json.dumps(report,indent=2),encoding='utf8')
print(json.dumps(dict(zip=str(archive),bytes=report['bytes'],files=len(files),restored=sum(r['restoredBuildResave'] for r in preservation))))
