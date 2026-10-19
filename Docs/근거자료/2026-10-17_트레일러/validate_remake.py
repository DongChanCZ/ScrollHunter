from pathlib import Path
import subprocess, json, re, hashlib, io, shutil
from PIL import Image, ImageDraw

evidence=Path(__file__).resolve().parent
work=Path('C:/Users/MBC-501-19/AppData/Local/Temp/ScrollHunter_TrailerSource_20261017/remake')
ff=Path('C:/Users/MBC-501-19/AppData/Local/Temp/ScrollHunter_TrailerTools/imageio_ffmpeg/binaries/ffmpeg-win-x86_64-v7.1.exe')
video=work/'ScrollHunter_Trailer_KO_v2_60s.mp4'
meta=subprocess.run([str(ff),'-hide_banner','-i',str(video)],capture_output=True,text=True).stderr
assert 'Duration: 00:01:00.00' in meta,meta
assert '1920x1080' in meta and '30 fps' in meta and 'Audio: aac' in meta,meta
decode=subprocess.run([str(ff),'-v','error','-i',str(video),'-progress','pipe:1','-f','null','-'],capture_output=True,text=True,check=True)
assert not decode.stderr.strip(),decode.stderr
assert re.findall(r'frame=(\d+)',decode.stdout)[-1]=='1800',decode.stdout[-1500:]
(evidence/'remake-validation.txt').write_text(meta+'\nFull decode: success, 1800 frames, no errors.\n'+decode.stdout[-400:],encoding='utf8')
audio=subprocess.run([str(ff),'-hide_banner','-nostats','-i',str(video),'-vn','-af','ebur128=peak=true','-f','null','-'],capture_output=True,text=True,check=True)
(evidence/'remake-audio.txt').write_text(audio.stderr[-1600:],encoding='utf8')
plan=json.loads((evidence/'remake-plan.json').read_text(encoding='utf8'))
times=[s['start']+min(1.5,s['d']/2) for s in plan['shots']]
im=Image.new('RGB',(1536,318*((len(times)+2)//3)),(8,10,16));d=ImageDraw.Draw(im)
for i,t in enumerate(times):
    data=subprocess.check_output([str(ff),'-v','error','-ss',str(t),'-i',str(video),'-frames:v','1','-vf','scale=512:288','-f','image2pipe','-vcodec','png','-'])
    im.paste(Image.open(io.BytesIO(data)),(i%3*512,i//3*318));d.text((i%3*512+6,i//3*318+293),f'{t:.1f}s - {plan["shots"][i]["name"]}',fill='white')
im.save(evidence/'remake-contact.png')
desktop=Path('C:/Users/MBC-501-19/Desktop/ScrollHunter_Trailer_KO')
assert desktop.is_dir()
dest=desktop/video.name
shutil.copy2(video,dest)
shutil.copy2(work/'ScrollHunter_Trailer_KO_v2_60s.srt',desktop/'ScrollHunter_Trailer_KO_v2_60s.srt')
shutil.copy2(evidence/'remake-contact.png',desktop/'v2_장면미리보기.png')
sha=hashlib.sha256(video.read_bytes()).hexdigest()
assert hashlib.sha256(dest.read_bytes()).hexdigest()==sha
data=dict(video=str(dest),bytes=dest.stat().st_size,sha256=sha,duration=60,width=1920,height=1080,fps=30,decodedFrames=1800,previousDraftPreserved=(desktop/'ScrollHunter_Trailer_KO_60s.mp4').exists())
(evidence/'remake-delivery.json').write_text(json.dumps(data,indent=2,ensure_ascii=False),encoding='utf8')
(desktop/'v2_영상 안내.txt').write_text('Scroll Hunter 트레일러 재편집본\n\n60초 · 1920×1080 · 30fps · 한글 문구 · H.264/AAC\n게임 시작 화면 제외. 카드·차단·오브 확대, 느린 보스 등장, 장면 전환, 별도 타이틀 엔딩 적용.\n게임 플레이 촬영과 게임 내 일러스트로 편집한 홍보 영상입니다. 연속된 한 런의 기록은 아닙니다.\n음원 출처는 Game_Asset_Credits.txt 참고.\nMP4에 문구가 포함되어 있습니다. SRT는 별도 자막 입력용이며 같이 켜면 문구가 중복됩니다.\n이전 초안은 그대로 보존했습니다.\n',encoding='utf-8-sig')
print(json.dumps(data,ensure_ascii=False),flush=True)
