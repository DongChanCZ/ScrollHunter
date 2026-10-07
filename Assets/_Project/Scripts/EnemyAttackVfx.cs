using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 적 공격 연출(10/6, 10 A41). EnemyAnimationDriver의 공격 진행(클립 위치)과 실제 발동 알림만 읽는다.
///   - 무기 궤적: 공격 클립 위치가 베기 구간 안일 때만 TrailRenderer를 켠다(대기·시전 준비·복귀에는 없음).
///   - 타격·발사 효과: 실제 발동 알림 때 한 번. 발동 프레임의 Animator 평가 뒤(LateUpdate) 타격 자세의 손·발·칼끝에 만든다.
///   - 차단 성공·사망·편성 끄기·재활성화 때 궤적과 남은 효과를 지운다.
/// 피해·차단 판정을 부르지 않으며 별도 타이머가 없다. 파티클·궤적은 게임 시간이라 정지·배속을 따른다.
/// </summary>
[RequireComponent(typeof(EnemyAnimationDriver))]
public class EnemyAttackVfx : MonoBehaviour
{
    [System.Serializable]
    public class Attack
    {
        [Tooltip("궤적을 켤 공격 클립 위치(0~1). 끝이 시작보다 작거나 같으면 궤적 없음")]
        [Range(0f, 1f)] public float trailStart;
        [Range(0f, 1f)] public float trailEnd;
        [Tooltip("실제 발동 때 한 번 만들 효과(파티클, 끝나면 스스로 사라짐)")]
        public GameObject impactEffect;
        [Tooltip("효과 위치. 손·발 뼈이거나, LastBone이면 무기 끝(weaponTip)")]
        public HumanBodyBones impactBone = HumanBodyBones.LastBone;
        [Tooltip("뼈 방향(부모 뼈 → 뼈)으로 앞쪽 거리(모델 키 1 기준)")]
        public float impactForward;
    }

    [SerializeField] private EnemyAnimationDriver driver;
    [SerializeField] private Animator animator;
    [Tooltip("칼날 끝쪽(85%)에 둔 궤적. 카메라를 향한 띠라 칼끝 경로·속도가 보인다")]
    [SerializeField] private TrailRenderer weaponTrail;
    [Tooltip("무기 끝 효과 위치(LastBone일 때)")]
    [SerializeField] private Transform weaponTip;
    [Tooltip("초록·주황·빨강. 빈 칸은 초록을 쓴다")]
    [SerializeField] private Attack[] attacks = new Attack[3];

    [Header("궁병")]
    [SerializeField] private BowRig bow;
    [Tooltip("화살이 활을 떠날 때 활 손잡이 앞쪽에 한 번")]
    [SerializeField] private GameObject releaseEffect;
    [SerializeField] private float releaseForward = 0.03f;

    private readonly List<GameObject> spawned = new List<GameObject>();
    private int pendingImpact = -1;
    private bool finishing;
    public bool HasFinishingEffect => spawned.Exists(FinishingVfx.IsAlive);

    public int SpawnedCount { get { spawned.RemoveAll(s => s == null); return spawned.Count; } }
    public bool TrailEmitting => weaponTrail != null && weaponTrail.emitting;

    private void Awake()
    {
        if (driver == null) driver = GetComponent<EnemyAnimationDriver>();
        if (animator == null) animator = GetComponent<Animator>();
        if (bow == null) bow = GetComponentInChildren<BowRig>(true);
    }

    private void OnEnable()
    {
        driver.AttackImpact += OnImpact;
        driver.AttackCanceled += Clear;
        Clear();
    }

    private void OnDisable()
    {
        driver.AttackImpact -= OnImpact;
        driver.AttackCanceled -= Clear;
        Clear();
    }

    private Attack Current(int index)
    {
        if (index < 0 || attacks == null || attacks.Length == 0) return null;
        Attack a = index < attacks.Length ? attacks[index] : null;
        if (a == null || (a.impactEffect == null && a.trailEnd <= a.trailStart)) a = attacks[0];
        return a;
    }

    private void OnImpact(int index)
    {
        if (bow != null)
        {
            if (releaseEffect != null && bow.NockPoint != null)
            {
                // 화살 출발 지점: 활 손잡이에서 오늬 반대쪽(날아가는 쪽)으로 조금 앞.
                Vector3 dir = (bow.transform.position - bow.NockPoint.position).normalized;
                Spawn(releaseEffect, bow.transform.position + dir * releaseForward * transform.lossyScale.y, Quaternion.LookRotation(dir));
            }
            return;
        }
        pendingImpact = index;   // 타격 자세는 이 프레임 Animator 평가 뒤에 확정된다
    }

    private void LateUpdate()
    {
        Attack attack = !finishing && driver.IsAttackMotionActive ? Current(driver.AttackIndex) : null;
        if (weaponTrail != null)
        {
            float t = driver.AttackNormalizedTime;
            bool on = attack != null && attack.trailEnd > attack.trailStart && t >= attack.trailStart && t <= attack.trailEnd;
            if (on && !weaponTrail.emitting) { weaponTrail.Clear(); weaponTrail.emitting = true; }
            else if (!on && weaponTrail.emitting) weaponTrail.emitting = false;
        }
        if (pendingImpact < 0) return;
        Attack hit = Current(pendingImpact);
        pendingImpact = -1;
        if (hit == null || hit.impactEffect == null) return;
        Transform at = hit.impactBone == HumanBodyBones.LastBone ? weaponTip : animator.GetBoneTransform(hit.impactBone);
        if (at == null) return;
        Vector3 forward = at.parent != null ? (at.position - at.parent.position).normalized : transform.forward;
        Spawn(hit.impactEffect, at.position + forward * hit.impactForward * transform.lossyScale.y, Quaternion.LookRotation(forward));
    }

    private void Spawn(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        GameObject fx = Instantiate(prefab, position, rotation);
        fx.transform.localScale = prefab.transform.localScale * (transform.lossyScale.y / 2f);   // 키 2 기준으로 만든 크기
        spawned.Add(fx);
    }

    public void PlayOutUnscaled()
    {
        finishing = true;
        LateUpdate(); // 이미 실제 발동된 예약 타격만 생성. 새 공격 판정은 없다.
        if (weaponTrail != null) { weaponTrail.emitting = false; weaponTrail.Clear(); }
        foreach (var fx in spawned) FinishingVfx.PlayUnscaled(fx);
    }

    /// <summary>궤적·예약 효과·남은 효과를 모두 지운다.</summary>
    public void Clear()
    {
        finishing = false;
        pendingImpact = -1;
        if (weaponTrail != null) { weaponTrail.emitting = false; weaponTrail.Clear(); }
        foreach (GameObject fx in spawned) if (fx != null) Destroy(fx);
        spawned.Clear();
    }
}
