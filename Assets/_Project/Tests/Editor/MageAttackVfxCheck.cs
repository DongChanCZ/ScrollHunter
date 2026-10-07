using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 마법사 보스 공격 연출 확인(10/6, 09 마법사 공격 연출). Battle Play(마법사 직행 옵션 권장)에서 사용한다.
///   - Force(공격 번호): 검사용으로 다음 시전을 그 공격으로 바꾼다(실행 중 상태만, 저장 없음).
///   - Arm(공격 번호, 진행률, 발동 후 프레임): 준비 진행률 또는 실제 발동 뒤 N프레임에서 멈춘다. Shoot로 1920×1080(UI 포함)과 근접을 찍는다.
///   - Start(): 실제 프레임 검사. 5종 시전·발동, 차단 성공·빨강 차단 실패·정지·0.5배속·2페이즈·발동 간격 대기·사망·재시작을 함수로 넣고 매 프레임 판정한다.
/// 입력은 함수 호출이며 직접 손 조작이 아니다. 이 Play에서만 백그라운드 실행을 켜고 끝나면 되돌린다.
/// </summary>
public static class MageAttackVfxCheck
{
    const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    public static string Dir => Path.GetFullPath("Docs/근거자료/2026-10-07_마법사VFX보정");   // 10/6 결과는 2026-10-06_마법사VFX에 보존

    static Enemy Boss => Object.FindObjectsByType<Enemy>(FindObjectsSortMode.None).FirstOrDefault(e => e.HasPhases);
    static EnemyAnimationDriver Driver(Enemy e) => Object.FindObjectsByType<EnemyAnimationDriver>(FindObjectsInactive.Include, FindObjectsSortMode.None)
        .FirstOrDefault(d => (Enemy)typeof(EnemyAnimationDriver).GetField("enemy", Hidden).GetValue(d) == e);
    static BossAttackVfx Vfx(Enemy e) { var d = Driver(e); return d != null ? d.GetComponent<BossAttackVfx>() : null; }
    // 검사 중 패배 방지(실행 중 상태만). 디엔드(600)는 한 번에 쓰러지므로 발동 직전에 방어도를 넣어 빨강 반감·흡수로 받는다.
    static void KeepPlayer()
    {
        var p = Object.FindFirstObjectByType<Player>(); if (p == null) return;
        if (p.CurrentHp < 250f) p.BeginBattle(true);
        var b = Boss;
        if (b != null && b.IsCasting && b.CurrentAttack != null && b.Data.Attacks.ToList().IndexOf(b.CurrentAttack) == 4 && b.CastProgress01 > 0.9f && p.Shield < 400) p.AddShield(800);
    }

    /// <summary>다음 시전을 이 공격(EnemyData 목록 순서)으로 바꾼다. phase를 주면 그 페이즈의 패턴에서 찾는다.</summary>
    public static string Force(int skill, int phase = -1)
    {
        var boss = Boss; if (boss == null) return "no boss";
        var data = boss.Data;
        for (int p = 0; p < data.Phases.Count; p++)
        {
            if (phase >= 0 && p != phase) continue;
            int i = data.Phases[p].Pattern.ToList().IndexOf(skill + 1);
            if (i < 0) continue;
            typeof(Enemy).GetField("phaseIndex", Hidden).SetValue(boss, p);
            typeof(Enemy).GetField("phasePatternIndex", Hidden).SetValue(boss, i);
            typeof(Enemy).GetField("phaseTransitionRemaining", Hidden).SetValue(boss, 0f);
            typeof(Enemy).GetField("staggerTimer", Hidden).SetValue(boss, 0f);
            typeof(Enemy).GetField("restTimer", Hidden).SetValue(boss, 0f);
            typeof(Enemy).GetMethod("BeginNextCast", Hidden).Invoke(boss, null);
            return $"forced {boss.CurrentAttack.SkillName} phase {p + 1} cast {boss.CurrentCastTime:0.00}s";
        }
        return "no pattern for " + skill;
    }

    /// <summary>frames &lt; 0: 그 공격의 준비 진행률이 progress 이상일 때 멈춤. frames ≥ 0: 그 공격 실제 발동 뒤 frames 프레임에 멈춤.</summary>
    public static string Arm(int skill, float progress, int frames)
    {
        var boss = Boss; var driver = Driver(boss); var vfx = Vfx(boss);
        if (boss == null || driver == null || vfx == null) return "missing";
        Application.runInBackground = true;
        int fired = -1;
        System.Action<int> onImpact = i => { if (vfx.PreparedSkill == skill || driver.MotionIndex == skill) fired = Time.frameCount; };
        driver.AttackImpact += onImpact;
        EditorApplication.CallbackFunction cb = null;
        cb = () =>
        {
            if (!EditorApplication.isPlaying) { EditorApplication.update -= cb; return; }
            KeepPlayer();
            bool stop = frames >= 0 ? fired >= 0 && Time.frameCount >= fired + frames
                : vfx.PreparedSkill == skill && boss.IsCasting && boss.CastProgress01 >= progress && vfx.ChargeObject != null;
            if (!stop) return;
            EditorApplication.isPaused = true; EditorApplication.update -= cb; driver.AttackImpact -= onImpact;
        };
        EditorApplication.update += cb;
        EditorApplication.isPaused = false;
        return "armed " + skill;
    }

    /// <summary>그 공격의 실제 발동 뒤 게임 시간 seconds초에 멈춘다(프레임이 짧은 백그라운드 편집기용). 디엔드는 발동 직전 방어도를 넣어 패배를 막는다.</summary>
    public static string ArmAfter(int skill, float seconds)
    {
        var boss = Boss; var driver = Driver(boss); var vfx = Vfx(boss);
        if (boss == null || driver == null || vfx == null) return "missing";
        Application.runInBackground = true;
        float fired = -1f;
        System.Action<int> onImpact = i => { if (fired < 0f && vfx.PreparedSkill == skill) fired = Time.time; };
        driver.AttackImpact += onImpact;
        EditorApplication.CallbackFunction cb = null;
        cb = () =>
        {
            if (!EditorApplication.isPlaying) { EditorApplication.update -= cb; return; }
            KeepPlayer();
            if (fired < 0f || Time.time < fired + seconds) return;
            EditorApplication.isPaused = true; EditorApplication.update -= cb; driver.AttackImpact -= onImpact;
        };
        EditorApplication.update += cb;
        EditorApplication.isPaused = false;
        return "armed after " + skill;
    }

    /// <summary>멈춘 화면을 1920×1080(UI 포함)과 근접(UI 없음)으로 찍는다. height는 보는 높이(m).</summary>
    public static string Shoot(string name, float distance = 4.5f, float height = 1.6f)
    {
        if (!EditorApplication.isPaused) return "not paused";
        Directory.CreateDirectory(Dir);
        string full = UiCapture.Save(Path.Combine(Dir, name + "_전체.png"), 1920, 1080);
        var boss = Boss; var main = Camera.main;
        var go = new GameObject("CAL_Cam"); var cam = go.AddComponent<Camera>(); cam.CopyFrom(main); cam.enabled = false;
        Vector3 at = Driver(boss).transform.position + Vector3.up * height;   // 적 루트는 y 1에 있어 모델 발 기준을 쓴다
        cam.transform.position = at + (main.transform.position - at).normalized * distance; cam.transform.LookAt(at); cam.fieldOfView = 34f;
        var rt = new RenderTexture(900, 800, 24); cam.targetTexture = rt; cam.Render(); RenderTexture.active = rt;
        var tex = new Texture2D(900, 800, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, 900, 800), 0, 0); tex.Apply();
        RenderTexture.active = null; cam.targetTexture = null; rt.Release(); Object.DestroyImmediate(go);
        File.WriteAllBytes(Path.Combine(Dir, name + "_근접.png"), tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        return full;
    }

    /// <summary>현재 상태 요약(디버그용).</summary>
    public static string State()
    {
        var boss = Boss; var d = Driver(boss); var v = Vfx(boss);
        if (boss == null) return "no boss";
        var screen = Object.FindFirstObjectByType<ScreenBlastVfx>();
        return (screen != null ? $"[screen scale {screen.ScreenScale:0.00} opacity {screen.Opacity:0.00}] " : "") +
               $"t{Time.time:0.00} phase {boss.PhaseNumber} cast {(boss.CurrentAttack != null ? boss.CurrentAttack.SkillName : "-")} {boss.CastProgress01:0.00} casting {boss.IsCasting} " +
               $"motion {d?.MotionIndex} prepared {v?.PreparedSkill} charge {(v?.ChargeObject != null ? v.ChargeObject.name : "-")} scale {v?.ChargeScale:0.00} spawned {v?.SpawnedCount} flights {v?.FlightCount} paused {EditorApplication.isPaused}";
    }

    // ───────── 실제 프레임 검사 ─────────

    public static bool Running { get; private set; }
    static readonly StringBuilder log = new StringBuilder();
    static readonly List<string> fails = new List<string>();
    static readonly Dictionary<string, int> counts = new Dictionary<string, int>();
    static int stage, lastFrame, impactFrame = -1, preparedFrame = -1, releasesThisCast, chargesThisCast, pauseFrame, cancelCheck = -1;
    static int lastPrepared = -1, lastImpactSkill = -1;
    static float pauseScale, stageStart, lastProgress;
    static bool backgroundBefore;
    static string cancelWhat;
    static Enemy boss; static EnemyAnimationDriver driver; static BossAttackVfx vfx;
    static readonly HashSet<int> seenRelease = new HashSet<int>();
    static GameObject lastCharge;

    static void Count(string k) { counts.TryGetValue(k, out int v); counts[k] = v + 1; }
    static bool Once(string k) { if (counts.ContainsKey("once " + k)) return false; counts["once " + k] = 1; return true; }
    static void Fail(string m) { fails.Add($"[f{Time.frameCount} t{Time.time:0.00}] {m}"); }
    static void Note(string m) { log.AppendLine($"f{Time.frameCount} t{Time.time:0.000} {m}"); }
    static readonly string[] Names = { "매직미사일", "사일런스", "다크홀", "나이트메어", "디엔드" };

    public static string Start()
    {
        if (!Application.isPlaying) return "Play required";
        if (Running) return "already running";
        boss = Boss; driver = Driver(boss); vfx = Vfx(boss);
        if (boss == null || vfx == null) return "boss/vfx missing (마법사 직행 옵션으로 Play)";
        log.Clear(); fails.Clear(); counts.Clear(); seenRelease.Clear(); killFrame = -1; cancelCheck = -1; lastEndScale = 0f; lastProgress = 0f; screenSeenTime = -1f; screenPauseScale = -1f;
        // 검사 시작 전에 이미 발동한 공격의 남은 효과는 판정에서 뺀다(Play 직후 자연 시전이 먼저 발동한 경우).
        foreach (var ps in Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None)) seenRelease.Add(ps.GetInstanceID());
        foreach (var t in Object.FindObjectsByType<TrailRenderer>(FindObjectsSortMode.None)) seenRelease.Add(t.GetInstanceID());
        foreach (var sb in Object.FindObjectsByType<ScreenBlastVfx>(FindObjectsSortMode.None)) seenRelease.Add(sb.GetInstanceID());
        backgroundBefore = Application.runInBackground; Application.runInBackground = true;
        EditorApplication.isPaused = false; Time.timeScale = 1f;
        driver.AttackPrepared += OnPrepared; driver.AttackImpact += OnImpact; driver.AttackCanceled += OnCanceled;
        stage = 0; stageStart = Time.time; Running = true;
        EditorApplication.update -= Update; EditorApplication.update += Update;
        Note("START " + Force(0, 0));
        return "started";
    }

    static int raiseCount; static bool wasRaise;
    static int killFrame = -1;
    static void OnPrepared(int i) { preparedFrame = Time.frameCount; releasesThisCast = 0; chargesThisCast = 0; lastPrepared = i; raiseCount = 0; lastProgress = 0f; Note($"PREPARE {i} {Names[Mathf.Clamp(i, 0, 4)]} cast {boss.CurrentCastTime:0.00}s phase {boss.PhaseNumber}"); Count("prepare " + i); }
    static void OnImpact(int c) { impactFrame = Time.frameCount; lastImpactSkill = vfx.PreparedSkill; Note($"IMPACT color {c} prepared {vfx.PreparedSkill} progress {boss.CastProgress01:0.00} current {(boss.CurrentAttack != null ? boss.CurrentAttack.SkillName : "null")}"); Count("impact"); }
    static void OnCanceled() { Note("CANCEL"); }

    static void Update()
    {
        if (!EditorApplication.isPlaying) { Finish("Play stopped"); return; }
        if (Time.frameCount == lastFrame) return;
        lastFrame = Time.frameCount;
        KeepPlayer();

        // 다크홀·디엔드: 손 올리기(CastChargeRaise)는 시전당 1회, 이후 유지 상태
        var anim = driver.GetComponent<Animator>();
        bool raise = anim.GetCurrentAnimatorStateInfo(0).IsName("CastChargeRaise") || (anim.IsInTransition(0) && anim.GetNextAnimatorStateInfo(0).IsName("CastChargeRaise"));
        if (raise && !wasRaise) { raiseCount++; Count("raise skill " + vfx.PreparedSkill); if (raiseCount > 1) Fail("hand raise repeated in one cast"); }
        wasRaise = raise;
        if (vfx.PreparedSkill == 4 && anim.GetCurrentAnimatorStateInfo(0).IsName("CastChargeHold")) Count("the end hold frames");

        // 준비 효과: 시전당 1개, 같은 개체 유지
        var charge = vfx.ChargeObject;
        if (charge != null && charge != lastCharge) { chargesThisCast++; if (chargesThisCast > 1) Fail("charge recreated in one cast"); lastCharge = charge; }
        if (charge != null && vfx.PreparedSkill != driver.MotionIndex && driver.MotionIndex >= 0) Fail($"prepared {vfx.PreparedSkill} != motion {driver.MotionIndex}");
        // 성장은 시전 진행과 함께(정지 중 변화 없음)
        if (charge != null && boss.IsCasting && boss.CastProgress01 + 1e-4f < lastProgress && vfx.PreparedSkill == lastPrepared) Fail("cast progress went back within a cast");
        lastProgress = charge != null && boss.IsCasting ? boss.CastProgress01 : 0f;

        // 방출: 발동 프레임 다음 LateUpdate에 1회. 발동 전 방출 효과 없음.
        foreach (var ps in Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None))
        {
            if (ps.transform.parent != null || !ps.name.StartsWith("VFX_Mage_") || !ps.name.Contains("(Clone)")) continue;
            if (ps.name.Contains("Charge")) continue;
            if (!seenRelease.Add(ps.GetInstanceID())) continue;
            string n = ps.name.Replace("(Clone)", "");
            Count("fx " + n);
            bool impactNow = Time.frameCount - impactFrame <= 1 && Time.frameCount >= impactFrame;
            bool flightImpact = n == "VFX_Mage_FireImpact";
            if (!impactNow && !flightImpact) Fail("release without impact: " + n);
            if (!flightImpact) { releasesThisCast++; if (releasesThisCast > 1 && n != "VFX_Mage_Fireball") { } }
            if (!flightImpact && !n.Contains("Fireball") && !Expected(lastImpactSkill, n)) Fail($"wrong release {n} for skill {lastImpactSkill}");
        }
        // 디엔드 화면 폭발: 실제 발동 프레임에 디엔드일 때만 1개, 정지 중 크기·불투명도 변화 없음, 지정 시간 뒤 스스로 사라짐
        foreach (var sb in Object.FindObjectsByType<ScreenBlastVfx>(FindObjectsSortMode.None))
        {
            if (!seenRelease.Add(sb.GetInstanceID())) continue;
            Count("fx screen blast"); screenSeenTime = Time.time; screenDuration = sb.Duration;
            if (!(Time.frameCount - impactFrame <= 1 && Time.frameCount >= impactFrame)) Fail("screen blast without impact");
            if (lastImpactSkill != 4) Fail("screen blast for skill " + lastImpactSkill);
        }
        var live = Object.FindFirstObjectByType<ScreenBlastVfx>();
        if (live != null)
        {
            Count("screen blast frames");
            if (Time.timeScale == 0f && screenPauseScale >= 0f && (Mathf.Abs(live.ScreenScale - screenPauseScale) > 1e-5f || Mathf.Abs(live.Opacity - screenPauseOpacity) > 1e-5f)) Fail("screen blast changed during pause");
            screenPauseScale = Time.timeScale == 0f ? live.ScreenScale : -1f; screenPauseOpacity = live.Opacity;
            if (Time.timeScale > 0f && screenSeenTime > 0f && Time.time > screenSeenTime + screenDuration + 0.2f) Fail("screen blast outlived its duration");
        }
        foreach (var t in Object.FindObjectsByType<TrailRenderer>(FindObjectsSortMode.None))
            if (t.transform.parent == null && t.name.StartsWith("VFX_Mage_Fireball") && seenRelease.Add(t.GetInstanceID()))
            { Count("fx VFX_Mage_Fireball"); if (!(Time.frameCount - impactFrame <= 1)) Fail("fireball without impact"); if (lastImpactSkill != 0) Fail("fireball for skill " + lastImpactSkill); }

        if (cancelCheck >= 0 && Time.frameCount >= cancelCheck)
        {
            bool screenLeft = Object.FindFirstObjectByType<ScreenBlastVfx>() != null;
            if (vfx.ChargeObject != null || vfx.SpawnedCount != 0 || vfx.FlightCount != 0 || screenLeft) Fail($"not cleared after {cancelWhat}: charge {vfx.ChargeObject != null} spawned {vfx.SpawnedCount} flights {vfx.FlightCount} screen {screenLeft}");
            else Count("cleared " + cancelWhat);
            cancelCheck = -1;
        }
        Run();
    }

    static bool Expected(int skill, string n)
    {
        switch (skill)
        {
            case 0: return n == "VFX_Mage_FireMuzzle";
            case 1: return n == "VFX_Mage_SilenceWave";
            case 2: return n == "VFX_Mage_DarkHoleBurst";
            case 3: return n == "VFX_Mage_NightmareBurst";
            case 4: return n == "VFX_Mage_TheEndBlast" || n == "VFX_Mage_TheEndGround";
        }
        return false;
    }

    static void Cancelled(string what) { cancelWhat = what; cancelCheck = Time.frameCount + 1; }
    static float T => Time.time - stageStart;
    static void Next(string note) { stage++; stageStart = Time.time; Note("STAGE " + stage + " " + note); }

    static void Run()
    {
        switch (stage)
        {
            case 0: // 1페이즈 매직미사일 → 발동·화염구·착탄
                if (counts.ContainsKey("fx VFX_Mage_FireImpact")) { Note("FORCE " + Force(1, 0)); Next("silence"); }
                break;
            case 1: // 사일런스 → 진행 0.5에서 정지 0.5초(성장 멈춤) → 재개 → 발동
                if (vfx.PreparedSkill == 1 && boss.CastProgress01 > 0.5f && Once("pause"))
                { pauseScale = vfx.ChargeScale; pauseFrame = Time.frameCount; Time.timeScale = 0f; Note("ACTION pause"); }
                if (counts.ContainsKey("once pause") && Time.timeScale == 0f && Time.frameCount > pauseFrame + 20)
                { if (Mathf.Abs(vfx.ChargeScale - pauseScale) > 1e-5f) Fail("charge grew during pause"); else Count("pause-frozen"); Time.timeScale = 1f; Note("ACTION resume"); }
                if (counts.ContainsKey("fx VFX_Mage_SilenceWave")) { Note("FORCE " + Force(1, 0)); Next("silence interrupt"); }
                break;
            case 2: // 사일런스 차단 성공 → 준비 효과 제거, 뒤늦은 방출 없음
                if (vfx.PreparedSkill == 1 && boss.CastProgress01 > 0.6f && Once("int1"))
                { var r = boss.TryInterrupt(2.5f); Note("ACTION interrupt silence " + r); if (r != InterruptResult.Success) Fail("silence interrupt " + r); Cancelled("silence-interrupt"); }
                if (counts.ContainsKey("once int1") && T > 3f) { if (counts.ContainsKey("fx VFX_Mage_SilenceWave") && counts["fx VFX_Mage_SilenceWave"] > 1) Fail("late silence release after interrupt"); Note("FORCE " + Force(2, 0)); Next("darkhole red-fail + half speed"); }
                break;
            case 3: // 다크홀 빨강 차단 실패(유지) → 0.5배속 → 발동
                if (vfx.PreparedSkill == 2 && boss.CastProgress01 > 0.3f && Once("redfail"))
                {
                    var before = vfx.ChargeObject; var r = boss.TryInterrupt(2.5f); Note("ACTION red interrupt " + r);
                    if (r != InterruptResult.FailedRed) Fail("red interrupt " + r);
                    if (vfx.ChargeObject == null || vfx.ChargeObject != before) Fail("charge removed by failed red"); else Count("red-kept");
                    Time.timeScale = 0.5f; Note("ACTION half speed");
                }
                if (counts.ContainsKey("fx VFX_Mage_DarkHoleBurst")) { Time.timeScale = 1f; Note("FORCE " + Force(3, 1)); Next("nightmare phase2"); }
                break;
            case 4: // 2페이즈 나이트메어(배율 0.8) → 발동
                if (vfx.PreparedSkill == 3 && Once("p2")) { Note($"phase {boss.PhaseNumber} cast {boss.CurrentCastTime:0.00}"); if (Mathf.Abs(boss.CurrentCastTime - 4f) > 0.01f) Fail("phase2 nightmare cast time " + boss.CurrentCastTime); }
                if (counts.ContainsKey("fx VFX_Mage_NightmareBurst")) { Note("FORCE " + Force(2, 1)); Next("darkhole phase2 + fire gap"); }
                break;
            case 5: // 2페이즈 다크홀: 발동 간격 대기 재현(다른 적 발동 직후처럼 manager의 마지막 발동 시각을 당겨 둠)
            {
                if (vfx.PreparedSkill == 2 && boss.CastProgress01 >= 0.98f && Once("gap"))
                {
                    var mgr = Object.FindFirstObjectByType<EnemyManager>();
                    var f = typeof(EnemyManager).GetFields(Hidden).FirstOrDefault(x => x.FieldType == typeof(float) && x.Name.ToLower().Contains("last"));
                    if (f != null) { f.SetValue(mgr, Time.time + 0.4f); Note("ACTION fire gap via " + f.Name); Count("gap-set"); } else Note("fire gap field not found");
                }
                if (counts.ContainsKey("once gap") && boss.IsCasting && boss.CastProgress01 >= 1f && vfx.ChargeObject != null) Count("gap-hold-frames");
                if (counts.ContainsKey("once gap") && boss.IsCasting && boss.CastProgress01 >= 1f && vfx.SpawnedCount > 0 && impactFrame < preparedFrame) Fail("release during fire gap");
                if (counts.ContainsKey("fx VFX_Mage_DarkHoleBurst") && counts["fx VFX_Mage_DarkHoleBurst"] >= 2) { Note("FORCE " + Force(4, 2)); Next("the end"); }
                break;
            }
            case 6: // 3페이즈 디엔드 20초: 성장 → 유지 → 발동
                if (vfx.PreparedSkill == 4 && vfx.ChargeObject != null)
                {
                    float s = vfx.ChargeScale;
                    if (boss.CastProgress01 < 0.97f && s + 1e-4f < lastEndScale) Fail("the end sphere shrank during cast");
                    lastEndScale = s;
                    if (boss.CastProgress01 > 0.25f && Once("end25")) Note($"end 25% scale {s:0.00}");
                    if (boss.CastProgress01 > 0.75f && Once("end75")) Note($"end 75% scale {s:0.00}");
                }
                // 화면 폭발: 퍼지는 중 정지(크기·불투명도 고정) → 0.5배속으로 끝까지 → 스스로 사라짐
                if (counts.ContainsKey("fx screen blast") && Time.time > screenSeenTime + 0.12f && Once("spause"))
                { Time.timeScale = 0f; pauseFrame = Time.frameCount; Note("ACTION pause during screen blast"); }
                if (counts.ContainsKey("once spause") && Time.timeScale == 0f && Time.frameCount > pauseFrame + 20 && Once("shalf"))
                { Count("screen pause frozen"); Time.timeScale = 0.5f; Note("ACTION half speed during screen blast"); }
                if (counts.ContainsKey("once shalf") && Object.FindFirstObjectByType<ScreenBlastVfx>() == null && Once("sgone"))
                { Time.timeScale = 1f; Note($"screen blast gone after {Time.time - screenSeenTime:0.00}s game time (duration {screenDuration:0.00})"); Note("FORCE " + Force(4, 2)); Next("the end skip + kill during screen blast"); }
                break;
            case 7: // 두 번째 디엔드: 시전 끝 부분으로 건너뛰어 발동 → 화면 폭발 중 처치 → 정리
                if (vfx.PreparedSkill == 4 && boss.IsCasting && boss.CastProgress01 > 0.05f && Once("skip"))
                { typeof(Enemy).GetField("castTimer", Hidden).SetValue(boss, boss.CurrentCastTime - 0.25f); Note("ACTION skip to end of the end cast"); }
                if (counts.TryGetValue("fx screen blast", out int sc) && sc >= 2 && Time.time > screenSeenTime + 0.1f && Once("kill"))
                {
                    typeof(Enemy).GetProperty("CurrentHp").GetSetMethod(true).Invoke(boss, new object[] { 1 });
                    Note("ACTION kill during screen blast"); boss.TakeDamage(1); Cancelled("death during screen blast"); killFrame = Time.frameCount;
                }
                // 승리 뒤 게임 시간이 멈추므로 프레임으로 기다린 뒤(사망 정리 확인 후) 재시작
                if (counts.ContainsKey("once kill") && killFrame >= 0 && Time.frameCount > killFrame + 30 && cancelCheck < 0 && Once("restart"))
                { Note("ACTION restart"); Object.FindFirstObjectByType<BattleFlow>().RestartRun(); Next("after restart"); restartFrame = Time.frameCount; }
                break;
            case 8: // 재시작 다음 프레임: 방출·폭발 잔여 0, 준비 효과는 새 시전의 것 1개뿐
                if (Time.frameCount > restartFrame + 2)
                {
                    int left = Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None).Count(p => p.transform.parent == null && p.name.StartsWith("VFX_Mage_") && !p.name.Contains("Charge"));
                    int charges = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Count(t => t.parent == null && t.name.StartsWith("VFX_Mage_") && t.name.Contains("Charge"));
                    left += Object.FindObjectsByType<ScreenBlastVfx>(FindObjectsSortMode.None).Length;
                    var b = Boss; var bv = b != null ? Vfx(b) : null;
                    Note($"REMAIN after restart: releases {left} charges {charges} new cast {(bv != null ? bv.PreparedSkill : -1)}");
                    if (left > 0 || charges > 1) Fail($"leftover after restart: releases {left} charges {charges}"); else Count("clean after restart");
                    Finish("done");
                }
                break;
        }
        if (T > 40f) { Fail("stage timeout " + stage); Finish("timeout"); }
    }
    static float lastEndScale;
    static int restartFrame;
    static float screenSeenTime = -1f, screenDuration, screenPauseScale = -1f, screenPauseOpacity;

    static void Finish(string why)
    {
        if (!Running) return;
        Running = false;
        EditorApplication.update -= Update;
        if (driver != null) { driver.AttackPrepared -= OnPrepared; driver.AttackImpact -= OnImpact; driver.AttackCanceled -= OnCanceled; }
        Time.timeScale = 1f;
        Application.runInBackground = backgroundBefore;
        var sb = new StringBuilder("== 요약 (" + why + ") ==\n");
        foreach (var kv in counts.Where(k => !k.Key.StartsWith("once ")).OrderBy(k => k.Key)) sb.AppendLine($"{kv.Key}: {kv.Value}");
        sb.AppendLine("failures: " + fails.Count);
        foreach (string f in fails.Take(40)) sb.AppendLine("  " + f);
        Directory.CreateDirectory(Dir);
        File.WriteAllText(Path.Combine(Dir, "실프레임_검사.txt"), sb + "\n== 기록 ==\n" + log, new UTF8Encoding(false));
    }
}
