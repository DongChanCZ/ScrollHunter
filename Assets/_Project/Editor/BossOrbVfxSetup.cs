using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 보스 오브 외형·소환·파괴 연출 만들기(10/7, 10 A43 연출). 메뉴 Tools > Scroll Hunter > Apply Boss Orb VFX.
/// 루미너스의 빛·어둠 오브를 시각 참고로만 삼아 직접 만든 구체 셰이더·흐름 무늬 텍스처와 기존 반입 Kenney Particle Pack(CC0) 텍스처로 구성한다.
/// 재질은 전부 VFX/BossOrb의 새 사본이라 다른 스킬·적 연출 재질은 바뀌지 않는다. 결과 프리팹은 오브 EnemyData(Enemy_Orb_Ruin/Cycle)에만 연결한다.
/// 크기 단위: 오브 루트(판정 구체 지름 1, 실행 때 activeSize 배율) 기준. 다시 실행하면 같은 결과로 덮어쓴다(Inspector 조정값도 이 값으로 돌아감).
/// </summary>
public static class BossOrbVfxSetup
{
    const string Root = "Assets/_Project/VFX/BossOrb/";
    const string Tex = "Assets/_Project/VFX/Textures/";
    const string NoisePath = Root + "OrbFlowNoise.png";

    static Material glowAdd, glowAlpha, ringAdd, ringAlpha, smokeAlpha, twirlAdd, twirlAlpha, traceAdd, flareAdd, swirlQuad, ringQuad;
    static Mesh sphere, quad;

    class Palette
    {
        public string name;
        public Color center, shadow, lit, rim, flow, reveal, shellRim, haloBack, haloAdd, aura0, aura1, accent, accentDeep, spark, burstRing, burstDark;
        public float rimPower, rimIntensity, flowIntensity, lightPower;
        public float haloBackAlpha, auraAlpha;
        public bool dark;
    }

    // 파멸: 검정·짙은 보라 중심, 보라 가장자리, 흐르는 어둠의 기운
    static readonly Palette Ruin = new Palette
    {
        name = "Ruin", dark = true,
        shadow = new Color(0.006f, 0f, 0.014f), lit = new Color(0.08f, 0.016f, 0.15f), lightPower = 1.6f, center = new Color(0f, 0f, 0.01f, 0.75f),
        rim = new Color(0.6f, 0.2f, 1f), rimPower = 3.2f, rimIntensity = 1.15f,
        flow = new Color(0.42f, 0.09f, 0.78f), flowIntensity = 0.75f, reveal = new Color(0.26f, 0.06f, 0.5f),
        shellRim = new Color(0.55f, 0.18f, 1f),
        haloBack = new Color(0.1f, 0.01f, 0.18f), haloBackAlpha = 0.6f, haloAdd = new Color(0.42f, 0.1f, 0.78f),
        aura0 = new Color(0.45f, 0.14f, 0.7f), aura1 = new Color(0.16f, 0.03f, 0.28f), auraAlpha = 0.72f,
        accent = new Color(0.68f, 0.26f, 1f), accentDeep = new Color(0.3f, 0.06f, 0.55f), spark = new Color(0.82f, 0.55f, 1f),
        burstRing = new Color(0.62f, 0.2f, 1f), burstDark = new Color(0.05f, 0f, 0.09f),
    };

    // 순환: 청백색 중심, 옅은 하늘색 가장자리, 회전하는 빛의 기운(흰색으로 뭉개지지 않게 푸른 명암 유지)
    static readonly Palette Cycle = new Palette
    {
        name = "Cycle", dark = false,
        shadow = new Color(0.03f, 0.1f, 0.38f), lit = new Color(0.27f, 0.52f, 0.97f), lightPower = 1.25f, center = new Color(0.75f, 0.88f, 1f, 0.22f),
        rim = new Color(0.5f, 0.82f, 1f), rimPower = 2.6f, rimIntensity = 0.85f,
        flow = new Color(0.72f, 0.92f, 1f), flowIntensity = 0.8f, reveal = new Color(0.55f, 0.82f, 1f),
        shellRim = new Color(0.45f, 0.8f, 1f),
        haloBack = new Color(0.06f, 0.18f, 0.45f), haloBackAlpha = 0.35f, haloAdd = new Color(0.4f, 0.72f, 1f),
        aura0 = new Color(0.72f, 0.92f, 1f), aura1 = new Color(0.35f, 0.65f, 1f), auraAlpha = 0.55f,
        accent = new Color(0.6f, 0.9f, 1f), accentDeep = new Color(0.22f, 0.5f, 0.95f), spark = new Color(0.9f, 0.98f, 1f),
        burstRing = new Color(0.6f, 0.9f, 1f), burstDark = new Color(0.08f, 0.22f, 0.5f),
    };

    [MenuItem("Tools/Scroll Hunter/Apply Boss Orb VFX")]
    public static void Apply()
    {
        if (!AssetDatabase.IsValidFolder("Assets/_Project/VFX/BossOrb")) AssetDatabase.CreateFolder("Assets/_Project/VFX", "BossOrb");
        var noise = MakeNoise();
        var add = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/VFX/Materials/VFX_spark_01.mat");
        var alpha = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/VFX/Materials/VFX_spark_01_alpha.mat");
        glowAdd = Mat("ORB_Glow_Add", add, Tex + "circle_05.png", false);
        glowAlpha = Mat("ORB_Glow_Alpha", alpha, Tex + "circle_05.png", false);
        ringAdd = Mat("ORB_Ring_Add", add, Tex + "circle_02.png", false);
        ringAlpha = Mat("ORB_Ring_Alpha", alpha, Tex + "circle_02.png", false);
        smokeAlpha = Mat("ORB_Smoke_Alpha", alpha, Tex + "smoke_04.png", true);   // 구체와 겹치는 경계가 딱 잘리지 않게 부드러운 입자
        twirlAdd = Mat("ORB_Twirl_Add", add, Tex + "twirl_02.png", true);
        twirlAlpha = Mat("ORB_Twirl_Alpha", alpha, Tex + "twirl_02.png", true);
        traceAdd = Mat("ORB_Trace_Add", add, Tex + "trace_06.png", false);
        flareAdd = Mat("ORB_Flare_Add", add, Tex + "flare_01.png", false);
        swirlQuad = Mat("ORB_SwirlQuad_Add", add, Tex + "twirl_02.png", false); swirlQuad.SetFloat("_Cull", 0f);   // 기울어진 회전 고리는 양면
        ringQuad = Mat("ORB_RingQuad_Add", add, Tex + "circle_02.png", false); ringQuad.SetFloat("_Cull", 0f);

        var probe = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere = probe.GetComponent<MeshFilter>().sharedMesh;
        Object.DestroyImmediate(probe);
        probe = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad = probe.GetComponent<MeshFilter>().sharedMesh;
        Object.DestroyImmediate(probe);

        var shader = AssetDatabase.LoadAssetAtPath<Shader>(Root + "BossOrbSurface.shader");
        var created = new System.Collections.Generic.List<Material>();
        foreach (var p in new[] { Ruin, Cycle })
        {
            var core = Surface("ORB_" + p.name + "_Core", shader, noise, p, false); created.Add(core);
            var shell = Surface("ORB_" + p.name + "_Shell", shader, noise, p, true); created.Add(shell);
            var visual = Visual(p, core, shell);
            var brk = Break(p, core, shell);
            string dataPath = "Assets/_Project/Data/ScrollHunter/EnemyData/Enemy_Orb_" + p.name + ".asset";
            var data = AssetDatabase.LoadAssetAtPath<EnemyData>(dataPath);
            var so = new SerializedObject(data);
            so.FindProperty("orbVisual").objectReferenceValue = visual;
            so.FindProperty("orbBreak").objectReferenceValue = brk;
            so.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssetIfDirty(data);
        }
        // SaveAssets는 무관한 TMP 동적 폰트까지 다시 쓰므로 만든 재질만 저장한다(프리팹은 바로 저장됨).
        foreach (var m in new[] { glowAdd, glowAlpha, ringAdd, ringAlpha, smokeAlpha, twirlAdd, twirlAlpha, traceAdd, flareAdd, swirlQuad, ringQuad }) AssetDatabase.SaveAssetIfDirty(m);
        foreach (var m in created) AssetDatabase.SaveAssetIfDirty(m);
        Debug.Log("[보스 오브 연출] 파멸·순환 외형·파괴 연출 적용");
    }

    // ───────── 외형(소환·대기) ─────────

    static GameObject Visual(Palette p, Material coreMat, Material shellMat)
    {
        var root = new GameObject("VFX_Orb_" + p.name);
        var body = Child(root.transform, "Body");
        var core = MeshChild(body, "Core", coreMat, 0.92f);
        var shell = MeshChild(body, "Shell", shellMat, 1.06f);

        // 뒤 후광: 밝은 하늘·바닥에서도 외곽이 묻히지 않게 짙은 바탕 + 가장자리 발광
        var back = Make(body, "HaloBack", glowAlpha, 0, true, true); Life(back, 1.2f, 1.2f); Size(back, 1.55f, 1.65f); Rate(back, 3f);
        FadeKeys(back, new[] { p.haloBack, p.haloBack }, new[] { (0f, 0f), (0.35f, p.haloBackAlpha), (0.7f, p.haloBackAlpha), (1f, 0f) });
        var glow = Make(body, "HaloGlow", glowAdd, 1, true, true); Life(glow, 1.1f, 1.1f); Size(glow, 1.25f, 1.32f); Rate(glow, 3f);
        FadeKeys(glow, new[] { p.haloAdd, p.haloAdd }, new[] { (0f, 0f), (0.4f, p.dark ? 0.32f : 0.22f), (1f, 0f) });

        if (p.dark)
        {
            // 흐르는 어둠: 구체 표면을 따라 돌며 앞뒤로 지나가는 연기(뒤쪽은 중심체에 가려 입체감)
            var wisps = Make(body, "DarkWisps", smokeAlpha, 2, true, true); Life(wisps, 1.2f, 1.5f); Size(wisps, 0.28f, 0.4f); Rate(wisps, 12f);
            Sphere(wisps, 0.47f, 0f); Velocity(wisps, 0f, 0.05f, 0f, 1.3f, 0f); Spin(wisps, -50f, 50f); SizeEnd(wisps, 1.2f);
            FadeKeys(wisps, new[] { p.aura0, p.aura1 }, new[] { (0f, 0f), (0.25f, p.auraAlpha), (0.75f, p.auraAlpha * 0.8f), (1f, 0f) });
            var tendril = Make(body, "PurpleTendrils", twirlAdd, 3, true, true); Life(tendril, 1.3f, 1.5f); Size(tendril, 1.4f, 1.55f); Rate(tendril, 2.6f); Spin(tendril, 80f, 140f);
            FadeKeys(tendril, new[] { p.accent, p.accentDeep }, new[] { (0f, 0f), (0.35f, 0.85f), (1f, 0f) });
            var shade = Make(body, "ShadowTendrils", twirlAlpha, 2, true, true); Life(shade, 1.5f, 1.7f); Size(shade, 1.5f, 1.6f); Rate(shade, 1.6f); Spin(shade, -100f, -60f);
            FadeKeys(shade, new[] { p.burstDark, p.aura1 }, new[] { (0f, 0f), (0.4f, 0.55f), (1f, 0f) });
        }
        else
        {
            // 회전하는 빛: 기울어진 두 평면에서 도는 빛줄기(구체 뒤로 돌아가는 부분은 가려져 입체감)
            var tiltA = Child(body, "SwirlTiltA"); tiltA.localRotation = Quaternion.Euler(72f, 0f, 18f);
            var swA = Make(tiltA, "LightSwirl", swirlQuad, 3, true, true); Life(swA, 2.2f, 2.2f); Size(swA, 1.6f, 1.65f); Rate(swA, 1.4f); MeshLocal(swA); Spin(swA, 120f, 160f);
            FadeKeys(swA, new[] { p.aura0, p.accent }, new[] { (0f, 0f), (0.3f, 0.95f), (0.7f, 0.95f), (1f, 0f) });
            var tiltB = Child(body, "SwirlTiltB"); tiltB.localRotation = Quaternion.Euler(64f, 0f, -38f);
            var swB = Make(tiltB, "LightSwirl", swirlQuad, 3, true, true); Life(swB, 2.6f, 2.6f); Size(swB, 1.45f, 1.5f); Rate(swB, 1.1f); MeshLocal(swB); Spin(swB, -130f, -100f);
            FadeKeys(swB, new[] { p.accent, p.accentDeep }, new[] { (0f, 0f), (0.3f, 0.8f), (0.7f, 0.8f), (1f, 0f) });
            var orbitRing = Make(tiltA, "OrbitRing", ringQuad, 2, true, true); Life(orbitRing, 2f, 2f); Size(orbitRing, 1.62f, 1.62f); Rate(orbitRing, 1f); MeshLocal(orbitRing);
            FadeKeys(orbitRing, new[] { p.accentDeep, p.accent }, new[] { (0f, 0f), (0.5f, 0.32f), (1f, 0f) });
            var stream = Make(body, "LightStreams", twirlAdd, 3, true, true); Life(stream, 1.2f, 1.4f); Size(stream, 1.1f, 1.2f); Rate(stream, 2f); Spin(stream, 150f, 210f);
            FadeKeys(stream, new[] { p.aura0, p.aura1 }, new[] { (0f, 0f), (0.35f, 0.6f), (1f, 0f) });
        }
        var motes = Make(body, "Motes", p.dark ? glowAdd : flareAdd, 4, true, true); Life(motes, 0.9f, 1.2f); Size(motes, p.dark ? 0.035f : 0.12f, p.dark ? 0.055f : 0.18f); Rate(motes, 14f);
        Sphere(motes, 0.52f, 0f); Velocity(motes, 0f, 0.04f, 0f, 1.6f, 0f); FadeKeys(motes, new[] { p.spark, p.accent }, new[] { (0f, 0f), (0.3f, 1f), (1f, 0f) });

        // 소환: 바깥 기운·짧은 궤적이 중심으로 빨려 듦(몸통 배율과 무관하게 바깥에서 출발)
        var gather = Make(root.transform, "GatherTraces", traceAdd, 4, true, true); Life(gather, 0.55f, 0.55f); Size(gather, 0.09f, 0.13f); Rate(gather, 42f);
        Sphere(gather, 1.25f, 0f, new Vector3(1f, 0.75f, 1f)); Velocity(gather, 0f, 0f, 0f, 0f, -2f); Stretch(gather, 0.2f); NoAwake(gather);
        FadeKeys(gather, new[] { p.spark, p.accent }, new[] { (0f, 0f), (0.3f, 1f), (1f, 0.4f) });
        var gatherAura = Make(root.transform, "GatherAura", p.dark ? smokeAlpha : glowAdd, p.dark ? 2 : 3, true, true); Life(gatherAura, 0.8f, 0.8f);
        Size(gatherAura, p.dark ? 0.4f : 0.22f, p.dark ? 0.5f : 0.3f); Rate(gatherAura, p.dark ? 14f : 18f);
        Sphere(gatherAura, 1.05f, 0f, new Vector3(1f, 0.75f, 1f)); Velocity(gatherAura, 0f, 0f, 0f, 0.8f, -1.15f); SizeEnd(gatherAura, 0.35f); NoAwake(gatherAura);
        FadeKeys(gatherAura, new[] { p.dark ? p.aura0 : p.aura1, p.dark ? p.burstDark : p.accent }, new[] { (0f, 0f), (0.35f, p.dark ? 0.8f : 0.55f), (1f, 0.2f) });

        // 완성 맥동: 짧은 고리·섬광
        var pulse = BurstRoot(root.transform, "CompletePulse");
        var ring = Make(pulse.transform, "Ring", ringAdd, 4, false, true); Life(ring, 0.35f, 0.35f); Size(ring, 0.9f, 0.9f); SizeEnd(ring, 2.3f); Burst(ring, 1);
        FadeKeys(ring, new[] { p.accent, p.accentDeep }, new[] { (0f, 0.9f), (1f, 0f) });
        var flash = Make(pulse.transform, "Flash", glowAdd, 4, false, true); Life(flash, 0.22f, 0.22f); Size(flash, 1.3f, 1.3f); SizeEnd(flash, 0.7f); Burst(flash, 1);
        FadeKeys(flash, new[] { p.accent, p.accentDeep }, new[] { (0f, p.dark ? 0.7f : 0.45f), (1f, 0f) });

        var vis = root.AddComponent<BossOrbVisual>();
        var so = new SerializedObject(vis);
        so.FindProperty("body").objectReferenceValue = body;
        var surfaces = so.FindProperty("surfaces"); surfaces.arraySize = 2;
        surfaces.GetArrayElementAtIndex(0).objectReferenceValue = core.GetComponent<Renderer>();
        surfaces.GetArrayElementAtIndex(1).objectReferenceValue = shell.GetComponent<Renderer>();
        var g = so.FindProperty("gather"); g.arraySize = 2;
        g.GetArrayElementAtIndex(0).objectReferenceValue = gather;
        g.GetArrayElementAtIndex(1).objectReferenceValue = gatherAura;
        so.FindProperty("completeBurst").objectReferenceValue = pulse.GetComponent<ParticleSystem>();
        so.FindProperty("spinners").arraySize = 0;   // 회전은 입자 자체 회전으로 충분. 필요하면 Inspector에서 대상·속도 추가
        so.ApplyModifiedPropertiesWithoutUndo();
        return Save(root, "VFX_Orb_" + p.name);
    }

    // ───────── 파괴 ─────────

    /// <summary>수명·속도는 1초 기준 비율(OrbBreakVfx가 수축·방출·잔상 시간을 곱함). 속도 값 = 1초 동안 가는 거리.</summary>
    static GameObject Break(Palette p, Material coreMat, Material shellMat)
    {
        var root = new GameObject("VFX_Orb_" + p.name + "Break");
        var coreRoot = Child(root.transform, "Core");
        var core = MeshChild(coreRoot, "Core", coreMat, 0.92f);
        var shell = MeshChild(coreRoot, "Shell", shellMat, 1.06f);

        // 수축: 바깥 기운이 안으로 빨려 듦
        var pull = Make(root.transform, "ContractPull", traceAdd, 4, false, true); Life(pull, 1f, 1f); Size(pull, 0.06f, 0.08f); Speed(pull, -0.75f, -0.75f); Burst(pull, 16);
        Sphere(pull, 0.85f, 0f); Stretch(pull, 0.05f); NoAwake(pull);
        FadeKeys(pull, new[] { p.spark, p.accent }, new[] { (0f, 0.3f), (0.6f, 1f), (1f, 0.8f) });
        var squeeze = Make(root.transform, "ContractRing", ringAdd, 4, false, true); Life(squeeze, 1f, 1f); Size(squeeze, 1.5f, 1.5f); SizeEnd(squeeze, 0.25f); Burst(squeeze, 1); NoAwake(squeeze);
        FadeKeys(squeeze, new[] { p.accent, p.spark }, new[] { (0f, 0.2f), (1f, 0.9f) });

        // 방출: 섬광·충격 고리 두 겹·방사 궤적·기운 덩어리
        var flash = Make(root.transform, "Flash", glowAdd, 5, false, true); Life(flash, 0.5f, 0.5f); Size(flash, 1.5f, 1.5f); SizeEnd(flash, 0.3f); Burst(flash, 1); NoAwake(flash);
        FadeKeys(flash, new[] { p.dark ? p.accent : p.aura1, p.accentDeep }, new[] { (0f, p.dark ? 0.8f : 0.6f), (1f, 0f) });
        var shock = Make(root.transform, "Shock", ringAdd, 4, false, true); Life(shock, 1f, 1f); Size(shock, 0.5f, 0.5f); SizeEnd(shock, 4.2f); Burst(shock, 1); NoAwake(shock);
        FadeKeys(shock, new[] { p.burstRing, p.accentDeep }, new[] { (0f, 1f), (0.5f, 0.75f), (1f, 0f) });
        var shockDark = Make(root.transform, "ShockBack", ringAlpha, 3, false, true); Life(shockDark, 1f, 1f); Size(shockDark, 0.45f, 0.45f); SizeEnd(shockDark, 3.8f); Burst(shockDark, 1); NoAwake(shockDark);
        FadeKeys(shockDark, new[] { p.burstDark, p.burstDark }, new[] { (0f, 0.75f), (1f, 0f) });
        var rays = Make(root.transform, "Rays", traceAdd, 4, false, true); Life(rays, 0.65f, 1f); Size(rays, 0.09f, 0.12f); Speed(rays, 1.4f, 2f); Burst(rays, 18);
        Sphere(rays, 0.12f, 0f); Stretch(rays, 0.22f); NoAwake(rays);
        FadeKeys(rays, new[] { p.spark, p.accent }, new[] { (0f, 1f), (0.6f, 0.9f), (1f, 0f) });
        ParticleSystem puff;
        if (p.dark)
        {
            puff = Make(root.transform, "DarkPuff", smokeAlpha, 2, false, true); Life(puff, 0.7f, 1f); Size(puff, 0.32f, 0.42f); Speed(puff, 0.5f, 0.9f); Burst(puff, 8);
            Sphere(puff, 0.15f, 0f); SizeEnd(puff, 2.4f); Spin(puff, -90f, 90f); NoAwake(puff);
            FadeKeys(puff, new[] { p.aura0, p.aura1 }, new[] { (0f, 0.8f), (0.5f, 0.6f), (1f, 0f) });
        }
        else
        {
            puff = Make(root.transform, "LightPuff", twirlAdd, 3, false, true); Life(puff, 0.8f, 1f); Size(puff, 0.8f, 0.9f); SizeEnd(puff, 2.6f); Burst(puff, 3); Spin(puff, 150f, 240f); NoAwake(puff);
            FadeKeys(puff, new[] { p.aura0, p.aura1 }, new[] { (0f, 0.7f), (1f, 0f) });
        }

        // 잔상: 흩어지는 불티·옅은 기운
        var embers = Make(root.transform, "Embers", p.dark ? glowAdd : flareAdd, 4, false, true); Life(embers, 0.6f, 1f); Size(embers, p.dark ? 0.04f : 0.12f, p.dark ? 0.065f : 0.2f);
        Speed(embers, 0.5f, 1.1f); Burst(embers, 22); Sphere(embers, 0.3f, 0f); Damp(embers, 0.3f); NoAwake(embers);
        FadeKeys(embers, new[] { p.spark, p.accent }, new[] { (0f, 1f), (0.6f, 0.8f), (1f, 0f) });
        var drift = Make(root.transform, "Drift", p.dark ? smokeAlpha : glowAdd, p.dark ? 1 : 3, false, true); Life(drift, 0.7f, 1f); Size(drift, p.dark ? 0.4f : 0.18f, p.dark ? 0.55f : 0.26f);
        Speed(drift, 0.3f, 0.6f); Burst(drift, 8); Sphere(drift, 0.35f, 0f); SizeEnd(drift, 1.6f); Spin(drift, -40f, 40f); NoAwake(drift);
        FadeKeys(drift, new[] { p.dark ? p.aura0 : p.aura1, p.dark ? p.aura1 : p.accentDeep }, new[] { (0f, 0f), (0.25f, p.dark ? 0.6f : 0.45f), (1f, 0f) });

        var brk = root.AddComponent<OrbBreakVfx>();
        var so = new SerializedObject(brk);
        so.FindProperty("core").objectReferenceValue = coreRoot;
        Array(so, "surfaces", core.GetComponent<Renderer>(), shell.GetComponent<Renderer>());
        Array(so, "contract", pull, squeeze);
        Array(so, "burst", flash, shock, shockDark, rays, puff);
        Array(so, "linger", embers, drift);
        so.ApplyModifiedPropertiesWithoutUndo();
        return Save(root, "VFX_Orb_" + p.name + "Break");
    }

    static void Array(SerializedObject so, string name, params Object[] items)
    {
        var prop = so.FindProperty(name); prop.arraySize = items.Length;
        for (int i = 0; i < items.Length; i++) prop.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
    }

    // ───────── 재질·텍스처 ─────────

    static Material Surface(string name, Shader shader, Texture2D noise, Palette p, bool shell)
    {
        string path = Root + name + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, path); }
        mat.shader = shader;
        mat.SetColor("_ShadowColor", p.shadow); mat.SetColor("_LitColor", p.lit); mat.SetColor("_CenterColor", p.center); mat.SetFloat("_CenterPower", p.dark ? 2.2f : 3f); mat.SetFloat("_LightPower", p.lightPower);
        mat.SetVector("_LightDir", new Vector4(0.35f, 0.75f, -0.55f, 0f));   // 씬 방향광(위·오른쪽 앞)과 같은 쪽
        mat.SetColor("_RimColor", shell ? p.shellRim : p.rim);
        mat.SetFloat("_RimPower", shell ? 1.2f : p.rimPower);
        mat.SetFloat("_RimIntensity", shell ? p.rimIntensity * 0.55f : p.rimIntensity);
        mat.SetTexture("_FlowTex", noise); mat.SetColor("_FlowColor", p.flow); mat.SetFloat("_FlowIntensity", p.flowIntensity);
        mat.SetFloat("_FlowSharpness", p.dark ? 6f : 4f);
        mat.SetVector("_FlowSpeed", p.dark ? new Vector4(0.045f, 0.012f, -0.03f, 0.02f) : new Vector4(-0.06f, 0.008f, 0.04f, -0.015f));
        mat.SetVector("_FlowTiling", new Vector4(2f, 1f, 3.1f, 1.6f));
        mat.SetColor("_RevealColor", p.reveal);
        mat.SetFloat("_Shell", shell ? 1f : 0f); mat.SetFloat("_Glow", 1f); mat.SetFloat("_Reveal", 1f); mat.SetFloat("_Alpha", 1f);
        if (shell)
        {
            mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One); mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            mat.SetFloat("_ZWrite", 0f); mat.renderQueue = 3000;
        }
        else
        {
            mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One); mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.Zero);
            mat.SetFloat("_ZWrite", 1f); mat.renderQueue = 2000;
        }
        EditorUtility.SetDirty(mat);
        return mat;
    }

    /// <summary>바둑판처럼 이어지는 흐름 무늬(주기 격자 값 잡음 4옥타브). 구체 표면이 흐르며 이음매가 보이지 않는다.</summary>
    static Texture2D MakeNoise()
    {
        if (!File.Exists(NoisePath))
        {
            const int n = 256;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
            var rng = new System.Random(4307);
            int[] periods = { 4, 8, 16, 32 };
            float[] weights = { 0.5f, 0.27f, 0.15f, 0.08f };
            var grids = new float[periods.Length][];
            for (int o = 0; o < periods.Length; o++)
            {
                grids[o] = new float[periods[o] * periods[o]];
                for (int i = 0; i < grids[o].Length; i++) grids[o][i] = (float)rng.NextDouble();
            }
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float v = 0f;
                    for (int o = 0; o < periods.Length; o++)
                    {
                        int pd = periods[o];
                        float fx = x * pd / (float)n, fy = y * pd / (float)n;
                        int x0 = Mathf.FloorToInt(fx), y0 = Mathf.FloorToInt(fy);
                        float tx = fx - x0, ty = fy - y0;
                        tx = tx * tx * (3f - 2f * tx); ty = ty * ty * (3f - 2f * ty);
                        float a = grids[o][(y0 % pd) * pd + x0 % pd], b = grids[o][(y0 % pd) * pd + (x0 + 1) % pd];
                        float c = grids[o][((y0 + 1) % pd) * pd + x0 % pd], d = grids[o][((y0 + 1) % pd) * pd + (x0 + 1) % pd];
                        v += weights[o] * Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), ty);
                    }
                    tex.SetPixel(x, y, new Color(v, v, v, 1f));
                }
            File.WriteAllBytes(NoisePath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(NoisePath);
            var importer = (TextureImporter)AssetImporter.GetAtPath(NoisePath);
            importer.sRGBTexture = false; importer.wrapMode = TextureWrapMode.Repeat; importer.mipmapEnabled = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(NoisePath);
    }

    static Material Mat(string name, Material template, string texturePath, bool soft)
    {
        string path = Root + name + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null) { mat = new Material(template); AssetDatabase.CreateAsset(mat, path); }
        else mat.CopyPropertiesFromMaterial(template);
        mat.shaderKeywords = template.shaderKeywords;
        mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));
        mat.SetColor("_BaseColor", Color.white);
        if (soft)
        {
            mat.SetFloat("_SoftParticlesEnabled", 1f);
            mat.SetFloat("_SoftParticlesNearFadeDistance", 0f);
            mat.SetFloat("_SoftParticlesFarFadeDistance", 0.25f);
            mat.SetVector("_SoftParticleFadeParams", new Vector4(0f, 1f / 0.25f, 0f, 0f));
            mat.EnableKeyword("_SOFTPARTICLES_ON");
        }
        EditorUtility.SetDirty(mat);
        return mat;
    }

    // ───────── 도우미 ─────────

    static Transform Child(Transform parent, string name)
    {
        var t = new GameObject(name).transform;
        t.SetParent(parent, false);
        return t;
    }

    static GameObject MeshChild(Transform parent, string name, Material mat, float size)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localScale = Vector3.one * size;
        go.AddComponent<MeshFilter>().sharedMesh = sphere;
        var r = go.AddComponent<MeshRenderer>();
        r.sharedMaterial = mat;
        r.sortingOrder = mat.renderQueue >= 3000 ? 2 : 0;   // 가산 껍질은 짙은 뒤 후광(0)보다 나중에 그려 가장자리 빛이 덮이지 않게
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
        r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off; r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        return go;
    }

    static GameObject BurstRoot(Transform parent, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main; main.duration = 0.5f; main.loop = false; main.startLifetime = 0.01f; main.playOnAwake = false;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        var em = ps.emission; em.enabled = false;
        go.GetComponent<ParticleSystemRenderer>().enabled = false;
        return go;
    }

    static ParticleSystem Make(Transform parent, string name, Material mat, int order, bool loop, bool local)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop = loop; main.duration = loop ? 1f : 0.1f; main.playOnAwake = loop;   // 한 번 터지는 입자는 방출 기간을 짧게 두어 입자가 사라지면 곧 끝난 것으로 본다
        main.startSpeed = 0f; main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.simulationSpace = local ? ParticleSystemSimulationSpace.Local : ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.maxParticles = 200;
        if (loop) main.prewarm = true;
        var em = ps.emission; em.rateOverTime = 0f;
        var shape = ps.shape; shape.enabled = false;
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = mat; r.sortingOrder = order;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
        r.renderMode = ParticleSystemRenderMode.Billboard;
        return ps;
    }

    static void NoAwake(ParticleSystem ps) { var m = ps.main; m.playOnAwake = false; m.prewarm = false; }

    static void MeshLocal(ParticleSystem ps)
    {
        var r = ps.GetComponent<ParticleSystemRenderer>();
        r.renderMode = ParticleSystemRenderMode.Mesh; r.mesh = quad; r.alignment = ParticleSystemRenderSpace.Local;
        var m = ps.main; m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
    }

    static void Life(ParticleSystem ps, float a, float b) { var m = ps.main; m.startLifetime = new ParticleSystem.MinMaxCurve(a, b); }
    static void Size(ParticleSystem ps, float a, float b) { var m = ps.main; m.startSize = new ParticleSystem.MinMaxCurve(a, b); }
    static void Speed(ParticleSystem ps, float a, float b) { var m = ps.main; m.startSpeed = new ParticleSystem.MinMaxCurve(a, b); }
    static void Rate(ParticleSystem ps, float r) { var e = ps.emission; e.rateOverTime = r; }

    static void Burst(ParticleSystem ps, int count)
    {
        var e = ps.emission; e.rateOverTime = 0f; e.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });
        var m = ps.main; m.maxParticles = Mathf.Max(1, count);
    }

    static void Sphere(ParticleSystem ps, float radius, float thickness, Vector3? scale = null)
    {
        var s = ps.shape; s.enabled = true; s.shapeType = ParticleSystemShapeType.Sphere; s.radius = radius; s.radiusThickness = thickness;
        if (scale.HasValue) s.scale = scale.Value;
    }

    /// <summary>국소 속도: 직선(x,y,z)·Y축 공전·방사(음수면 중심으로).</summary>
    static void Velocity(ParticleSystem ps, float x, float y, float z, float orbitY, float radial)
    {
        var vel = ps.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.Local;
        vel.x = x; vel.y = y; vel.z = z; vel.orbitalX = 0f; vel.orbitalY = orbitY; vel.orbitalZ = 0f; vel.radial = radial;
    }

    static void Damp(ParticleSystem ps, float dampen)
    {
        var l = ps.limitVelocityOverLifetime; l.enabled = true; l.limit = 0f; l.dampen = dampen;
    }

    static void SizeEnd(ParticleSystem ps, float end)
    {
        var s = ps.sizeOverLifetime; s.enabled = true; s.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, end));
    }

    static void Spin(ParticleSystem ps, float minDeg, float maxDeg)
    {
        var r = ps.rotationOverLifetime; r.enabled = true; r.z = new ParticleSystem.MinMaxCurve(minDeg * Mathf.Deg2Rad, maxDeg * Mathf.Deg2Rad);
    }

    static void Stretch(ParticleSystem ps, float velocityScale)
    {
        var r = ps.GetComponent<ParticleSystemRenderer>(); r.renderMode = ParticleSystemRenderMode.Stretch; r.velocityScale = velocityScale; r.lengthScale = 1.4f;
    }

    static void FadeKeys(ParticleSystem ps, Color[] colors, (float t, float a)[] alphas)
    {
        var col = ps.colorOverLifetime; col.enabled = true;
        var g = new Gradient();
        var ck = new GradientColorKey[colors.Length];
        for (int i = 0; i < colors.Length; i++) ck[i] = new GradientColorKey(colors[i], colors.Length == 1 ? 0f : i / (float)(colors.Length - 1));
        var ak = new GradientAlphaKey[alphas.Length];
        for (int i = 0; i < alphas.Length; i++) ak[i] = new GradientAlphaKey(alphas[i].a, alphas[i].t);
        g.SetKeys(ck, ak);
        col.color = g;
    }

    static GameObject Save(GameObject root, string name)
    {
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, Root + name + ".prefab");
        Object.DestroyImmediate(root);
        return prefab;
    }
}
