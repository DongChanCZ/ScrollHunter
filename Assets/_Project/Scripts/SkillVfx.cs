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
        [Tooltip("타격 1회마다 대상 위치에 생성")]
        public GameObject impactAtTarget;
        [Tooltip("타격 연출의 대상 기준 높이")]
        public float impactHeight = 0f;
        [Tooltip("방어도 부여 시 켜는 화면 연출(Animator 포함). 켤 때마다 처음부터 재생")]
        public GameObject selfEffect;
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

    private GameObject activeCast;
    private SkillData activeCastSkill;
    private Enemy castTarget;
    private float castHeight;
    private readonly Dictionary<GameObject, float> selfEffectHideAt = new Dictionary<GameObject, float>();
    private readonly List<GameObject> expired = new List<GameObject>();

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
    }

    private void OnDisable()
    {
        if (deck != null)
        {
            deck.CastStarted -= OnCastStarted;
            deck.HitApplied -= OnHitApplied;
            deck.ShieldApplied -= OnShieldApplied;
        }
        StopCast();
        foreach (var effect in selfEffectHideAt.Keys) if (effect != null) effect.SetActive(false);
        selfEffectHideAt.Clear();
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
        Entry entry = Find(skill);
        if (entry == null || entry.castAtTarget == null || skill.CastTime <= 0f || enemies == null) return;
        Enemy target = enemies.CurrentTarget;
        if (target == null || !target.IsAlive) return;
        activeCastSkill = skill;
        castTarget = target;
        castHeight = entry.castHeight;
        activeCast = Instantiate(entry.castAtTarget, Place(target, castHeight), Quaternion.identity);
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
        if (entry == null || entry.impactAtTarget == null || target == null) return;
        // 타격이 나오면 해당 스킬의 시전 연출은 끝낸다 (완료 시점과 같은 프레임).
        if (skill == activeCastSkill) StopCast();
        GameObject impact = Instantiate(entry.impactAtTarget, Place(target, entry.impactHeight), Quaternion.identity);
        Destroy(impact, impactLifetime);
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
        if (activeCast != null)
        {
            bool stillCasting = deck != null && deck.CastingCard == activeCastSkill;
            if (!stillCasting || castTarget == null || !castTarget.IsAlive) StopCast();
            else activeCast.transform.position = Place(castTarget, castHeight);
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

    private void StopCast()
    {
        if (activeCast != null) Destroy(activeCast);
        activeCast = null;
        activeCastSkill = null;
        castTarget = null;
    }
}
