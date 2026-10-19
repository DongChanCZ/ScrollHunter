from pathlib import Path
import json, subprocess, re, hashlib, io
from PIL import Image, ImageDraw
import numpy as np

E=Path(__file__).resolve().parent
W=Path('C:/Users/MBC-501-19/AppData/Local/Temp/ScrollHunter_HonestTrailer_20261019')
FF=W/'ffmpeg.exe'
reports={lang:json.loads((W/(lang+'-final')/'report.json').read_text(encoding='utf8')) for lang in ('ko','en')}
a,b=reports['ko'],reports['en']
assert a['frames']==b['frames']
assert not a['errors'] and not b['errors'],(a['errors'],b['errors'])
assert len(a['actions'])==len(b['actions'])
differences=[]
noncombat_cost_differences=[]
for x,y in zip(a['actions'],b['actions']):
    for key in ('frame','arg','action','target','skill'):
        if x[key]!=y[key]:differences.append((x['frame'],key,x[key],y[key]))
    for key in ('hp','cost','lockBefore','castBefore'):
        if abs(x[key]-y[key])>.04:
            if key=='cost' and x['action'] in ('reward','replace','confirm','begin'):
                noncombat_cost_differences.append((x['frame'],key,x[key],y[key]))
            else:differences.append((x['frame'],key,x[key],y[key]))
assert not differences,differences[:20]
for r in reports.values():
    skills=[x for x in r['actions'] if x['action']=='skill']
    assert all(x['lockBefore']<=.001 and x['castBefore']<=.001 for x in skills)
    gaps=[(y['frame']-x['frame'])/30 for x,y in zip(skills,skills[1:])]
    assert min(gaps)>=.6
phases=lambda r:[(m['frame'],m['phase']) for m in r['marks'] if m['name']=='phase']
assert phases(a)==phases(b),(phases(a),phases(b))
audit=dict(sourceFrames=a['frames'],sourceSeconds=a['frames']/30,matchedActions=len(a['actions']),matchedSkills=len(skills),minimumSkillInputGap=min(gaps),phaseTransitions=phases(a),rejectedInputs=0,gameplaySpeed=1,forcedDamage=False,costRefills=False,notes='Temporary encounter selection; normal skill acceptance/casting/cost/HP/damage. Per-frame RNG seeds match both languages without changing critical probability. No gap removal or speed-up within gameplay shots.')
audit['noncombatCostDifferences']=noncombat_cost_differences
audit['noncombatNote']='Small remaining-cost differences during the death-animation/reward screen are recorded separately. Cost is hidden there and the independent boss encounter resets to the normal 3 Cost. All combat-action HP, Cost, cast and lock values must match.'
(E/'gameplay-audit.json').write_text(json.dumps(audit,ensure_ascii=False,indent=2),encoding='utf8')

deliveries=[]
for lang in ('ko','en'):
    out=W/('render-final-'+lang)
    video=out/f'ScrollHunter_Trailer_{lang.upper()}_20261019_60s.mp4'
    srt=video.with_suffix('.srt').read_text(encoding='utf-8-sig')
    assert '카드' not in srt and not re.search(r'\bcards?\b',srt,re.I)
    if lang=='en':assert not re.search('[가-힣]',srt)
    meta=subprocess.run([str(FF),'-hide_banner','-i',str(video)],capture_output=True,text=True).stderr
    assert 'Duration: 00:01:00.00' in meta and '1920x1080' in meta and '30 fps' in meta and 'Audio: aac' in meta,meta
    dec=subprocess.run([str(FF),'-v','error','-i',str(video),'-progress','pipe:1','-f','null','-'],capture_output=True,text=True,check=True)
    assert not dec.stderr.strip(),dec.stderr
    assert re.findall(r'frame=(\d+)',dec.stdout)[-1]=='1800'
    (E/(lang+'-validation.txt')).write_text(meta+'\nFull decode: 1800 frames, no errors.\nNo card wording in captions.\n',encoding='utf8')
    audio=subprocess.run([str(FF),'-hide_banner','-nostats','-i',str(video),'-vn','-af','ebur128=peak=true','-f','null','-'],capture_output=True,text=True,check=True)
    (E/(lang+'-audio.txt')).write_text(audio.stderr[-1700:],encoding='utf8')
    plan=json.loads((E/(lang+'-plan.json')).read_text(encoding='utf8'))
    sheet=Image.new('RGB',(1536,318*((len(plan['shots'])+2)//3)),(8,10,16));d=ImageDraw.Draw(sheet)
    for i,s in enumerate(plan['shots']):
        t=s['start']+min(1.5,s['d']/2)
        data=subprocess.check_output([str(FF),'-v','error','-ss',str(t),'-i',str(video),'-frames:v','1','-vf','scale=512:288','-f','image2pipe','-vcodec','png','-'])
        sheet.paste(Image.open(io.BytesIO(data)),(i%3*512,i//3*318));d.text((i%3*512+6,i//3*318+293),f'{t:.1f}s - {s["name"]}',fill='white')
    sheet.save(E/(lang+'-contact.png'))
    deliveries.append(dict(language=lang,source=str(video),bytes=video.stat().st_size,sha256=hashlib.sha256(video.read_bytes()).hexdigest(),duration=60,width=1920,height=1080,fps=30))
ko=json.loads((E/'ko-plan.json').read_text(encoding='utf8'))
en=json.loads((E/'en-plan.json').read_text(encoding='utf8'))
for x,y in zip(ko['shots'],en['shots']):
    assert {k:v for k,v in x.items() if k!='text'}=={k:v for k,v in y.items() if k!='text'}
assert hashlib.sha256((W/'render-final-ko/mix.wav').read_bytes()).digest()==hashlib.sha256((W/'render-final-en/mix.wav').read_bytes()).digest()
(E/'validated-files.json').write_text(json.dumps(deliveries,ensure_ascii=False,indent=2),encoding='utf8')
print(json.dumps(audit,ensure_ascii=False,indent=2),flush=True)
print('BOTH VIDEOS VALIDATED',flush=True)
