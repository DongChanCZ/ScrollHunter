using UnityEngine;

public enum SkillCategory
{
    Deal,
    Interrupt,
    Shield,
}

// 0은 기존 에셋의 완료 시 발동 동작을 보존한다.
public enum SkillActivation
{
    Cast = 0,
    Channeling = 1,
}

[CreateAssetMenu(fileName = "Skill_", menuName = "ScrollHunter/Skill Data")]
public class SkillData : ScriptableObject
{
    [Tooltip("화면에 표시되는 이름")]
    [SerializeField] private string displayName = "";

    [SerializeField] private float cost = 1f;

    [SerializeField] private SkillCategory category = SkillCategory.Deal;

    [Tooltip("1타 피해량(절대값). 공격력 스탯은 없다. 카드에 적힌 이 숫자가 곧 피해다.")]
    [SerializeField] private int damage = 0;

    [Tooltip("타수. 총 피해 = 피해량 × 타수")]
    [SerializeField] private int hitCount = 1;

    [Tooltip("일반 공격의 타격을 각각 계산·표시. 기존 합산형 에셋은 체크하지 않는다.")]
    [SerializeField] private bool resolveHitsSeparately = false;

    [SerializeField] private SkillActivation activation = SkillActivation.Cast;

    [Tooltip("일반 시전: 완료까지의 시간(0이면 즉발). 채널링: 효과 유지/발생 시간(0보다 커야 함).")]
    [SerializeField] private float castTime = 0f;

    [Tooltip("Shield 카드가 플레이어에게 부여하는 방어도. Deal/Interrupt는 0.")]
    [SerializeField] private int shieldAmount = 0;

    [Tooltip("체크하면 타겟과 무관하게 적 전체에 적용된다. Deal 카드에만 의미가 있다.")]
    [SerializeField] private bool isAreaOfEffect = false;

    [Tooltip("채널링 전용 효과. 시작/진행/종료 기능을 구현한 ChannelEffect 에셋을 연결한다.")]
    [SerializeField] private ChannelEffect channelEffect;

    public SkillActivation Activation => activation;
    public ChannelEffect ChannelEffect => channelEffect;
    public bool IsChanneling => activation == SkillActivation.Channeling;
    public bool HasValidChannel => !IsChanneling || (channelEffect != null
        && castTime > 0f && !float.IsInfinity(castTime) && !float.IsNaN(castTime)
        && channelEffect.IsValidFor(this));

    public string DisplayName => displayName;
    public float Cost => cost;
    public SkillCategory Category => category;
    public int Damage => damage;
    public int HitCount => Mathf.Max(1, hitCount); // 02 E19: 미설정·0타도 1타로 보정.
    public bool ResolveHitsSeparately => resolveHitsSeparately;
    public float CastTime => castTime;
    public int ShieldAmount => shieldAmount;
    public bool IsAreaOfEffect => isAreaOfEffect;
}
