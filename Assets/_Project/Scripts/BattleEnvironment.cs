using UnityEngine;

/// <summary>전투 진행을 읽어 배경만 표시한다. 전투 판정·조명·카메라 설정은 바꾸지 않는다.</summary>
public sealed class BattleEnvironment : MonoBehaviour
{
    [SerializeField] BattleFlow flow;
    [SerializeField] Enemy boss;
    [SerializeField] Camera view;
    [SerializeField] GameObject[] stages;
    [SerializeField] Transform[] backplates;
    [SerializeField] Transform magicCircle;
    [SerializeField] float[] circleDiameters = { 8f, 10.5f, 13f };
    [SerializeField, Min(.01f)] float circleGrowthSeconds = 2f;
    [SerializeField, Min(0f)] float circleDegreesPerSecond = 8f;
    [SerializeField, Min(.1f)] float circleGlowPeriod = 3f;
    [SerializeField, ColorUsage(true, true)] Color circleGlow = new Color(1.25f, .5f, 2f, 1f);
    [SerializeField] Renderer phaseWave;
    [SerializeField] Renderer[] phaseGlows = new Renderer[0];
    [SerializeField] Renderer[] phaseCracks = new Renderer[0];
    [SerializeField] ParticleSystem phaseEmbers;
    MaterialPropertyBlock ambienceProperties;
    float ambience, ambienceFrom, ambienceTo, cracks, cracksFrom, cracksTo;
    // 기존 씬 연결명은 유지하되, phaseCracks에는 3페이즈 전류의 중심선/잔광을 연결한다.
    Vector3[][] currentPaths;
    float currentTime, fogTime;
    Vector3[] glowOrigins;
    MaterialPropertyBlock waveProperties;
    Renderer circleRenderer;
    MaterialPropertyBlock circleProperties;
    Quaternion circleRotation;
    Player player;
    float circleAngle, glowTime;
    int shownStage = -1;
    int shownPhase;
    float circleFrom, circleTo, circleElapsed;
    float lastAspect, lastFov;
    const float BackplateDistance = 30f;
    const float ImageAspect = 16f / 9f;

    public int ShownStage => shownStage;
    public int ShownPhase => shownPhase;

    void Awake()
    {
        if (!flow) flow = FindFirstObjectByType<BattleFlow>();
        if (!view) view = Camera.main;
        if (!boss)
            foreach (var enemy in FindObjectsByType<Enemy>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (enemy.HasPhases) { boss = enemy; break; }
        circleRotation = magicCircle.localRotation;
        circleRenderer = magicCircle.GetComponent<Renderer>();
        circleProperties = new MaterialPropertyBlock();
        waveProperties = new MaterialPropertyBlock();
        player = FindFirstObjectByType<Player>();
        Refresh(0f);
    }

    void OnEnable() { if (player) player.BattleReset += ResetCircleMotion; }
    void OnDisable()
    {
        if (player) player.BattleReset -= ResetCircleMotion;
        if (magicCircle) magicCircle.localRotation = circleRotation;
        if (circleRenderer) circleRenderer.SetPropertyBlock(null);
        foreach (var r in phaseGlows) if (r) r.enabled = false;
        foreach (var r in phaseCracks) if (r) r.enabled = false;
        if (phaseEmbers) phaseEmbers.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (phaseWave) phaseWave.enabled = false;
    }
    void ResetCircleMotion()
    {
        circleAngle = glowTime = 0f;
        ambience = cracks = currentTime = fogTime = 0f;
        if (phaseEmbers) phaseEmbers.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (phaseWave) phaseWave.enabled = false;
        if (magicCircle) magicCircle.localRotation = circleRotation;
    }

    void LateUpdate() => Refresh(Time.deltaTime);

    // ponytail: 고정 카메라 전용 2.5D 배경. 이동 카메라를 도입하면 실제 지형으로 교체한다.
    public void Refresh(float deltaTime)
    {
        int battle = flow ? flow.BattleNumber : 0;
        int stage = battle <= 0 ? 0 : battle == 1 ? 1 : battle <= 3 ? 2 : 3;
        bool changed = stage != shownStage;
        if (changed)
        {
            for (int i = 0; i < stages.Length; i++) stages[i].SetActive(i == stage);
            shownStage = stage;
            ResetCircleMotion();
        }
        if (view && (lastAspect != view.aspect || lastFov != view.fieldOfView))
        {
            float height = 2f * BackplateDistance * Mathf.Tan(view.fieldOfView * .5f * Mathf.Deg2Rad);
            float cover = Mathf.Max(1f, view.aspect / ImageAspect);
            foreach (var plate in backplates)
                plate.localScale = new Vector3(height * ImageAspect * cover * 1.04f, height * cover * 1.04f, 1f);
            lastAspect = view.aspect; lastFov = view.fieldOfView;
        }
        int phase = stage == 3 && boss ? Mathf.Clamp(boss.PhaseNumber, 1, 3) : 1;
        if (changed || phase != shownPhase)
        {
            ambienceFrom = ambience; cracksFrom = cracks;
            ambienceTo = phase == 1 ? 0f : phase == 2 ? 1f : 1.65f;
            cracksTo = phase == 3 ? 1f : 0f;
            currentTime = 0f;
            if (changed || phase < shownPhase) { ambienceFrom = ambienceTo; cracksFrom = cracksTo; }
            circleFrom = magicCircle.localScale.x;
            circleTo = circleDiameters[phase - 1];
            circleElapsed = 0f;
            // 새 런·재시작은 즉시 초기 크기로 복원한다.
            if (changed || phase < shownPhase) circleFrom = circleTo;
            shownPhase = phase;
        }
        circleElapsed += Mathf.Max(0f, deltaTime);
        float diameter = Mathf.Lerp(circleFrom, circleTo, Mathf.SmoothStep(0f, 1f, circleElapsed / circleGrowthSeconds));
        magicCircle.localScale = new Vector3(diameter, diameter, 1f);
        bool transitioning = stage == 3 && boss && boss.IsAlive && (!player || player.IsAlive) && boss.IsPhaseTransitioning;
        float transition = transitioning ? Mathf.Clamp01(1f - boss.PhaseTransitionRemaining / Mathf.Max(.01f, boss.Data.PhaseTransitionSeconds)) : 0f;
        float pulse = Mathf.Sin(transition * Mathf.PI);
        if (phaseWave)
        {
            phaseWave.enabled = transitioning;
            if (transitioning)
            {
                float waveDiameter = circleTo * Mathf.Lerp(.25f, 1.45f, transition);
                phaseWave.transform.localScale = new Vector3(waveDiameter, waveDiameter, 1f);
                waveProperties.SetColor("_BaseColor", new Color(1.15f, .3f, 1.8f, pulse * .65f));
                phaseWave.SetPropertyBlock(waveProperties);
            }
        }
        if (stage == 3)
        {
            float dt = Mathf.Max(0f, deltaTime);
            fogTime += dt;
            circleAngle = Mathf.Repeat(circleAngle + dt * circleDegreesPerSecond, 360f);
            glowTime = Mathf.Repeat(glowTime + dt, circleGlowPeriod);
            // 카메라를 향한 평면의 로컬 -Z 회전은 화면에서 시계방향.
            magicCircle.localRotation = circleRotation * Quaternion.Euler(0, 0, -circleAngle);
            Color glow = circleGlow * (1f + .15f * Mathf.Sin(glowTime / circleGlowPeriod * Mathf.PI * 2f));
            glow *= 1f + pulse * 1.4f;
            glow.a = circleGlow.a;
            circleProperties.SetColor("_BaseColor", glow);
            circleRenderer.SetPropertyBlock(circleProperties);
        }
        if (stage == 3 && phase == 3) currentTime += Mathf.Max(0f, deltaTime);
        RefreshPhaseAmbience(stage == 3, Mathf.SmoothStep(0f, 1f, circleElapsed / circleGrowthSeconds));
    }

    void RefreshPhaseAmbience(bool visible, float blend)
    {
        ambience = Mathf.Lerp(ambienceFrom, ambienceTo, blend);
        cracks = Mathf.Lerp(cracksFrom, cracksTo, blend);
        if (ambienceProperties == null) ambienceProperties = new MaterialPropertyBlock();
        if (glowOrigins == null)
        {
            glowOrigins = new Vector3[phaseGlows.Length];
            for (int i = 0; i < phaseGlows.Length; i++)
                if (phaseGlows[i]) glowOrigins[i] = phaseGlows[i].transform.position;
        }
        for (int i = 0; i < phaseGlows.Length; i++)
        {
            var glow = phaseGlows[i];
            if (!glow) continue;
            glow.enabled = visible && ambience > 0f;
            // 배경 안개만 천천히 흐른다. 적·오브 앞에는 배치하지 않는다.
            glow.transform.position = glowOrigins[i] + view.transform.right * (Mathf.Sin(fogTime * .19f + i * 2f) * .4f)
                + (i == 2 ? Vector3.forward : view.transform.up) * (Mathf.Sin(fogTime * .13f + i) * .18f);
            ambienceProperties.SetColor("_BaseColor", new Color(.65f, .15f, 1.1f, .48f * ambience));
            glow.SetPropertyBlock(ambienceProperties);
        }
        RefreshCurrents(visible && cracks > 0f);
        if (!phaseEmbers) return;
        if (visible && cracks > 0f)
        {
            var emission = phaseEmbers.emission;
            emission.rateOverTime = 6f * cracks;
            if (!phaseEmbers.isPlaying) phaseEmbers.Play();
        }
        else if (phaseEmbers.isPlaying || phaseEmbers.particleCount > 0)
            phaseEmbers.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }
    void RefreshCurrents(bool visible)
    {
        if (currentPaths == null)
        {
            currentPaths = new Vector3[phaseCracks.Length][];
            for (int i = 0; i < phaseCracks.Length; i++)
            {
                var line = phaseCracks[i] as LineRenderer;
                if (!line) continue;
                currentPaths[i] = new Vector3[line.positionCount];
                line.GetPositions(currentPaths[i]);
            }
        }
        for (int i = 0; i < phaseCracks.Length; i++)
        {
            var line = phaseCracks[i] as LineRenderer;
            if (!line || currentPaths[i] == null) continue;
            int arc = i / 2; bool glow = i % 2 == 1;
            float clock = currentTime + arc * .51f;
            float period = 2.1f + arc * .23f;
            float age = Mathf.Repeat(clock, period);
            line.enabled = visible && age < .36f;
            if (!line.enabled) continue;
            float alpha = Mathf.Sin(age / .36f * Mathf.PI) * cracks;
            ambienceProperties.SetColor("_BaseColor", glow
                ? new Color(.65f, .16f, 1.5f, alpha * .26f)
                : new Color(1.15f, .55f, 2f, alpha * .9f));
            line.SetPropertyBlock(ambienceProperties);
            var path = currentPaths[i];
            float end = Mathf.Clamp01(age / .11f) * (path.Length - 1);
            int count = Mathf.Min(path.Length, Mathf.CeilToInt(end) + 1);
            line.positionCount = Mathf.Max(2, count);
            float seed = Mathf.Floor(clock / period) * 9.7f + arc * 13.1f + Mathf.Floor(age * 24f) * 3.3f;
            // 확대 중에도 시작점은 마법진 테두리에 붙인다. 끝점과 중앙 전투 영역은 고정.
            var originShift = (path[0] - magicCircle.position) * (magicCircle.localScale.x / circleDiameters[2] - 1f);
            for (int j = 0; j < line.positionCount; j++)
            {
                float at = Mathf.Min(j, end); int k = Mathf.Min(Mathf.FloorToInt(at), path.Length - 2);
                var p = Vector3.Lerp(path[k], path[k + 1], at - k) + originShift * Mathf.Clamp01(1f - at / 6f);
                float jitter = Mathf.Sin(at * Mathf.PI / (path.Length - 1));
                p += (view.transform.right * Mathf.Sin(at * 17.3f + seed)
                    + view.transform.up * Mathf.Sin(at * 11.9f - seed)) * (.13f * jitter);
                line.SetPosition(j, p);
            }
        }
    }

}
