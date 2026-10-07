using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>차단 시도 결과. 코스트를 소모할지 여기서 갈린다.</summary>
public enum InterruptResult
{
    /// <summary>캐스팅 중이 아니라 시도 자체가 불가. 코스트 소모 없음.</summary>
    NotCasting,
    /// <summary>캐스팅 취소 + 경직.</summary>
    Success,
    /// <summary>빨강 캐스팅이라 실패. 코스트는 소모된다.</summary>
    FailedRed,
}

/// <summary>
/// 공격을 하나 골라 캐스팅하고, 발동한 뒤 경직만큼 쉬고 다시 고른다.
///
/// 공격 선택은 「적 패턴 쿨다운」 규칙을 따른다. 쿨다운을 시간이 아니라
/// 초록 사용 횟수로 세므로 플레이어가 직접 셀 수 있다.
///   1. 직전 행동이 상위 공격(주황·빨강)이었다면 반드시 초록
///   2. 쿨다운이 끝난 상위 공격 중 우선순위가 가장 높은 것 (빨강 > 주황)
///   3. 없으면 초록
///
/// 플레이어 카드에는 쿨타임이 없다. 이것은 적 전용 규칙이다.
/// </summary>
public class Enemy : MonoBehaviour
{
    [SerializeField] private EnemyData data;

    [Header("패턴 쿨다운 (초록 사용 횟수)")]
    [Tooltip("주황을 다시 쓰려면 그 사이에 필요한 초록 횟수")]
    [SerializeField] private int orangeCooldownGreens = 1;

    [Tooltip("빨강을 다시 쓰려면 그 사이에 필요한 초록 횟수")]
    [SerializeField] private int redCooldownGreens = 2;

    [Header("캐스팅 바 — 화면 고정 (02 문서 §2.2)")]
    [Tooltip("Canvas(Screen Space) 아래에 있는 바 묶음. 매 프레임 적의 머리 위 화면 좌표로 옮긴다.")]
    [SerializeField] private RectTransform castBarRoot;

    [Tooltip("바가 따라붙을 기준점의 높이. 적 transform 기준 로컬 오프셋(캡슐 머리 = 1.05)")]
    [SerializeField] private float barWorldHeight = 1.05f;
    [SerializeField] private Image castBarFill;
    [SerializeField] private TMP_Text castSkillText;

    [Tooltip("차단당해 경직된 동안의 캐스팅 바 색")]
    [SerializeField] private Color staggerColor = new Color(0.55f, 0.55f, 0.6f);

    [Header("HP 표시")]
    [SerializeField] private Image hpBarFill;
    [SerializeField] private TMP_Text hpText;
    private RectTransform hpBarRect;

    /// <summary>타겟 강조선 위치용 HP 바 영역(읽기 전용). 바 묶음 바로 아래에서 HP 채움을 담은 오브젝트.</summary>
    public RectTransform HpBarRect
    {
        get
        {
            if (hpBarRect != null || hpBarFill == null || castBarRoot == null) return hpBarRect;
            Transform t = hpBarFill.transform;
            while (t.parent != null && t.parent != castBarRoot) t = t.parent;
            if (t.parent == castBarRoot) hpBarRect = (RectTransform)t;
            return hpBarRect;
        }
    }

    [Tooltip("{0}=현재 HP, {1}=최대 HP")]
    [SerializeField] private string hpFormat = "{0} / {1}";

    [SerializeField] private string phaseAttackFormat = "{0}페이즈 · {1}";
    [SerializeField] private string phaseTransitionFormat = "전환 · 무적 {0:0.0}초";
    private int phaseIndex;
    private int phasePatternIndex;
    private float phaseTransitionRemaining;
    public bool HasPhases => data != null && data.Phases.Count > 0;
    public int PhaseNumber => HasPhases ? phaseIndex + 1 : 0;
    public bool IsPhaseTransitioning => phaseTransitionRemaining > 0f;
    public bool IsInvulnerable => IsAlive && IsPhaseTransitioning;
    public float PhaseTransitionRemaining => phaseTransitionRemaining;
    private BossOrbs orbs;
    private bool ruinAtCastStart, cycleAtCastStart;
    public bool IsBossOrb { get; private set; }
    private Enemy summoner;
    public int OrbDestructionDamage => summoner != null ? summoner.Data.OrbDestructionDamage : 0;
    public bool CastBlocksCostRecovery => ruinAtCastStart && current != null && current.StunSeconds > 0f;
    public float CurrentDamageMultiplier => ruinAtCastStart ? data.OrbDamageMultiplier : 1f;
    public float CurrentCastTime => current == null ? 0f : current.CastTime * Mathf.Max(.01f,
        (HasPhases ? data.Phases[phaseIndex].CastTimeMultiplier : 1f) - (cycleAtCastStart ? data.OrbCastTimeReduction : 0f));

    private EnemyManager manager;
    private Player player;
    private Camera cam;

    private EnemyAttack current;
    private float castTimer;
    private float restTimer;
    private float staggerTimer;
    private bool resting;

    // 패턴 쿨다운 상태. 초록을 몇 번 썼는지로 센다.
    private int greenCount;
    private int greenAtLastOrange = int.MinValue / 2;
    private int greenAtLastRed = int.MinValue / 2;
    private bool lastWasUpper;

    public Vector3 BarWorldPosition => transform.position + Vector3.up * barWorldHeight;

    public EnemyData Data => data;
    public int MaxHp => data != null ? data.MaxHp : 0;
    public int CurrentHp { get; private set; }
    public bool IsAlive => CurrentHp > 0 || HoldTutorialDefeat;
    public bool HoldTutorialDefeat { get; set; }
    private bool tutorialPattern;
    private int tutorialAttackIndex;
    private static readonly CastColor[] TutorialPattern = { CastColor.Green, CastColor.Orange, CastColor.Red };
    public event System.Action CastAdvanced;
    public event System.Action<CastColor> AttackFired;

    // 표시용 알림(10/6 모델 동작 연결). 판정·타이머·순서는 바꾸지 않고 이미 일어난 일을 알리기만 한다.
    public event System.Action CastStarted;            // 새 캐스팅 시작
    public event System.Action Interrupted;            // 차단 성공(경직 시작)
    public event System.Action<int> Damaged;           // 피해 적용 직후
    public event System.Action Died;                   // 사망 확정, 오브젝트를 끄기 직전
    public event System.Action<bool> EncounterActiveChanging;   // 전투 편성 켜기/끄기 직전

    public void BeginTutorial(int hp, int patternIndex)
    {
        tutorialPattern = true;
        HoldTutorialDefeat = true;
        CurrentHp = Mathf.Clamp(hp, 0, MaxHp);
        tutorialAttackIndex = patternIndex;
        BeginNextCast();
    }

    public void ReleaseTutorialDefeat()
    {
        HoldTutorialDefeat = false;
        if (CurrentHp <= 0) { Die(); if (manager != null) manager.NotifyEnemyDied(); }
    }

    public bool IsStaggered => staggerTimer > 0f;

    /// <summary>캐스팅 진행 중인지. 차단 가능 여부의 기준이다.</summary>
    public bool IsCasting => IsAlive && !resting && !IsStaggered && !IsPhaseTransitioning && current != null;

    /// <summary>지금 캐스팅 중인 공격. 없으면 null.</summary>
    public EnemyAttack CurrentAttack => current;

    public CastColor CastColor => current != null ? current.CastColor : CastColor.Green;

    public float CastProgress01
    {
        get
        {
            if (current == null || CurrentCastTime <= 0f) return 0f;
            return Mathf.Clamp01(castTimer / CurrentCastTime);
        }
    }

    public void Initialize(EnemyManager owner, Player target)
    {
        manager = owner;
        player = target;
    }

    private void Awake()
    {
        cam = Camera.main;
        if (data != null) CurrentHp = data.MaxHp;
    }

    private void Start()
    {
        if (current == null && !IsPhaseTransitioning && !IsStaggered && !resting) BeginNextCast();
    }

    public void SetEncounterActive(bool active)
    {
        EncounterActiveChanging?.Invoke(active);
        if (!active) { phaseTransitionRemaining = 0f; current = null; if (orbs != null) { orbs.Clear(); orbs.RemoveEffects(); } }
        gameObject.SetActive(active);
        if (castBarRoot != null) castBarRoot.gameObject.SetActive(active);
    }

    public void BeginBattle()
    {
        if (orbs != null) orbs.Clear();
        if (HasPhases && data.RuinOrb != null && data.CycleOrb != null)
        {
            if (orbs == null) orbs = gameObject.AddComponent<BossOrbs>();
            orbs.Initialize(this, manager, player);
        }
        SetEncounterActive(true);
        tutorialPattern = HoldTutorialDefeat = false;
        CurrentHp = MaxHp;
        phaseIndex = phasePatternIndex = 0;
        phaseTransitionRemaining = 0f;
        greenCount = 0;
        greenAtLastOrange = greenAtLastRed = int.MinValue / 2;
        lastWasUpper = false;
        restTimer = 0f;
        BeginNextCast();
    }

    private void Update() => AdvanceCombat(Time.deltaTime);

    private void AdvanceCombat(float deltaTime)
    {
        if (data == null || !IsAlive || Time.timeScale <= 0f
            || (manager != null && manager.CombatEnded) || (player != null && !player.IsAlive)) return;

        // 전환과 발동/차단 경직은 같은 시간에 진행한다.
        if (IsPhaseTransitioning || IsStaggered || resting)
        {
            bool wasTransitioning = IsPhaseTransitioning;
            phaseTransitionRemaining = Mathf.Max(0f, phaseTransitionRemaining - deltaTime);
            if (wasTransitioning && !IsPhaseTransitioning && orbs != null) orbs.ActivateInitial();
            staggerTimer = Mathf.Max(0f, staggerTimer - deltaTime);
            restTimer = Mathf.Max(0f, restTimer - deltaTime);
            if (restTimer <= 0f) resting = false;
            RefreshBar();
            if (!IsPhaseTransitioning && !IsStaggered && !resting) BeginNextCast();
            return;
        }

        if (current == null) { BeginNextCast(); return; }
        castTimer = Mathf.Min(castTimer + deltaTime, CurrentCastTime);
        CastAdvanced?.Invoke();
        if (Time.timeScale <= 0f || castTimer < CurrentCastTime) return;
        if (manager != null && !manager.TryFire(this)) return;
        Fire();
    }

    private void AdvancePattern()
    {
        if (HasPhases) phasePatternIndex = (phasePatternIndex + 1) % data.Phases[phaseIndex].Pattern.Count;
    }

    private void TryBeginPhaseTransition()
    {
        if (!HasPhases || !IsAlive || IsCasting || IsPhaseTransitioning
            || (manager != null && manager.CombatEnded) || (player != null && !player.IsAlive)) return;
        int next = phaseIndex;
        for (int i = phaseIndex + 1; i < data.Phases.Count; i++)
            if (CurrentHp <= MaxHp * data.Phases[i].HpThreshold) next = i;
        if (next == phaseIndex) return;
        phaseIndex = next;
        phasePatternIndex = 0;
        current = null;
        phaseTransitionRemaining = data.PhaseTransitionSeconds;
        if (orbs != null) { orbs.BeginTransition(); if (!IsPhaseTransitioning) orbs.ActivateInitial(); }
        Debug.Log($"[{name}] {PhaseNumber}페이즈 전환 {phaseTransitionRemaining:0.0}초", this);
        RefreshBar();
    }

    /// <summary>패턴 쿨다운 규칙에 따라 다음 공격을 고르고 캐스팅을 시작한다.</summary>
    private void BeginNextCast()
    {
        current = SelectNextAttack();
        ruinAtCastStart = orbs != null && orbs.RuinActive;
        cycleAtCastStart = orbs != null && orbs.CycleActive;
        castTimer = 0f;
        resting = false;
        staggerTimer = 0f;
        RefreshBar();
        if (current != null) CastStarted?.Invoke();
    }

    private EnemyAttack SelectNextAttack()
    {
        if (data == null) return null;

        if (tutorialPattern) return data.Find(TutorialPattern[tutorialAttackIndex]);
        if (HasPhases)
        {
            int attackIndex = data.Phases[phaseIndex].Pattern[phasePatternIndex] - 1;
            return data.Attacks[attackIndex];
        }
        EnemyAttack green = data.Find(CastColor.Green);

        // 1. 상위 공격 직후에는 반드시 초록이 들어간다.
        if (lastWasUpper && green != null) return green;

        // 2. 쿨다운이 끝난 상위 공격 중 우선순위가 가장 높은 것. 빨강 > 주황.
        EnemyAttack red = data.Find(CastColor.Red);
        if (red != null && greenCount - greenAtLastRed >= redCooldownGreens) return red;

        EnemyAttack orange = data.Find(CastColor.Orange);
        if (orange != null && greenCount - greenAtLastOrange >= orangeCooldownGreens) return orange;

        // 3. 남은 것은 초록.
        if (green != null) return green;
        return red != null ? red : orange;
    }

    private void Fire()
    {
        bool restoreOrb = orbs != null && PhaseNumber == 3 && current == data.Attacks[2];
        if (player != null)
        {
            player.TakeModifiedDamage(current.Damage, current.CastColor, CurrentDamageMultiplier);
            if (current.StunSeconds > 0f)
            {
                if (CastBlocksCostRecovery) player.ApplyCostBlockingStun(current.StunSeconds);
                else player.ApplyStun(current.StunSeconds);
            }
        }
        Debug.Log($"[{name}] 발동: {current.SkillName} ({current.CastColor}) 피해 {current.Damage}" +
            (ruinAtCastStart ? $" ×{CurrentDamageMultiplier:0.##} (파멸 강화)" : ""), this);

        // 쿨다운 상태 갱신
        if (current.CastColor == CastColor.Green)
        {
            greenCount++;
            lastWasUpper = false;
        }
        else
        {
            if (current.CastColor == CastColor.Orange) greenAtLastOrange = greenCount;
            else greenAtLastRed = greenCount;
            lastWasUpper = true;
        }

        resting = true;
        restTimer = current.StaggerAfterCast;
        castTimer = 0f;
        if (tutorialPattern) tutorialAttackIndex = (tutorialAttackIndex + 1) % TutorialPattern.Length;
        RefreshBar();
        CastColor firedColor = current.CastColor;
        AdvancePattern();
        TryBeginPhaseTransition();
        AttackFired?.Invoke(firedColor);
        if (restoreOrb) orbs.RestoreOne();
    }

    public static event System.Action<Vector3> InterruptSucceeded;

    /// <summary>차단 시도. 캐스팅 중이 아니면 코스트를 쓰지 않도록 NotCasting을 돌려준다.</summary>
    public InterruptResult TryInterrupt(float staggerDuration)
    {
        if (!IsCasting) return InterruptResult.NotCasting;

        // 빨강은 차단으로 막을 수 없다. 코스트는 소모된다.
        if (current.CastColor == CastColor.Red) return InterruptResult.FailedRed;

        // 차단당한 캐스팅도 「그 공격을 한 번 쓴 것」으로 쳐서 쿨다운을 소모한다.
        // 안 그러면 경직이 풀리자마자 같은 주황이 다시 올라와 차단의 보상이 없다.
        ConsumeCooldownOnInterrupt(current);
        if (tutorialPattern) tutorialAttackIndex = (tutorialAttackIndex + 1) % TutorialPattern.Length;

        castTimer = 0f;
        resting = false;
        restTimer = 0f;
        staggerTimer = staggerDuration;
        // 보스 초록 차단은 같은 순번을 다시 시전한다. 주황만 다음 순번으로 넘긴다.
        if (current.CastColor != CastColor.Green) AdvancePattern();
        TryBeginPhaseTransition();
        RefreshBar();
        InterruptSucceeded?.Invoke(transform.position);
        Interrupted?.Invoke();
        return InterruptResult.Success;
    }

    /// <summary>
    /// 차단당한 공격의 쿨다운을 소모시킨다.
    ///
    /// 초록은 건드리지 않는다. 초록 횟수는 플레이어가 화면을 보고 직접 세는 값이므로,
    /// 취소되어 발동하지 않은 캐스팅까지 세면 플레이어가 센 숫자와 어긋난다.
    /// 초록은 애초에 쿨다운도 없다.
    /// </summary>
    private void ConsumeCooldownOnInterrupt(EnemyAttack attack)
    {
        if (attack.CastColor == CastColor.Green) return;

        if (attack.CastColor == CastColor.Orange) greenAtLastOrange = greenCount;
        else greenAtLastRed = greenCount;

        lastWasUpper = true;
    }

    /// <summary>확정 피해와 치명타 결과를 숫자 UI에 전달. 표시가 피해를 다시 계산하지 않는다.</summary>
    public static event System.Action<Vector3, int, bool> DamageTaken;

    public void TakeDamage(int amount, bool isCritical = false)
    {
        if (!IsAlive || IsInvulnerable || amount <= 0 || (manager != null && manager.CombatEnded)
            || (player != null && !player.IsAlive)) return;

        CurrentHp = Mathf.Max(0, CurrentHp - amount);
        DamageTaken?.Invoke(transform.position, amount, isCritical);
        Damaged?.Invoke(amount);
        if (!IsAlive) Die();
        else TryBeginPhaseTransition();
    }

    /// <summary>이미 수락된 다단히트의 사망 후 잔여 표시. HP·사망 판정은 호출하지 않는다.</summary>
    public void ShowOverkill(int amount)
    {
        if (!IsAlive && amount > 0) DamageTaken?.Invoke(transform.position, amount, false);
    }

    private void Die()
    {
        current = null;
        castTimer = 0f;
        staggerTimer = 0f;
        resting = false;
        phaseTransitionRemaining = 0f;
        Debug.Log($"[{name}] 사망", this);
        if (orbs != null) orbs.Clear();
        Died?.Invoke();   // 모델은 이때 적에서 떼어 사망 동작을 보인다(EnemyAnimationDriver). 판정은 아래 그대로

        // 바는 이제 Canvas 아래에 있어 적을 꺼도 같이 사라지지 않는다. 직접 끈다.
        if (castBarRoot != null) castBarRoot.gameObject.SetActive(false);

        gameObject.SetActive(false);
    }

    private void LateUpdate()
    {
        // 화면 고정. 적의 머리 위 월드 좌표를 화면 좌표로 옮겨 붙인다.
        // 전투 중 카메라가 고정이므로 원근에 따라 바가 작아지는 문제를 피한다.
        if (castBarRoot != null && cam != null)
        {
            Vector3 head = BarWorldPosition;
            castBarRoot.position = cam.WorldToScreenPoint(head);
        }

        SetFill(castBarFill, IsPhaseTransitioning ? phaseTransitionRemaining / data.PhaseTransitionSeconds : (resting || IsStaggered) ? 0f : CastProgress01);

        if (MaxHp > 0) SetFill(hpBarFill, (float)CurrentHp / MaxHp);

        if (hpText != null)
        {
            hpText.text = string.Format(hpFormat, CurrentHp, MaxHp);
        }

    }

    /// <summary>
    /// 바를 t(0~1)만큼 채운다.
    ///
    /// Image.fillAmount를 쓰지 않는다. 그쪽은 스프라이트가 있어야 동작하는데,
    /// 스프라이트를 쓰면 모서리가 둥글어져 바가 타원처럼 보인다.
    /// 스프라이트 없는 Image는 각진 사각형으로 그려지므로, 대신 오른쪽 앵커를 움직인다.
    /// </summary>
    private static void SetFill(Image fill, float t)
    {
        if (fill == null) return;

        Vector2 max = fill.rectTransform.anchorMax;
        max.x = Mathf.Clamp01(t);
        fill.rectTransform.anchorMax = max;
    }

    /// <summary>캐스팅 바의 색과 스킬 이름을 지금 공격에 맞춘다.</summary>
    private void RefreshBar()
    {
        if (castSkillText != null)
            castSkillText.text = IsPhaseTransitioning ? string.Format(phaseTransitionFormat, phaseTransitionRemaining)
                : IsBossOrb ? data.DisplayName : current == null ? string.Empty : HasPhases ? string.Format(phaseAttackFormat, PhaseNumber, current.SkillName) : current.SkillName;

        if (castBarFill == null) return;

        if (IsStaggered || IsPhaseTransitioning) { castBarFill.color = staggerColor; return; }
        if (manager == null || current == null) return;
        castBarFill.color = manager.GetCastColor(current.CastColor);
    }

    // 오브도 같은 HP·타겟·피해 경로를 쓴다. 준비 구체는 HP 0이고 목록에 등록하지 않는다.
    internal Enemy CreateOrb(EnemyData orbData, Vector3 position, Color color, float barHeight)
    {
        var obj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        obj.name = orbData.DisplayName;
        obj.transform.position = position;
        var orb = obj.AddComponent<Enemy>();
        orb.data = orbData;
        orb.IsBossOrb = true;
        orb.summoner = this;
        orb.barWorldHeight = barHeight;
        orb.Initialize(manager, player);
        if (castBarRoot != null)
        {
            orb.castBarRoot = Instantiate(castBarRoot, castBarRoot.parent);
            orb.castBarRoot.name = orbData.name + "Bars";
            orb.hpBarFill = CopyBarReference(hpBarFill, orb.castBarRoot);
            orb.hpText = CopyBarReference(hpText, orb.castBarRoot);
            orb.castSkillText = CopyBarReference(castSkillText, orb.castBarRoot);
            var castFill = CopyBarReference(castBarFill, orb.castBarRoot);
            if (castFill != null)
            {
                Transform bar = castFill.transform;
                while (bar.parent != orb.castBarRoot) bar = bar.parent;
                bar.gameObject.SetActive(false);
            }
            orb.castBarRoot.gameObject.SetActive(false);
        }
        var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        material.color = color;
        obj.GetComponent<Renderer>().sharedMaterial = material;
        obj.GetComponent<Collider>().enabled = false;
        return orb;
    }

    private T CopyBarReference<T>(T source, RectTransform destination) where T : Component
    {
        if (source == null) return null;
        int index = System.Array.IndexOf(castBarRoot.GetComponentsInChildren<T>(true), source);
        return index < 0 ? null : destination.GetComponentsInChildren<T>(true)[index];
    }

    internal void HideOrb()
    {
        CurrentHp = 0;
        SetEncounterActive(false); // 종료 정리는 Died를 호출하지 않는다.
        GetComponent<Collider>().enabled = false;
    }

    private void OnDestroy()
    {
        if (!IsBossOrb) return;
        if (castBarRoot != null) Destroy(castBarRoot.gameObject);
        var renderer = GetComponent<Renderer>();
        if (renderer != null) Destroy(renderer.sharedMaterial);
    }

    /// <summary>EnemyManager가 Initialize 직후 색을 확정할 때 호출한다.</summary>
    public void RefreshBarColor() => RefreshBar();
}
