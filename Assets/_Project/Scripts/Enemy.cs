using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 캐스팅 시간만큼 채운 뒤 발동하고, 휴식 후 반복한다.
/// 발동 직전 EnemyManager에 허가를 받으므로 적끼리 동시에 터지지 않는다.
/// </summary>
public class Enemy : MonoBehaviour
{
    [SerializeField] private EnemyData data;

    [Tooltip("발동 후 다음 캐스팅까지 쉬는 시간(초)")]
    [SerializeField] private float restAfterCast = 1f;

    [Header("캐스팅 바")]
    [SerializeField] private Transform castBarRoot;
    [SerializeField] private Image castBarFill;
    [SerializeField] private TMP_Text castSkillText;

    private EnemyManager manager;
    private Player player;
    private Camera cam;

    private float castTimer;
    private float restTimer;
    private bool resting;

    public EnemyData Data => data;
    public float CurrentHp { get; private set; }
    public bool IsAlive => CurrentHp > 0f;

    /// <summary>캐스팅 진행 중인지. 3단계 차단 판정에서 쓴다.</summary>
    public bool IsCasting => IsAlive && !resting;

    public float CastProgress01
    {
        get
        {
            if (data == null || data.CastTime <= 0f) return 0f;
            return Mathf.Clamp01(castTimer / data.CastTime);
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
        if (castSkillText != null && data != null) castSkillText.text = data.CastSkillName;
        ApplyBarColor();
    }

    private void Update()
    {
        if (data == null || !IsAlive) return;

        if (resting)
        {
            restTimer -= Time.deltaTime;
            if (restTimer <= 0f)
            {
                resting = false;
                castTimer = 0f;
            }
            return;
        }

        if (castTimer < data.CastTime)
        {
            castTimer = Mathf.Min(castTimer + Time.deltaTime, data.CastTime);
        }

        if (castTimer < data.CastTime) return;

        // 바가 다 찼다. 매니저가 최소 간격을 강제하므로 허가가 날 때까지 기다린다.
        if (manager != null && !manager.TryFire(this)) return;

        Fire();
    }

    private void Fire()
    {
        if (player != null) player.TakeDamage(data.Damage);
        Debug.Log($"[{name}] 발동: {data.CastSkillName} ({data.CastColor}) 피해 {data.Damage:F0}", this);

        resting = true;
        restTimer = restAfterCast;
        castTimer = 0f;
    }

    private void LateUpdate()
    {
        if (castBarFill != null)
        {
            castBarFill.fillAmount = resting ? 0f : CastProgress01;
        }

        // 카메라 각도를 어떻게 잡아도 바가 정면으로 보이게 한다.
        if (castBarRoot != null && cam != null)
        {
            castBarRoot.forward = cam.transform.forward;
        }
    }

    private void ApplyBarColor()
    {
        if (castBarFill == null || manager == null || data == null) return;
        castBarFill.color = manager.GetCastColor(data.CastColor);
    }

    /// <summary>EnemyManager가 Initialize 직후 색을 확정할 때 호출한다.</summary>
    public void RefreshBarColor() => ApplyBarColor();
}
