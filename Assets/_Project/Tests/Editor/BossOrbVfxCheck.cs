using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 보스 오브 외형·소환·파괴 연출 실제 프레임 검사(10/7, 10 A43 연출). Play 중 일시정지 상태에서 EditorApplication.Step으로 프레임을 진행한다.
/// 전환·시전·피해·카드 사용·정지 지정은 함수 호출이고 그 뒤 Update가 실제로 돈다. 직접 손 입력·밸런스 확인이 아니다.
/// </summary>
public static class BossOrbVfxCheck
{
    const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    static void Set(object o, string n, object v) => o.GetType().GetField(n, Hidden).SetValue(o, v);
    static object Call(object o, string n, params object[] args) => o.GetType().GetMethod(n, Hidden).Invoke(o, args);
    static T Find<T>() where T : UnityEngine.Object => UnityEngine.Object.FindFirstObjectByType<T>();
    static Enemy Boss() => UnityEngine.Object.FindObjectsByType<Enemy>(FindObjectsInactive.Include, FindObjectsSortMode.None).Single(e => e.HasPhases);
    static BossOrbVisual Visual(Enemy orb) => orb.GetComponentInChildren<BossOrbVisual>(true);
    static OrbBreakVfx[] Breaks() => UnityEngine.Object.FindObjectsByType<OrbBreakVfx>(FindObjectsSortMode.None);
    static bool Near(float a, float b, float eps = .001f) => Mathf.Abs(a - b) < eps;

    static int frames, passed;
    static string notes;
    public static string Result { get; private set; }

    static void Steps(int n) { for (int i = 0; i < n; i++) EditorApplication.Step(); frames += n; }
    static void Check(bool ok, string label) { if (!ok) throw new Exception("BossOrbVfx: " + label); passed++; }
    static void Fresh() { Call(Find<BattleFlow>(), "BeginBattle", 3, true, null); Find<Player>().AddShield(100000); }
    static void StepUntil(Func<bool> done, int max, string label) { int i = 0; while (!done()) { if (++i > max) throw new Exception("BossOrbVfx timeout: " + label); Steps(1); } }
    static void SetSpeed(float s) { var info = Find<CombatInfoUI>(); if (!Near(info.BattleSpeed, s)) info.ToggleSpeed(); }

    /// <summary>전환을 실제 프레임으로 끝까지 진행해 오브를 활성화한다.</summary>
    static BossOrbs Enter(int phase)
    {
        Fresh(); var e = Boss(); e.TakeDamage(phase == 2 ? 1600 : 3000); e.TryInterrupt(2.5f);
        StepUntil(() => !e.IsPhaseTransitioning, 400, "transition " + phase);
        var o = e.GetComponent<BossOrbs>();
        StepUntil(() => !Visual(o.Ruin).IsSummoning && !Visual(o.Cycle).IsSummoning, 100, "idle " + phase);
        return o;
    }

    static bool UseCard(Func<SkillData, bool> pick, Enemy target)
    {
        var d = Find<DeckSystem>(); var c = Find<CostSystem>();
        var cards = d.CopyStartingDeck(); var card = cards.First(pick);
        cards.Remove(card); cards.Insert(0, card); d.SetDeck(cards); c.EnsureTutorialCost(10);
        if (target != null) Call(Find<EnemyManager>(), "SelectTarget", target);
        bool ok = d.TryUseSlot(0);
        StepUntil(() => !d.IsCasting, 300, "card " + card.name);
        return ok;
    }

    // ───────── 화면 기록 도우미(근거 화면용). 촬영 프레임 동안만 아주 짧은 프레임 시간을 써서 상태가 거의 움직이지 않게 한다. ─────────

    public static void Advance(int n) { Time.captureDeltaTime = .02f; for (int i = 0; i < n; i++) EditorApplication.Step(); }

    public static string Shot(string path)
    {
        float keep = Time.captureDeltaTime; Time.captureDeltaTime = .0005f;
        string r = UiCapture.Save(path, 1920, 1080);
        Time.captureDeltaTime = keep; return r;
    }

    /// <summary>전체 화면들에서 두 오브 주변(위 파멸·아래 순환)을 잘라 가로로 잇고 2배 확대한다.</summary>
    public static void Strip(string output, int width, int height, params string[] shots)
    {
        var o = Boss().GetComponent<BossOrbs>(); var cam = Camera.main;
        Vector3 a = cam.WorldToScreenPoint(o.Ruin.transform.position), b = cam.WorldToScreenPoint(o.Cycle.transform.position);
        var strip = new Texture2D(width * shots.Length * 2, height * 4, TextureFormat.RGB24, false);
        for (int i = 0; i < shots.Length; i++)
        {
            var img = new Texture2D(2, 2); img.LoadImage(System.IO.File.ReadAllBytes(shots[i]));
            for (int row = 0; row < 2; row++)
            {
                Vector3 c = row == 0 ? a : b;
                var px = img.GetPixels((int)c.x - width / 2, (int)c.y - height / 2 + 10, width, height);
                for (int y = 0; y < height * 2; y++)
                    for (int x = 0; x < width * 2; x++)
                        strip.SetPixel(i * width * 2 + x, (1 - row) * height * 2 + y, px[(y / 2) * width + x / 2]);
            }
            UnityEngine.Object.DestroyImmediate(img);
        }
        System.IO.File.WriteAllBytes(output, strip.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(strip);
    }

    [MenuItem("Tools/Scroll Hunter/Check Boss Orb VFX (Play)")]
    public static void Menu() => Debug.Log(Run());

    public static string Run()
    {
        if (!Application.isPlaying) throw new InvalidOperationException("Play required");
        var f = Find<BattleFlow>(); var t = Find<TutorialFlow>(); var info = Find<CombatInfoUI>(); var p = Find<Player>(); var m = Find<EnemyManager>();
        bool tutorial = t.enabled, paused = EditorApplication.isPaused; float capture = Time.captureDeltaTime, speed = info.BattleSpeed;
        var random = UnityEngine.Random.state;
        frames = passed = 0; notes = "";
        EditorApplication.isPaused = true; Time.captureDeltaTime = .02f; t.enabled = false;
        try
        {
            // ① 최초 전환 소환 → 활성화 → 대기. 판정 크기(루트·충돌체)는 그대로, 성장은 시각 자식만.
            f.RestartRun(); Fresh(); SetSpeed(1f);
            var e = Boss(); var o = e.GetComponent<BossOrbs>();
            Check(Breaks().Length == 0, "no break effects at start");
            e.TakeDamage(1600); e.TryInterrupt(2.5f);
            var rv = Visual(o.Ruin); var cv = Visual(o.Cycle);
            Check(rv != null && cv != null && !o.Ruin.GetComponent<Renderer>().enabled && !o.Cycle.GetComponent<Renderer>().enabled, "visual attached, temp sphere hidden");
            Check(rv.IsSummoning && cv.IsSummoning && rv.CurrentScale < .2f, "transition starts small summon");
            var col = (SphereCollider)o.Ruin.GetComponent<Collider>(); float root = o.Ruin.transform.localScale.x, radius = col.radius;
            float last = 0f; bool grew = true; int transitionFrames = 0;
            while (e.IsPhaseTransitioning)
            {
                if (++transitionFrames > 300) throw new Exception("transition timeout");
                if (transitionFrames == 40)
                {
                    info.TogglePause(); float s0 = rv.CurrentScale, t0 = rv.PhaseElapsed; Steps(15);
                    Check(Near(rv.CurrentScale, s0, 1e-5f) && Near(rv.PhaseElapsed, t0, 1e-5f) && e.IsPhaseTransitioning, "summon frozen while paused");
                    info.Resume();
                }
                Steps(1);
                if (!e.IsPhaseTransitioning) break;
                if (o.RuinActive || o.CycleActive) throw new Exception("orb active before transition end");
                if (col.enabled || !Near(o.Ruin.transform.localScale.x, root)) throw new Exception("judgement size/collider changed during summon");
                if (rv.CurrentScale < last - 1e-4f) grew = false;
                last = rv.CurrentScale;
            }
            Check(o.RuinActive && o.CycleActive && col.enabled, "activation on the transition-end frame");
            Check(grew && last > .7f, "core grows during transition (" + last.ToString("0.00") + ")");
            Check(rv.IsSummoning && cv.IsSummoning, "completion pulse after activation");
            Check(Near(o.Ruin.transform.localScale.x, root) && Near(col.radius, radius), "collider size unchanged by pulse");
            StepUntil(() => !rv.IsSummoning && !cv.IsSummoning, 60, "pulse to idle");
            Vector3 pos = o.Ruin.transform.position; float lo = 9f, hi = 0f;
            for (int i = 0; i < 120; i++) { Steps(1); lo = Mathf.Min(lo, rv.CurrentScale); hi = Mathf.Max(hi, rv.CurrentScale); }
            Check(o.Ruin.transform.position == pos && rv.transform.localPosition == Vector3.zero && lo > .9f && hi < 1.1f, "idle pulse only, no position shake");
            notes += "initial: transition " + transitionFrames + "f, core 0.12→" + last.ToString("0.00") + ", idle pulse " + lo.ToString("0.000") + "~" + hi.ToString("0.000") + "\n";

            // ② 단일 공격으로 파멸·순환 각각 파괴: 판정(보스 150)은 같은 프레임, 파괴 연출 1개, 정지·0.5배속 추종, 스스로 소멸
            p.SetSanctuary(true);
            o.Ruin.TakeDamage(o.Ruin.CurrentHp - 1);
            int hp = e.CurrentHp; Vector3 ruinPos = o.Ruin.transform.position;
            Check(UseCard(x => !x.IsAreaOfEffect && x.Damage > 0 && !x.IsChanneling, o.Ruin), "single card accepted");
            var br = Breaks();
            Check(!o.RuinActive && br.Length == 1 && e.CurrentHp == hp - 150, "single kill: one break, boss 150 immediately");
            var brk = br[0];
            Check(Vector3.Distance(brk.transform.position, ruinPos) < .01f && !brk.Released, "break at orb position, contracting");
            info.TogglePause(); float el = brk.Elapsed; Steps(12); Check(Near(brk.Elapsed, el, 1e-5f), "break frozen while paused"); info.Resume();
            SetSpeed(.5f); el = brk.Elapsed; Steps(5); Check(Near(brk.Elapsed - el, 5 * .02f * .5f, .002f), "break follows 0.5x"); SetSpeed(1f);
            int breakFrames = 0; StepUntil(() => { breakFrames++; return brk == null; }, 200, "break self destroy");
            Check(Breaks().Length == 0, "break removed itself");
            notes += "single ruin: break gone after " + breakFrames + "f (pause/0.5x included)\n";
            o.Cycle.TakeDamage(o.Cycle.CurrentHp - 1); hp = e.CurrentHp;
            Check(UseCard(x => !x.IsAreaOfEffect && x.Damage > 0 && !x.IsChanneling, o.Cycle), "single card on cycle");
            Check(!o.CycleActive && Breaks().Length == 1 && e.CurrentHp == hp - 150, "cycle single kill: one break, boss 150");
            StepUntil(() => Breaks().Length == 0, 200, "cycle break end");

            // ③ 광역으로 두 오브 동시 파괴
            o = Enter(2); e = Boss();
            o.Ruin.TakeDamage(o.Ruin.CurrentHp - 1); o.Cycle.TakeDamage(o.Cycle.CurrentHp - 1);
            Check(UseCard(x => x.IsAreaOfEffect && !x.IsChanneling && x.Damage > 0, null), "AoE accepted");
            Check(!o.RuinActive && !o.CycleActive && Breaks().Length == 2 && m.OrbsDestroyed == 2, "AoE destroys both: two breaks");
            StepUntil(() => Breaks().Length == 0, 200, "AoE breaks end");

            // ④ 3페이즈 다크홀 재소환: 발동 직후 같은 프레임 활성, 시각 요소만 성장. 둘 다 있으면 재소환 연출 없음.
            o = Enter(3); e = Boss(); p.AddShield(100000);
            o.Ruin.TakeDamage(400); StepUntil(() => Breaks().Length == 0, 200, "pre-respawn break");
            Call(e, "BeginNextCast"); Set(e, "current", e.Data.Attacks[2]);
            Call(e, "Fire");
            rv = Visual(o.Ruin); cv = Visual(o.Cycle);
            Check(o.RuinActive && o.Ruin.CurrentHp == 400 && o.Ruin.GetComponent<Collider>().enabled, "respawn active immediately after dark hole");
            Check(rv.IsSummoning && rv.CurrentScale >= .5f && !cv.IsSummoning, "respawn visual starts visible, live orb untouched");
            Check(rv.DrawnInFront && !cv.DrawnInFront, "respawn drawn over dark hole smoke");
            SetSpeed(.5f); Steps(1); float r0 = rv.PhaseElapsed; Steps(4); Check(Near(rv.PhaseElapsed - r0, 4 * .02f * .5f, .002f), "respawn summon follows 0.5x"); SetSpeed(1f);
            StepUntil(() => !rv.IsSummoning, 80, "respawn summon end");
            Check(!rv.DrawnInFront, "normal draw order after respawn");
            o.Cycle.TakeDamage(15); int cycleHp = o.Cycle.CurrentHp;
            Call(e, "BeginNextCast"); Set(e, "current", e.Data.Attacks[2]); Call(e, "Fire");
            Check(!rv.IsSummoning && !cv.IsSummoning && o.Cycle.CurrentHp == cycleHp && o.Ruin.CurrentHp == 400 && Breaks().Length == 0, "both alive: no replay/heal");

            // ⑤ 소환 중 피해·즉시 파괴: 연출 1개, 남은 소환 입자 없음
            o.Ruin.TakeDamage(400); StepUntil(() => Breaks().Length == 0, 200, "break before summon-kill");
            Call(e, "BeginNextCast"); Set(e, "current", e.Data.Attacks[2]); Call(e, "Fire");
            Steps(3); Check(rv.IsSummoning, "respawn in progress");
            o.Ruin.TakeDamage(50); Check(rv.IsSummoning && o.Ruin.CurrentHp == 350, "damage during summon keeps visual");
            o.Ruin.TakeDamage(400); Steps(1);
            Check(Breaks().Length == 1 && !o.Ruin.gameObject.activeSelf, "destroy during summon: single break, orb hidden");
            Steps(5); Check(Breaks().Length == 1, "no duplicate break");
            StepUntil(() => Breaks().Length == 0, 200, "summon-kill break end");

            // ⑥ 2→3 전환 중 살아 있는 오브: 소환 연출·회복 없음
            o = Enter(2); e = Boss(); o.Ruin.TakeDamage(30); int ruinHp = o.Ruin.CurrentHp;
            e.TakeDamage(1400); e.TryInterrupt(2.5f); Check(e.PhaseNumber == 3 && e.IsPhaseTransitioning, "phase3 transition");
            Steps(20); Check(!Visual(o.Ruin).IsSummoning && !Visual(o.Cycle).IsSummoning && o.Ruin.CurrentHp == ruinHp, "phase3 transition: live orbs not resummoned");
            StepUntil(() => !e.IsPhaseTransitioning, 200, "phase3 transition end");
            Check(!Visual(o.Ruin).IsSummoning && o.Ruin.CurrentHp == ruinHp, "phase3 activation: no replay");

            // ⑦ 2페이즈 건너뛰기: 최초 소환 연출
            Fresh(); e = Boss(); o = e.GetComponent<BossOrbs>(); e.TakeDamage(3000); e.TryInterrupt(2.5f);
            Check(e.PhaseNumber == 3 && Visual(o.Ruin).IsSummoning && Visual(o.Ruin).CurrentScale < .2f && !o.RuinActive, "phase2 skip: initial summon during transition");
            StepUntil(() => !e.IsPhaseTransitioning, 200, "skip transition"); Check(o.RuinActive && o.CycleActive, "skip activation");

            // ⑧ 재시작(소환 중): 잔여 없음
            Fresh(); e = Boss(); o = e.GetComponent<BossOrbs>(); e.TakeDamage(1600); e.TryInterrupt(2.5f); Steps(30);
            f.RestartRun(); Steps(2);
            Check(Breaks().Length == 0 && !o.Ruin.gameObject.activeSelf && !o.Cycle.gameObject.activeSelf, "restart mid-summon leaves nothing");

            // ⑨ 보스 사망(직접 처치): 정리 제거에 파괴 연출 없음
            o = Enter(3); e = Boss(); e.TakeDamage(e.CurrentHp); Steps(2);
            Check(!e.IsAlive && Breaks().Length == 0 && !o.RuinActive && !o.CycleActive && m.OrbsDestroyed == 0, "boss death cleanup: no break, no destruction count");
            Check(f.State == BattleFlowState.Victory, "boss death ends battle");

            // ⑩ 오브 파괴 피해로 보스 사망: 실제 파괴 연출 1개만, 기존 종료 처리, 연출은 실제 시간으로 끝남
            o = Enter(3); e = Boss(); Set(e, "<CurrentHp>k__BackingField", 150);
            o.Ruin.TakeDamage(400); Steps(2);
            br = Breaks();
            Check(!e.IsAlive && f.State == BattleFlowState.Victory && br.Length == 1 && !o.CycleActive, "orb-destruction kill: one break, victory");
            Check(br[0].GetComponentsInChildren<ParticleSystem>(true).All(ps => ps.main.useUnscaledTime) && Time.timeScale == 0f, "break plays out in real time after battle end");
            brk = br[0]; StepUntil(() => brk == null, 200, "unscaled break end");

            // ⑪ 플레이어 패배: 정리 제거에 파괴 연출 없음, 재시작 후 잔여 없음
            o = Enter(2); e = Boss(); p.SetSanctuary(false); p.TakeDamage(1000000); Steps(2);
            Check(f.State == BattleFlowState.Defeat && Breaks().Length == 0 && !o.RuinActive && !o.CycleActive && m.OrbsDestroyed == 0, "defeat cleanup: no break");
            o = Enter(3); o.Ruin.TakeDamage(400); Check(Breaks().Length == 1, "break before restart");
            f.RestartRun(); Steps(2); Check(Breaks().Length == 0, "restart removes playing break");

            Result = "Boss orb VFX checks passed: " + passed + " / frames " + frames + "\n" + notes;
            return Result;
        }
        catch (Exception error) { Result = "FAILED after " + passed + " (frames " + frames + "): " + error; return Result; }
        finally
        {
            p.SetSanctuary(false);
            Set(info, "resumeScale", speed);
            UnityEngine.Random.state = random; t.enabled = tutorial; f.RestartRun();
            Time.captureDeltaTime = capture; EditorApplication.isPaused = paused;
        }
    }
}
