using UnityEditor;
using UnityEngine;

/// <summary>
/// 마법사 보스 공격 연출 만들기·붙이기(10/6, 10 A42). 메뉴 Tools > Scroll Hunter > Apply Mage Attack VFX.
/// 적 빌더를 다시 돌리지 않고 마법사 프리팹에 BossAttackVfx만 더한다. 모델·동작·손 보정·전투 수치·다른 적은 건드리지 않는다.
/// 텍스처는 기존 반입 Kenney Particle Pack(CC0)·자체 TrailSoft 재사용, 재질은 기존 URP 파티클 재질 복사.
/// 크기는 키 2 모델 기준 m(BossAttackVfx가 실제 크기에 맞춰 늘린다). 다시 실행하면 같은 결과로 덮어쓴다.
/// </summary>
public static class MageAttackVfxSetup
{
    const string Root = "Assets/_Project/VFX/Mage/";
    const string Prefab = "Assets/_Project/Prefabs/Enemies/BOSS_Mage_ALL_v01.prefab";

    // 매직미사일(파이어볼 연출) 불꽃 색. 10/6 사용자 요청으로 보라 불꽃: 중심 연보라 → 보라 → 짙은 남보라
    static readonly Color FireCore = new Color(0.86f, 0.62f, 1f), FireMid = new Color(0.74f, 0.32f, 1f), FireRed = new Color(0.4f, 0.08f, 0.72f);
    static readonly Color FireBack = new Color(0.26f, 0.04f, 0.46f), FireSmoke = new Color(0.14f, 0.05f, 0.22f), FireSmokeEnd = new Color(0.08f, 0.03f, 0.13f);
    static readonly Color PurpleCore = new Color(0.9f, 0.75f, 1f), Purple = new Color(0.66f, 0.28f, 1f), PurpleDeep = new Color(0.32f, 0.08f, 0.58f);
    static readonly Color Crimson = new Color(0.88f, 0.1f, 0.26f), Black = new Color(0.02f, 0f, 0.03f);

    static Material glowAdd, glowAlpha, ringAdd, ringAlpha, fireAdd, fire2Add, flameAdd, smokeAlpha, twirlAdd, twirlAlpha, traceAdd, sigilAdd, trailFire;

    [MenuItem("Tools/Scroll Hunter/Apply Mage Attack VFX")]
    public static void Apply()
    {
        if (!AssetDatabase.IsValidFolder("Assets/_Project/VFX/Mage")) AssetDatabase.CreateFolder("Assets/_Project/VFX", "Mage");
        var add = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/VFX/Materials/VFX_spark_01.mat");
        var alpha = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/VFX/Materials/VFX_spark_01_alpha.mat");
        const string tex = "Assets/_Project/VFX/Textures/";
        glowAdd = Mat("MG_Glow_Add", add, tex + "circle_05.png");
        glowAlpha = Mat("MG_Glow_Alpha", alpha, tex + "circle_05.png");
        ringAdd = Mat("MG_Ring_Add", add, tex + "circle_02.png");
        ringAlpha = Mat("MG_Ring_Alpha", alpha, tex + "circle_02.png");
        fireAdd = Mat("MG_Fire_Add", add, tex + "fire_01.png");
        fire2Add = Mat("MG_Fire2_Add", add, tex + "fire_02.png");
        flameAdd = Mat("MG_Flame_Add", add, tex + "flame_05.png");
        smokeAlpha = Mat("MG_Smoke_Alpha", alpha, tex + "smoke_04.png");
        twirlAdd = Mat("MG_Twirl_Add", add, tex + "twirl_02.png");
        twirlAlpha = Mat("MG_Twirl_Alpha", alpha, tex + "twirl_02.png");
        traceAdd = Mat("MG_Trace_Add", add, tex + "trace_02.png");
        sigilAdd = Mat("MG_Sigil_Add", add, "Assets/_Project/UI/SanctuaryVfx/particle_magic_02.png");
        trailFire = Mat("MG_Trail_Fire", add, tex + "TrailSoft.png");

        var skills = new[]
        {
            new Skill("매직미사일(파이어볼 연출)", FireCharge(), BossAttackVfx.Anchor.RightHand, Vector3.zero, 0.35f, 1f, 0.35f, 0f,
                FireMuzzle(), BossAttackVfx.Anchor.RightHand, Vector3.zero, 0.08f) { projectile = Fireball(), impact = FireImpact(), travel = 0.5f, seconds = 0.22f, chargeSize = 1.8f, releaseSize = 1.5f },
            new Skill("사일런스", SilenceCharge(), BossAttackVfx.Anchor.RightHand, Vector3.zero, 0.4f, 1f, 0.3f, 0f,
                SilenceWave(), BossAttackVfx.Anchor.RightHand, new Vector3(0f, 0f, -0.05f), 0.08f) { chargeSize = 2.2f, releaseSize = 2.4f },
            // 손을 든 자리는 보스 게이지 뒤라 보이지 않는다. 든 손에서 기운이 내려와 가슴 앞(양손 방출 자리)에 응축된다.
            new Skill("다크홀", DarkHoleCharge(), BossAttackVfx.Anchor.Chest, new Vector3(-0.15f, -0.1f, -0.45f), 0.15f, 0.5f, 0.25f, 0.08f,
                DarkHoleBurst(), BossAttackVfx.Anchor.Charge, Vector3.zero, 0.12f) { chargeSize = 1.4f, releaseSize = 3.6f },   // 10/7 발동 폭발만 2배(1.8→3.6), 준비 크기 유지
            new Skill("나이트메어", NightmareCharge(), BossAttackVfx.Anchor.Chest, new Vector3(0f, -0.1f, -0.1f), 0.5f, 1f, 0.2f, 0.05f,
                NightmareBurst(), BossAttackVfx.Anchor.Chest, new Vector3(0f, 0f, -0.15f), 0.15f) { chargeSize = 1.5f, releaseSize = 1.8f },
            // 머리 위 높이는 BossAttackVfx.aboveHeadHeight(게이지·마커 위 빈 공간). 든 손에서 구체까지 기운이 이어진다.
            new Skill("디엔드", TheEndCharge(), BossAttackVfx.Anchor.AboveHead, Vector3.zero, 0.25f, 1f, 0.5f, 0.18f,
                TheEndBlast(), BossAttackVfx.Anchor.Charge, Vector3.zero, 0.12f) { chargeSize = 1.6f, releaseSize = 1f, ground = TheEndGround(), screen = TheEndScreen() },
        };

        var root = PrefabUtility.LoadPrefabContents(Prefab);
        try
        {
            var vfx = root.GetComponent<BossAttackVfx>();
            if (vfx == null) vfx = root.AddComponent<BossAttackVfx>();
            var so = new SerializedObject(vfx);
            so.FindProperty("driver").objectReferenceValue = root.GetComponent<EnemyAnimationDriver>();
            so.FindProperty("animator").objectReferenceValue = root.GetComponent<Animator>();
            so.FindProperty("palmForward").floatValue = 0.08f;
            so.FindProperty("aboveHeadHeight").floatValue = 2.7f;   // 머리 뼈 위 2.7m: 보스 게이지·타겟 마커 위 빈 공간(전투 카메라 기준)
            so.FindProperty("aboveHeadHandBlend").floatValue = 0.35f;
            var list = so.FindProperty("skills");
            list.arraySize = skills.Length;
            for (int i = 0; i < skills.Length; i++)
            {
                Skill s = skills[i];
                var e = list.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("label").stringValue = s.label;
                e.FindPropertyRelative("charge").objectReferenceValue = s.charge;
                e.FindPropertyRelative("chargeSize").floatValue = s.chargeSize;
                e.FindPropertyRelative("releaseSize").floatValue = s.releaseSize;
                e.FindPropertyRelative("groundRelease").objectReferenceValue = s.ground;
                e.FindPropertyRelative("screenRelease").objectReferenceValue = s.screen;
                e.FindPropertyRelative("chargeAnchor").enumValueIndex = (int)s.chargeAnchor;
                e.FindPropertyRelative("chargeOffset").vector3Value = s.chargeOffset;
                e.FindPropertyRelative("chargeStartScale").floatValue = s.startScale;
                e.FindPropertyRelative("chargeEndScale").floatValue = s.endScale;
                e.FindPropertyRelative("growth").animationCurveValue = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
                e.FindPropertyRelative("startEmission").floatValue = s.startEmission;
                e.FindPropertyRelative("follow").floatValue = s.follow;
                e.FindPropertyRelative("release").objectReferenceValue = s.release;
                e.FindPropertyRelative("releaseAnchor").enumValueIndex = (int)s.releaseAnchor;
                e.FindPropertyRelative("releaseOffset").vector3Value = s.releaseOffset;
                e.FindPropertyRelative("chargeCollapse").floatValue = s.collapse;
                e.FindPropertyRelative("projectile").objectReferenceValue = s.projectile;
                e.FindPropertyRelative("projectileImpact").objectReferenceValue = s.impact;
                e.FindPropertyRelative("projectileTravel").floatValue = s.travel;
                e.FindPropertyRelative("projectileSeconds").floatValue = s.seconds;
                e.FindPropertyRelative("projectileAim").vector3Value = new Vector3(0f, -1.6f, 0f);
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, Prefab);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }

        // SaveAssets는 무관한 TMP 동적 폰트까지 다시 쓰므로 만든 재질만 저장한다(프리팹은 위에서 바로 저장됨).
        foreach (var m in new[] { glowAdd, glowAlpha, ringAdd, ringAlpha, fireAdd, fire2Add, flameAdd, smokeAlpha, twirlAdd, twirlAlpha, traceAdd, sigilAdd, trailFire })
            AssetDatabase.SaveAssetIfDirty(m);
        Debug.Log("[마법사 공격 연출] 5종 적용");
    }

    class Skill
    {
        public string label; public GameObject charge, release, projectile, impact, ground, screen;
        public BossAttackVfx.Anchor chargeAnchor, releaseAnchor; public Vector3 chargeOffset, releaseOffset;
        public float startScale, endScale, startEmission, follow, collapse, travel = 0.5f, seconds = 0.2f, chargeSize = 1f, releaseSize = 1f;
        public Skill(string l, GameObject c, BossAttackVfx.Anchor ca, Vector3 co, float s0, float s1, float e0, float f,
            GameObject r, BossAttackVfx.Anchor ra, Vector3 ro, float col)
        { label = l; charge = c; chargeAnchor = ca; chargeOffset = co; startScale = s0; endScale = s1; startEmission = e0; follow = f; release = r; releaseAnchor = ra; releaseOffset = ro; collapse = col; }
    }

    // ───────── ① 파이어볼(매직미사일 연출) ─────────

    static GameObject FireCharge()
    {
        var root = new GameObject("VFX_Mage_FireCharge");
        var g = Child(root.transform, "Grow");
        var heat = Make(g, "HeatBack", glowAlpha, 1, true, true); Life(heat, 0.4f, 0.5f); Size(heat, 0.62f, 0.7f); Rate(heat, 8f);
        Fade(heat, new[] { FireBack, FireRed }, 0f, 0f, 0.3f, 0.5f, 1f, 0f);   // 밝은 바닥·하늘에서도 불이 묻히지 않게 짙은 보라 바탕
        var core = Make(g, "Core", glowAdd, 3, true, true); Life(core, 0.25f, 0.35f); Size(core, 0.42f, 0.5f); Rate(core, 14f);
        Fade(core, new[] { FireCore, FireMid }, 0f, 1f, 0.15f, 1f, 1f, 0f);
        var flames = Make(g, "Flames", flameAdd, 2, true, true); Life(flames, 0.35f, 0.5f); Size(flames, 0.24f, 0.34f); Speed(flames, 0.35f, 0.6f); Rate(flames, 24f);
        Cone(flames, 22f, 0.07f, new Vector3(-90f, 0f, 0f)); SizeEnd(flames, 0.4f); Spin(flames, -40f, 40f);
        Fade(flames, new[] { FireCore, FireMid, FireRed }, 0f, 0f, 0.2f, 1f, 1f, 0f);
        var embers = Make(g, "Embers", glowAdd, 4, true, true); Life(embers, 0.4f, 0.6f); Size(embers, 0.035f, 0.06f); Rate(embers, 20f);
        Sphere(embers, 0.32f, 0f); Radial(embers, -0.6f); Fade(embers, new[] { FireCore, FireMid }, 0f, 0f, 0.3f, 1f, 1f, 0f);
        return Save(root, "VFX_Mage_FireCharge");
    }

    static GameObject FireMuzzle()
    {
        var root = BurstRoot("VFX_Mage_FireMuzzle", 0.5f);
        var flash = Make(root.transform, "Flash", glowAdd, 4, false, false); Life(flash, 0.14f, 0.14f); Size(flash, 0.6f, 0.6f); SizeEnd(flash, 1.5f); Burst(flash, 1);
        Fade(flash, new[] { FireCore, FireMid }, 1f, 1f, 0.5f, 0.8f, 1f, 0f);
        var puff = Make(root.transform, "Puff", fireAdd, 3, false, false); Life(puff, 0.25f, 0.32f); Size(puff, 0.28f, 0.36f); Speed(puff, 0.5f, 1f); Burst(puff, 7);
        Sphere(puff, 0.05f, 0f); SizeEnd(puff, 1.6f); Spin(puff, -90f, 90f); Fade(puff, new[] { FireMid, FireRed }, 1f, 1f, 0.5f, 0.7f, 1f, 0f);
        return Save(root, "VFX_Mage_FireMuzzle");
    }

    static GameObject Fireball()
    {
        var root = new GameObject("VFX_Mage_Fireball");
        var heat = Make(root.transform, "HeatBack", glowAlpha, 1, true, true); Life(heat, 0.12f, 0.12f); Size(heat, 0.62f, 0.66f); Rate(heat, 30f);
        Fade(heat, new[] { FireRed, FireRed }, 0.55f, 0.55f, 0.5f, 0.55f, 1f, 0.3f);
        var core = Make(root.transform, "Core", glowAdd, 4, true, true); Life(core, 0.12f, 0.12f); Size(core, 0.45f, 0.5f); Rate(core, 30f);
        Fade(core, new[] { FireCore, FireCore }, 1f, 1f, 0.5f, 1f, 1f, 0.6f);
        var body = Make(root.transform, "Body", fireAdd, 3, true, true); Life(body, 0.15f, 0.2f); Size(body, 0.38f, 0.5f); Rate(body, 34f); Spin(body, -360f, 360f);
        Fade(body, new[] { FireMid, FireRed }, 0.6f, 1f, 0.5f, 1f, 1f, 0f);
        var trail = Make(root.transform, "TrailFlames", fire2Add, 2, true, false); Life(trail, 0.18f, 0.28f); Size(trail, 0.22f, 0.3f); Distance(trail, 40f);
        SizeEnd(trail, 0.25f); Spin(trail, -180f, 180f); Fade(trail, new[] { FireMid, FireRed, FireBack }, 1f, 1f, 0.4f, 0.8f, 1f, 0f);
        var smoke = Make(root.transform, "Smoke", smokeAlpha, 1, true, false); Life(smoke, 0.3f, 0.4f); Size(smoke, 0.18f, 0.24f); Distance(smoke, 12f); SizeEnd(smoke, 1.8f);
        Fade(smoke, new[] { FireSmoke, FireSmokeEnd }, 0.5f, 0.5f, 0.3f, 0.4f, 1f, 0f);
        var tr = root.AddComponent<TrailRenderer>();
        tr.time = 0.1f; tr.minVertexDistance = 0.02f; tr.widthMultiplier = 0.22f; tr.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(FireCore, 0f), new GradientColorKey(FireMid, 0.4f), new GradientColorKey(FireRed, 1f) },
                  new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0f, 1f) });
        tr.colorGradient = g; tr.sharedMaterial = trailFire; tr.alignment = LineAlignment.View; tr.numCapVertices = 2;
        tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; tr.receiveShadows = false;
        return Save(root, "VFX_Mage_Fireball");
    }

    static GameObject FireImpact()
    {
        var root = BurstRoot("VFX_Mage_FireImpact", 0.8f);
        // 기존 Explosion03 시트는 주황색이 텍스처에 들어 있어 보라로 물들지 않으므로 흰 Kenney 텍스처로 폭발을 구성한다.
        var back = Make(root.transform, "DarkBack", glowAlpha, 1, false, false); Life(back, 0.4f, 0.4f); Size(back, 0.9f, 0.9f); SizeEnd(back, 1.4f); Burst(back, 1);
        Fade(back, new[] { FireBack, FireRed }, 0.6f, 0.6f, 0.5f, 0.45f, 1f, 0f);
        var flash = Make(root.transform, "Flash", glowAdd, 4, false, false); Life(flash, 0.18f, 0.18f); Size(flash, 0.8f, 0.8f); SizeEnd(flash, 1.6f); Burst(flash, 1);
        Fade(flash, new[] { FireCore, FireMid }, 1f, 1f, 0.5f, 0.9f, 1f, 0f);
        var boom = Make(root.transform, "Explosion", fireAdd, 3, false, false); Life(boom, 0.35f, 0.45f); Size(boom, 0.45f, 0.6f); Speed(boom, 0.8f, 1.5f); Burst(boom, 9);
        Sphere(boom, 0.08f, 0f); SizeEnd(boom, 1.8f); Spin(boom, -120f, 120f); Fade(boom, new[] { FireCore, FireMid, FireRed }, 1f, 1f, 0.4f, 0.9f, 1f, 0f);
        var ring = Make(root.transform, "Ring", ringAdd, 3, false, false); Life(ring, 0.3f, 0.3f); Size(ring, 0.3f, 0.3f); SizeEnd(ring, 4.5f); Burst(ring, 1);
        Fade(ring, new[] { FireMid, FireRed }, 0.9f, 0.9f, 0.5f, 0.7f, 1f, 0f);
        var sparks = Make(root.transform, "Sparks", traceAdd, 4, false, false); Life(sparks, 0.25f, 0.45f); Size(sparks, 0.04f, 0.06f); Speed(sparks, 2f, 3.5f); Burst(sparks, 14);
        Sphere(sparks, 0.05f, 0f); Stretch(sparks, 0.08f); Fade(sparks, new[] { FireCore, FireMid }, 1f, 1f, 0.5f, 1f, 1f, 0f);
        var smoke = Make(root.transform, "Smoke", smokeAlpha, 1, false, false); Life(smoke, 0.55f, 0.7f); Size(smoke, 0.4f, 0.5f); Speed(smoke, 0.2f, 0.4f); Burst(smoke, 4);
        Sphere(smoke, 0.1f, 0f); SizeEnd(smoke, 1.8f); Spin(smoke, -60f, 60f); Fade(smoke, new[] { FireSmoke, FireSmokeEnd }, 0.5f, 0.5f, 0.3f, 0.45f, 1f, 0f);
        return Save(root, "VFX_Mage_FireImpact");
    }

    // ───────── ② 사일런스 ─────────

    static GameObject SilenceCharge()
    {
        var root = new GameObject("VFX_Mage_SilenceCharge");
        var g = Child(root.transform, "Grow");
        var back = Make(g, "DarkBack", glowAlpha, 1, true, true); Life(back, 0.4f, 0.4f); Size(back, 0.42f, 0.42f); Rate(back, 7f);
        Fade(back, new[] { new Color(0.22f, 0.04f, 0.38f), PurpleDeep }, 0f, 0.55f, 0.3f, 0.7f, 1f, 0f);
        var glow = Make(g, "Glow", glowAdd, 3, true, true); Life(glow, 0.3f, 0.3f); Size(glow, 0.34f, 0.38f); Rate(glow, 12f);
        Fade(glow, new[] { PurpleCore, Purple }, 0f, 1f, 0.3f, 1f, 1f, 0f);
        var ring = Make(g, "CompressRing", ringAdd, 2, true, true); Life(ring, 0.6f, 0.6f); Size(ring, 0.6f, 0.6f); Rate(ring, 2.5f); SizeEnd(ring, 0.25f);
        Fade(ring, new[] { Purple, PurpleCore }, 0f, 0f, 0.4f, 0.9f, 1f, 0f);
        var motes = Make(g, "Motes", glowAdd, 4, true, true); Life(motes, 0.45f, 0.45f); Size(motes, 0.03f, 0.05f); Rate(motes, 22f);
        Sphere(motes, 0.35f, 0f); Radial(motes, -0.7f); Fade(motes, new[] { PurpleCore, Purple }, 0f, 0f, 0.3f, 1f, 1f, 0f);
        return Save(root, "VFX_Mage_SilenceCharge");
    }

    static GameObject SilenceWave()
    {
        var root = BurstRoot("VFX_Mage_SilenceWave", 0.6f);
        var flash = Make(root.transform, "Flash", glowAdd, 4, false, false); Life(flash, 0.1f, 0.1f); Size(flash, 0.4f, 0.4f); Burst(flash, 1);
        Fade(flash, new[] { PurpleCore, Purple }, 1f, 1f, 0.5f, 0.8f, 1f, 0f);
        var ring = Make(root.transform, "Ring", ringAdd, 3, false, false); Life(ring, 0.35f, 0.35f); Size(ring, 0.15f, 0.15f); SizeEnd(ring, 6.5f); Burst(ring, 1);
        Fade(ring, new[] { PurpleCore, Purple }, 1f, 1f, 0.5f, 0.8f, 1f, 0f);
        var inner = Make(root.transform, "InnerRing", ringAlpha, 2, false, false); Life(inner, 0.4f, 0.4f); Size(inner, 0.1f, 0.1f); SizeEnd(inner, 8.5f); Burst(inner, 1, 0.06f);
        Fade(inner, new[] { PurpleDeep, PurpleDeep }, 0.85f, 0.85f, 0.5f, 0.6f, 1f, 0f);
        var sigil = Make(root.transform, "Sigil", sigilAdd, 2, false, false); Life(sigil, 0.3f, 0.3f); Size(sigil, 0.5f, 0.5f); SizeEnd(sigil, 1.3f); Burst(sigil, 1); Spin(sigil, 120f, 120f);
        Fade(sigil, new[] { Purple, PurpleCore }, 1f, 1f, 0.5f, 0.8f, 1f, 0f);
        var sparks = Make(root.transform, "Sparks", traceAdd, 4, false, false); Life(sparks, 0.22f, 0.28f); Size(sparks, 0.035f, 0.05f); Speed(sparks, 1.5f, 2.5f); Burst(sparks, 10);
        Cone(sparks, 35f, 0.03f, new Vector3(0f, 180f, 0f)); Stretch(sparks, 0.1f); Fade(sparks, new[] { PurpleCore, Purple }, 1f, 1f, 0.5f, 1f, 1f, 0f);
        return Save(root, "VFX_Mage_SilenceWave");
    }

    // ───────── ③ 다크홀 ─────────

    static GameObject DarkHoleCharge()
    {
        var root = new GameObject("VFX_Mage_DarkHoleCharge");
        DarkSphere(Child(root.transform, "Grow"), Crimson, new Color(0.45f, 0.08f, 0.6f));
        var inward = Make(root.transform, "Inward", traceAdd, 4, true, true); Life(inward, 0.45f, 0.45f); Size(inward, 0.05f, 0.08f); Rate(inward, 28f);
        Sphere(inward, 0.55f, 0f); Radial(inward, -1.1f); Stretch(inward, 0.12f); Fade(inward, new[] { Crimson, PurpleDeep }, 0f, 0f, 0.3f, 1f, 1f, 0.4f);
        var wisps = Make(root.transform, "Wisps", smokeAlpha, 0, true, true); Life(wisps, 0.6f, 0.6f); Size(wisps, 0.3f, 0.34f); Rate(wisps, 9f);
        Sphere(wisps, 0.45f, 0f); Radial(wisps, -0.6f); SizeEnd(wisps, 0.3f); Spin(wisps, -60f, 60f);
        Fade(wisps, new[] { new Color(0.08f, 0.02f, 0.1f), new Color(0.05f, 0.01f, 0.06f) }, 0f, 0f, 0.4f, 0.7f, 1f, 0f);
        HandLink(root.transform, Crimson, PurpleDeep);   // 든 오른손에서 가슴 앞 구체로 내려오는 기운
        return Save(root, "VFX_Mage_DarkHoleCharge");
    }

    static GameObject DarkHoleBurst()
    {
        var root = BurstRoot("VFX_Mage_DarkHoleBurst", 1f);
        var implode = Make(root.transform, "Implode", glowAlpha, 1, false, true); Life(implode, 0.18f, 0.18f); Size(implode, 0.6f, 0.6f); SizeEnd(implode, 0.15f); Burst(implode, 1);
        Fade(implode, new[] { Black, Black }, 1f, 1f, 0.6f, 1f, 1f, 0f);
        var blast = Make(root.transform, "DarkBlast", smokeAlpha, 1, false, true); Life(blast, 0.5f, 0.7f); Size(blast, 0.3f, 0.35f); Speed(blast, 1.2f, 2f); Burst(blast, 8);
        Sphere(blast, 0.05f, 0f); SizeEnd(blast, 2.3f); Spin(blast, -90f, 90f); Damp(blast, 0.2f);
        Fade(blast, new[] { new Color(0.06f, 0.02f, 0.08f), new Color(0.12f, 0.02f, 0.08f) }, 0.85f, 0.85f, 0.4f, 0.6f, 1f, 0f);
        var shock = Make(root.transform, "Shock", ringAdd, 3, false, true); Life(shock, 0.35f, 0.35f); Size(shock, 0.2f, 0.2f); SizeEnd(shock, 8f); Burst(shock, 1);
        Fade(shock, new[] { Crimson, Purple }, 1f, 1f, 0.5f, 0.7f, 1f, 0f);
        var shock2 = Make(root.transform, "ShockDark", ringAlpha, 2, false, true); Life(shock2, 0.4f, 0.4f); Size(shock2, 0.2f, 0.2f); SizeEnd(shock2, 7f); Burst(shock2, 1, 0.02f);
        Fade(shock2, new[] { PurpleDeep, PurpleDeep }, 0.8f, 0.8f, 0.5f, 0.5f, 1f, 0f);
        var flash = Make(root.transform, "Flash", glowAdd, 4, false, true); Life(flash, 0.12f, 0.12f); Size(flash, 0.7f, 0.7f); Burst(flash, 1);
        Fade(flash, new[] { new Color(0.95f, 0.25f, 0.4f), Crimson }, 0.9f, 0.9f, 0.5f, 0.6f, 1f, 0f);
        var shards = Make(root.transform, "Shards", traceAdd, 4, false, true); Life(shards, 0.25f, 0.35f); Size(shards, 0.04f, 0.06f); Speed(shards, 2.5f, 4f); Burst(shards, 16);
        Sphere(shards, 0.05f, 0f); Stretch(shards, 0.1f); Fade(shards, new[] { Crimson, Purple }, 1f, 1f, 0.5f, 1f, 1f, 0f);
        MaxScreenSize(root, 2f);   // 2배 확대 뒤 큰 고리가 화면 절반 제한(기본 0.5)에 잘리지 않게
        return Save(root, "VFX_Mage_DarkHoleBurst");
    }

    // ───────── ④ 나이트메어 ─────────

    static GameObject NightmareCharge()
    {
        var root = new GameObject("VFX_Mage_NightmareCharge");
        var g = Child(root.transform, "Grow");
        var aura = Make(g, "DarkAura", glowAlpha, 0, true, true); Life(aura, 0.8f, 0.8f); Size(aura, 0.95f, 1.05f); Rate(aura, 6f);
        Fade(aura, new[] { Black, PurpleDeep }, 0f, 0f, 0.4f, 0.65f, 1f, 0f);
        var smoke = Make(g, "SmokeSwirl", smokeAlpha, 1, true, true); Life(smoke, 0.9f, 1.2f); Size(smoke, 0.45f, 0.55f); Rate(smoke, 22f);
        Circle(smoke, 0.65f); Radial(smoke, -0.35f); Orbit(smoke, 1.6f); SizeEnd(smoke, 0.45f); Spin(smoke, -60f, 60f);
        Fade(smoke, new[] { new Color(0.22f, 0.06f, 0.34f), new Color(0.1f, 0.03f, 0.16f) }, 0f, 0f, 0.35f, 0.85f, 1f, 0f);
        var wisps = Make(g, "PurpleWisps", twirlAdd, 2, true, true); Life(wisps, 0.7f, 0.9f); Size(wisps, 0.45f, 0.6f); Rate(wisps, 6f);
        Circle(wisps, 0.5f); Orbit(wisps, 2f); Spin(wisps, 120f, 200f);   // 몸을 감도는 보라 잔상
        Fade(wisps, new[] { Purple, PurpleDeep }, 0f, 0f, 0.4f, 0.9f, 1f, 0f);
        var gather = Make(g, "Gather", smokeAlpha, 1, true, true); Life(gather, 0.7f, 0.7f); Size(gather, 0.35f, 0.4f); Rate(gather, 10f);
        Sphere(gather, 1f, 0f, new Vector3(1f, 0.7f, 1f)); Radial(gather, -1.3f); SizeEnd(gather, 0.4f); Spin(gather, -80f, 80f);   // 바깥에서 몸 쪽으로 모이는 그림자
        Fade(gather, new[] { new Color(0.12f, 0.03f, 0.18f), Black }, 0f, 0f, 0.4f, 0.7f, 1f, 0f);
        var after = Make(g, "Afterimage", traceAdd, 3, true, true); Life(after, 0.5f, 0.5f); Size(after, 0.07f, 0.1f); Rate(after, 26f);
        Circle(after, 0.6f); Orbit(after, 2.4f); Radial(after, -0.3f); Stretch(after, 0.18f);
        Fade(after, new[] { PurpleCore, Purple }, 0f, 0f, 0.3f, 1f, 1f, 0f);
        return Save(root, "VFX_Mage_NightmareCharge");
    }

    static GameObject NightmareBurst()
    {
        var root = BurstRoot("VFX_Mage_NightmareBurst", 1.2f);
        var flash = Make(root.transform, "DarkFlash", glowAlpha, 0, false, false); Life(flash, 0.25f, 0.25f); Size(flash, 0.6f, 0.6f); SizeEnd(flash, 2.5f); Burst(flash, 1);
        Fade(flash, new[] { Black, PurpleDeep }, 0.7f, 0.7f, 0.5f, 0.4f, 1f, 0f);
        var wave = Make(root.transform, "SmokeWave", smokeAlpha, 1, false, false); Life(wave, 0.7f, 1f); Size(wave, 0.4f, 0.5f); Speed(wave, 2f, 3f); Burst(wave, 18);
        Circle(wave, 0.2f); SizeEnd(wave, 2.5f); Spin(wave, -80f, 80f); Damp(wave, 0.2f);
        Fade(wave, new[] { new Color(0.22f, 0.06f, 0.32f), new Color(0.08f, 0.02f, 0.12f) }, 0.8f, 0.8f, 0.4f, 0.6f, 1f, 0f);
        var streaks = Make(root.transform, "ShadowStreaks", traceAdd, 3, false, false); Life(streaks, 0.35f, 0.5f); Size(streaks, 0.05f, 0.07f); Speed(streaks, 3f, 4.5f); Burst(streaks, 14);
        Circle(streaks, 0.15f); Stretch(streaks, 0.15f); Fade(streaks, new[] { Purple, PurpleDeep }, 1f, 1f, 0.5f, 0.9f, 1f, 0f);
        var swirl = Make(root.transform, "Afterglow", twirlAdd, 2, false, false); Life(swirl, 0.5f, 0.5f); Size(swirl, 0.6f, 0.6f); SizeEnd(swirl, 2.7f); Burst(swirl, 2); Spin(swirl, 200f, 200f);
        Fade(swirl, new[] { Purple, PurpleDeep }, 0.8f, 0.8f, 0.5f, 0.6f, 1f, 0f);
        return Save(root, "VFX_Mage_NightmareBurst");
    }

    // ───────── ⑤ 디엔드 ─────────

    static GameObject TheEndCharge()
    {
        var root = new GameObject("VFX_Mage_TheEndCharge");
        var g = Child(root.transform, "Grow");
        DarkSphere(g, new Color(0.78f, 0.12f, 0.45f), new Color(0.35f, 0.05f, 0.5f));
        var swirl2 = Make(g, "InnerSwirlDark", twirlAlpha, 2, true, true); Life(swirl2, 1.4f, 1.4f); Size(swirl2, 0.7f, 0.7f); Rate(swirl2, 3f); Spin(swirl2, -120f, -120f);
        Fade(swirl2, new[] { new Color(0.25f, 0.05f, 0.4f), PurpleDeep }, 0f, 0f, 0.4f, 0.7f, 1f, 0f);
        var orbit = Make(g, "OrbitSparks", glowAdd, 4, true, true); Life(orbit, 0.8f, 0.8f); Size(orbit, 0.03f, 0.05f); Rate(orbit, 14f);
        Sphere(orbit, 0.5f, 0f); Orbit(orbit, 1.5f); Fade(orbit, new[] { new Color(0.95f, 0.4f, 0.65f), Purple }, 0f, 0f, 0.3f, 1f, 1f, 0f);
        // 바깥 기운은 위아래로 납작하게 모아 아래쪽 타겟 마커·게이지로 내려오지 않게 한다.
        var inward = Make(root.transform, "Inward", traceAdd, 4, true, true); Life(inward, 0.55f, 0.55f); Size(inward, 0.07f, 0.1f); Rate(inward, 44f);
        Sphere(inward, 1.1f, 0f, new Vector3(1f, 0.5f, 1f)); Radial(inward, -1.9f); Stretch(inward, 0.12f);
        Fade(inward, new[] { PurpleCore, Crimson }, 0f, 0f, 0.3f, 1f, 1f, 0.5f);
        var inSmoke = Make(root.transform, "InSmoke", smokeAlpha, 1, true, true); Life(inSmoke, 0.8f, 0.8f); Size(inSmoke, 0.42f, 0.48f); Rate(inSmoke, 14f);
        Sphere(inSmoke, 1f, 0f, new Vector3(1f, 0.5f, 1f)); Radial(inSmoke, -1.1f); SizeEnd(inSmoke, 0.3f); Spin(inSmoke, -60f, 60f);
        Fade(inSmoke, new[] { new Color(0.06f, 0.01f, 0.09f), new Color(0.03f, 0f, 0.05f) }, 0f, 0f, 0.4f, 0.7f, 1f, 0.2f);
        HandLink(root.transform, Purple, Crimson);
        return Save(root, "VFX_Mage_TheEndCharge");
    }

    /// <summary>든 오른손에서 준비 효과 중심으로 흐르는 입자. BossAttackVfx가 위치·방향·속도(거리/수명)를 매 프레임 맞춘다.</summary>
    static void HandLink(Transform root, Color c0, Color c1)
    {
        var link = Make(root, "HandLink", glowAdd, 4, true, false); Life(link, 0.35f, 0.35f); Size(link, 0.07f, 0.1f); Rate(link, 40f);
        Cone(link, 4f, 0.03f, Vector3.zero); Fade(link, new[] { c0, c1 }, 0f, 0f, 0.2f, 1f, 1f, 0.3f);
        var lm = link.main; lm.scalingMode = ParticleSystemScalingMode.Local;   // 부모 배율과 무관하게 손→효과 거리만큼 날아간다
        // 같은 줄기를 따라 흐르는 어두운 기운(밝은 하늘에서도 보이게)
        var smoke = Make(link.transform, "LinkSmoke", smokeAlpha, 1, true, false); Life(smoke, 0.35f, 0.35f); Size(smoke, 0.14f, 0.2f); Rate(smoke, 24f);
        Cone(smoke, 6f, 0.04f, Vector3.zero); Spin(smoke, -90f, 90f); SizeEnd(smoke, 0.5f);
        Fade(smoke, new[] { new Color(0.12f, 0.02f, 0.18f), Black }, 0f, 0f, 0.25f, 0.75f, 1f, 0.2f);
        var sm = smoke.main; sm.scalingMode = ParticleSystemScalingMode.Local;
    }

    static GameObject TheEndGround()
    {
        var root = BurstRoot("VFX_Mage_TheEndGround", 1.2f);
        var ground = Make(root.transform, "GroundWave", ringAdd, 2, false, false); Life(ground, 0.7f, 0.7f); Size(ground, 0.8f, 0.8f); SizeEnd(ground, 9f); Burst(ground, 1);
        ground.transform.localPosition = new Vector3(0f, 0.05f, 0f);
        ground.GetComponent<ParticleSystemRenderer>().renderMode = ParticleSystemRenderMode.HorizontalBillboard;
        Fade(ground, new[] { new Color(0.6f, 0.1f, 0.4f), PurpleDeep }, 0.8f, 0.8f, 0.5f, 0.5f, 1f, 0f);
        var groundDark = Make(root.transform, "GroundWaveDark", ringAlpha, 1, false, false); Life(groundDark, 0.8f, 0.8f); Size(groundDark, 0.6f, 0.6f); SizeEnd(groundDark, 9f); Burst(groundDark, 1, 0.02f);
        groundDark.transform.localPosition = new Vector3(0f, 0.04f, 0f);
        groundDark.GetComponent<ParticleSystemRenderer>().renderMode = ParticleSystemRenderMode.HorizontalBillboard;
        Fade(groundDark, new[] { PurpleDeep, Black }, 0.7f, 0.7f, 0.5f, 0.45f, 1f, 0f);
        var dust = Make(root.transform, "Dust", smokeAlpha, 1, false, false); Life(dust, 0.7f, 1f); Size(dust, 0.5f, 0.6f); Speed(dust, 2.5f, 4f); Burst(dust, 18);
        Circle(dust, 0.3f); SizeEnd(dust, 2f); Spin(dust, -60f, 60f); Damp(dust, 0.25f);
        Fade(dust, new[] { new Color(0.1f, 0.03f, 0.12f), new Color(0.06f, 0.02f, 0.08f) }, 0.7f, 0.7f, 0.4f, 0.5f, 1f, 0f);
        return Save(root, "VFX_Mage_TheEndGround");
    }

    /// <summary>
    /// 디엔드 화면 폭발(10/7). 지름 1 단위로 만들고 ScreenBlastVfx가 카메라 앞에서 화면 높이 × 배율로 키운다.
    /// 검은 중심·짙은 보라 가장자리·연기·충격 고리·보라 줄기를 겹친다. 흰 섬광 층은 두지 않는다.
    /// 전체 불투명도(퍼짐·유지·사라짐)는 ScreenBlastVfx가 맡으므로 층별 색은 거의 일정하게 둔다.
    /// </summary>
    static GameObject TheEndScreen()
    {
        var root = new GameObject("VFX_Mage_TheEndScreen");
        var t = root.transform;
        // 정렬 순서 100번대: 전투 쪽 파티클(보스 폭발·바닥 충격파 등 1~7번)보다 위에 그려 화면을 덮는다. HUD(Overlay)는 그래도 위.
        // 검은 중심은 유지 끝 무렵 먼저 걷히고, 보라 연기·잔상이 조금 더 남아 흩어진다.
        var core = Make(t, "DarkCore", glowAlpha, 100, false, true); Life(core, 0.8f, 0.8f); Size(core, 1f, 1f); Burst(core, 1);
        FadeKeys(core, new[] { Black, Black }, new[] { (0f, 1f), (0.65f, 1f), (1f, 0f) });
        var fill = Make(t, "DarkFill", glowAlpha, 101, false, true); Life(fill, 0.95f, 0.95f); Size(fill, 0.7f, 0.8f); Burst(fill, 6);
        ScreenCircle(fill, 0.2f); FadeKeys(fill, new[] { new Color(0.08f, 0.01f, 0.13f), new Color(0.05f, 0.01f, 0.08f) }, new[] { (0f, 0.9f), (0.6f, 0.9f), (1f, 0f) });
        var smoke = Make(t, "Smoke", smokeAlpha, 102, false, true); Life(smoke, 1.6f, 2f); Size(smoke, 0.3f, 0.45f); Speed(smoke, 0.06f, 0.14f); Burst(smoke, 40);
        ScreenCircle(smoke, 0.34f); Spin(smoke, -60f, 60f); SizeEnd(smoke, 1.3f);
        FadeKeys(smoke, new[] { new Color(0.32f, 0.08f, 0.48f), new Color(0.12f, 0.03f, 0.2f) }, new[] { (0f, 0.95f), (1f, 0.95f) });
        var swirl = Make(t, "Swirl", twirlAlpha, 103, false, true); Life(swirl, 1.4f, 1.4f); Size(swirl, 0.6f, 0.75f); Burst(swirl, 2); Spin(swirl, 140f, 200f);
        FadeKeys(swirl, new[] { PurpleDeep, new Color(0.15f, 0.03f, 0.25f) }, new[] { (0f, 0.6f), (0.7f, 0.6f), (1f, 0f) });
        var rimDark = Make(t, "RimDark", ringAlpha, 104, false, true); Life(rimDark, 0.6f, 0.6f); Size(rimDark, 1.04f, 1.04f); Burst(rimDark, 1);
        FadeKeys(rimDark, new[] { PurpleDeep, Black }, new[] { (0f, 0.9f), (0.6f, 0.6f), (1f, 0f) });
        var rim = Make(t, "Rim", ringAdd, 105, false, true); Life(rim, 0.5f, 0.5f); Size(rim, 1f, 1f); Burst(rim, 1);
        FadeKeys(rim, new[] { new Color(0.62f, 0.2f, 1f), PurpleDeep }, new[] { (0f, 1f), (0.6f, 0.7f), (1f, 0f) });   // 퍼지는 폭발 앞자리의 짙은 보라 테두리
        var streaks = Make(t, "Streaks", traceAdd, 106, false, true); Life(streaks, 0.3f, 0.4f); Size(streaks, 0.02f, 0.03f); Speed(streaks, 0.6f, 1f); Burst(streaks, 22);
        ScreenCircle(streaks, 0.1f); Stretch(streaks, 0.25f); FadeKeys(streaks, new[] { Purple, PurpleDeep }, new[] { (0f, 1f), (1f, 0f) });
        var embers = Make(t, "Afterimage", glowAdd, 107, false, true); Life(embers, 1.2f, 1.6f); Size(embers, 0.008f, 0.014f); Speed(embers, 0.02f, 0.06f); Burst(embers, 30);
        ScreenCircle(embers, 0.42f); FadeKeys(embers, new[] { Purple, PurpleDeep }, new[] { (0f, 0f), (0.2f, 0.9f), (1f, 0f) });
        MaxScreenSize(root, 10f);   // 화면보다 큰 입자가 화면 절반 제한(기본 0.5)에 잘리지 않게
        root.AddComponent<ScreenBlastVfx>();   // 퍼짐 0.3·최대 불투명도 0.95·유지 0.25·사라짐 0.8초·최종 지름 화면 높이 6배(컴포넌트 기본값, Inspector 조정)
        return Save(root, "VFX_Mage_TheEndScreen");
    }

    /// <summary>카메라를 향한 평면(로컬 XY)의 원에서 바깥으로 내보낸다.</summary>
    static void ScreenCircle(ParticleSystem ps, float radius)
    {
        var s = ps.shape; s.enabled = true; s.shapeType = ParticleSystemShapeType.Circle; s.radius = radius; s.radiusThickness = 1f; s.rotation = Vector3.zero;
    }

    static void MaxScreenSize(GameObject root, float fraction)
    {
        foreach (var r in root.GetComponentsInChildren<ParticleSystemRenderer>(true)) r.maxParticleSize = fraction;
    }

    static GameObject TheEndBlast()
    {
        var root = BurstRoot("VFX_Mage_TheEndBlast", 1.4f);
        var implode = Make(root.transform, "Implode", glowAlpha, 1, false, false); Life(implode, 0.2f, 0.2f); Size(implode, 1f, 1f); SizeEnd(implode, 0.2f); Burst(implode, 1);
        Fade(implode, new[] { Black, Black }, 1f, 1f, 0.6f, 1f, 1f, 0f);
        var blast = Make(root.transform, "Blast", smokeAlpha, 1, false, false); Life(blast, 0.8f, 1.2f); Size(blast, 0.6f, 0.7f); Speed(blast, 3f, 5f); Burst(blast, 22);
        Sphere(blast, 0.3f, 0f); SizeEnd(blast, 2.6f); Spin(blast, -90f, 90f); Damp(blast, 0.25f);
        Fade(blast, new[] { new Color(0.05f, 0.01f, 0.08f), new Color(0.14f, 0.02f, 0.1f) }, 0.85f, 0.85f, 0.4f, 0.6f, 1f, 0f);
        var shock = Make(root.transform, "Shock", ringAdd, 3, false, false); Life(shock, 0.55f, 0.55f); Size(shock, 0.4f, 0.4f); SizeEnd(shock, 16f); Burst(shock, 1);
        Fade(shock, new[] { new Color(0.85f, 0.15f, 0.45f), Purple }, 1f, 1f, 0.5f, 0.7f, 1f, 0f);
        var shock2 = Make(root.transform, "ShockDark", ringAlpha, 2, false, false); Life(shock2, 0.6f, 0.6f); Size(shock2, 0.3f, 0.3f); SizeEnd(shock2, 15f); Burst(shock2, 1, 0.03f);
        Fade(shock2, new[] { PurpleDeep, PurpleDeep }, 0.8f, 0.8f, 0.5f, 0.5f, 1f, 0f);
        var flash = Make(root.transform, "Flash", glowAdd, 4, false, false); Life(flash, 0.15f, 0.15f); Size(flash, 1.6f, 1.6f); Burst(flash, 1);
        Fade(flash, new[] { new Color(0.75f, 0.2f, 0.5f), PurpleDeep }, 0.8f, 0.8f, 0.5f, 0.5f, 1f, 0f);
        var shards = Make(root.transform, "Shards", traceAdd, 4, false, false); Life(shards, 0.4f, 0.6f); Size(shards, 0.05f, 0.08f); Speed(shards, 5f, 8f); Burst(shards, 30);
        Sphere(shards, 0.2f, 0f); Stretch(shards, 0.1f); Fade(shards, new[] { Crimson, Purple }, 1f, 1f, 0.5f, 1f, 1f, 0f);
        return Save(root, "VFX_Mage_TheEndBlast");
    }

    /// <summary>검은 응축 구체(다크홀·디엔드 공통). 지름 1 기준. 검은 중심 + 테두리 두 겹 + 안쪽 소용돌이.</summary>
    static void DarkSphere(Transform g, Color rim, Color rimDark)
    {
        var core = Make(g, "DarkCore", glowAlpha, 1, true, true); Life(core, 0.9f, 1f); Size(core, 0.9f, 1f); Rate(core, 7f); Spin(core, -30f, 30f);
        FadeKeys(core, new[] { Black, Black }, new[] { (0f, 0f), (0.2f, 0.97f), (0.8f, 0.97f), (1f, 0f) });
        var rimDarkPs = Make(g, "RimDark", ringAlpha, 2, true, true); Life(rimDarkPs, 0.8f, 1f); Size(rimDarkPs, 1.05f, 1.05f); Rate(rimDarkPs, 3f); SizeEnd(rimDarkPs, 0.92f);
        Fade(rimDarkPs, new[] { rimDark, rimDark }, 0f, 0f, 0.4f, 0.7f, 1f, 0f);
        var rimPs = Make(g, "Rim", ringAdd, 3, true, true); Life(rimPs, 0.7f, 0.9f); Size(rimPs, 1f, 1f); Rate(rimPs, 4f); SizeEnd(rimPs, 1.12f);
        Fade(rimPs, new[] { rim, rim }, 0f, 0f, 0.4f, 0.9f, 1f, 0f);
        var swirl = Make(g, "InnerSwirl", twirlAdd, 2, true, true); Life(swirl, 1f, 1.2f); Size(swirl, 0.85f, 0.95f); Rate(swirl, 7f); Spin(swirl, 160f, 220f);
        Fade(swirl, new[] { Color.Lerp(rim, Color.white, 0.25f), rim }, 0f, 0f, 0.4f, 1f, 1f, 0f);
        // 밝은 하늘에서 구체 둘레가 보이도록 반투명 보라 후광, 안쪽 움직임이 보이도록 작은 붉은 불티
        var halo = Make(g, "Halo", glowAlpha, 0, true, true); Life(halo, 1f, 1f); Size(halo, 1.5f, 1.6f); Rate(halo, 4f);
        Fade(halo, new[] { rimDark, PurpleDeep }, 0f, 0f, 0.4f, 0.45f, 1f, 0f);
        var crackle = Make(g, "Crackle", glowAdd, 4, true, true); Life(crackle, 0.25f, 0.4f); Size(crackle, 0.03f, 0.05f); Rate(crackle, 18f);
        Sphere(crackle, 0.32f, 1f); Orbit(crackle, 3f); Fade(crackle, new[] { Color.Lerp(rim, Color.white, 0.3f), rim }, 0f, 0f, 0.3f, 1f, 1f, 0f);
    }

    // ───────── 파티클 도우미 ─────────

    static Material Mat(string name, Material template, string texturePath)
    {
        string path = Root + name + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null) { mat = new Material(template); AssetDatabase.CreateAsset(mat, path); }
        else mat.CopyPropertiesFromMaterial(template);
        mat.shaderKeywords = template.shaderKeywords;
        mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));
        mat.SetColor("_BaseColor", Color.white);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static Transform Child(Transform parent, string name)
    {
        var t = new GameObject(name).transform;
        t.SetParent(parent, false);
        return t;
    }

    static GameObject BurstRoot(string name, float duration)
    {
        var root = new GameObject(name);
        var ps = root.AddComponent<ParticleSystem>();
        var main = ps.main; main.duration = duration; main.loop = false; main.startLifetime = 0.01f;
        main.stopAction = ParticleSystemStopAction.Destroy; main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        var em = ps.emission; em.enabled = false;
        root.GetComponent<ParticleSystemRenderer>().enabled = false;
        return root;
    }

    static ParticleSystem Make(Transform parent, string name, Material mat, int order, bool loop, bool local)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop = loop; main.duration = loop ? 1f : 1.5f; main.playOnAwake = true;
        main.startSpeed = 0f; main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.simulationSpace = local ? ParticleSystemSimulationSpace.Local : ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.maxParticles = 300;
        var em = ps.emission; em.rateOverTime = 0f;
        var shape = ps.shape; shape.enabled = false;
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = mat; r.sortingOrder = order;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
        r.renderMode = ParticleSystemRenderMode.Billboard;
        return ps;
    }

    static void Life(ParticleSystem ps, float a, float b) { var m = ps.main; m.startLifetime = new ParticleSystem.MinMaxCurve(a, b); }
    static void Size(ParticleSystem ps, float a, float b) { var m = ps.main; m.startSize = new ParticleSystem.MinMaxCurve(a, b); }
    static void Speed(ParticleSystem ps, float a, float b) { var m = ps.main; m.startSpeed = new ParticleSystem.MinMaxCurve(a, b); }
    static void Rate(ParticleSystem ps, float r) { var e = ps.emission; e.rateOverTime = r; }
    static void Distance(ParticleSystem ps, float r) { var e = ps.emission; e.rateOverDistance = r; }

    static void Burst(ParticleSystem ps, int count, float delay = 0f)
    {
        var e = ps.emission; e.rateOverTime = 0f; e.SetBursts(new[] { new ParticleSystem.Burst(delay, (short)count) });
        var m = ps.main; m.maxParticles = Mathf.Max(1, count);
    }

    static void Sphere(ParticleSystem ps, float radius, float thickness, Vector3? scale = null)
    {
        var s = ps.shape; s.enabled = true; s.shapeType = ParticleSystemShapeType.Sphere; s.radius = radius; s.radiusThickness = thickness;
        if (scale.HasValue) s.scale = scale.Value;
    }

    static void Circle(ParticleSystem ps, float radius)
    {
        var s = ps.shape; s.enabled = true; s.shapeType = ParticleSystemShapeType.Circle; s.radius = radius; s.radiusThickness = 0f;
        s.rotation = new Vector3(90f, 0f, 0f);   // 바닥과 나란한 원
    }

    static void Cone(ParticleSystem ps, float angle, float radius, Vector3 rotation)
    {
        var s = ps.shape; s.enabled = true; s.shapeType = ParticleSystemShapeType.Cone; s.angle = angle; s.radius = radius; s.rotation = rotation;
    }

    static void Radial(ParticleSystem ps, float v)
    {
        var vel = ps.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.Local;
        vel.x = 0f; vel.y = 0f; vel.z = 0f; vel.radial = v;
    }

    static void Orbit(ParticleSystem ps, float y)
    {
        var vel = ps.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.Local;
        vel.x = 0f; vel.y = 0f; vel.z = 0f; vel.orbitalY = y;
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
        var r = ps.GetComponent<ParticleSystemRenderer>(); r.renderMode = ParticleSystemRenderMode.Stretch; r.velocityScale = velocityScale; r.lengthScale = 1.2f;
    }

    static void Sheet(ParticleSystem ps, int x, int y)
    {
        var t = ps.textureSheetAnimation; t.enabled = true; t.numTilesX = x; t.numTilesY = y;
        t.animation = ParticleSystemAnimationType.WholeSheet; t.frameOverTime = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0f, 1f, 1f));
    }

    /// <summary>색은 처음→끝 순서로 고르게, 투명도는 (시작, 시작 값) (중간 위치, 중간 값) (끝, 끝 값).</summary>
    static void Fade(ParticleSystem ps, Color[] colors, float a0, float aStart, float tMid, float aMid, float tEnd, float aEnd)
        => FadeKeys(ps, colors, new[] { (0f, a0 > 0f ? aStart : 0f), (tMid, aMid), (tEnd, aEnd) });

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
