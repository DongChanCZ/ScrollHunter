using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// 궁병 기본 동작 생성(10/2, 07 D-13). 외부 동작 없이 Humanoid 근육 값 자세를 키프레임으로 묶는다.
/// 자세 값은 Unity에서 손·활·화살 위치를 맞춘 결과(07 기록). 제자리 동작이며 루트 이동은 없다.
/// 시전 시간은 바꾸지 않는다. 연결 때 DrawSpeed로 Draw 재생 속도를 시전 시간에 맞춘다.
/// </summary>
public static class ArcherAnimationBuilder
{
    private const string Folder = "Assets/_Project/Animations/Archer";
    private const string PrefabPath = "Assets/_Project/Prefabs/Enemies/ENM_Archer_ORG_v01.prefab";
    private const string ControllerPath = Folder + "/ENM_Archer_ORG_v01.controller";

    private static readonly Dictionary<string, float> Idle = new Dictionary<string, float>
    {
        { "Left Arm Down-Up", -0.68f }, { "Left Arm Front-Back", 0.03f }, { "Left Arm Twist In-Out", 0.12f }, { "Left Forearm Stretch", 0.91f },
        { "Left Forearm Twist In-Out", -0.15f }, { "Left Hand Down-Up", -0.73f }, { "Left Hand In-Out", -1f },
        { "Right Arm Down-Up", -1f }, { "Right Arm Front-Back", 0.56f }, { "Right Arm Twist In-Out", 0.06f }, { "Right Forearm Stretch", 0.29f },
        { "Right Forearm Twist In-Out", 0.28f }, { "Right Hand Down-Up", 0.12f }, { "Right Hand In-Out", 0.09f },
    };

    private static readonly Dictionary<string, float> BowArm = new Dictionary<string, float>
    {
        { "Left Arm Down-Up", 0.06f }, { "Left Arm Front-Back", -0.51f }, { "Left Arm Twist In-Out", 0.16f }, { "Left Forearm Stretch", 0.69f },
        { "Left Forearm Twist In-Out", 0.37f }, { "Left Hand Down-Up", 0.66f }, { "Left Hand In-Out", 0.67f },
    };

    private static readonly Dictionary<string, float> FullDraw = new Dictionary<string, float>
    {
        { "Right Arm Down-Up", 0.03f }, { "Right Arm Front-Back", 0.87f }, { "Right Arm Twist In-Out", 1f }, { "Right Forearm Stretch", -0.66f },
        { "Right Forearm Twist In-Out", 0.83f }, { "Right Hand Down-Up", -0.70f }, { "Right Hand In-Out", -0.57f },
    };

    // 활 뒤 0.45 지점을 노린 중간 당김. 기본 근육 범위에서 오늬 오차 약 0.14(키 2 기준)는 남는다.
    private static readonly Dictionary<string, float> MidDraw = new Dictionary<string, float>
    {
        { "Right Arm Down-Up", 1f }, { "Right Arm Front-Back", 1f }, { "Right Arm Twist In-Out", 1f }, { "Right Forearm Stretch", -0.73f },
        { "Right Forearm Twist In-Out", 0.61f }, { "Right Hand Down-Up", 0.15f }, { "Right Hand In-Out", 0.25f },
    };

    private static readonly Dictionary<string, float> Follow = new Dictionary<string, float>
    {
        { "Right Arm Down-Up", 0.13f }, { "Right Arm Front-Back", 0f }, { "Right Arm Twist In-Out", 1f }, { "Right Forearm Stretch", -0.71f },
        { "Right Forearm Twist In-Out", 1f }, { "Right Hand Down-Up", -0.26f }, { "Right Hand In-Out", -0.98f },
    };

    private static readonly Dictionary<string, float> Breath = new Dictionary<string, float>
    {
        { "Chest Front-Back", -0.04f }, { "Spine Front-Back", -0.02f }, { "Head Nod Down-Up", 0.03f },
    };

    private static readonly Dictionary<string, float> Flinch = new Dictionary<string, float>
    {
        { "Spine Front-Back", -0.35f }, { "Chest Front-Back", -0.3f }, { "Head Nod Down-Up", 0.35f }, { "Neck Nod Down-Up", 0.2f },
        { "Left Arm Down-Up", -0.35f }, { "Left Arm Front-Back", -0.2f }, { "Right Arm Down-Up", -0.55f }, { "Right Arm Front-Back", 0.1f },
        { "Left Forearm Stretch", 0.4f }, { "Right Forearm Stretch", 0.2f }, { "Left Upper Leg Front-Back", 0.45f }, { "Right Upper Leg Front-Back", 0.45f },
        { "Left Lower Leg Stretch", 0.8f }, { "Right Lower Leg Stretch", 0.8f },
    };

    // 사망 중간: 뒤로 주저앉음. 다리 Front-Back은 음수가 앞, 팔 Front-Back은 양수가 뒤.
    private static readonly Dictionary<string, float> Slump = new Dictionary<string, float>
    {
        { "Left Upper Leg Front-Back", -0.55f }, { "Right Upper Leg Front-Back", -0.35f }, { "Left Lower Leg Stretch", -0.2f }, { "Right Lower Leg Stretch", 0.1f },
        { "Spine Front-Back", 0.2f }, { "Chest Front-Back", 0.1f }, { "Head Nod Down-Up", 0.25f },
        { "Left Arm Down-Up", -0.5f }, { "Right Arm Down-Up", -0.6f }, { "Left Arm Front-Back", 0.4f }, { "Right Arm Front-Back", 0.4f },
        { "Left Forearm Stretch", 0.6f }, { "Right Forearm Stretch", 0.6f },
    };

    private static readonly Dictionary<string, float> Lying = new Dictionary<string, float>
    {
        { "Left Upper Leg Front-Back", 0.55f }, { "Right Upper Leg Front-Back", 0.5f }, { "Left Lower Leg Stretch", 0.9f }, { "Right Lower Leg Stretch", 0.7f },
        { "Left Upper Leg In-Out", 0.3f }, { "Right Upper Leg In-Out", 0.2f }, { "Spine Front-Back", 0f }, { "Chest Front-Back", 0f }, { "Head Turn Left-Right", 0.4f },
        { "Left Arm Down-Up", -0.3f }, { "Right Arm Down-Up", -0.3f }, { "Left Arm Front-Back", 0.35f }, { "Right Arm Front-Back", 0.35f },
        { "Left Forearm Stretch", 0.7f }, { "Right Forearm Stretch", 0.7f },
    };

    // 손가락: 왼손은 항상 활 손잡이를 쥔다. 오른손은 쉼·시위 걸기·놓음.
    private static readonly Dictionary<string, float> LeftGrip = Fingers("Left", -0.6f, -0.6f, -0.3f);
    private static readonly Dictionary<string, float> RightRelax = Fingers("Right", -0.35f, -0.35f, -0.2f);
    private static readonly Dictionary<string, float> RightHook = new Dictionary<string, float>
    {
        { "Right Index 1 Stretched", 0.2f }, { "Right Index 2 Stretched", -0.2f }, { "Right Index 3 Stretched", -0.2f },
        { "Right Middle 1 Stretched", 0.2f }, { "Right Middle 2 Stretched", -0.2f }, { "Right Middle 3 Stretched", -0.2f },
        { "Right Ring 1 Stretched", 0.2f }, { "Right Ring 2 Stretched", -0.2f }, { "Right Ring 3 Stretched", -0.2f },
        { "Right Little 1 Stretched", -0.4f }, { "Right Little 2 Stretched", -0.6f }, { "Right Little 3 Stretched", -0.6f },
        { "Right Thumb 1 Stretched", -0.2f }, { "Right Thumb 2 Stretched", -0.3f },
    };
    private static readonly Dictionary<string, float> RightOpen = Fingers("Right", 0.3f, 0.3f, 0.2f);

    private static Dictionary<string, float> Fingers(string side, float first, float rest, float thumb)
    {
        var map = new Dictionary<string, float>();
        foreach (string f in new[] { "Index", "Middle", "Ring", "Little" })
        {
            map[$"{side} {f} 1 Stretched"] = first;
            map[$"{side} {f} 2 Stretched"] = rest;
            map[$"{side} {f} 3 Stretched"] = rest;
        }
        map[$"{side} Thumb 1 Stretched"] = thumb;
        map[$"{side} Thumb 2 Stretched"] = thumb;
        map[$"{side} Thumb 3 Stretched"] = thumb;
        return map;
    }

    private struct Pose
    {
        public float[] muscles;
        public Vector3 body;
        public Quaternion rotation;
        public float draw;
    }

    [MenuItem("Tools/Scroll Hunter/Build Archer Animations")]
    public static void Build()
    {
        if (!AssetDatabase.IsValidFolder("Assets/_Project/Animations")) AssetDatabase.CreateFolder("Assets/_Project", "Animations");
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/_Project/Animations", "Archer");

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        var animator = instance.GetComponent<Animator>();
        var handler = new HumanPoseHandler(animator.avatar, instance.transform);
        try
        {
            var human = new HumanPose();
            handler.GetHumanPose(ref human);
            BuildClips(human.muscles, human.bodyPosition, human.bodyRotation, handler, animator);
        }
        finally
        {
            handler.Dispose();
            UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
        }
        BuildController();
        AssetDatabase.SaveAssets();
        Debug.Log("[궁병 동작] 클립 6개·Animator 생성");
    }

    // 가장 낮은 접지 지점(발바닥·무릎·엉덩이·가슴·머리·손 근사)이 바닥에 닿도록 몸 높이를 맞춘다.
    private static void Ground(ref Pose pose, HumanPoseHandler handler, Animator animator)
    {
        var human = new HumanPose { bodyPosition = pose.body, bodyRotation = pose.rotation, muscles = pose.muscles };
        handler.SetHumanPose(ref human);
        float Y(HumanBodyBones bone, float radius) => animator.GetBoneTransform(bone).position.y - radius;
        float lowest = new[]
        {
            Y(HumanBodyBones.LeftToes, 0.06f), Y(HumanBodyBones.RightToes, 0.06f), Y(HumanBodyBones.LeftFoot, 0.16f), Y(HumanBodyBones.RightFoot, 0.16f),
            Y(HumanBodyBones.LeftLowerLeg, 0.07f), Y(HumanBodyBones.RightLowerLeg, 0.07f), Y(HumanBodyBones.Hips, 0.14f),
            Y(HumanBodyBones.Chest, 0.14f), Y(HumanBodyBones.Head, 0.12f), Y(HumanBodyBones.LeftHand, 0.04f), Y(HumanBodyBones.RightHand, 0.04f),
        }.Min();
        pose.body.y -= lowest;
    }

    private static void BuildClips(float[] tpose, Vector3 body, Quaternion rotation, HumanPoseHandler handler, Animator animator)
    {
        Pose Make(float draw, params Dictionary<string, float>[] layers)
        {
            var m = (float[])tpose.Clone();
            foreach (var layer in layers)
                foreach (var pair in layer) m[System.Array.IndexOf(HumanTrait.MuscleName, pair.Key)] = pair.Value;
            return new Pose { muscles = m, body = body, rotation = rotation, draw = draw };
        }

        Pose idle = Make(0f, Idle, LeftGrip, RightRelax);
        Pose breath = Make(0f, Idle, Breath, LeftGrip, RightRelax);
        Pose raise = Make(0f, BowArm, MidDraw, LeftGrip, RightHook);   // 활을 들고 손을 시위로
        Pose nock = Make(0.45f, BowArm, MidDraw, LeftGrip, RightHook);  // 시위를 손에 걸고 당기기 시작
        Pose full = Make(1f, BowArm, FullDraw, LeftGrip, RightHook);
        Pose aimHold = Make(1f, BowArm, FullDraw, Breath, LeftGrip, RightHook);
        Pose follow = Make(0f, BowArm, Follow, LeftGrip, RightOpen);
        Pose flinch = Make(0f, Idle, Flinch, LeftGrip, RightRelax);
        flinch.body += rotation * new Vector3(0f, -0.03f, -0.05f);
        flinch.rotation = rotation * Quaternion.Euler(-8f, 0f, 0f);
        Ground(ref flinch, handler, animator);
        Pose slump = Make(0f, Slump, LeftGrip, RightRelax);
        slump.body += rotation * new Vector3(0f, 0f, -0.25f);
        slump.rotation = rotation * Quaternion.Euler(-35f, 0f, 0f);
        Ground(ref slump, handler, animator);
        Pose lying = Make(0f, Lying, Fingers("Left", -0.4f, -0.4f, -0.2f), RightRelax);
        lying.body = body + rotation * new Vector3(0f, 0f, -0.75f);
        lying.rotation = rotation * Quaternion.Euler(-86f, 0f, 0f);
        Ground(ref lying, handler, animator);

        Save("Archer_Idle", true, (0f, idle), (1f, breath), (2f, idle));
        Save("Archer_Draw", false, (0f, idle), (0.3f, raise), (0.45f, nock), (0.85f, full), (1f, full));
        Save("Archer_Aim", true, (0f, full), (0.5f, aimHold), (1f, full));
        Save("Archer_Release", false, (0f, full), (0.06f, follow), (0.45f, follow));
        Save("Archer_Hit", false, (0f, idle), (0.12f, flinch), (0.6f, idle));
        Save("Archer_Death", false, (0f, idle), (0.15f, flinch), (0.55f, slump), (1.1f, lying), (1.3f, lying));
    }

    private static string CurveName(string muscle)
    {
        foreach (string side in new[] { "Left", "Right" })
            foreach (string f in new[] { "Thumb", "Index", "Middle", "Ring", "Little" })
                if (muscle.StartsWith($"{side} {f} ")) return $"{side}Hand.{f}.{muscle.Substring(side.Length + f.Length + 2)}";
        return muscle;
    }

    private static void Save(string name, bool loop, params (float time, Pose pose)[] keys)
    {
        string path = $"{Folder}/{name}.anim";
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (clip == null) { clip = new AnimationClip(); AssetDatabase.CreateAsset(clip, path); }
        clip.ClearCurves();
        void Curve(string property, System.Func<Pose, float> value, string objectPath = "", System.Type type = null)
        {
            var curve = new AnimationCurve(keys.Select(k => new Keyframe(k.time, value(k.pose))).ToArray());
            for (int i = 0; i < curve.length; i++)
            {
                AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto);
                AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto);
            }
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(objectPath, type ?? typeof(Animator), property), curve);
        }
        for (int i = 0; i < HumanTrait.MuscleCount; i++)
        {
            int index = i;
            Curve(CurveName(HumanTrait.MuscleName[i]), p => p.muscles[index]);
        }
        Curve("RootT.x", p => p.body.x); Curve("RootT.y", p => p.body.y); Curve("RootT.z", p => p.body.z);
        Curve("RootQ.x", p => p.rotation.x); Curve("RootQ.y", p => p.rotation.y); Curve("RootQ.z", p => p.rotation.z); Curve("RootQ.w", p => p.rotation.w);
        Curve("BowDraw", p => p.draw);   // Animator Float 파라미터 → BowRig가 읽는다
        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = loop;
        // 제자리 동작: 몸 회전·높이·수평 이동을 모두 자세에 굽는다. 루트 모션으로 분리되면 쓰러질 때 높이가 버려진다.
        settings.loopBlendOrientation = settings.loopBlendPositionY = settings.loopBlendPositionXZ = true;
        settings.keepOriginalOrientation = settings.keepOriginalPositionY = settings.keepOriginalPositionXZ = true;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        EditorUtility.SetDirty(clip);
    }

    private static void BuildController()
    {
        // 지우고 새로 만들면 GUID가 바뀌어 프리팹 참조가 끊긴다. 있으면 비우고 다시 채운다.
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath) ?? AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        foreach (var p in controller.parameters) controller.RemoveParameter(p);
        var old = controller.layers[0].stateMachine;
        foreach (var t in old.anyStateTransitions) old.RemoveAnyStateTransition(t);
        foreach (var s in old.states) old.RemoveState(s.state);
        foreach (string trigger in new[] { "Draw", "Release", "Hit", "Death" }) controller.AddParameter(trigger, AnimatorControllerParameterType.Trigger);
        controller.AddParameter(new AnimatorControllerParameter { name = "DrawSpeed", type = AnimatorControllerParameterType.Float, defaultFloat = 1f });
        controller.AddParameter(new AnimatorControllerParameter { name = "BowDraw", type = AnimatorControllerParameterType.Float, defaultFloat = 0f });
        var sm = controller.layers[0].stateMachine;
        // Mixamo 클립이 반입돼 있으면 그것을, 없으면 직접 만든 클립을 쓴다.
        AnimatorState State(string clip, Vector2 at)
        {
            var s = sm.AddState(clip.Replace("Archer_", ""), at);
            string mixamo = MixamoClips.FirstOrDefault(m => m.state == s.name).file;
            s.motion = (mixamo != null ? MixamoClip(mixamo) : null) ?? AssetDatabase.LoadAssetAtPath<AnimationClip>($"{Folder}/{clip}.anim");
            return s;
        }
        var idle = State("Archer_Idle", new Vector2(250, 0));
        var draw = State("Archer_Draw", new Vector2(500, -100));
        var aim = State("Archer_Aim", new Vector2(750, -100));
        var release = State("Archer_Release", new Vector2(750, 50));
        var hit = State("Archer_Hit", new Vector2(250, 200));
        var death = State("Archer_Death", new Vector2(500, 250));
        draw.speedParameterActive = true; draw.speedParameter = "DrawSpeed";
        sm.defaultState = idle;

        AnimatorStateTransition Link(AnimatorState from, AnimatorState to, string trigger, float duration, float exitTime = -1f)
        {
            var t = from.AddTransition(to);
            t.duration = duration; t.hasFixedDuration = true;
            t.hasExitTime = exitTime >= 0f; if (t.hasExitTime) t.exitTime = exitTime;
            if (trigger != null) t.AddCondition(AnimatorConditionMode.If, 0f, trigger);
            return t;
        }
        Link(idle, draw, "Draw", 0.1f);
        Link(draw, aim, null, 0.05f, 1f);
        Link(draw, release, "Release", 0.02f);
        Link(aim, release, "Release", 0.02f);
        Link(release, idle, null, 0.25f, 1f);
        Link(hit, idle, null, 0.2f, 0.9f);
        // 피격은 살아 있는 상태에서만 받는다(Any State로 두면 사망 뒤 Hit가 다시 재생된다). 사망은 어디서나.
        foreach (var child in sm.states)
            if (child.state != hit && child.state != death) Link(child.state, hit, "Hit", 0.05f);
        var toDeath = sm.AddAnyStateTransition(death);
        toDeath.duration = 0.05f; toDeath.hasFixedDuration = true; toDeath.canTransitionToSelf = false;
        toDeath.AddCondition(AnimatorConditionMode.If, 0f, "Death");

        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            var animator = root.GetComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            var rig = new SerializedObject(root.GetComponentInChildren<BowRig>());
            rig.FindProperty("drawAnimator").objectReferenceValue = animator;
            rig.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    // ---------------- Mixamo(10/2 사용자 요청) ----------------
    // 원본: Docs/근거자료/2026-10-02_Mixamo원본/ (기본 캐릭터 akai_e_espiritu, FBX for Unity, Without Skin, 30fps)
    private const string MixamoFolder = Folder + "/Mixamo";
    private static readonly (string state, string file, bool loop)[] MixamoClips =
    {
        ("Idle", "MX_StandingIdle01", true), ("Draw", "MX_StandingDrawArrow", false), ("Aim", "MX_StandingAimIdle01", true),
        ("Release", "MX_StandingAimRecoil", false), ("Hit", "MX_StandingReactSmallFromFront", false), ("Death", "MX_StandingDeathBackward01", false),
    };
    private const float FullDrawDistance = 0.8f;   // 키 2 기준 활 받침에서 오늬까지 완전 당김 거리
    private const float StringReach = 0.12f;       // 오른손 오늬가 시위 선에서 이 거리 안이면 시위를 잡은 것으로 본다

    private static AnimationClip MixamoClip(string file) =>
        AssetDatabase.LoadAllAssetsAtPath($"{MixamoFolder}/{file}.fbx").OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview__"));

    /// <summary>
    /// Mixamo 활 동작 적용. ① 카메라(-Z) 쪽으로 쏘도록 회전 보정·수평 이동 제거 ② 조준 자세 기준으로 활·화살 장착 각도 재설정
    /// ③ 손 위치에서 BowDraw(시위 당김) 곡선 생성 ④ 컨트롤러를 Mixamo 클립으로 다시 연결.
    /// </summary>
    [MenuItem("Tools/Scroll Hunter/Apply Archer Mixamo Clips")]
    public static void ApplyMixamo()
    {
        // ① 회전 보정 0으로 조준 방향을 재서, 조준 방향이 -Z가 되는 보정 각을 모든 클립에 같은 값으로 넣는다.
        SetMixamoImport(0f, null);
        float aimYaw = MeasureAimYaw();
        float offset = Mathf.DeltaAngle(0f, 180f - aimYaw);
        var curves = new Dictionary<string, AnimationCurve>();
        SetMixamoImport(offset, curves);

        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            Transform Find(string n) => root.GetComponentsInChildren<Transform>(true).First(t => t.name == n);
            var animator = root.GetComponent<Animator>();
            Transform hand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            Transform bowSocket = Find("BowSocket"), arrowSocket = Find("ArrowSocket"), bow = bowSocket.GetChild(0), rest = Find("Bow_ArrowRest");

            // ② 조준 유지 중간 프레임: 활은 세우고 앞면을 조준 방향으로, 화살은 오늬에서 활 받침을 향하게.
            AnimationClip aim = MixamoClip("MX_StandingAimIdle01");
            aim.SampleAnimation(root, aim.length * 0.4f);
            // 활 앞면을 오늬→활 받침 방향으로 돌려 오늬가 시위 평면 위에 오게 한다(받침이 함께 움직이므로 몇 번 반복).
            for (int i = 0; i < 4; i++)
                bowSocket.rotation = Quaternion.LookRotation(rest.position - arrowSocket.position, Vector3.up);
            arrowSocket.rotation = Quaternion.LookRotation(rest.position - arrowSocket.position, Vector3.up);

            // ③ 클립마다 30fps로 오늬가 시위 선 가까이에서 얼마나 뒤로 당겨졌는지 BowDraw로 기록.
            Vector3 restLocal = new Vector3(0f, 0f, -0.0699f);
            foreach (var m in MixamoClips)
            {
                AnimationClip clip = MixamoClip(m.file);
                var keys = new List<Keyframe>();
                for (float t = 0f; t <= clip.length + 1e-4f; t += 1f / 30f)
                {
                    clip.SampleAnimation(root, Mathf.Min(t, clip.length));
                    Vector3 stringPoint = bow.TransformPoint(restLocal);
                    Vector3 toNock = arrowSocket.position - stringPoint;
                    float behind = -Vector3.Dot(toNock, bow.forward);
                    float side = Mathf.Abs(Vector3.Dot(toNock, bow.right));   // 시위 평면에서 벗어난 거리
                    float height = Mathf.Abs(Vector3.Dot(toNock, bow.up));    // 시위 위아래(평면 안이라 크게 허용)
                    float value = m.state is "Draw" or "Aim" or "Release" && side < StringReach && height < 0.3f && behind > -0.05f
                        ? Mathf.Clamp01(behind / FullDrawDistance) : 0f;
                    keys.Add(new Keyframe(Mathf.Min(t, clip.length), value));
                }
                curves[m.file] = new AnimationCurve(keys.ToArray());
            }
            RestoreModelPose(root);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }

        SetMixamoImport(offset, curves);
        BuildController();
        AssetDatabase.SaveAssets();
        Debug.Log($"[궁병 Mixamo] 조준 각 {aimYaw:0.0}° → 회전 보정 {offset:0.0}°, 클립 {MixamoClips.Length}개 연결");
    }

    private static void SetMixamoImport(float rotationOffset, Dictionary<string, AnimationCurve> curves)
    {
        foreach (var m in MixamoClips)
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath($"{MixamoFolder}/{m.file}.fbx");
            var clips = importer.clipAnimations.Length > 0 ? importer.clipAnimations : importer.defaultClipAnimations;
            foreach (var c in clips)
            {
                c.name = m.file.Replace("MX_", "Mixamo_");
                c.loopTime = m.loop;
                // 회전·높이는 자세에 굽히고(쓰러짐 유지), 수평 이동은 굽히지 않아 루트 모션으로 버려진다(제자리).
                c.lockRootRotation = c.lockRootHeightY = true;
                c.lockRootPositionXZ = false;
                c.keepOriginalOrientation = c.keepOriginalPositionY = true;
                c.rotationOffset = rotationOffset;
                c.curves = curves != null && curves.TryGetValue(m.file, out var curve)
                    ? new[] { new ClipAnimationInfoCurve { name = "BowDraw", curve = curve } }
                    : new ClipAnimationInfoCurve[0];
            }
            importer.clipAnimations = clips;
            importer.SaveAndReimport();
        }
    }

    /// <summary>
    /// 시험 샘플로 움직인 뼈를 모델 FBX의 원래 자세로 되돌린다(루트·추가한 소켓 등은 그대로).
    /// 저장 전에 부르지 않으면 마지막 샘플 자세가 프리팹 오버라이드로 남는다.
    /// </summary>
    public static void RestoreModelPose(GameObject root)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t == root.transform) continue;
            var source = PrefabUtility.GetCorrespondingObjectFromOriginalSource(t);
            if (source == null || !AssetDatabase.GetAssetPath(source).EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase)) continue;
            t.localPosition = source.localPosition;
            t.localRotation = source.localRotation;
            t.localScale = source.localScale;
        }
    }

    private static float MeasureAimYaw()
    {
        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            var animator = root.GetComponent<Animator>();
            AnimationClip aim = MixamoClip("MX_StandingAimIdle01");
            aim.SampleAnimation(root, aim.length * 0.4f);
            Vector3 d = animator.GetBoneTransform(HumanBodyBones.LeftHand).position - animator.GetBoneTransform(HumanBodyBones.RightHand).position;
            return Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
}
