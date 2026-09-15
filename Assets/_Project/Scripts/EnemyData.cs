using UnityEngine;

/// <summary>적 캐스팅 3색. 대응 수단이 색으로 결정된다.</summary>
public enum CastColor
{
    Green,
    Orange,
    Red,
}

[CreateAssetMenu(fileName = "Enemy_", menuName = "ScrollHunter/Enemy Data")]
public class EnemyData : ScriptableObject
{
    [Tooltip("화면에 표시되는 적 이름")]
    [SerializeField] private string displayName = "";

    [SerializeField] private int maxHp = 300;

    [Tooltip("적의 방어력. 플레이어 공격이 생기는 3단계부터 쓰인다.")]
    [SerializeField] private float defense = 0f;

    [SerializeField] private float castTime = 2.5f;

    [SerializeField] private CastColor castColor = CastColor.Green;

    [Tooltip("발동 시 플레이어에게 주는 피해량(절대값). 방어력 적용 전 값.")]
    [SerializeField] private int damage = 30;

    [Tooltip("캐스팅 바 위에 표시되는 스킬 이름")]
    [SerializeField] private string castSkillName = "";

    public string DisplayName => displayName;
    public int MaxHp => maxHp;
    public float Defense => defense;
    public float CastTime => castTime;
    public CastColor CastColor => castColor;
    public int Damage => damage;
    public string CastSkillName => castSkillName;
}
