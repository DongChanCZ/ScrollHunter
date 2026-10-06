using UnityEngine;

/// <summary>
/// 궁병 활 연출(10/2). 시위 당김 정도·활대 휨·손에 든 화살·발사 화살만 표시한다.
/// 피해·판정·충돌은 없다. 전투 피해와 시점은 Enemy·Player가 기존대로 처리한다.
/// </summary>
public class BowRig : MonoBehaviour
{
    [SerializeField] private LineRenderer bowString;
    [SerializeField] private Transform upperLimb;
    [SerializeField] private Transform lowerLimb;
    [SerializeField] private Transform upperTip;
    [SerializeField] private Transform lowerTip;

    [Tooltip("당길 때 시위 가운데가 따라갈 지점(오른손 화살 오늬 소켓)")]
    [SerializeField] private Transform drawPoint;

    [Tooltip("최대로 당겼을 때 위·아래 활대가 시위 쪽으로 휘는 각도")]
    [SerializeField] private float maxLimbBend = 10f;

    [Tooltip("당김 값이 이 값에 이르면 시위 가운데가 손에 완전히 붙는다. 활대 휨은 당김 값에 비례")]
    [SerializeField] private float attachDraw = 0.15f;

    [Tooltip("손에 든 준비 화살. 놓으면 숨기고 ReadyArrow로 다시 보인다.")]
    [SerializeField] private GameObject nockedArrow;

    [Tooltip("발사 연출용 화살 프리팹(ArrowFlight, 충돌체 없음)")]
    [SerializeField] private ArrowFlight arrowProjectile;

    [Tooltip("놓은 뒤 시위가 제자리로 돌아가는 시간")]
    [SerializeField] private float releaseSeconds = 0.06f;

    [Tooltip("시위 당김을 읽을 Animator. 클립의 BowDraw 곡선이 같은 이름의 Float 파라미터를 움직인다. 비우면 Draw 값을 직접 쓴다.")]
    [SerializeField] private Animator drawAnimator;
    [SerializeField] private string drawParameter = "BowDraw";

    [Range(0f, 1f)] [SerializeField] private float draw;
    private int drawHash;
    private bool readAnimator, released;

    private Quaternion upperRest, lowerRest;
    private Vector3 restCenter;
    private float releaseFrom, releaseTimer;

    /// <summary>시위 당김 정도 0~1. 동작 연결 시 애니메이션 진행에 맞춰 넣는다.</summary>
    public float Draw
    {
        get => draw;
        set { draw = Mathf.Clamp01(value); releaseTimer = 0f; }
    }

    public bool ArrowReady => nockedArrow != null && nockedArrow.activeSelf;

    /// <summary>시위 가운데가 따라가는 오늬 지점(읽기 전용, 발사 연출 방향용).</summary>
    public Transform NockPoint => drawPoint;

    private void Awake()
    {
        upperRest = upperLimb.localRotation;
        lowerRest = lowerLimb.localRotation;
        restCenter = (bowString.GetPosition(0) + bowString.GetPosition(2)) * 0.5f;
        drawHash = Animator.StringToHash(drawParameter);
        if (drawAnimator != null && drawAnimator.runtimeAnimatorController != null)
            foreach (AnimatorControllerParameter p in drawAnimator.parameters)
                if (p.nameHash == drawHash && p.type == AnimatorControllerParameterType.Float) readAnimator = true;
    }

    public void ReadyArrow()
    {
        if (nockedArrow != null) nockedArrow.SetActive(true);
    }

    /// <summary>손의 화살을 숨긴다(사망 등). 발사 연출은 만들지 않는다.</summary>
    public void HideArrow()
    {
        if (nockedArrow != null) nockedArrow.SetActive(false);
    }

    /// <summary>시위를 놓고 손의 화살을 숨긴다. 발사 연출 화살을 target까지 날려 돌려준다. 피해는 없다.</summary>
    public ArrowFlight Release(Vector3 target, float flightSeconds)
    {
        releaseFrom = draw;
        releaseTimer = releaseSeconds;
        released = readAnimator;
        if (!ArrowReady) return null;
        Transform hand = nockedArrow.transform;
        nockedArrow.SetActive(false);
        if (arrowProjectile == null) return null;
        ArrowFlight shot = Instantiate(arrowProjectile, hand.position, Quaternion.LookRotation(target - hand.position));
        shot.transform.localScale = hand.lossyScale;
        shot.Launch(target, flightSeconds);
        return shot;
    }

    private void LateUpdate()
    {
        if (releaseTimer > 0f)
        {
            releaseTimer = Mathf.Max(0f, releaseTimer - Time.deltaTime);
            draw = releaseSeconds > 0f ? releaseFrom * (releaseTimer / releaseSeconds) : 0f;
        }
        else if (readAnimator)
        {
            // 놓은 뒤에는 Animator 곡선이 한 번 0 근처로 내려올 때까지 다시 당기지 않는다(발사 동작 앞부분의 당김 값 무시).
            float animated = Mathf.Clamp01(drawAnimator.GetFloat(drawHash));
            if (released && animated < 0.05f) released = false;
            draw = released ? 0f : animated;
        }
        upperLimb.localRotation = upperRest * Quaternion.AngleAxis(-maxLimbBend * draw, Vector3.right);
        lowerLimb.localRotation = lowerRest * Quaternion.AngleAxis(maxLimbBend * draw, Vector3.right);

        Transform space = bowString.transform;
        float attach = attachDraw > 0f ? Mathf.Clamp01(draw / attachDraw) : (draw > 0f ? 1f : 0f);
        Vector3 center = drawPoint != null ? Vector3.Lerp(restCenter, space.InverseTransformPoint(drawPoint.position), attach) : restCenter;
        bowString.SetPosition(0, space.InverseTransformPoint(upperTip.position));
        bowString.SetPosition(1, center);
        bowString.SetPosition(2, space.InverseTransformPoint(lowerTip.position));
    }
}
