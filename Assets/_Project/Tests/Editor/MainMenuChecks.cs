using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;
using TMPro;

public static class MainMenuChecks
{
    private const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
    private static object Get(object o,string n)=>o.GetType().GetField(n,Hidden).GetValue(o);
    private static void Set(object o,string n,object v)=>o.GetType().GetField(n,Hidden).SetValue(o,v);
    private static void Call(object o,string n)=>o.GetType().GetMethod(n,Hidden).Invoke(o,null);
    [MenuItem("Tools/Scroll Hunter/Check Main Menu (Fresh Play)")]
    public static void Menu()=>Debug.Log(Run());
    public static string Run()
    {
        if(!Application.isPlaying)throw new Exception("Fresh Play required");
        var f=UnityEngine.Object.FindFirstObjectByType<BattleFlow>();
        var menu=f.GetComponent<MainMenuUI>();
        if(f.State!=BattleFlowState.Title||f.Inheritance==null)throw new Exception("Wait for fresh title");
        int count=0;Action<bool,string> check=(ok,label)=>{if(!ok)throw new Exception(label);count++;};
        var originalKey=(string)Get(menu,"preferencesPrefix");
        var key="ScrollHunter.Options.Check-"+Guid.NewGuid().ToString("N")+".";
        var parameters=(string[])Get(menu,"mixerParameters");
        var mixer=(AudioMixer)Get(menu,"mixer");
        var sliders=(Slider[])Get(menu,"volumeSliders");
        var title=(GameObject)Get(menu,"titlePanel");
        var controls=(GameObject)Get(menu,"controlsPanel");
        var options=(GameObject)Get(menu,"optionsPanel");
        var inheritance=(GameObject)Get(menu,"inheritancePanel");
        var originalMix=new float[parameters.Length];
        for(int i=0;i<parameters.Length;i++)mixer.GetFloat(parameters[i],out originalMix[i]);
        try
        {
            Set(menu,"preferencesPrefix",key);
            check(Time.timeScale==0&&!controls.activeSelf&&!options.activeSelf,"title idle");
            var ordered=new[]{(Button)Get(f,"startButton"),(Button)Get(menu,"controlsOpen"),(Button)Get(menu,"inheritanceOpen"),(Button)Get(menu,"optionsOpen"),(Button)Get(menu,"quitButton")};
            for(int i=0;i<ordered.Length;i++)
            {
                var rt=(RectTransform)ordered[i].transform;
                check(ordered[i].IsInteractable()&&Mathf.Approximately(rt.anchoredPosition.x,((RectTransform)ordered[0].transform).anchoredPosition.x),"menu aligned "+i);
                if(i>0)check(rt.anchoredPosition.y<((RectTransform)ordered[i-1].transform).anchoredPosition.y,"menu order "+i);
            }
            ((Button)Get(menu,"controlsOpen")).onClick.Invoke();
            check(controls.activeInHierarchy&&!options.activeSelf&&f.State==BattleFlowState.Title&&Time.timeScale==0,"controls opens without combat");
            string guide=string.Join("\n",controls.GetComponentsInChildren<TMP_Text>().Select(t=>t.text));
            check(guide.Contains("A 또는")&&guide.Contains("S 또는")&&guide.Contains("Shift")&&guide.Contains("Q / W / E / R"),"controls include existing keys and A/S");
            ((Button)Get(menu,"controlsClose")).onClick.Invoke();check(!controls.activeSelf,"controls close");
            ((Button)Get(menu,"inheritanceOpen")).onClick.Invoke();check(inheritance.activeSelf,"inheritance still opens");
            menu.OpenOptions();check(options.activeSelf&&!inheritance.activeSelf&&!controls.activeSelf,"options exclusive modal");
            bool before=(bool)Get(menu,"windowed");((Button)Get(menu,"modeButton")).onClick.Invoke();check((bool)Get(menu,"windowed")!=before,"mode toggle");
            var size=(TMP_Text)Get(menu,"resolutionLabel");string old=size.text;
            ((Button)Get(menu,"resolutionNext")).onClick.Invoke();check(size.text!=old,"resolution next");
            ((Button)Get(menu,"resolutionPrevious")).onClick.Invoke();check(size.text==old,"resolution previous");
            sliders[0].value=.2f;menu.ClosePanels();menu.OpenOptions();
            check(sliders[0].value==1f&&!PlayerPrefs.HasKey(key+"Width"),"cancel discards and does not save");
            ((Button)Get(menu,"modeButton")).onClick.Invoke();sliders[0].value=0f;sliders[1].value=.5f;sliders[2].value=.8f;
            ((Button)Get(menu,"optionsApply")).onClick.Invoke();
            check(!options.activeSelf&&PlayerPrefs.HasKey(key+"Width")&&PlayerPrefs.GetInt(key+"Windowed")==1,"apply saves settings");
            menu.OpenOptions();check(sliders[0].value==0&&sliders[1].value==.5f&&sliders[2].value==.8f,"saved values reread");
            for(int i=0;i<parameters.Length;i++)
            {
                float actual;float expected=i==0?-80f:20f*Mathf.Log10(sliders[i].value);
                check(mixer.GetFloat(parameters[i],out actual)&&Mathf.Abs(actual-expected)<.01f,"independent audio group "+i);
            }
            float period=(float)Get(menu,"glowPeriod");
            check(menu.GlowAt(0)<.001f&&menu.GlowAt(period*.5f)>.999f&&menu.GlowAt(period)<.001f,"runes brighten and fade over full cycle");
            check(((RawImage)Get(menu,"background")).material.shader.name=="ScrollHunter/UI/TitleRuneGlow","glow shader bound");
            menu.ClosePanels();
            check(f.State==BattleFlowState.Title&&Time.timeScale==0,"settings preserve game state");
            return "Main menu: "+count+" passed. Temporary preference keys; editor display switching and actual sound playback are not covered.";
        }
        finally
        {
            foreach(var name in parameters.Concat(new[]{"Windowed","Width","Height"}))PlayerPrefs.DeleteKey(key+name);
            PlayerPrefs.Save();Set(menu,"preferencesPrefix",originalKey);menu.ClosePanels();inheritance.SetActive(false);Call(menu,"ReadSavedOptions");
            for(int i=0;i<parameters.Length;i++)mixer.SetFloat(parameters[i],originalMix[i]);
        }
    }
}
