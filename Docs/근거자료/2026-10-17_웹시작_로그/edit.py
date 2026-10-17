from pathlib import Path
import shutil
root=Path(__file__).resolve().parents[3]
template=root/'Assets/WebGLTemplates/ScrollHunter'
def replace(path, old, new):
    data=path.read_bytes(); a=old.replace('\n','\r\n').encode(); b=new.replace('\n','\r\n').encode()
    if a not in data: a=old.encode(); b=new.encode()
    assert data.count(a)==1, (path,old[:60])
    path.write_bytes(data.replace(a,b))
p=template/'index.html'
replace(p,'h1{font-size:clamp(28px,5vw,64px);font-weight:normal;letter-spacing:.08em;margin:0}', '''@font-face{font-family:Cinzel;src:url('Cinzel-Bold.ttf') format('truetype');font-weight:700;font-display:swap}
h1{font-family:Cinzel,Georgia,serif;font-size:clamp(28px,5vw,64px);font-weight:700;color:#e6c98f;margin:0 0 clamp(20px,4vw,48px);text-shadow:0 3px 4px #000;white-space:nowrap}
.launch-controls{display:flex;align-items:center;justify-content:center;flex-wrap:nowrap;gap:clamp(12px,3vw,28px);max-width:96%}
#play{white-space:nowrap;padding:10px clamp(16px,3vw,40px)}''')
replace(p,'.language-choice{display:flex;align-items:center;gap:12px;font:14px sans-serif}', '.language-choice{display:flex;align-items:center;gap:8px;font:14px sans-serif;white-space:nowrap}')
replace(p,'      <div class="language-choice">','      <div class="launch-controls">\n      <div class="language-choice">')
replace(p,'      <button id="play" type="button">Play</button>','      <button id="play" type="button">Play</button>\n      </div>')
replace(p,"showBanner:(message,type)=>{if(type==='error'){status.textContent=message;console.error(message);}}", "showBanner:(message,type)=>{if(type==='error'){status.textContent=message;console.error(message);}else if(type==='warning'){console.warn(message);}}")
shutil.copyfile(root/'Assets/_Project/Fonts/Cinzel/Cinzel-Bold.ttf',template/'Cinzel-Bold.ttf')
shutil.copyfile(root/'Assets/_Project/Fonts/Cinzel/OFL.txt',template/'OFL_Cinzel.txt')
p=root/'Assets/_Project/Scripts/BattleFlow.cs'
replace(p,'    private void Awake()','''#if UNITY_WEBGL && !UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ConfigureWebLogging()
    {
        // 웹에서는 보상·편성 등 일반 기록도 숨기고 경고·오류·예외는 남긴다.
        Debug.unityLogger.filterLogType = LogType.Warning;
    }
#endif

    private void Awake()''')
print('Updated web template and WebGL log filter.')
