using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 스킬 연출 재생기. DeckSystem의 연출 알림만 받아 이펙트를 띄우며 판정·수치에는 관여하지 않는다.
/// 파티클은 기본 배속 시간을 쓰므로 일시정지·배속을 그대로 따른다.
/// </summary>
public class SkillVfx : MonoBehaviour
{
    [System.Serializable]
    public class Entry
    {
        public SkillData skill;
        [Tooltip("시전 중 대상 위에 유지. 시전이 끝나거나 기절로 끊기면 사라진다")]
        public GameObject castAtTarget;
        [Tooltip("시전 중 연출의 대상 기준 높이")]
        public float castHeight = 3.2f;
        [Tooltip("시전·채널링 동안 적 구역 전체(배치된 적 자리들의 중심)에 유지. 끝나거나 기절로 끊기면 사라진다")]
        public GameObject castZone;
        [Tooltip("타격 1회마다 대상 위치에 생성")]
        public GameObject impactAtTarget;
        [Tooltip("타격마다 이 중 하나를 무작위로 사용(직전과 같은 것은 피함). 비워두면 Impact At Target 사용")]
        public GameObject[] impactVariants;
        [Tooltip("타격 연출의 대상 기준 높이")]
        public float impactHeight = 0f;
        [Tooltip("한 번에 여러 타가 동시에 들어가도 대상마다 타격 연출은 1개만 생성(동시 다타 광역용). 막타 판정은 그 연출로 이어진다")]
        public bool impactOncePerTarget;
        [Tooltip("단일 피해 채널링의 타격보다 먼저 비행 연출을 시작하는 시간. 0이면 타격 시 생성")]
        [Min(0f)] public float channelHitLeadTime;
        [Tooltip("방어도 부여 시 켜는 화면 연출(Animator 포함). 켤 때마다 처음부터 재생")]
        public GameObject selfEffect;
        [Tooltip("차단 성공 시 대상 위치에 생성. 실패하면 나오지 않는다")]
        public GameObject interruptSuccessAtTarget;
    }

    [SerializeField] private DeckSystem deck;
    [SerializeField] private EnemyManager enemies;
    [SerializeField] private Entry[] entries = new Entry[0];
    [Tooltip("생성한 타격 연출을 지우기까지의 시간(초, 배속 시간)")]
    [SerializeField] private float impactLifetime = 2f;
    [Tooltip("화면 연출을 끄기까지의 시간(초, 배속 시간)")]
    [SerializeField] private float selfEffectDuration = 1.1f;
    [Tooltip("대상 몸체에 가려지지 않게 연출을 카메라 쪽으로 당기는 거리(월드 단위)")]
    [SerializeField] private float towardCamera = 0.9f;

    private GameObject activeZone;
    private SkillData activeZoneSkill;
    private GameObject activeCast;
    private SkillData activeCastSkill;
    private Enemy castTarget;
    private float castHeight;
    private readonly Dictionary<GameObject, float> selfEffectHideAt = new Dictionary<GameObject, float>();
    private readonly List<GameObject> expired = new List<GameObject>();
    // 생성한 타격 연출. 전투가 끝나 시간이 멈추면 제거 타이머가 흐르지 않으므로 직접 정리한다.
    private readonly List<GameObject> spawned = new List<GameObject>();

    // 연출 전용 난수. 크리티컬 판정(UnityEngine.Random)의 순서를 바꾸지 않도록 분리한다.
    private readonly System.Random variantRandom = new System.Random();
    private readonly Dictionary<Entry, int> lastVariant = new Dictionary<Entry, int>();

    // 같은 프레임에 이미 띄운 대상별 타격 연출. Impact Once Per Target 항목만 쓴다.
    private readonly Dictionary<Enemy, GameObject> frameImpacts = new Dictionary<Enemy, GameObject>();
    private Entry frameImpactsEntry;
    private int frameImpactsFrame = -1;

    // 그 타격으로 대상이 쓰러진 연출(막타). 다음 전투 시작·정리 때 비운다.
    private GameObject finishingImpact;

    // 아직 피해가 없는 비행 연출. 실제 타격 때 spawned로 넘기며, 중단되면 즉시 정리한다.
    private Entry channelEntry;
    private Enemy channelTarget;
    private int channelHits;
    private GameObject pendingImpact;

    /// <summary>막타 연출이 아직 남아 있는지. 승리 시 결과 화면을 잠깐 늦출지 판단한다.</summary>
    public bool HasFinishingEffect => finishingImpact != null;

    /// <summary>
    /// 전투 종료로 게임 시간이 멈춘 동안에도 남은 연출을 실제 시간으로 끝까지 재생한다.
    /// 판정은 이미 끝났으므로 보이기만 한다. Clear에서 원래 시간 기준으로 되돌린다.
    /// </summary>
    public void PlayOutUnscaled()
    {
        spawned.RemoveAll(effect => effect == null);
        foreach (GameObject effect in spawned)
            foreach (ParticleSystem system in effect.GetComponentsInChildren<ParticleSystem>())
            {
                ParticleSystem.MainModule main = system.main;
                main.useUnscaledTime = true;
            }
        foreach (GameObject effect in selfEffectHideAt.Keys)
        {
            if (effect == null) continue;
            Animator animator = effect.GetComponent<Animator>();
            if (animator != null) animator.updateMode = AnimatorUpdateMode.UnscaledTime;
        }
    }

    /// <summary>남은 연출을 모두 즉시 지운다. 다음 전투·재시작·결과 화면 전환 때 잔여 연출이 재생되지 않게 한다.</summary>
    public void Clear()
    {
        foreach (GameObject effect in spawned) if (effect != null) Destroy(effect);
        spawned.Clear();
        frameImpacts.Clear();
        finishingImpact = null;
        StopCast();
        StopZone();
        StopPendingImpact();
        foreach (GameObject effect in selfEffectHideAt.Keys)
        {
            if (effect == null) continue;
            Animator animator = effect.GetComponent<Animator>();
            if (animator != null) animator.updateMode = AnimatorUpdateMode.Normal;
            effect.SetActive(false);
        }
        selfEffectHideAt.Clear();
    }

    private void Awake()
    {
        if (deck == null) deck = FindFirstObjectByType<DeckSystem>();
        if (enemies == null) enemies = FindFirstObjectByType<EnemyManager>();
    }

    private void OnEnable()
    {
        if (deck == null) return;
        deck.CastStarted += OnCastStarted;
        deck.HitApplied += OnHitApplied;
        deck.ShieldApplied += OnShieldApplied;
        deck.InterruptResolved += OnInterruptResolved;
    }

    private void OnDisable()
    {
        if (deck != null)
        {
            deck.CastStarted -= OnCastStarted;
            deck.HitApplied -= OnHitApplied;
            deck.ShieldApplied -= OnShieldApplied;
            deck.InterruptResolved -= OnInterruptResolved;
        }
        Clear();
    }

    private Entry Find(SkillData skill)
    {
        if (skill == null) return null;
        for (int i = 0; i < entries.Length; i++)
            if (entries[i] != null && entries[i].skill == skill) return entries[i];
        return null;
    }

    private void OnCastStarted(SkillData skill)
    {
        StopCast();
        StopZone();
        StopPendingImpact();
        Entry entry = Find(skill);
        if (entry == null || enemies == null) return;
        if (entry.castZone != null && skill.CastTime > 0f)
        {
            activeZoneSkill = skill;
            activeZone = Instantiate(entry.castZone, ZoneCenter(), Quaternion.identity);
        }
        Enemy target = enemies.CurrentTarget;
        if (target == null || !target.IsAlive) return;
        if (entry.channelHitLeadTime > 0f && skill.IsChanneling && !skill.IsAreaOfEffect
            && skill.ChannelEffect is DamageChannelEffect)
        {
            channelEntry = entry;
            channelTarget = target;
        }
        if (entry.castAtTarget == null || skill.CastTime <= 0f) return;
        activeCastSkill = skill;
        castTarget = target;
        castHeight = entry.castHeight;
        activeCast = Instantiate(entry.castAtTarget, Place(target, castHeight), Quaternion.identity);
    }

    /// <summary>적 1~3번 자리의 중심. 몇 명이 나오든 구역 전체를 덮는 연출의 기준.</summary>
    private Vector3 ZoneCenter()
    {
        Vector3 center = enemies != null ? enemies.FormationCenter : transform.position;
        Camera view = Camera.main;
        if (view == null || towardCamera <= 0f) return center;
        Vector3 toCamera = view.transform.position - center;
        toCamera.y = 0f;
        return toCamera.sqrMagnitude > 0.0001f ? center + toCamera.normalized * towardCamera : center;
    }

    /// <summary>대상 기준 위치에서 카메라 쪽으로 조금 당긴 위치.</summary>
    private Vector3 Place(Enemy target, float height)
    {
        Vector3 position = target.transform.position + Vector3.up * height;
        Camera view = Camera.main;
        if (view == null || towardCamera <= 0f) return position;
        Vector3 toCamera = view.transform.position - position;
        toCamera.y = 0f;
        return toCamera.sqrMagnitude > 0.0001f ? position + toCamera.normalized * towardCamera : position;
    }

    private void OnHitApplied(SkillData skill, Enemy target)
    {
        Entry entry = Find(skill);
        if (entry == null || target == null) return;
        if (entry.impactOncePerTarget)
        {
            if (frameImpactsFrame != Time.frameCount || frameImpactsEntry != entry)
            {
                frameImpacts.Clear();
                frameImpactsFrame = Time.frameCount;
                frameImpactsEntry = entry;
            }
            // 같은 사용의 나머지 타격은 새로 띄우지 않고, 쓰러졌으면 이미 띄운 연출을 막타로 둔다.
            if (frameImpacts.TryGetValue(target, out GameObject shown) && shown != null)
            {
                if (!target.IsAlive) finishingImpact = shown;
                return;
            }
        }
        bool preparedChannel = entry == channelEntry;
        GameObject impact = null;
        if (preparedChannel)
        {
            channelHits++;
            impact = pendingImpact;
            pendingImpact = null;
        }
        if (impact == null)
        {
            GameObject prefab = PickImpact(entry);
            if (prefab == null) return;
            impact = Instantiate(prefab, Place(target, entry.impactHeight), Quaternion.identity);
        }
        // 프레임 지연으로 비행을 못 보여준 경우에도 실제 피해와 착탄을 같은 시점에 맞춘다.
        // Simulate는 정확한 버스트 경계의 입자를 아직 만들지 않아 아주 조금 넘겨 표시한다.
        if (preparedChannel) SetParticleAge(impact, entry.channelHitLeadTime + 0.00001f);
        if (skill == activeCastSkill) StopCast();
        Destroy(impact, impactLifetime);
        spawned.RemoveAll(effect => effect == null);
        spawned.Add(impact);
        if (entry.impactOncePerTarget) frameImpacts[target] = impact;
        // 알림은 피해 적용 직후라, 여기서 쓰러져 있으면 이 연출이 막타다.
        if (!target.IsAlive) finishingImpact = impact;
    }

    /// <summary>변형 목록이 있으면 직전과 다른 하나를 무작위로, 없으면 기본 타격 연출.</summary>
    private GameObject PickImpact(Entry entry)
    {
        if (entry == null) return null;
        GameObject[] variants = entry.impactVariants;
        if (variants == null || variants.Length == 0) return entry.impactAtTarget;
        int previous = lastVariant.TryGetValue(entry, out int last) ? last : -1;
        int index = variantRandom.Next(variants.Length);
        if (variants.Length > 1 && index == previous) index = (index + 1 + variantRandom.Next(variants.Length - 1)) % variants.Length;
        lastVariant[entry] = index;
        return variants[index] != null ? variants[index] : entry.impactAtTarget;
    }

    private void OnInterruptResolved(SkillData skill, Enemy target, bool success)
    {
        Entry entry = Find(skill);
        if (!success || entry == null || entry.interruptSuccessAtTarget == null || target == null) return;
        GameObject seal = Instantiate(entry.interruptSuccessAtTarget, Place(target, 0f), Quaternion.identity);
        Destroy(seal, impactLifetime);
        spawned.RemoveAll(effect => effect == null);
        spawned.Add(seal);
    }

    private void OnShieldApplied(SkillData skill, int granted)
    {
        Entry entry = Find(skill);
        if (entry == null || entry.selfEffect == null) return;
        // 비활성→활성으로 Animator 기본 상태를 처음부터 다시 재생한다.
        entry.selfEffect.SetActive(false);
        entry.selfEffect.SetActive(true);
        selfEffectHideAt[entry.selfEffect] = Time.time + selfEffectDuration;
    }

    private void LateUpdate()
    {
        UpdateChannelImpact();
        if (activeZone != null && (deck == null || deck.CastingCard != activeZoneSkill)) StopZone();
        if (activeCast != null)
        {
            Enemy target = enemies != null ? enemies.CurrentTarget : null;
            bool stillCasting = deck != null && deck.CastingCard == activeCastSkill;
            if (!stillCasting || target == null || !target.IsAlive) StopCast();
            // 사망 후 자동 이동 시 이전 위치의 입자까지 정리하고 새 대상에서 시작한다.
            else if (target != castTarget) OnCastStarted(activeCastSkill);
            else activeCast.transform.position = Place(target, castHeight);
        }

        if (selfEffectHideAt.Count == 0) return;
        expired.Clear();
        foreach (var pair in selfEffectHideAt) if (Time.time >= pair.Value) expired.Add(pair.Key);
        foreach (var effect in expired)
        {
            if (effect != null) effect.SetActive(false);
            selfEffectHideAt.Remove(effect);
        }
    }

    private void UpdateChannelImpact()
    {
        if (channelEntry == null) return;
        SkillData skill = channelEntry.skill;
        if (deck == null || deck.CastingCard != skill || channelTarget == null || !channelTarget.IsAlive)
        {
            StopPendingImpact();
            return;
        }
        if (pendingImpact != null || channelHits >= skill.HitCount) return;
        var timing = (DamageChannelEffect)skill.ChannelEffect;
        float untilHit = timing.FirstHitDelay + channelHits * timing.HitInterval
            - (skill.CastTime - deck.CastRemaining);
        if (untilHit > channelEntry.channelHitLeadTime) return;
        GameObject prefab = PickImpact(channelEntry);
        if (prefab == null) return;
        pendingImpact = Instantiate(prefab, Place(channelTarget, channelEntry.impactHeight), Quaternion.identity);
        SetParticleAge(pendingImpact, Mathf.Clamp(channelEntry.channelHitLeadTime - untilHit,
            0f, channelEntry.channelHitLeadTime));
    }

    private static void SetParticleAge(GameObject effect, float age)
    {
        foreach (ParticleSystem system in effect.GetComponentsInChildren<ParticleSystem>())
        {
            system.Simulate(age, false, true, false);
            system.Play(false);
        }
    }

    private void StopPendingImpact()
    {
        if (pendingImpact != null) Destroy(pendingImpact);
        pendingImpact = null;
        channelEntry = null;
        channelTarget = null;
        channelHits = 0;
    }

    private void StopZone()
    {
        if (activeZone != null) Destroy(activeZone);
        activeZone = null;
        activeZoneSkill = null;
    }

    private void StopCast()
    {
        if (activeCast != null) Destroy(activeCast);
        activeCast = null;
        activeCastSkill = null;
        castTarget = null;
    }
}
