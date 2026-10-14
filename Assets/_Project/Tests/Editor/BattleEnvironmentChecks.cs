using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class BattleEnvironmentChecks
{
    const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    static object Call(object o, string name, params object[] args) => o.GetType().GetMethod(name, Hidden).Invoke(o, args);
    [MenuItem("Tools/Scroll Hunter/Check Battle Backgrounds (Play)")]
    public static void Menu() => Debug.Log(Run());
    public static string Run()
    {
        if (!Application.isPlaying) throw new InvalidOperationException("Play required");
        var env = UnityEngine.Object.FindFirstObjectByType<BattleEnvironment>();
        var flow = UnityEngine.Object.FindFirstObjectByType<BattleFlow>();
        var tutorial = UnityEngine.Object.FindFirstObjectByType<TutorialFlow>();
        var boss = UnityEngine.Object.FindObjectsByType<Enemy>(FindObjectsInactive.Include, FindObjectsSortMode.None).Single(e => e.HasPhases);
        var camera = Camera.main;
        var light = UnityEngine.Object.FindFirstObjectByType<Light>();
        var player = UnityEngine.Object.FindFirstObjectByType<Player>();
        var cost = UnityEngine.Object.FindFirstObjectByType<CostSystem>();
        var circle = (Transform)typeof(BattleEnvironment).GetField("magicCircle", Hidden).GetValue(env);
        var roots = (GameObject[])typeof(BattleEnvironment).GetField("stages", Hidden).GetValue(env);
        var plates = (Transform[])typeof(BattleEnvironment).GetField("backplates", Hidden).GetValue(env);
        bool enabled = tutorial.enabled;
        float aspect = camera.aspect, intensity = light.intensity;
        var cameraPosition = camera.transform.position;
        bool preview = SessionState.GetBool("Tools/Scroll Hunter/Mage Model Preview on Play", false);
        int passed = 0;
        Action<bool,string> check = (ok,label) => { if (!ok) throw new Exception("Backgrounds: " + label); passed++; };
        try
        {
            SessionState.SetBool("Tools/Scroll Hunter/Mage Model Preview on Play", false);
            tutorial.enabled = true; flow.RestartRun(); flow.StartRun(); env.Refresh(0);
            check(flow.BattleNumber == 0 && env.ShownStage == 0, "tutorial forest edge");
            tutorial.enabled = false;
            int[] expected = { 1, 2, 2, 3 };
            for (int i = 0; i < 4; i++)
            {
                Call(flow, "StartBattleCountdown", i, true); env.Refresh(0);
                check(flow.IsCountingDown && env.ShownStage == expected[i], "background before countdown " + i);
                check(roots.Count(r => r.activeSelf) == 1 && roots[expected[i]].activeSelf, "one root " + i);
                Call(flow, "BeginBattle", i, true, null); env.Refresh(0);
                check(env.ShownStage == expected[i], "fighting background " + i);
            }
            check(Mathf.Abs(circle.localScale.x-8) < .001f, "phase1 reset");
            var circleHome=circle.localRotation;
            env.Refresh(1f);
            check(Mathf.Abs(Mathf.DeltaAngle(circleHome.eulerAngles.z,circle.localEulerAngles.z)+8f)<.01f,"clockwise8 degrees per second");
            var block=new MaterialPropertyBlock();circle.GetComponent<Renderer>().GetPropertyBlock(block);
            Color glow=block.GetColor("_BaseColor");
            check(glow.b>glow.r&&glow.r>glow.g&&glow.b>1f,"purple additive glow");
            var pausedAngle=circle.localRotation;env.Refresh(0);
            circle.GetComponent<Renderer>().GetPropertyBlock(block);
            check(circle.localRotation==pausedAngle&&block.GetColor("_BaseColor")==glow,"pause holds rotation and glow");
            env.Refresh(.5f);
            check(Mathf.Abs(Mathf.DeltaAngle(circleHome.eulerAngles.z,circle.localEulerAngles.z)+12f)<.01f,"half-second rotation");
            boss.TakeDamage(1200); boss.TryInterrupt(2.5f); env.Refresh(0);
            check(boss.PhaseNumber == 2 && env.ShownPhase == 2 && boss.IsPhaseTransitioning, "phase2 starts with transition");
            float size = circle.localScale.x; env.Refresh(0);
            check(circle.localScale.x == size, "paused growth");
            float hp = player.CurrentHp; float resource = cost.Current;
            env.Refresh(1f); check(circle.localScale.x > 8 && circle.localScale.x < 10.5f, "halfway growth");
            env.Refresh(1f); check(Mathf.Abs(circle.localScale.x-10.5f)<.001f, "phase2 completed");
            check(hp == player.CurrentHp && resource == cost.Current, "visual update no combat mutation");
            Call(boss, "AdvanceCombat", 2.5f); boss.TakeDamage(1125); boss.TryInterrupt(2.5f); env.Refresh(2f);
            check(boss.PhaseNumber == 3 && Mathf.Abs(circle.localScale.x-13)<.001f, "phase3 completed");
            Call(flow, "BeginBattle", 3, true, null); env.Refresh(0);
            check(Mathf.Abs(circle.localScale.x-8)<.001f, "boss restart shrinks immediately");
            check(Quaternion.Angle(circleHome,circle.localRotation)<.01f,"restart resets rotation");
            boss.TakeDamage(2325); boss.TryInterrupt(2.5f); env.Refresh(2);
            check(env.ShownPhase == 3 && Mathf.Abs(circle.localScale.x-13)<.001f, "direct phase1 to3");
            foreach (float ratio in new[]{ 4f/3, 16f/10, 16f/9, 21f/9 })
            {
                camera.aspect = ratio; env.Refresh(0);
                var p = plates[env.ShownStage];
                var left = camera.WorldToViewportPoint(p.TransformPoint(new Vector3(-.5f,-.5f,0)));
                var right = camera.WorldToViewportPoint(p.TransformPoint(new Vector3(.5f,.5f,0)));
                check(left.x <= .001f && left.y <= .001f && right.x >= .999f && right.y >= .999f, "backplate covers " + ratio);
            }
            check(light.intensity == intensity && camera.transform.position == cameraPosition, "lighting and camera preserved");
            check(env.GetComponentsInChildren<Collider>(true).Length == 0, "background cannot intercept targeting");
            check(env.GetComponentsInChildren<Renderer>(true).All(r=>r.shadowCastingMode==ShadowCastingMode.Off && !r.receiveShadows), "props do not darken enemies");
            check(env.GetComponentsInChildren<Renderer>(true).All(r=>r.sharedMaterial && r.sharedMaterial.shader.isSupported), "valid materials");
            tutorial.enabled = true; flow.RestartRun(); env.Refresh(0);
            check(env.ShownStage==0 && Mathf.Abs(circle.localScale.x-8)<.001f, "run restart forest edge");
            return "Battle background checks passed: " + passed + ". Function checks; screenshots and real-frame timing separate.";
        }
        finally
        {
            camera.aspect=aspect; tutorial.enabled=enabled;
            SessionState.SetBool("Tools/Scroll Hunter/Mage Model Preview on Play", preview);
            flow.RestartRun(); env.Refresh(0);
        }
    }

    // 전환 시간은 보스가 결정하며 연출은 이를 읽기만 하는지 실제 프레임에서 확인한다.
    public static string RunPhaseFrames()
    {
        if (!Application.isPlaying) throw new InvalidOperationException("Play required");
        var env = UnityEngine.Object.FindFirstObjectByType<BattleEnvironment>();
        var flow = UnityEngine.Object.FindFirstObjectByType<BattleFlow>();
        var tutorial = UnityEngine.Object.FindFirstObjectByType<TutorialFlow>();
        var info = UnityEngine.Object.FindFirstObjectByType<CombatInfoUI>();
        var boss = UnityEngine.Object.FindObjectsByType<Enemy>(FindObjectsInactive.Include, FindObjectsSortMode.None).Single(e => e.HasPhases);
        var circle = (Transform)typeof(BattleEnvironment).GetField("magicCircle", Hidden).GetValue(env);
        var wave = (Renderer)typeof(BattleEnvironment).GetField("phaseWave", Hidden).GetValue(env);
        var glows = (Renderer[])typeof(BattleEnvironment).GetField("phaseGlows", Hidden).GetValue(env);
        var cracks = (Renderer[])typeof(BattleEnvironment).GetField("phaseCracks", Hidden).GetValue(env);
        var motes = (ParticleSystem)typeof(BattleEnvironment).GetField("phaseEmbers", Hidden).GetValue(env);
        var block = new MaterialPropertyBlock();
        bool paused = EditorApplication.isPaused, oldTutorial = tutorial.enabled;
        float capture = Time.captureDeltaTime, oldSpeed = info.BattleSpeed;
        int passed = 0, frames = 0;
        Action<bool,string> check = (ok,label) => { if (!ok) throw new Exception("Phase VFX: " + label); passed++; };
        Action<int> steps = n => { for (int i=0;i<n;i++) { EditorApplication.Step(); frames++; } };
        try
        {
            tutorial.enabled=false; EditorApplication.isPaused=true; Time.captureDeltaTime=.02f;
            foreach (float speed in new[]{1f,.5f})
            {
                Call(flow,"BeginBattle",3,true,null);
                if (info.BattleSpeed!=speed) info.ToggleSpeed();
                steps(2); check(!wave.enabled,"phase1 no wave");
                check(glows.All(r=>!r.enabled) && cracks.All(r=>!r.enabled) && motes.particleCount==0,"phase1 clears surroundings");
                boss.TakeDamage(1200); boss.TryInterrupt(2.5f); steps(1);
                check(wave.enabled && boss.IsPhaseTransitioning,"phase2 starts wave");
                float firstSize=wave.transform.localScale.x;
                steps((int)(.5f/(.02f*speed)));
                wave.GetPropertyBlock(block); Color waveColor=block.GetColor("_BaseColor");
                circle.GetComponent<Renderer>().GetPropertyBlock(block);
                check(wave.transform.localScale.x>firstSize && waveColor.a>0,"expands and fades in");
                check(block.GetColor("_BaseColor").b>3f,"circle brightens");
                check(glows.All(r=>r.enabled) && cracks.All(r=>!r.enabled) && !motes.isPlaying,"phase2 glow only");
                var position=wave.transform.localScale; var rotation=circle.localRotation;
                var fogPosition=glows[0].transform.position;
                float remaining=boss.PhaseTransitionRemaining;
                info.TogglePause(); steps(12);
                wave.GetPropertyBlock(block);
                check(wave.transform.localScale==position && circle.localRotation==rotation && block.GetColor("_BaseColor")==waveColor && boss.PhaseTransitionRemaining==remaining,"pause freezes effect and transition");
                check(glows[0].transform.position==fogPosition,"pause freezes fog");
                info.Resume();
                int limit=(int)(2.1f/(.02f*speed));
                for(int i=0;i<limit && boss.IsPhaseTransitioning;i++)steps(1);
                check(!boss.IsPhaseTransitioning && !wave.enabled,"wave ends with transition");
                check(Mathf.Abs(circle.localScale.x-10.5f)<.01f,"phase2 final size");
                glows[0].GetPropertyBlock(block);
                check(glows.Length==4 && glows[0].transform.position!=fogPosition && block.GetColor("_BaseColor").a>=.47f,"phase2 broad drifting fog");
                steps((int)(.6f/(.02f*speed)));
                boss.TakeDamage(1050); boss.TryInterrupt(2.5f); steps(1);
                check(boss.PhaseNumber==3 && wave.enabled,"phase3 repeats once");
                steps((int)(2.4f/(.02f*speed)));
                bool currentSeen=false, gapSeen=false;
                for(int i=0;i<(int)(3f/(.02f*speed));i++){steps(1);currentSeen|=cracks.Any(r=>r.enabled);gapSeen|=cracks.Any(r=>!r.enabled);}
                check(currentSeen && gapSeen && motes.particleCount>0,"phase3 intermittent currents and rising motes");
                var particles=new ParticleSystem.Particle[40];int count=motes.GetParticles(particles);
                Vector3 first=particles[0].position;float lifetime=particles[0].remainingLifetime;
                cracks[0].GetPropertyBlock(block);Color before=block.GetColor("_BaseColor");
                info.TogglePause();steps(12);int afterCount=motes.GetParticles(particles);cracks[0].GetPropertyBlock(block);
                check(count==afterCount && particles[0].position==first && particles[0].remainingLifetime==lifetime && block.GetColor("_BaseColor")==before,"pause freezes motes and current glow");
                info.Resume();steps(5);motes.GetParticles(particles);
                check(particles[0].position.y>first.y,"motes rise after resume");
                Call(flow,"BeginBattle",3,true,null);steps(1);
                check(!wave.enabled && Mathf.Abs(circle.localScale.x-8f)<.01f,"restart clears wave and size");
                check(glows.All(r=>!r.enabled) && cracks.All(r=>!r.enabled) && motes.particleCount==0,"restart clears surroundings");
                boss.TakeDamage(2250);boss.TryInterrupt(2.5f);steps(1);
                check(boss.PhaseNumber==3 && wave.enabled,"skip phase2 still shows wave");
                UnityEngine.Object.FindFirstObjectByType<Player>().TakeDamage(100000);steps(1);
                check(!wave.enabled,"defeat removes wave");
            }
            return "Phase VFX frames passed: "+passed+" / frames "+frames+" (1x, 0.5x, manual pause, restart, phase skip, defeat)";
        }
        finally
        {
            tutorial.enabled=oldTutorial; flow.RestartRun();
            if(info.BattleSpeed!=oldSpeed)info.ToggleSpeed();
            Time.captureDeltaTime=capture;EditorApplication.isPaused=paused;
        }
    }
}
