from pathlib import Path
import json, hashlib, shutil, zipfile
folder=Path(__file__).parent
result=json.loads((folder/'build-result.json').read_text(encoding='utf-8-sig'))
assert result['result']=='Succeeded' and result['errors']==0,result
build=Path(result['path']); archive=build.parent/(build.name+'.zip')
assert build.name=='ScrollHunter_Web_1.3' and build.is_dir()
assert not archive.exists(),archive
assert (build/'index.html').exists()
assert (build/'Cinzel-Bold.ttf').exists() and (build/'OFL_Cinzel.txt').exists()
html=(build/'index.html').read_text(encoding='utf8')
assert 'launch-controls' in html and '1.3' in html
files=sorted(p for p in build.rglob('*') if p.is_file())
assert not any('DoNotShip' in str(p) or p.suffix=='.meta' for p in files)
with zipfile.ZipFile(archive,'x',zipfile.ZIP_DEFLATED,compresslevel=6) as z:
    for p in files: z.write(p,p.relative_to(build).as_posix())
checks=[]
with zipfile.ZipFile(archive) as z:
    assert z.testzip() is None
    for p in files:
        n=p.relative_to(build).as_posix()
        a=hashlib.sha256(p.read_bytes()).hexdigest(); b=hashlib.sha256(z.read(n)).hexdigest()
        assert a==b and '\\' not in n
        checks.append(dict(file=n,bytes=p.stat().st_size,sha256=a))
report=dict(zip=str(archive),bytes=archive.stat().st_size,sha256=hashlib.sha256(archive.read_bytes()).hexdigest(),files=checks)
(folder/'zip-checks.json').write_text(json.dumps(report,indent=2),encoding='utf8')
# 빌드가 다시 저장한 폰트/렌더 설정만 이번 작업 직전 사본으로 복원.
preservation=[]
for row in json.loads((folder/'before.json').read_text(encoding='utf8')):
    p=Path(row['path']); actual=hashlib.sha256(p.read_bytes()).hexdigest(); changed=actual!=row['sha256']
    restored=False
    if changed and ('/Fonts/' in p.as_posix() or '/Assets/Settings/' in p.as_posix()):
        assert hashlib.sha256(Path(row['backup']).read_bytes()).hexdigest()==row['sha256']
        shutil.copyfile(row['backup'],p);restored=True
    if changed and not restored and p.name!='ProjectSettings.asset': raise AssertionError(('Unexpected modified file',str(p)))
    preservation.append(dict(path=str(p),restoredBuildResave=restored,unchanged=hashlib.sha256(p.read_bytes()).hexdigest()==row['sha256']))
(folder/'preservation.json').write_text(json.dumps(preservation,indent=2),encoding='utf8')
print(json.dumps(dict(zip=str(archive),bytes=report['bytes'],files=len(files),restored=sum(r['restoredBuildResave'] for r in preservation))))
