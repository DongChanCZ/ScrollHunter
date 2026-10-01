using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 플레이어 HP 게이지와 피격·회복 표시(10 A37 후속). 현재 HP는 즉시 반영하고, 잃은 구간은 잔상으로
/// 남겼다가 추격한다. 방어도 흡수·무적·회복·전투 초기화를 구분해 표시만 하며 판정·수치에는 관여하지 않는다.
/// </summary>
public class HpGaugeUI : MonoBehaviour
{
    [SerializeField] private Player player;
    [Tooltip("전투가 끝나면 실제 시간으로 연출을 마친다. 비워두면 씬에서 찾는다")]
    [SerializeField] private CombatMetrics metrics;

    [Header("게이지")]
    [Tooltip("현재 HP. 오른쪽 앵커로 채운다 (Image.fillAmount는 쓰지 않는다)")]
    [SerializeField] private RectTransform fill;
    [Tooltip("잃은 구간 잔상. 현재 HP 채움 뒤에 둔다")]
    [SerializeField] private RectTransform ghost;
    [Tooltip("피격 시 짧게 켜지는 밝은 테두리. 색의 알파가 최대 밝기")]
    [SerializeField] private Graphic frameFlash;
    [Tooltip("실제 회복 시 게이지 위에 켜지는 초록빛. 색의 알파가 최대 밝기")]
    [SerializeField] private Graphic healGlow;

    [Header("방어도 흡수 — 방패 표시만 반응")]
    [SerializeField] private Graphic shieldFlash;
    [SerializeField] private RectTransform shieldIcon;

    [Header("잔상 — 첫 시험값: 0.2초 유지 후 0.3초 추격 (10 A37 후속)")]
    [SerializeField, Min(0f)] private float ghostHoldSeconds = 0.2f;
    [SerializeField, Min(0.01f)] private float ghostChaseSeconds = 0.3f;

    [Header("점등")]
    [SerializeField, Min(0.01f)] private float frameFlashSeconds = 0.18f;
    [SerializeField, Min(0.01f)] private float healGlowSeconds = 0.45f;
    [SerializeField, Min(0.01f)] private float shieldFlashSeconds = 0.3f;
    [SerializeField, Min(1f)] private float shieldPulseScale = 1.2f;

    [Header("실제 HP 감소량·회복량")]
    [Tooltip("복제해 쓰는 숫자. 놓인 위치에서 위로 떠오른다")]
    [SerializeField] private TMP_Text numberTemplate;
    [SerializeField] private string damageFormat = "-{0}";
    [SerializeField] private string healFormat = "+{0:0}";
    [SerializeField] private Color damageColor = new Color(1f, 0.62f, 0.36f, 1f);
    [SerializeField] private Color healColor = new Color(0.56f, 0.95f, 0.58f, 1f);
    [SerializeField, Min(0.01f)] private float numberSeconds = 0.9f;
    [SerializeField, Min(0f)] private float numberRise = 34f;
    [SerializeField, Min(0f)] private float numberStackSpacing = 26f;
    [SerializeField, Range(0f, 0.99f)] private float numberFadeStart = 0.45f;

    private sealed class Number
    {
        public TMP_Text text;
        public Vector2 origin;
        public float age;
        public Color color;
    }

    private readonly List<Number> numbers = new List<Number>();
    // 잔상은 HP 수치로 둔다. 비율로 두면 최대 HP 증가(현재 HP 유지)가 잃은 구간처럼 보인다.
    private float ghostHp;
    private float holdRemaining;
    private float chaseFrom = -1f; // 추격을 시작한 잔상 HP. 음수면 아직 유지 중이거나 추격 없음
    private float chaseElapsed;
    private float frameFlashRemaining;
    private float healGlowRemaining;
    private float shieldFlashRemaining;
    private Color frameFlashColor;
    private Color healGlowColor;
    private Color shieldFlashColor;
    private Vector3 shieldBaseScale = Vector3.one;

    public float GhostRatio => player != null ? Mathf.Clamp01(Mathf.Max(ghostHp, player.CurrentHp) / player.MaxHp) : 0f;
    public float FillRatio => CurrentRatio;
    public int ActiveNumberCount => numbers.Count;
    public bool FrameFlashing => frameFlashRemaining > 0f;
    public bool HealGlowing => healGlowRemaining > 0f;
    public bool ShieldFlashing => shieldFlashRemaining > 0f;

    private float CurrentRatio => player != null ? Mathf.Clamp01(player.CurrentHp / player.MaxHp) : 0f;

    private void Awake()
    {
        if (player == null) player = FindFirstObjectByType<Player>();
        if (metrics == null) metrics = FindFirstObjectByType<CombatMetrics>();
        if (numberTemplate != null) numberTemplate.gameObject.SetActive(false);
        if (frameFlash != null) frameFlashColor = frameFlash.color;
        if (healGlow != null) healGlowColor = healGlow.color;
        if (shieldFlash != null) shieldFlashColor = shieldFlash.color;
        if (shieldIcon != null) shieldBaseScale = shieldIcon.localScale;
    }

    private void OnEnable()
    {
        if (player != null)
        {
            player.Damaged += OnDamaged;
            player.Healed += OnHealed;
            player.BattleReset += Snap;
        }
        Snap();
    }

    private void OnDisable()
    {
        if (player != null)
        {
            player.Damaged -= OnDamaged;
            player.Healed -= OnHealed;
            player.BattleReset -= Snap;
        }
        Snap();
    }

    private void OnDamaged(int hpLoss, int absorbed)
    {
        if (absorbed > 0) shieldFlashRemaining = shieldFlashSeconds;
        if (hpLoss <= 0 || player == null) return; // 전부 흡수: 방패 표시만 반응한다.
        // 잔상은 이번 피격 직전 HP 이상을 유지한다. 연속 피격이면 남은 잔상에서 다시 유지·추격한다.
        ghostHp = Mathf.Max(ghostHp, player.CurrentHp + hpLoss);
        holdRemaining = ghostHoldSeconds;
        chaseFrom = -1f;
        frameFlashRemaining = frameFlashSeconds;
        Spawn(string.Format(damageFormat, hpLoss), damageColor);
    }

    private void OnHealed(float amount)
    {
        healGlowRemaining = healGlowSeconds;
        Spawn(string.Format(healFormat, amount), healColor);
    }

    /// <summary>전투 시작·재시작·화면 전환: 이전 잔상·숫자·점등을 지우고 새 값에 바로 맞춘다.</summary>
    public void Snap()
    {
        ghostHp = player != null ? player.CurrentHp : 0f;
        holdRemaining = 0f;
        chaseFrom = -1f;
        frameFlashRemaining = healGlowRemaining = shieldFlashRemaining = 0f;
        for (int i = 0; i < numbers.Count; i++)
            if (numbers[i].text != null) Remove(numbers[i].text);
        numbers.Clear();
        Apply(0f);
    }

    // 정보 확인 정지·안내 정지 중에는 함께 멈추고, 전투가 끝난 뒤의 마지막 피격은 끝까지 보여준다.
    private void LateUpdate() => Tick(metrics != null && metrics.Ended ? Time.unscaledDeltaTime : Time.deltaTime);

    private void Tick(float dt)
    {
        float current = player != null ? player.CurrentHp : 0f;
        if (ghostHp <= current)
        {
            // 회복 등으로 현재 HP가 잔상 위로 오면 잔상 없이 맞춘다.
            ghostHp = current;
            chaseFrom = -1f;
            holdRemaining = 0f;
        }
        else
        {
            // 유지 시간을 먼저 쓰고, 같은 프레임에 남은 시간은 추격에 이어 쓴다(유지+추격 길이를 정확히 맞춤).
            float remaining = dt;
            if (holdRemaining > 0f)
            {
                float used = Mathf.Min(holdRemaining, remaining);
                holdRemaining -= used;
                remaining -= used;
            }
            if (holdRemaining <= 0f && remaining > 0f)
            {
                if (chaseFrom < 0f) { chaseFrom = ghostHp; chaseElapsed = 0f; }
                chaseElapsed += remaining;
                float t = Mathf.Clamp01(chaseElapsed / ghostChaseSeconds);
                ghostHp = Mathf.Lerp(chaseFrom, current, 1f - (1f - t) * (1f - t));
                if (t >= 1f) { ghostHp = current; chaseFrom = -1f; }
            }
        }
        Apply(dt);
    }

    private void Apply(float dt)
    {
        SetFill(fill, CurrentRatio);
        SetFill(ghost, GhostRatio);
        frameFlashRemaining = Fade(frameFlash, frameFlashColor, frameFlashRemaining, frameFlashSeconds, dt);
        healGlowRemaining = Fade(healGlow, healGlowColor, healGlowRemaining, healGlowSeconds, dt);
        shieldFlashRemaining = Fade(shieldFlash, shieldFlashColor, shieldFlashRemaining, shieldFlashSeconds, dt);
        if (shieldIcon != null)
            shieldIcon.localScale = shieldBaseScale * Mathf.Lerp(1f, shieldPulseScale, shieldFlashRemaining / shieldFlashSeconds);
        AdvanceNumbers(dt);
    }

    private static float Fade(Graphic graphic, Color full, float remaining, float duration, float dt)
    {
        remaining = Mathf.Max(0f, remaining - dt);
        if (graphic != null)
        {
            Color c = full;
            c.a *= remaining / duration;
            graphic.color = c;
            graphic.enabled = remaining > 0f;
        }
        return remaining;
    }

    private void Spawn(string label, Color color)
    {
        if (numberTemplate == null) return;
        foreach (Number previous in numbers) previous.origin.y += numberStackSpacing;
        TMP_Text text = Instantiate(numberTemplate, numberTemplate.transform.parent);
        text.text = label;
        text.color = color;
        text.raycastTarget = false;
        text.gameObject.SetActive(true);
        numbers.Add(new Number { text = text, origin = numberTemplate.rectTransform.anchoredPosition, color = color });
        AdvanceNumbers(0f);
    }

    private void AdvanceNumbers(float dt)
    {
        float lifetime = Mathf.Max(0.01f, numberSeconds);
        for (int i = numbers.Count - 1; i >= 0; i--)
        {
            Number number = numbers[i];
            number.age += dt;
            if (number.text == null || number.age >= lifetime)
            {
                if (number.text != null) Remove(number.text);
                numbers.RemoveAt(i);
                continue;
            }
            float t = number.age / lifetime;
            number.text.rectTransform.anchoredPosition = number.origin + Vector2.up * (numberRise * (1f - (1f - t) * (1f - t)));
            Color c = number.color;
            c.a *= 1f - Mathf.InverseLerp(numberFadeStart, 1f, t);
            number.text.color = c;
        }
    }

    private static void SetFill(RectTransform rect, float t)
    {
        if (rect == null) return;
        Vector2 max = rect.anchorMax;
        max.x = Mathf.Clamp01(t);
        rect.anchorMax = max;
    }

    private static void Remove(TMP_Text text)
    {
        text.gameObject.SetActive(false);
        if (Application.isPlaying) Destroy(text.gameObject);
        else DestroyImmediate(text.gameObject);
    }
}
