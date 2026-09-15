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

    public float Current { get; private set; }
    public float Max => maxCost;

    private void Awake()
    {
        Current = Mathf.Min(startCost, maxCost);
    }

    private void Update()
    {
        Current = Mathf.Min(Current + regenPerSecond * Time.deltaTime, maxCost);
    }

    public bool CanAfford(float amount) => Current >= amount;

    public bool TrySpend(float amount)
    {
        if (!CanAfford(amount)) return false;
        Current -= amount;
        return true;
    }
}
