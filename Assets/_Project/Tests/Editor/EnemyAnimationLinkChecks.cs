using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 적 모델 동작 연결 검사(10/6, 09 적 모델 Unity 확인). Battle Play에서 함수 호출과 Animator 수동 진행으로 확인한다.
/// 실제 프레임·직접 손 조작 검사와 구분한다. 끝나면 런을 다시 시작해 처음 상태로 돌린다.
/// </summary>
public static class EnemyAnimationLinkChecks
{
    const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    static object Get(object o, string n) => o.GetType().GetField(n, Hidden).GetValue(o);
    static void Set(object o, string n, object v) => o.GetType().GetField(n, Hidden).SetValue(o, v);
    static object Call(object o, string n, params object[] args) => o.GetType().GetMethod(n, Hidden).Invoke(o, args);
    static T Find<T>() where T : UnityEngine.Object => UnityEngine.Object.FindFirstObjectByType<T>();

    static EnemyAnimationDriver Driver(Enemy e)
    {
        foreach (var d in UnityEngine.Object.FindObjectsByType<EnemyAnimationDriver>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if ((Enemy)Get(d, "enemy") == e) return d;
        return null;
    }

    static bool InState(Animator a, string state, int layer = 0)
    {
        var cur = a.GetCurrentAnimatorStateInfo(layer);
        if (cur.IsName(state)) return true;
        return a.IsInTransition(layer) && a.GetNextAnimatorStateInfo(layer).IsName(state);
    }

    [MenuItem("Tools/Scroll Hunter/Check Enemy Death Ending (Play)")]
    public static void EndingMenu() => Debug.Log(RunEnding());

    public static string RunEnding()
    {
        if (!Application.isPlaying) throw new InvalidOperationException("Battle Play에서 실행하세요.");
        var f = Find<BattleFlow>(); var t = Find<TutorialFlow>(); var m = Find<EnemyManager>();
        var deck = Find<DeckSystem>();
        bool tutorialEnabled = t.enabled;
        float hold = (float)Get(f, "finishingEffectHold");
        int passed = 0;
        Action<bool, string> check = (ok, message) => { if (!ok) throw new Exception(message); passed++; };
        var all = UnityEngine.Object.FindObjectsByType<Enemy>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        try
        {
            t.enabled = false; Set(f, "finishingEffectHold", 0.6f);
            foreach (var e in all)
            {
                f.RestartRun(); m.BeginBattle(new[] { e });
                var d = Driver(e); var a = d.GetComponent<Animator>();
                e.TakeDamage(e.CurrentHp); m.NotifyEnemyDied();
                check(a.updateMode == AnimatorUpdateMode.Normal, e.name + " death initially uses game time");
                Call(f, "Update");
                float remaining = (float)Get(f, "panelHoldRemaining");
                check(Time.timeScale == 0f && a.updateMode == AnimatorUpdateMode.UnscaledTime, e.name + " victory only animates corpse");
                check(remaining > 0f && !f.CanEditOrder && !deck.TryUseSlot(0), e.name + " result waits / combat input blocked");
                for (int i = 0; i < Mathf.CeilToInt(remaining * 60f) + 1; i++) a.Update(1f / 60f);
                var state = a.GetCurrentAnimatorStateInfo(0);
                check(state.IsName("Death") && state.normalizedTime >= 1f, e.name + " death finishes within hold");
                Set(f, "panelHoldRemaining", -0.01f); Call(f, "RefreshUI");
                check(((GameObject)Get(Find<BattleRewardUI>(), "panel")).activeSelf, e.name + " reward becomes available");
                f.RestartRun();
                check(!d.IsDead && !d.IsDetached && a.updateMode == AnimatorUpdateMode.Normal, e.name + " restart restores normal animation time");
            }
            // 마지막 적이 아닌 사망에는 실제 시간 재생을 적용하지 않는다.
            f.RestartRun(); var first = m.Enemies[0]; var other = m.Enemies[1];
            var corpse = Driver(first).GetComponent<Animator>();
            first.TakeDamage(first.CurrentHp); m.NotifyEnemyDied(); Call(f, "Update");
            check(other.IsAlive && !m.CombatEnded && corpse.updateMode == AnimatorUpdateMode.Normal, "mid-battle death keeps pause / speed rules");
            // 보스 승리도 보상 대신 결과 화면을 모션 뒤에 연다.
            var encounters = (BattleFlow.Encounter[])Get(f, "encounters");
            Call(f, "BeginBattle", encounters.Length - 1, true, null);
            var boss = m.Enemies[0]; boss.TakeDamage(boss.CurrentHp); m.NotifyEnemyDied(); Call(f, "Update");
            var result = (GameObject)Get(f, "resultPanel");
            check(f.State == BattleFlowState.Victory && !result.activeSelf, "boss result waits for death");
            Set(f, "panelHoldRemaining", -0.01f); Call(f, "RefreshUI");
            check(result.activeSelf, "boss result opens after presentation");
        }
        finally
        {
            Set(f, "finishingEffectHold", hold); t.enabled = tutorialEnabled; f.RestartRun();
        }
        return "Enemy death ending checks passed: " + passed + ". Function/Animator checks; actual frames separate.";
    }

    [MenuItem("Tools/Scroll Hunter/Check Enemy Animation Link (Play)")]
    public static void Menu() => Debug.Log(Run());

    public static string Run()
    {
        if (!Application.isPlaying) throw new InvalidOperationException("Battle Play에서 실행하세요.");
        var f = Find<BattleFlow>(); var t = Find<TutorialFlow>(); var m = Find<EnemyManager>(); var p = Find<Player>();
        int passed = 0;
        var log = new List<string>();
        Action<bool, string> check = (ok, msg) => { if (!ok) throw new Exception("Animation link: " + msg); passed++; };
        bool tutorialEnabled = t.enabled; float hold = (float)Get(f, "finishingEffectHold");
        float speed = Time.timeScale; var random = UnityEngine.Random.state;

        void Tick(Enemy e, float dt) { Set(m, "lastFireTime", float.NegativeInfinity); Call(e, "AdvanceCombat", dt); }
        void Step(Animator a, float dt = 0.02f) => a.Update(dt);
        string Name(Enemy e) => e.Data.name;
        void TickUntil(Enemy e, Func<bool> done, string what, float dt = 0.05f, int max = 400)
        {
            for (int i = 0; i < max && !done(); i++) Tick(e, dt);
            check(done(), Name(e) + " " + what);
        }
        void Heal() { if ((float)Get(p, "currentHp") < 200f) p.BeginBattle(true); }

        void CheckEncounter(string label, IList<Enemy> enemies, bool killAll)
        {
            foreach (Enemy e in enemies)
            {
                var d = Driver(e); check(d != null, label + " " + Name(e) + " driver");
                var a = d.GetComponent<Animator>(); bool archer = Get(d, "bow") != null;
                check(!d.IsDetached && d.transform.parent == e.transform && d.gameObject.activeInHierarchy, label + " " + Name(e) + " model attached");
                Step(a);
                check(e.IsCasting && InState(a, archer ? "Draw" : "Cast"), label + " " + Name(e) + " cast start pose");
                float hips = a.GetBoneTransform(HumanBodyBones.Hips).position.y - d.transform.position.y;
                check(hips > 0.6f * d.transform.lossyScale.y * 0.5f, label + " " + Name(e) + " standing pose (no T/death)");
                if (archer)
                {
                    float expect = ((AnimationClip)Get(d, "drawClip")).length / e.CurrentCastTime;
                    check(Mathf.Abs(a.GetFloat("DrawSpeed") - expect) < 1e-4f, label + " archer draw speed = clip / cast");
                }

                // 발동: 실제 진행(AdvanceCombat)으로 시전을 끝까지 보낸다.
                bool fired = false; Action<CastColor> onFire = c => fired = true;
                e.AttackFired += onFire;
                Heal();
                int arrowsBefore = d.ArrowsInFlight;
                TickUntil(e, () => fired, "fires");
                e.AttackFired -= onFire;
                Step(a);
                check(archer ? InState(a, "Release") && d.ArrowsInFlight == arrowsBefore + 1
                             : InState(a, "Fire0") || InState(a, "Fire1") || InState(a, "Fire2") || InState(a, "Fire3") || InState(a, "Fire4"), label + " " + Name(e) + " fire pose/arrow");

                // 다음 시전까지 진행 → 준비 동작으로 복귀.
                TickUntil(e, () => e.IsCasting, "next cast");
                Step(a);
                check(InState(a, archer ? "Draw" : "Cast"), label + " " + Name(e) + " back to cast pose");

                // 시전 중 일반 피해: 피격 동작 없이 논리 시전·진행 유지. 연속 3회.
                float progress = e.CastProgress01; var attack = e.CurrentAttack;
                for (int k = 0; k < 3; k++) { e.TakeDamage(1); Step(a, 0.03f); }
                int flinch = a.GetLayerIndex("Flinch");
                check(e.IsCasting && e.CurrentAttack == attack && Mathf.Abs(e.CastProgress01 - progress) < 1e-4f, label + " " + Name(e) + " cast kept on hits");
                check(InState(a, archer ? "Draw" : "Cast") || InState(a, "Aim"), label + " " + Name(e) + " cast pose kept on hits");
                check(flinch < 0 || !InState(a, "Flinch", flinch), label + " " + Name(e) + " no flinch on ordinary damage");

                // 차단: 초록·주황 시전에만. 성공하면 Hit, 경직 뒤 다음 시전으로 복귀.
                if (e.CastColor != CastColor.Red && !e.HasPhases)
                {
                    check(e.TryInterrupt(2.5f) == InterruptResult.Success, label + " " + Name(e) + " interrupt success");
                    Step(a);
                    check(InState(a, "Hit") && d.ArrowsInFlight <= arrowsBefore + 1, label + " " + Name(e) + " hit pose on interrupt");
                    TickUntil(e, () => e.IsCasting, "recover after stagger");
                    Step(a);
                    check(InState(a, archer ? "Draw" : "Cast"), label + " " + Name(e) + " cast after stagger");
                }
            }
            if (!killAll) return;
            foreach (Enemy e in enemies)
            {
                var d = Driver(e); var a = d.GetComponent<Animator>();
                if (e.HasPhases) Set(e, "phaseTransitionRemaining", 0f);
                e.TakeDamage(e.CurrentHp);
                m.NotifyEnemyDied();   // 카드 피해 경로(DeckSystem)와 같은 순서
                check(!e.IsAlive && !e.gameObject.activeSelf, label + " " + Name(e) + " logic death unchanged");
                check(d.IsDetached && d.IsDead && d.gameObject.activeInHierarchy, label + " " + Name(e) + " corpse visible");
                check(m.CurrentTarget != e, label + " " + Name(e) + " target moved");
                Step(a, 0.1f);
                check(InState(a, "Death"), label + " " + Name(e) + " death pose");
                check(e.TryInterrupt(2.5f) == InterruptResult.NotCasting, label + " " + Name(e) + " dead not interruptible");
                Step(a, 2f);
                check(InState(a, "Death"), label + " " + Name(e) + " stays dead");
            }
        }

        try
        {
            t.enabled = false; Set(f, "finishingEffectHold", 0f);
            f.RestartRun();
            f.BeginTutorial(false);   // 튜토리얼 흐름을 끈 상태의 재시작은 A+B로 바로 가므로 튜토리얼 전투를 직접 연다
            Time.timeScale = 1f;
            var tutorialEnemy = t.Enemy;
            CheckEncounter("tutorial", new[] { tutorialEnemy }, false);
            var encounters = (BattleFlow.Encounter[])Get(f, "encounters");
            for (int i = 0; i < encounters.Length; i++)
            {
                List<Enemy> previous = m.Enemies.ToList();
                Call(f, "BeginBattle", i, true, null);
                Time.timeScale = 1f;
                foreach (Enemy e in previous)
                {
                    var d = Driver(e);
                    check(!d.IsDetached && d.transform.parent == e.transform && d.ArrowsInFlight == 0, "battle " + i + " previous " + Name(e) + " reattached/cleared");
                }
                bool boss = encounters[i].enemies.Any(e => e.HasPhases);
                CheckEncounter("battle " + i, m.Enemies.ToList(), !boss);
                if (boss)
                {
                    // 페이즈 전환: 쉬는 중 임계치를 넘기면 전환(시전 없음) → 끝나면 2페이즈 시전(배율 반영)으로 준비 동작.
                    Enemy b = m.Enemies[0]; var a = Driver(b).GetComponent<Animator>();
                    TickUntil(b, () => !b.IsCasting && !b.IsPhaseTransitioning, "rest before phase");
                    float threshold = b.Data.Phases[1].HpThreshold * b.MaxHp;
                    b.TakeDamage(Mathf.CeilToInt(b.CurrentHp - threshold + 1));
                    check(b.IsPhaseTransitioning && b.PhaseNumber == 2, "boss phase transition");
                    Step(a, 0.3f);
                    check(!InState(a, "Cast"), "boss no cast pose during transition");
                    TickUntil(b, () => b.IsCasting, "boss phase 2 cast");
                    Step(a);
                    check(InState(a, "Cast") && Mathf.Abs(b.CurrentCastTime - b.CurrentAttack.CastTime * b.Data.Phases[1].CastTimeMultiplier) < 1e-4f, "boss phase 2 cast pose and time");
                    CheckEncounter("boss kill", new[] { b }, true);
                }
            }

            // 재시작: 사망 모델이 원위치·대기/시전 자세로 돌아온다.
            f.RestartRun();
            foreach (var d in UnityEngine.Object.FindObjectsByType<EnemyAnimationDriver>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                check(!d.IsDetached && !d.IsDead && d.ArrowsInFlight == 0, "restart " + d.name + " reset");
            log.Add("Encounters: tutorial + " + encounters.Length);
        }
        finally
        {
            t.enabled = tutorialEnabled; Set(f, "finishingEffectHold", hold); UnityEngine.Random.state = random;
            f.RestartRun(); Time.timeScale = speed;
        }
        return "Enemy animation link checks passed: " + passed + ". " + string.Join(" ", log) + " Function calls and manual Animator steps; real frames and physical input separate.";
    }

    [MenuItem("Tools/Scroll Hunter/Check Enemy Attack Timing (Play)")]
    public static void TimingMenu() => Debug.Log(RunTiming());

    public static string RunTiming()
    {
        if (!Application.isPlaying) throw new InvalidOperationException("Battle Play required");
        int checks = 0, cases = 0;
        void Check(bool ok, string label) { if (!ok) throw new Exception("Attack timing: " + label); checks++; }
        var sources = UnityEngine.Object.FindObjectsByType<Enemy>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where(e => e.GetComponentInChildren<EnemyAnimationDriver>(true) != null).ToArray();
        float scale = Time.timeScale;
        try
        {
            foreach (var source in sources)
            {
                // 별도 런타임 오브젝트. 씬 적·HP·데이터를 바꾸지 않는다.
                var root = new GameObject("TimingCheck"); root.SetActive(false); root.transform.position = Vector3.right * 200f;
                EnemyAnimationDriver d = null;
                try
                {
                    var e = new GameObject("Enemy").AddComponent<Enemy>(); e.transform.SetParent(root.transform, false);
                    e.gameObject.SetActive(false); Set(e, "data", source.Data);
                    var p = new GameObject("Player").AddComponent<Player>(); p.transform.SetParent(root.transform, false); p.enabled = false;
                    var mg = new GameObject("Manager"); mg.SetActive(false); mg.transform.SetParent(root.transform, false);
                    var m = mg.AddComponent<EnemyManager>(); m.enabled = false;
                    Set(m, "enemies", new List<Enemy> { e }); Set(m, "player", p);
                    var sourceDriver = source.GetComponentInChildren<EnemyAnimationDriver>(true);
                    string prefab = "Assets/_Project/Prefabs/Enemies/" + sourceDriver.GetComponent<Animator>().runtimeAnimatorController.name + ".prefab";
                    var model = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(prefab), e.transform);
                    d = model.GetComponent<EnemyAnimationDriver>(); var a = model.GetComponent<Animator>();
                    root.SetActive(true); e.gameObject.SetActive(true); mg.SetActive(true);
                    Set(p, "metrics", null); Set(p, "tutorial", null); Set(p, "maxHp", 10000f);
                    Set(m, "metrics", null); Set(m, "combatEnded", false);
                    bool archer = Get(d, "bow") != null;
                    int phaseCount = e.HasPhases ? e.Data.Phases.Count : 1;
                    for (int phase = 0; phase < phaseCount; phase++)
                    foreach (var attack in e.Data.Attacks)
                    {
                        d.ResetPose(); e.BeginBattle(); Set(e, "phaseIndex", phase); Set(e, "current", attack);
                        Set(p, "currentHp", 10000f); Time.timeScale = 1f;
                        Call(d, "OnCastStarted"); a.Update(0.2f);
                        int fired = 0; Action<CastColor> onFire = c => fired++;
                        e.AttackFired += onFire;
                        string label = source.name + " phase" + phase + " " + attack.SkillName;
                        try
                        {
                            float duration = e.CurrentCastTime;
                            var motions = (EnemyAnimationDriver.AttackMotion[])Get(d, "attacks");
                            int index = attack.CastColor == CastColor.Green ? 0 : attack.CastColor == CastColor.Orange ? 1 : 2;
                            if (!archer && (bool)Get(d, "useAttackOrder"))
                                index = e.Data.Attacks.ToList().IndexOf(attack);
                            if (!archer && (motions[index] == null || motions[index].clip == null)) index = 0;
                            var motion = archer ? null : motions[index];
                            float windup = archer ? duration : Mathf.Min(duration, (motion.impact - motion.start) * motion.clip.length);
                            var castOnce = (AnimationClip)Get(d, motion != null && motion.holdRaisedHand ? "chargeRaiseClip" : "castOnceClip");
                            if (castOnce != null)
                            {
                                Check(!castOnce.isLooping, label + " preparation is not looping");
                                float prep = duration - windup;
                                float onceTime = Mathf.Min(prep, castOnce.length);
                                if (onceTime > 0f)
                                {
                                    Call(e, "AdvanceCombat", onceTime * 0.5f); a.Update(0f);
                                    Check(Mathf.Abs(a.GetFloat("CastTime") - 0.5f) < 0.001f, label + " one preparation progresses");
                                    Call(e, "AdvanceCombat", Mathf.Max(0f, prep - onceTime * 0.5f - 0.001f)); a.Update(0f);
                                    Check(a.GetFloat("CastTime") > 0.99f && a.GetFloat("CastTime") <= 1f,
                                        label + " preparation ends once / holds without wrap");
                                    if (motion.holdRaisedHand && prep > castOnce.length)
                                        Check(InState(a, "CastChargeHold"), label + " raised preparation enters breathing hold");
                                    Set(e, "castTimer", 0f); Call(d, "OnCastStarted"); a.Update(0.2f);
                                    Check(a.GetFloat("CastTime") == 0f, label + " next cast resets preparation");
                                }
                            }
                            // 공격 전반부를 이미 보여주지만 판정·피해는 아직 없다.
                            Call(e, "AdvanceCombat", duration - windup * 0.4f); a.Update(0.2f);
                            Check(e.IsCasting && fired == 0 && p.CurrentHp == 10000f, label + " no early damage");
                            if (!archer)
                                Check(d.IsAttackMotionActive && InState(a, "Fire" + index)
                                    && d.AttackNormalizedTime > motion.start && d.AttackNormalizedTime < motion.impact, label + " windup before cast end");

                            float before = d.AttackNormalizedTime; float castBefore = e.CastProgress01;
                            for (int k = 0; k < 3; k++) e.TakeDamage(1);
                            a.Update(0f);
                            Check(!InState(a, "Hit") && a.GetLayerIndex("Flinch") < 0 && e.CastProgress01 == castBefore
                                && d.AttackNormalizedTime == before, label + " ordinary hits do not react/restart");

                            Time.timeScale = 0f; Call(e, "AdvanceCombat", 5f); Call(d, "AdvanceRecovery", 5f); a.Update(0f);
                            Check(e.CastProgress01 == castBefore && d.AttackNormalizedTime == before && fired == 0, label + " pause");
                            Time.timeScale = 1f;

                            // 다른 적의 발동 간격 대기. 게이지가 가득 차도 자세·HP는 유지.
                            Set(m, "lastFireTime", Time.time); Call(e, "AdvanceCombat", duration); a.Update(0.1f);
                            Check(e.CastProgress01 == 1f && fired == 0 && p.CurrentHp == 10000f, label + " fire gap waits");
                            float held = d.AttackNormalizedTime;
                            Call(e, "AdvanceCombat", 0.4f); a.Update(0.1f);
                            Check(d.AttackNormalizedTime == held && fired == 0, label + " pose held during gap");
                            if (!archer) Check(held < motion.impact && held > motion.start, label + " hold before contact");

                            Set(m, "lastFireTime", float.NegativeInfinity); Call(e, "AdvanceCombat", 0f); a.Update(0f);
                            int expected = Mathf.FloorToInt(attack.Damage * 100f / (100f + p.Defense) + 0.5f);
                            Check(fired == 1 && p.CurrentHp == 10000f - expected, label + " unchanged one hit damage");
                            if (archer) Check(InState(a, "Release") && d.ArrowsInFlight == 1, label + " release on fire");
                            else
                            {
                                Check(InState(a, "Fire" + index) && Mathf.Abs(d.AttackNormalizedTime - motion.impact) < 0.0001f, label + " impact on fire (no restart)");
                                Set(d, "firedFrame", -1); Call(d, "AdvanceRecovery", 0.1f); a.Update(0f);
                                Check(d.AttackNormalizedTime > motion.impact && fired == 1, label + " recovery only");
                                Call(d, "AdvanceRecovery", motion.clip.length); a.Update(0.2f);
                                Check(InState(a, "Idle") && !d.IsAttackMotionActive, label + " returns to idle");
                            }

                            // 발동 직전 차단 성공/빨강 실패. 취소된 동작은 뒤늦게 발동하지 않는다.
                            d.ResetPose(); e.BeginBattle(); Set(e, "phaseIndex", phase); Set(e, "current", attack); Call(d, "OnCastStarted");
                            Call(e, "AdvanceCombat", duration - windup * 0.2f); a.Update(0.2f);
                            int firesBefore = fired;
                            var result = e.TryInterrupt(2.5f); a.Update(0.15f);
                            if (attack.CastColor == CastColor.Red)
                                Check(result == InterruptResult.FailedRed && e.IsCasting && !InState(a, "Hit"), label + " red failure no hit pose");
                            else
                            {
                                Check(result == InterruptResult.Success && InState(a, "Hit") && !d.IsAttackMotionActive, label + " interrupt cancels windup");
                                Call(e, "AdvanceCombat", 1f); Call(d, "AdvanceRecovery", 1f); a.Update(0.1f);
                                Check(fired == firesBefore && !d.IsAttackMotionActive, label + " no delayed strike");
                            }
                            cases++;
                        }
                        finally { e.AttackFired -= onFire; }
                    }
                    e.TakeDamage(e.CurrentHp); a.Update(0.15f);
                    Check(d.IsDead && d.IsDetached && InState(a, "Death") && !d.IsAttackMotionActive, source.name + " death cancels attack");
                    e.SetEncounterActive(false); e.BeginBattle(); a.Update(0.2f);
                    Check(!d.IsDetached && !d.IsDead, source.name + " encounter restart clears death");
                }
                finally
                {
                    if (d != null && d.IsDetached) UnityEngine.Object.DestroyImmediate(d.gameObject);
                    UnityEngine.Object.DestroyImmediate(root);
                }
            }
        }
        finally { Time.timeScale = scale; }
        return $"Enemy attack timing checks passed: {checks}, attacks/phases: {cases}, models: {sources.Length}. Function/Animator checks; real frames separate.";
    }

    /// <summary>전투 카메라 기준 좌/중/우의 시선·몸 보존·사망 해제. Time.deltaTime > 0인 Play에서 실행.</summary>
    public static string RunFacing()
    {
        if (!Application.isPlaying || Time.deltaTime <= 0f) throw new InvalidOperationException("1배속 Play에서 실행하세요.");
        int checks = 0;
        void Check(bool ok, string why) { if (!ok) throw new Exception("Facing: " + why); checks++; }
        var cameraObject = new GameObject("FacingCheckCamera");
        var camera = cameraObject.AddComponent<Camera>(); camera.enabled = false;
        Vector3 stage = new Vector3(200, 0, 0);
        camera.transform.position = Camera.main.transform.position + stage;
        var log = new System.Text.StringBuilder();
        try
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Project/Prefabs/Enemies" }))
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                var go = UnityEngine.Object.Instantiate(prefab, stage, prefab.transform.rotation);
                try
                {
                    var a = go.GetComponent<Animator>(); var d = go.GetComponent<EnemyAnimationDriver>();
                    Set(d, "lookCamera", camera);
                    if (prefab.name.Contains("Thug")) Check(Mathf.Abs(Mathf.DeltaAngle(go.transform.eulerAngles.y, 25f)) < 0.01f, "Thug Y25");
                    var head = a.GetBoneTransform(HumanBodyBones.Head);
                    var body = new[] { HumanBodyBones.Hips, HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot, HumanBodyBones.LeftHand, HumanBodyBones.RightHand };
                    string prepare = a.HasState(0, Animator.StringToHash("Cast")) ? "Cast" : "Aim";
                    foreach (float x in new[] { -2.5f, 0f, 2.5f })
                    foreach (string state in new[] { "Idle", prepare })
                    {
                        go.transform.position = stage + Vector3.right * x;
                        d.enabled = false; a.Play(state, 0, 0.5f); a.Update(0f);
                        var positions = body.Select(b => a.GetBoneTransform(b).position).ToArray();
                        float before = Vector3.Angle(-head.forward, camera.transform.position - head.position);
                        d.enabled = true; a.Play(state, 0, 0.5f); a.Update(0f);
                        float after = Vector3.Angle(-head.forward, camera.transform.position - head.position);
                        Check(after < 12f, prefab.name + " " + state + " gaze angle " + after);
                        for (int i = 0; i < body.Length; i++)
                            Check(Vector3.Distance(positions[i], a.GetBoneTransform(body[i]).position) < 0.005f, prefab.name + " body moved " + body[i]);
                        log.AppendLine($"{prefab.name} x{x} {state}: angle {before:0.00} -> {after:0.00}");
                    }
                    foreach (string state in new[] { a.HasState(0, Animator.StringToHash("Fire0")) ? "Fire0" : "Release", "Hit", "Death" })
                    {
                        a.Play(state, 0, 0.4f);
                        int steps = Mathf.CeilToInt(0.2f / Time.deltaTime) + 2;
                        for (int i = 0; i < steps; i++) a.Update(0f);
                        Check((float)Get(d, "lookWeight") < 0.001f, prefab.name + " releases " + state);
                        Quaternion original = head.rotation;
                        camera.transform.position += Vector3.right * 5f; a.Update(0f);
                        Check(Quaternion.Angle(original, head.rotation) < 0.01f, prefab.name + " no tracking " + state);
                        camera.transform.position -= Vector3.right * 5f;
                    }
                    Call(d, "OnDied"); a.Play("Death", 0, 0.5f); a.Update(0.2f);
                    Check(d.IsDead, prefab.name + " dead");
                    d.ResetPose(); a.Update(0f);
                    Check(!d.IsDead && Vector3.Angle(-head.forward, camera.transform.position - head.position) < 12f, prefab.name + " reset gaze");
                }
                finally { UnityEngine.Object.DestroyImmediate(go); }
            }
        }
        finally { UnityEngine.Object.DestroyImmediate(cameraObject); }
        return $"Enemy facing checks passed: {checks}. Function/Animator checks.\n" + log;
    }
}
