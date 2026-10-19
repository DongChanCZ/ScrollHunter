from pathlib import Path
import shutil

evidence = Path(__file__).resolve().parent
previous = evidence.parent / '2026-10-19_트레일러후속편집'
archive = Path('C:/Users/MBC-501-19/AppData/Local/Temp/ScrollHunter_HonestTrailer_20261019')

renderer = (previous / 'render_revision.py').read_text(encoding='utf8')
renderer = renderer.replace("('revision-render-'+LANG)", "('orbzoom-render-'+LANG)")
renderer = renderer.replace('_20261019_revision2_60s', '_20261019_orbzoom_60s')
renderer = renderer.replace("recording='ending',globalSrc=impact-7.6)", "recording='ending',globalSrc=impact-7.6,zoomStart=4.0,focus=(940/1920,225/1080),hold=.65,fullAt=6.6)")
anchor = 'for i,s in enumerate(shots):\n    dest='
assert renderer.count(anchor) == 1
camera = '''def camera(s, seconds):
    u=max(0,min(1,(seconds-s['hold'])/(s['fullAt']-s['hold'])))
    ease=u*u*u*(u*(u*6-15)+10)
    fraction=1/s['zoomStart']+(1-1/s['zoomStart'])*ease
    left=(s['focus'][0]-.5/s['zoomStart'])*(1-ease)
    top=(s['focus'][1]-.5/s['zoomStart'])*(1-ease)
    return fraction,1920*left,1080*top

cameraShot=shots[9]
cameraFrames=[dict(time=round(cameraShot['start']+j/30,5),affine=camera(cameraShot,j/30)) for j in range(round(cameraShot['d']*30))]
assert all(x['affine'][0]<=y['affine'][0] for x,y in zip(cameraFrames,cameraFrames[1:]))
assert camera(cameraShot,cameraShot['fullAt'])==(1,0,0)
assert cameraShot['start']+cameraShot['fullAt'] < cameraShot['start']+impact-cameraShot['globalSrc']
(EVIDENCE/(LANG+'-camera.json')).write_text(json.dumps(cameraFrames,indent=2),encoding='utf8')

'''
renderer = renderer.replace(anchor, camera+anchor)
old = """        if z!=1:
            cx,cy=s['c'];w=1920/z;h=1080/z;x=max(0,min(1920-w,1920*cx-w/2));y=max(0,min(1080-h,1080*cy-h/2))
            im=im.transform((1920,1080),Image.Transform.AFFINE,(1/z,0,x,0,1/z,y),resample=Image.Resampling.BICUBIC)
"""
new = """        if s['name']=='boss-detail':
            fraction,x,y=camera(s,j/30)
            if fraction!=1:
                im=im.transform((1920,1080),Image.Transform.AFFINE,(fraction,0,x,0,fraction,y),resample=Image.Resampling.BICUBIC)
        elif z!=1:
            cx,cy=s['c'];w=1920/z;h=1080/z;x=max(0,min(1920-w,1920*cx-w/2));y=max(0,min(1080-h,1080*cy-h/2))
            im=im.transform((1920,1080),Image.Transform.AFFINE,(1/z,0,x,0,1/z,y),resample=Image.Resampling.BICUBIC)
"""
assert renderer.count(old) == 1
renderer = renderer.replace(old,new)
renderer = renderer.replace('Fixed gameplay crops; no removed waiting within clips.', 'Orb close-up smoothly expands to full frame at 52.1s, before the unchanged 53.1s impact. Floating-point affine transform. No removed waiting within clips.')
(evidence/'render_zoom.py').write_text(renderer,encoding='utf8')

validator=(previous/'validate_revision.py').read_text(encoding='utf8')
validator=validator.replace('revision-render-', 'orbzoom-render-').replace('_20261019_revision2_60s', '_20261019_orbzoom_60s')
validator=validator.replace('(27.2,31.0,33.9,35.7,52.8,53.4)', '(45.9,47.5,49.3,51.0,52.2,53.4)')
validator=validator.replace("(E/'validated-files.json').write_text", """assert json.loads((E/'ko-camera.json').read_text())==json.loads((E/'en-camera.json').read_text())
assert endShot['start']+endShot['fullAt']==52.1
assert abs(endShot['start']+impact(a)/30-endShot['globalSrc']-53.1)<.001
for lang in ('ko','en'):
    for old in (W/('revision-render-'+lang)).glob('[0-9][0-9]-*.mp4'):
        if old.name.startswith('09-'):continue
        assert hashlib.sha256(old.read_bytes()).digest()==hashlib.sha256((W/('orbzoom-render-'+lang)/old.name).read_bytes()).digest()
    assert hashlib.sha256((W/('revision-render-'+lang)/'mix.wav').read_bytes()).digest()==hashlib.sha256((W/('orbzoom-render-'+lang)/'mix.wav').read_bytes()).digest()
(E/'validated-files.json').write_text""")
(evidence/'validate_zoom.py').write_text(validator,encoding='utf8')

for lang in ('ko','en'):
    destination=archive/('orbzoom-render-'+lang)
    destination.mkdir(exist_ok=True)
    for source in (archive/('revision-render-'+lang)).glob('[0-9][0-9]-*.mp4'):
        if source.name.startswith('09-'):continue
        shutil.copy2(source,destination/source.name)
print('Prepared both renders; previous outputs preserved.')
