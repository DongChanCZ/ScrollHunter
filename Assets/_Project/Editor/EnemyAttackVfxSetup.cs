using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 적 공격 연출 만들기·붙이기(10/6, 10 A41). 메뉴 Tools > Scroll Hunter > Apply Enemy Attack VFX.
/// 적 빌더를 다시 돌리지 않고 깡패·약탈자·궁병·우두머리 프리팹에 EnemyAttackVfx·궤적만 더하고, 발사 화살 프리팹에 꼬리를 단다.
/// 재질은 기존 플레이어 스킬 재질을 복사해 만든다(원본 수정 없음). 텍스처는 기존 Kenney Particle Pack(CC0)·TrailSoft 재사용.
/// 모델·무기·애니메이션·소켓·손 보정·전투 수치는 건드리지 않는다. 다시 실행하면 같은 결과로 덮어쓴다.
/// 적 빌더(Build Enemy Rigs)로 프리팹을 새로 만들었다면 이 메뉴를 다시 실행한다.
/// </summary>
public static class EnemyAttackVfxSetup
{
    const string Root = "Assets/_Project/VFX/Enemy/";
    const string Tex = "Assets/_Project/VFX/Textures/";
    const string Prefabs = "Assets/_Project/Prefabs/Enemies/";

    [MenuItem("Tools/Scroll Hunter/Apply Enemy Attack VFX")]
    public static void Apply()
    {
        if (!AssetDatabase.IsValidFolder("Assets/_Project/VFX/Enemy")) AssetDatabase.CreateFolder("Assets/_Project/VFX", "Enemy");
        var additive = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/VFX/Materials/VFX_spark_01.mat");
        var alpha = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/VFX/Materials/VFX_spark_01_alpha.mat");

        Material flash = Mat("ENM_VFX_Flash", additive, "flare_01", Color.white);
        Material glow = Mat("ENM_VFX_Glow", additive, "circle_05", Color.white);
        Material ring = Mat("ENM_VFX_Ring", additive, "circle_02", Color.white);
        Material lines = Mat("ENM_VFX_Lines", additive, "trace_01", Color.white);
        Material puff = Mat("ENM_VFX_Puff", alpha, "smoke_04", Color.white);
        Material raiderTrail = Mat("ENM_VFX_Trail_Raider", alpha, "TrailSoft", new Color(0.62f, 0.8f, 1f, 1f));
        Material leaderTrail = Mat("ENM_VFX_Trail_Leader", alpha, "TrailSoft", new Color(1f, 0.9f, 0.66f, 1f));
        Material arrowTrail = Mat("ENM_VFX_Trail_Arrow", alpha, "TrailSoft", new Color(0.95f, 0.95f, 0.92f, 1f));

        // 크기는 키 2 모델 기준(EnemyAttackVfx가 실제 크기에 맞춰 늘린다).
        GameObject punch = Burst("VFX_EnemyPunchHit", 0.3f,
            new Layer(glow, 1, 0.12f, 0.45f, 0.65f, 0f, 0.9f),
            new Layer(ring, 1, 0.22f, 0.18f, 0.9f, 0f, 1f),
            new Layer(lines, 8, 0.15f, 0.07f, 0.07f, 5f, 1f, stretch: 0.22f));
        GameObject kick = Burst("VFX_EnemyKickHit", 0.32f,
            new Layer(glow, 1, 0.13f, 0.55f, 0.8f, 0f, 0.9f),
            new Layer(ring, 1, 0.24f, 0.22f, 1.15f, 0f, 1f),
            new Layer(lines, 9, 0.16f, 0.08f, 0.08f, 6f, 1f, stretch: 0.24f));
        GameObject slash = Burst("VFX_LeaderSlashHit", 0.32f,
            new Layer(glow, 1, 0.12f, 0.65f, 0.9f, 0f, 0.8f),
            new Layer(flash, 1, 0.12f, 0.9f, 0.9f, 0f, 1f),
            new Layer(lines, 9, 0.15f, 0.09f, 0.09f, 7f, 1f, stretch: 0.26f));
        GameObject release = Burst("VFX_ArrowRelease", 0.3f,
            new Layer(glow, 1, 0.1f, 0.3f, 0.4f, 0f, 0.8f),
            new Layer(flash, 1, 0.1f, 0.4f, 0.4f, 0f, 1f),
            new Layer(ring, 1, 0.2f, 0.12f, 0.6f, 0f, 1f),
            new Layer(puff, 5, 0.3f, 0.14f, 0.26f, 0.7f, 0.55f, cone: 18f));

        Setup("ENM_Thug_ALL_v01", null, 0f, 0f, 0f, null,
            new Hit(0f, 0f, punch, HumanBodyBones.LeftHand, 0.08f),    // 잽: 왼손 주먹 앞(손목 뼈에서 주먹 너머로)
            new Hit(0f, 0f, punch, HumanBodyBones.RightHand, 0.08f),   // 크로스: 오른손 주먹 앞
            new Hit(0f, 0f, kick, HumanBodyBones.RightToes, 0.03f));   // 좀비킥: 오른발 발끝(발목 뼈 기준은 정강이 방향이라 위로 뜸)
        Setup("ENM_Raider_GRN_v01", raiderTrail, 0.2f, 1f, 0.065f, null,
            new Hit(0.26f, 0.42f, null, HumanBodyBones.LastBone, 0f));  // 회전 베기 칼날 구간
        Setup("ENM_Leader_RED_v01", leaderTrail, 0.15f, 0.95f, 0.11f, null,
            new Hit(0.33f, 0.49f, slash, HumanBodyBones.LastBone, 0f),  // 초록 올려베기
            null,
            new Hit(0.31f, 0.49f, slash, HumanBodyBones.LastBone, 0f)); // 빨강 내려베기
        Setup("ENM_Archer_ORG_v01", null, 0f, 0f, 0f, release);
        ArrowTail(arrowTrail);
        // SaveAssets는 무관한 TMP 동적 폰트까지 다시 쓰므로 만든 재질만 저장한다(프리팹은 위에서 바로 저장됨).
        foreach (var m in new[] { flash, glow, ring, lines, puff, raiderTrail, leaderTrail, arrowTrail }) AssetDatabase.SaveAssetIfDirty(m);
        Debug.Log("[적 공격 연출] 깡패·약탈자·궁병·우두머리 적용");
    }

    struct Layer
    {
        public Material mat; public int count; public float life, size0, size1, speed, alpha, stretch, cone;
        public Layer(Material m, int c, float l, float s0, float s1, float sp, float a, float stretch = 0f, float cone = 0f)
        { mat = m; count = c; life = l; size0 = s0; size1 = s1; speed = sp; alpha = a; this.stretch = stretch; this.cone = cone; }
    }

    class Hit
    {
        public float start, end; public GameObject fx; public HumanBodyBones bone; public float forward;
        public Hit(float s, float e, GameObject f, HumanBodyBones b, float fw) { start = s; end = e; fx = f; bone = b; forward = fw; }
    }

    static Material Mat(string name, Material template, string texture, Color color)
    {
        string path = Root + name + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null) { mat = new Material(template); AssetDatabase.CreateAsset(mat, path); }
        else mat.CopyPropertiesFromMaterial(template);
        mat.shaderKeywords = template.shaderKeywords;
        mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Tex + texture + ".png"));
        mat.SetColor("_BaseColor", color);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    // 한 번 터지고 스스로 사라지는 파티클 묶음. 뿌리는 아무것도 내지 않고 지속 시간 뒤 전체를 지운다.
    static GameObject Burst(string name, float duration, params Layer[] layers)
    {
        var root = new GameObject(name);
        var main = root.AddComponent<ParticleSystem>().main;
        main.duration = duration; main.loop = false; main.startLifetime = 0.01f; main.stopAction = ParticleSystemStopAction.Destroy;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        var rootEmission = root.GetComponent<ParticleSystem>().emission; rootEmission.enabled = false;
        root.GetComponent<ParticleSystemRenderer>().enabled = false;
        int i = 0;
        foreach (Layer l in layers)
        {
            var go = new GameObject("Layer" + i++);
            go.transform.SetParent(root.transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            var m = ps.main;
            m.duration = duration; m.loop = false; m.playOnAwake = true;
            m.startLifetime = l.life; m.startSpeed = l.speed; m.startSize = l.size0;
            m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            m.startColor = new Color(1f, 1f, 1f, l.alpha);
            m.simulationSpace = ParticleSystemSimulationSpace.World;
            m.scalingMode = ParticleSystemScalingMode.Hierarchy;
            m.maxParticles = Mathf.Max(1, l.count);
            var em = ps.emission; em.rateOverTime = 0f; em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)l.count) });
            var shape = ps.shape;
            if (l.cone > 0f) { shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = l.cone; shape.radius = 0.01f; }
            else if (l.speed > 0f) { shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = 0.02f; }
            else shape.enabled = false;
            var sol = ps.sizeOverLifetime; sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, l.size1 / Mathf.Max(0.0001f, l.size0)));
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.9f, 0.5f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = l.mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
            if (l.stretch > 0f) { r.renderMode = ParticleSystemRenderMode.Stretch; r.velocityScale = l.stretch; r.lengthScale = 1.5f; }
            else r.renderMode = ParticleSystemRenderMode.Billboard;
            r.sortingFudge = -10f;   // 같은 위치의 다른 반투명보다 앞에
        }
        string path = Root + name + ".prefab";
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return prefab;
    }

    static void Setup(string prefabName, Material trailMat, float trailTime, float trailAlpha, float trailWidth, GameObject release, params Hit[] hits)
    {
        string path = Prefabs + prefabName + ".prefab";
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var vfx = root.GetComponent<EnemyAttackVfx>();
            if (vfx == null) vfx = root.AddComponent<EnemyAttackVfx>();
            var so = new SerializedObject(vfx);
            so.FindProperty("driver").objectReferenceValue = root.GetComponent<EnemyAnimationDriver>();
            so.FindProperty("animator").objectReferenceValue = root.GetComponent<Animator>();

            // 무기 궤적·끝: 칼날(무기 +Z) 가운데에 폭 = 칼날 길이로 둔다. 면은 칼날 면(무기 X)에 수직(무기 Y)을 향한다.
            Transform weapon = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name.StartsWith("WPN_") && t.GetComponent<MeshFilter>() != null);
            TrailRenderer trail = null; Transform tip = null;
            if (weapon != null && trailMat != null)
            {
                Bounds b = weapon.GetComponent<MeshFilter>().sharedMesh.bounds;
                float bladeStart = Mathf.Max(0.03f, b.min.z + (b.max.z - b.min.z) * 0.25f), bladeEnd = b.max.z;
                trail = Child(weapon, "AttackTrail").gameObject.GetComponent<TrailRenderer>();
                if (trail == null) trail = Child(weapon, "AttackTrail").gameObject.AddComponent<TrailRenderer>();
                trail.transform.localPosition = new Vector3(0f, 0f, bladeStart + (bladeEnd - bladeStart) * 0.85f);
                trail.transform.localRotation = Quaternion.identity;
                trail.alignment = LineAlignment.View;
                trail.time = trailTime;
                trail.minVertexDistance = 0.01f;
                trail.widthMultiplier = trailWidth * root.transform.lossyScale.y;   // 모델 키 1 기준 폭
                trail.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0.35f);   // 꼬리까지 굵기를 남긴다
                var g = new Gradient();
                g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                          new[] { new GradientAlphaKey(trailAlpha, 0f), new GradientAlphaKey(trailAlpha * 0.8f, 0.5f), new GradientAlphaKey(0f, 1f) });
                trail.colorGradient = g;
                trail.sharedMaterial = trailMat;
                trail.numCapVertices = 0; trail.numCornerVertices = 2;
                trail.textureMode = LineTextureMode.Stretch;
                trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; trail.receiveShadows = false;
                trail.emitting = false;
                trail.autodestruct = false;
                tip = Child(weapon, "AttackTip");
                tip.localPosition = new Vector3(0f, 0f, bladeEnd * 0.95f);
                tip.localRotation = Quaternion.identity;
            }
            so.FindProperty("weaponTrail").objectReferenceValue = trail;
            so.FindProperty("weaponTip").objectReferenceValue = tip;

            var list = so.FindProperty("attacks");
            list.arraySize = 3;
            for (int i = 0; i < 3; i++)
            {
                var e = list.GetArrayElementAtIndex(i);
                Hit h = i < hits.Length ? hits[i] : null;
                e.FindPropertyRelative("trailStart").floatValue = h != null ? h.start : 0f;
                e.FindPropertyRelative("trailEnd").floatValue = h != null ? h.end : 0f;
                e.FindPropertyRelative("impactEffect").objectReferenceValue = h != null ? h.fx : null;
                e.FindPropertyRelative("impactBone").intValue = (int)(h != null ? h.bone : HumanBodyBones.LastBone);
                e.FindPropertyRelative("impactForward").floatValue = h != null ? h.forward : 0f;
            }
            so.FindProperty("releaseEffect").objectReferenceValue = release;
            var bow = root.GetComponentInChildren<BowRig>(true);
            so.FindProperty("bow").objectReferenceValue = bow;
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    static Transform Child(Transform parent, string name)
    {
        Transform t = parent.Find(name);
        if (t == null) { t = new GameObject(name).transform; t.SetParent(parent, false); }
        return t;
    }

    // 발사 화살 꼬리: 기존 발사 화살(ArrowFlight) 프리팹에만. 화살은 새로 만들지 않는다.
    static void ArrowTail(Material mat)
    {
        const string path = "Assets/_Project/Prefabs/Weapons/WPN_Arrow_Projectile.prefab";
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var trail = root.GetComponent<TrailRenderer>();
            if (trail == null) trail = root.AddComponent<TrailRenderer>();
            trail.time = 0.09f;
            trail.minVertexDistance = 0.01f;
            trail.widthMultiplier = 0.02f;
            trail.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0.3f);
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(0.8f, 0f), new GradientAlphaKey(0f, 1f) });
            trail.colorGradient = g;
            trail.sharedMaterial = mat;
            trail.numCapVertices = 0;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; trail.receiveShadows = false;
            trail.alignment = LineAlignment.View;
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
}
