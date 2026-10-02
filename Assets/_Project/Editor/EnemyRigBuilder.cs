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
///    무기 손은 Grip 층(손가락만)으로 늘 쥔 자세를 덮어쓴다. 전투 연결은 다음 단계.
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
        public string idle, cast, fire0, fire1, fire2, hit, death;
    }

    private static readonly Spec[] Specs =
    {
        new Spec { name = "Thug", model = "Assets/_Project/Models/Enemies/Thug/ENM_Thug_ALL_v01.fbx", prefab = "Assets/_Project/Prefabs/Enemies/ENM_Thug_ALL_v01.prefab",
            controllerFolder = "Assets/_Project/Animations/Thug", idle = "MX_BreathingIdle", cast = "MX_FightingIdle",
            fire0 = "MX_PunchingJab", fire1 = "MX_CrossPunch", fire2 = "MX_ZombieKicking", hit = "MX_HitReaction", death = "MX_FallingBackDeath" },
        new Spec { name = "Raider", model = "Assets/_Project/Models/Enemies/Raider/ENM_Raider_GRN_v01.fbx", prefab = "Assets/_Project/Prefabs/Enemies/ENM_Raider_GRN_v01.prefab",
            controllerFolder = "Assets/_Project/Animations/Raider", weapon = "WPN_Dagger_v01", weaponHand = HumanBodyBones.RightHand, reverseGrip = true, gripRadius = 0.0085f,
            idle = "MX_BreathingIdle", cast = "MX_KnifeIdle", fire0 = "MX_StabbingReverse", hit = "MX_HitReaction", death = "MX_SwordShieldDeath" },
        new Spec { name = "Leader", model = "Assets/_Project/Models/Enemies/Leader/ENM_Leader_RED_v01.fbx", prefab = "Assets/_Project/Prefabs/Enemies/ENM_Leader_RED_v01.prefab",
            controllerFolder = "Assets/_Project/Animations/Leader", weapon = "WPN_Greatsword_v01", weaponHand = HumanBodyBones.RightHand, gripBothHands = true, gripRadius = 0.0105f,
            idle = "MX_GreatSwordIdle", cast = "MX_GreatSwordBlocking", fire0 = "MX_GreatSwordDownwardSlash", fire2 = "MX_GreatSwordPowerSlash",
            hit = "MX_GreatSwordImpact", death = "MX_TwoHandedSwordDeath" },
        new Spec { name = "Mage", model = "Assets/_Project/Models/Enemies/Mage/BOSS_Mage_ALL_v01.fbx", prefab = "Assets/_Project/Prefabs/Enemies/BOSS_Mage_ALL_v01.prefab",
            controllerFolder = "Assets/_Project/Animations/Mage", weapon = "WPN_Staff_v01", weaponHand = HumanBodyBones.LeftHand, gripRadius = 0.0125f,
            idle = "MX_MagicIdle03", cast = "MX_1HCastSpell01", fire0 = "MX_1HMagicAttack01", fire1 = "MX_2HMagicAttack01", fire2 = "MX_2HMagicAreaAttack02",
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
        AssetDatabase.SaveAssets();
        Debug.Log("[적 리그] 깡패·약탈자·우두머리·마법사 프리팹·컨트롤러 생성");
    }

    private static IEnumerable<string> Clips(Spec s) => new[] { s.idle, s.cast, s.fire0, s.fire1, s.fire2, s.hit, s.death }.Where(c => c != null);

    public static AnimationClip Clip(string file) =>
        AssetDatabase.LoadAllAssetsAtPath(MixamoFolder + file + ".fbx").OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview__"));

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
            string path = MixamoFolder + file + ".fbx";
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
    private static Dictionary<string, float> GripMuscles(string side)
    {
        var map = new Dictionary<string, float>();
        foreach (string f in new[] { "Index", "Middle", "Ring", "Little" })
            for (int k = 1; k <= 3; k++) map[$"{side} {f} {k} Stretched"] = GripCurl;
        for (int k = 1; k <= 3; k++) map[$"{side} Thumb {k} Stretched"] = GripThumb;
        return map;
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
                var handler = new HumanPoseHandler(animator.avatar, root.transform);
                var pose = new HumanPose(); handler.GetHumanPose(ref pose);
                var bind = (float[])pose.muscles.Clone();
                foreach (var kv in GripMuscles(side)) pose.muscles[System.Array.IndexOf(HumanTrait.MuscleName, kv.Key)] = kv.Value;
                handler.SetHumanPose(ref pose);

                Transform B(string bone) => animator.GetBoneTransform((HumanBodyBones)System.Enum.Parse(typeof(HumanBodyBones), side + bone));
                Vector3 tunnel = (B("IndexProximal").position - B("LittleProximal").position).normalized;
                Vector3 forearm = Vector3.ProjectOnPlane(B("MiddleProximal").position - hand.position, tunnel).normalized;
                Vector3 e2 = Vector3.Cross(tunnel, forearm);
                var pts = new List<Vector2>();
                foreach (string f in new[] { "Index", "Middle", "Ring", "Little" })
                    foreach (string seg in new[] { "Proximal", "Intermediate", "Distal" })
                    {
                        Vector3 d = B(f + seg).position - hand.position;
                        pts.Add(new Vector2(Vector3.Dot(d, forearm), Vector3.Dot(d, e2)));
                    }
                Vector2 c = FitCircle(pts);
                float along = new[] { "Index", "Middle", "Ring", "Little" }.Average(f => Vector3.Dot(B(f + "Intermediate").position - hand.position, tunnel));
                Vector3 center = hand.position + forearm * c.x + e2 * c.y + tunnel * along;

                var socket = new GameObject("WeaponSocket").transform;
                socket.SetParent(hand, false);
                socket.position = center;
                Vector3 length = spec.reverseGrip ? -tunnel : tunnel;
                // 쥔 손가락 원이 손잡이보다 작으면 손잡이가 손가락을 뚫는다. 모든 손가락 관절이 (반지름 + 손가락 두께) 밖에 오도록 손바닥 쪽으로 옮긴다.
                float need = spec.gripRadius + FingerThickness;
                for (int it = 0; it < 30; it++)
                {
                    float min = float.MaxValue;
                    foreach (string f in new[] { "Index", "Middle", "Ring", "Little" })
                        foreach (string seg in new[] { "Proximal", "Intermediate", "Distal" })
                            min = Mathf.Min(min, Vector3.ProjectOnPlane(B(f + seg).position - socket.position, length).magnitude);
                    if (min >= need - 0.0005f) break;
                    socket.position += Vector3.ProjectOnPlane(hand.position - socket.position, length).normalized * (need - min);
                }
                socket.rotation = Quaternion.LookRotation(length, Vector3.Cross(length, forearm));
                socket.localScale = Vector3.one / hand.lossyScale.x;
                var weapon = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(WeaponFolder + spec.weapon + ".fbx"), scene);
                weapon.transform.SetParent(socket, false);

                pose.muscles = bind; handler.SetHumanPose(ref pose);
                handler.Dispose();

                // 양손 무기: 양손 공격 프레임에서 반대 손이 손잡이 선 위에 오도록 길이 방향을 (반대 손 → 무기 손)의 평균으로 맞춘다.
                if (spec.gripBothHands)
                {
                    Transform other = animator.GetBoneTransform(spec.weaponHand == HumanBodyBones.RightHand ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
                    Transform otherMiddle = animator.GetBoneTransform(spec.weaponHand == HumanBodyBones.RightHand ? HumanBodyBones.LeftMiddleProximal : HumanBodyBones.RightMiddleProximal);
                    Vector3 localDir = Vector3.zero;
                    foreach (string file in new[] { spec.fire0, spec.fire2 }.Where(f => f != null))
                    {
                        AnimationClip clip = Clip(file);
                        for (int i = 2; i <= 8; i++)
                        {
                            clip.SampleAnimation(root, clip.length * i / 10f);
                            Vector3 otherGrip = (other.position + otherMiddle.position) * 0.5f;
                            localDir += hand.InverseTransformDirection((socket.position - otherGrip).normalized);
                        }
                    }
                    ArcherAnimationBuilder.RestoreModelPose(root);
                    Vector3 axis = hand.TransformDirection(localDir.normalized);
                    socket.rotation = Quaternion.LookRotation(axis, Vector3.Cross(axis, forearm));
                    // 반대 손 IK 목표: 무기 손 아래 손잡이(무기 길이 방향 -Z로 0.09).
                    var offTarget = new GameObject("OffHandTarget").transform;
                    offTarget.SetParent(weapon.transform, false);
                    offTarget.localPosition = new Vector3(0f, 0f, -0.09f);
                    var ik = root.AddComponent<OffHandGrip>();
                    var so = new SerializedObject(ik);
                    so.FindProperty("target").objectReferenceValue = offTarget;
                    so.FindProperty("goal").intValue = (int)(spec.weaponHand == HumanBodyBones.RightHand ? AvatarIKGoal.LeftHand : AvatarIKGoal.RightHand);
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
            }
            ArcherAnimationBuilder.RestoreModelPose(root);
            root.transform.localScale = Vector3.one * 2f;
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
        var hit = State("Hit", spec.hit, new Vector2(250, 220));
        var death = State("Death", spec.death, new Vector2(500, 260));
        sm.defaultState = idle;
        Link(idle, cast, 0.15f).AddCondition(AnimatorConditionMode.If, 0f, "Cast");
        string[] fires = { spec.fire0, spec.fire1, spec.fire2 };
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
                foreach (var kv in GripMuscles(side))
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
        var layers = controller.layers;
        layers[0].iKPass = spec.gripBothHands;   // 양손 무기의 반대 손 IK(OffHandGrip)
        controller.layers = layers;
        EditorUtility.SetDirty(controller);

        var root = PrefabUtility.LoadPrefabContents(spec.prefab);
        try
        {
            var animator = root.GetComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            PrefabUtility.SaveAsPrefabAsset(root, spec.prefab);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
}
