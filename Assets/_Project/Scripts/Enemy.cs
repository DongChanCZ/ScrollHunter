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

    [Tooltip("{0}=현재 HP, {1}=최대 HP")]
    [SerializeField] private string hpFormat = "{0} / {1}";

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

    public EnemyData Data => data;
    public int MaxHp => data != null ? data.MaxHp : 0;
    public int CurrentHp { get; private set; }
    public bool IsAlive => CurrentHp > 0;

    public bool IsStaggered => staggerTimer > 0f;

    /// <summary>캐스팅 진행 중인지. 차단 가능 여부의 기준이다.</summary>
    public bool IsCasting => IsAlive && !resting && !IsStaggered && current != null;

    /// <summary>지금 캐스팅 중인 공격. 없으면 null.</summary>
    public EnemyAttack CurrentAttack => current;

    public CastColor CastColor => current != null ? current.CastColor : CastColor.Green;

    public float CastProgress01
    {
        get
        {
            if (current == null || current.CastTime <= 0f) return 0f;
            return Mathf.Clamp01(castTimer / current.CastTime);
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
        BeginNextCast();
    }

    private void Update()
    {
        if (data == null || !IsAlive || Time.timeScale <= 0f
            || (manager != null && manager.CombatEnded) || (player != null && !player.IsAlive)) return;

        if (staggerTimer > 0f)
        {
            staggerTimer = Mathf.Max(0f, staggerTimer - Time.deltaTime);
            if (staggerTimer <= 0f) BeginNextCast();
            return;
        }

        if (resting)
        {
            restTimer -= Time.deltaTime;
            if (restTimer <= 0f)
            {
                resting = false;
                BeginNextCast();
            }
            return;
        }

        if (current == null) { BeginNextCast(); return; }

        if (castTimer < current.CastTime)
        {
            castTimer = Mathf.Min(castTimer + Time.deltaTime, current.CastTime);
        }

        if (castTimer < current.CastTime) return;

        // 바가 다 찼다. 매니저가 최소 간격을 강제하므로 허가가 날 때까지 기다린다.
        if (manager != null && !manager.TryFire(this)) return;

        Fire();
    }

    /// <summary>패턴 쿨다운 규칙에 따라 다음 공격을 고르고 캐스팅을 시작한다.</summary>
    private void BeginNextCast()
    {
        current = SelectNextAttack();
        castTimer = 0f;
        resting = false;
        staggerTimer = 0f;
        RefreshBar();
    }

    private EnemyAttack SelectNextAttack()
    {
        if (data == null) return null;

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
        if (player != null)
        {
            player.TakeDamage(current.Damage);
            // 주황의 미대응 페널티. 피해보다 이쪽이 본체다.
            if (current.StunSeconds > 0f) player.ApplyStun(current.StunSeconds);
        }
        Debug.Log($"[{name}] 발동: {current.SkillName} ({current.CastColor}) 피해 {current.Damage}", this);

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
        RefreshBar();
    }

    /// <summary>차단 시도. 캐스팅 중이 아니면 코스트를 쓰지 않도록 NotCasting을 돌려준다.</summary>
    public InterruptResult TryInterrupt(float staggerDuration)
    {
        if (!IsCasting) return InterruptResult.NotCasting;

        // 빨강은 차단으로 막을 수 없다. 코스트는 소모된다.
        if (current.CastColor == CastColor.Red) return InterruptResult.FailedRed;

        // 차단당한 캐스팅도 「그 공격을 한 번 쓴 것」으로 쳐서 쿨다운을 소모한다.
        // 안 그러면 경직이 풀리자마자 같은 주황이 다시 올라와 차단의 보상이 없다.
        ConsumeCooldownOnInterrupt(current);

        castTimer = 0f;
        resting = false;
        staggerTimer = staggerDuration;
        RefreshBar();
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

    public void TakeDamage(int amount)
    {
        if (!IsAlive || amount <= 0) return;

        CurrentHp = Mathf.Max(0, CurrentHp - amount);
        if (!IsAlive) Die();
    }

    private void Die()
    {
        current = null;
        castTimer = 0f;
        staggerTimer = 0f;
        resting = false;
        Debug.Log($"[{name}] 사망", this);

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
            Vector3 head = transform.position + Vector3.up * barWorldHeight;
            castBarRoot.position = cam.WorldToScreenPoint(head);
        }

        SetFill(castBarFill, (resting || IsStaggered) ? 0f : CastProgress01);

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
            castSkillText.text = current != null ? current.SkillName : string.Empty;

        if (castBarFill == null) return;

        if (IsStaggered) { castBarFill.color = staggerColor; return; }
        if (manager == null || current == null) return;
        castBarFill.color = manager.GetCastColor(current.CastColor);
    }

    /// <summary>EnemyManager가 Initialize 직후 색을 확정할 때 호출한다.</summary>
    public void RefreshBarColor() => RefreshBar();
}
