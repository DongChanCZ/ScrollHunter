using UnityEngine;

/// <summary>정해진 간격으로 타격. 단일은 입력 대상 고정, 광역은 매 타격 생존 적 선택.</summary>
[CreateAssetMenu(fileName = "Channel_Damage", menuName = "ScrollHunter/Channel Damage")]
public class DamageChannelEffect : ChannelEffect
{
    [SerializeField, Min(0f)] private float firstHitDelay = 0.5f;
    [SerializeField, Min(0.01f)] private float hitInterval = 0.5f;
    [SerializeField, Min(0f)] private float targetDeathRecovery = 0.3f;
    [SerializeField, TextArea] private string timingFormat = "기본 피해 {0} × {1}회\n첫 타격 {2:0.##}초 / 간격 {3:0.##}초\n타격 후 남은 잠금 {4:0.##}초\n{5}";
    [SerializeField] private string singleTargetFormat = "입력 대상 고정. 사망 시 후딜 {0:0.##}초";
    [SerializeField] private string areaTargetText = "매 타격 시 생존 적 전체";

    private Enemy target;
    private float elapsed;
    private int hits;
    private bool targetLost;
    private float recoveryRemaining;

    public float FirstHitDelay => firstHitDelay;
    public float HitInterval => hitInterval;
    public float TargetDeathRecovery => targetDeathRecovery;
    public override bool TargetLost => targetLost;
    public override float TargetLossRecoveryRemaining => recoveryRemaining;

    public override bool IsValidFor(SkillData skill)
        => skill.Category == SkillCategory.Deal && skill.HitCount > 0 && firstHitDelay >= 0f
        && hitInterval > 0f && targetDeathRecovery >= 0f
        && !float.IsInfinity(targetDeathRecovery)
        && firstHitDelay + (skill.HitCount - 1) * hitInterval <= skill.CastTime;

    public override string Describe(SkillData skill)
        => string.Format(timingFormat, skill.Damage, skill.HitCount, firstHitDelay, hitInterval,
            Mathf.Max(0f, skill.CastTime - firstHitDelay - (skill.HitCount - 1) * hitInterval),
            skill.IsAreaOfEffect ? areaTargetText : string.Format(singleTargetFormat, targetDeathRecovery));

    public override void Begin(ChannelContext context)
    {
        target = context.Enemies != null ? context.Enemies.CurrentTarget : null;
        elapsed = 0f;
        hits = 0;
        targetLost = false;
        recoveryRemaining = 0f;
        if (firstHitDelay == 0f) Tick(context, 0f);
    }

    public override void Tick(ChannelContext context, float deltaTime)
    {
        if (targetLost || context.Enemies == null || context.Enemies.CombatEnded) return;
        if (!context.Skill.IsAreaOfEffect && (target == null || !target.IsAlive))
        {
            LoseTarget(deltaTime);
            return;
        }
        elapsed += deltaTime;
        // 한 프레임에 여러 타격 시점이 지나도 누락 없이, 설정한 횟수까지만 적용.
        while (hits < context.Skill.HitCount)
        {
            float hitAt = firstHitDelay + hits * hitInterval;
            if (elapsed + 0.000001f < hitAt) break;
            hits++;
            if (context.Skill.IsAreaOfEffect)
            {
                foreach (Enemy enemy in context.Enemies.GetAliveEnemies()) context.ApplyHit(enemy, hits);
            }
            else context.ApplyHit(target, hits);
            context.Enemies.NotifyEnemyDied();
            if (context.Enemies.CombatEnded) return;
            if (!context.Skill.IsAreaOfEffect && (target == null || !target.IsAlive))
            {
                LoseTarget(elapsed - hitAt);
                return;
            }
        }
    }

    private void LoseTarget(float timeAfterDeath)
    {
        targetLost = true;
        recoveryRemaining = Mathf.Max(0f, targetDeathRecovery - timeAfterDeath);
    }

    public override void End(ChannelContext context, ChannelEndReason reason) => target = null;
}
