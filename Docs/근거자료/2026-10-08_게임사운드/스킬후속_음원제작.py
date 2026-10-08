from pathlib import Path
import tempfile,sys,json,numpy as np
stage=Path(tempfile.gettempdir())/'scrollhunter-skill-audio';stage.mkdir(exist_ok=True)
sys.path.insert(0,str(Path(tempfile.gettempdir())/'scrollhunter-audio-tuning/audio_lib'));import soundfile as sf
root=Path(r'C:\Users\MBC-501-19\Documents\ScrollHunter\Assets\_Project\Audio');out=stage/'processed';out.mkdir(exist_ok=True)
sr=44100; rng=np.random.default_rng(1008);rows=[]
def read(path):
 x,rate=sf.read(root/path,dtype='float32',always_2d=True);x=x.mean(axis=1).astype(np.float64)
 if rate!=sr:x=np.interp(np.arange(round(len(x)*sr/rate))*rate/sr,np.arange(len(x)),x)
 return x
def fade(x,a=.003,b=.03):
 x=x.copy();n=min(len(x)//2,int(a*sr));m=min(len(x)//2,int(b*sr))
 if n:x[:n]*=np.linspace(0,1,n)
 if m:x[-m:]*=np.linspace(1,0,m)
 return x
def onset(x):
 env=np.convolve(np.abs(x),np.ones(128)/128,mode='same');active=np.flatnonzero(env>max(env.max()*.045,.0001));return x[max(0,int(active[0])-128):] if len(active) else x
def sized(x,seconds):return x[:int(seconds*sr)]
def mix(parts,seconds):
 y=np.zeros(round(seconds*sr))
 for x,t,g in parts:
  i=round(t*sr);n=min(len(x),len(y)-i)
  if n>0:y[i:i+n]+=x[:n]*g
 return y
def body(seconds,f0,f1):
 t=np.arange(round(seconds*sr))/sr;phase=2*np.pi*(f1*t+(f0-f1)*.04*(1-np.exp(-t/.04)))
 return np.sin(phase)*np.exp(-t/.13)
def finish(name,x,rms=.22,peak=.9,loop=False):
 if loop:x=np.roll(x,-int(np.argmin(np.abs(x[:int(.1*sr)]))))
 else:x=fade(x)
 # Smooth peak compression raises quiet body while leaving headroom.
 x=np.tanh(x/(np.max(np.abs(x))+1e-8)*2.0)
 x*=min(rms/(np.sqrt(np.mean(x*x))+1e-8),peak/(np.max(np.abs(x))+1e-8))
 sf.write(out/(name+'.wav'),x,sr,subtype='PCM_16')
 bins=[float(np.sqrt(np.mean(v*v))) for v in np.array_split(x,10)]
 rows.append(dict(file=name+'.wav',seconds=len(x)/sr,peak=float(np.max(np.abs(x))),rms=float(np.sqrt(np.mean(x*x))),rms_by_tenth=bins,loop_seam=float(abs(x[0]-x[-1]))))
# Immediate, audible slash; shared dagger/raider source files remain untouched.
dag=fade(sized(onset(read('Weapons/jc_dagger_swing_01.wav')),.25))
knife=fade(sized(onset(read('Weapons/knife_slice.ogg')),.35))
finish('sh_magic_cutter',mix([(dag,0,1),(knife,.018,.55)],.39),.23)
# Three short explosions inside one cue, because all three gameplay hits resolve in one frame.
fire=fade(sized(onset(read('Magic/jc_fire_impact_01.wav')),.27),.002,.06)
fire=fire/(max(abs(fire))+1e-8)
pop=mix([(fire,0,.7),(body(.28,150,55),0,.4)],.28)
short_pop=fade(sized(pop,.085),.002,.025)
finish('sh_magic_impact_triple',mix([(short_pop,0,.85),(short_pop,.105,.95),(pop,.21,1.1)],.55),.27)
# 0.08 s descent followed by a low strike and bright holy resonance; matches the beam flash.
chime=read('Magic/jc_healing_chime_02.wav')[int(.8*sr):int(2.2*sr)]
chime=chime/(max(abs(chime))+1e-8)
strike=fade(sized(onset(read('Impacts/soft_heavy_01.ogg')),.35))
strike=strike/(max(abs(strike))+1e-8)
t=np.arange(int(.95*sr))/sr
ring=(np.sin(2*np.pi*1046.5*t)+.4*np.sin(2*np.pi*1568*t))*np.exp(-t/0.24)*.13
lead=rng.normal(0,1,int(.08*sr))*np.linspace(0,.08,int(.08*sr))
finish('sh_judgment_strike',mix([(lead,0,1),(strike,.08,.8),(body(.5,125,48),.08,.45),(ring,.08,1),(fade(chime,.01,.3),.08,.16)],1.5),.22)
# Seamless, sustained major chord: soft harmonics + existing healing chime texture.
t=np.arange(4*sr)/sr;pad=np.zeros_like(t)
for f in [261.5,329.5,392,523]:
 for detune in [-.25,.25]:
  for h,weight in [(1,1),(2,.32),(3,.12),(4,.05)]:
   pad+=weight*np.sin(2*np.pi*(f+detune)*h*t+rng.uniform(0,2*np.pi))
pad/=np.max(np.abs(pad));pad*=.72+.12*np.sin(2*np.pi*.25*t)
# Periodic chime excerpts; tails overlap instead of leaving silence.
tex=fade(chime,.12,.3);tex/=max(abs(tex));layer=np.zeros_like(t)
for offset in [0,1,2,3]:
 idx=(np.arange(len(tex))+int(offset*sr))%len(layer);layer[idx]+=tex*.12
finish('sh_sanctuary_holy_loop',pad+layer,.19,.72,True)
# Louder dedicated dark blasts; do not alter other users of the source clips.
for old,new in [('lrsf_spell_deep.mp3','sh_dark_blast'),('lrsf_spell_deep_02.mp3','sh_darkhole_blast')]:
 x=onset(read('Magic/'+old));x=np.sign(x)*np.abs(x)**.66
 finish(new,x,.44,.92)
(stage/'processing.json').write_text(json.dumps(rows,indent=2),encoding='utf-8');print(json.dumps(rows,indent=2))

