using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// 깡패·약탈자·우두머리·마법사 외형·무기·기본 동작 준비(10/2, 07 D-13). 궁병에서 확인한 방식을 일반화했다.
/// ① Mixamo 클립: Humanoid·회전 보정(시작 자세가 카메라 -Z를 보게)·몸 회전/높이는 자세에 굽히고 수평 이동은 버림
/// ② 프리팹: 모델(키 2)·무기 소켓(쥔 손가락 원 맞춤 중심)·Animator(루트 모션 끔)
/// ③ 컨트롤러: Idle → Cast(공격 준비 반복) → Fire0/1/2(초록/주황/빨강) → Idle, Any State → Hit/Death.
///    무기 손은 Grip 층(손가락만)으로 늘 쥔 자세를 덮어쓴다. 전투 연결·공격 타이밍은 EnemyAnimationDriver가 처리한다.
/// 전투 규칙·공격은 만들지 않는다. 공격 번호는 기존 캐스팅 색(0 초록, 1 주황, 2 빨강)을 그대로 쓴다.
/// </summary>
public static class EnemyRigBuilder
{
    private const string MixamoFolder = "Assets/_Project/Animations/Mixamo/";
    private const string WeaponFolder = "Assets/_Project/Models/Weapons/Enemies/";
    private const float FingerThickness = 0.006f;   // 손가락 관절 중심에서 피부까지(모델 높이 1 기준, 약 1cm)

    private class Spec
    {
        public string name, model, prefab, controllerFolder, weapon;
        public HumanBodyBones weaponHand;
        public bool reverseGrip, gripBothHands;
        public float gripRadius;   // 손잡이 반지름(모델 높이 1 기준, make_weapons.py 값)
        public float gripCurl = GripCurl, gripThumb = GripThumb;   // 쥔 손가락 근육 값. 손가락이 긴 모델은 더 굽힌다
        public bool staffIdle;     // 대기 상태에서 무기 손을 지팡이 짚는 자세로 보정(HandIK)
        public string idle, cast, fire0, fire1, fire2, hit, death;
    }

    private static readonly Spec[] Specs =
    {
        new Spec { name = "Thug", model = "Assets/_Project/Models/Enemies/Thug/ENM_Thug_ALL_v01.fbx", prefab = "Assets/_Project/Prefabs/Enemies/ENM_Thug_ALL_v01.prefab",
            controllerFolder = "Assets/_Project/Animations/Thug", idle = "MX_BreathingIdle", cast = "MX_FightingIdle",
            fire0 = "MX_PunchingJab", fire1 = "MX_CrossPunch", fire2 = "MX_ZombieKicking", hit = "MX_HitReaction", death = "MX_FallingBackDeath" },
        new Spec { name = "Raider", model = "Assets/_Project/Models/Enemies/Raider/ENM_Raider_GRN_v01.fbx", prefab = "Assets/_Project/Prefabs/Enemies/ENM_Raider_GRN_v01.prefab",
            controllerFolder = "Assets/_Project/Animations/Raider", weapon = "WPN_Dagger_v01", weaponHand = HumanBodyBones.RightHand, reverseGrip = true, gripRadius = 0.0085f,
            idle = "MX_BreathingIdle", cast = "MX_KnifeIdle", fire0 = "MX_StandingMeleeAttack360High", hit = "MX_HitReaction", death = "MX_SwordShieldDeath" },
        new Spec { name = "Leader", model = "Assets/_Project/Models/Enemies/Leader/ENM_Leader_RED_v01.fbx", prefab = "Assets/_Project/Prefabs/Enemies/ENM_Leader_RED_v01.prefab",
            controllerFolder = "Assets/_Project/Animations/Leader", weapon = "WPN_Greatsword_v01", weaponHand = HumanBodyBones.RightHand, gripBothHands = true, gripRadius = 0.0105f, gripCurl = -0.85f, gripThumb = -0.5f,
            idle = "RPG-Character@2Hand-Sword-Idle", cast = "RPG-Character@2Hand-Sword-Idle", fire0 = "RPG-Character@2Hand-Sword-Attack4", fire2 = "RPG-Character@2Hand-Sword-Attack2",
            hit = "RPG-Character@2Hand-Sword-GetHit-F1", death = "RPG-Character@2Hand-Sword-Knockdown1" },
        new Spec { name = "Mage", model = "Assets/_Project/Models/Enemies/Mage/BOSS_Mage_ALL_v01.fbx", prefab = "Assets/_Project/Prefabs/Enemies/BOSS_Mage_ALL_v01.prefab",
            controllerFolder = "Assets/_Project/Animations/Mage", weapon = "WPN_Staff_v01", weaponHand = HumanBodyBones.LeftHand, gripRadius = 0.0125f,
            gripCurl = -0.85f, gripThumb = -0.5f, staffIdle = true,
            idle = "MX_MagicIdle03", cast = "MX_1HCastSpell01", fire0 = "MX_1HMagicAttack01", fire1 = "Silence", fire2 = "MX_2HMagicAttack01",
            hit = "MX_ReactLargeFront", death = "MX_ReactDeathBackward" },
    };

    private static readonly HashSet<string> Loops = new HashSet<string> { "MX_BreathingIdle", "MX_FightingIdle", "MX_KnifeIdle", "MX_GreatSwordIdle", "MX_GreatSwordBlocking", "MX_MagicIdle03", "MX_1HCastSpell01" };
    private const float GripCurl = -0.6f, GripThumb = -0.3f;

    [MenuItem("Tools/Scroll Hunter/Build Enemy Rigs (Thug·Raider·Leader·Mage)")]
    public static void BuildAll()
    {
        PrepareWeapons();
        foreach (var spec in Specs)
        {
            ImportClips(spec);
            BuildPrefab(spec);
            BuildController(spec);
        }
        // SaveAssets는 무관한 TMP 동적 폰트까지 다시 쓰므로 이 빌더가 만든 에셋만 저장한다.
        foreach (var spec in Specs)
            foreach (string path in AssetDatabase.FindAssets("", new[] { spec.controllerFolder }).Select(AssetDatabase.GUIDToAssetPath))
                AssetDatabase.SaveAssetIfDirty(AssetDatabase.GUIDFromAssetPath(path));
        Debug.Log("[적 리그] 깡패·약탈자·우두머리·마법사 프리팹·컨트롤러 생성");
    }

    private static IEnumerable<string> Clips(Spec s) => new[] { s.idle, s.cast, s.fire0, s.fire1, s.fire2, s.hit, s.death }
        .Concat(s.name == "Mage" ? new[] { "Nightmare", "TheEnd" } : new string[0]).Where(c => c != null);

    private static string ClipPath(string file) => file.StartsWith("RPG-Character@")
        ? "Assets/_Project/Animations/ExplosiveRPGFree/" + file + ".FBX"
        : file == "Silence" || file == "Nightmare" || file == "TheEnd"
            ? "Assets/_Project/Animations/Mage/" + file + ".fbx" : MixamoFolder + file + ".fbx";

    public static AnimationClip Clip(string file) =>
        AssetDatabase.LoadAllAssetsAtPath(ClipPath(file)).OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview__"));

    private static void PrepareWeapons()
    {
        foreach (string w in new[] { "WPN_Dagger_v01", "WPN_Greatsword_v01", "WPN_Staff_v01" })
        {
            string fbx = WeaponFolder + w + ".fbx";
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(WeaponFolder + w + "_BaseColor.png");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(WeaponFolder + w + ".mat");
            if (mat == null) { mat = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(mat, WeaponFolder + w + ".mat"); }
            mat.SetTexture("_BaseMap", tex); mat.SetColor("_BaseColor", Color.white);
            mat.SetFloat("_Metallic", 0f); mat.SetFloat("_Smoothness", 0.3f);
            EditorUtility.SetDirty(mat); AssetDatabase.SaveAssetIfDirty(mat);
            var importer = (ModelImporter)AssetImporter.GetAtPath(fbx);
            importer.animationType = ModelImporterAnimationType.None; importer.importAnimation = false;
            importer.importCameras = importer.importLights = importer.importVisibility = importer.importBlendShapes = false;
            importer.bakeAxisConversion = true;
            foreach (string n in new[] { "Steel", "DarkSteel", "Leather", "Bronze", "Wood", "Crystal" })
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), n), mat);
            importer.SaveAndReimport();
        }
    }

    // ① 클립 반입. 회전 보정은 시작 프레임의 다리 방향으로 재서 카메라(-Z)를 보게 한다.
    private static void ImportClips(Spec spec)
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(spec.model);
        foreach (string file in Clips(spec).Distinct())
        {
            string path = ClipPath(file);
            // 무료 팩은 제공 Avatar와 저장된 시전 방향/루트 설정을 그대로 사용한다.
            if (file.StartsWith("RPG-Character@")) continue;
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            if (importer.animationType != ModelImporterAnimationType.Human)
            {
                importer.animationType = ModelImporterAnimationType.Human;
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                importer.materialImportMode = ModelImporterMaterialImportMode.None;
                importer.importAnimation = true;
                importer.SaveAndReimport();
            }
            // 이미 보정한 클립은 회전 보정을 유지한다(Play의 Animator로 맞춘 값, CalibrateFacingInPlay).
            if (importer.clipAnimations.Length > 0 && Mathf.Abs(importer.clipAnimations[0].rotationOffset) > 0.01f)
            {
                SetClips(importer, importer.clipAnimations, file, importer.clipAnimations[0].rotationOffset);
                continue;
            }
            float offset = 0f;
            var clips = importer.clipAnimations.Length > 0 ? importer.clipAnimations : importer.defaultClipAnimations;
            SetClips(importer, clips, file, 0f);
            var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
            try
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(model, scene);
                var animator = go.GetComponent<Animator>();
                // 클립 전체의 평균 몸 방향(다리 기준)을 카메라 쪽으로 돌린다. 도중에 몸을 트는 동작도 평균은 정면을 본다.
                AnimationClip clip = Clip(file);
                Vector3 forward = Vector3.zero;
                for (int i = 0; i <= 10; i++)
                {
                    clip.SampleAnimation(go, clip.length * i / 10f);
                    Vector3 across = animator.GetBoneTransform(HumanBodyBones.RightUpperLeg).position - animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg).position;
                    forward += Vector3.Cross(across, Vector3.up).normalized;
                }
                // 미리보기 샘플은 회전 보정을 반영하지 않는다. 첫 추정값이며 Play에서 CalibrateFacingInPlay로 맞춘다.
                offset = Mathf.DeltaAngle(0f, Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg - 180f);
            }
            finally { UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene); }
            SetClips(importer, importer.clipAnimations, file, offset);
        }
    }

    private static void SetClips(ModelImporter importer, ModelImporterClipAnimation[] clips, string file, float offset)
    {
        foreach (var c in clips)
        {
            c.name = file.Replace("MX_", "Mixamo_");
            c.loopTime = Loops.Contains(file);
            c.lockRootRotation = c.lockRootHeightY = true;
            c.lockRootPositionXZ = false;
            c.keepOriginalOrientation = c.keepOriginalPositionY = true;
            c.rotationOffset = offset;
        }
        importer.clipAnimations = clips;
        importer.SaveAndReimport();
    }

    // 쥔 손가락 근육 값(무기 손·Grip 층 공용)
    private static Dictionary<string, float> GripMuscles(string side, Spec spec)
    {
        var map = new Dictionary<string, float>();
        foreach (string f in new[] { "Index", "Middle", "Ring", "Little" })
            for (int k = 1; k <= 3; k++) map[$"{side} {f} {k} Stretched"] = spec.gripCurl;
        for (int k = 1; k <= 3; k++) map[$"{side} Thumb {k} Stretched"] = spec.gripThumb;
        return map;
    }

    // 쥔 손의 손잡이 기준(손 뼈 기준, 회전 없는 월드 길이). 손가락 관절에 원을 맞춘 중심을 손바닥 쪽으로 밀어
    // 모든 손가락 관절이 (반지름 + 손가락 두께) 밖에 오게 한다. 손가락 근육 값을 적용한 상태에서 부른다.
    private struct Grip
    {
        public Vector3 center, tunnel, forearm; public float halfWidth;
        public Vector3 localTunnel, localForearm, localToHand;   // 손 회전 기준(팔 자세와 무관)
    }

    private static Grip GripFrame(Animator animator, string side, float radius, bool reverse)
    {
        Transform B(string bone) => animator.GetBoneTransform((HumanBodyBones)System.Enum.Parse(typeof(HumanBodyBones), side + bone));
        Transform hand = B("Hand");
        Vector3 tunnel = (B("IndexProximal").position - B("LittleProximal").position).normalized;
        Vector3 forearm = Vector3.ProjectOnPlane(B("MiddleProximal").position - hand.position, tunnel).normalized;
        Vector3 e2 = Vector3.Cross(tunnel, forearm);
        string[] fingers = { "Index", "Middle", "Ring", "Little" }, segs = { "Proximal", "Intermediate", "Distal" };
        var pts = new List<Vector2>();
        foreach (string f in fingers)
            foreach (string seg in segs)
            {
                Vector3 d = B(f + seg).position - hand.position;
                pts.Add(new Vector2(Vector3.Dot(d, forearm), Vector3.Dot(d, e2)));
            }
        Vector2 c = FitCircle(pts);
        float along = fingers.Average(f => Vector3.Dot(B(f + "Intermediate").position - hand.position, tunnel));
        Vector3 center = hand.position + forearm * c.x + e2 * c.y + tunnel * along;
        Vector3 length = reverse ? -tunnel : tunnel;
        float need = radius + FingerThickness;
        for (int it = 0; it < 30; it++)
        {
            float min = float.MaxValue;
            foreach (string f in fingers)
                foreach (string seg in segs)
                    min = Mathf.Min(min, Vector3.ProjectOnPlane(B(f + seg).position - center, length).magnitude);
            if (min >= need - 0.0005f) break;
            center += Vector3.ProjectOnPlane(hand.position - center, length).normalized * (need - min);
        }
        Quaternion inv = Quaternion.Inverse(hand.rotation);
        return new Grip { center = center, tunnel = tunnel, forearm = forearm,
            halfWidth = Vector3.Distance(B("IndexProximal").position, B("LittleProximal").position) * 0.5f + FingerThickness,
            localTunnel = inv * tunnel, localForearm = inv * forearm, localToHand = inv * (hand.position - center) };
    }

    // HandIK: target은 손잡이 축 위의 점(+Z = 검지 쪽 축). 쥔 손 기준값은 손 회전 기준이며 루트 크기 1에서 잰 길이.
    private static void AddHandIK(GameObject root, bool leftHand, Transform target, Grip g, float slideMin, float slideMax, string[] only, string[] skip)
    {
        var ik = root.AddComponent<HandIK>();
        var so = new SerializedObject(ik);
        so.FindProperty("animator").objectReferenceValue = root.GetComponent<Animator>();
        so.FindProperty("leftHand").boolValue = leftHand;
        so.FindProperty("target").objectReferenceValue = target;
        so.FindProperty("slideMin").floatValue = slideMin;
        so.FindProperty("slideMax").floatValue = slideMax;
        so.FindProperty("localTunnel").vector3Value = g.localTunnel.normalized;
        so.FindProperty("localForearm").vector3Value = g.localForearm.normalized;
        so.FindProperty("localToHand").vector3Value = g.localToHand;
        void Strings(string name, string[] values)
        {
            var p = so.FindProperty(name); p.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) p.GetArrayElementAtIndex(i).stringValue = values[i];
        }
        Strings("onlyStates", only);
        Strings("skipStates", skip);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ② 프리팹. 무기 소켓은 쥔 손가락 관절에 원을 맞춘 중심, 무기 길이 방향은 검지(역수면 새끼) 쪽, 날은 팔 방향.
    private static void BuildPrefab(Spec spec)
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(spec.model);
        var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
        try
        {
            // 매번 모델에서 새로 만들어 같은 경로에 저장한다(GUID 유지).
            var root = (GameObject)PrefabUtility.InstantiatePrefab(model, scene);
            root.name = System.IO.Path.GetFileNameWithoutExtension(spec.prefab);
            var animator = root.GetComponent<Animator>();
            animator.applyRootMotion = false;
            ArcherAnimationBuilder.RestoreModelPose(root);

            if (spec.weapon != null)
            {
                Transform hand = animator.GetBoneTransform(spec.weaponHand);
                string side = spec.weaponHand == HumanBodyBones.LeftHand ? "Left" : "Right";
                string otherSide = side == "Left" ? "Right" : "Left";
                var handler = new HumanPoseHandler(animator.avatar, root.transform);
                var pose = new HumanPose(); handler.GetHumanPose(ref pose);
                var bind = (float[])pose.muscles.Clone();
                foreach (string s in new[] { side, otherSide })
                    foreach (var kv in GripMuscles(s, spec)) pose.muscles[System.Array.IndexOf(HumanTrait.MuscleName, kv.Key)] = kv.Value;
                handler.SetHumanPose(ref pose);

                Grip g = GripFrame(animator, side, spec.gripRadius, spec.reverseGrip);
                Grip offGrip = GripFrame(animator, otherSide, spec.gripRadius, false);
                Vector3 forearm = g.forearm;
                var socket = new GameObject("WeaponSocket").transform;
                socket.SetParent(hand, false);
                socket.position = g.center;
                Vector3 length = spec.reverseGrip ? -g.tunnel : g.tunnel;
                socket.rotation = Quaternion.LookRotation(length, Vector3.Cross(length, forearm));
                socket.localScale = Vector3.one / hand.lossyScale.x;
                var weapon = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(WeaponFolder + spec.weapon + ".fbx"), scene);
                weapon.transform.SetParent(socket, false);

                pose.muscles = bind; handler.SetHumanPose(ref pose);
                handler.Dispose();

                // 마법사: 대기 동작이 지팡이를 거꾸로 들어 대기 상태에서만 왼팔을 지팡이 짚는 자세로 보정한다.
                // 손은 어깨 바깥·아래·앞, 지팡이는 거의 수직(수정 위), 손등은 앞. 몸통 흔들림을 따르도록 엉덩이 아래에 둔다.
                if (spec.staffIdle)
                {
                    Transform shoulder = animator.GetBoneTransform(side == "Left" ? HumanBodyBones.LeftUpperArm : HumanBodyBones.RightUpperArm);
                    Vector3 outward = Vector3.right * Mathf.Sign(shoulder.position.x - root.transform.position.x);
                    Vector3 forward = -Vector3.forward;   // 모델 정면 -Z
                    // 아래팔이 너무 처지면 손목이 꺾이고, 너무 높으면 지팡이 끝이 뜬다(손잡이~끝 0.55).
                    Vector3 point = shoulder.position + outward * 0.035f + Vector3.down * 0.18f + forward * 0.13f;
                    var staffTarget = new GameObject("StaffIdleTarget").transform;
                    staffTarget.SetParent(animator.GetBoneTransform(HumanBodyBones.Hips), true);
                    staffTarget.SetPositionAndRotation(point, Quaternion.LookRotation((Vector3.up + outward * 0.06f).normalized, forward));
                    AddHandIK(root, side == "Left", staffTarget, g, 0f, 0f, new[] { "Idle" }, new string[0]);
                }

                // 양손 무기: 공격 중 반대 손이 닿는 방향으로 손잡이 축을 맞춘다.
                if (spec.gripBothHands)
                {
                    // 새 양손검 동작은 위에서 계산한 쥔 손 축을 사용한다. 이전 Mixamo 고정 각도는 적용하지 않는다.
                    // 반대 손 목표: 무기 손 바로 아래 손잡이 축 위의 점(검지는 칼날 쪽). 축을 도는 손 회전은 HandIK가 아래팔 방향으로 고른다.
                    // 닿지 않는 자세에서는 HandIK가 손잡이 끝(-0.16)까지 미끄러진다. 사망에서는 끈다.
                    float z = -(g.halfWidth + offGrip.halfWidth + 0.003f);
                    var offTarget = new GameObject("OffHandTarget").transform;
                    offTarget.SetParent(weapon.transform, false);
                    offTarget.localPosition = new Vector3(0f, 0f, z);
                    float gripEnd = -0.16f + offGrip.halfWidth;
                    AddHandIK(root, side == "Right", offTarget, offGrip, Mathf.Min(0f, gripEnd - z), 0f, new string[0], new[] { "Death" });
                }
            }
            ArcherAnimationBuilder.RestoreModelPose(root);
            root.transform.localScale = Vector3.one * (spec.name == "Leader" ? 2.6f : 2f);
            if (spec.name == "Thug") root.transform.localRotation = Quaternion.Euler(0f, 25f, 0f);
            PrefabUtility.SaveAsPrefabAsset(root, spec.prefab);
        }
        finally { UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene); }
    }

    private static Vector2 FitCircle(List<Vector2> pts)
    {
        double sx = 0, sy = 0, sxx = 0, syy = 0, sxy = 0, sxz = 0, syz = 0, sz = 0; int n = pts.Count;
        foreach (var q in pts) { double x = q.x, y = q.y, z = x * x + y * y; sx += x; sy += y; sxx += x * x; syy += y * y; sxy += x * y; sxz += x * z; syz += y * z; sz += z; }
        double[,] a = { { sxx, sxy, sx }, { sxy, syy, sy }, { sx, sy, n } }; double[] b = { sxz, syz, sz };
        for (int i = 0; i < 3; i++)
        {
            int p = i; for (int r = i + 1; r < 3; r++) if (System.Math.Abs(a[r, i]) > System.Math.Abs(a[p, i])) p = r;
            for (int k = 0; k < 3; k++) { var t = a[i, k]; a[i, k] = a[p, k]; a[p, k] = t; } { var t = b[i]; b[i] = b[p]; b[p] = t; }
            for (int r = 0; r < 3; r++) { if (r == i) continue; double f = a[r, i] / a[i, i]; for (int k = 0; k < 3; k++) a[r, k] -= f * a[i, k]; b[r] -= f * b[i]; }
        }
        return new Vector2((float)(b[0] / a[0, 0] / 2), (float)(b[1] / a[1, 1] / 2));
    }

    // 원본 FBX는 보존한다. 준비 동작의 몸 이동·다리 곡선만 첫 프레임 값으로 고정한다.
    private static AnimationClip BuildThugCastClip() => BuildFixedLowerCastClip(Specs.First(s => s.name == "Thug"), 15f);
    private static AnimationClip BuildMageCastClip() => BuildFixedLowerCastClip(Specs.First(s => s.name == "Mage"), 0f);

    private static AnimationClip BuildFixedLowerCastClip(Spec spec, float facingOffset)
    {
        string path = spec.controllerFolder + "/" + spec.name + "_CastUpper.anim";
        var source = Clip(spec.cast);
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (clip == null) { clip = Object.Instantiate(source); AssetDatabase.CreateAsset(clip, path); }
        else EditorUtility.CopySerialized(source, clip);
        clip.name = spec.name + "_CastUpper";
        foreach (var binding in AnimationUtility.GetCurveBindings(source))
        {
            string n = binding.propertyName;
            bool lower = n.StartsWith("Root") || n.StartsWith("LeftFoot") || n.StartsWith("RightFoot")
                || n.Contains("Upper Leg") || n.Contains("Lower Leg") || n.Contains(" Foot ") || n.Contains(" Toes ");
            if (!lower) continue;
            float value = AnimationUtility.GetEditorCurve(source, binding).Evaluate(0f);
            AnimationUtility.SetEditorCurve(clip, binding, AnimationCurve.Constant(0f, source.length, value));
        }
        BakeFixedHips(spec, clip);
        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.orientationOffsetY += facingOffset;   // 깡패는 기존 +15도, 마법사는 원본 방향 유지.
        if (spec.name == "Mage") settings.loopTime = false;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        EditorUtility.SetDirty(clip);
        AssetDatabase.SaveAssetIfDirty(clip);
        return clip;
    }

    private static void BakeFixedHips(Spec spec, AnimationClip clip)
    {
        // Humanoid는 상체의 무게중심 변화로 골반도 돌린다. 고정한 골반에 맞춘 몸 기준을 클립에 굽는다.
        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopBlendPositionXZ = settings.keepOriginalPositionXZ = true;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
        var graph = UnityEngine.Playables.PlayableGraph.Create(spec.name + " cast bake");
        HumanPoseHandler handler = null;
        try
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(spec.model);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(model, scene);
            go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            go.transform.localScale = Vector3.one;
            var animator = go.GetComponent<Animator>();
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            graph.SetTimeUpdateMode(UnityEngine.Playables.DirectorUpdateMode.Manual);
            var playable = UnityEngine.Animations.AnimationClipPlayable.Create(graph, clip);
            playable.SetApplyFootIK(false);
            var output = UnityEngine.Animations.AnimationPlayableOutput.Create(graph, "Pose", animator);
            UnityEngine.Playables.PlayableOutputExtensions.SetSourcePlayable(output, playable);
            graph.Play(); graph.Evaluate(0f);
            handler = new HumanPoseHandler(animator.avatar, go.transform);
            var pose = new HumanPose();
            var hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            Vector3 fixedPosition = hips.position;
            Quaternion fixedRotation = hips.rotation;
            string[] names = { "RootT.x", "RootT.y", "RootT.z", "RootQ.x", "RootQ.y", "RootQ.z", "RootQ.w" };
            var bindings = names.Select(n => EditorCurveBinding.FloatCurve("", typeof(Animator), n)).ToArray();
            var initial = bindings.Select(b => AnimationUtility.GetEditorCurve(clip, b).Evaluate(0f)).ToArray();
            var rootPosition = new Vector3(initial[0], initial[1], initial[2]);
            var rootRotation = new Quaternion(initial[3], initial[4], initial[5], initial[6]);
            var curves = names.Select(n => new AnimationCurve()).ToArray();
            int samples = Mathf.CeilToInt(clip.length * 60f);
            for (int i = 0; i <= samples; i++)
            {
                float time = clip.length * i / samples;
                UnityEngine.Playables.PlayableExtensions.SetTime(playable, time);
                graph.Evaluate(0f);
                handler.GetHumanPose(ref pose);
                Vector3 before = pose.bodyPosition;
                Quaternion toSource = Quaternion.Inverse(pose.bodyRotation * Quaternion.Inverse(rootRotation));
                hips.SetPositionAndRotation(fixedPosition, fixedRotation);
                handler.GetHumanPose(ref pose);
                Vector3 position = rootPosition + toSource * (pose.bodyPosition - before);
                Quaternion rotation = toSource * pose.bodyRotation;
                float[] values = { position.x, position.y, position.z, rotation.x, rotation.y, rotation.z, rotation.w };
                for (int k = 0; k < curves.Length; k++) curves[k].AddKey(time, values[k]);
            }
            for (int k = 0; k < curves.Length; k++) AnimationUtility.SetEditorCurve(clip, bindings[k], curves[k]);
        }
        finally
        {
            if (graph.IsValid()) graph.Destroy();
            handler?.Dispose();
            UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
        }
    }

    // ③ 컨트롤러
    private static void BuildController(Spec spec)
    {
        if (!AssetDatabase.IsValidFolder(spec.controllerFolder)) AssetDatabase.CreateFolder("Assets/_Project/Animations", spec.name);
        string path = $"{spec.controllerFolder}/{System.IO.Path.GetFileNameWithoutExtension(spec.prefab)}.controller";
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path) ?? AnimatorController.CreateAnimatorControllerAtPath(path);
        foreach (var p in controller.parameters) controller.RemoveParameter(p);
        while (controller.layers.Length > 1)
        {
            var oldMachine = controller.layers[1].stateMachine;
            controller.RemoveLayer(1);
            if (oldMachine != null) Object.DestroyImmediate(oldMachine, true);
        }
        var sm = controller.layers[0].stateMachine;
        foreach (var t in sm.anyStateTransitions) sm.RemoveAnyStateTransition(t);
        foreach (var s in sm.states) sm.RemoveState(s.state);
        foreach (string trigger in new[] { "Cast", "Fire", "Hit", "Death" }) controller.AddParameter(trigger, AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Attack", AnimatorControllerParameterType.Int);

        AnimatorState State(string name, string clip, Vector2 at) { var s = sm.AddState(name, at); s.motion = Clip(clip); return s; }
        AnimatorStateTransition Link(AnimatorState from, AnimatorState to, float duration, float exitTime = -1f)
        {
            var t = from.AddTransition(to); t.duration = duration; t.hasFixedDuration = true;
            t.hasExitTime = exitTime >= 0f; if (t.hasExitTime) t.exitTime = exitTime;
            return t;
        }
        var idle = State("Idle", spec.idle, new Vector2(250, 0));
        var cast = State("Cast", spec.cast, new Vector2(500, -120));
        if (spec.name == "Thug") cast.motion = BuildThugCastClip();   // 하체는 첫 자세, 상체는 기존 반복
        if (spec.name == "Mage") cast.motion = BuildMageCastClip();
        var hit = State("Hit", spec.hit, new Vector2(250, 220));
        var death = State("Death", spec.death, new Vector2(500, 260));
        sm.defaultState = idle;
        Link(idle, cast, 0.15f).AddCondition(AnimatorConditionMode.If, 0f, "Cast");
        string[] fires = spec.name == "Mage"
            ? new[] { spec.fire0, spec.fire1, spec.fire2, "Nightmare", "TheEnd" }
            : new[] { spec.fire0, spec.fire1, spec.fire2 };
        for (int i = 0; i < fires.Length; i++)
        {
            if (fires[i] == null) continue;
            var fire = State("Fire" + i, fires[i], new Vector2(750, -160 + i * 90));
            foreach (var from in new[] { idle, cast })
            {
                var t = Link(from, fire, 0.08f);
                t.AddCondition(AnimatorConditionMode.If, 0f, "Fire");
                t.AddCondition(AnimatorConditionMode.Equals, i, "Attack");
            }
            Link(fire, idle, 0.25f, 0.95f);
        }
        Link(hit, idle, 0.2f, 0.9f);
        // 피격은 살아 있는 상태에서만 받는다(Any State로 두면 사망 뒤 Hit가 다시 재생된다). 사망은 어디서나.
        foreach (var child in sm.states)
            if (child.state != hit && child.state != death)
                Link(child.state, hit, 0.08f).AddCondition(AnimatorConditionMode.If, 0f, "Hit");
        var toDeath = sm.AddAnyStateTransition(death);
        toDeath.duration = 0.08f; toDeath.hasFixedDuration = true; toDeath.canTransitionToSelf = false;
        toDeath.AddCondition(AnimatorConditionMode.If, 0f, "Death");

        if (spec.weapon != null)
        {
            string sideA = spec.weaponHand == HumanBodyBones.LeftHand ? "Left" : "Right";
            var sides = spec.gripBothHands ? new[] { "Left", "Right" } : new[] { sideA };
            string gripPath = $"{spec.controllerFolder}/{spec.name}_Grip.anim";
            var grip = AssetDatabase.LoadAssetAtPath<AnimationClip>(gripPath);
            if (grip == null) { grip = new AnimationClip(); AssetDatabase.CreateAsset(grip, gripPath); }
            grip.ClearCurves();
            foreach (string side in sides)
                foreach (var kv in GripMuscles(side, spec))
                {
                    string[] parts = kv.Key.Split(' ');   // "Left Index 1 Stretched"
                    AnimationUtility.SetEditorCurve(grip, EditorCurveBinding.FloatCurve("", typeof(Animator), $"{parts[0]}Hand.{parts[1]}.{parts[2]} {parts[3]}"), AnimationCurve.Constant(0f, 1f, kv.Value));
                }
            string maskPath = $"{spec.controllerFolder}/{spec.name}_GripMask.mask";
            var mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(maskPath);
            if (mask == null) { mask = new AvatarMask(); AssetDatabase.CreateAsset(mask, maskPath); }
            for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++) mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i, false);
            foreach (string side in sides) mask.SetHumanoidBodyPartActive(side == "Left" ? AvatarMaskBodyPart.LeftFingers : AvatarMaskBodyPart.RightFingers, true);
            EditorUtility.SetDirty(mask); EditorUtility.SetDirty(grip);
            var layer = new AnimatorControllerLayer { name = "Grip", defaultWeight = 1f, avatarMask = mask, blendingMode = AnimatorLayerBlendingMode.Override, stateMachine = new AnimatorStateMachine { name = "Grip" } };
            AssetDatabase.AddObjectToAsset(layer.stateMachine, controller);
            layer.stateMachine.hideFlags = HideFlags.HideInHierarchy;
            var held = layer.stateMachine.AddState("Held"); held.motion = grip; held.writeDefaultValues = false;
            controller.AddLayer(layer);
        }
        if (spec.name == "Mage") BuildMagePresentation(controller);
        ConfigureAttackController(controller);
        var layers = controller.layers;
        layers[0].iKPass = true;   // 머리 시선만 Animator IK. 손은 기존 HandIK(LateUpdate)
        controller.layers = layers;
        EditorUtility.SetDirty(controller);

        var root = PrefabUtility.LoadPrefabContents(spec.prefab);
        try
        {
            var animator = root.GetComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            if (root.GetComponent<EnemyAnimationDriver>() == null) root.AddComponent<EnemyAnimationDriver>();
            ConfigureAttackDriver(root.GetComponent<EnemyAnimationDriver>(), controller, spec.name);
            PrefabUtility.SaveAsPrefabAsset(root, spec.prefab);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    // 동작만 갱신한다. 모델·무기·메시를 다시 만들지 않아 손 보정 작업도 보존한다.
    private static void ConfigureAttackController(AnimatorController controller)
    {
        var layers = controller.layers;
        layers[0].iKPass = true;
        controller.layers = layers;
        if (!controller.parameters.Any(p => p.name == "AttackTime"))
            controller.AddParameter("AttackTime", AnimatorControllerParameterType.Float);
        bool mage = controller.layers[0].stateMachine.states.Any(c => c.state.name == "Fire4");
        if (mage && !controller.parameters.Any(p => p.name == "CastTime"))
            controller.AddParameter("CastTime", AnimatorControllerParameterType.Float);
        foreach (var child in controller.layers[0].stateMachine.states)
        {
            var state = child.state;
            if (mage && (state.name == "Cast" || state.name == "CastChargeRaise"))
            {
                state.timeParameter = "CastTime";
                state.timeParameterActive = true;
            }
            if (!state.name.StartsWith("Fire")) continue;
            state.timeParameter = "AttackTime";
            state.timeParameterActive = true;
            // 복귀도 드라이버가 처리한다. 원래 클립 시간으로 먼저 Idle로 빠지면 타격이 누락된다.
            foreach (var tr in state.transitions)
                if (tr.hasExitTime && tr.conditions.Length == 0) state.RemoveTransition(tr);
        }
        for (int i = controller.layers.Length - 1; i > 0; i--)
            if (controller.layers[i].name == "Flinch") controller.RemoveLayer(i);
        EditorUtility.SetDirty(controller);
    }

    private static void ConfigureAttackDriver(EnemyAnimationDriver driver, AnimatorController controller, string name)
    {
        // 현재 클립의 타격 자세. 교체 시 프리팹의 attacks에서 다시 맞춘다(전투 수치 아님).
        float[] impacts = name == "Thug" ? new[] { 0.55f, 0.57f, 0.24f }
            : name == "Raider" ? new[] { 0.32f, 0.32f, 0.32f }
            : name == "Leader" ? new[] { 0.38f, 0.38f, 0.39f }
            : new[] { 0.46f, 0.55f, 0.54f, 0.55f, 0.56f };
        var so = new SerializedObject(driver);
        bool mage = name == "Mage";
        so.FindProperty("useAttackOrder").boolValue = mage;
        so.FindProperty("castOnceClip").objectReferenceValue = mage
            ? controller.layers[0].stateMachine.states.First(c => c.state.name == "Cast").state.motion : null;
        so.FindProperty("chargeRaiseClip").objectReferenceValue = mage
            ? controller.layers[0].stateMachine.states.First(c => c.state.name == "CastChargeRaise").state.motion : null;
        var attacks = so.FindProperty("attacks"); attacks.arraySize = mage ? 5 : 3;
        for (int i = 0; i < attacks.arraySize; i++)
        {
            var state = controller.layers[0].stateMachine.states.Select(c => c.state).FirstOrDefault(st => st.name == "Fire" + i);
            var slot = attacks.GetArrayElementAtIndex(i);
            slot.FindPropertyRelative("clip").objectReferenceValue = state == null ? null : state.motion;
            slot.FindPropertyRelative("holdRaisedHand").boolValue = mage && (i == 2 || i == 4);
            slot.FindPropertyRelative("start").floatValue = 0f;
            slot.FindPropertyRelative("impact").floatValue = impacts[i];
        }
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // 기존 동작에서 손을 든 자세를 뽑는다. 원본 FBX·일반 준비·전투 타이밍은 유지.
    private static void BuildMagePresentation(AnimatorController controller)
    {
        var spec = Specs.First(s => s.name == "Mage");
        var sm = controller.layers[0].stateMachine;
        var source = AssetDatabase.LoadAssetAtPath<AnimationClip>(spec.controllerFolder + "/Mage_CastUpper.anim");
        float raisedTime = source.length * 0.42f;
        for (int kind = 0; kind < 3; kind++)
        {
            bool silence = kind == 2, hold = kind == 1;
            string name = silence ? "Mage_SilenceFront" : hold ? "Mage_ChargeHold" : "Mage_ChargeRaise";
            var input = silence ? Clip("Silence") : source;
            float duration = silence ? input.length : hold ? 2.4f : raisedTime;
            string path = spec.controllerFolder + "/" + name + ".anim";
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip == null) { clip = Object.Instantiate(input); AssetDatabase.CreateAsset(clip, path); }
            else EditorUtility.CopySerialized(input, clip);
            clip.name = name;
            clip.ClearCurves();
            int count = Mathf.CeilToInt(duration * 60f);
            foreach (var binding in AnimationUtility.GetCurveBindings(input))
            {
                var curve = AnimationUtility.GetEditorCurve(input, binding);
                var keys = new Keyframe[count + 1];
                for (int i = 0; i <= count; i++)
                {
                    float time = duration * i / count;
                    float value = curve.Evaluate(hold ? raisedTime : time);
                    string n = binding.propertyName;
                    if (hold)
                    {
                        float breath = Mathf.Sin(time / duration * Mathf.PI * 2f);
                        if (n == "Spine Front-Back") value += breath * 0.018f;
                        if (n == "Chest Front-Back") value += breath * 0.012f;
                        if (n == "Right Arm Down-Up") value += breath * 0.008f;
                    }
                    if (silence)
                    {
                        float turn = Mathf.Sin(Mathf.PI * time / duration);
                        if (n == "Spine Twist Left-Right") value += turn * 0.45f;
                        if (n == "Chest Twist Left-Right") value += turn * 0.25f;
                        if (n == "UpperChest Twist Left-Right") value += turn * 0.25f;
                    }
                    keys[i] = new Keyframe(time, value);
                }
                AnimationUtility.SetEditorCurve(clip, binding, new AnimationCurve(keys));
            }
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.startTime = 0f; settings.stopTime = duration; settings.loopTime = hold;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            if (hold) BakeFixedHips(spec, clip);
            EditorUtility.SetDirty(clip); AssetDatabase.SaveAssetIfDirty(clip);
            string stateName = silence ? "Fire1" : hold ? "CastChargeHold" : "CastChargeRaise";
            var state = sm.states.Select(c => c.state).FirstOrDefault(s => s.name == stateName)
                ?? sm.AddState(stateName, new Vector3(500f, hold ? -310f : -220f));
            state.motion = clip;
            state.timeParameterActive = !hold;
            state.timeParameter = silence ? "AttackTime" : hold ? "" : "CastTime";
            EditorUtility.SetDirty(state);
        }
    }

    [MenuItem("Tools/Scroll Hunter/Apply Mage Skill Motions")]
    public static void ApplyMageSkillMotions()
    {
        var spec = Specs.First(s => s.name == "Mage");
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(spec.controllerFolder + "/BOSS_Mage_ALL_v01.controller");
        var sm = controller.layers[0].stateMachine;
        string[] files = { spec.fire0, spec.fire1, spec.fire2, "Nightmare", "TheEnd" };
        for (int i = 0; i < files.Length; i++)
        {
            var state = sm.states.Select(s => s.state).FirstOrDefault(s => s.name == "Fire" + i)
                ?? sm.AddState("Fire" + i, new Vector3(750f, -160f + i * 90f));
            state.motion = Clip(files[i]);
            EditorUtility.SetDirty(state);
        }
        var cast = sm.states.First(s => s.state.name == "Cast").state.motion as AnimationClip;
        var settings = AnimationUtility.GetAnimationClipSettings(cast);
        settings.loopTime = false;
        AnimationUtility.SetAnimationClipSettings(cast, settings);
        EditorUtility.SetDirty(cast); AssetDatabase.SaveAssetIfDirty(cast);
        BuildMagePresentation(controller);
        ConfigureAttackController(controller);
        foreach (var child in sm.states) EditorUtility.SetDirty(child.state);
        AssetDatabase.SaveAssetIfDirty(controller);
        var root = PrefabUtility.LoadPrefabContents(spec.prefab);
        try
        {
            ConfigureAttackDriver(root.GetComponent<EnemyAnimationDriver>(), controller, spec.name);
            PrefabUtility.SaveAsPrefabAsset(root, spec.prefab);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    [MenuItem("Tools/Scroll Hunter/Apply Enemy Attack Timing")]
    public static void ApplyAttackTiming()
    {
        foreach (var spec in Specs)
        {
            string path = $"{spec.controllerFolder}/{System.IO.Path.GetFileNameWithoutExtension(spec.prefab)}.controller";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            ConfigureAttackController(controller);
            AssetDatabase.SaveAssetIfDirty(controller);
            var root = PrefabUtility.LoadPrefabContents(spec.prefab);
            try
            {
                ConfigureAttackDriver(root.GetComponent<EnemyAnimationDriver>(), controller, spec.name);
                PrefabUtility.SaveAsPrefabAsset(root, spec.prefab);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        ApplyArcherCombatLink();
        Debug.Log("[적 동작] 캐스팅 타격 시점·차단 전용 피격 적용");
    }

    // 궁병 컨트롤러·프리팹(10/6): 궁병 빌더를 다시 돌리지 않고 동작 연결 컴포넌트를 유지한다.
    [MenuItem("Tools/Scroll Hunter/Apply Archer Combat Link")]
    public static void ApplyArcherCombatLink()
    {
        const string controllerPath = "Assets/_Project/Animations/Archer/ENM_Archer_ORG_v01.controller";
        const string prefabPath = "Assets/_Project/Prefabs/Enemies/ENM_Archer_ORG_v01.prefab";
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        var layers = controller.layers;
        layers[0].iKPass = true;
        controller.layers = layers;
        AnimationClip StateClip(string state) =>
            controller.layers[0].stateMachine.states.Select(s => s.state).First(s => s.name == state).motion as AnimationClip;
        for (int i = controller.layers.Length - 1; i > 0; i--)
            if (controller.layers[i].name == "Flinch") controller.RemoveLayer(i);
        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssetIfDirty(controller);

        var root = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            var driver = root.GetComponent<EnemyAnimationDriver>();
            if (driver == null) driver = root.AddComponent<EnemyAnimationDriver>();
            var so = new SerializedObject(driver);
            so.FindProperty("animator").objectReferenceValue = root.GetComponent<Animator>();
            so.FindProperty("bow").objectReferenceValue = root.GetComponentInChildren<BowRig>(true);
            so.FindProperty("drawClip").objectReferenceValue = StateClip("Draw");
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        Debug.Log("[궁병] 차단 전용 피격·동작 연결 적용");
    }
}
