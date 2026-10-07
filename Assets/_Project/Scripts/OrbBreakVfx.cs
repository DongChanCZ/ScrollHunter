using UnityEngine;

/// <summary>
/// 보스 오브 파괴 연출(10/7, 10 A43 연출). 오브와 분리된 독립 오브젝트라 오브가 Died 직후 꺼져도 끝까지 재생된다.
/// 수축(중심체·기운이 안쪽으로) → 방출(충격 고리·방사 궤적) → 잔상(입자가 흩어짐). 판정은 이미 끝난 뒤의 표시만 맡는다.
/// 입자의 수명 배율은 수축·방출·잔상 시간, 속도 배율은 그 역수라 시간을 바꿔도 퍼지는 거리는 같다(프리팹 값은 거리·수명 비율).
/// 게임 시간이라 정지·배속을 따르고, 전투 종료 뒤에는 BossOrbs가 실제 시간으로 마저 재생시킨다.
/// </summary>
public sealed class OrbBreakVfx : MonoBehaviour
{
    [Header("시간 (초)")]
    [SerializeField] private float contractSeconds = 0.14f;
    [SerializeField] private float burstSeconds = 0.35f;
    [SerializeField] private float lingerSeconds = 0.9f;

    [Header("수축")]
    [SerializeField] private Transform core;
    [Tooltip("수축 끝 크기(처음 1)")]
    [SerializeField] private float contractTo = 0.3f;
    [SerializeField] private float contractGlow = 1.6f;
    [SerializeField] private Renderer[] surfaces = new Renderer[0];

    [Header("입자 묶음")]
    [SerializeField] private ParticleSystem[] contract = new ParticleSystem[0];
    [SerializeField] private ParticleSystem[] burst = new ParticleSystem[0];
    [SerializeField] private ParticleSystem[] linger = new ParticleSystem[0];

    private float timer;
    private bool released, unscaled, begun;
    private MaterialPropertyBlock block;
    private static readonly int GlowId = Shader.PropertyToID("_Glow");
    private static readonly int FlowTimeId = Shader.PropertyToID("_FlowTime");

    public float Duration => contractSeconds + Mathf.Max(burstSeconds, lingerSeconds);
    public bool Released => released;
    public float Elapsed => timer;

    /// <summary>오브의 현재 보이는 크기(월드 배율)에서 시작한다.</summary>
    public void Begin(float worldScale)
    {
        begun = true;
        transform.localScale = Vector3.one * Mathf.Max(0.01f, worldScale);
        Configure(contract, contractSeconds);
        Configure(burst, burstSeconds);
        Configure(linger, lingerSeconds);
        foreach (var ps in contract) if (ps != null) ps.Play(true);
        Apply(1f, 1f);
    }

    /// <summary>전투 종료(timeScale 0) 뒤 이미 시작한 파괴 연출을 실제 시간으로 마친다.</summary>
    public void PlayOutUnscaled()
    {
        unscaled = true;
        FinishingVfx.PlayUnscaled(gameObject);
    }

    private static void Configure(ParticleSystem[] group, float seconds)
    {
        float s = Mathf.Max(0.02f, seconds);
        foreach (var ps in group)
        {
            if (ps == null) continue;
            var main = ps.main;
            main.startLifetime = Scale(main.startLifetime, s);   // 두 상수 모드는 Multiplier가 최대값만 바꾸므로 값을 직접 곱한다
            main.startSpeed = Scale(main.startSpeed, 1f / s);
        }
    }

    private static ParticleSystem.MinMaxCurve Scale(ParticleSystem.MinMaxCurve c, float f)
    {
        if (c.mode == ParticleSystemCurveMode.TwoConstants) { c.constantMin *= f; c.constantMax *= f; }
        else if (c.mode == ParticleSystemCurveMode.Constant) c.constant *= f;
        else c.curveMultiplier *= f;
        return c;
    }

    private void Update()
    {
        if (!begun) return;
        timer += unscaled ? Time.unscaledDeltaTime : Time.deltaTime;
        if (!released)
        {
            float k = Mathf.Clamp01(timer / Mathf.Max(0.01f, contractSeconds));
            Apply(Mathf.Lerp(1f, contractTo, k * k), 1f + contractGlow * k);
            if (k >= 1f)
            {
                released = true;
                if (core != null) core.gameObject.SetActive(false);
                foreach (var ps in burst) if (ps != null) ps.Play(true);
                foreach (var ps in linger) if (ps != null) ps.Play(true);
            }
        }
        else if (timer >= Duration && !AnyAlive()) Destroy(gameObject);
    }

    private bool AnyAlive()
    {
        foreach (var ps in GetComponentsInChildren<ParticleSystem>()) if (ps.IsAlive(true)) return true;
        return false;
    }

    private void Apply(float coreScale, float glow)
    {
        if (core != null) core.localScale = Vector3.one * coreScale;
        if (block == null) block = new MaterialPropertyBlock();
        foreach (var r in surfaces)
        {
            if (r == null) continue;
            r.GetPropertyBlock(block);
            block.SetFloat(GlowId, glow);
            block.SetFloat(FlowTimeId, timer * 3f);
            r.SetPropertyBlock(block);
        }
    }
}
