from pathlib import Path
import subprocess, json, re, hashlib, io, shutil
from PIL import Image, ImageDraw
import numpy as np

evidence=Path(__file__).resolve().parent
work=Path('C:/Users/MBC-501-19/AppData/Local/Temp/ScrollHunter_TrailerSource_20261017/english')
ff=Path('C:/Users/MBC-501-19/AppData/Local/Temp/ScrollHunter_TrailerTools/imageio_ffmpeg/binaries/ffmpeg-win-x86_64-v7.1.exe')
video=work/'ScrollHunter_Trailer_EN_v4_60s.mp4'
meta=subprocess.run([str(ff),'-hide_banner','-i',str(video)],capture_output=True,text=True).stderr
assert 'Duration: 00:01:00.00' in meta,meta
assert '1920x1080' in meta and '30 fps' in meta and 'Audio: aac' in meta,meta
decode=subprocess.run([str(ff),'-v','error','-i',str(video),'-progress','pipe:1','-f','null','-'],capture_output=True,text=True,check=True)
assert not decode.stderr.strip(),decode.stderr
assert re.findall(r'frame=(\d+)',decode.stdout)[-1]=='1800',decode.stdout[-1500:]
(evidence/'english-validation.txt').write_text(meta+'\nFull decode: success, 1800 frames, no errors.\n'+decode.stdout[-400:],encoding='utf8')
audio=subprocess.run([str(ff),'-hide_banner','-nostats','-i',str(video),'-vn','-af','ebur128=peak=true','-f','null','-'],capture_output=True,text=True,check=True)
(evidence/'english-audio.txt').write_text(audio.stderr[-1600:],encoding='utf8')
plan=json.loads((evidence/'english-plan.json').read_text(encoding='utf8'))
times=[s['start']+min(1.5,s['d']/2) for s in plan['shots']]
im=Image.new('RGB',(1536,318*((len(times)+2)//3)),(8,10,16));d=ImageDraw.Draw(im)
for i,t in enumerate(times):
    data=subprocess.check_output([str(ff),'-v','error','-ss',str(t),'-i',str(video),'-frames:v','1','-vf','scale=512:288','-f','image2pipe','-vcodec','png','-'])
    im.paste(Image.open(io.BytesIO(data)),(i%3*512,i//3*318));d.text((i%3*512+6,i//3*318+293),f'{t:.1f}s - {plan["shots"][i]["name"]}',fill='white')
im.save(evidence/'english-contact.png')
# A static deck cut must not drift or resize between frames.
frames=[]
for t in (.8,2.3,3.7):
    b=subprocess.check_output([str(ff),'-v','error','-ss',str(t),'-i',str(work/'10-deck.mp4'),'-frames:v','1','-f','rawvideo','-pix_fmt','gray','-'])
    frames.append(np.frombuffer(b,dtype=np.uint8).astype(float))
mae=[float(np.mean(abs(a-frames[0]))) for a in frames[1:]]
assert max(mae)<.6,mae
samples=[v for s in plan['stabilization'] for v in s['samples']]
stability=dict(deckFrameMeanAbsoluteDifference=mae,correctedFrames=len(samples),maximumRecordedShift=max([float(np.hypot(v['dx'],v['dy'])) for v in samples] or [0]),maximumResidualShift=max([v['residual'] for v in samples] or [0]))
(evidence/'english-stability.json').write_text(json.dumps(stability,indent=2),encoding='utf8')
desktop=Path('C:/Users/MBC-501-19/Desktop/ScrollHunter_Trailer_EN')
desktop.mkdir(exist_ok=True)
shutil.copy2(Path('C:/Users/MBC-501-19/Desktop/ScrollHunter_Trailer_KO/Game_Asset_Credits.txt'),desktop/'Game_Asset_Credits.txt')
dest=desktop/video.name
shutil.copy2(video,dest)
srt=work/'ScrollHunter_Trailer_EN_v4_60s.srt'
caption=srt.read_text(encoding='utf-8-sig')
if 'PLAY NOW ON ITCH.IO' not in caption: srt.write_text(caption.rstrip()+'\nPLAY NOW ON ITCH.IO\nconze.itch.io/scroll-hunter\n',encoding='utf-8-sig')
assert not re.search(r'[가-힣]',srt.read_text(encoding='utf-8-sig'))
shutil.copy2(srt,desktop/srt.name)
shutil.copy2(evidence/'english-contact.png',desktop/'EN_v4_장면미리보기.png')
sha=hashlib.sha256(video.read_bytes()).hexdigest()
assert hashlib.sha256(dest.read_bytes()).hexdigest()==sha
data=dict(video=str(dest),bytes=dest.stat().st_size,sha256=sha,duration=60,width=1920,height=1080,fps=30,decodedFrames=1800,koreanVersionPreserved=Path('C:/Users/MBC-501-19/Desktop/ScrollHunter_Trailer_KO/ScrollHunter_Trailer_KO_v4_60s.mp4').exists())
(evidence/'english-delivery.json').write_text(json.dumps(data,indent=2,ensure_ascii=False),encoding='utf8')
(desktop/'EN_v4_영상 안내.txt').write_text('Scroll Hunter 트레일러 재편집본\n\n60초 · 1920×1080 · 30fps · 영문 문구·게임 UI 영어 · H.264/AAC\n인트로 일러스트 5장과 전투 하이라이트 구성 유지. 전투 구도 고정·피격 화면 이동 보정·짧은 암전 전환. 강조 문구와 빛 효과·등장 애니메이션 적용. 시작 화면·장식선 제외.\n영어 UI로 다시 촬영한 게임 플레이와 게임 내 일러스트로 편집한 홍보 영상입니다. 연속된 한 런의 기록은 아닙니다.\n음원 출처는 Game_Asset_Credits.txt 참고.\nMP4에 문구가 포함되어 있습니다. SRT는 별도 자막 입력용이며 같이 켜면 문구가 중복됩니다.\n이전 초안은 그대로 보존했습니다.\n',encoding='utf-8-sig')
print(json.dumps(data,ensure_ascii=False),flush=True)
