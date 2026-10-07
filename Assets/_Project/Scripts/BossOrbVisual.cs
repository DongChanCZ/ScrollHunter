using UnityEngine;

/// <summary>
/// 보스 오브 외형(10/7, 10 A43 연출). 오브(Enemy) 루트의 자식으로 붙어 소환·대기 표시만 맡는다.
/// 성장·맥동은 Body 자식에만 적용해 루트의 충돌체·클릭 영역·HP바 위치는 바뀌지 않는다. 판정·활성화 시점은 BossOrbs 그대로.
/// 시간은 게임 시간(Time.deltaTime)이라 정지·배속을 따른다. BeginSummon이 남은 입자·축소 상태·발광을 매번 처음으로 되돌린다.
/// </summary>
public sealed class BossOrbVisual : MonoBehaviour
{
    [System.Serializable]
    public struct Spinner
    {
        public Transform target;
        public Vector3 degreesPerSecond;
    }

    [Header("크기 (오브 루트 기준, 판정 크기와 무관)")]
    [SerializeField] private Transform body;
    [SerializeField] private float bodyScale = 1f;

    [Header("대기")]
    [SerializeField] private float pulseAmount = 0.03f;
    [SerializeField] private float pulseHz = 0.55f;
    [SerializeField] private float flowSpeed = 1f;
    [SerializeField] private Spinner[] spinners = new Spinner[0];

    [Header("발광·입자량")]
    [SerializeField] private float glow = 1f;
    [Tooltip("대기 기운 입자(Body 아래 반복 방출) 양 배율")]
    [SerializeField] private float auraAmount = 1f;
    [SerializeField] private Renderer[] surfaces = new Renderer[0];

    [Header("최초 소환 (페이즈 전환 동안)")]
    [SerializeField] private float initialStartScale = 0.12f;
    [Tooltip("전환이 끝나 활성화되기 전까지 자라는 크기. 활성화 순간 완성 맥동으로 1이 된다.")]
    [SerializeField] private float previewScale = 0.82f;
    [SerializeField] private float previewReveal = 0.75f;

    [Header("다크홀 재소환 (활성 상태로 시작)")]
    [SerializeField] private float respawnSeconds = 0.5f;
    [Tooltip("재소환은 활성화 순간부터 위치를 알아볼 수 있게 이 크기에서 시작한다")]
    [SerializeField] private float respawnStartScale = 0.55f;
    [Tooltip("재소환 동안 그리기 순서를 올려 다크홀 발동 연기에 오브가 묻히지 않게 한다(대기로 돌아가면 원래 순서)")]
    [SerializeField] private int respawnSortingBoost = 20;

    [Header("완성 맥동")]
    [SerializeField] private float completeSeconds = 0.3f;
    [SerializeField] private float completeOvershoot = 0.14f;
    [SerializeField] private float completeGlow = 0.8f;

    [Header("소환 입자 (안쪽으로 모임) / 완성 고리")]
    [SerializeField] private ParticleSystem[] gather = new ParticleSystem[0];
    [SerializeField] private ParticleSystem completeBurst;

    private enum Phase { Idle, Summon, Hold, Complete }
    private Phase phase = Phase.Idle;
    private float timer, duration, from, to, revealFrom, revealTo, holdScale, flowTime, idleTime;
    private bool completeAfterSummon;
    private MaterialPropertyBlock block;
    private ParticleSystem[] all;
    private static readonly int FlowTimeId = Shader.PropertyToID("_FlowTime");
    private static readonly int RevealId = Shader.PropertyToID("_Reveal");
    private static readonly int GlowId = Shader.PropertyToID("_Glow");

    /// <summary>현재 보이는 구체 지름 배율(Body 월드 크기). 파괴 연출이 이 크기에서 수축을 시작한다.</summary>
    public float WorldScale => body != null ? body.lossyScale.x : transform.lossyScale.x;
    public bool IsSummoning => phase != Phase.Idle;
    public float PhaseElapsed => timer;
    public float CurrentScale { get; private set; } = 1f;
    public float CurrentReveal { get; private set; } = 1f;

    /// <summary>최초 소환: 전환 동안 작은 중심체가 자라며 기운이 모인다. 활성화 때 <see cref="Complete"/>.</summary>
    public void BeginInitialSummon(float seconds) { Begin(Mathf.Max(0.01f, seconds), initialStartScale, previewScale, 0f, previewReveal, false); SetFront(false); }

    /// <summary>재소환: 이미 활성화된 오브의 시각 요소만 짧게 자란다. 다크홀 발동 연기 위에 그려 활성화 순간부터 위치가 보이게 한다.</summary>
    public void BeginRespawn() { Begin(respawnSeconds, respawnStartScale, 1f, 0.35f, 1f, true); SetFront(true); }

    private Renderer[] renderers;
    private int[] baseOrders;
    private Material frontCore, baseCore;
    public bool DrawnInFront { get; private set; }

    /// <summary>불투명 중심체는 반투명 연기보다 먼저 그려져 가려지므로, 재소환 동안만 투명 대기열 사본으로 바꿔 순서를 올린다.</summary>
    private void SetFront(bool front)
    {
        if (renderers == null)
        {
            renderers = GetComponentsInChildren<Renderer>(true);
            baseOrders = new int[renderers.Length];
            for (int i = 0; i < renderers.Length; i++) baseOrders[i] = renderers[i].sortingOrder;
        }
        if (DrawnInFront == front) return;
        DrawnInFront = front;
        for (int i = 0; i < renderers.Length; i++) renderers[i].sortingOrder = baseOrders[i] + (front ? respawnSortingBoost : 0);
        var core = surfaces.Length > 0 ? surfaces[0] : null;
        if (core == null) return;
        if (front)
        {
            baseCore = core.sharedMaterial;
            if (frontCore == null) frontCore = new Material(baseCore) { name = baseCore.name + " (front)", renderQueue = 3000 };
            core.sharedMaterial = frontCore;
            core.sortingOrder = baseOrders[System.Array.IndexOf(renderers, core)] + respawnSortingBoost - 1;   // 자기 기운 입자보다는 먼저
        }
        else if (baseCore != null) core.sharedMaterial = baseCore;
    }

    private void OnDestroy() { if (frontCore != null) Destroy(frontCore); }

    /// <summary>완성 맥동 뒤 대기로 넘어간다. 소환 중이 아니면 맥동만 짧게 보인다.</summary>
    public void Complete()
    {
        holdScale = CurrentScale;
        revealFrom = CurrentReveal;
        phase = Phase.Complete;
        timer = 0f;
        foreach (var ps in gather) if (ps != null) ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        if (completeBurst != null) { completeBurst.Clear(true); completeBurst.Play(true); }
    }

    private void Begin(float seconds, float start, float end, float reveal0, float reveal1, bool complete)
    {
        Initialize();
        foreach (var ps in all) { ps.Clear(true); }
        foreach (var ps in all)
        {
            if (ps == completeBurst || System.Array.IndexOf(gather, ps) >= 0) continue;
            if (ps.main.playOnAwake) ps.Play(false);
        }
        foreach (var ps in gather) if (ps != null) ps.Play(true);
        phase = Phase.Summon;
        timer = 0f; duration = seconds; from = start; to = end;
        revealFrom = reveal0; revealTo = reveal1;
        completeAfterSummon = complete;
        flowTime = Random.value * 10f;
        Apply(start, reveal0, 1f);
    }

    private void Initialize()
    {
        if (block != null) return;
        block = new MaterialPropertyBlock();
        all = GetComponentsInChildren<ParticleSystem>(true);
        if (body == null) return;
        foreach (var ps in body.GetComponentsInChildren<ParticleSystem>(true))
        {
            var em = ps.emission;
            em.rateOverTimeMultiplier *= Mathf.Max(0f, auraAmount);
        }
    }

    private void Awake() => Initialize();

    private void Update()
    {
        float dt = Time.deltaTime;
        flowTime += dt * flowSpeed;
        foreach (var s in spinners) if (s.target != null) s.target.Rotate(s.degreesPerSecond * dt, Space.Self);
        timer += dt;
        float scale = 1f, reveal = 1f, extraGlow = 0f;
        switch (phase)
        {
            case Phase.Summon:
            {
                float k = Mathf.Clamp01(timer / duration);
                float eased = 1f - Mathf.Pow(1f - k, 3f);
                scale = Mathf.Lerp(from, to, eased);
                reveal = Mathf.Lerp(revealFrom, revealTo, k);
                if (k >= 1f)
                {
                    CurrentScale = scale; CurrentReveal = reveal;
                    if (completeAfterSummon) Complete(); else { phase = Phase.Hold; timer = 0f; }
                }
                break;
            }
            case Phase.Hold:
                // 활성화 대기: 다 자라기 직전 크기에서 숨 쉬듯 맥동(위치는 고정)
                scale = to * (1f + pulseAmount * 1.5f * Mathf.Sin(timer * Mathf.PI * 2f * pulseHz * 1.6f));
                reveal = revealTo;
                break;
            case Phase.Complete:
            {
                float k = Mathf.Clamp01(timer / Mathf.Max(0.01f, completeSeconds));
                scale = Mathf.Lerp(holdScale, 1f, Mathf.SmoothStep(0f, 1f, k)) + completeOvershoot * Mathf.Sin(Mathf.PI * k);
                reveal = Mathf.Lerp(revealFrom, 1f, k);
                extraGlow = completeGlow * (1f - k);
                if (k >= 1f) { phase = Phase.Idle; idleTime = 0f; SetFront(false); }
                break;
            }
            default:
                idleTime += dt;
                scale = 1f + pulseAmount * Mathf.Sin(idleTime * Mathf.PI * 2f * pulseHz);
                break;
        }
        Apply(scale, reveal, 1f + extraGlow);
    }

    private void Apply(float scale, float reveal, float glowBoost)
    {
        CurrentScale = scale; CurrentReveal = reveal;
        if (body != null) body.localScale = Vector3.one * (bodyScale * Mathf.Max(0.001f, scale));
        if (block == null) return;
        foreach (var r in surfaces)
        {
            if (r == null) continue;
            r.GetPropertyBlock(block);
            block.SetFloat(FlowTimeId, flowTime);
            block.SetFloat(RevealId, reveal);
            block.SetFloat(GlowId, glow * glowBoost);
            r.SetPropertyBlock(block);
        }
    }
}
