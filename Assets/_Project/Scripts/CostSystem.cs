using UnityEngine;

/// <summary>
/// 코스트 자원. 초당 일정량 충전되고 상한을 넘은 분은 소멸한다.
/// Time.deltaTime을 쓰므로 일시정지(timeScale 0)와 배속이 그대로 반영된다.
/// </summary>
public class CostSystem : MonoBehaviour
{
    [Tooltip("초당 충전량")]
    [SerializeField] private float regenPerSecond = 0.8f;

    [Tooltip("상한. 초과분은 소멸")]
    [SerializeField] private float maxCost = 10f;

    [Tooltip("전투 시작값")]
    [SerializeField] private float startCost = 3f;

    [Tooltip("4단계 계측. 비워두면 씬에서 자동으로 찾는다.")]
    [SerializeField] private CombatMetrics metrics;

    public float Current { get; private set; }
    public float Max => maxCost;

    private void Awake()
    {
        BeginBattle();
        if (metrics == null) metrics = FindFirstObjectByType<CombatMetrics>();
    }

    public void BeginBattle() => Current = Mathf.Clamp(startCost, 0f, maxCost);

    private void Update()
    {
        if (metrics != null && metrics.Ended) return;
        // 상한을 넘긴 충전분은 소멸한다. 계측은 그 버려진 양만 누적한다
        // (상한에 머문 시간과는 다른 값이다 — 09 문서 4단계 ②).
        float charged = Current + regenPerSecond * Time.deltaTime;
        if (charged > maxCost)
        {
            if (metrics != null) metrics.RecordCostWasted(charged - maxCost);
            charged = maxCost;
        }
        Current = charged;
    }

    public bool CanAfford(float amount) => Current >= amount;

    public bool TrySpend(float amount)
    {
        if (!CanAfford(amount)) return false;
        Current -= amount;
        return true;
    }
}
