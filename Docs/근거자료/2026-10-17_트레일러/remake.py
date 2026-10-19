from pathlib import Path
import subprocess, json, wave, shutil, io
import numpy as np
from PIL import Image, ImageDraw, ImageFont, ImageFilter

ROOT=Path('C:/Users/MBC-501-19/Documents/ScrollHunter')
ARCHIVE=Path('C:/Users/MBC-501-19/AppData/Local/Temp/ScrollHunter_TrailerSource_20261017')
OUT=ARCHIVE/'remake'; OUT.mkdir(exist_ok=True)
FF=Path('C:/Users/MBC-501-19/AppData/Local/Temp/ScrollHunter_TrailerTools/imageio_ffmpeg/binaries/ffmpeg-win-x86_64-v7.1.exe')
RAW=ARCHIVE/'take01/raw.mp4'
FONT=ROOT/'Assets/_Project/Fonts/GmarketSansOTF/GmarketSansBold.otf'
TITLE=ROOT/'Assets/_Project/Fonts/Cinzel/Cinzel-Bold.ttf'
EVIDENCE=Path(__file__).resolve().parent

def run(args):
    subprocess.run([str(FF),'-hide_banner','-loglevel','error','-y',*map(str,args)],check=True)

def frame(t,path):
    run(['-ss',t,'-i',RAW,'-frames:v','1',path])

frame(21.5,OUT/'reward.png')
# Duration includes the 0.3-second overlap with the following shot.
# Source ranges never include either recorded main-menu interval (0–3, 57–60).
shots=[
 dict(name='threat',src=53.0,d=3.2,z=(1.55,1.78),c=(.50,.44),text='숲의 끝에\n도사린 위협',pos='left'),
 dict(name='journey',still='Intro05_Traveler.png',d=4.0,z=(1.02,1.13),c=(.50,.48),text='전설의 힘을 찾아',pos='center'),
 dict(name='combat',src=3.15,d=4.8,z=(1.0,1.04),c=(.50,.50),text='네 장의 손패.\n실시간으로 바뀌는 전장.',pos='top'),
 dict(name='hand',src=9.0,d=3.6,z=(1.35,1.85),c=(.63,.81),text='코스트를 모으고',pos='top'),
 dict(name='interrupt',src=12.35,d=3.2,z=(1.45,1.82),c=(.68,.45),text='발동 직전, 차단!',pos='left'),
 dict(name='decision',still='Intro02_Scroll.png',d=2.4,z=(1.10,1.17),c=(.50,.50),text='한 번의 선택이\n전투를 바꾼다',pos='center'),
 dict(name='deck',still='reward',d=4.4,z=(1.0,1.05),c=(.50,.50),text=None),
 dict(name='reward-detail',still='reward',d=3.4,z=(1.50,1.65),c=(.25,.51),text='보상을 고르고\n나만의 덱을 완성하라',pos='right'),
 dict(name='leader',src=23.2,d=3.4,z=(1.15,1.48),c=(.50,.44),text=None),
 dict(name='outnumbered',src=33.05,d=3.6,z=(1.0,1.08),c=(.50,.50),text='몰려드는 적을 넘어',pos='top'),
 dict(name='pillar',src=25.3,d=3.6,z=(1.55,1.75),c=(.50,.43),text=None),
 dict(name='counterattack',src=38.8,d=3.2,z=(1.20,1.35),c=(.58,.47),text=None),
 dict(name='cave',src=43.05,speed=.30,d=4.5,z=(1.02,1.18),c=(.50,.48),text='불길한 동굴의 주인',pos='top'),
 dict(name='orbs',src=45.7,d=3.8,z=(1.60,1.78),c=(.49,.47),text='두 개의 오브,\n강화되는 위협',pos='left'),
 dict(name='phase-three',src=51.3,d=4.0,z=(1.02,1.16),c=(.50,.50),text=None),
 dict(name='climax',src=53.0,d=3.7,z=(1.38,1.67),c=(.50,.43),text='끝까지 살아남아라',pos='top'),
 dict(name='end',still='Intro02_Scroll.png',d=6.0,z=(1.02,1.08),c=(.5,.5),text='END',pos='center'),
]
assert abs(sum(s['d'] for s in shots)-.3*(len(shots)-1)-60)<.001
cursor=0
for s in shots:
    s['start']=round(cursor,4); cursor+=s['d']-.3
    if 'src' in s: assert s['src']>=3 and s['src']+s['d']<57

def overlay(s,index):
    im=Image.new('RGBA',(1920,1080)); d=ImageDraw.Draw(im)
    text=s.get('text')
    if not text: im.save(OUT/f'type-{index}.png'); return
    pos=s['pos']
    # Directional gradients support typography, rather than a fixed subtitle band.
    if pos=='center':
        d.rectangle((0,0,1920,1080),fill=(3,7,17,175 if text=='END' else 150))
    elif pos in ('left','right'):
        for x in range(1920):
            q=x if pos=='left' else 1919-x
            a=int(210*max(0,1-q/1150)**1.5)
            d.line((x,0,x,1080),fill=(3,7,16,a))
    else:
        for y in range(330): d.line((0,y,1920,y),fill=(3,7,16,int(195*(1-y/330)**1.2)))
    gold=(230,198,129,255); white=(246,241,225,255)
    def line(t,x,y,size,color=white,anchor='mm',font=FONT):
        f=ImageFont.truetype(str(font),size)
        d.text((x,y),t,font=f,fill=color,anchor=anchor,stroke_width=2,stroke_fill=(4,7,14,180))
    if text=='END':
        line('SCROLL HUNTER',960,435,117,gold,font=TITLE)
        d.line((600,527,1320,527),fill=gold,width=2)
        line('적의 시전을 읽고, 반격의 순간을 선택하라',960,595,40)
        line('지금 itch.io에서 플레이',960,760,38,gold)
        line('conze.itch.io/scroll-hunter',960,828,28)
    elif pos=='center':
        lines=text.split('\n'); y=535-(len(lines)-1)*55
        for t in lines: line(t,960,y,76); y+=110
        d.line((840,y-20,1080,y-20),fill=gold,width=3)
    elif pos=='top':
        for i,t in enumerate(text.split('\n')): line(t,960,130+i*78,54)
    else:
        x=130 if pos=='left' else 1160
        d.line((x,392,x+68,392),fill=gold,width=4)
        for i,t in enumerate(text.split('\n')): line(t,x,445+i*88,60,anchor='lm')
    im.save(OUT/f'type-{index}.png')

for i,s in enumerate(shots):
    overlay(s,i)
    dest=OUT/f'{i:02d}-{s["name"]}.mp4'
    if dest.exists(): continue
    if 'src' in s: args=['-ss',s['src'],'-i',RAW]
    else:
        p=OUT/'reward.png' if s['still']=='reward' else ROOT/'Assets/_Project/UI/Story'/s['still']
        args=['-loop','1','-framerate','30','-i',p]
    n=round(s['d']*30); a,b=s['z'];cx,cy=s['c']
    ease=f'(0.5-0.5*cos(PI*min(on/{n-1},1)))'
    z=f'{a}+({b}-{a})*{ease}'
    # Upsample before animated cropping to reduce one-pixel pan judder.
    prefix=f"trim=duration={s['d']*s['speed']},setpts=(PTS-STARTPTS)/{s['speed']},fps=30," if 'speed' in s else ''
    vf=prefix+f"scale=2560:1440:force_original_aspect_ratio=increase,crop=2560:1440,zoompan=z='{z}':x='max(0,min(iw-iw/zoom,iw*{cx}-iw/zoom/2))':y='max(0,min(ih-ih/zoom,ih*{cy}-ih/zoom/2))':d=1:s=1920x1080:fps=30,setsar=1"
    fadeout=s['d']-.40
    graph=f"[0:v]{vf}[scene];[1:v]format=rgba,fade=t=in:st=0.25:d=0.40:alpha=1,fade=t=out:st={fadeout}:d=0.35:alpha=1[type];[scene][type]overlay=shortest=1,format=yuv420p[v]"
    run([*args,'-loop','1','-framerate','30','-i',OUT/f'type-{i}.png','-filter_complex_threads','1','-filter_complex',graph,'-map','[v]','-an','-t',s['d'],'-c:v','libx264','-threads','3','-preset','fast','-crf','18',dest])
    print('Rendered',s['name'],flush=True)

# Cross-dissolves at scene changes; quick transitions into action remain readable.
args=[]
for i,s in enumerate(shots):args+=['-i',OUT/f'{i:02d}-{s["name"]}.mp4']
filters=[];last='0:v'; length=shots[0]['d']
for i in range(1,len(shots)):
    trans='fadeblack' if i in (1,5,6,12,16) else 'fade'
    filters.append(f'[{last}][{i}:v]xfade=transition={trans}:duration=0.3:offset={length-.3:.4f}[v{i}]')
    last=f'v{i}';length+=shots[i]['d']-.3
filters.append(f'[{last}]fade=t=in:st=0:d=0.35,fade=t=out:st=59.1:d=0.9,format=yuv420p[final]')
if not (OUT/'picture.mp4').exists():
    run([*args,'-filter_complex_threads','1','-filter_complex',';'.join(filters),'-map','[final]','-an','-t','60','-r','30','-c:v','libx264','-threads','3','-preset','fast','-crf','18',OUT/'picture.mp4'])
    print('Transitions finished',flush=True)

rate=48000; total=60*rate; mix=np.zeros((total,2),np.float32); cache={}
def decode(path):
    key=str(path)
    if key not in cache:
        data=subprocess.check_output([str(FF),'-v','error','-i',key,'-f','f32le','-ac','2','-ar',str(rate),'-'])
        cache[key]=np.frombuffer(data,dtype='<f4').reshape(-1,2)
    return cache[key]
music=decode(ROOT/'Assets/_Project/Audio/Music/bgm_boss_evil_awaits.wav')
music=np.tile(music,(int(np.ceil(total/len(music))),1))[:total].copy()
t=np.arange(total)/rate
envelope=np.interp(t,[0,1,16,19,25,36,43,53,56,58,60],[0,.29,.29,.17,.26,.34,.29,.39,.30,.24,0])
mix+=music*envelope[:,None]
events=json.loads((ARCHIVE/'take01/sounds.json').read_text())['events']; used=[]
for s in shots:
    if 'src' not in s:continue
    for e in events:
        rel=(e['time']-s['src'])/s.get('speed',1)
        if not 0<=rel<s['d'] or not e['path']:continue
        a=decode(ROOT/e['path']);start=round((s['start']+rel)*rate)
        count=min(len(a),round((s['d']-rel)*rate),int(3.5*rate),total-start)
        if count<=0:continue
        a=a[:count].copy()*min(e['volume'],1)*.55
        edge=min(480,count//2);a[:edge]*=np.linspace(0,1,edge)[:,None];a[-edge:]*=np.linspace(1,0,edge)[:,None]
        mix[start:start+count]+=a;used.append(dict(time=start/rate,path=e['path']))
peak=float(np.max(np.abs(mix)));mix*=min(1,.94/max(.001,peak))
with wave.open(str(OUT/'mix.wav'),'wb') as w:
    w.setnchannels(2);w.setsampwidth(2);w.setframerate(rate);w.writeframes((mix*32767).astype('<i2').tobytes())
final=OUT/'ScrollHunter_Trailer_KO_v2_60s.mp4'
run(['-i',OUT/'picture.mp4','-i',OUT/'mix.wav','-map','0:v','-map','1:a','-c:v','copy','-af','loudnorm=I=-16:TP=-1.5:LRA=11,afade=t=out:st=58:d=2','-c:a','aac','-b:a','192k','-ar','48000','-t','60','-movflags','+faststart',final])
plan=dict(duration=60,shots=shots,transitions='0.3-second crossfade / fade through black',soundEvents=len(used),sourceMenuIntervalsExcluded=[[0,3],[57,60]],notes='Edited gameplay and original story art. No main-menu footage. Promotional capture, not an uninterrupted normal run.')
(EVIDENCE/'remake-plan.json').write_text(json.dumps(plan,ensure_ascii=False,indent=2),encoding='utf8')
def stamp(x):
    ms=round(x*1000);return f'{ms//3600000:02}:{ms//60000%60:02}:{ms//1000%60:02},{ms%1000:03}'
subs=[]
for s in shots:
    if not s.get('text'):continue
    text=s['text'] if s['text']!='END' else 'SCROLL HUNTER\n적의 시전을 읽고, 반격의 순간을 선택하라\n지금 itch.io에서 플레이'
    subs.append(f'{len(subs)+1}\n{stamp(s["start"]+.25)} --> {stamp(s["start"]+s["d"]-.35)}\n{text}')
(OUT/'ScrollHunter_Trailer_KO_v2_60s.srt').write_text('\n\n'.join(subs)+'\n',encoding='utf-8-sig')
print('FINISHED',str(final),flush=True)
