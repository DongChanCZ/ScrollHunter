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

    [Tooltip("4단계 계측. 비워두면 씬에서 자동으로 찾는다.")]
    [SerializeField] private CombatMetrics metrics;

    [Tooltip("플레이어 공격의 타격·적마다 판정하는 크리티컬 확률(%).")]
    [SerializeField, Range(0f, 100f)] private float criticalChance = 15f;
    [SerializeField, Min(1f)] private float criticalMultiplier = 1.5f;

    private float criticalChanceBonus;
    public void AddCriticalChanceBonus(float percentagePoints) => criticalChanceBonus += percentagePoints;
    public float CriticalChance => Mathf.Clamp(criticalChance + criticalChanceBonus, 0f, 100f);
    public float CriticalMultiplier => Mathf.Max(1f, criticalMultiplier);

    public bool RollCritical()
    {
        float chance = CriticalChance;
        return chance > 0f && (chance >= 100f || Random.value * 100f < chance);
    }

    private float maxHpBonus;
    private float shieldGainBonus;
    private float interruptStaggerBonus;
    private int potionCapacityBonus;
    [SerializeField, Min(0)] private int basePotionCapacity = 3;
    [SerializeField, Range(0f, 1f)] private float potionHealFraction = 0.3f;
    [SerializeField] private KeyCode potionKey = KeyCode.LeftShift;
    [SerializeField] private KeyCode alternatePotionKey = KeyCode.RightShift;
    public int PotionsRemaining { get; private set; }
    public float PotionHealAmount => Mathf.Max(0, Mathf.FloorToInt(MaxHp * potionHealFraction + 0.5f));
    public bool CanUsePotion => Time.timeScale > 0f && IsAlive && !IsStunned
        && PotionsRemaining > 0 && currentHp < MaxHp && PotionHealAmount > 0f
        && (metrics == null || !metrics.Ended);
    public bool TryUsePotion()
    {
        if (!CanUsePotion) return false;
        float healed = Heal(PotionHealAmount);
        PotionsRemaining--;
        if (metrics != null) metrics.RecordPotionUsed(healed, PotionsRemaining, PotionCapacity);
        return true;
    }
    public int PotionCapacity => Mathf.Max(0, basePotionCapacity + potionCapacityBonus);
    public float MaxHp => Mathf.Max(1f, maxHp + maxHpBonus);
    public float ShieldGainMultiplier => Mathf.Max(0f, 1f + shieldGainBonus);
    public float InterruptStaggerBonus => interruptStaggerBonus;
    public void AddShieldGainBonus(float amount) => shieldGainBonus += amount;
    public void AddInterruptStaggerBonus(float seconds) => interruptStaggerBonus += seconds;
    public void AddMaxHpBonus(float amount)
    {
        maxHpBonus += amount;
        currentHp = Mathf.Min(currentHp, MaxHp);
    }
    public void AddPotionCapacityBonus(int amount)
    {
        potionCapacityBonus += amount;
        PotionsRemaining = Mathf.Min(PotionsRemaining, PotionCapacity);
    }
    public int GetShieldAmount(int baseAmount) => baseAmount <= 0 ? 0
        : Mathf.Max(0, Mathf.FloorToInt(baseAmount * ShieldGainMultiplier + 0.5f));

    [SerializeField] private string healLogFormat = "[Player] 회복 {0:0} / 요청 {1:0} → HP {2:0}/{3:0}";
    public float Heal(float amount)
    {
        if (!IsAlive || amount <= 0f || float.IsNaN(amount) || float.IsInfinity(amount)) return 0f;
        float restored = Mathf.Min(amount, MaxHp - currentHp);
        currentHp += restored;
        Debug.Log(string.Format(healLogFormat, restored, amount, currentHp, MaxHp), this);
        return restored;
    }
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

        if (resetHpOnStart) currentHp = MaxHp;
        PotionsRemaining = PotionCapacity;
        currentHp = Mathf.Clamp(currentHp, 0f, MaxHp);

        Shield = 0;
        StunRemaining = 0f;
        defeatHandled = false;

        if (metrics == null) metrics = FindFirstObjectByType<CombatMetrics>();
    }

    private void Update()
    {
        if (StunRemaining > 0f)
        {
            StunRemaining = Mathf.Max(0f, StunRemaining - Time.deltaTime);
        }

        if (Input.GetKeyDown(potionKey) || Input.GetKeyDown(alternatePotionKey)) TryUsePotion();

        // 인스펙터에서 HP를 0으로 내린 경우에도 패배가 걸리게 한다.
        if (!IsAlive && !defeatHandled) Defeat();
    }

    public void BeginBattle(bool restoreHp)
    {
        if (restoreHp) { currentHp = MaxHp; PotionsRemaining = PotionCapacity; }
        EndBattle();
        defeatHandled = false;
    }

    public void EndBattle()
    {
        ClearShield();
        StunRemaining = 0f;
    }

    public int AddShield(int baseAmount)
    {
        int granted = GetShieldAmount(baseAmount);
        Shield += granted;
        return granted;
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
        if (seconds <= 0f || !IsAlive || (metrics != null && metrics.Ended)) return;

        StunRemaining = Mathf.Max(StunRemaining, seconds);
        Debug.Log($"[{nameof(Player)}] 기절 {StunRemaining:0.#}초", this);
    }

    /// <param name="incomingDamage">방어력 적용 전 피해량(절대값).</param>
    public void TakeDamage(int incomingDamage)
    {
        if (!IsAlive || (metrics != null && metrics.Ended)) return;

        int taken = DamageFormula.Compute(incomingDamage, 1, defense);

        // 방어도에서 먼저 차감하고 남은 피해만 HP로 간다.
        int absorbed = Mathf.Min(Shield, taken);
        Shield -= absorbed;
        int toHp = taken - absorbed;

        currentHp = Mathf.Max(0f, currentHp - toHp);

        // 계측은 부여량이 아니라 실제로 방어도가 막아낸 양을 센다 (09 문서 4단계 ②).
        if (metrics != null) metrics.RecordShieldAbsorbed(absorbed);

        Debug.Log($"[{nameof(Player)}] 피격 {taken} (원본 {incomingDamage}) " +
                  $"방어도 흡수 {absorbed} → HP -{toHp} = {currentHp:F0}/{MaxHp:F0} (방어도 {Shield})", this);

        if (!IsAlive) Defeat();
    }

    private void Defeat()
    {
        defeatHandled = true;
        EndBattle();
        if (metrics != null) metrics.ReportDefeat();
        Debug.Log($"[{nameof(Player)}] 패배", this);
        Time.timeScale = 0f;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (maxHp < 1f) maxHp = 1f;
        currentHp = Mathf.Clamp(currentHp, 0f, MaxHp);
    }
#endif
}
