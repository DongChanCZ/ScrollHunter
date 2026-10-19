from pathlib import Path
import subprocess, json, math, wave
import numpy as np
from PIL import Image, ImageDraw, ImageFont, ImageFilter, ImageOps

ROOT=Path('C:/Users/MBC-501-19/Documents/ScrollHunter')
EVIDENCE=Path(__file__).resolve().parent
ARCHIVE=Path('C:/Users/MBC-501-19/AppData/Local/Temp/ScrollHunter_TrailerSource_20261017')
OUT=ARCHIVE/'english'; OUT.mkdir(exist_ok=True)
FF=ARCHIVE.parent/'ScrollHunter_TrailerTools/imageio_ffmpeg/binaries/ffmpeg-win-x86_64-v7.1.exe'
RAW=ARCHIVE/'english-take/raw.mp4'
FONT=ROOT/'Assets/_Project/Fonts/GmarketSansOTF/GmarketSansBold.otf'
TITLE=ROOT/'Assets/_Project/Fonts/Cinzel/Cinzel-Bold.ttf'
shots=json.loads((EVIDENCE/'story-remake-plan.json').read_text(encoding='utf8'))['shots']
wording={
 'origin-war':('FORGED IN WAR','An empire created the Arcane Scrolls.'),
 'origin-scroll':('POWER, PASSED ON','Magic. Skill. Knowledge. Sealed in scrolls.'),
 'origin-fall':('UNDONE BY GREED','Their power tore the empire apart.'),
 'origin-peace':('LOST TO LEGEND','Ages passed. Their true power was forgotten.'),
 'origin-hunter':('SEEKERS OF LEGEND','Their name: Scroll Hunters.'),
 'combat':('FOUR CARDS. ENDLESS CHOICES.','Real-time roguelike card combat'),
 'interrupt':('READ THE CAST.\nBREAK THE SPELL.','Strike before their attack lands.'),
 'hand':('BUILD UP. STRIKE HARD.','Make every point of Cost count.'),
 'skill-release':('MAKE YOUR NEXT MOVE COUNT','Unleash the power you prepared.'),
 'reward-detail':('YOUR DECK.\nYOUR STRATEGY.','Choose your rewards.\nBuild your own strategy.'),
 'multi-target':('CHOOSE WHO FALLS FIRST','Pick your target. Seize the advantage.'),
 'pillar':('TURN THE TIDE','Your choices decide the battle.'),
 'end':('SCROLL HUNTER','Read their casts. Choose your moment to strike.'),
}
zooms=[1.035,1.045,1.12,1.035,1.045,1.035,1.55,1.035,1.65,1.25,1.0,1.50,1.035,1.55,1.50,1.40,1.04]
for s,z in zip(shots,zooms):
    s['fixedZoom']=z
    s['text']='\n'.join(wording[s['name']]) if s['name'] in wording else None

def run(args):
    subprocess.run([str(FF),'-hide_banner','-loglevel','error','-y',*map(str,args)],check=True)

def smooth(x):
    x=max(0,min(1,x));return x*x*(3-2*x)

class Stabilizer:
    def __init__(self):
        self.win=np.outer(np.hanning(104),np.hanning(480));self.ref=None
    def prepare(self,im):
        a=np.asarray(im,dtype=float);a=np.diff(a,axis=1,prepend=a[:,:1])
        return np.fft.fft2(a*self.win)
    def measure(self,im):
        f=self.prepare(im)
        if self.ref is None:self.ref=f
        c=f*np.conj(self.ref);c/=np.maximum(abs(c),1e-8)
        r=np.fft.ifft2(c).real;iy,ix=np.unravel_index(r.argmax(),r.shape)
        score=float(r[iy,ix]);dx=float(ix if ix<240 else ix-480);dy=float(iy if iy<52 else iy-104)
        def sub(a,b,c):
            den=a-2*b+c
            return float(np.clip(.5*(a-c)/den,-.5,.5)) if abs(den)>1e-8 else 0.
        dx+=sub(r[iy,(ix-1)%480],r[iy,ix],r[iy,(ix+1)%480])
        dy+=sub(r[(iy-1)%104,ix],r[iy,ix],r[(iy+1)%104,ix])
        if score<.10 or abs(dx)>20 or abs(dy)>20:return 0.,0.,score
        if abs(dx)<.12:dx=0.
        if abs(dy)<.12:dy=0.
        return dx,dy,score

# Check correction direction on an intentionally translated fixed UI-like image.
test=Image.new('L',(480,104));td=ImageDraw.Draw(test)
td.rectangle((30,16,250,78),outline=255,width=3);td.text((80,36),'STABLE CAMERA',fill=220)
st=Stabilizer();st.measure(test)
moved=test.transform(test.size,Image.Transform.AFFINE,(1,0,-11,0,1,5),resample=Image.Resampling.BICUBIC)
dx,dy,_=st.measure(moved);assert abs(dx-11)<.5 and abs(dy+5)<.5,(dx,dy)

def text_layer(text,size,colors,font=FONT):
    f=ImageFont.truetype(str(font),size)
    probe=ImageDraw.Draw(Image.new('L',(1,1)))
    box=probe.multiline_textbbox((0,0),text,font=f,spacing=15,align='center')
    w=math.ceil(box[2]-box[0]+72);h=math.ceil(box[3]-box[1]+72)
    mask=Image.new('L',(w,h));ImageDraw.Draw(mask).multiline_text((36-box[0],36-box[1]),text,font=f,spacing=15,fill=255,align='center')
    yy=np.linspace(0,1,h)[:,None,None];top=np.array(colors[0]);bot=np.array(colors[1])
    rgb=np.repeat((top[None,None,:]*(1-yy)+bot[None,None,:]*yy).astype('uint8'),w,axis=1)
    face=Image.fromarray(rgb,'RGB').convert('RGBA');face.putalpha(mask)
    glow=Image.new('RGBA',(w,h),(*colors[1],0));glow.putalpha(mask.filter(ImageFilter.GaussianBlur(9)).point(lambda a:round(a*.42)))
    shadow=Image.new('RGBA',(w,h),(0,0,0,0));shadow.putalpha(mask.filter(ImageFilter.GaussianBlur(4)).point(lambda a:round(a*.95)))
    base=Image.alpha_composite(shadow,glow);base=Image.alpha_composite(base,face)
    return base,np.asarray(mask,dtype=np.float32),w,h

def typography(s):
    shade=Image.new('RGBA',(1920,1080));d=ImageDraw.Draw(shade);layers=[]
    if s['name'] not in wording:return shade,layers
    main,sub=wording[s['name']];pos=s.get('pos','top')
    if pos=='story':
        for y in range(610,1080):d.line((0,y,1920,y),fill=(3,7,16,round(225*smooth((y-610)/470))))
        x=960;my=820;sy=930;size=90;ss=35
    elif pos=='center':
        d.rectangle((0,0,1920,1080),fill=(3,7,17,178));x=960;my=425;sy=592;size=121;ss=39
    elif pos in ('left','right'):
        for a in range(1920):
            q=a if pos=='left' else 1919-a
            d.line((a,0,a,1080),fill=(3,7,16,round(220*max(0,1-q/1160)**1.5)))
        x=410 if pos=='left' else 1480;my=455;sy=615 if pos=='left' else 570;size=52;ss=26
    else:
        for y in range(340):d.line((0,y,1920,y),fill=(3,7,16,round(215*(1-y/340)**1.2)))
        x=960;my=125;sy=220;size=73;ss=32
    color=((255,242,201),(231,174,73))
    if s['name'] in ('origin-scroll','hand'):color=((225,250,255),(90,185,255))
    if s['name']=='interrupt':color=((249,234,255),(197,145,255))
    if s['name']=='origin-fall':color=((255,232,202),(244,145,87))
    for txt,fs,cols,cy,delay,font in [(main,size,color,my,.10,TITLE if s['name']=='end' else FONT),(sub,ss,((245,242,231),(221,222,229)),sy,.32,FONT)]:
        base,mask,w,h=text_layer(txt,fs,cols,font)
        assert x-w/2>=30 and x+w/2<=1890,(s['name'],w,x)
        layers.append((base,mask,x-w/2,cy-h/2,delay))
    if s['name']=='end':
        for txt,cy,fs in [('PLAY NOW ON ITCH.IO',760,40),('conze.itch.io/scroll-hunter',830,29)]:
            base,mask,w,h=text_layer(txt,fs,((255,242,201),(231,190,108)))
            layers.append((base,mask,960-w/2,cy-h/2,.65))
    return shade,layers

def compose_text(frame,s,t,shade,layers):
    if not layers:return frame
    fade=smooth(t/.3)*smooth((s['d']-t)/.35)
    sh=shade.copy();sh.putalpha(sh.getchannel('A').point(lambda v:round(v*fade)))
    frame=Image.alpha_composite(frame.convert('RGBA'),sh)
    for base,mask,x,y,delay in layers:
        enter=smooth((t-delay)/.40);opacity=enter*smooth((s['d']-t)/.35)
        if opacity<=0:continue
        a=base.copy()
        # A single highlight moves inside the glyphs only; no moving lines or frames.
        sweep=(t-delay-.35)/.95
        if 0<sweep<1:
            xx=np.arange(a.width)[None,:];center=-120+sweep*(a.width+240)
            band=np.exp(-((xx-center)/72)**2)*mask*.48
            hi=Image.new('RGBA',a.size,(255,255,244,0));hi.putalpha(Image.fromarray(band.astype('uint8'),'L'))
            a=Image.alpha_composite(a,hi)
        a.putalpha(a.getchannel('A').point(lambda v:round(v*opacity)))
        frame.alpha_composite(a,(round(x),round(y+14*(1-enter)**3)))
    return frame.convert('RGB')

for s in shots: typography(s)
run(['-ss','21.5','-i',RAW,'-frames:v','1',OUT/'reward.png'])
stats=[]
for i,s in enumerate(shots):
    dest=OUT/f'{i:02d}-{s["name"]}.mp4';statpath=OUT/f'{i:02d}-stability.json'
    if dest.exists() and statpath.exists():stats.append(json.loads(statpath.read_text()));continue
    shade,layers=typography(s);n=round(s['d']*30);st=Stabilizer();samples=[]
    if 'src' in s:
        dec=subprocess.Popen([str(FF),'-v','error','-ss',str(s['src']),'-i',str(RAW),'-frames:v',str(n),'-vf','setpts=PTS-STARTPTS,fps=30','-f','rawvideo','-pix_fmt','rgb24','-'],stdout=subprocess.PIPE)
        still=None
    else:
        p=OUT/'reward.png' if s['still']=='reward' else ROOT/'Assets/_Project/UI/Story'/s['still']
        still=ImageOps.fit(Image.open(p).convert('RGB'),(1920,1080),method=Image.Resampling.LANCZOS)
    enc=subprocess.Popen([str(FF),'-v','error','-y','-f','rawvideo','-pix_fmt','rgb24','-s','1920x1080','-r','30','-i','-','-an','-c:v','libx264','-threads','3','-preset','fast','-crf','18','-pix_fmt','yuv420p',str(dest)],stdin=subprocess.PIPE)
    for j in range(n):
        t=j/30;dx=dy=0.
        if still is None:
            b=dec.stdout.read(1920*1080*3);assert len(b)==1920*1080*3,(s['name'],j,len(b))
            im=Image.frombytes('RGB',(1920,1080),b)
            roi=im.crop((0,0,480,104)).convert('L');dx,dy,score=st.measure(roi)
            if abs(dx)+abs(dy)>.3:
                corrected=roi.transform(roi.size,Image.Transform.AFFINE,(1,0,dx,0,1,dy),resample=Image.Resampling.BICUBIC)
                rx,ry,_=st.measure(corrected);samples.append(dict(frame=j,dx=dx,dy=dy,residual=math.hypot(rx,ry)))
        else:im=still
        z=s['fixedZoom']
        # Still art has only a 1.2% floating-point push-in. Gameplay framing is constant.
        if s.get('pos')=='story':z+=.012*smooth(j/max(1,n-1))
        cx,cy=s['c'];w=1920/z;h=1080/z
        x=max(0,min(1920-w,1920*cx-w/2));y=max(0,min(1080-h,1080*cy-h/2))
        im=im.transform((1920,1080),Image.Transform.AFFINE,(1/z,0,x+dx,0,1/z,y+dy),resample=Image.Resampling.BICUBIC)
        im=compose_text(im,s,t,shade,layers)
        if j==min(45,n//2):im.save(OUT/f'{i:02d}-preview.png')
        enc.stdin.write(im.tobytes())
    enc.stdin.close();assert enc.wait()==0
    if still is None:dec.stdout.close();assert dec.wait()==0
    report=dict(shot=s['name'],correctedFrames=len(samples),maxOriginalPixels=max([math.hypot(q['dx'],q['dy']) for q in samples] or [0]),maxResidualPixels=max([q['residual'] for q in samples] or [0]),samples=samples)
    statpath.write_text(json.dumps(report,indent=2),encoding='utf8');stats.append(report)
    print('Rendered',s['name'],'corrected',len(samples),'frames',flush=True)

args=[]
for i,s in enumerate(shots):args+=['-i',OUT/f'{i:02d}-{s["name"]}.mp4']
filters=[];last='0:v';length=shots[0]['d']
for i in range(1,len(shots)):
    # Brief fades through dark avoid the double-image drift of overlapping zooms.
    filters.append(f'[{last}][{i}:v]xfade=transition=fadeblack:duration=0.3:offset={length-.3:.4f}[v{i}]')
    last=f'v{i}';length+=shots[i]['d']-.3
assert abs(length-60)<.001
filters.append(f'[{last}]fade=t=in:st=0:d=0.35,fade=t=out:st=59.1:d=0.9,format=yuv420p[final]')
if not (OUT/'picture.mp4').exists():
    run([*args,'-filter_complex_threads','1','-filter_complex',';'.join(filters),'-map','[final]','-an','-t','60','-r','30','-c:v','libx264','-threads','3','-preset','fast','-crf','18',OUT/'picture.mp4'])
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
events=json.loads((ARCHIVE/'english-take/sounds.json').read_text())['events']; used=[]
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

run(['-i',OUT/'picture.mp4','-i',OUT/'mix.wav','-map','0:v','-map','1:a','-c:v','copy','-af','loudnorm=I=-16:TP=-1.5:LRA=11,afade=t=out:st=58:d=2','-c:a','aac','-b:a','192k','-ar','48000','-t','60','-movflags','+faststart',OUT/'ScrollHunter_Trailer_EN_v4_60s.mp4'])
plan=dict(duration=60,shots=shots,stabilization=stats,transitions='0.3-second fade through dark; no cross-dissolved moving frames',notes='Fixed gameplay framing; HUD-based subpixel translation correction. 1.2% floating-point push-in on intro art only. Animated gradient lettering, single glyph-only light sweep, no decorative lines. Fresh English gameplay capture with screen shake disabled temporarily. English trailer copy and English in-game UI. Original game and saves unchanged.')
(EVIDENCE/'english-plan.json').write_text(json.dumps(plan,ensure_ascii=False,indent=2),encoding='utf8')
def stamp(x):
    ms=round(x*1000);return f'{ms//3600000:02}:{ms//60000%60:02}:{ms//1000%60:02},{ms%1000:03}'
subs=[]
for s in shots:
    if not s['text']:continue
    subs.append(f'{len(subs)+1}\n{stamp(s["start"]+.1)} --> {stamp(s["start"]+s["d"]-.35)}\n{s["text"]}')
(OUT/'ScrollHunter_Trailer_EN_v4_60s.srt').write_text('\n\n'.join(subs)+'\n',encoding='utf-8-sig')
print('FINISHED',flush=True)
