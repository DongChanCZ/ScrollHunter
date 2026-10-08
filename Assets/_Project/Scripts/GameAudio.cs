using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

/// <summary>
/// 게임 소리 재생(10/8, 10 A48). 기존 표시용 알림만 구독하며 판정·수치·시간·연출에 관여하지 않는다.
/// - 출력: 배경음 Music, 플레이어 스킬·포션·플레이어 상태·공통 UI Skill, 적 공격·경고·보스·오브 Enemy (시작 화면 음량 3종).
/// - 수동 정지 중 전투 소리만 일시정지·재개. 배경음·UI음은 계속. timeScale 0(시작·보상·결과 화면)만으로는 멈추지 않는다.
/// - 배속은 소리 음높이·배경음 속도에 영향 없음. 반복음·준비음은 게임 진행(시전·발동·취소)에 맞춰 시작·종료.
/// - 전투 종료(승리·패배)에는 반복·준비음만 정리하고 이미 난 발동음은 끝까지 둔다. 전투 시작·재시작에는 이전 전투 소리를 모두 지운다.
/// </summary>
public sealed class GameAudio : MonoBehaviour
{
    [SerializeField] private SoundLibrary library;
    [SerializeField] private AudioMixerGroup musicGroup, skillGroup, enemyGroup;
    [SerializeField, Min(4)] private int combatVoices = 24;
    [SerializeField, Min(1)] private int uiVoices = 6;
    [Tooltip("배경음 전환 페이드(실제 초)")]
    [SerializeField, Min(0f)] private float musicFade = 0.8f;
    [Tooltip("승리·패배 짧은 UI음이 나는 동안 배경음 배율")]
    [SerializeField, Range(0f, 1f)] private float jingleDuck = 0.3f;
    [Tooltip("반복·준비음을 멈출 때 줄이는 시간(실제 초)")]
    [SerializeField, Min(0f)] private float stopFade = 0.1f;
    [Tooltip("거절음 최소 간격(실제 초). 키를 연타해도 반복이 쌓이지 않게")]
    [SerializeField, Min(0f)] private float rejectInterval = 0.25f;
    [Tooltip("빨강 경고음 최소 간격(실제 초). 여러 적이 동시에 시전해도 겹치지 않게")]
    [SerializeField, Min(0f)] private float warningInterval = 0.3f;
    [Tooltip("축적 반복음이 시전 시작에 내는 음량 비율")]
    [SerializeField, Range(0f, 1f)] private float risingLoopStart = 0.3f;

    private sealed class Voice
    {
        public AudioSource source;
        public bool combat, paused, cancelOnEnd;
        public object owner;
        public float baseVolume, fade = -1f, started;
        public System.Func<float> level;
        public bool Busy => source.isPlaying || paused;
    }

    private readonly List<Voice> combat = new List<Voice>();
    private readonly List<Voice> ui = new List<Voice>();
    private AudioSource musicA, musicB;
    private MusicTrack currentTrack;
    private float musicFadeIn = 1f;
    private readonly Dictionary<SoundCue, float> lastPlayed = new Dictionary<SoundCue, float>();
    // 소리 변형 선택은 전투 난수(크리티컬 등)와 분리한다(SkillVfx 변형과 같은 원칙).
    private readonly System.Random variation = new System.Random();

    private BattleFlow flow;
    private DeckSystem deck;
    private Player player;
    private CombatInfoUI info;
    private readonly List<Enemy> enemies = new List<Enemy>();
    private readonly Dictionary<Enemy, int> enemyAttack = new Dictionary<Enemy, int>();
    private readonly Dictionary<Enemy, bool> wasTransitioning = new Dictionary<Enemy, bool>();
    private readonly Dictionary<Enemy, System.Action> castHandlers = new Dictionary<Enemy, System.Action>();
    private readonly Dictionary<Enemy, System.Action<CastColor>> fireHandlers = new Dictionary<Enemy, System.Action<CastColor>>();
    private readonly Dictionary<Enemy, System.Action> cancelHandlers = new Dictionary<Enemy, System.Action>();
    private readonly Dictionary<Enemy, System.Action<bool>> encounterHandlers = new Dictionary<Enemy, System.Action<bool>>();

    private struct Delayed { public SoundCue cue; public float remaining; public AudioMixerGroup group; }
    private readonly List<Delayed> delayed = new List<Delayed>();

    private readonly object castOwner = new object();
    private SkillData castCard;
    private int useHits, hitFrame = -1;
    private SkillData hitCard;
    private bool pausedState, fightingLast;
    private BattleFlowState lastState;
    private bool resultPending;

    private enum UiKind { Click, Confirm, Cancel, Select, Reward, Replace }
    private UiKind? pendingReward;
    private bool prevResolved;
    private BattleFlowState prevStateForUi;

    // 검사용 기록
    public int PlayCount { get; private set; }
    public string LastCueName { get; private set; } = string.Empty;
    public System.Action<SoundCue, AudioClip, AudioMixerGroup> Played;
    public bool IsPlaying(AudioClip clip) { foreach (var v in combat) if (v.Busy && v.source.clip == clip && v.fade < 0f) return true; foreach (var v in ui) if (v.Busy && v.source.clip == clip) return true; return false; }
    public bool MusicPlaying => (musicA != null && musicA.isPlaying) || (musicB != null && musicB.isPlaying);
    public float MusicPitch => musicA != null ? musicA.pitch : 1f;
    public AudioClip CurrentMusic => currentTrack != null ? currentTrack.clip : null;
    public int ActiveCombatVoices { get { int n = 0; foreach (var v in combat) if (v.Busy) n++; return n; } }
    public int ActiveLoopVoices { get { int n = 0; foreach (var v in combat) if (v.Busy && v.source.loop) n++; return n; } }
    public int PausedVoices { get { int n = 0; foreach (var v in combat) if (v.paused) n++; return n; } }
    public int PendingDelayed => delayed.Count;
    public float MusicVolume => musicA != null ? Mathf.Max(musicA.isPlaying ? musicA.volume : 0f, musicB.isPlaying ? musicB.volume : 0f) : 0f;

    private void Awake()
    {
        for (int i = 0; i < combatVoices; i++) combat.Add(MakeVoice("CombatVoice" + i, true));
        for (int i = 0; i < uiVoices; i++) ui.Add(MakeVoice("UiVoice" + i, false));
        musicA = MakeSource("MusicA", musicGroup); musicB = MakeSource("MusicB", musicGroup);
        musicA.loop = musicB.loop = true;
    }

    private AudioSource MakeSource(string name, AudioMixerGroup group)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var s = go.AddComponent<AudioSource>();
        s.playOnAwake = false; s.spatialBlend = 0f; s.outputAudioMixerGroup = group;
        return s;
    }

    private Voice MakeVoice(string name, bool isCombat) => new Voice { source = MakeSource(name, null), combat = isCombat };

    private void Start()
    {
        flow = FindFirstObjectByType<BattleFlow>(FindObjectsInactive.Include);
        deck = FindFirstObjectByType<DeckSystem>(FindObjectsInactive.Include);
        player = FindFirstObjectByType<Player>(FindObjectsInactive.Include);
        info = FindFirstObjectByType<CombatInfoUI>(FindObjectsInactive.Include);
        if (deck != null)
        {
            deck.CastStarted += OnCastStarted;
            deck.HitApplied += OnHit;
            deck.ShieldApplied += OnShield;
            deck.InterruptResolved += OnInterrupt;
            deck.SlotRejected += OnSlotRejected;
        }
        if (player != null)
        {
            player.Damaged += OnPlayerDamaged;
            player.PotionUsed += OnPotion;
            player.PotionRejected += OnReject;
            player.BattleReset += StopCombat;
        }
        foreach (var e in FindObjectsByType<Enemy>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (!e.IsBossOrb) Watch(e);
        BossOrbs.OrbSummoning += OnOrbSummoning;
        BossOrbs.OrbBroken += OnOrbBroken;
        foreach (var b in FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None)) WatchButton(b);
        if (flow != null) { lastState = prevStateForUi = flow.State; prevResolved = flow.RewardResolved; }
    }

    private void OnDestroy()
    {
        if (deck != null)
        {
            deck.CastStarted -= OnCastStarted; deck.HitApplied -= OnHit; deck.ShieldApplied -= OnShield;
            deck.InterruptResolved -= OnInterrupt; deck.SlotRejected -= OnSlotRejected;
        }
        if (player != null)
        {
            player.Damaged -= OnPlayerDamaged; player.PotionUsed -= OnPotion; player.PotionRejected -= OnReject; player.BattleReset -= StopCombat;
        }
        foreach (var e in enemies)
        {
            if (e == null) continue;
            e.CastStarted -= castHandlers[e]; e.AttackFired -= fireHandlers[e];
            e.Interrupted -= cancelHandlers[e]; e.Died -= cancelHandlers[e]; e.EncounterActiveChanging -= encounterHandlers[e];
        }
        BossOrbs.OrbSummoning -= OnOrbSummoning;
        BossOrbs.OrbBroken -= OnOrbBroken;
    }

    // ───────── 재생 ─────────

    private Voice Play(SoundCue cue, AudioMixerGroup group, bool isCombat, object owner = null, bool loop = false,
        float volumeScale = 1f, bool cancelOnEnd = false, System.Func<float> level = null)
    {
        if (library == null || cue == null || cue.IsEmpty) return null;
        if (isCombat && pausedState) return null;
        float now = Time.unscaledTime;
        if (cue.cooldown > 0f && lastPlayed.TryGetValue(cue, out float last) && now - last < cue.cooldown) return null;
        var clip = cue.clips[variation.Next(cue.clips.Length)];
        if (clip == null) return null;
        lastPlayed[cue] = now;
        var pool = isCombat ? combat : ui;
        Voice voice = null;
        foreach (var v in pool) if (!v.Busy) { voice = v; break; }
        if (voice == null)
        {
            // 모두 쓰는 중이면 가장 오래된 단발음을 끊는다(반복음은 유지).
            foreach (var v in pool) if (!v.source.loop && (voice == null || v.started < voice.started)) voice = v;
            if (voice == null) return null;
        }
        var s = voice.source;
        s.Stop();
        s.clip = clip; s.loop = loop; s.outputAudioMixerGroup = group;
        s.pitch = Mathf.Lerp(Mathf.Min(cue.pitch.x, cue.pitch.y), Mathf.Max(cue.pitch.x, cue.pitch.y), (float)variation.NextDouble());
        voice.baseVolume = cue.volume * volumeScale;
        voice.level = level;
        s.volume = voice.baseVolume * (level != null ? level() : 1f);
        voice.owner = owner; voice.cancelOnEnd = cancelOnEnd; voice.fade = -1f; voice.paused = false; voice.started = now;
        s.Play();
        PlayCount++; LastCueName = clip.name;
        Played?.Invoke(cue, clip, group);
        return voice;
    }

    private void StopOwner(object owner, float fade)
    {
        if (owner == null) return;
        foreach (var v in combat) if (v.owner == owner && v.Busy) Fade(v, fade);
    }

    private void Fade(Voice v, float seconds)
    {
        if (seconds <= 0f || v.paused) { v.source.Stop(); v.paused = false; v.owner = null; return; }
        v.fade = seconds;
    }

    /// <summary>전투 시작·재시작·시작 화면 복귀: 이전 전투 소리와 예약을 모두 지운다.</summary>
    public void StopCombat()
    {
        foreach (var v in combat) { v.source.Stop(); v.paused = false; v.owner = null; v.fade = -1f; }
        delayed.Clear();
        enemyAttack.Clear();
        castCard = null; hitCard = null; useHits = 0;
    }

    private void Update()
    {
        bool paused = (info != null && info.IsInfoPaused) || (flow != null && flow.IsMenuPaused);
        if (paused != pausedState)
        {
            pausedState = paused;
            foreach (var v in combat)
            {
                if (paused && v.source.isPlaying) { v.source.Pause(); v.paused = true; }
                else if (!paused && v.paused) { v.source.UnPause(); v.paused = false; }
            }
        }

        foreach (var v in combat) UpdateVoice(v);
        foreach (var v in ui) UpdateVoice(v);

        if (castCard != null && deck != null && deck.CastingCard != castCard)
        {
            castCard = null;
            StopOwner(castOwner, stopFade);
        }

        bool fighting = flow == null || flow.State == BattleFlowState.Fighting;
        if (fightingLast && !fighting)
        {
            // 승패 확정: 반복·준비음만 정리. 이미 발동한 소리(디엔드 폭발 등)는 끝까지 둔다.
            foreach (var v in combat) if (v.Busy && (v.cancelOnEnd || v.source.loop)) Fade(v, stopFade);
            castCard = null;
        }
        fightingLast = fighting;

        AdvanceDelayed();
        WatchTransitions();
        WatchResults();
        UpdateMusic();
    }

    private void UpdateVoice(Voice v)
    {
        if (!v.Busy || v.paused) return;
        float level = v.level != null ? v.level() : 1f;
        if (v.fade > 0f)
        {
            v.fade -= Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(v.fade / Mathf.Max(0.01f, stopFade));
            v.source.volume = v.baseVolume * level * k;
            if (v.fade <= 0f) { v.source.Stop(); v.owner = null; v.fade = -1f; }
            return;
        }
        v.source.volume = v.baseVolume * level;
        if (!v.source.loop && !v.source.isPlaying) v.owner = null;
    }

    private void AdvanceDelayed()
    {
        if (delayed.Count == 0 || pausedState) return;
        // 전투 중은 게임 시간(정지·배속), 전투가 끝나 timeScale 0이면 이미 날아간 화살·투사체를 실제 시간으로 마친다.
        float dt = Time.timeScale > 0f ? Time.deltaTime : Time.unscaledDeltaTime;
        for (int i = delayed.Count - 1; i >= 0; i--)
        {
            var d = delayed[i];
            d.remaining -= dt;
            if (d.remaining > 0f) { delayed[i] = d; continue; }
            delayed.RemoveAt(i);
            Play(d.cue, d.group, true);
        }
    }

    // ───────── 플레이어 스킬 ─────────

    private SkillSound FindSkill(SkillData card)
    {
        if (library == null || card == null) return null;
        foreach (var s in library.skills) if (s != null && s.skill == card) return s;
        return null;
    }

    private void OnCastStarted(SkillData card)
    {
        if (castCard != null) StopOwner(castOwner, stopFade);
        castCard = card; useHits = 0;
        var s = FindSkill(card);
        if (s == null) return;
        Play(s.cast, skillGroup, true, castOwner, false, 1f, true);
        Play(s.castLoop, skillGroup, true, castOwner, true, 1f, true);
    }

    private void OnHit(SkillData card, Enemy target)
    {
        var s = FindSkill(card);
        if (s == null) return;
        // 같은 프레임 여러 대상·여러 타는 1번만(광역 큰 발동음이 적 수만큼 겹치지 않게).
        if (Time.frameCount == hitFrame && card == hitCard) return;
        hitFrame = Time.frameCount; hitCard = card;
        var cue = useHits == 0 || s.minor.IsEmpty ? s.major : s.minor;
        if (cue.IsEmpty) cue = s.minor;
        useHits++;
        Play(cue, skillGroup, true);
    }

    private void OnShield(SkillData card, int amount)
    {
        if (amount <= 0 || library == null) return;
        var s = FindSkill(card);
        Play(s != null && !s.major.IsEmpty ? s.major : library.shieldGain, skillGroup, true);
    }

    private void OnInterrupt(SkillData card, Enemy target, bool success)
    {
        if (library == null) return;
        if (success)
        {
            Play(library.interruptSuccess, skillGroup, true);
            Play(library.interruptSuccessLayer, skillGroup, true);
        }
        else Play(library.interruptFail, skillGroup, true);
    }

    private void OnSlotRejected(int slot, SlotState state)
    {
        // 행동 잠금 중 입력은 시전 중 무시 규칙(10 A1)이라 소리를 내지 않는다.
        if (state == SlotState.NotEnoughCost || state == SlotState.NoValidTarget || state == SlotState.Stunned) OnReject();
    }

    private void OnReject()
    {
        if (library == null) return;
        if (lastPlayed.TryGetValue(library.reject, out float last) && Time.unscaledTime - last < rejectInterval) return;
        Play(library.reject, skillGroup, true);
    }

    // ───────── 플레이어 상태 ─────────

    private void OnPlayerDamaged(int hpLost, int absorbed)
    {
        if (library == null || (hpLost <= 0 && absorbed <= 0)) return;
        float shared = hpLost > 0 ? library.overlapVolume : 1f;
        if (absorbed > 0) Play(player != null && player.Shield <= 0 ? library.shieldBreak : library.shieldAbsorb, skillGroup, true, null, false, shared);
        if (hpLost > 0) Play(library.playerHit, skillGroup, true);
    }

    private void OnPotion()
    {
        if (library == null) return;
        Play(library.potion, skillGroup, true);
        Play(library.potionLayer, skillGroup, true);
    }

    // ───────── 적·보스·오브 ─────────

    private void Watch(Enemy e)
    {
        enemies.Add(e);
        castHandlers[e] = () => OnEnemyCast(e);
        fireHandlers[e] = color => OnEnemyFire(e);
        cancelHandlers[e] = () => OnEnemyCancel(e);
        encounterHandlers[e] = active => { if (!active) OnEnemyCancel(e); };
        e.CastStarted += castHandlers[e];
        e.AttackFired += fireHandlers[e];
        e.Interrupted += cancelHandlers[e];
        e.Died += cancelHandlers[e];
        e.EncounterActiveChanging += encounterHandlers[e];
    }

    private EnemyAttackSound FindAttack(Enemy e, int index)
    {
        if (library == null || e == null || e.Data == null || index < 0) return null;
        foreach (var s in library.enemies)
            if (s != null && s.enemy == e.Data) return index < s.attacks.Length ? s.attacks[index] : null;
        return null;
    }

    private void OnEnemyCast(Enemy e)
    {
        StopOwner(e, stopFade);
        int index = -1;
        if (e.Data != null && e.CurrentAttack != null)
            for (int i = 0; i < e.Data.Attacks.Count; i++) if (e.Data.Attacks[i] == e.CurrentAttack) { index = i; break; }
        enemyAttack[e] = index;
        if (library != null && e.CurrentAttack != null)
        {
            // 초록·주황은 공격 고유음만. 빨강만 시전 시작에 공통 경고음 1회.
            var warn = e.CastColor == CastColor.Red ? library.warnRed : null;
            if (warn != null && !(lastPlayed.TryGetValue(warn, out float last) && Time.unscaledTime - last < warningInterval))
                Play(warn, enemyGroup, true);
        }
        var a = FindAttack(e, index);
        if (a == null) return;
        Play(a.prepare, enemyGroup, true, e, false, 1f, true);
        System.Func<float> rise = null;
        if (a.loopRises) rise = () => e != null && e.IsCasting ? Mathf.Lerp(risingLoopStart, 1f, e.CastProgress01) : 1f;
        Play(a.prepareLoop, enemyGroup, true, e, true, 1f, true, rise);
    }

    private void OnEnemyFire(Enemy e)
    {
        StopOwner(e, stopFade * 0.5f);
        int index = enemyAttack.TryGetValue(e, out int i) ? i : -1;
        enemyAttack.Remove(e);
        var a = FindAttack(e, index);
        if (a == null) return;
        Play(a.release, enemyGroup, true);
        Play(a.releaseLayer, enemyGroup, true);
        if (!a.delayed.IsEmpty) delayed.Add(new Delayed { cue = a.delayed, remaining = a.delaySeconds, group = enemyGroup });
    }

    private void OnEnemyCancel(Enemy e)
    {
        // 차단·사망·편성 끄기: 준비음·지속음만 멈춘다. 발동음은 발동하지 않았으므로 나지 않는다.
        enemyAttack.Remove(e);
        StopOwner(e, stopFade);
    }

    private void WatchTransitions()
    {
        foreach (var e in enemies)
        {
            if (e == null || !e.HasPhases) continue;
            bool t = e.isActiveAndEnabled && e.IsPhaseTransitioning;
            wasTransitioning.TryGetValue(e, out bool was);
            if (t && !was && library != null) Play(library.phaseTransition, enemyGroup, true);
            wasTransitioning[e] = t;
        }
    }

    private void OnOrbSummoning(Enemy orb, bool respawn) { if (library != null) Play(library.orbSummon, enemyGroup, true); }
    private void OnOrbBroken(Enemy orb) { if (library != null) Play(library.orbBreak, enemyGroup, true); }

    // ───────── 결과·UI ─────────

    private void WatchResults()
    {
        if (flow == null || library == null) return;
        var state = flow.State;
        if (state != lastState)
        {
            bool won = state == BattleFlowState.Victory || state == BattleFlowState.BetweenBattles || state == BattleFlowState.TutorialComplete;
            resultPending = lastState == BattleFlowState.Fighting && (won || state == BattleFlowState.Defeat);
            if (state == BattleFlowState.Title) StopCombat();
            lastState = state;
        }
        // 막타·사망·패배 연출로 화면이 보류되는 동안 기다렸다가 결과 화면이 열릴 때 1회.
        if (resultPending && !flow.ResultPanelHeld)
        {
            resultPending = false;
            var cue = state == BattleFlowState.Defeat ? library.defeat : state == BattleFlowState.Victory ? library.victoryFinal : library.victoryBattle;
            jingleVoice = Play(cue, skillGroup, false);
        }
    }

    private Voice jingleVoice;

    private void WatchButton(Button button)
    {
        string n = button.name;
        UiKind kind = UiKind.Click;
        if (n == "Close" || n == "Cancel" || n == "Back" || n == "CancelOrder" || n == "Skip") kind = UiKind.Cancel;
        else if (n == "StartButton" || n == "NextBattle" || n == "RestartRun" || n == "Apply" || n == "SaveOrder" || n == "Confirm") kind = UiKind.Confirm;
        else if (n.StartsWith("Reward")) kind = UiKind.Reward;
        else if (n == "ConfirmReplacement") kind = UiKind.Replace;
        else if (n.StartsWith("Deck")) kind = UiKind.Select;
        button.onClick.AddListener(() => OnButton(kind));
    }

    private void OnButton(UiKind kind)
    {
        if (library == null) return;
        switch (kind)
        {
            case UiKind.Cancel: Play(library.cancel, skillGroup, false); break;
            case UiKind.Confirm: Play(library.confirm, skillGroup, false); break;
            case UiKind.Select: Play(library.select, skillGroup, false); break;
            case UiKind.Click: Play(library.click, skillGroup, false); break;
            default: pendingReward = kind; break;   // 실제 확정 여부는 프레임 끝에 판단(자리 선택만으로는 확정음 없음)
        }
    }

    private void LateUpdate()
    {
        if (flow == null) return;
        bool resolvedNow = (flow.RewardResolved && !prevResolved)
            || (prevStateForUi == BattleFlowState.Inheriting && flow.State != BattleFlowState.Inheriting);
        if (pendingReward.HasValue && library != null)
        {
            if (resolvedNow) Play(library.rewardConfirm, skillGroup, false);
            else if (pendingReward.Value == UiKind.Reward) Play(library.select, skillGroup, false);
            else Play(library.reject, skillGroup, false);
            pendingReward = null;
        }
        prevResolved = flow.RewardResolved;
        prevStateForUi = flow.State;
    }

    // ───────── 배경음 ─────────

    private MusicTrack DesiredTrack()
    {
        if (library == null) return null;
        if (flow == null) return library.battleMusic;
        var state = flow.State;
        if (state == BattleFlowState.Title || state == BattleFlowState.Inheriting) return library.titleMusic;
        if (flow.BattleNumber <= 0) return library.tutorialMusic;
        if (flow.BattleNumber >= flow.BattleCount && flow.BattleCount > 0) return library.bossMusic;
        // 같은 산적 아지트의 전투 2·3은 같은 곡을 이어서 재생한다.
        return flow.BattleNumber == 1 ? library.battleMusic : library.hideoutMusic;
    }

    private void UpdateMusic()
    {
        var want = DesiredTrack();
        if (want != null && want.clip != null && want != currentTrack)
        {
            // 새 곡은 처음부터, 같은 곡이면 이어서(재시작하지 않음).
            var swap = musicA; musicA = musicB; musicB = swap;
            musicA.clip = want.clip; musicA.volume = 0f; musicA.Play();
            currentTrack = want; musicFadeIn = 0f;
        }
        if (currentTrack == null) return;
        bool jingle = jingleVoice != null && jingleVoice.Busy && jingleVoice.source.clip != null;
        float duck = jingle ? jingleDuck : 1f;
        float step = musicFade > 0f ? Time.unscaledDeltaTime / musicFade : 1f;
        musicFadeIn = Mathf.Min(1f, musicFadeIn + step);
        musicA.volume = Mathf.MoveTowards(musicA.volume, currentTrack.volume * duck * musicFadeIn, step);
        if (musicB.isPlaying)
        {
            musicB.volume = Mathf.MoveTowards(musicB.volume, 0f, step);
            if (musicB.volume <= 0f) musicB.Stop();
        }
    }
}
