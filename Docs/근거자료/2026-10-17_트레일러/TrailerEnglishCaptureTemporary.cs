#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

// 촬영 후 Assets에서 제거. 씬/에셋/실제 저장은 쓰지 않는다.
public sealed class TrailerEnglishCaptureTemporary : MonoBehaviour
{
    const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
    public static string Status="idle";
    string folder; int frame; int total; System.Diagnostics.Process encoder;
    BattleFlow flow; DeckSystem deck; EnemyManager enemies; CostSystem cost; Player player;
    InheritanceSave realSave; GameLanguage oldLanguage; GameAudio audio;
    List<SkillData> originalDeck; List<SoundEvent> sounds=new List<SoundEvent>();
    [Serializable] public class SoundEvent { public float time, volume; public string path; }
    [Serializable] public class Events { public List<SoundEvent> events; }
    static object Get(object o,string n)=>o.GetType().GetField(n,Flags).GetValue(o);
    static void Set(object o,string n,object v)=>o.GetType().GetField(n,Flags).SetValue(o,v);
    static object Call(object o,string n,params object[] args)=>o.GetType().GetMethod(n,Flags).Invoke(o,args);
    public void Begin(string output,int frames)
    {
        folder=output;total=frames; Directory.CreateDirectory(folder);
        flow=FindFirstObjectByType<BattleFlow>();deck=FindFirstObjectByType<DeckSystem>();
        enemies=FindFirstObjectByType<EnemyManager>();cost=FindFirstObjectByType<CostSystem>();player=FindFirstObjectByType<Player>();
        realSave=flow.Inheritance;oldLanguage=GameText.Current;originalDeck=deck.CopyStartingDeck();
        flow.ReturnToTitle();
        Set(flow,"inheritance",new InheritanceSave(Path.Combine(folder,"temporary-save.json"),(BattleRewardOption[])Get(flow,"rewardPool"),originalDeck));
        GameText.SetLanguage(GameLanguage.English);LanguageFonts.ApplyAll();
        var menu=FindFirstObjectByType<MainMenuUI>();
        if(menu!=null)Set(menu,"<ScreenShakeEnabled>k__BackingField",false);
        flow.GetComponent<TutorialFlow>().enabled=false;flow.GetComponent<StoryUI>().enabled=false;
        audio=FindFirstObjectByType<GameAudio>();audio.Played+=OnSound;
        Assembly.Load("Assembly-CSharp-Editor").GetType("UiCapture").GetMethod("SetGameViewSize").Invoke(null,new object[]{1920,1080});
        EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView")).Focus();
        Time.captureFramerate=30;Application.runInBackground=true;EditorApplication.isPaused=false;
        StartCoroutine(Capture());
    }
    void OnSound(SoundCue cue,AudioClip clip,UnityEngine.Audio.AudioMixerGroup group)
    { sounds.Add(new SoundEvent {time=frame/30f,volume=cue.volume,path=AssetDatabase.GetAssetPath(clip)}); }
    void Battle(int index,bool advanced)
    {
        Call(flow,"ClearRunRewards");
        var cards=new List<SkillData>(originalDeck);
        if(advanced)
        {
            foreach(var guid in AssetDatabase.FindAssets("t:SkillData",new[]{"Assets/_Project/Data"}))
            {
                var s=AssetDatabase.LoadAssetAtPath<SkillData>(AssetDatabase.GUIDToAssetPath(guid));
                if((string)Get(s,"displayName")=="저지먼트")cards[0]=s;
                if((string)Get(s,"displayName")=="생츄어리")cards[1]=s;
            }
        }
        Set(flow,"runDeck",cards);Call(flow,"BeginBattle",index,true,null);
        cost.EnsureTutorialCost(10);Time.timeScale=1;
    }
    void Act()
    {
        if(frame==90)Battle(0,false);
        if(frame==480)foreach(var e in enemies.GetAliveEnemies())e.TakeDamage(10000);
        if(frame==555 && flow.State==BattleFlowState.BetweenBattles)flow.SelectReward(0);
        if(frame==690)Battle(1,true);
        if(frame==990)Battle(2,true);
        if(frame==1290)Battle(3,false);
        if(frame==1335 || frame==1485)
        {
            var boss=enemies.GetAliveEnemies().Find(e=>e.PhaseNumber>0);
            if(boss!=null)boss.TakeDamage(frame==1335?1250:1000);
        }
        if(frame==1710)flow.ReturnToTitle();
        if(flow.State!=BattleFlowState.Fighting || frame>=480&&frame<690)return;
        if(frame%12!=0||deck.IsLocked)return;
        var target=enemies.CurrentTarget;
        // 실제 카드 사용 경로. 코스트·피해·충전 속도 유지.
        for(int i=0;i<4;i++)
        {
            var c=deck.GetHandCard(i);if(c==null||!cost.CanAfford(c.Cost))continue;
            if(c.Category==SkillCategory.Interrupt && (target==null||!target.IsCasting||target.CurrentAttack.CastColor==CastColor.Red||target.CastProgress01<.60f))continue;
            if(deck.TryUseSlot(i))break;
        }
        if(player.CurrentHp<220)player.RequestPotionUse();
    }
    IEnumerator Capture()
    {
        yield return null;yield return new WaitForEndOfFrame();
        try
        {
            for(frame=0;frame<total;frame++)
            {
                Act();yield return new WaitForEndOfFrame();
                var tex=ScreenCapture.CaptureScreenshotAsTexture();
                if(encoder==null)
                {
                    string format=tex.format==TextureFormat.RGB24?"rgb24":"rgba";
                    string exe=Path.Combine(Path.GetTempPath(),"ScrollHunter_TrailerTools/imageio_ffmpeg/binaries/ffmpeg-win-x86_64-v7.1.exe");
                    var info=new System.Diagnostics.ProcessStartInfo(exe,"-y -loglevel error -f rawvideo -pixel_format "+format+" -video_size "+tex.width+"x"+tex.height+" -framerate 30 -i pipe:0 -vf vflip -an -c:v libx264 -preset ultrafast -crf 18 -pix_fmt yuv420p -movflags +faststart \""+Path.Combine(folder,"raw.mp4")+"\"");
                    info.UseShellExecute=false;info.CreateNoWindow=true;info.RedirectStandardInput=true;info.RedirectStandardError=true;
                    encoder=System.Diagnostics.Process.Start(info);encoder.ErrorDataReceived+=(s,e)=>{if(e.Data!=null)File.AppendAllText(Path.Combine(folder,"encoder-errors.txt"),e.Data+"\n");};encoder.BeginErrorReadLine();
                }
                byte[] bytes=tex.GetRawTextureData();encoder.StandardInput.BaseStream.Write(bytes,0,bytes.Length);Destroy(tex);
                if(frame%30==0){Status="recording "+frame+"/"+total;File.WriteAllText(Path.Combine(folder,"progress.txt"),Status);}
            }
            Status="captured";
        }
        finally
        {
            if(encoder!=null){encoder.StandardInput.Close();encoder.WaitForExit(20000);}
            File.WriteAllText(Path.Combine(folder,"sounds.json"),JsonUtility.ToJson(new Events{events=sounds},true));
            File.WriteAllText(Path.Combine(folder,"progress.txt"),Status);
            audio.Played-=OnSound;Time.captureFramerate=0;flow.ReturnToTitle();Set(flow,"inheritance",realSave);
            GameText.SetLanguage(oldLanguage);LanguageFonts.ApplyAll();
        }
    }
}
#endif
