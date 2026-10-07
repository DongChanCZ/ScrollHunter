using UnityEngine;

/// <summary>무방어 빨강 피격에 화면과 HUD를 함께 흔든다. 전투 난수·판정은 유지한다.</summary>
[DisallowMultipleComponent, DefaultExecutionOrder(10000)]
public class RedHitShake : MonoBehaviour
{
    [SerializeField, Min(0.01f)] private float duration = 0.24f;
    [SerializeField, Min(0f)] private float amplitude = 16f; // 1080 높이 기준 화면 픽셀
    [SerializeField, Min(0f)] private float frequency = 24f;
    private Player player;
    private CombatMetrics metrics;
    private Camera view;
    private Canvas canvas;
    private RectTransform hud;
    private Matrix4x4 baseProjection;
    private bool applied;
    private Vector2 offset;
    private float remaining;
    public bool IsShaking => remaining > 0f;

    private void Awake()
    {
        player = FindFirstObjectByType<Player>();
        metrics = FindFirstObjectByType<CombatMetrics>();
        view = GetComponent<Camera>();
    }
    private void Start()
    {
        // 기존 Canvas의 레이아웃·참조를 유지한 채 표시용 부모만 만든다.
        canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null || !canvas.isRootCanvas || canvas.renderMode != RenderMode.ScreenSpaceOverlay) return;
        hud = new GameObject("Screen shake content", typeof(RectTransform)).GetComponent<RectTransform>();
        hud.SetParent(canvas.transform, false);
        hud.anchorMin = Vector2.zero; hud.anchorMax = Vector2.one;
        hud.sizeDelta = Vector2.zero;
        CollectHud();
    }
    private void CollectHud()
    {
        if (hud == null) return;
        for (int i = 0; i < canvas.transform.childCount;)
        {
            Transform child = canvas.transform.GetChild(i);
            if (child == hud) { i++; continue; }
            child.SetParent(hud, false);
        }
    }
    private void OnEnable()
    {
        if (player == null) return;
        player.UnshieldedRedHit += Shake;
        player.BattleReset += Clear;
    }
    private void OnDisable()
    {
        if (player != null) { player.UnshieldedRedHit -= Shake; player.BattleReset -= Clear; }
        Clear();
    }
    private void Shake() => remaining = duration;
    // UI가 화면 좌표를 계산하기 전에 이전 표시 오프셋을 제거한다.
    private void Update() => RestoreView();
    private void LateUpdate() => Advance(metrics != null && metrics.Ended ? Time.unscaledDeltaTime : Time.deltaTime);
    private void Advance(float dt)
    {
        RestoreView();
        if (dt > 0f)
        {
            remaining = Mathf.Max(0f, remaining - dt);
            float phase = (duration - remaining) * frequency * Mathf.PI * 2f;
            offset = new Vector2(Mathf.Sin(phase), Mathf.Sin(phase * 1.3f))
                * (amplitude * remaining / duration * Screen.height / 1080f);
        }
        if (!IsShaking || view == null) return;
        CollectHud();
        baseProjection = view.projectionMatrix;
        var projection = baseProjection;
        // 원근 거리와 무관하게 배경·적·HUD를 같은 화면 픽셀만큼 이동한다.
        projection.m02 -= offset.x * 2f / view.pixelWidth;
        projection.m12 -= offset.y * 2f / view.pixelHeight;
        view.projectionMatrix = projection;
        if (hud != null) hud.anchoredPosition = offset / canvas.scaleFactor;
        applied = true;
    }
    private void RestoreView()
    {
        if (!applied) return;
        if (view != null) view.projectionMatrix = baseProjection;
        if (hud != null) hud.anchoredPosition = Vector2.zero;
        applied = false;
    }
    public void Clear()
    {
        RestoreView();
        offset = Vector2.zero;
        remaining = 0f;
    }
}
