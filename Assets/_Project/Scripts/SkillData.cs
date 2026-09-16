using UnityEngine;

public enum SkillCategory
{
    Deal,
    Interrupt,
    Shield,
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

    [Tooltip("0이면 즉발. 행동 잠금은 MAX(캐스팅 시간, 최소 GCD)")]
    [SerializeField] private float castTime = 0f;

    [Tooltip("Shield 카드가 플레이어에게 부여하는 방어도. Deal/Interrupt는 0.")]
    [SerializeField] private int shieldAmount = 0;

    [Tooltip("체크하면 타겟과 무관하게 적 전체에 적용된다. Deal 카드에만 의미가 있다.")]
    [SerializeField] private bool isAreaOfEffect = false;

    public string DisplayName => displayName;
    public float Cost => cost;
    public SkillCategory Category => category;
    public int Damage => damage;
    public int HitCount => hitCount;
    public float CastTime => castTime;
    public int ShieldAmount => shieldAmount;
    public bool IsAreaOfEffect => isAreaOfEffect;
}
