using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// 게임 소리 연결 실제 프레임 검사(10/8, 10 A48). Play를 멈추지 않고 게임 프레임마다 한 단계씩 진행한다(에디터 일시정지·Step은 소리 재생을 멈추므로 쓰지 않음). 프레임 시간 0.02초 고정.
/// 전투 시작·카드 사용·적 시전 지정·피해·정지·배속·버튼 클릭은 함수 호출이고 그 뒤 Update가 실제로 돈다.
/// AudioSource 재생 여부·시점·횟수·출력 그룹만 본다. 음질·타격감·음량 균형(사람이 듣는 판단)은 검사하지 않는다.
/// </summary>
public static class GameAudioChecks
{
    const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    static void Set(object o, string n, object v) => o.GetType().GetField(n, Hidden).SetValue(o, v);
    static object Get(object o, string n) => o.GetType().GetField(n, Hidden).GetValue(o);
    static object Call(object o, string n, params object[] a) => o.GetType().GetMethod(n, Hidden).Invoke(o, a);
    static T Find<T>() where T : UnityEngine.Object => UnityEngine.Object.FindFirstObjectByType<T>(FindObjectsInactive.Include);
    static Enemy EnemyOf(string data) => UnityEngine.Object.FindObjectsByType<Enemy>(FindObjectsInactive.Include, FindObjectsSortMode.None)
        .First(e => !e.IsBossOrb && e.Data != null && e.Data.name == data);

    struct Entry { public SoundCue cue; public AudioClip clip; public AudioMixerGroup group; public int frame; }
    static readonly List<Entry> log = new List<Entry>();
    static int passed, frames;
    static string notes;
    public static string Result { get; private set; }

    static IEnumerator Steps(int n) { for (int i = 0; i < n; i++) { frames++; yield return null; } }
    static IEnumerator Until(Func<bool> done, int max, string label) { int i = 0; while (!done()) { if (++i > max) throw new Exception("timeout: " + label); frames++; yield return null; } }
    static IEnumerator RealWait(float seconds) { float t0 = Time.realtimeSinceStartup; while (Time.realtimeSinceStartup - t0 < seconds) { frames++; yield return null; } }
    static void Check(bool ok, string label) { if (!ok) throw new Exception(label); passed++; }
    static int Count(SoundCue cue) => log.Count(x => x.cue == cue);

    /// <summary>여러 소리를 같은 순간 시작했을 때(각 첫 파일·음량 적용) 합산 최대치. 1을 넘으면 출력에서 잘린다.</summary>
    static float MixPeak(SoundCue[] cues)
    {
        float[] sum = null;
        foreach (var cue in cues)
        {
            if (cue == null || cue.IsEmpty) continue;
            var c = cue.clips[0]; c.LoadAudioData();
            var d = new float[c.samples * c.channels];
            if (!c.GetData(d, 0)) continue;
            int n = c.samples; if (sum == null || sum.Length < n) Array.Resize(ref sum, n);
            for (int i = 0; i < n; i++) { float v = 0; for (int k = 0; k < c.channels; k++) v += d[i * c.channels + k]; sum[i] += v / c.channels * cue.volume; }
        }
        float peak = 0; if (sum != null) foreach (var v in sum) peak = Mathf.Max(peak, Mathf.Abs(v));
        return peak;
    }
    static int CountSince(SoundCue cue, int frame) => log.Count(x => x.cue == cue && x.frame >= frame);

    static BattleFlow flow; static DeckSystem deck; static Player player; static CostSystem cost; static EnemyManager manager; static CombatInfoUI info; static GameAudio audio; static SoundLibrary lib;

    static IEnumerator Fresh(int index)
    {
        Call(flow, "BeginBattle", index, true, null);
        yield return Steps(2);
        player.SetSanctuary(true);   // 스킬·적 검사 중 플레이어 피해음이 섞이지 않게(무적은 피해 알림 없음)
        foreach (var e in manager.GetAliveEnemies()) Set(e, "<CurrentHp>k__BackingField", 99999);
    }

    static void UseFirst(SkillData card, Enemy target)
    {
        var cards = deck.CopyStartingDeck();
        cards.Remove(card); cards.Insert(0, card);
        while (cards.Count > 8) cards.RemoveAt(cards.Count - 1);
        deck.SetDeck(cards); cost.EnsureTutorialCost(10);
        if (target != null) Call(manager, "SelectTarget", target);
    }

    /// <summary>적 시전을 지정한 공격으로 바꾸고 시전 시작 알림을 다시 보낸다(소리·모델·연출이 같은 공격을 보게).</summary>
    static void ForceCast(Enemy e, int attack, float progress = 0f)
    {
        // BeginNextCast와 같은 상태를 직접 맞춘다(자연 선택 공격의 시작 알림·경고가 섞이지 않게).
        Set(e, "current", e.Data.Attacks[attack]);
        Set(e, "castTimer", 0f);
        Set(e, "resting", false);
        Set(e, "staggerTimer", 0f);
        Call(e, "RefreshBar");
        var started = (Action)typeof(Enemy).GetField("CastStarted", Hidden).GetValue(e);
        started?.Invoke();
        if (progress > 0f) Set(e, "castTimer", e.CurrentCastTime * progress);
    }

    [MenuItem("Tools/Scroll Hunter/Check Game Audio (Play)")]
    public static void Menu() => Run();

    static readonly Stack<IEnumerator> stack = new Stack<IEnumerator>();
    static int lastFrame;
    static Action finish;

    /// <summary>검사를 시작한다. 끝나면 Result와 Console에 결과를 남긴다(약 1~2분).</summary>
    public static void Run()
    {
        if (!Application.isPlaying) throw new InvalidOperationException("Play required");
        if (finish != null) Stop();   // 진행 중인 검사의 기록 구독을 먼저 정리(두 번 시작해도 중복 기록 없음)
        EditorApplication.update -= Tick;
        flow = Find<BattleFlow>(); deck = Find<DeckSystem>(); player = Find<Player>(); cost = Find<CostSystem>();
        manager = Find<EnemyManager>(); info = Find<CombatInfoUI>(); audio = Find<GameAudio>();
        lib = (SoundLibrary)Get(audio, "library");
        var tutorial = Find<TutorialFlow>();
        bool tutorialOn = tutorial.enabled;
        var realSave = Get(flow, "inheritance");
        var checkPath = System.IO.Path.Combine(Application.temporaryCachePath, "AudioProgressCheck-" + Guid.NewGuid().ToString("N"), "save.json");
        Set(flow, "inheritance", new InheritanceSave(checkPath, (BattleRewardOption[])Get(flow, "rewardPool"), deck.CopyStartingDeck()));
        float capture = Time.captureDeltaTime, speed = info.BattleSpeed;
        bool background = Application.runInBackground;
        var random = UnityEngine.Random.state;
        log.Clear(); passed = frames = 0; notes = ""; Result = "running";
        Action<SoundCue, AudioClip, AudioMixerGroup> record = (c, clip, g) => log.Add(new Entry { cue = c, clip = clip, group = g, frame = Time.frameCount });
        audio.Played += record;
        var skillGroup = (AudioMixerGroup)Get(audio, "skillGroup"); var enemyGroup = (AudioMixerGroup)Get(audio, "enemyGroup");
        EditorApplication.isPaused = false; Application.runInBackground = true; Time.captureDeltaTime = .02f; tutorial.enabled = false;
        finish = () =>
        {
            audio.Played -= record;
            player.SetSanctuary(false);
            Set(info, "resumeScale", speed);
            UnityEngine.Random.state = random; tutorial.enabled = tutorialOn; Set(flow, "inheritance", realSave); flow.ReturnToTitle();
            Time.captureDeltaTime = capture; Application.runInBackground = background;
            Debug.Log("Game audio: " + Result);
        };
        stack.Clear();
        stack.Push(Sequence(skillGroup, enemyGroup));
        lastFrame = -1;
        EditorApplication.update += Tick;
    }

    static IEnumerator Sequence(AudioMixerGroup skillGroup, AudioMixerGroup enemyGroup)
    {
        flow.RestartRun(); yield return Steps(2);
        yield return AreaMusic();
        yield return CommonFeedback(skillGroup);
        yield return Skills(skillGroup);
        yield return Enemies(enemyGroup);
        yield return Boss();
        yield return PauseSpeedRestart();
        yield return Results();
        Volumes();
        Result = "Game audio checks passed: " + passed + " / frames " + frames + "\n" + notes;
    }

    /// <summary>게임 프레임이 넘어갈 때마다 한 단계 진행. 중첩 단계(IEnumerator)는 바로 이어서 시작한다.</summary>
    static void Tick()
    {
        if (!Application.isPlaying) { Result = "FAILED: play stopped"; Stop(); return; }
        if (Time.frameCount == lastFrame) return;
        lastFrame = Time.frameCount;
        try
        {
            while (stack.Count > 0)
            {
                var top = stack.Peek();
                if (!top.MoveNext()) { stack.Pop(); continue; }
                if (top.Current is IEnumerator nested) { stack.Push(nested); continue; }
                return;   // 다음 프레임까지 대기
            }
            Stop();
        }
        catch (Exception error)
        {
            Result = "FAILED after " + passed + " (frames " + frames + "): " + error.Message + " @ " + string.Join(" | ", (error.StackTrace ?? "").Split('\n').Where(l => l.Contains("GameAudio")).Take(3));
            Stop();
        }
    }

    static void Stop()
    {
        EditorApplication.update -= Tick;
        stack.Clear();
        var done = finish; finish = null;
        done?.Invoke();
    }

    // 지역별 3곡·보스·타이틀 전환. 같은 아지트 2/3전투는 곡을 되감지 않는다.
    static IEnumerator AreaMusic()
    {
        flow.ReturnToTitle(); yield return Steps(60);
        Check(audio.CurrentMusic == lib.titleMusic.clip && audio.MusicPlaying, "title BGM playing");
        var tutorial = Find<TutorialFlow>(); bool enabled = tutorial.enabled;
        try { tutorial.enabled = true; flow.StartRun(); yield return Steps(60);
            Check(flow.BattleNumber == 0 && audio.CurrentMusic == lib.tutorialMusic.clip && audio.MusicPlaying, "tutorial BGM playing"); }
        finally { tutorial.enabled = enabled; }
        var expected = new[] { lib.battleMusic, lib.hideoutMusic, lib.hideoutMusic, lib.bossMusic };
        AudioSource previous = null; int sample = 0;
        for (int i = 0; i < expected.Length; i++)
        {
            yield return Fresh(i); yield return Steps(60);
            var source = (AudioSource)Get(audio, "musicA");
            Check(audio.CurrentMusic == expected[i].clip && source.isPlaying, "battle " + (i + 1) + " area BGM playing");
            if (i == 2) Check(source == previous && source.timeSamples > sample, "hideout 2 to 3 continues without restart");
            previous = source; sample = source.timeSamples;
        }
        Check(new[] { lib.tutorialMusic.clip, lib.battleMusic.clip, lib.hideoutMusic.clip }.All(c => c != null)
            && new[] { lib.tutorialMusic.clip, lib.battleMusic.clip, lib.hideoutMusic.clip }.Distinct().Count() == 3, "three distinct pre-boss tracks");
        notes += "music: title/tutorial/path/hideout/boss, same-map continuity PASS\n";
    }

    // ① 차단·방어도·피격·포션·거절
    static IEnumerator CommonFeedback(AudioMixerGroup skillGroup)
    {
        yield return Fresh(0); player.SetSanctuary(false);
        int f = Time.frameCount; player.AddShield(100); player.TakeDamage(30); yield return Steps(8); yield return RealWait(.15f);
        Check(CountSince(lib.shieldAbsorb, f) == 1 && CountSince(lib.playerHit, f) == 0 && CountSince(lib.shieldBreak, f) == 0, "absorb only: absorb sound, no HP hit (absorb " + CountSince(lib.shieldAbsorb, f) + " hit " + CountSince(lib.playerHit, f) + " break " + CountSince(lib.shieldBreak, f) + " state " + flow.State + ")");
        player.BeginBattle(true); f = Time.frameCount; player.AddShield(10); player.TakeDamage(60); yield return Steps(8); yield return RealWait(.15f);
        Check(CountSince(lib.shieldBreak, f) == 1 && CountSince(lib.playerHit, f) == 1 && CountSince(lib.shieldAbsorb, f) == 0, "shield depleted + HP loss: break + hit once each");
        f = Time.frameCount; player.TakeDamage(30); yield return Steps(8); yield return RealWait(.15f);
        Check(CountSince(lib.playerHit, f) == 1 && CountSince(lib.shieldAbsorb, f) == 0 && CountSince(lib.shieldBreak, f) == 0, "unshielded hit: HP hit only");
        f = Time.frameCount; player.SetSanctuary(true); player.TakeDamage(30); yield return Steps(8); yield return RealWait(.15f);
        Check(CountSince(lib.playerHit, f) == 0, "invulnerable: no hit sound");
        player.SetSanctuary(false);
        f = Time.frameCount; Check(player.TryUsePotion(), "potion used"); yield return Steps(2);
        Check(CountSince(lib.potion, f) == 1 && CountSince(lib.potionLayer, f) == 1, "potion sound once");
        player.BeginBattle(true); f = Time.frameCount; Check(!player.TryUsePotion(), "full HP potion rejected"); yield return Steps(2);
        Check(CountSince(lib.potion, f) == 0, "rejected potion: no use sound");
        player.SetSanctuary(true);   // 이후 대기 중 적 주황 공격 기절이 입력 검사를 막지 않게

        var expensive = deck.CopyStartingDeck().OrderByDescending(c => c.Cost).First();
        UseFirst(expensive, null); Set(cost, "<Current>k__BackingField", 0f);
        f = Time.frameCount; Check(!deck.TryUseSlot(0), "cost-short rejected"); Check(!deck.TryUseSlot(0), "cost-short rejected again"); yield return Steps(2);
        Check(CountSince(lib.reject, f) == 1, "reject once for rapid repeats");
        yield return RealWait(.35f); f = Time.frameCount; Check(!deck.TryUseSlot(0), "cost-short later"); yield return Steps(1);
        Check(CountSince(lib.reject, f) == 1, "reject again after interval");
        var cheap = deck.CopyStartingDeck().First(c => c.Category == SkillCategory.Deal && !c.IsAreaOfEffect && !c.IsChanneling && c.CastTime > 0f);
        UseFirst(cheap, manager.GetAliveEnemies()[0]); Check(deck.TryUseSlot(0), "cast accepted"); yield return Steps(12);
        f = Time.frameCount; deck.TryUseSlot(1); yield return Steps(1);
        Check(CountSince(lib.reject, f) == 0, "action-locked input: no reject sound");

        // 차단 성공(궁병 주황)·실패(우두머리 빨강)
        var archer = EnemyOf("Enemy_B_Orange"); var silence = deck.CopyStartingDeck().First(c => c.Category == SkillCategory.Interrupt && c.Damage == 0);
        yield return Steps(30); ForceCast(archer, 1); UseFirst(silence, archer);
        f = Time.frameCount; Check(deck.TryUseSlot(0), "silence accepted"); yield return Until(() => !deck.IsCasting, 40, "silence cast");
        int layer = lib.interruptSuccessLayer.IsEmpty ? 0 : 1;
        Check(CountSince(lib.interruptSuccess, f) == 1 && CountSince(lib.interruptSuccessLayer, f) == layer && CountSince(lib.interruptFail, f) == 0, "interrupt success sound only on success");
        Check(log.Last(x => x.cue == lib.interruptSuccess).group == skillGroup, "interrupt success on Skill group");
        yield return Fresh(1); var leader = EnemyOf("Enemy_C_Red"); ForceCast(leader, 1); UseFirst(silence, leader); yield return Steps(25);
        f = Time.frameCount; Check(deck.TryUseSlot(0), "silence on red accepted"); yield return Until(() => !deck.IsCasting, 40, "silence red");
        Check(CountSince(lib.interruptFail, f) == 1 && CountSince(lib.interruptSuccess, f) == 0, "red interrupt: fail sound, no success");
        yield return Fresh(0); archer = EnemyOf("Enemy_B_Orange"); ForceCast(archer, 1);
        var suppression = lib.skills.First(x => x.skill != null && x.skill.name == "Skill_SK12_Suppression").skill;
        UseFirst(suppression, archer); f = Time.frameCount;
        Check(deck.TryUseSlot(0), "suppression accepted"); yield return Until(() => !deck.IsCasting, 60, "suppression cast");
        Check(CountSince(lib.interruptSuccess, f) == 1 && CountSince(lib.interruptSuccessLayer, f) == layer, "suppression uses the same new interrupt sound");
        var suppressionSound = lib.skills.First(x => x.skill == suppression);
        Check(CountSince(suppressionSound.major, f) == 1 && !suppressionSound.major.clips.Any(c => c.name.Contains("metal")), "suppression damage sound once, no metal clang");
        var interruptClips = lib.interruptSuccess.clips.Concat(lib.interruptSuccessLayer.clips).ToArray();
        Check(!interruptClips.Any(c => c.name.Contains("glass") || c.name.Contains("dagger") || c.name.Contains("knife")), "interrupt sound has no glass/dagger layer");
        Check(MixPeak(new[] { lib.interruptSuccess, suppressionSound.major }) <= 1f, "seal + suppression hit summed peak within 0dBFS (" + MixPeak(new[] { lib.interruptSuccess, suppressionSound.major }).ToString("0.00") + ")");
        notes += "common: absorb/break/hit/potion/reject/silence/suppression PASS\n";
    }

    // ② 플레이어 스킬 14종: 준비·반복·첫 효과·이후 타격, 광역 3체 1회, 취소 시 정리
    static IEnumerator Skills(AudioMixerGroup skillGroup)
    {
        int tested = 0;
        foreach (var s in lib.skills)
        {
            if (s.skill == null) continue;
            if (s.skill.Category == SkillCategory.Interrupt) continue;   // ①에서 차단 대상과 함께 확인
            yield return Fresh(2); yield return Steps(5);
            var target = manager.GetAliveEnemies()[0];
            UseFirst(s.skill, target);
            int f = Time.frameCount;
            Check(deck.TryUseSlot(0), s.skill.name + " accepted");
            int accept = Time.frameCount;
            if (s.skill.name == "Skill_SK03_MagicCutter" || s.skill.name == "Skill_SK07_MagicImpact")
            {
                Check(!deck.IsCasting && CountSince(s.major, f) == 1 && s.major.clips.Any(audio.IsPlaying), s.skill.name + " instant: composed sound plays once");
                yield return Steps(2);
                Check(s.major.clips.Any(audio.IsPlaying), s.skill.name + " sound continues after instant cast cleared");
            }
            if (s.skill.name == "Skill_SK14_Sanctuary")
            {
                yield return Steps(20);
                Check(s.castLoop.clips.Any(audio.IsPlaying), "sanctuary sustained loop during channel");
                info.TogglePause(); yield return Steps(5);
                Check(audio.PausedVoices > 0, "sanctuary loop pauses with game");
                info.Resume(); info.ToggleSpeed(); yield return Steps(60);
                Check(deck.IsCasting && s.castLoop.clips.Any(audio.IsPlaying), "sanctuary sustained at 0.5x");
                info.ToggleSpeed();
            }
            yield return Until(() => !deck.IsCasting, 400, s.skill.name + " cast");
            yield return Steps(12);
            if (!s.cast.IsEmpty) Check(log.Count(x => x.cue == s.cast && x.frame == accept) == 1, s.skill.name + " cast sound at accept");
            if (!s.castLoop.IsEmpty) Check(CountSince(s.castLoop, f) == 1 && !s.castLoop.clips.Any(audio.IsPlaying), s.skill.name + " loop once and stopped");
            if (!s.major.IsEmpty) Check(CountSince(s.major, f) == 1, s.skill.name + " major once (AoE 3 targets: " + s.skill.IsAreaOfEffect + ")");
            if (!s.minor.IsEmpty) Check(CountSince(s.minor, f) <= Mathf.Max(0, s.skill.HitCount - 1) * (s.skill.IsChanneling ? 1 : 3), s.skill.name + " minor bounded");
            if (s.skill.Category == SkillCategory.Shield && s.major.IsEmpty && s.castLoop.IsEmpty) Check(CountSince(lib.shieldGain, f) == 1, s.skill.name + " shield gain once");
            Check(log.Where(x => x.frame >= f && (x.cue == s.cast || x.cue == s.castLoop || x.cue == s.major || x.cue == s.minor)).All(x => x.group == skillGroup), s.skill.name + " on Skill group");
            tested++;
        }
        Check(tested == 12, "12 non-interrupt skills tested (+ silence/suppression in common)");

        // 10/8 보완: 커터는 약탈자와 다른 원본, 생츄어리 0.6, 다크 계열은 디엔드와 분리·합산 여유
        SkillSound Skill(string n) => lib.skills.First(x => x.skill != null && x.skill.name == n);
        var raider = lib.enemies.First(x => x.enemy.name == "Enemy_A_Green").attacks[0];
        var raiderClips = raider.release.clips.Concat(raider.releaseLayer.clips).ToArray();
        Check(!Skill("Skill_SK03_MagicCutter").major.clips.Intersect(raiderClips).Any(), "magic cutter does not share raider clips");
        Check(Mathf.Approximately(Skill("Skill_SK14_Sanctuary").castLoop.volume, .6f), "sanctuary loop volume 0.6");
        var bossSounds = lib.enemies.First(x => x.enemy.name == "Enemy_Boss").attacks;
        var eruption = Skill("Skill_SK10_DarkEruption").major;
        Check(!eruption.clips.Intersect(bossSounds[4].release.clips).Any() && bossSounds[4].release.clips.Any(c => c.name == "sh_dark_blast") && Mathf.Approximately(bossSounds[4].release.volume, 1f),
            "dark eruption separated from The End (The End keeps sh_dark_blast at 1.0)");
        Check(bossSounds[2].release.clips.All(c => c.name != "sh_darkhole_blast"), "dark hole uses the new clean blast");
        float eruptionMix = MixPeak(new[] { eruption, lib.playerHit }), holeMix = MixPeak(new[] { bossSounds[2].release, lib.playerHit });
        Check(eruptionMix <= 1f && holeMix <= 1f, "dark blast + player hit summed peak within 0dBFS (eruption " + eruptionMix.ToString("0.00") + ", dark hole " + holeMix.ToString("0.00") + ")");
        notes += "tuning: cutter≠raider, sanctuary 0.6, dark split, mix peaks eruption " + eruptionMix.ToString("0.00") + " / dark hole " + holeMix.ToString("0.00") + "\n";

        // 취소: 파이어 필라 시전 중 기절 → 준비음 정지, 발동음 없음
        var pillar = lib.skills.First(x => x.skill != null && x.skill.name == "Skill_SK04_FirePillar");
        yield return Fresh(2); UseFirst(pillar.skill, null); int g = Time.frameCount;
        Check(deck.TryUseSlot(0), "pillar accepted"); yield return Steps(10);
        Check(pillar.cast.clips.Any(audio.IsPlaying), "pillar buildup playing");
        player.SetSanctuary(false); player.ApplyStun(2f); yield return Steps(10);
        Check(deck.LastCastCancelled && !pillar.cast.clips.Any(audio.IsPlaying) && CountSince(pillar.major, g) == 0, "stun cancel: buildup stopped, no impact sound");
        // 채널링 취소: 아이스애로우 반복음 정리
        var ice = lib.skills.First(x => x.skill != null && x.skill.name == "Skill_SK02_IceArrow");
        yield return Fresh(2); yield return Steps(110); UseFirst(ice.skill, manager.GetAliveEnemies()[0]); Check(deck.TryUseSlot(0), "ice arrow accepted"); yield return Steps(30);
        Check(ice.castLoop.clips.Any(audio.IsPlaying), "ice loop playing");
        player.SetSanctuary(false); player.ApplyStun(2f); yield return Steps(10);
        Check(!ice.castLoop.clips.Any(audio.IsPlaying), "channel cancel: loop stopped");
        notes += "skills: " + tested + " + silence/suppression, cancel cleanup PASS\n";
    }

    // ③ 적 5종: 시전 시작 준비음·경고, 실제 발동 프레임 발동음, 지연 착탄, 차단·사망 시 정리
    static IEnumerator Enemies(AudioMixerGroup enemyGroup)
    {
        foreach (var es in lib.enemies)
        {
            var e = EnemyOf(es.enemy.name);
            if (es.enemy.name == "Enemy_Boss") yield return Fresh(3);
            else { yield return Fresh(0); manager.BeginBattle(new List<Enemy> { e }); yield return Steps(2); Set(e, "<CurrentHp>k__BackingField", 99999); }
            for (int i = 0; i < es.attacks.Length && i < e.Data.Attacks.Count; i++)
            {
                var a = es.attacks[i];
                yield return Steps(20); yield return RealWait(.35f);
                int fired = -1; Action<CastColor> onFire = _ => fired = Time.frameCount;
                e.AttackFired += onFire;
                ((Dictionary<SoundCue, float>)Get(audio, "lastPlayed")).Clear();   // 직전 자연 시전의 경고 간격 기록을 비워 이 공격만 본다
                int f = Time.frameCount;
                ForceCast(e, i, es.enemy.name == "Enemy_Boss" ? 0.85f : 0f);
                var color = e.CastColor;
                if (color == CastColor.Orange) Check(CountSince(lib.warnRed, f) == 0 && log.Where(x => x.frame >= f).All(x => x.cue == a.prepare || x.cue == a.prepareLoop), es.enemy.name + " " + i + " orange: only attack preparation, no common warning");
                else if (color == CastColor.Red) Check(CountSince(lib.warnRed, f) == 1, es.enemy.name + " " + i + " red warning once");
                else Check(CountSince(lib.warnRed, f) == 0, es.enemy.name + " " + i + " green: no common warning");
                if (!a.prepare.IsEmpty) Check(CountSince(a.prepare, f) == 1, es.enemy.name + " " + i + " prepare at cast start");
                if (!a.prepareLoop.IsEmpty) Check(a.prepareLoop.clips.Any(audio.IsPlaying), es.enemy.name + " " + i + " prepare loop");
                yield return Until(() => fired >= 0, 2000, es.enemy.name + " " + i + " fire");
                e.AttackFired -= onFire;
                if (!a.release.IsEmpty) Check(log.Count(x => x.cue == a.release && x.frame == fired) == 1, es.enemy.name + " " + i + " release on fire frame");
                if (!a.releaseLayer.IsEmpty) Check(log.Count(x => x.cue == a.releaseLayer && x.frame == fired) == 1, es.enemy.name + " " + i + " release layer on fire frame");
                yield return Steps(3);
                if (!a.prepareLoop.IsEmpty || !a.prepare.IsEmpty) Check(!a.prepareLoop.clips.Any(audio.IsPlaying), es.enemy.name + " " + i + " loop stopped at fire");
                if (!a.delayed.IsEmpty)
                {
                    yield return Until(() => CountSince(a.delayed, fired) > 0, 60, es.enemy.name + " delayed");
                    int lag = log.First(x => x.cue == a.delayed && x.frame >= fired).frame - fired;
                    Check(Mathf.Abs(lag * .02f - a.delaySeconds) <= .03f, es.enemy.name + " " + i + " delayed impact at " + a.delaySeconds + "s (" + lag + "f)");
                }
                Check(log.Where(x => x.frame >= f && (x.cue == a.release || x.cue == a.prepare || x.cue == a.prepareLoop)).All(x => x.group == enemyGroup), es.enemy.name + " on Enemy group");
            }
        }
        // 차단: 나이트메어(주황) 준비 반복음 → 차단 시 정지, 발동음 없음
        yield return Fresh(3); var boss = EnemyOf("Enemy_Boss"); var night = lib.enemies.First(x => x.enemy.name == "Enemy_Boss").attacks[3];
        yield return Steps(20); ForceCast(boss, 3, .3f); yield return Steps(5);
        Check(night.prepareLoop.clips.Any(audio.IsPlaying), "nightmare loop playing");
        int g = Time.frameCount; Check(boss.TryInterrupt(2.5f) == InterruptResult.Success, "nightmare interrupted"); yield return Steps(10);
        Check(!night.prepareLoop.clips.Any(audio.IsPlaying) && CountSince(night.release, g) == 0, "interrupt: loop stopped, no release");
        // 사망: 다크홀 축적음 → 처치 시 정지
        var dark = lib.enemies.First(x => x.enemy.name == "Enemy_Boss").attacks[2];
        yield return Steps(130); ForceCast(boss, 2, .2f); yield return Steps(5); Check(dark.prepareLoop.clips.Any(audio.IsPlaying), "dark hole loop playing");
        boss.TakeDamage(boss.CurrentHp + 999999); yield return Steps(6);
        Check(!dark.prepareLoop.clips.Any(audio.IsPlaying), "death: loop stopped");
        // 주황 연속 시전에도 경고음 없음. 빨강은 중복 제한 유지.
        yield return Fresh(0); var archer = EnemyOf("Enemy_B_Orange"); yield return Steps(20); g = Time.frameCount;
        ForceCast(archer, 1); yield return Steps(2); ForceCast(archer, 1); yield return Steps(2);
        Check(!log.Any(x => x.clip.name == "warn_orange"), "orange warning never played throughout all enemy attacks");
        yield return Fresh(1); var leader = EnemyOf("Enemy_C_Red"); yield return RealWait(.35f); g = Time.frameCount;
        ForceCast(leader, 1); ForceCast(leader, 1);
        Check(CountSince(lib.warnRed, g) == 1, "repeated red warning within interval plays once");
        notes += "enemies: 5 types / prepare-release-delayed-warning, interrupt/death cleanup PASS\n";
    }

    // ④ 보스 페이즈·오브 소환/파괴·정리 제거
    static IEnumerator Boss()
    {
        yield return Fresh(3); var boss = EnemyOf("Enemy_Boss"); Set(boss, "<CurrentHp>k__BackingField", boss.MaxHp); int f = Time.frameCount;   // 페이즈 기준을 넘도록 원래 HP로
        boss.TakeDamage(1600); boss.TryInterrupt(2.5f); yield return Steps(2);
        Check(CountSince(lib.phaseTransition, f) == 1 && CountSince(lib.orbSummon, f) == 1, "phase transition once, two orbs one summon sound");
        yield return Until(() => !boss.IsPhaseTransitioning, 200, "transition");
        var orbs = boss.GetComponent<BossOrbs>(); f = Time.frameCount;
        orbs.Ruin.TakeDamage(400); yield return Steps(2);
        Check(CountSince(lib.orbBreak, f) == 1, "real orb destruction: break sound");
        f = Time.frameCount; boss.TakeDamage(boss.CurrentHp + 999999); yield return Steps(3);
        Check(CountSince(lib.orbBreak, f) == 0, "boss death cleanup: no orb break sound");
        Check(flow.State == BattleFlowState.Victory, "boss victory");
        yield return Until(() => !flow.ResultPanelHeld, 4000, "victory hold"); yield return Steps(2);
        Check(CountSince(lib.victoryFinal, f) == 1 && CountSince(lib.victoryBattle, f) == 0, "final victory sound once when panel opens");
        notes += "boss: phase/orb summon/break/cleanup/victory PASS\n";
    }

    // ⑤ 수동 정지·0.5배속·재시작·중복 구독
    static IEnumerator PauseSpeedRestart()
    {
        yield return Fresh(3); var boss = EnemyOf("Enemy_Boss"); yield return Steps(20);
        var dark = lib.enemies.First(x => x.enemy.name == "Enemy_Boss").attacks[2];
        ForceCast(boss, 2, .2f); yield return Steps(5);
        Check(audio.ActiveLoopVoices >= 1 && audio.MusicPlaying, "loop and music playing");
        info.TogglePause(); yield return Steps(5);
        Check(audio.PausedVoices >= 1 && audio.MusicPlaying && audio.MusicVolume > 0f, "manual pause: combat paused, music continues");
        int f = Time.frameCount; var click = UnityEngine.Object.FindObjectsByType<UnityEngine.UI.Button>(FindObjectsInactive.Include, FindObjectsSortMode.None).First(b => b.name == "StatsToggle");
        click.onClick.Invoke(); yield return Steps(1); click.onClick.Invoke(); yield return Steps(1);
        Check(CountSince(lib.click, f) >= 1, "UI click plays during pause");
        info.Resume(); yield return Steps(3);
        Check(audio.PausedVoices == 0 && dark.prepareLoop.clips.Any(audio.IsPlaying), "resume: loop continues");
        info.ToggleSpeed(); yield return Steps(5);
        Check(Mathf.Approximately(Time.timeScale, .5f) && Mathf.Approximately(audio.MusicPitch, 1f), "0.5x: music pitch unchanged");
        info.ToggleSpeed();
        // 재시작: 반복음·지연 예약 정리
        yield return Fresh(0); var archer = EnemyOf("Enemy_B_Orange"); yield return Steps(20); ForceCast(archer, 0, .99f); yield return Until(() => audio.PendingDelayed > 0, 20, "arrow pending");
        var arrowHit = lib.enemies.First(x => x.enemy.name == "Enemy_B_Orange").attacks[0].delayed;
        int voicesBefore = audio.ActiveCombatVoices;
        flow.RestartRun(); int restart = Time.frameCount;
        // 재시작 직후(같은 호출) 이전 전투 소리·착탄 예약이 비어야 한다. 새 전투 적의 시전음은 그 뒤 정상적으로 시작될 수 있다.
        Check(voicesBefore > 0 && audio.ActiveLoopVoices == 0 && audio.PendingDelayed == 0 && audio.ActiveCombatVoices < voicesBefore,
            "restart clears previous combat voices (" + voicesBefore + "->" + audio.ActiveCombatVoices + ") and pending");
        yield return Steps(15);
        Check(log.Count(x => x.cue == arrowHit && x.frame >= restart) == 0, "restart: earlier arrow impact never plays");
        // 재시작 뒤 중복 구독 없음: 같은 사건 1회
        yield return Fresh(1); var leader = EnemyOf("Enemy_C_Red"); var silence = deck.CopyStartingDeck().First(c => c.Category == SkillCategory.Interrupt && c.Damage == 0);
        ForceCast(leader, 1); UseFirst(silence, leader); yield return Steps(25); f = Time.frameCount;
        Check(deck.TryUseSlot(0), "silence after restart"); yield return Until(() => !deck.IsCasting, 40, "silence");
        Check(CountSince(lib.interruptFail, f) == 1, "after restart: one sound per event");
        notes += "pause/0.5x/restart/no duplicate PASS\n";
    }

    // ⑥ 일반 승리·보상 선택/확정·패배(디엔드 발동음 유지)
    static IEnumerator Results()
    {
        yield return Fresh(0); int f = Time.frameCount;
        foreach (var e in manager.GetAliveEnemies().ToList()) e.TakeDamage(9999999);
        yield return Until(() => flow.State == BattleFlowState.BetweenBattles, 30, "battle win");
        yield return Until(() => !flow.ResultPanelHeld, 4000, "win hold"); yield return Steps(2);
        Check(CountSince(lib.victoryBattle, f) == 1, "battle victory sound once");
        var buttons = UnityEngine.Object.FindObjectsByType<UnityEngine.UI.Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        int skill = -1; for (int i = 0; i < flow.RewardChoiceCount; i++) if (flow.GetRewardChoice(i)?.Card != null && !deck.CopyStartingDeck().Contains(flow.GetRewardChoice(i).Card)) { skill = i; break; }
        if (skill >= 0)
        {
            f = Time.frameCount; buttons.First(b => b.name == "Reward" + skill).onClick.Invoke(); yield return Steps(1);
            Check(CountSince(lib.select, f) == 1 && CountSince(lib.rewardConfirm, f) == 0, "skill reward choice: select, not confirm");
            f = Time.frameCount; buttons.First(b => b.name == "Deck2").onClick.Invoke(); yield return Steps(1);
            Check(CountSince(lib.select, f) == 1 && CountSince(lib.rewardConfirm, f) == 0 && !flow.RewardResolved, "slot choice: select, not confirm");
            f = Time.frameCount; buttons.First(b => b.name == "ConfirmReplacement").onClick.Invoke(); yield return Steps(1);
            Check(flow.RewardResolved && CountSince(lib.rewardConfirm, f) == 1, "confirm replacement: reward confirm once");
        }
        else notes += "reward: no skill choice this roll, replacement path not exercised\n";
        // 패배: 디엔드 발동음이 결과 정지(timeScale 0) 때문에 잘리지 않음
        yield return Fresh(3); var boss = EnemyOf("Enemy_Boss"); var end = lib.enemies.First(x => x.enemy.name == "Enemy_Boss").attacks[4];
        player.SetSanctuary(false); yield return Steps(20);
        int fired = -1; Action<CastColor> onFire = _ => fired = Time.frameCount; boss.AttackFired += onFire;
        ForceCast(boss, 4, .995f); Set(player, "currentHp", 1f);
        yield return Until(() => fired >= 0, 200, "the end fire"); boss.AttackFired -= onFire; yield return Steps(3);
        Check(flow.State == BattleFlowState.Defeat, "the end defeat");
        Check(end.release.clips.Any(audio.IsPlaying) && Time.timeScale == 0f, "the end explosion keeps playing after defeat stop");
        f = Time.frameCount; yield return Until(() => !flow.ResultPanelHeld, 4000, "defeat hold"); yield return Steps(2);
        Check(CountSince(lib.defeat, fired) == 1, "defeat sound once when panel opens");
        notes += "results: battle victory, reward select/confirm, defeat with The End PASS\n";
    }

    // ⑦ 음량 3종: 0%는 -80dB, 그 외는 dB 변환. 저장값은 바꾸지 않는다.
    static void Volumes()
    {
        var menu = Find<MainMenuUI>(); var mixer = (AudioMixer)Get(menu, "mixer"); var sliders = (UnityEngine.UI.Slider[])Get(menu, "volumeSliders");
        var names = (string[])Get(menu, "mixerParameters");
        float[] keep = sliders.Select(s => s.value).ToArray();
        try
        {
            for (int k = 0; k < sliders.Length; k++)
            {
                for (int i = 0; i < sliders.Length; i++) sliders[i].SetValueWithoutNotify(i == k ? 0f : .5f);
                Call(menu, "ApplyAudio");
                for (int i = 0; i < sliders.Length; i++) { mixer.GetFloat(names[i], out float db); Check(i == k ? db <= -79.9f : Mathf.Abs(db + 6.02f) < .05f, names[i] + " independent (" + db.ToString("0.0") + "dB)"); }
            }
        }
        finally { for (int i = 0; i < sliders.Length; i++) sliders[i].SetValueWithoutNotify(keep[i]); Call(menu, "ReadSavedOptions"); Call(menu, "ApplyAudio"); }
        notes += "volumes: Master/Music/Skill/Enemy independent, 0% mute PASS (PlayerPrefs untouched)\n";
    }
}
