using System.Collections.Generic;
using UnityEngine;

/// <summary>적 캐스팅 3색. 대응 수단이 색으로 결정된다.</summary>
public enum CastColor
{
    Green,
    Orange,
    Red,
}

/// <summary>
/// 적이 가진 공격 하나. 모든 적은 초록을 보유하고, 주황·빨강은 그 위에 추가된다.
/// </summary>
[System.Serializable]
public class EnemyAttack
{
    [Tooltip("캐스팅 바 위에 표시되는 이름")]
    [SerializeField] private string skillName = "";

    [SerializeField] private CastColor castColor = CastColor.Green;

    [SerializeField] private float castTime = 2.5f;

    [Tooltip("방어력 적용 전 피해량(절대값)")]
    [SerializeField] private int damage = 30;

    [Tooltip("발동부터 다음 캐스팅 시작까지의 간격(초). 초록 1.0 / 주황 1.5 / 빨강 2.0")]
    [SerializeField] private float staggerAfterCast = 1f;

    [Tooltip("발동 시 플레이어를 기절시키는 시간(초). 주황만 2.0, 나머지는 0")]
    [SerializeField] private float stunSeconds = 0f;

    public string SkillName => skillName;
    public CastColor CastColor => castColor;
    public float CastTime => castTime;
    public int Damage => damage;
    public float StaggerAfterCast => staggerAfterCast;
    public float StunSeconds => stunSeconds;
}

[CreateAssetMenu(fileName = "Enemy_", menuName = "ScrollHunter/Enemy Data")]
public class EnemyData : ScriptableObject
{
    [Tooltip("화면에 표시되는 적 이름")]
    [SerializeField] private string displayName = "";

    [SerializeField] private int maxHp = 300;

    [Tooltip("적의 방어력. 플레이어 피해 계산에 쓰인다.")]
    [SerializeField] private float defense = 0f;

    [Tooltip("보유 공격. 초록을 반드시 하나 포함할 것. 순서는 상관없다.")]
    [SerializeField] private List<EnemyAttack> attacks = new List<EnemyAttack>();

    public string DisplayName => displayName;
    public int MaxHp => maxHp;
    public float Defense => defense;
    public IReadOnlyList<EnemyAttack> Attacks => attacks;

    /// <summary>해당 색의 공격. 없으면 null.</summary>
    public EnemyAttack Find(CastColor color)
    {
        for (int i = 0; i < attacks.Count; i++)
            if (attacks[i] != null && attacks[i].CastColor == color) return attacks[i];
        return null;
    }
}
