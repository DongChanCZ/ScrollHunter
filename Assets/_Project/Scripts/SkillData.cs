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

    [Tooltip("1타 피해 = 공격력 × 계수")]
    [SerializeField] private float coefficient = 1f;

    [Tooltip("타수. 총 피해 = 1타 피해 × 타수")]
    [SerializeField] private int hitCount = 1;

    [Tooltip("0이면 즉발. 행동 잠금은 MAX(캐스팅 시간, 최소 GCD)")]
    [SerializeField] private float castTime = 0f;

    public string DisplayName => displayName;
    public float Cost => cost;
    public SkillCategory Category => category;
    public float Coefficient => coefficient;
    public int HitCount => hitCount;
    public float CastTime => castTime;
}
