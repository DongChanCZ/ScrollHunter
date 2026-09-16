using UnityEngine;

/// <summary>
/// 플레이어의 HP·방어도·기절 상태와 피격 처리.
/// 플레이어에게 공격력 스탯은 없다. 피해는 카드에 적힌 피해량으로만 결정된다.
/// </summary>
public class Player : MonoBehaviour
{
    [SerializeField] private float maxHp = 500f;

    [Tooltip("현재 HP. 플레이 중 인스펙터에서 직접 조절해 테스트할 수 있다.")]
    [SerializeField] private float currentHp = 500f;

    [Tooltip("체크하면 시작 시 HP를 최대치로 되돌린다. " +
             "끄면 위 현재 HP 값으로 시작한다 — 전투 간 HP 이월을 테스트할 때 쓴다.")]
    [SerializeField] private bool resetHpOnStart = true;

    [Tooltip("피해 감소에 쓰인다. 감소율 = 방어력 / (방어력 + 100)")]
    [SerializeField] private float defense = 20f;

    public float MaxHp => maxHp;
    public float Defense => defense;
    public float CurrentHp => currentHp;
    public bool IsAlive => currentHp > 0f;

    /// <summary>방어도. 합산 누적, 상한 없음. 전투 종료 시 소멸한다.</summary>
    public int Shield { get; private set; }

    /// <summary>남은 기절 시간(초). 주황 캐스팅에 대응하지 못하면 걸린다.</summary>
    public float StunRemaining { get; private set; }

    public bool IsStunned => StunRemaining > 0f;

    private bool defeatHandled;

    private void Awake()
    {
        // 직전 실행이 패배로 끝나 timeScale이 0인 채 남는 것을 막는다.
        Time.timeScale = 1f;

        if (resetHpOnStart) currentHp = maxHp;
        currentHp = Mathf.Clamp(currentHp, 0f, maxHp);

        Shield = 0;
        StunRemaining = 0f;
        defeatHandled = false;
    }

    private void Update()
    {
        if (StunRemaining > 0f)
        {
            StunRemaining = Mathf.Max(0f, StunRemaining - Time.deltaTime);
        }

        // 인스펙터에서 HP를 0으로 내린 경우에도 패배가 걸리게 한다.
        if (!IsAlive && !defeatHandled) Defeat();
    }

    public void AddShield(int amount)
    {
        if (amount <= 0) return;
        Shield += amount;
    }

    /// <summary>전투 종료 시 호출. 방어도는 이월되지 않는다.</summary>
    public void ClearShield() => Shield = 0;

    /// <summary>
    /// 기절을 건다. 이미 기절 중이면 남은 시간을 갱신한다 (02 문서 E15).
    /// 더 짧은 기절이 긴 기절을 덮어쓰지 않도록 둘 중 큰 값을 쓴다.
    /// 코스트는 기절 중에도 계속 충전된다 (E09) — CostSystem이 독립적으로 돌기 때문.
    /// </summary>
    public void ApplyStun(float seconds)
    {
        if (seconds <= 0f || !IsAlive) return;

        StunRemaining = Mathf.Max(StunRemaining, seconds);
        Debug.Log($"[{nameof(Player)}] 기절 {StunRemaining:0.#}초", this);
    }

    /// <param name="incomingDamage">방어력 적용 전 피해량(절대값).</param>
    public void TakeDamage(int incomingDamage)
    {
        if (!IsAlive) return;

        int taken = DamageFormula.Compute(incomingDamage, 1, defense);

        // 방어도에서 먼저 차감하고 남은 피해만 HP로 간다.
        int absorbed = Mathf.Min(Shield, taken);
        Shield -= absorbed;
        int toHp = taken - absorbed;

        currentHp = Mathf.Max(0f, currentHp - toHp);

        Debug.Log($"[{nameof(Player)}] 피격 {taken} (원본 {incomingDamage}) " +
                  $"방어도 흡수 {absorbed} → HP -{toHp} = {currentHp:F0}/{maxHp:F0} (방어도 {Shield})", this);

        if (!IsAlive) Defeat();
    }

    private void Defeat()
    {
        defeatHandled = true;
        StunRemaining = 0f;
        Debug.Log($"[{nameof(Player)}] 패배", this);
        Time.timeScale = 0f;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (maxHp < 1f) maxHp = 1f;
        currentHp = Mathf.Clamp(currentHp, 0f, maxHp);
    }
#endif
}
