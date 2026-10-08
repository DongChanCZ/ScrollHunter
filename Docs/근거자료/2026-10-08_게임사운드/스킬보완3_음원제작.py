from pathlib import Path
import tempfile,sys,json,numpy as np,shutil,hashlib
stage=Path(tempfile.gettempdir())/'scrollhunter-audio-round3';out=stage/'processed';out.mkdir(exist_ok=True)
sys.path.insert(0,str(Path(tempfile.gettempdir())/'scrollhunter-audio-tuning/audio_lib'));import soundfile as sf
sr=44100;meta=[]
def load(rel):
 x,r=sf.read(stage/rel,always_2d=True);x=x.mean(axis=1)
 if r!=sr:x=np.interp(np.arange(round(len(x)*sr/r))*r/sr,np.arange(len(x)),x)
 return x

def fade(x,start=.002,end=.035):
 x=x.copy();a=min(len(x)//2,int(start*sr));b=min(len(x)//2,int(end*sr))
 if a:x[:a]*=np.linspace(0,1,a)
 if b:x[-b:]*=np.linspace(1,0,b)
 return x

def save(name,x,source,edit,volume):
 assert np.isfinite(x).all() and abs(x).max()<1
 sf.write(out/(name+'.wav'),x,sr,subtype='PCM_16')
 rms=float(np.sqrt(np.mean(x*x)));peak=float(abs(x).max())
 meta.append(dict(file=name+'.wav',source=source,edit=edit,seconds=len(x)/sr,peak=peak,rms=rms,volume=volume,effective_rms=rms*volume))

# One original explosion, no repeats, pitch changes or layers.
x=load('candidates/explosion2.ogg')
save('sh_impact_explosion',x,'EZduzziteh / Explosions / explosion2.ogg','Mono decode only; complete original explosion',.95)
# A short magical strike pair; no weapon/knife samples and no nonlinear saturation.
x=load('spell/magical_3.ogg');pulse=fade(x[:int(.25*sr)],.003,.065)
y=np.zeros(int(.57*sr));y[:len(pulse)]+=pulse;y[int(.23*sr):int(.23*sr)+len(pulse)]+=pulse*.94
y*=.68/max(abs(y));y=fade(y,.002,.04)
save('sh_cutter_magic_claw',y,'JaggedStone / Magic Spell SFX / magical_3.ogg','First 0.25s, fades, two strikes 0 and 0.23s, linear peak 0.68',.85)
# First natural thunder strike; no generated percussion or harmonic layers.
x=load('candidates/446753_1790434-hq.mp3')[int(1.10*sr):int(4.90*sr)]
x=fade(x,.003,1.2);x*=.78/max(abs(x));x=np.r_[np.zeros(int(.08*sr)),x]
save('sh_judgment_thunder',x,'BlueDelta / Heavy Thunder Strike - no Rain - QUADRO / HQ MP3 preview','1.10-4.90s, 1.2s fade tail, 0.08s beam lead, linear peak 0.78',.9)
# Gentle healing tail becomes a continuous loop without the original sharp opening.
x=load('magic/Healing Full.wav')[int(.55*sr):int(1.85*sr)]
n=int(.20*sr);t=np.linspace(0,1,n);over=x[-n:]*(1-t)+x[:n]*t;x=np.r_[over,x[n:-n]]
x*=min(.115/(np.sqrt(np.mean(x*x))+1e-10),.6/(max(abs(x))+1e-10))
save('sh_sanctuary_soft_loop',x,'ViRiX Dreamcore (David Mckee) / Magic SFX Sample / Healing Full.wav','0.55-1.85s tail, 0.20s loop overlap, linear RMS target 0.115 / peak max 0.6',.6)
root=Path(r'C:\Users\MBC-501-19\Documents\ScrollHunter');audio=root/'Assets/_Project/Audio/Magic'
before=stage/'before';before.mkdir(exist_ok=True)
for rel in ['Assets/_Project/Editor/GameAudioSetup.cs','Assets/_Project/Audio/GameSoundLibrary.asset','Assets/_Project/Tests/Editor/GameAudioChecks.cs']:
 p=root/rel; dest=before/p.name
 if not dest.exists():shutil.copy2(p,dest)
for p in out.glob('*.wav'):
 dest=audio/p.name
 if dest.exists():raise RuntimeError('New path already exists: '+str(dest))
 shutil.copy2(p,dest)
# Copy only selected originals for traceable re-editing, without unrelated pack files.
for rel,name in [('candidates/explosion2.ogg','ez_explosion2.ogg'),('spell/magical_3.ogg','js_magical3.ogg'),('magic/Healing Full.wav','virix_healing_full.wav')]:
 dest=audio/name
 if dest.exists():raise RuntimeError('Source path exists: '+str(dest))
 shutil.copy2(stage/rel,dest)
report={'processing':'linear gain/fades/crop/overlap only; no saturation, compressor or synthesis','files':meta,'prior_sanctuary_effective_rms':.19*.6}
(stage/'processing.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf8')
print(json.dumps(report,ensure_ascii=False,indent=2))