using UnityEngine;

/// <summary>
/// 화면 전체를 삼키는 폭발 표시(10/7, 10 A42 후속 — 디엔드). BossAttackVfx가 실제 발동 알림 때 1회 만든다.
/// 카메라 앞 가까운 평면에 두고, 시작점(구체의 화면 위치)에서 퍼져 화면 가장자리를 넘을 때까지 커진 뒤 유지·사라진다.
/// 전투 HUD(Screen Space Overlay)는 항상 이 위에 그려지고, 충돌체가 없어 클릭·키 입력을 막지 않는다.
/// 게임 시간을 따르므로 정지 중 멈추고 배속에 맞춰 느려진다. 피해·판정과 무관하다.
/// </summary>
public class ScreenBlastVfx : MonoBehaviour
{
    [Tooltip("카메라 앞 거리(m). 근거리 클립(0.3)보다 멀고 적보다 가깝게")]
    [SerializeField] private float distance = 1.2f;
    [Tooltip("퍼지는 시간(초). 짧을수록 빠르게 화면을 덮는다")]
    [SerializeField] private float expandSeconds = 0.3f;
    [Tooltip("최대 불투명도(0~1)")]
    [Range(0f, 1f)] [SerializeField] private float maxOpacity = 0.95f;
    [Tooltip("화면을 덮은 채 유지하는 시간(초)")]
    [SerializeField] private float holdSeconds = 0.25f;
    [Tooltip("연기·잔상이 걷히는 시간(초)")]
    [SerializeField] private float fadeSeconds = 0.8f;
    [Tooltip("시작 지름(화면 높이 배율)")]
    [SerializeField] private float startScale = 0.12f;
    [Tooltip("최종 지름(화면 높이 배율). 가장자리 빈틈이 없도록 화면 대각선보다 넉넉하게")]
    [SerializeField] private float endScale = 6f;
    [Tooltip("퍼지는 동안 중심이 화면 가운데로 옮겨가는 비율")]
    [Range(0f, 1f)] [SerializeField] private float centerPull = 0.85f;

    private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
    private Camera cam;
    private Vector2 startViewport = new Vector2(0.5f, 0.5f);
    private float time;
    private bool finishing;
    public float Remaining => Mathf.Max(0f, Duration - time);
    public void PlayOutUnscaled() => finishing = true;
    private Renderer[] renderers;
    private MaterialPropertyBlock block;

    public float Duration => expandSeconds + holdSeconds + fadeSeconds;
    /// <summary>현재 불투명도 배율(검사용).</summary>
    public float Opacity { get; private set; }
    /// <summary>현재 지름(화면 높이 배율, 검사용).</summary>
    public float ScreenScale { get; private set; }

    private void Awake()
    {
        renderers = GetComponentsInChildren<Renderer>(true);
        block = new MaterialPropertyBlock();
    }

    /// <summary>시작점(월드)을 화면 위치로 바꿔 그 자리에서 퍼지기 시작한다.</summary>
    public void Begin(Camera camera, Vector3 focusWorld)
    {
        cam = camera != null ? camera : Camera.main;
        if (cam != null)
        {
            Vector3 vp = cam.WorldToViewportPoint(focusWorld);
            if (vp.z > 0f) startViewport = new Vector2(Mathf.Clamp01(vp.x), Mathf.Clamp01(vp.y));
        }
        time = 0f;
        finishing = false;
        Apply();
    }

    private void LateUpdate()
    {
        time += finishing ? Time.unscaledDeltaTime : Time.deltaTime;
        if (time >= Duration) { Destroy(gameObject); return; }
        Apply();
    }

    private void Apply()
    {
        if (cam == null) cam = Camera.main;
        if (cam == null) return;
        float e = expandSeconds > 0f ? Mathf.Clamp01(time / expandSeconds) : 1f;
        float grow = 1f - (1f - e) * (1f - e) * (1f - e);   // 처음 빠르게 퍼지고 끝에서 느려짐
        Vector2 vp = Vector2.Lerp(startViewport, new Vector2(0.5f, 0.5f), centerPull * grow);
        transform.SetPositionAndRotation(cam.ViewportToWorldPoint(new Vector3(vp.x, vp.y, distance)), cam.transform.rotation);
        float screenHeight = 2f * distance * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
        ScreenScale = Mathf.Lerp(startScale, endScale, grow);
        transform.localScale = Vector3.one * screenHeight * ScreenScale;

        float fade = Mathf.Clamp01((time - expandSeconds - holdSeconds) / Mathf.Max(0.01f, fadeSeconds));
        Opacity = maxOpacity * (time < expandSeconds ? Mathf.Lerp(0.7f, 1f, grow) : 1f - fade);
        block.SetColor(BaseColor, new Color(1f, 1f, 1f, Opacity));
        foreach (Renderer r in renderers) if (r != null) r.SetPropertyBlock(block);
    }
}
