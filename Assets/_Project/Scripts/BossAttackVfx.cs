using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 마법사 보스 공격 연출(10/6, 10 A42). EnemyAnimationDriver의 표시용 알림만 읽는다.
///   - 시전 시작(AttackPrepared): 공격 목록 순서 번호를 받아 그 시전의 준비 효과를 1개 만든다. 같은 색 공격도 번호로 구분한다.
///   - 시전 중: 준비 효과를 손·머리 위 기준점에 두고, 실제 시전 진행률(2페이즈 배율 포함)로 크기·입자량을 키운다.
///     동시 발동 간격 대기에는 진행률 1로 유지된다.
///   - 실제 발동(AttackImpact): 시전 시작 때 받은 번호로 방출·폭발·투사체를 1회 만든다. 발동 순간 CurrentAttack을 읽지 않는다.
///   - 차단 성공·사망·편성 끄기·재활성화(AttackCanceled): 준비 효과·투사체·남은 효과를 모두 지운다. 빨강 차단 실패는 알림이 없어 그대로 진행한다.
/// 피해·기절·차단 판정을 부르지 않는다. 파티클과 이동은 게임 시간이라 정지·0.5배속을 따른다.
/// </summary>
[RequireComponent(typeof(EnemyAnimationDriver))]
public class BossAttackVfx : MonoBehaviour
{
    public enum Anchor { RightHand, LeftHand, HandsMid, Chest, AboveHead, Charge }

    [System.Serializable]
    public class Skill
    {
        [Tooltip("확인용 이름. EnemyData 공격 목록 순서와 같게 둔다")]
        public string label;

        [Header("준비 (시전당 1개)")]
        [Tooltip("시전 동안 유지하는 효과. 자식 'Grow'만 성장 배율로 커지고, 'HandLink'는 오른손에서 효과 쪽으로 향한다")]
        public GameObject charge;
        [Tooltip("준비 효과 전체 크기 배율")] public float chargeSize = 1f;
        public Anchor chargeAnchor;
        [Tooltip("기준점에서의 거리(키 2 모델 기준 m, 모델 방향 기준, -Z가 카메라 쪽)")]
        public Vector3 chargeOffset;
        [Tooltip("시전 진행 0일 때 'Grow' 배율")] public float chargeStartScale = 0.2f;
        [Tooltip("시전 진행 1일 때 'Grow' 배율")] public float chargeEndScale = 1f;
        [Tooltip("시전 진행 → 성장(0~1)")] public AnimationCurve growth = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [Tooltip("시전 시작 때 입자량 비율(진행 1에서 1)")]
        [Range(0f, 1f)] public float startEmission = 0.3f;
        [Tooltip("기준점을 따라가는 시간(초, 0이면 즉시)")] public float follow;

        [Header("방출 (실제 발동 1회)")]
        public GameObject release;
        [Tooltip("방출·투사체·착탄 크기 배율")] public float releaseSize = 1f;
        public Anchor releaseAnchor;
        public Vector3 releaseOffset;
        [Tooltip("모델 발밑에 함께 만드는 효과(바닥 충격파 등, 선택)")]
        public GameObject groundRelease;
        [Tooltip("화면 전체로 퍼지는 효과(ScreenBlastVfx, 선택). 준비 효과 위치의 화면 좌표에서 시작한다. 퍼짐·불투명도·유지·사라짐은 그 프리팹에서 조정")]
        public GameObject screenRelease;
        [Tooltip("방출 때 준비 효과가 줄어들며 사라지는 시간")] public float chargeCollapse = 0.12f;

        [Header("투사체 (선택)")]
        public GameObject projectile;
        public GameObject projectileImpact;
        [Tooltip("출발점에서 카메라까지 중 날아갈 비율")]
        [Range(0.1f, 0.95f)] public float projectileTravel = 0.5f;
        [Tooltip("비행 시간(연출). 피해는 발동 순간 이미 적용된다")] public float projectileSeconds = 0.2f;
        [Tooltip("겨냥점 = 카메라 위치 + 이 값(월드 m). 아래로 내리면 화면 아래쪽(플레이어 쪽)으로 날아가 궤적이 보인다")]
        public Vector3 projectileAim = new Vector3(0f, -1.6f, 0f);
    }

    private class Flight
    {
        public Transform transform;
        public Vector3 from, to;
        public float time, duration, fade;
        public GameObject impact;
        public float size;
        public bool arrived;
    }

    private class Collapse
    {
        public GameObject go;
        public Vector3 scale;
        public float time, duration;
    }

    [SerializeField] private EnemyAnimationDriver driver;
    [SerializeField] private Animator animator;
    [Tooltip("공격 목록 순서(매직미사일·사일런스·다크홀·나이트메어·디엔드)")]
    [SerializeField] private Skill[] skills = new Skill[5];

    [Header("기준점")]
    [Tooltip("손목 뼈에서 손바닥 쪽으로 미는 거리(키 2 모델 기준 m)")]
    [SerializeField] private float palmForward = 0.08f;
    [Tooltip("머리 위 기준점 높이(머리 뼈에서, 키 2 모델 기준 m). 보스 게이지·타겟 마커 위 빈 공간에 오도록 둔다")]
    [SerializeField] private float aboveHeadHeight = 2.55f;
    [Tooltip("머리 위 기준점을 오른손 쪽으로 옮기는 비율(수평만)")]
    [Range(0f, 1f)] [SerializeField] private float aboveHeadHandBlend = 0.35f;

    private Enemy enemy;
    private int skillIndex = -1, pendingRelease = -1;
    private GameObject chargeObject;
    private Transform grow, handLink;
    private ParticleSystem[] chargeSystems;
    private float[] baseRates;
    private Vector3 chargeVelocity;
    private bool chargePlaced;
    private bool finishing;
    public bool HasFinishingEffect => flights.Count > 0 || collapses.Count > 0 || spawned.Exists(FinishingVfx.IsAlive);
    private readonly List<GameObject> spawned = new List<GameObject>();
    private readonly List<Flight> flights = new List<Flight>();
    private readonly List<Collapse> collapses = new List<Collapse>();

    /// <summary>현재 준비 효과(검사용).</summary>
    public GameObject ChargeObject => chargeObject;
    /// <summary>시전 시작 때 받은 공격 번호(검사용). 없으면 -1.</summary>
    public int PreparedSkill => skillIndex;
    /// <summary>방출·폭발·투사체 등 남은 효과 수(준비 효과 제외, 검사용).</summary>
    public int SpawnedCount { get { spawned.RemoveAll(s => s == null); return spawned.Count; } }
    public int FlightCount => flights.Count;
    /// <summary>현재 준비 효과 'Grow' 배율(검사용).</summary>
    public float ChargeScale => grow != null ? grow.localScale.x : 0f;

    private float ModelScale => transform.lossyScale.y / 2f;   // 키 2 모델 기준으로 만든 크기

    private void Awake()
    {
        if (driver == null) driver = GetComponent<EnemyAnimationDriver>();
        if (animator == null) animator = GetComponent<Animator>();
        enemy = GetComponentInParent<Enemy>(true);
    }

    private void OnEnable()
    {
        driver.AttackPrepared += OnPrepared;
        driver.AttackImpact += OnImpact;
        driver.AttackCanceled += Clear;
        Clear();
    }

    private void OnDisable()
    {
        driver.AttackPrepared -= OnPrepared;
        driver.AttackImpact -= OnImpact;
        driver.AttackCanceled -= Clear;
        Clear();
    }

    private Skill Get(int index) => index >= 0 && skills != null && index < skills.Length ? skills[index] : null;

    private void OnPrepared(int index)
    {
        pendingRelease = -1;
        DestroyCharge();
        skillIndex = index;
        Skill skill = Get(index);
        if (skill == null || skill.charge == null) return;
        chargeObject = Instantiate(skill.charge, AnchorPosition(skill.chargeAnchor, skill.chargeOffset), transform.rotation);
        chargeObject.transform.localScale = skill.charge.transform.localScale * ModelScale * skill.chargeSize;
        grow = chargeObject.transform.Find("Grow");
        handLink = chargeObject.transform.Find("HandLink");
        chargeSystems = chargeObject.GetComponentsInChildren<ParticleSystem>(true);
        baseRates = new float[chargeSystems.Length];
        for (int i = 0; i < chargeSystems.Length; i++) baseRates[i] = chargeSystems[i].emission.rateOverTimeMultiplier;
        chargeVelocity = Vector3.zero;
        chargePlaced = false;
        UpdateCharge();
    }

    private void OnImpact(int colorIndex)
    {
        // 페이즈 전환이 발동 직전에 CurrentAttack을 비울 수 있으므로 시전 시작 때 받은 번호를 쓴다.
        int index = skillIndex >= 0 ? skillIndex : driver.MotionIndex;
        if (Get(index) != null) pendingRelease = index;   // 손 위치는 이 프레임 Animator 평가 뒤에 확정된다
    }

    private void LateUpdate()
    {
        UpdateCharge();
        if (pendingRelease >= 0) Release(pendingRelease);
        UpdateFlights();
        UpdateCollapses();
    }

    private void UpdateCharge()
    {
        if (chargeObject == null) return;
        Skill skill = Get(skillIndex);
        if (skill == null) return;
        float progress = enemy != null && enemy.IsCasting ? enemy.CastProgress01 : 1f;
        float k = Mathf.Clamp01(skill.growth.Evaluate(progress));

        Vector3 target = AnchorPosition(skill.chargeAnchor, skill.chargeOffset);
        Transform t = chargeObject.transform;
        if (!chargePlaced || skill.follow <= 0f) { t.position = target; chargePlaced = true; }
        else if (Time.deltaTime > 0f) t.position = Vector3.SmoothDamp(t.position, target, ref chargeVelocity, skill.follow, Mathf.Infinity, Time.deltaTime);

        if (grow != null) grow.localScale = Vector3.one * Mathf.Lerp(skill.chargeStartScale, skill.chargeEndScale, k);
        float rate = Mathf.Lerp(skill.startEmission, 1f, k);
        for (int i = 0; i < chargeSystems.Length; i++)
        {
            if (chargeSystems[i] == null) continue;
            var emission = chargeSystems[i].emission;
            emission.rateOverTimeMultiplier = baseRates[i] * rate;
        }

        if (handLink != null)
        {
            Vector3 hand = Palm(HumanBodyBones.RightHand, HumanBodyBones.RightLowerArm);
            Vector3 toCharge = t.position - hand;
            handLink.position = hand;
            if (toCharge.sqrMagnitude > 1e-6f) handLink.rotation = Quaternion.LookRotation(toCharge);
            foreach (var link in handLink.GetComponentsInChildren<ParticleSystem>())
            {
                var main = link.main;
                float life = Mathf.Max(0.05f, main.startLifetime.constant);
                main.startSpeed = toCharge.magnitude / life;   // 월드 공간·자체 크기 기준(부모 배율 무시)이라 수명 안에 효과에 닿는다
            }
        }
    }

    private void Release(int index)
    {
        pendingRelease = -1;
        Skill skill = Get(index);
        if (skill == null) return;
        Vector3 chargePosition = chargeObject != null ? chargeObject.transform.position : AnchorPosition(skill.chargeAnchor, skill.chargeOffset);
        if (skill.release != null)
        {
            Vector3 at = skill.releaseAnchor == Anchor.Charge ? chargePosition + Offset(skill.releaseOffset)
                : AnchorPosition(skill.releaseAnchor, skill.releaseOffset);
            Spawn(skill.release, at, skill.releaseSize);
        }
        if (skill.groundRelease != null) Spawn(skill.groundRelease, transform.position, skill.releaseSize);
        if (skill.screenRelease != null)
        {
            GameObject screen = Instantiate(skill.screenRelease);   // 카메라 기준 크기라 모델 배율을 쓰지 않는다
            spawned.Add(screen);
            var blast = screen.GetComponent<ScreenBlastVfx>();
            if (blast != null) blast.Begin(Camera.main, chargePosition);
        }
        if (skill.projectile != null)
        {
            Vector3 from = skill.releaseAnchor == Anchor.Charge ? chargePosition : AnchorPosition(skill.releaseAnchor, skill.releaseOffset);
            Camera cam = Camera.main;
            Vector3 to = cam != null ? Vector3.Lerp(from, cam.transform.position + skill.projectileAim, skill.projectileTravel) : from - transform.forward * 3f;
            GameObject shot = Spawn(skill.projectile, from, skill.releaseSize);
            shot.transform.rotation = Quaternion.LookRotation(to - from);
            flights.Add(new Flight { transform = shot.transform, from = from, to = to, duration = Mathf.Max(0.02f, skill.projectileSeconds), impact = skill.projectileImpact, size = skill.releaseSize });
        }
        if (chargeObject != null)
        {
            StopEmitting(chargeObject);
            collapses.Add(new Collapse { go = chargeObject, scale = chargeObject.transform.localScale, duration = Mathf.Max(0.01f, skill.chargeCollapse) });
            spawned.Add(chargeObject);
            chargeObject = null; grow = handLink = null; chargeSystems = null;
        }
        skillIndex = -1;
    }

    private void UpdateFlights()
    {
        float dt = finishing ? Time.unscaledDeltaTime : Time.deltaTime;
        if (dt <= 0f) return;
        for (int i = flights.Count - 1; i >= 0; i--)
        {
            Flight f = flights[i];
            if (f.transform == null) { flights.RemoveAt(i); continue; }
            if (!f.arrived)
            {
                f.time += dt;
                float u = Mathf.Clamp01(f.time / f.duration);
                f.transform.position = Vector3.Lerp(f.from, f.to, u);
                if (u < 1f) continue;
                f.arrived = true;
                if (f.impact != null) Spawn(f.impact, f.to, f.size);
                StopEmitting(f.transform.gameObject);
                foreach (var r in f.transform.GetComponentsInChildren<TrailRenderer>()) r.emitting = false;
                continue;
            }
            f.fade += dt;
            if (f.fade < 0.5f) continue;   // 남은 꼬리 입자가 사라질 시간
            Destroy(f.transform.gameObject);
            flights.RemoveAt(i);
        }
    }

    private void UpdateCollapses()
    {
        float dt = finishing ? Time.unscaledDeltaTime : Time.deltaTime;
        if (dt <= 0f) return;
        for (int i = collapses.Count - 1; i >= 0; i--)
        {
            Collapse c = collapses[i];
            if (c.go == null) { collapses.RemoveAt(i); continue; }
            c.time += dt;
            float u = Mathf.Clamp01(c.time / c.duration);
            c.go.transform.localScale = c.scale * (1f - u);
            if (u < 1f) continue;
            Destroy(c.go);
            collapses.RemoveAt(i);
        }
    }

    private Vector3 Offset(Vector3 offset) => transform.rotation * offset * ModelScale;

    private Vector3 Palm(HumanBodyBones hand, HumanBodyBones lowerArm)
    {
        Transform h = animator.GetBoneTransform(hand), a = animator.GetBoneTransform(lowerArm);
        if (h == null) return transform.position;
        Vector3 along = a != null ? (h.position - a.position).normalized : Vector3.zero;
        return h.position + along * palmForward * ModelScale;
    }

    private Vector3 AnchorPosition(Anchor anchor, Vector3 offset)
    {
        Vector3 p;
        switch (anchor)
        {
            case Anchor.LeftHand: p = Palm(HumanBodyBones.LeftHand, HumanBodyBones.LeftLowerArm); break;
            case Anchor.HandsMid:
                p = (Palm(HumanBodyBones.RightHand, HumanBodyBones.RightLowerArm) + Palm(HumanBodyBones.LeftHand, HumanBodyBones.LeftLowerArm)) * 0.5f; break;
            case Anchor.Chest:
            {
                Transform c = animator.GetBoneTransform(HumanBodyBones.UpperChest);
                if (c == null) c = animator.GetBoneTransform(HumanBodyBones.Chest);
                p = c != null ? c.position : transform.position; break;
            }
            case Anchor.AboveHead:
            {
                Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
                Vector3 h = head != null ? head.position : transform.position;
                Vector3 hand = Palm(HumanBodyBones.RightHand, HumanBodyBones.RightLowerArm);
                Vector3 side = hand - h; side.y = 0f;
                p = h + side * aboveHeadHandBlend + Vector3.up * aboveHeadHeight * ModelScale; break;
            }
            case Anchor.Charge:
                p = chargeObject != null ? chargeObject.transform.position : transform.position; break;
            default: p = Palm(HumanBodyBones.RightHand, HumanBodyBones.RightLowerArm); break;
        }
        return p + Offset(offset);
    }

    private GameObject Spawn(GameObject prefab, Vector3 position, float size)
    {
        GameObject fx = Instantiate(prefab, position, transform.rotation);
        fx.transform.localScale = prefab.transform.localScale * ModelScale * size;
        spawned.Add(fx);
        if (finishing) FinishingVfx.PlayUnscaled(fx);
        return fx;
    }

    private static void StopEmitting(GameObject go)
    {
        foreach (var ps in go.GetComponentsInChildren<ParticleSystem>()) ps.Stop(false, ParticleSystemStopBehavior.StopEmitting);
    }

    private void DestroyCharge()
    {
        if (chargeObject != null) Destroy(chargeObject);
        chargeObject = null; grow = handLink = null; chargeSystems = null;
    }

    /// <summary>실제 발동이 예약된 효과는 먼저 만들고, 미발동 준비 효과만 지운다.</summary>
    public void PlayOutUnscaled()
    {
        if (pendingRelease >= 0) Release(pendingRelease);
        DestroyCharge();
        skillIndex = -1;
        finishing = true;
        foreach (var fx in spawned) FinishingVfx.PlayUnscaled(fx);
    }

    /// <summary>준비 효과·예약 방출·투사체·남은 효과를 모두 지운다.</summary>
    public void Clear()
    {
        finishing = false;
        pendingRelease = -1;
        skillIndex = -1;
        DestroyCharge();
        foreach (Flight f in flights) if (f.transform != null) Destroy(f.transform.gameObject);
        flights.Clear();
        collapses.Clear();
        foreach (GameObject fx in spawned) if (fx != null) Destroy(fx);
        spawned.Clear();
    }
}
