using UnityEngine;

/// <summary>
/// 플레이어의 HP·방어도와 피격 처리.
/// 플레이어에게 공격력 스탯은 없다. 피해는 카드에 적힌 피해량으로만 결정된다.
/// </summary>
public class Player : MonoBehaviour
{
    [SerializeField] private float maxHp = 500f;

    [Tooltip("피해 감소에 쓰인다. 감소율 = 방어력 / (방어력 + 100)")]
    [SerializeField] private float defense = 20f;

    public float MaxHp => maxHp;
    public float Defense => defense;
    public float CurrentHp { get; private set; }
    public bool IsAlive => CurrentHp > 0f;

    /// <summary>방어도. 합산 누적, 상한 없음. 전투 종료 시 소멸한다.</summary>
    public int Shield { get; private set; }

    private void Awake()
    {
        // 직전 실행이 패배로 끝나 timeScale이 0인 채 남는 것을 막는다.
        Time.timeScale = 1f;
        CurrentHp = maxHp;
        Shield = 0;
    }

    public void AddShield(int amount)
    {
        if (amount <= 0) return;
        Shield += amount;
    }

    /// <summary>전투 종료 시 호출. 방어도는 이월되지 않는다.</summary>
    public void ClearShield() => Shield = 0;

    /// <param name="incomingDamage">방어력 적용 전 피해량(절대값).</param>
    public void TakeDamage(int incomingDamage)
    {
        if (!IsAlive) return;

        int taken = DamageFormula.Compute(incomingDamage, 1, defense);

        // 방어도에서 먼저 차감하고 남은 피해만 HP로 간다.
        int absorbed = Mathf.Min(Shield, taken);
        Shield -= absorbed;
        int toHp = taken - absorbed;

        CurrentHp = Mathf.Max(0f, CurrentHp - toHp);

        Debug.Log($"[{nameof(Player)}] 피격 {taken} (원본 {incomingDamage}) " +
                  $"방어도 흡수 {absorbed} → HP -{toHp} = {CurrentHp:F0}/{maxHp:F0} (방어도 {Shield})", this);

        if (!IsAlive)
        {
            Debug.Log($"[{nameof(Player)}] 패배", this);
            Time.timeScale = 0f;
        }
    }
}
