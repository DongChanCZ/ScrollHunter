using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 적 모델 표시만 처리한다(10 A40). Enemy의 시전 진행으로 공격을 미리 재생하고 실제 발동에 타격 자세를 맞춘다.
/// 일반 피해에는 움찔하지 않으며 차단 성공만 Hit. 사망 모델은 분리하고 전투 전환 때 되돌린다.
/// 판정·시전 시간·피해·경직은 Enemy가 처리한다. 발동 이후 자세 복귀만 게임 시간으로 진행한다.
/// </summary>
[RequireComponent(typeof(Animator))]
public class EnemyAnimationDriver : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [Tooltip("비우면 부모에서 찾는다")]
    [SerializeField] private Enemy enemy;
    [SerializeField] private float crossFade = 0.12f;

    [System.Serializable]
    public class AttackMotion
    {
        public AnimationClip clip;
        [Tooltip("손을 올린 뒤 호흡하며 준비 자세 유지")]
        public bool holdRaisedHand;
        [Tooltip("공격 준비 시작 위치(클립 0~1)")]
        [Range(0f, 1f)] public float start;
        [Tooltip("실제 피해와 맞출 타격/방출 자세(클립 0~1)")]
        [Range(0.01f, 0.99f)] public float impact = 0.5f;
    }

    [Header("공격 타이밍 (초록·주황·빨강, 빈 칸은 초록 사용)")]
    [SerializeField] private AttackMotion[] attacks = new AttackMotion[3];
    [Tooltip("켜면 색 대신 EnemyData의 공격 목록 순서로 Fire0~ 상태를 선택한다")]
    [SerializeField] private bool useAttackOrder;
    [Tooltip("준비 동작을 한 번만 재생하고 마지막 자세 유지. CastTime으로 구동하는 Cast 상태에서 사용")]
    [SerializeField] private AnimationClip castOnceClip;
    [SerializeField] private AnimationClip chargeRaiseClip;

    [Header("궁병 (활이 있을 때만)")]
    [SerializeField] private BowRig bow;
    [Tooltip("Draw 상태 클립. 당김 속도 = 길이 / 실제 시전 시간")]
    [SerializeField] private AnimationClip drawClip;
    [SerializeField] private string drawSpeedParameter = "DrawSpeed";
    [Tooltip("발사 화살이 날아가는 시간(연출). 피해는 발동 순간 이미 적용된다")]
    [SerializeField] private float arrowFlightSeconds = 0.18f;
    [Tooltip("활에서 카메라까지 중 화살이 날아갈 비율")]
    [Range(0.1f, 0.95f)] [SerializeField] private float arrowTravel = 0.7f;

    private static readonly int IdleState = Animator.StringToHash("Idle");
    private static readonly int CastState = Animator.StringToHash("Cast");
    private static readonly int DrawState = Animator.StringToHash("Draw");
    private static readonly int ReleaseState = Animator.StringToHash("Release");
    private static readonly int HitState = Animator.StringToHash("Hit");
    private static readonly int DeathState = Animator.StringToHash("Death");
    private static readonly int AttackTime = Animator.StringToHash("AttackTime");
    private static readonly int CastTime = Animator.StringToHash("CastTime");
    private static readonly int ChargeRaiseState = Animator.StringToHash("CastChargeRaise");
    private static readonly int ChargeHoldState = Animator.StringToHash("CastChargeHold");

    private readonly List<ArrowFlight> arrows = new List<ArrowFlight>();
    private Transform home;
    private Vector3 homePosition, homeScale;
    private Quaternion homeRotation;
    private bool detached, dead, subscribed;
    private AttackMotion motion;
    private int attackState, firedFrame;
    private bool attacking, recovering, holdingCharge;
    private float motionTime;
    private Camera lookCamera;
    private float lookWeight = 1f;

    // 대기·시전 때만 카메라를 본다. 몸·무기는 건드리지 않고 공격/피격/사망 자세로 부드럽게 넘긴다.
    private void OnAnimatorIK(int layerIndex)
    {
        if (layerIndex != 0 || animator == null || !animator.isHuman) return;
        if (lookCamera == null) lookCamera = Camera.main;
        var state = animator.IsInTransition(0) ? animator.GetNextAnimatorStateInfo(0) : animator.GetCurrentAnimatorStateInfo(0);
        bool looking = !dead && lookCamera != null
            && (state.IsName("Idle") || state.IsName("Cast") || state.IsName("CastChargeRaise") || state.IsName("CastChargeHold") || state.IsName("Draw") || state.IsName("Aim"));
        lookWeight = Mathf.MoveTowards(lookWeight, looking ? 1f : 0f, Time.deltaTime / 0.12f);
        animator.SetLookAtWeight(lookCamera == null ? 0f : lookWeight, 0f, 1f, 0f, 0.45f);
        if (lookCamera != null) animator.SetLookAtPosition(lookCamera.transform.position);
    }

    public bool IsAttackMotionActive => attacking;
    public float AttackNormalizedTime => motionTime;

    // 적 공격 연출용 표시 알림(10/6, EnemyAttackVfx). 판정과 무관하다.
    /// <summary>실제 발동 직후(공격 번호 0 초록·1 주황·2 빨강). 이 프레임 Animator 평가 뒤 타격 자세가 된다.</summary>
    public event System.Action<int> AttackImpact;
    /// <summary>공격 연출을 지워야 할 때(차단 성공·사망·편성 끄기·재활성화·비활성).</summary>
    public event System.Action AttackCanceled;
    /// <summary>지금 준비·진행 중인 공격 번호. 없으면 -1.</summary>
    public int AttackIndex { get; private set; } = -1;
    /// <summary>시전 시작 때 고른 공격 동작 번호(useAttackOrder면 EnemyData 공격 목록 순서). 발동·복귀까지 유지, 없으면 -1.</summary>
    public int MotionIndex { get; private set; } = -1;
    /// <summary>시전 시작 직후(공격 동작 번호). 보스 공격 연출이 시전 단위로 준비 효과를 만든다(10/6, BossAttackVfx).</summary>
    public event System.Action<int> AttackPrepared;

    public bool IsDetached => detached;
    public bool IsDead => dead;
    public int ArrowsInFlight { get { arrows.RemoveAll(a => a == null); return arrows.Count; } }

    private void Awake()
    {
        if (animator == null) animator = GetComponent<Animator>();
        if (enemy == null) enemy = GetComponentInParent<Enemy>(true);
        if (bow == null) bow = GetComponentInChildren<BowRig>(true);
        home = transform.parent;
        homePosition = transform.localPosition;
        homeRotation = transform.localRotation;
        homeScale = transform.localScale;
        // 모델 기본값(보이지 않으면 뼈 갱신 생략)이면 재활성화 때 숨긴 동안 자세가 쓰이지 않는다. 적은 최대 3체라 항상 갱신한다.
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        Subscribe(true);
    }

    private void OnDestroy()
    {
        Subscribe(false);
        ClearArrows();
    }

    private void OnEnable() => ResetPose();

    private void OnDisable()
    {
        CancelAttack();
        AttackCanceled?.Invoke();
        ClearArrows();
        SetHidden(false);   // 꺼진 채로 숨김 목록이 남지 않게(다음 OnEnable에서 다시 숨긴다)
    }

    private void Subscribe(bool on)
    {
        if (enemy == null || subscribed == on) return;
        subscribed = on;
        if (on)
        {
            enemy.CastStarted += OnCastStarted; enemy.AttackFired += OnAttackFired; enemy.Interrupted += OnInterrupted;
            enemy.CastAdvanced += OnCastAdvanced; enemy.Died += OnDied; enemy.EncounterActiveChanging += OnEncounterActiveChanging;
        }
        else
        {
            enemy.CastStarted -= OnCastStarted; enemy.AttackFired -= OnAttackFired; enemy.Interrupted -= OnInterrupted;
            enemy.CastAdvanced -= OnCastAdvanced; enemy.Died -= OnDied; enemy.EncounterActiveChanging -= OnEncounterActiveChanging;
        }
    }

    /// <summary>
    /// 대기 자세로 되돌린다. 다시 켜진 프레임에는 Animator가 아직 자세를 쓰지 못해 T자나 이전 사망 자세가 남으므로,
    /// Animator가 한 번 평가될 때까지(LateUpdate) 모델을 숨긴다. 카운트다운(게임 시간 0)에서도 평가는 일어난다.
    /// </summary>
    public void ResetPose()
    {
        if (animator == null || !animator.isActiveAndEnabled || animator.runtimeAnimatorController == null) return;
        dead = false;
        lookWeight = 1f;
        CancelAttack();
        AttackCanceled?.Invoke();
        ClearArrows();
        animator.updateMode = AnimatorUpdateMode.Normal;
        animator.Rebind();
        // 구 컨트롤러를 쓰더라도 일반 피해용 상체 층은 표시하지 않는다.
        int flinch = animator.GetLayerIndex("Flinch");
        if (flinch >= 0) animator.SetLayerWeight(flinch, 0f);
        animator.Play(IdleState, 0, 0f);
        animator.Update(0f);
        if (bow != null) bow.ReadyArrow();
        SetHidden(true);
    }

    private readonly List<Renderer> hidden = new List<Renderer>();
    private int hiddenFrame;

    private void SetHidden(bool hide)
    {
        if (hide)
        {
            hiddenFrame = Time.frameCount;
            if (hidden.Count > 0) return;
            foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
                if (r.enabled) { r.enabled = false; hidden.Add(r); }
            return;
        }
        foreach (Renderer r in hidden) if (r != null) r.enabled = true;
        hidden.Clear();
    }

    private void LateUpdate()
    {
        // 다시 켜진 프레임에는 Animator가 아직 자세를 쓰지 못한다(10/6 실프레임 확인). 항상 갱신(AlwaysAnimate)이라
        // 숨긴 동안에도 다음 프레임에 대기 자세가 쓰이므로, 그 프레임의 LateUpdate(평가 뒤)에 보인다.
        if (hidden.Count > 0 && animator.isInitialized && Time.frameCount > hiddenFrame) SetHidden(false);
    }

    /// <summary>재활성화 뒤 첫 평가 전까지 숨겨져 있는지(검사용).</summary>
    public bool IsHiddenUntilPosed => hidden.Count > 0;

    private bool CanAct => !dead && isActiveAndEnabled && animator.runtimeAnimatorController != null;

    private void OnCastStarted()
    {
        if (!CanAct) return;
        CancelAttack();
        if (bow != null)
        {
            float cast = Mathf.Max(0.05f, enemy.CurrentCastTime);
            if (drawClip != null) animator.SetFloat(drawSpeedParameter, drawClip.length / cast);
            bow.ReadyArrow();
            animator.CrossFadeInFixedTime(DrawState, 0.1f, 0, 0f);
            return;
        }
        SelectAttack(enemy.CastColor);
        animator.CrossFadeInFixedTime(motion != null && motion.holdRaisedHand ? ChargeRaiseState : CastState, crossFade, 0, 0f);
        OnCastAdvanced();
        AttackPrepared?.Invoke(MotionIndex);
    }

    private void SelectAttack(CastColor color)
    {
        int colorIndex = color == CastColor.Orange ? 1 : color == CastColor.Red ? 2 : 0;
        int index = colorIndex;
        if (useAttackOrder && enemy != null && enemy.Data != null)
            for (int i = 0; i < enemy.Data.Attacks.Count; i++)
                if (enemy.Data.Attacks[i] == enemy.CurrentAttack) { index = i; break; }
        if (index >= attacks.Length || attacks[index] == null || attacks[index].clip == null
            || !animator.HasState(0, Animator.StringToHash("Fire" + index))) index = 0;
        motion = attacks.Length > index ? attacks[index] : null;
        attackState = Animator.StringToHash("Fire" + index);
        AttackIndex = colorIndex; // VFX 알림은 기존 초록·주황·빨강 번호를 유지한다.
        MotionIndex = index;
    }

    private void OnCastAdvanced()
    {
        if (!CanAct || bow != null || !enemy.IsCasting || motion == null || motion.clip == null) return;
        float start = Mathf.Min(motion.start, motion.impact);
        float windup = Mathf.Min(enemy.CurrentCastTime, (motion.impact - start) * motion.clip.length);
        float remaining = (1f - enemy.CastProgress01) * enemy.CurrentCastTime;
        if (castOnceClip != null)
        {
            var prepareClip = motion.holdRaisedHand && chargeRaiseClip != null ? chargeRaiseClip : castOnceClip;
            float preparation = Mathf.Min(prepareClip.length, enemy.CurrentCastTime - windup);
            float elapsed = enemy.CastProgress01 * enemy.CurrentCastTime;
            animator.SetFloat(CastTime, preparation > 0f ? Mathf.Clamp01(elapsed / preparation) : 1f);
            if (motion.holdRaisedHand && !holdingCharge && elapsed >= preparation && remaining > windup)
            {
                holdingCharge = true;
                animator.CrossFadeInFixedTime(ChargeHoldState, crossFade, 0, 0f);
            }
        }
        if (remaining > windup) return;
        float progress = windup > 0f ? 1f - remaining / windup : 1f;
        // 동시 발동 간격 대기에는 타격 한 프레임 전에 멈춘다. 실제 발동 때만 타격 자세로 간다.
        float beforeImpact = Mathf.Max(start, motion.impact - 1f / (motion.clip.frameRate * motion.clip.length));
        SetAttackTime(Mathf.Min(beforeImpact, Mathf.Lerp(start, motion.impact, progress)));
        if (attacking) return;
        attacking = true;
        animator.CrossFadeInFixedTime(attackState, Mathf.Min(crossFade, windup), 0, 0f);
    }

    private void SetAttackTime(float value)
    {
        motionTime = value;
        animator.SetFloat(AttackTime, value);
    }

    private void Update() => AdvanceRecovery(Time.deltaTime);

    private void AdvanceRecovery(float deltaTime)
    {
        if (!CanAct || !recovering || motion == null || Time.frameCount == firedFrame || Time.timeScale <= 0f) return;
        SetAttackTime(Mathf.Min(1f, motionTime + deltaTime / motion.clip.length));
        if (motionTime < 1f) return;
        CancelAttack();
        animator.CrossFadeInFixedTime(IdleState, crossFade, 0, 0f);
    }

    private void CancelAttack()
    {
        attacking = recovering = holdingCharge = false;
        motion = null;
        AttackIndex = MotionIndex = -1;
    }

    private void OnAttackFired(CastColor color)
    {
        if (!CanAct) return;
        if (bow != null)
        {
            animator.CrossFadeInFixedTime(ReleaseState, 0.02f, 0, 0f);
            Camera cam = Camera.main;
            Vector3 from = bow.transform.position;
            Vector3 to = cam != null ? Vector3.Lerp(from, cam.transform.position, arrowTravel) : from + transform.forward * 3f;
            ArrowFlight shot = bow.Release(to, arrowFlightSeconds);
            if (shot != null) arrows.Add(shot);
            AttackImpact?.Invoke(color == CastColor.Orange ? 1 : color == CastColor.Red ? 2 : 0);
            return;
        }
        // 페이즈 전환이 발동 직전에 CurrentAttack을 비워도 시전 시작 때 고른 모션을 유지한다.
        if (motion == null) SelectAttack(color);
        if (motion == null || motion.clip == null) return;
        // 시전 때 이미 시작한 공격을 처음부터 다시 틀지 않는다. 피해 프레임에 블렌딩도 마친다.
        SetAttackTime(motion.impact);
        animator.Play(attackState, 0, motion.impact);
        attacking = recovering = true;
        firedFrame = Time.frameCount;
        AttackImpact?.Invoke(AttackIndex);
    }

    private void OnInterrupted()
    {
        if (!CanAct) return;
        CancelAttack();
        AttackCanceled?.Invoke();
        if (bow != null) bow.HideArrow();
        animator.CrossFadeInFixedTime(HitState, 0.08f, 0, 0f);
    }

    private void OnDied()
    {
        if (dead || !isActiveAndEnabled) return;
        dead = true;
        CancelAttack();
        AttackCanceled?.Invoke();
        // 적 오브젝트가 곧 꺼지므로 모델을 떼어 둔다. 위치·크기는 그대로 유지.
        transform.SetParent(null, true);
        detached = true;
        if (bow != null) bow.HideArrow();
        animator.CrossFadeInFixedTime(DeathState, 0.1f, 0, 0f);
    }

    /// <summary>승리 후 사망 모션만 실제 시간으로 마저 재생한다. 전투 중 사망은 기존 게임 시간을 따른다.</summary>
    public float PlayDeathUnscaled()
    {
        if (!dead || !isActiveAndEnabled || animator.runtimeAnimatorController == null) return 0f;
        animator.updateMode = AnimatorUpdateMode.UnscaledTime;
        lookWeight = 0f; // 게임 시간 0에서도 사망 자세가 카메라 시선에 묶이지 않게 한다.
        // 같은 프레임에 요청된 Death 전환을 평가한 뒤 남은 모션 시간을 읽는다.
        animator.Update(0f);
        bool transitioning = animator.IsInTransition(0);
        var state = transitioning ? animator.GetNextAnimatorStateInfo(0) : animator.GetCurrentAnimatorStateInfo(0);
        if (state.shortNameHash != DeathState) return 0f;
        return Mathf.Max(0f, 1f - state.normalizedTime) * state.length + (transitioning ? 0.1f : 0f);
    }

    private void OnEncounterActiveChanging(bool active)
    {
        if (active) return;
        CancelAttack();
        AttackCanceled?.Invoke();
        ClearArrows();
        if (!detached) return;
        detached = false;
        animator.updateMode = AnimatorUpdateMode.Normal;
        dead = false;   // 다음에 켜질 때 OnEnable이 대기 자세로 되돌린다
        transform.SetParent(home, false);
        transform.localPosition = homePosition;
        transform.localRotation = homeRotation;
        transform.localScale = homeScale;
    }

    private void ClearArrows()
    {
        foreach (ArrowFlight a in arrows) if (a != null) Destroy(a.gameObject);
        arrows.Clear();
    }
}
