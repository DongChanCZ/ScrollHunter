from pathlib import Path
import tempfile,sys,json,numpy as np,shutil,hashlib
root=Path(r'C:\Users\MBC-501-19\Documents\ScrollHunter');stage=Path(tempfile.gettempdir())/'scrollhunter-audio-round4';out=stage/'processed';out.mkdir(exist_ok=True);before=stage/'before';before.mkdir(exist_ok=True)
sys.path.insert(0,str(Path(tempfile.gettempdir())/'scrollhunter-audio-tuning/audio_lib'));import soundfile as sf
sr=44100;meta=[]
def load(p):
 x,r=sf.read(p,always_2d=True);x=x.mean(axis=1)
 if r!=sr:x=np.interp(np.arange(round(len(x)*sr/r))*r/sr,np.arange(len(x)),x)
 return x

def save(name,x,source,edit,volume):
 assert np.isfinite(x).all() and abs(x).max()<1
 sf.write(out/name,x,sr,subtype='PCM_16')
 step=441;env=[np.sqrt(np.mean(x[i:i+step]**2)) for i in range(0,len(x),step)]
 meta.append(dict(file=name,source=source,edit=edit,seconds=len(x)/sr,peak=float(abs(x).max()),rms=float(np.sqrt(np.mean(x*x))),strongest_10ms_at=float(np.argmax(env)*.01),volume=volume))
x=load(stage/'sword.6.ogg');x[:44]*=np.linspace(0,1,44);x[-882:]*=np.linspace(1,0,882);x*=.80/abs(x).max()
save('sh_cutter_sharp_slash.wav',x,'StarNinjas / 20 Sword Sound Effects / sword.6.ogg','Mono, 1ms/20ms edge fades, linear peak 0.80; one complete sharp slash',.90)
p=Path(tempfile.gettempdir())/'scrollhunter-audio-round3/candidates/explosion3.ogg'
x=load(p);x[:44]*=np.linspace(0,1,44);x[-4410:]*=np.linspace(1,0,4410);x*=.80/abs(x).max();x=np.r_[np.zeros(int(.06*sr)),x]
save('sh_judgment_slam.wav',x,'EZduzziteh / Explosions / explosion3.ogg','Mono, 1ms/100ms edge fades, linear peak 0.80; 60ms lead aligns strongest pulse near 80ms beam',.95)
for rel in ['Assets/_Project/Editor/GameAudioSetup.cs','Assets/_Project/Audio/GameSoundLibrary.asset']:
 src=root/rel;shutil.copy2(src,before/src.name)
audio=root/'Assets/_Project/Audio/Magic'
for p in out.glob('*.wav'):
 assert not (audio/p.name).exists();shutil.copy2(p,audio/p.name)
for src,name in [(stage/'sword.6.ogg','sn_sword_06.ogg'),(Path(tempfile.gettempdir())/'scrollhunter-audio-round3/candidates/explosion3.ogg','ez_explosion3.ogg')]:
 assert not (audio/name).exists();shutil.copy2(src,audio/name)
p=root/'Assets/_Project/Editor/GameAudioSetup.cs';s=p.read_text(encoding='utf8')
a='// 매직 커터: 단검 없이 짧은 마력 타격 2회. 매직클로는 느낌 참고이며 원본을 사용하지 않음.';b='// 매직 커터: 약탈자와 다른 날카로운 베기 1회. 실제 피해 2타는 유지.'
assert a in s;s=s.replace(a,b)
a='Cue(0.85f, 1f, 1f, 0f, "Magic/sh_cutter_magic_claw.wav")';assert a in s;s=s.replace(a,'Cue(0.9f, 1f, 1f, 0f, "Magic/sh_cutter_sharp_slash.wav")')
a='Cue(0.9f, 1f, 1f, 0f, "Magic/sh_judgment_thunder.wav")';assert a in s;s=s.replace(a,'Cue(0.95f, 1f, 1f, 0f, "Magic/sh_judgment_slam.wav")')
p.write_text(s,encoding='utf8')
evidence=root/'Docs/근거자료/2026-10-08_게임사운드';report={'files':meta,'processing':'linear only; no layers, saturation, compressor or pitch shift','sources':['https://opengameart.org/content/20-sword-sound-effects-attacks-and-clashes','https://opengameart.org/content/explosions-4'],'listening':'Not directly auditioned; source description and waveform verified'}
(evidence/'스킬보완4_음원보정.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf8')
for entry in meta:
 x=load(out/entry['file'])*entry['volume'];sf.write(evidence/('미리듣기_'+entry['file']),x,sr,subtype='PCM_16')
x=np.r_[load(out/meta[0]['file'])*meta[0]['volume'],np.zeros(sr),load(out/meta[1]['file'])*meta[1]['volume']];sf.write(evidence/'스킬보완4_미리듣기.wav',x,sr,subtype='PCM_16')
shutil.copy2(Path(__file__),evidence/'스킬보완4_음원제작.py')
print(json.dumps(report,ensure_ascii=False,indent=2))
