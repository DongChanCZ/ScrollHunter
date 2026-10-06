using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 적 동작 연결 실제 프레임 기록(10/6). Battle Play에서 Start()로 시작하면 편집기 업데이트마다 기록하고,
/// 정해진 시각에 차단·연속 피격·처치(함수 호출)·수동 정지·0.5배속·전투 전환(카운트다운 포함)·재시작을 넣는다.
/// 끝나면 Docs/근거자료/2026-10-06_적모델보정_전투연결/40_타격동기화_실프레임.txt에 원문과 요약을 쓴다.
/// 입력은 함수 호출이며 직접 손 조작이 아니다. 편집기가 뒤에 있어도 진행되도록 이 Play에서만 백그라운드 실행을 켠다.
/// </summary>
public static class EnemyAnimationFrameCheck
{
    const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    static object Get(object o, string n) => o.GetType().GetField(n, Hidden).GetValue(o);
    static void Set(object o, string n, object v) => o.GetType().GetField(n, Hidden).SetValue(o, v);

    public static string OutPath => Path.GetFullPath("Docs/근거자료/2026-10-06_적모델보정_전투연결/40_타격동기화_실프레임.txt");
    public static bool Running { get; private set; }

    static BattleFlow flow; static EnemyManager manager; static TutorialFlow tutorial; static Player player;
    static readonly StringBuilder raw = new StringBuilder();
    static readonly List<string> failures = new List<string>();
    static readonly Dictionary<string, int> counts = new Dictionary<string, int>();
    static readonly List<(Enemy e, string what, int frame, float time)> events = new List<(Enemy, string, int, float)>();
    static readonly HashSet<Enemy> hooked = new HashSet<Enemy>();
    static double startReal; static int phase; static bool backgroundBefore; static float pauseNorm = -1f, pauseProgress = -1f;
    static Enemy pauseEnemy; static int lastFrame;

    static void Count(string k) { counts.TryGetValue(k, out int v); counts[k] = v + 1; }
    static void Fail(string m) { failures.Add($"[f{Time.frameCount} t{Time.time:0.00}] {m}"); }

    public static string Start()
    {
        if (!Application.isPlaying) return "Play required";
        if (Running) return "already running";   // MCP 호출 재시도로 여러 번 등록되지 않게
        EditorApplication.update -= Update;
        flow =UnityEngine.Object.FindFirstObjectByType<BattleFlow>(); manager = UnityEngine.Object.FindFirstObjectByType<EnemyManager>();
        tutorial = UnityEngine.Object.FindFirstObjectByType<TutorialFlow>(); player = UnityEngine.Object.FindFirstObjectByType<Player>();
        raw.Clear(); failures.Clear(); counts.Clear(); events.Clear(); hooked.Clear(); phase = 0;
        backgroundBefore = Application.runInBackground; Application.runInBackground = true;
        EditorApplication.isPaused = false;
        // 튜토리얼을 넘기고 전투 1(A+B)을 카운트다운부터 시작한다.
        flow.RestartRun();
        Set(tutorial, "consumedFrame", -1); tutorial.Skip();
        Set(tutorial, "consumedFrame", -1); flow.NextBattle();
        startReal = EditorApplication.timeSinceStartup;
        Running = true;
        EditorApplication.update += Update;
        return "started";
    }

    static void Hook(Enemy e)
    {
        if (!hooked.Add(e)) return;
        e.CastStarted += () => events.Add((e, "cast", Time.frameCount, Time.time));
        e.AttackFired += c => { events.Add((e, "fire:" + c, Time.frameCount, Time.time)); var d = Driver(e); if (Get(d, "bow") == null) { var motion = (EnemyAnimationDriver.AttackMotion)Get(d, "motion"); if (motion == null || Mathf.Abs(d.AttackNormalizedTime - motion.impact) > 0.0001f) Fail(e.name + " impact not aligned"); else Count("impact-aligned"); } };
        e.Interrupted += () => events.Add((e, "interrupt", Time.frameCount, Time.time));
        e.Damaged += a => events.Add((e, "damage", Time.frameCount, Time.time));
        e.Died += () => events.Add((e, "died", Time.frameCount, Time.time));
    }

    static EnemyAnimationDriver Driver(Enemy e) =>
        UnityEngine.Object.FindObjectsByType<EnemyAnimationDriver>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(d => (Enemy)Get(d, "enemy") == e);

    static string StateName(Animator a, int layer = 0)
    {
        var info = a.IsInTransition(layer) ? a.GetNextAnimatorStateInfo(layer) : a.GetCurrentAnimatorStateInfo(layer);
        foreach (string n in new[] { "Idle", "Cast", "Draw", "Aim", "Release", "Fire0", "Fire1", "Fire2", "Hit", "Death", "Flinch", "Empty" })
            if (info.IsName(n)) return n;
        return "?";
    }

    static void Update()
    {
        if (!EditorApplication.isPlaying) { Finish("Play stopped"); return; }
        if (Time.frameCount == lastFrame) return;
        lastFrame = Time.frameCount;
        double t = EditorApplication.timeSinceStartup - startReal;
        foreach (Enemy e in manager.Enemies) Hook(e);

        // 프레임 기록과 즉시 판정
        var line = new StringBuilder($"f{Time.frameCount} t{Time.time:0.000} real{t:0.00} scale{Time.timeScale:0.0} cd{(flow.IsCountingDown ? 1 : 0)} |");
        foreach (var d in UnityEngine.Object.FindObjectsByType<EnemyAnimationDriver>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            var e = (Enemy)Get(d, "enemy"); var a = d.GetComponent<Animator>();
            string st = StateName(a); float hips = (a.GetBoneTransform(HumanBodyBones.Hips).position.y - d.transform.position.y) / d.transform.lossyScale.y;
            line.Append($" {e.Data.name.Replace("Enemy_", "")}:{st}{(d.IsDetached ? "*" : "")}{(d.IsHiddenUntilPosed ? "(숨김)" : "")} h{hips:0.00} {(e.IsCasting ? "C" + e.CastProgress01.ToString("0.00") : e.IsStaggered ? "S" : e.IsAlive ? "R" : "D")}");
            Count("model-frames");
            if (e.IsCasting && d.IsAttackMotionActive) Count("windup-before-fire-frames");
            int flinch = a.GetLayerIndex("Flinch"); if (flinch >= 0 && StateName(a, flinch) == "Flinch") Fail(e.name + " unexpected ordinary hit reaction");
            if (d.IsHiddenUntilPosed) { Count("hidden-until-posed-frames"); continue; }
            // 새 양손검 내려베기는 무릎을 굽혀 hips가 0.23까지 내려간다. 머리도 낮아져야 붕괴로 판정한다.
            float head = (a.GetBoneTransform(HumanBodyBones.Head).position.y - d.transform.position.y) / d.transform.lossyScale.y;
            if (!d.IsDead && hips < 0.30f && head < 0.45f) Fail($"{e.name} low/collapsed pose while alive hips{hips:0.00} head{head:0.00} state {st}");
            if (!d.IsDead && st == "Death") Fail($"{e.name} death state while alive");
            if (d.IsDead && st != "Death" && !a.IsInTransition(0)) Fail($"{e.name} dead model not in Death ({st})");
            if (e.IsAlive && e.IsCasting && Time.timeScale > 0 && (st == "Idle")) Count("casting-but-idle-frame");
        }
        raw.AppendLine(line.ToString());

        // 시나리오(실시간 초). 카운트다운 3초 뒤 전투.
        Enemy first = manager.Enemies.Count > 0 ? manager.Enemies[0] : null;
        Enemy second = manager.Enemies.Count > 1 ? manager.Enemies[1] : null;
        if (phase == 0 && t > 5.0 && second != null && second.IsAlive && second.IsCasting && second.CastColor != CastColor.Red)
        { var r = second.TryInterrupt(2.5f); raw.AppendLine("ACTION interrupt " + second.name + " " + r); phase = 1; }
        else if (phase == 1 && t > 6.5 && first != null && first.IsAlive && first.IsCasting)
        { for (int k = 0; k < 3; k++) first.TakeDamage(1); raw.AppendLine("ACTION 3 hits " + first.name); phase = 2; }
        else if (phase == 2 && t > 7.5)
        {
            pauseEnemy = manager.Enemies.FirstOrDefault(e => e.IsAlive);
            var a = Driver(pauseEnemy).GetComponent<Animator>();
            pauseNorm = a.GetCurrentAnimatorStateInfo(0).normalizedTime; pauseProgress = pauseEnemy.CastProgress01;
            Time.timeScale = 0f; raw.AppendLine("ACTION pause"); phase = 3;
        }
        else if (phase == 3 && t > 8.5)
        {
            var a = Driver(pauseEnemy).GetComponent<Animator>();
            float n = a.GetCurrentAnimatorStateInfo(0).normalizedTime;
            if (Mathf.Abs(n - pauseNorm) > 1e-4f || Mathf.Abs(pauseEnemy.CastProgress01 - pauseProgress) > 1e-4f) Fail($"pause drift anim {pauseNorm:0.000}->{n:0.000} cast {pauseProgress:0.000}->{pauseEnemy.CastProgress01:0.000}");
            else Count("pause-frozen-ok");
            Time.timeScale = 0.5f; raw.AppendLine("ACTION half speed"); phase = 4;
        }
        else if (phase == 4 && t > 10.5) { Time.timeScale = 1f; raw.AppendLine("ACTION normal speed"); phase = 5; }
        else if (phase == 5 && t > 11.5 && first != null && first.IsAlive)
        { first.TakeDamage(first.CurrentHp); manager.NotifyEnemyDied(); raw.AppendLine("ACTION kill " + first.name); phase = 6; }
        else if (phase == 6 && t > 13.5)
        { raw.AppendLine("ACTION next battle (C) with countdown"); Set(flow, "finishingEffectHold", 0f); InvokeNext(); phase = 7; }
        else if (phase == 7 && t > 25.5) { raw.AppendLine("ACTION restart run"); flow.RestartRun(); Set(tutorial, "consumedFrame", -1); tutorial.Skip(); Set(tutorial, "consumedFrame", -1); flow.NextBattle(); phase = 8; }
        else if (phase == 8 && t > 30.0) Finish("done");
    }

    static void InvokeNext()
    {
        // 남은 적을 정리해 승리로 만든 뒤 다음 전투로(보상 화면은 건너뜀).
        foreach (Enemy e in manager.Enemies.ToList()) if (e.IsAlive) { e.TakeDamage(e.CurrentHp); manager.NotifyEnemyDied(); }
        typeof(BattleFlow).GetMethod("StartBattleCountdown", Hidden).Invoke(flow, new object[] { 1, false });
    }

    static void Finish(string why)
    {
        EditorApplication.update -= Update;
        Running = false;
        Application.runInBackground = backgroundBefore;
        // 알림과 다음 프레임 상태 대조
        var summary = new StringBuilder();
        summary.AppendLine("== 요약 (" + why + ") ==");
        foreach (var ev in events)
        {
            Count("event-" + ev.what.Split(':')[0]);
        }
        foreach (var kv in counts.OrderBy(k => k.Key)) summary.AppendLine($"{kv.Key}: {kv.Value}");
        summary.AppendLine("failures: " + failures.Count);
        foreach (string f in failures.Take(40)) summary.AppendLine("  " + f);
        summary.AppendLine("== 알림 ==");
        foreach (var ev in events) summary.AppendLine($"f{ev.frame} t{ev.time:0.000} {ev.e.name} {ev.what}");
        File.WriteAllText(OutPath, summary + "\n== 프레임 ==\n" + raw, new UTF8Encoding(false));
    }
}
