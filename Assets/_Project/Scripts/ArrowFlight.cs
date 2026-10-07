using UnityEngine;

/// <summary>
/// 발사 연출용 화살 비행(10/2). 충돌·피해 없음. 목표에 닿으면 사라진다.
/// 게임 시간으로 진행하므로 정지 중에는 멈춘다.
/// </summary>
public class ArrowFlight : MonoBehaviour
{
    [Tooltip("비행 중간의 최대 포물선 높이(월드 단위)")]
    [SerializeField] private float arcHeight = 0.15f;

    private Vector3 start, end;
    private float duration, elapsed;
    private bool finishing;
    public bool IsFlying => elapsed < duration;
    public void PlayOutUnscaled()
    {
        finishing = true;
        FinishingVfx.PlayUnscaled(gameObject);
    }

    public void Launch(Vector3 target, float seconds)
    {
        start = transform.position;
        end = target;
        duration = Mathf.Max(0.01f, seconds);
        elapsed = 0f;
        finishing = false;
        enabled = true;
    }

    private void Update()
    {
        elapsed += finishing ? Time.unscaledDeltaTime : Time.deltaTime;
        float k = Mathf.Clamp01(elapsed / duration);
        Vector3 next = Vector3.Lerp(start, end, k) + Vector3.up * (arcHeight * 4f * k * (1f - k));
        Vector3 step = next - transform.position;
        if (step.sqrMagnitude > 1e-8f) transform.rotation = Quaternion.LookRotation(step);
        transform.position = next;
        if (k >= 1f) Destroy(gameObject);
    }
}
