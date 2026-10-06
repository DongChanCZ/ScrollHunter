using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 적 공격 연출 실제 프레임 검사(10/6, 09 적 모델 Unity 확인). Battle Play에서 Start()로 시작.
/// 깡패(튜토리얼 적) → A+B(약탈자·궁병) → C(우두머리)를 실제 프레임으로 돌리며, 정해진 때 차단 성공·빨강 차단 실패·
/// 정지·0.5배속·공격 중 사망·전투 전환·재시작을 함수로 넣는다. 매 프레임 판정:
///   궤적은 공격 진행이 베기 구간일 때만, 타격·발사 효과는 실제 발동 프레임에 적마다 한 번, 취소 뒤 궤적·효과 0.
/// 화면 근거는 Battle·Arm·Shoot로 원하는 공격 지점에서 멈춘 뒤 찍는다(1920×1080 UI 포함 + 근접). 결과·원문은 Docs/근거자료/2026-10-06_적공격VFX/.
/// 입력은 함수 호출이며 직접 손 조작이 아니다. 이 Play에서만 백그라운드 실행을 켜고 끝나면 되돌린다.
/// </summary>
public static class EnemyAttackVfxCheck
{
    const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    static object Get(object o, string n) => o.GetType().GetField(n, Hidden).GetValue(o);
    static void Set(object o, string n, object v) => o.GetType().GetField(n, Hidden).SetValue(o, v);

    public static string Dir => Path.GetFullPath("Docs/근거자료/2026-10-06_적공격VFX");
    public static bool Running { get; private set; }

    static BattleFlow flow; static EnemyManager manager; static TutorialFlow tutorial; static Player player;
    static readonly StringBuilder log = new StringBuilder();
    static readonly List<string> fails = new List<string>();
    static readonly Dictionary<string, int> counts = new Dictionary<string, int>();
    static readonly HashSet<int> seenFx = new HashSet<int>();
    static readonly Dictionary<EnemyAnimationDriver, int> impactFrame = new Dictionary<EnemyAnimationDriver, int>();
    static readonly Dictionary<EnemyAnimationDriver, int> impactsSinceCast = new Dictionary<EnemyAnimationDriver, int>();
    static readonly HashSet<EnemyAnimationDriver> hooked = new HashSet<EnemyAnimationDriver>();
    static readonly HashSet<string> shots = new HashSet<string>();
    static double start; static int stage, lastFrame, cancelCheckFrame = -1, pauseFrame;
    static bool backgroundBefore; static EnemyAttackVfx cancelTarget; static float pauseTrailTime;
    static string pending; static int pendingFrame, impactsAtHalf;

    static void Count(string k) { counts.TryGetValue(k, out int v); counts[k] = v + 1; }
    static bool Once(string k) { if (counts.ContainsKey("once " + k)) return false; counts["once " + k] = 1; return true; }
    static void Fail(string m) { fails.Add($"[f{Time.frameCount} t{Time.time:0.00}] {m}"); }
    static void Note(string m) { log.AppendLine($"f{Time.frameCount} t{Time.time:0.000} {m}"); }

    public static string Start()
    {
        if (!Application.isPlaying) return "Play required";
        if (Running) return "already running";
        EditorApplication.update -= Update;
        flow = Object.FindFirstObjectByType<BattleFlow>(); manager = Object.FindFirstObjectByType<EnemyManager>();
        tutorial = Object.FindFirstObjectByType<TutorialFlow>(); player = Object.FindFirstObjectByType<Player>();
        log.Clear(); fails.Clear(); counts.Clear(); seenFx.Clear(); impactFrame.Clear(); impactsSinceCast.Clear(); hooked.Clear(); shots.Clear();
        Directory.CreateDirectory(Dir);
        UiCapture.SetGameViewSize(1920, 1080);
        backgroundBefore = Application.runInBackground; Application.runInBackground = true;
        EditorApplication.isPaused = false;
        Set(flow, "finishingEffectHold", 0f);
        tutorial.enabled = false;
        Battle(-1);   // 튜토리얼 안내 없이 깡패 전투(일반 패턴: 빨강→초록→주황)
        stage = 0; start = EditorApplication.timeSinceStartup; Running = true;
        EditorApplication.update += Update;
        Note("START thug");
        return "started";
    }

    static void Hook(EnemyAnimationDriver d)
    {
        if (!hooked.Add(d)) return;
        var e = (Enemy)Get(d, "enemy");
        d.AttackImpact += i => { impactFrame[d] = Time.frameCount; impactsSinceCast.TryGetValue(d, out int n); impactsSinceCast[d] = n + 1; Note($"IMPACT {e.name} #{i}"); Count("impact"); };
        d.AttackCanceled += () => Note($"CANCEL {e.name}");
        e.CastStarted += () => impactsSinceCast[d] = 0;
    }

    // 진행 표시만 남긴다. 백그라운드 실행 중 ScreenCapture는 지난 프레임이 찍혀 화면 근거는 Arm·Shoot(멈춘 뒤 촬영)으로 따로 만든다.
    static void Shot(string name)
    {
        if (shots.Add(name)) Note("MARK " + name);
    }

    static void Update()
    {
        if (!EditorApplication.isPlaying) { Finish("Play stopped"); return; }
        if (Time.frameCount == lastFrame) return;
        lastFrame = Time.frameCount;
        double t = EditorApplication.timeSinceStartup - start;
        if (player != null && player.CurrentHp < 200f) player.BeginBattle(true);   // 검사 중 패배 방지(표시 검사용)

        var vfxs = Object.FindObjectsByType<EnemyAttackVfx>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (var v in vfxs)
        {
            var d = v.GetComponent<EnemyAnimationDriver>(); Hook(d);
            var e = (Enemy)Get(d, "enemy");
            string who = e.name.Replace("Enemy_", "");
            if (v.TrailEmitting)
            {
                Count("trail-frames " + who + " #" + d.AttackIndex);
                var list = (EnemyAttackVfx.Attack[])Get(v, "attacks");
                var a = d.AttackIndex >= 0 && d.AttackIndex < list.Length ? list[d.AttackIndex] : null;
                if (a != null && a.trailEnd <= a.trailStart) a = list[0];
                float nt = d.AttackNormalizedTime;
                if (!d.IsAttackMotionActive || a == null || nt < a.trailStart || nt > a.trailEnd) Fail($"{who} trail outside slash window t{nt:0.00}");
                if (!e.IsAlive) Fail($"{who} trail on dead enemy");
                // 대표 프레임: 구간 가운데쯤
                if (a != null && nt > (a.trailStart + a.trailEnd) * 0.5f) Shot($"{who}_궤적_{d.AttackIndex}");
            }
            if (impactFrame.TryGetValue(d, out int f) && Time.frameCount == f + 3) Shot($"{who}_발동_{d.AttackIndex}_{(int)e.CastColor}");
        }

        // 새로 생긴 효과: 발동 알림이 있었던 프레임(또는 다음 프레임 LateUpdate)에만.
        foreach (var ps in Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None))
        {
            if (ps.transform.parent != null || !(ps.name.StartsWith("VFX_Enemy") || ps.name.StartsWith("VFX_Leader") || ps.name.StartsWith("VFX_Arrow"))) continue;
            if (!seenFx.Add(ps.GetInstanceID())) continue;
            Count("fx " + ps.name.Replace("(Clone)", ""));
            bool matched = impactFrame.Values.Any(fr => Time.frameCount - fr <= 1 && Time.frameCount - fr >= 0);
            if (!matched) Fail("effect without impact this frame: " + ps.name);
        }
        foreach (var kv in impactsSinceCast) if (kv.Value > 1) { Fail(kv.Key.name + " impact twice in one cast"); impactsSinceCast[kv.Key] = 1; }

        // 취소 확인: 다음 프레임에 궤적·효과 0
        if (cancelTarget != null && Time.frameCount == cancelCheckFrame)
        {
            if (cancelTarget.TrailEmitting || cancelTarget.SpawnedCount != 0) Fail($"{cancelTarget.name} not cleared after {pending}"); else Count("cleared " + pending);
            cancelTarget = null;
        }

        Run(t);
    }

    static EnemyAttackVfx Vfx(Enemy e) => Object.FindObjectsByType<EnemyAttackVfx>(FindObjectsInactive.Include, FindObjectsSortMode.None)
        .FirstOrDefault(v => (Enemy)Get(v.GetComponent<EnemyAnimationDriver>(), "enemy") == e);

    static void Cancelled(EnemyAttackVfx v, string what) { cancelTarget = v; cancelCheckFrame = Time.frameCount + 1; pending = what; }

    static void Run(double t)
    {
        Enemy Find(string n) => manager.Enemies.FirstOrDefault(x => x != null && x.name.Contains(n) && x.IsAlive);
        switch (stage)
        {
            case 0: // 깡패 세 공격(빨강 발차기→초록 잽→주황 크로스)
                if (shots.Count(s => s.StartsWith("Tutorial_발동")) >= 3 || t > 26) { Note("STAGE A+B"); typeof(BattleFlow).GetMethod("BeginBattle", Hidden).Invoke(flow, new object[] { 0, true, null }); Time.timeScale = 1f; stage = 1; start = EditorApplication.timeSinceStartup; }
                break;
            case 1: // A+B: 약탈자·궁병 자연 진행. 약탈자 두 번째 공격 궤적 중 차단 성공, 궁병 주황 당김 중 차단 성공.
            {
                Enemy a = Find("A_Green"), b = Find("B_Orange");
                if (a != null && shots.Contains("A_Green_발동_0_0") && !counts.ContainsKey("raider-int") && Vfx(a).TrailEmitting && a.IsCasting && cancelTarget == null)
                { Count("raider-int"); Note("ACTION interrupt raider during slash " + a.TryInterrupt(2.5f)); Cancelled(Vfx(a), "raider-interrupt"); }
                if (b != null && b.IsCasting && b.CastColor == CastColor.Orange && b.CastProgress01 > 0.6f && !counts.ContainsKey("archer-int") && cancelTarget == null)
                { Count("archer-int"); Note("ACTION interrupt archer drawing " + b.TryInterrupt(2.5f)); Cancelled(Vfx(b), "archer-interrupt"); }
                if ((counts.ContainsKey("cleared raider-interrupt") && shots.Any(s => s.StartsWith("B_Orange_발동"))) || t > 30)
                { Note("STAGE C"); typeof(BattleFlow).GetMethod("BeginBattle", Hidden).Invoke(flow, new object[] { 1, true, null }); Time.timeScale = 1f; stage = 2; start = EditorApplication.timeSinceStartup; }
                break;
            }
            case 2: // C: 빨강 차단 실패(공격 계속) → 빨강·초록 궤적과 발동, 초록 궤적 중 정지 0.5초·0.5배속, 이후 공격 중 사망
            {
                Enemy c = Find("C_Red");
                if (c == null) break;
                var v = Vfx(c);
                if (c.IsCasting && c.CastColor == CastColor.Red && c.CastProgress01 > 0.3f && !counts.ContainsKey("red-fail"))
                { var r = c.TryInterrupt(2.5f); Note("ACTION red interrupt attempt " + r); Count("red-fail"); if (r != InterruptResult.FailedRed) Fail("red interrupt not FailedRed"); }
                if (v.TrailEmitting && Time.timeScale > 0f && !counts.ContainsKey("paused") && shots.Contains("C_Red_발동_2_2"))
                { Time.timeScale = 0f; pauseFrame = Time.frameCount; pauseTrailTime = c.GetComponentInChildren<EnemyAnimationDriver>().AttackNormalizedTime; Count("paused"); Note("ACTION pause during slash"); }
                if (counts.ContainsKey("paused") && Time.timeScale == 0f && Time.frameCount > pauseFrame + 30)
                {
                    float now = c.GetComponentInChildren<EnemyAnimationDriver>().AttackNormalizedTime;
                    if (Mathf.Abs(now - pauseTrailTime) > 1e-5f) Fail("attack progressed during pause"); else Count("pause-frozen");
                    Time.timeScale = 0.5f; Note("ACTION half speed"); impactsAtHalf = counts["impact"]; stage = 3; start = EditorApplication.timeSinceStartup;
                }
                if (t > 40) { Fail("stage C timeout"); stage = 3; }
                break;
            }
            case 3: // 0.5배속으로 다음 공격 하나 보고, 궤적 중 사망
            {
                Enemy c = Find("C_Red");
                if (!counts.ContainsKey("half-impact") && counts.TryGetValue("impact", out int n) && n > impactsAtHalf) Count("half-impact");   // 0.5배속 발동 하나 본 뒤
                if (c != null && Vfx(c).TrailEmitting && counts.ContainsKey("half-impact"))
                { var v = Vfx(c); Time.timeScale = 1f; Note("ACTION kill during slash"); c.TakeDamage(c.CurrentHp); manager.NotifyEnemyDied(); Cancelled(v, "death"); stage = 4; start = EditorApplication.timeSinceStartup; }
                if (t > 20) { Fail("stage kill timeout"); stage = 4; start = EditorApplication.timeSinceStartup; }
                break;
            }
            case 4: // 전환·재시작 뒤 남은 효과 0
                if (t > 1.0 && Once("next")) { Note("ACTION next battle"); typeof(BattleFlow).GetMethod("BeginBattle", Hidden).Invoke(flow, new object[] { 2, true, null }); }
                if (t > 1.5 && Once("remain1")) Remaining("after transition");
                if (t > 2.0 && Once("restart")) { Note("ACTION restart"); flow.RestartRun(); }
                if (t > 2.6) { Remaining("after restart"); Finish("done"); }
                break;
        }
    }

    // ---- 화면 확인용: 원하는 공격 지점에서 멈추고 찍는다(실제 프레임 진행, 멈춘 뒤 UiCapture) ----

    /// <summary>전투를 연다. -1 = 깡패(튜토리얼 적, 안내 없이), 0 = A+B, 1 = C.</summary>
    public static string Battle(int index)
    {
        flow = Object.FindFirstObjectByType<BattleFlow>(); tutorial = Object.FindFirstObjectByType<TutorialFlow>();
        var begin = typeof(BattleFlow).GetMethod("BeginBattle", Hidden);
        if (index < 0) begin.Invoke(flow, new object[] { 0, true, new[] { tutorial.Enemy } });
        else begin.Invoke(flow, new object[] { index, true, null });
        Time.timeScale = 1f;
        return "battle " + index;
    }

    /// <summary>
    /// 이름에 enemy가 들어간 적이 attack번 공격일 때 멈춘다. frames ≥ 0이면 발동 뒤 그 프레임 수, 아니면 궤적이 켜져 있고 진행이 time 이상일 때.
    /// </summary>
    public static string Arm(string enemy, int attack, float time, int frames)
    {
        Application.runInBackground = true;
        var vfx = Object.FindObjectsByType<EnemyAttackVfx>(FindObjectsSortMode.None).FirstOrDefault(v => v.transform.parent != null && v.transform.parent.name.Contains(enemy));
        if (vfx == null) return "no enemy " + enemy;
        var d = vfx.GetComponent<EnemyAnimationDriver>();
        int fired = -1;
        System.Action<int> onImpact = i => { if (attack < 0 || i == attack) fired = Time.frameCount; };
        d.AttackImpact += onImpact;
        EditorApplication.CallbackFunction cb = null;
        cb = () =>
        {
            if (!EditorApplication.isPlaying) { EditorApplication.update -= cb; return; }
            var p = Object.FindFirstObjectByType<Player>(); if (p != null && p.CurrentHp < 200f) p.BeginBattle(true);
            bool stop = frames >= 0 ? fired >= 0 && Time.frameCount >= fired + frames
                : vfx.TrailEmitting && (attack < 0 || d.AttackIndex == attack) && d.AttackNormalizedTime >= time;
            if (!stop) return;
            EditorApplication.isPaused = true; EditorApplication.update -= cb; d.AttackImpact -= onImpact;
        };
        EditorApplication.update += cb;
        EditorApplication.isPaused = false;
        return "armed " + enemy;
    }

    /// <summary>멈춘 화면을 1920×1080(UI 포함)과 적 근접(UI 없음)으로 찍는다.</summary>
    public static string Shoot(string name, string enemy, float distance, float height)
    {
        if (!EditorApplication.isPaused) return "not paused";
        Directory.CreateDirectory(Dir);
        string full = UiCapture.Save(Path.Combine(Dir, name + "_전체.png"), 1920, 1080);
        var target = Object.FindObjectsByType<EnemyAnimationDriver>(FindObjectsSortMode.None).First(v => (v.transform.parent != null && v.transform.parent.name.Contains(enemy)) || (v.transform.parent == null && v.name.Contains(enemy)));
        var main = Camera.main; var go = new GameObject("CAL_Cam"); var cam = go.AddComponent<Camera>(); cam.CopyFrom(main); cam.enabled = false;
        Vector3 at = target.transform.position + Vector3.up * height;
        cam.transform.position = at + (main.transform.position - at).normalized * distance; cam.transform.LookAt(at); cam.fieldOfView = 32f;
        var rt = new RenderTexture(900, 700, 24); cam.targetTexture = rt; cam.Render(); RenderTexture.active = rt;
        var tex = new Texture2D(900, 700, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, 900, 700), 0, 0); tex.Apply();
        RenderTexture.active = null; cam.targetTexture = null; rt.Release(); Object.DestroyImmediate(go);
        File.WriteAllBytes(Path.Combine(Dir, name + "_근접.png"), tex.EncodeToPNG());
        return full;
    }

    static void Remaining(string when)
    {
        int fx = Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None).Count(p => p.transform.parent == null && p.name.Contains("(Clone)") && (p.name.StartsWith("VFX_Enemy") || p.name.StartsWith("VFX_Leader") || p.name.StartsWith("VFX_Arrow")));
        int trails = Object.FindObjectsByType<EnemyAttackVfx>(FindObjectsInactive.Include, FindObjectsSortMode.None).Count(v => v.TrailEmitting);
        Note($"REMAIN {when}: effects {fx} trails {trails}");
        if (fx > 0 || trails > 0) Fail($"leftover {when}: effects {fx} trails {trails}"); else Count("clean " + when);
    }

    static void Finish(string why)
    {
        if (!Running) return;
        EditorApplication.update -= Update;
        Running = false;
        Application.runInBackground = backgroundBefore;
        if (tutorial != null) tutorial.enabled = true;
        var sb = new StringBuilder("== 요약 (" + why + ") ==\n");
        foreach (var kv in counts.OrderBy(k => k.Key)) sb.AppendLine($"{kv.Key}: {kv.Value}");
        sb.AppendLine("shots: " + string.Join(", ", shots.OrderBy(s => s)));
        sb.AppendLine("failures: " + fails.Count);
        foreach (string f in fails.Take(40)) sb.AppendLine("  " + f);
        File.WriteAllText(Path.Combine(Dir, "실프레임_검사.txt"), sb + "\n== 기록 ==\n" + log, new UTF8Encoding(false));
    }
}
