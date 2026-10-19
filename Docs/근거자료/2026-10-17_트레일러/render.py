from pathlib import Path
import subprocess,json,os,shutil,wave
import numpy as np
from PIL import Image,ImageDraw,ImageFont
work=Path(__file__).resolve().parent;root=work.parents[2]
ff=Path(os.environ['TEMP'])/'ScrollHunter_TrailerTools/imageio_ffmpeg/binaries/ffmpeg-win-x86_64-v7.1.exe'
raw=work/'take01/raw.mp4';out=work/'edit';out.mkdir(exist_ok=True)
font=root/'Assets/_Project/Fonts/GmarketSansOTF/GmarketSansMedium.otf'
def run(args):
    subprocess.run([str(ff),'-hide_banner','-loglevel','error','-y',*map(str,args)],check=True)
shots=[('boss-hook',51,3),('title',0,3),('story',None,5),('forest',3,13),('reward',18,5),('leader',23,4),('party',33,10),('boss',43,14),('end',57,3)]
captions=[(0,3,'끝까지 살아남을 수 있을까?'),(3,6,'실시간 로그라이크 카드 전투'),(6,11,'비전 스크롤의 힘을 찾아'),(11,17,'적의 시전을 읽고'),(17,24,'공격과 차단의 순간을 선택하라'),(24,29,'보상으로 나만의 덱을 완성하라'),(29,33,'강력한 스킬로 돌파하라'),(33,43,'몰려드는 적을 상대하라'),(43,50,'숲 깊은 곳, 마법사가 기다린다'),(50,57,'강화되는 위협을 넘어, 끝까지'),(57,60,'SCROLL HUNTER · itch.io에서 플레이')]
for n,(start,end,text) in enumerate(captions):
    im=Image.new('RGBA',(1920,1080),(0,0,0,0));d=ImageDraw.Draw(im)
    d.rectangle((0,960,1920,1080),fill=(8,10,16,255));d.line((490,971,1430,971),fill=(170,140,78,180),width=2)
    f=ImageFont.truetype(str(font),44 if n<10 else 39)
    box=d.textbbox((0,0),text,font=f);width=box[2]-box[0]
    d.text(((1920-width)/2,988 if n==10 else 1004),text,font=f,fill=(242,229,204,255),anchor='lt')
    if n==10:
        url='conze.itch.io/scroll-hunter'; f2=ImageFont.truetype(str(font),24); b=d.textbbox((0,0),url,font=f2)
        d.text(((1920-b[2])/2,1040),url,font=f2,fill=(195,170,111,255))
    im.save(out/f'caption-{n}.png')
cursor=0;mapping=[]
for n,(name,source,duration) in enumerate(shots):
    dest=out/f'{n:02d}-{name}.mp4'
    if source is None:
        args=['-loop','1','-framerate','30','-i',root/'Assets/_Project/UI/Story/Intro05_Traveler.png']
        filt="scale=2100:-1,crop=1920:1080:(iw-ow)/2:(ih-oh)/2,zoompan=z='1+0.00035*on':x='iw/2-iw/zoom/2':y='ih/2-ih/zoom/2':d=1:s=1920x1080:fps=30,"
    else:
        args=['-ss',source,'-i',raw];filt=''
    filt+=f'scale=1706:960,pad=1920:1080:107:0:color=0x080a10,setsar=1,fade=t=in:st=0:d=0.15,fade=t=out:st={duration-.15}:d=0.15'
    run([*args,'-t',duration,'-vf',filt,'-an','-r','30','-c:v','libx264','-preset','fast','-crf','19','-pix_fmt','yuv420p',dest])
    mapping.append(dict(name=name,source=source,start=cursor,duration=duration));cursor+=duration
    print('Rendered',name,flush=True)
assert cursor==60
(out/'concat.txt').write_text('\n'.join("file '"+f'{n:02d}-{name}.mp4'+"'" for n,(name,_,_) in enumerate(shots)),encoding='utf8')
run(['-f','concat','-safe','0','-i',out/'concat.txt','-c','copy',out/'picture.mp4'])
# 実際の再生イベントから効果音を再配置し、別トラックのBGMと混ぜる。
rate=48000;total=60*rate;mix=np.zeros((total,2),dtype=np.float32);cache={}
def decode(p):
    key=str(p)
    if key not in cache:
        rawdata=subprocess.check_output([str(ff),'-v','error','-i',key,'-f','f32le','-ac','2','-ar',str(rate),'-'])
        cache[key]=np.frombuffer(rawdata,dtype='<f4').reshape(-1,2)
    return cache[key]
music=decode(root/'Assets/_Project/Audio/Music/bgm_boss_evil_awaits.wav')
music=np.tile(music,(int(np.ceil(total/len(music))),1))[:total].copy()*.30
music[:rate*2]*=np.linspace(0,1,rate*2)[:,None];music[-rate*3:]*=np.linspace(1,0,rate*3)[:,None];mix+=music
events=json.loads((work/'take01/sounds.json').read_text())['events'];mixed=[]
for shot in mapping:
    if shot['source'] is None:continue
    for event in events:
        rel=event['time']-shot['source']
        if not 0<=rel<shot['duration'] or not event['path']:continue
        t=shot['start']+rel;p=root/event['path'];audio=decode(p)
        count=min(len(audio),int((shot['duration']-rel)*rate),int(3.5*rate))
        start=int(t*rate);count=min(count,total-start)
        if count<=0:continue
        a=audio[:count].copy()*min(event['volume'],1)*.6
        edge=min(480,count//2);a[:edge]*=np.linspace(0,1,edge)[:,None];a[-edge:]*=np.linspace(1,0,edge)[:,None]
        mix[start:start+count]+=a;mixed.append(dict(time=t,path=event['path']))
peak=float(np.max(np.abs(mix)));mix*=min(1,.94/max(peak,.001))
with wave.open(str(out/'mix.wav'),'wb') as w:
    w.setnchannels(2);w.setsampwidth(2);w.setframerate(rate);w.writeframes((mix*32767).astype('<i2').tobytes())
args=['-i',out/'picture.mp4','-i',out/'mix.wav'];filters=[];last='0:v'
for n,(a,b,_) in enumerate(captions):
    args+=['-loop','1','-i',out/f'caption-{n}.png'];label=f'v{n}'
    filters.append(f'[{last}][{n+2}:v]overlay=enable=\'gte(t,{a})*lt(t,{b})\':shortest=1[{label}]');last=label
filters.append(f'[{last}]fade=t=in:st=0:d=0.35,fade=t=out:st=59.5:d=0.5[video]')
filters.append('[1:a]loudnorm=I=-16:TP=-1.5:LRA=11,afade=t=out:st=58:d=2[audio]')
run([*args,'-filter_complex',';'.join(filters),'-map','[video]','-map','[audio]','-t','60','-r','30','-c:v','libx264','-preset','fast','-crf','19','-pix_fmt','yuv420p','-c:a','aac','-b:a','192k','-ar','48000','-movflags','+faststart',work/'ScrollHunter_Trailer_KO_60s.mp4'])
def stamp(t):return f'00:{int(t)//60:02d}:{int(t)%60:02d},000'
(work/'ScrollHunter_Trailer_KO_60s.srt').write_text('\n\n'.join(f'{n+1}\n{stamp(a)} --> {stamp(b)}\n{text}' for n,(a,b,text) in enumerate(captions))+'\n',encoding='utf-8-sig')
(work/'edit-plan.json').write_text(json.dumps(dict(duration=60,shots=mapping,captions=captions,soundEvents=len(mixed),preMixPeak=peak),ensure_ascii=False,indent=2),encoding='utf8')
print('Finished 60s 1080p30 Korean trailer.',flush=True)
