from pathlib import Path
import subprocess, json, math, wave, ast, sys
import numpy as np
from PIL import Image, ImageDraw, ImageFont, ImageFilter, ImageOps

ROOT=Path('C:/Users/MBC-501-19/Documents/ScrollHunter')
EVIDENCE=Path(__file__).resolve().parent
ARCHIVE=Path('C:/Users/MBC-501-19/AppData/Local/Temp/ScrollHunter_HonestTrailer_20261019')
FF=ARCHIVE/'ffmpeg.exe'
LANG=sys.argv[1]
assert LANG in ('ko','en')
OUT=ARCHIVE/('render-final-'+LANG);OUT.mkdir(exist_ok=True)
RAW=ARCHIVE/(LANG+'-final')/'raw.mp4'
FONT=ROOT/'Assets/_Project/Fonts/GmarketSansOTF/GmarketSansBold.otf'
TITLE=ROOT/'Assets/_Project/Fonts/Cinzel/Cinzel-Bold.ttf'
report=json.loads((ARCHIVE/'ko-final/report.json').read_text(encoding='utf8'))
wording_ko={
 'origin-war':('전쟁이 낳은 힘','고대의 제국, 비전 스크롤을 만들다'),
 'origin-scroll':('힘을 전하는 마법','기술과 지식까지 담아낸 스크롤'),
 'origin-fall':('탐욕이 부른 몰락','끝없는 다툼 속에 무너진 제국'),
 'origin-peace':('전설이 된 힘','긴 세월 뒤, 잊혀진 비전'),
 'origin-hunter':('전설을 쫓는 자들','그 이름, 스크롤 헌터'),
 'combat':('네 가지 스킬, 무수한 선택','실시간 반응형 전략 전투'),
 'hand':('다음 수를 준비하라','시전과 코스트를 고려하는 전투'),
 'boss':('시전을 읽고, 반격하라','차단할 순간과 방어할 순간'),
 'end':('SCROLL HUNTER','적의 시전을 읽고, 반격의 순간을 선택하라')}
wording_en={
 'origin-war':('FORGED IN WAR','An empire created the Arcane Scrolls.'),
 'origin-scroll':('POWER, PASSED ON','Magic. Skill. Knowledge. Sealed in scrolls.'),
 'origin-fall':('UNDONE BY GREED','Their power tore the empire apart.'),
 'origin-peace':('LOST TO LEGEND','Ages passed. Their true power was forgotten.'),
 'origin-hunter':('SEEKERS OF LEGEND','Their name: Scroll Hunters.'),
 'combat':('FOUR SKILLS. COUNTLESS CHOICES.','Real-time tactical skill combat'),
 'hand':('PREPARE YOUR NEXT MOVE','Manage your Cost. Commit to each cast.'),
 'boss':('READ THE CAST. FIGHT BACK.','Know when to interrupt. Know when to defend.'),
 'end':('SCROLL HUNTER','Read their casts. Choose your moment to strike.')}
wording=wording_ko if LANG=='ko' else wording_en
assert not any('카드' in v or 'card' in v.lower() for pair in wording.values() for v in pair)

# Reuse the previous approved typography and float-coordinate framing helpers only.
old=ast.parse((EVIDENCE.parent/'2026-10-17_트레일러/english_trailer.py').read_text(encoding='utf-8-sig'))
functions=[n for n in old.body if isinstance(n,ast.FunctionDef) and n.name in ('run','smooth','text_layer','typography','compose_text')]
exec(compile(ast.Module(body=functions,type_ignores=[]),'<approved-trailer-typography>','exec'))

def marks(name):return [m['frame']/30 for m in report['marks'] if m['name']==name]
boss=marks('boss-start')[0]
reward=marks('reward-comparison')
assert reward, 'Need a naturally earned reward comparison shot'
actions=[a for a in report['actions'] if a['action']=='skill']
def choose(start,end,length):
    candidates=np.arange(start,max(start+.1,end-length),.5)
    def score(t):
        a=[q for q in actions if t<=q['frame']/30<t+length]
        unique=len(set(q['skill'] for q in a))
        return unique*3+len(a)+sum(('Silence' in q['skill'] or 'Armor' in q['skill']) for q in a)*2
    return round(max(candidates,key=score)*30)/30
forest_end=marks('reward-candidates')[0]
forest=choose(1,forest_end-1,18.3)
phases=[m for m in report['marks'] if m['name']=='phase' and m['phase']>=2]
boss_length=17.8
end=report['frames']/30
boss_source=max(boss+3, phases[0]['frame']/30-3) if phases else choose(boss+4,end-2,boss_length)
boss_source=min(boss_source,end-boss_length-1)
shots=[]
for name,still,center,z in [
 ('origin-war','Intro01_War.png',(.5,.48),1.035),
 ('origin-scroll','Intro02_Scroll.png',(.5,.45),1.045),
 ('origin-fall','Intro03_Conflict.png',(.5,.38),1.12),
 ('origin-peace','Intro04_Peace.png',(.5,.48),1.035),
 ('origin-hunter','Intro05_Traveler.png',(.5,.5),1.045)]:
    shots.append(dict(name=name,still=still,d=2.8,c=center,fixedZoom=z,pos='story'))
shots.extend([
 dict(name='combat',src=forest,d=10.3,c=(.5,.5),fixedZoom=1,pos='top'),
 dict(name='hand',src=forest+10,d=8.3,c=(.65,.80),fixedZoom=1.5,pos='top'),
 dict(name='reward',src=reward[0]+.65,d=6.3,c=(.5,.5),fixedZoom=1),
 dict(name='boss',src=boss_source,d=9.3,c=(.5,.5),fixedZoom=1,pos='top'),
 dict(name='boss-detail',src=boss_source+9,d=8.8,c=(.50,.70),fixedZoom=1.25),
 dict(name='end',still='Intro02_Scroll.png',d=6,c=(.5,.5),fixedZoom=1.04,pos='center')])
clock=0
for s in shots:
    s['start']=round(clock,5);clock+=s['d']-.3
    s['text']='\n'.join(wording[s['name']]) if s['name'] in wording else None
assert abs(clock+.3-60)<.001
# Adjacent combat crops preserve the same source clock, with a visible fade between framings.
assert abs(shots[6]['src']-(shots[5]['src']+shots[5]['d']-.3))<1e-6
assert abs(shots[9]['src']-(shots[8]['src']+shots[8]['d']-.3))<1e-6
for s in shots:typography(s)
(EVIDENCE/(LANG+'-plan.json')).write_text(json.dumps(dict(duration=60,shots=shots,playbackSpeed=1,notes='New gameplay. No cast/cost/damage edits. Separate forest and boss encounters. Matching input replay for English. Fixed gameplay crops; no removed waiting within clips.'),ensure_ascii=False,indent=2),encoding='utf8')

for i,s in enumerate(shots):
    dest=OUT/f'{i:02d}-{s["name"]}.mp4'
    if dest.exists():continue
    shade,layers=typography(s);n=round(s['d']*30)
    if 'src' in s:
        dec=subprocess.Popen([str(FF),'-v','error','-ss',str(s['src']),'-i',str(RAW),'-frames:v',str(n),'-f','rawvideo','-pix_fmt','rgb24','-'],stdout=subprocess.PIPE)
        still=None
    else:still=ImageOps.fit(Image.open(ROOT/'Assets/_Project/UI/Story'/s['still']).convert('RGB'),(1920,1080),method=Image.Resampling.LANCZOS)
    enc=subprocess.Popen([str(FF),'-v','error','-y','-f','rawvideo','-pix_fmt','rgb24','-s','1920x1080','-r','30','-i','-','-an','-c:v','libx264','-threads','3','-preset','fast','-crf','18','-pix_fmt','yuv420p',str(dest)],stdin=subprocess.PIPE)
    for j in range(n):
        if still is None:
            b=dec.stdout.read(1920*1080*3);assert len(b)==1920*1080*3,(s['name'],j,len(b))
            im=Image.frombytes('RGB',(1920,1080),b)
        else:im=still
        z=s['fixedZoom']+(.012*smooth(j/max(1,n-1)) if s.get('pos')=='story' else 0)
        if z!=1:
            cx,cy=s['c'];w=1920/z;h=1080/z;x=max(0,min(1920-w,1920*cx-w/2));y=max(0,min(1080-h,1080*cy-h/2))
            im=im.transform((1920,1080),Image.Transform.AFFINE,(1/z,0,x,0,1/z,y),resample=Image.Resampling.BICUBIC)
        im=compose_text(im,s,j/30,shade,layers)
        if j==45:im.save(OUT/f'{i:02d}-preview.png')
        enc.stdin.write(im.tobytes())
    enc.stdin.close();assert enc.wait()==0
    if still is None:dec.stdout.close();assert dec.wait()==0
    print(LANG,'rendered',s['name'],flush=True)

args=[]
for i,s in enumerate(shots):args+=['-i',OUT/f'{i:02d}-{s["name"]}.mp4']
filters=[];last='0:v';length=shots[0]['d']
for i in range(1,len(shots)):
    filters.append(f'[{last}][{i}:v]xfade=transition=fadeblack:duration=0.3:offset={length-.3:.4f}[v{i}]')
    last=f'v{i}';length+=shots[i]['d']-.3
filters.append(f'[{last}]fade=t=in:st=0:d=0.35,fade=t=out:st=59.1:d=0.9,format=yuv420p[final]')
if not (OUT/'picture.mp4').exists():run([*args,'-filter_complex_threads','1','-filter_complex',';'.join(filters),'-map','[final]','-an','-t','60','-r','30','-c:v','libx264','-threads','3','-preset','fast','-crf','18',OUT/'picture.mp4'])

rate=48000;total=rate*60;mix=np.zeros((total,2),np.float32);cache={}
def decode(path):
    key=str(path)
    if key not in cache:
        data=subprocess.check_output([str(FF),'-v','error','-i',key,'-f','f32le','-ac','2','-ar',str(rate),'-'])
        cache[key]=np.frombuffer(data,dtype='<f4').reshape(-1,2)
    return cache[key]
music=decode(ROOT/'Assets/_Project/Audio/Music/bgm_boss_evil_awaits.wav')
music=np.tile(music,(int(np.ceil(total/len(music))),1))[:total].copy()
t=np.arange(total)/rate
mix+=music*np.interp(t,[0,1,12,15,30,36,48,54,58,60],[0,.29,.29,.18,.24,.22,.31,.3,.22,0])[:,None]
used=set()
for s in shots:
    if 'src' not in s:continue
    for e in report['sounds']:
        rel=e['frame']/30-s['src']
        if not 0<=rel<s['d'] or not e['path']:continue
        start=round((s['start']+rel)*rate)
        signature=(e['frame'],e['path'],start)
        if signature in used:continue
        used.add(signature)
        a=decode(ROOT/e['path']);n=min(len(a),total-start,round((s['d']-rel)*rate),rate*4)
        if n<=0:continue
        a=a[:n].copy()*min(e['volume'],1)*.55
        edge=min(480,n//2);a[:edge]*=np.linspace(0,1,edge)[:,None];a[-edge:]*=np.linspace(1,0,edge)[:,None]
        mix[start:start+n]+=a
mix*=min(1,.94/max(.001,float(np.max(np.abs(mix)))))
with wave.open(str(OUT/'mix.wav'),'wb') as w:w.setnchannels(2);w.setsampwidth(2);w.setframerate(rate);w.writeframes((mix*32767).astype('<i2').tobytes())
name='ScrollHunter_Trailer_'+LANG.upper()+'_20261019_60s'
run(['-i',OUT/'picture.mp4','-i',OUT/'mix.wav','-map','0:v','-map','1:a','-c:v','copy','-af','loudnorm=I=-16:TP=-1.5:LRA=11,afade=t=out:st=58:d=2','-c:a','aac','-b:a','192k','-ar','48000','-t','60','-movflags','+faststart',OUT/(name+'.mp4')])
def stamp(x):
    ms=round(x*1000);return f'{ms//3600000:02}:{ms//60000%60:02}:{ms//1000%60:02},{ms%1000:03}'
subs=[]
for s in shots:
    if s['text']:subs.append(f'{len(subs)+1}\n{stamp(s["start"]+.1)} --> {stamp(s["start"]+s["d"]-.35)}\n{s["text"]}')
(OUT/(name+'.srt')).write_text('\n\n'.join(subs)+'\n',encoding='utf-8-sig')
print('FINISHED',LANG,flush=True)
