using UnityEngine;

/// <summary>
/// 손 쥠 보정(10/6). Animator가 자세를 쓴 뒤(LateUpdate) 위팔·아래팔을 두 마디 IK로 돌려
/// 쥔 손의 손잡이 중심을 target(손잡이 축 위의 점, +Z = 검지 쪽)에 맞춘다.
/// 손은 손잡이 축만 맞추고, 축을 도는 회전은 아래팔 방향에 맞춰 매 프레임 고른다(손목이 과하게 꺾이지 않게).
/// 뼈 길이는 바꾸지 않는다. 닿지 않으면 손잡이 범위 안에서 어깨에 가장 가까운 점으로 옮기고, 그래도 멀면 팔을 편 만큼만 간다.
/// 표시 전용이며 판정·수치·타이머와 무관하다.
///   - 우두머리: 왼손을 대검 손잡이(OffHandTarget, 무기 아래)에. 차단 피격·사망에서는 끈다.
///   - 마법사: 대기 상태에서 왼손을 지팡이를 세워 짚는 자리(StaffIdleTarget, 엉덩이 아래)에.
/// </summary>
public class HandIK : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private bool leftHand = true;

    [Tooltip("손잡이 중심이 올 점. +Z가 손잡이 축(검지 쪽)")]
    [SerializeField] private Transform target;

    [Tooltip("닿지 않을 때 target +Z 방향으로 옮길 수 있는 범위(target 로컬). 손잡이를 따라 미끄러지는 범위")]
    [SerializeField] private float slideMin;
    [SerializeField] private float slideMax;

    [Header("쥔 손 기준(손 뼈 회전 기준, 루트 크기 1일 때의 길이). 빌더가 채운다")]
    [SerializeField] private Vector3 localTunnel = Vector3.forward;
    [SerializeField] private Vector3 localForearm = Vector3.right;
    [SerializeField] private Vector3 localToHand;

    [Tooltip("비우면 아래 제외 상태를 뺀 모든 상태에서 켠다")]
    [SerializeField] private string[] onlyStates = new string[0];
    [SerializeField] private string[] skipStates = new string[0];

    [Tooltip("초당 가중치 변화. 상태가 바뀔 때 손이 튀지 않게 한다")]
    [SerializeField] private float blendSpeed = 8f;

    private Transform upper, lower, hand;
    private int[] onlyHashes, skipHashes;
    private float weight;
    private Quaternion localFrameInverse;

    public float Weight => weight;
    public Transform Target => target;

    /// <summary>마지막 풀이에서 손잡이 중심이 목표 점에 못 미친 거리(월드).</summary>
    public float LastMiss { get; private set; }

    private void Awake()
    {
        if (animator == null) animator = GetComponent<Animator>();
        upper = animator.GetBoneTransform(leftHand ? HumanBodyBones.LeftUpperArm : HumanBodyBones.RightUpperArm);
        lower = animator.GetBoneTransform(leftHand ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm);
        hand = animator.GetBoneTransform(leftHand ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
        onlyHashes = Hashes(onlyStates);
        skipHashes = Hashes(skipStates);
        localFrameInverse = Quaternion.Inverse(Quaternion.LookRotation(localTunnel, Vector3.Cross(localTunnel, localForearm)));
    }

    private void OnEnable() => Snap();

    /// <summary>현재 상태의 목표 가중치로 바로 맞춘다(재활성화·검사 화면용).</summary>
    public void Snap()
    {
        if (animator != null && onlyHashes != null) weight = TargetWeight();
    }

    private void LateUpdate()
    {
        if (target == null || upper == null) return;
        float dt = Time.deltaTime;
        weight = Mathf.MoveTowards(weight, TargetWeight(), blendSpeed * dt);
        // 정지 중(dt 0) 부분 가중치를 다시 섞으면 이전 결과에 누적되므로 완전 적용일 때만 다시 푼다.
        if (weight <= 0.001f || (dt <= 0f && weight < 0.999f)) return;
        Solve(weight);
    }

    private float TargetWeight()
    {
        if (animator == null || animator.runtimeAnimatorController == null || !animator.isInitialized) return 0f;
        // 전환 중에는 다음 상태 기준으로 미리 바꾼다(전환 시간과 가중치 변화가 함께 진행).
        AnimatorStateInfo info = animator.IsInTransition(0) ? animator.GetNextAnimatorStateInfo(0) : animator.GetCurrentAnimatorStateInfo(0);
        foreach (int h in skipHashes) if (info.shortNameHash == h) return 0f;
        if (onlyHashes.Length == 0) return 1f;
        foreach (int h in onlyHashes) if (info.shortNameHash == h) return 1f;
        return 0f;
    }

    private void Solve(float w)
    {
        Quaternion upperAnim = upper.localRotation, lowerAnim = lower.localRotation, handAnim = hand.localRotation;
        float scale = transform.lossyScale.x;
        Vector3 shoulder = upper.position;
        float la = Vector3.Distance(shoulder, lower.position), lb = Vector3.Distance(lower.position, hand.position);
        Vector3 axis = target.forward;
        Vector3 toHand = localToHand * scale;

        // 손잡이 중심: 닿지 않으면 손잡이 범위 안에서 어깨에 가장 가까운 점.
        Vector3 center = target.position;
        if (slideMax > slideMin)
        {
            Vector3 along = axis * target.lossyScale.z;
            float s = Mathf.Clamp(Vector3.Dot(shoulder - center, along) / along.sqrMagnitude, slideMin, slideMax);
            Vector3 slid = center + along * s;
            if (Vector3.Distance(shoulder, center) > (la + lb) * 0.98f) center = slid;
        }

        Quaternion handRotation = hand.rotation;
        Vector3 goal = hand.position;
        for (int pass = 0; pass < 3; pass++)
        {
            // 손등(팔) 방향을 지금 아래팔 방향에 맞추되 손잡이 축에 수직으로. 축을 도는 회전만 자유롭다.
            Vector3 forearm = Vector3.ProjectOnPlane(goal - lower.position, axis);
            if (forearm.sqrMagnitude < 1e-10f) forearm = Vector3.ProjectOnPlane(hand.rotation * localForearm, axis);
            handRotation = Quaternion.LookRotation(axis, Vector3.Cross(axis, forearm.normalized)) * localFrameInverse;
            goal = center + handRotation * toHand;
            TwoBone(shoulder, goal, la, lb);
        }
        hand.rotation = handRotation;
        LastMiss = Vector3.Distance(hand.position, goal);

        if (w < 0.999f)
        {
            upper.localRotation = Quaternion.Slerp(upperAnim, upper.localRotation, w);
            lower.localRotation = Quaternion.Slerp(lowerAnim, lower.localRotation, w);
            hand.localRotation = Quaternion.Slerp(handAnim, hand.localRotation, w);
        }
    }

    // 손목이 goal에 오도록 위팔·아래팔을 돌린다. 팔꿈치는 지금 굽은 쪽을 유지한다.
    private void TwoBone(Vector3 shoulder, Vector3 goal, float la, float lb)
    {
        Vector3 toGoal = goal - shoulder;
        float lt = Mathf.Clamp(toGoal.magnitude, Mathf.Abs(la - lb) + 1e-5f, la + lb - 1e-5f);
        Vector3 dir = toGoal.normalized;
        Vector3 pole = Vector3.ProjectOnPlane(lower.position - shoulder, dir);
        if (pole.sqrMagnitude < 1e-10f) pole = Vector3.ProjectOnPlane(-transform.up, dir);
        pole.Normalize();
        float cosA = Mathf.Clamp((la * la + lt * lt - lb * lb) / (2f * la * lt), -1f, 1f);
        Vector3 elbow = shoulder + la * (cosA * dir + Mathf.Sqrt(1f - cosA * cosA) * pole);
        upper.rotation = Quaternion.FromToRotation(lower.position - shoulder, elbow - shoulder) * upper.rotation;
        lower.rotation = Quaternion.FromToRotation(hand.position - lower.position, goal - lower.position) * lower.rotation;
    }

    private static int[] Hashes(string[] names)
    {
        var result = new int[names.Length];
        for (int i = 0; i < names.Length; i++) result[i] = Animator.StringToHash(names[i]);
        return result;
    }
}
