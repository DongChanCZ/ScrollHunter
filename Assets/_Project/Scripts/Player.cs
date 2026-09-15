using UnityEngine;

/// <summary>
/// 플레이어의 HP와 피격 처리. 2단계에서는 맞기만 한다.
/// 방어도(Shield)는 3단계에서 붙인다.
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

    private void Awake()
    {
        // 직전 실행이 패배로 끝나 timeScale이 0인 채 남는 것을 막는다.
        Time.timeScale = 1f;
        CurrentHp = maxHp;
    }

    /// <param name="incomingDamage">방어력 적용 전 피해량(절대값).</param>
    public void TakeDamage(int incomingDamage)
    {
        if (!IsAlive) return;

        int taken = DamageFormula.Compute(incomingDamage, 1, defense);
        CurrentHp = Mathf.Max(0f, CurrentHp - taken);

        Debug.Log($"[{nameof(Player)}] 피격 {taken} (원본 {incomingDamage}) → HP {CurrentHp:F0}/{maxHp:F0}", this);

        if (!IsAlive)
        {
            Debug.Log($"[{nameof(Player)}] 패배", this);
            Time.timeScale = 0f;
        }
    }
}
