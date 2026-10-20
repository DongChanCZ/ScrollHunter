#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// 촬영·검사 전용. 제품 빌드에서 제외하며 임시 저장과 런 덱만 사용한다.
public sealed class ArcaneBarrierSoundCapture : MonoBehaviour
{
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    [Serializable] public class Sound { public int frame; public float volume; public string path; }
    [Serializable] public class Report
    {
        public List<string> passed = new List<string>(), failed = new List<string>();
        public List<Sound> sounds = new List<Sound>();
        public float inputCost, afterCost, castTime, effectTime;
        public int frames, granted, barrierSounds, armorSounds, sampleRate; public long audioSamples;
    }
    BattleFlow flow; DeckSystem deck; Player player; CostSystem cost; SkillVfx vfx;
    GameAudio audioSystem; InheritanceSave saved; GameLanguage languageBefore;
    SkillData barrier, armor; GameObject barrierVfx, armorVfx;
    System.Diagnostics.Process encoder;
    string folder, ffmpeg;
    BinaryWriter audioWriter; bool audioRendering; AudioClip barrierClip; int frame;
    float inputTime;
    Report report = new Report();
    public static string Status = "idle";
    static object Get(object o, string n) => o.GetType().GetField(n, Flags).GetValue(o);
    static void Set(object o, string n, object v) => o.GetType().GetField(n, Flags).SetValue(o, v);
    static object Call(object o, string n, params object[] args) => o.GetType().GetMethod(n, Flags).Invoke(o, args);
    void Check(bool condition, string name) { (condition ? report.passed : report.failed).Add(name); }

    public void Begin(string output, string executable)
    {
        folder = output; ffmpeg = executable; Directory.CreateDirectory(folder);
        flow = FindFirstObjectByType<BattleFlow>(); deck = FindFirstObjectByType<DeckSystem>();
        player = FindFirstObjectByType<Player>(); cost = FindFirstObjectByType<CostSystem>();
        vfx = FindFirstObjectByType<SkillVfx>(); audioSystem = FindFirstObjectByType<GameAudio>();
        saved = flow.Inheritance; languageBefore = GameText.Current;
        Set(flow, "inheritance", new InheritanceSave(Path.Combine(folder, "temporary-save.json"),
            (BattleRewardOption[])Get(flow, "rewardPool"), deck.CopyStartingDeck()));
        flow.ReturnToTitle();
        flow.GetComponent<TutorialFlow>().enabled = false;
        flow.GetComponent<StoryUI>().enabled = false;
        GameText.SetLanguage(GameLanguage.Korean); LanguageFonts.ApplyAll();
        barrier = AssetDatabase.LoadAssetAtPath<SkillData>("Assets/_Project/Data/ScrollHunter/SkillData/Skill_SK15_ArcaneBarrier.asset");
        armor = AssetDatabase.LoadAssetAtPath<SkillData>("Assets/_Project/Data/ScrollHunter/SkillData/Skill_CrystalShield.asset");
        foreach (var e in (SkillVfx.Entry[])Get(vfx, "entries"))
        {
            if (e.skill == barrier) barrierVfx = e.selfEffect;
            if (e.skill == armor) armorVfx = e.selfEffect;
        }
        Check(barrierVfx != null && barrierVfx != armorVfx, "dedicated self effect; Magic Armor unchanged");
        Check(barrier.Icon != armor.Icon, "dedicated icon");
        Check(barrier.Cost == 5 && Mathf.Abs(barrier.CastTime - .2f) < .001f && barrier.ShieldAmount == 250, "5 cost / 0.2 cast / 250 shield unchanged");
        Check(barrierVfx.GetComponent<Animator>().updateMode == AnimatorUpdateMode.Normal, "scaled animator");
        foreach (var image in barrierVfx.GetComponentsInChildren<Image>(true)) Check(!image.raycastTarget, image.name + " cannot block clicks");
        barrierClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/Audio/Magic/sh_arcane_barrier.wav");
        Check(barrierClip != null && Mathf.Abs(barrierClip.length - 2.76f) < .01f, "dedicated audio imported: 2.76 seconds");
        report.sampleRate = AudioSettings.outputSampleRate;
        deck.ShieldApplied += OnShield;
        audioSystem.Played += OnSound;
        Assembly.Load("Assembly-CSharp-Editor").GetType("UiCapture").GetMethod("SetGameViewSize")
            .Invoke(null, new object[] { 1920, 1080 });
        EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView")).Focus();
        Time.captureFramerate = 30;
        Application.runInBackground = true;
        EditorApplication.isPaused = false;
        StartCoroutine(Run());
    }

    void ResetBattle()
    {
        Call(flow, "ClearRunRewards");
        var cards = deck.CopyStartingDeck(); cards[0] = barrier;
        Set(flow, "runDeck", cards);
        Call(flow, "BeginBattle", 0, true, null);
        Time.timeScale = 1f;
    }
    void OnSound(SoundCue cue, AudioClip clip, UnityEngine.Audio.AudioMixerGroup group)
    {
        if (clip == barrierClip) report.barrierSounds++;
        if (clip.name == "jc_magic_shield") report.armorSounds++;
        if (frame < 360) report.sounds.Add(new Sound { frame = frame, volume = cue.volume, path = AssetDatabase.GetAssetPath(clip) });
    }
    void OnShield(SkillData skill, int granted)
    {
        if (skill != barrier) return;
        report.granted = granted; report.effectTime = Time.time - inputTime;
        Check(granted == 250, "actual ShieldApplied grants 250");
        Check(barrierVfx.activeSelf && !armorVfx.activeSelf, "actual ShieldApplied starts only rune shield");
    }
    bool UseBarrier()
    {
        inputTime = Time.time; report.inputCost = cost.Current;
        bool accepted = deck.TryUseSlot(0); report.afterCost = cost.Current; report.castTime = deck.CastRemaining;
        Check(accepted, "real TryUseSlot input accepted");
        Check(Mathf.Abs(report.inputCost - report.afterCost - 5f) < .001f, "exact 5 cost consumption");
        Check(!barrierVfx.activeSelf, "effect absent before cast completes");
        return accepted;
    }
    IEnumerator Run()
    {
        yield return null;
        ResetBattle();
        yield return new WaitForEndOfFrame();
        try
        {
            if (AudioSettings.GetConfiguration().speakerMode != AudioSpeakerMode.Stereo) throw new Exception("Stereo output required for capture.");
            audioRendering = AudioRenderer.Start();
            if (!audioRendering) throw new Exception("AudioRenderer did not start.");
            audioWriter = new BinaryWriter(File.Open(Path.Combine(folder, "audio.f32"), FileMode.Create));
            for (frame = 0; frame < 360; frame++)
            {
                if (frame == 162) UseBarrier();
                if (frame == 207)
                {
                    Check(report.barrierSounds == 1 && report.armorSounds == 0, "one dedicated sound at shield grant; common shield sound absent");
                    Check(report.granted == 250, "recorded cast completed, not cancelled");
                    Check(!barrierVfx.activeSelf, "effect auto hides after 1.1 game seconds");
                }
                // 자연 충전만 사용. 기존 매직아머도 뒤에 보여 차이를 비교한다.
                if (frame == 285) Check(deck.TryUseSlot(3), "original Magic Armor still usable");
                yield return new WaitForEndOfFrame();
                int count = AudioRenderer.GetSampleCountForCaptureFrame() * 2;
                using (var buffer = new Unity.Collections.NativeArray<float>(count, Unity.Collections.Allocator.Temp))
                {
                    if (!AudioRenderer.Render(buffer)) throw new Exception("Audio render failed.");
                    foreach (float sample in buffer) audioWriter.Write(sample);
                    report.audioSamples += count;
                }
                var texture = ScreenCapture.CaptureScreenshotAsTexture();
                if (frame == 178) File.WriteAllBytes(Path.Combine(folder, "shield-in-game.png"), texture.EncodeToPNG());
                if (encoder == null)
                {
                    string format = texture.format == TextureFormat.RGB24 ? "rgb24" : "rgba";
                    var start = new System.Diagnostics.ProcessStartInfo(ffmpeg,
                        "-y -loglevel error -f rawvideo -pixel_format " + format + " -video_size " + texture.width + "x" + texture.height
                        + " -framerate 30 -i pipe:0 -vf vflip -an -c:v libx264 -threads 3 -preset ultrafast -crf 18 -pix_fmt yuv420p \""
                        + Path.Combine(folder, "raw.mp4") + "\"");
                    start.UseShellExecute = false; start.CreateNoWindow = true;
                    start.RedirectStandardInput = true; start.RedirectStandardError = true;
                    encoder = System.Diagnostics.Process.Start(start);
                    encoder.ErrorDataReceived += (s, e) => { if (e.Data != null) File.AppendAllText(Path.Combine(folder, "encode-errors.txt"), e.Data + "\n"); };
                    encoder.BeginErrorReadLine();
                }
                byte[] bytes = texture.GetRawTextureData();
                encoder.StandardInput.BaseStream.Write(bytes, 0, bytes.Length);
                Destroy(texture);
                if (frame % 30 == 0) { Status = "recording " + frame + "/360"; File.WriteAllText(Path.Combine(folder, "progress.txt"), Status); }
            }
            encoder.StandardInput.Close(); encoder.WaitForExit(10000); encoder = null;
            report.frames = frame;
            audioWriter.Dispose(); audioWriter = null; AudioRenderer.Stop(); audioRendering = false;
            Check(report.barrierSounds == 1 && report.armorSounds == 1, "recorded Arcane Barrier and Magic Armor use separate sounds once each");
            Check(System.Math.Abs(report.audioSamples - (long)report.sampleRate * 2 * 12) < report.sampleRate * 2 / 30, "actual Unity stereo output matches video within one capture frame (DSP buffer rounding)");
            ResetBattle(); cost.EnsureTutorialCost(10); UseBarrier();
            yield return new WaitForSeconds(.35f);
            yield return new WaitForEndOfFrame();
            var information = FindFirstObjectByType<CombatInfoUI>();
            information.TogglePause();
            var animator = barrierVfx.GetComponent<Animator>();
            float beforePause = animator.GetCurrentAnimatorStateInfo(0).normalizedTime;
            yield return new WaitForSecondsRealtime(.25f);
            Check(audioSystem.PausedVoices > 0, "pause suspends active combat audio");
            Check(barrierVfx.activeSelf && Mathf.Abs(animator.GetCurrentAnimatorStateInfo(0).normalizedTime - beforePause) < .001f, "pause freezes shield");
            information.ToggleSpeed(); information.Resume();
            Check(Mathf.Approximately(Time.timeScale, 2f), "real speed control switches to 2x");
            yield return new WaitForSeconds(1.05f);
            Check(!barrierVfx.activeSelf, "2x resume completes and hides shield");
            ResetBattle(); cost.EnsureTutorialCost(10); UseBarrier();
            yield return new WaitForSeconds(.3f);
            vfx.Clear();
            audioSystem.StopCombat();
            Check(!audioSystem.IsPlaying(barrierClip), "combat cleanup stops barrier audio");
            ResetBattle(); cost.EnsureTutorialCost(10);
            int soundCount = report.barrierSounds;
            UseBarrier(); player.ApplyStun(1f);
            yield return new WaitForSeconds(.35f);
            Check(deck.LastCastCancelled && report.barrierSounds == soundCount && !barrierVfx.activeSelf, "stun cancellation emits no shield sound or effect");
            Check(!barrierVfx.activeSelf, "battle cleanup removes shield immediately");
            Status = report.failed.Count == 0 ? "complete" : "checks failed";
        }
        finally
        {
            if (audioWriter != null) audioWriter.Dispose(); if (audioRendering) AudioRenderer.Stop();
            if (encoder != null) { encoder.StandardInput.Close(); encoder.WaitForExit(10000); }
            deck.ShieldApplied -= OnShield; audioSystem.Played -= OnSound;
            Time.captureFramerate = 0;
            flow.ReturnToTitle(); Set(flow, "inheritance", saved);
            GameText.SetLanguage(languageBefore); LanguageFonts.ApplyAll();
            File.WriteAllText(Path.Combine(folder, "report.json"), JsonUtility.ToJson(report, true));
            File.WriteAllText(Path.Combine(folder, "progress.txt"), Status);
        }
    }
}
#endif
