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

    [Tooltip("플레이어 기절 시간. 일반 주황 2초, 보스 나이트메어 5초.")]
    [SerializeField] private float stunSeconds = 0f;

    public string SkillName => skillName;
    public CastColor CastColor => castColor;
    public float CastTime => castTime;
    public int Damage => damage;
    public float StaggerAfterCast => staggerAfterCast;
    public float StunSeconds => stunSeconds;
}

[System.Serializable]
public class EnemyPhase
{
    [SerializeField, Range(0f, 1f)] private float hpThreshold = 1f;
    [Tooltip("공격 목록의 1번부터 시작하는 번호. 차단도 한 순서로 소모한다.")]
    [SerializeField] private int[] pattern = { 1 };
    [SerializeField, Min(0.01f)] private float castTimeMultiplier = 1f;
    public float HpThreshold => hpThreshold;
    public IReadOnlyList<int> Pattern => pattern;
    public float CastTimeMultiplier => castTimeMultiplier;
}

[CreateAssetMenu(fileName = "Enemy_", menuName = "ScrollHunter/Enemy Data")]
public class EnemyData : ScriptableObject
{
    [Tooltip("화면에 표시되는 적 이름")]
    [SerializeField] private string displayName = "";

    [SerializeField] private int maxHp = 300;

    [Tooltip("적의 방어력. 플레이어 피해 계산에 쓰인다.")]
    [SerializeField] private float defense = 0f;

    [Tooltip("보유 공격. 초록 필수. 보스 패턴 번호는 이 목록 순서(1부터).")]
    [SerializeField] private List<EnemyAttack> attacks = new List<EnemyAttack>();

    [Tooltip("비어 있으면 일반 적 규칙. 보스는 HP 기준 내림차순으로 작성한다.")]
    [SerializeField] private List<EnemyPhase> phases = new List<EnemyPhase>();
    [SerializeField, Min(0f)] private float phaseTransitionSeconds = 2f;
    public IReadOnlyList<EnemyPhase> Phases => phases;
    public float PhaseTransitionSeconds => phaseTransitionSeconds;

    [Header("보스 오브 (A43)")]
    [SerializeField] private EnemyData ruinOrb;
    [SerializeField] private EnemyData cycleOrb;
    [SerializeField, TextArea] private string passiveDescription;
    [SerializeField, Min(1f)] private float orbDamageMultiplier = 1.1f;
    [SerializeField, Range(0f, .99f)] private float orbCastTimeReduction = .2f;
    [SerializeField, Min(0)] private int orbDestructionDamage = 150;
    public float OrbDamageMultiplier => orbDamageMultiplier;
    public float OrbCastTimeReduction => orbCastTimeReduction;
    public int OrbDestructionDamage => orbDestructionDamage;
    public EnemyData RuinOrb => ruinOrb;
    public EnemyData CycleOrb => cycleOrb;
    public string PassiveDescription => passiveDescription;

    [Header("오브 자신의 표시 (A43 연출, 오브 데이터에만 연결)")]
    [Tooltip("BossOrbVisual 프리팹. 비우면 임시 구체로 표시")]
    [SerializeField] private GameObject orbVisual;
    [Tooltip("OrbBreakVfx 프리팹. 실제 공격으로 파괴됐을 때만 재생")]
    [SerializeField] private GameObject orbBreak;
    public GameObject OrbVisual => orbVisual;
    public GameObject OrbBreak => orbBreak;

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
