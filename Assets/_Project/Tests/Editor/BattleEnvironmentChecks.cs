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
            boss.TakeDamage(1600); boss.TryInterrupt(2.5f); env.Refresh(0);
            check(boss.PhaseNumber == 2 && env.ShownPhase == 2 && boss.IsPhaseTransitioning, "phase2 starts with transition");
            float size = circle.localScale.x; env.Refresh(0);
            check(circle.localScale.x == size, "paused growth");
            float hp = player.CurrentHp; float resource = cost.Current;
            env.Refresh(1f); check(circle.localScale.x > 8 && circle.localScale.x < 10.5f, "halfway growth");
            env.Refresh(1f); check(Mathf.Abs(circle.localScale.x-10.5f)<.001f, "phase2 completed");
            check(hp == player.CurrentHp && resource == cost.Current, "visual update no combat mutation");
            Call(boss, "AdvanceCombat", 2.5f); boss.TakeDamage(1500); boss.TryInterrupt(2.5f); env.Refresh(2f);
            check(boss.PhaseNumber == 3 && Mathf.Abs(circle.localScale.x-13)<.001f, "phase3 completed");
            Call(flow, "BeginBattle", 3, true, null); env.Refresh(0);
            check(Mathf.Abs(circle.localScale.x-8)<.001f, "boss restart shrinks immediately");
            check(Quaternion.Angle(circleHome,circle.localRotation)<.01f,"restart resets rotation");
            boss.TakeDamage(3100); boss.TryInterrupt(2.5f); env.Refresh(2);
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
}
