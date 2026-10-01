using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 스킬·포션 입력이 수락됐을 때의 짧은 아이콘 연출(10 A37 후속). 거절된 입력에는 재생하지 않는다.
/// 카드는 수락 즉시 순환하므로 사용한 카드 아이콘을 따로 띄워 수축·복귀·점등 뒤 위로 사라지게 해
/// 새로 들어온 카드와 구분한다. 입력 수락만 알리며 효과 성공 여부는 표시하지 않는다.
/// 카드 순환·시전·GCD·입력 가능 시점에는 관여하지 않는다.
/// </summary>
public class UseFeedbackUI : MonoBehaviour
{
    [System.Serializable]
    private class SlotFx
    {
        [Tooltip("사용한 카드 아이콘을 잠깐 띄우는 이미지. 슬롯의 다른 표시보다 위에 둔다")]
        public Image usedIcon;
        [Tooltip("금속 테두리 점등. 색의 알파가 최대 밝기")]
        public Image frameFlash;
        [System.NonSerialized] public float age = -1f;
        [System.NonSerialized] public Vector2 basePosition;
        [System.NonSerialized] public Vector3 baseScale;
        [System.NonSerialized] public Color flashColor;
    }

    [SerializeField] private DeckSystem deck;
    [SerializeField] private Player player;
    [SerializeField] private SlotFx[] slots = new SlotFx[DeckSystem.HandSize];

    [Header("포션")]
    [SerializeField] private RectTransform potionIcon;
    [Tooltip("포션 뒤 초록빛. 색의 알파가 최대 밝기")]
    [SerializeField] private Image potionGlow;
    [Tooltip("초록빛 입자. 사용할 때마다 위로 퍼지며 사라진다")]
    [SerializeField] private Image[] potionMotes;

    [Header("시간(실제 초) — 입력 반응이라 배속·정지와 관계없이 같은 길이")]
    [SerializeField, Min(0.01f)] private float shrinkSeconds = 0.07f;
    [SerializeField, Min(0.01f)] private float returnSeconds = 0.1f;
    [SerializeField, Min(0.01f)] private float fadeSeconds = 0.16f;
    [SerializeField, Range(0.5f, 1f)] private float shrinkScale = 0.84f;
    [SerializeField, Min(1f)] private float overshootScale = 1.05f;
    [Tooltip("사용한 카드 아이콘이 사라지며 올라가는 거리. 새 카드와 겹쳐 보이지 않게 한다")]
    [SerializeField, Min(0f)] private float usedIconRise = 24f;
    [SerializeField, Min(0.01f)] private float moteSeconds = 0.55f;
    [SerializeField, Min(0f)] private float moteRise = 48f;
    [SerializeField, Min(0f)] private float moteSpread = 30f;

    // 연출 난수는 전투의 UnityEngine.Random 상태를 바꾸지 않는다(치명타 추첨과 분리).
    private readonly System.Random moteRandom = new System.Random();
    private float potionAge = -1f;
    private Vector3 potionBaseScale = Vector3.one;
    private Color potionGlowColor;
    private Vector2[] moteStart;
    private Vector2[] moteDirection;
    private Color[] moteColor;

    public int ActiveSlotCount
    {
        get { int n = 0; foreach (SlotFx fx in slots) if (fx != null && fx.age >= 0f) n++; return n; }
    }
    public bool PotionPlaying => potionAge >= 0f;
    public Sprite UsedIconSprite(int slot) => slot >= 0 && slot < slots.Length && slots[slot] != null && slots[slot].usedIcon != null ? slots[slot].usedIcon.sprite : null;

    private float SlotDuration => shrinkSeconds + returnSeconds + fadeSeconds;

    private void Awake()
    {
        if (deck == null) deck = FindFirstObjectByType<DeckSystem>();
        if (player == null) player = FindFirstObjectByType<Player>();
        foreach (SlotFx fx in slots)
        {
            if (fx == null) continue;
            if (fx.usedIcon != null)
            {
                fx.basePosition = fx.usedIcon.rectTransform.anchoredPosition;
                fx.baseScale = fx.usedIcon.rectTransform.localScale;
            }
            if (fx.frameFlash != null) fx.flashColor = fx.frameFlash.color;
        }
        if (potionIcon != null) potionBaseScale = potionIcon.localScale;
        if (potionGlow != null) potionGlowColor = potionGlow.color;
        int count = potionMotes != null ? potionMotes.Length : 0;
        moteStart = new Vector2[count];
        moteDirection = new Vector2[count];
        moteColor = new Color[count];
        for (int i = 0; i < count; i++)
            if (potionMotes[i] != null)
            {
                moteStart[i] = potionMotes[i].rectTransform.anchoredPosition;
                moteColor[i] = potionMotes[i].color;
            }
        Clear();
    }

    private void OnEnable()
    {
        if (deck != null) deck.SlotAccepted += OnSlotAccepted;
        if (player != null)
        {
            player.PotionUsed += OnPotionUsed;
            player.BattleReset += Clear;
        }
    }

    private void OnDisable()
    {
        if (deck != null) deck.SlotAccepted -= OnSlotAccepted;
        if (player != null)
        {
            player.PotionUsed -= OnPotionUsed;
            player.BattleReset -= Clear;
        }
        Clear();
    }

    private void OnSlotAccepted(int slot, SkillData card)
    {
        if (slot < 0 || slot >= slots.Length || slots[slot] == null) return;
        SlotFx fx = slots[slot];
        fx.age = 0f;
        if (fx.usedIcon != null)
        {
            fx.usedIcon.sprite = card != null ? card.Icon : null;
            fx.usedIcon.enabled = fx.usedIcon.sprite != null;
        }
        Apply(fx);
    }

    private void OnPotionUsed()
    {
        potionAge = 0f;
        for (int i = 0; potionMotes != null && i < potionMotes.Length; i++)
        {
            float angle = Mathf.Lerp(-55f, 55f, (float)moteRandom.NextDouble()) * Mathf.Deg2Rad;
            moteDirection[i] = new Vector2(Mathf.Sin(angle), Mathf.Cos(angle)) * Mathf.Lerp(0.6f, 1f, (float)moteRandom.NextDouble());
        }
        ApplyPotion();
    }

    /// <summary>전투 시작·재시작·화면 전환 때 남은 연출을 지운다.</summary>
    public void Clear()
    {
        foreach (SlotFx fx in slots)
        {
            if (fx == null) continue;
            fx.age = -1f;
            Apply(fx);
        }
        potionAge = -1f;
        ApplyPotion();
    }

    private void Update() => Tick(Time.unscaledDeltaTime);

    private void Tick(float dt)
    {
        foreach (SlotFx fx in slots)
        {
            if (fx == null || fx.age < 0f) continue;
            fx.age += dt;
            if (fx.age >= SlotDuration) fx.age = -1f;
            Apply(fx);
        }
        if (potionAge >= 0f)
        {
            potionAge += dt;
            if (potionAge >= Mathf.Max(SlotDuration, moteSeconds)) potionAge = -1f;
            ApplyPotion();
        }
    }

    /// <summary>수축 → 복귀(살짝 넘침) → 제자리. 0~1 진행에 따른 배율.</summary>
    private float PressScale(float age)
    {
        if (age < shrinkSeconds) return Mathf.Lerp(1f, shrinkScale, Smooth(age / shrinkSeconds));
        age -= shrinkSeconds;
        if (age < returnSeconds) return Mathf.Lerp(shrinkScale, overshootScale, Smooth(age / returnSeconds));
        age -= returnSeconds;
        return Mathf.Lerp(overshootScale, 1f, Smooth(Mathf.Clamp01(age / fadeSeconds)));
    }

    /// <summary>점등은 수축이 끝날 때 가장 밝고 이후 서서히 꺼진다.</summary>
    private float FlashAlpha(float age)
    {
        if (age < 0f) return 0f;
        if (age < shrinkSeconds) return age / shrinkSeconds;
        return Mathf.Clamp01(1f - (age - shrinkSeconds) / (returnSeconds + fadeSeconds));
    }

    private void Apply(SlotFx fx)
    {
        bool playing = fx.age >= 0f;
        if (fx.usedIcon != null)
        {
            RectTransform rect = fx.usedIcon.rectTransform;
            if (!playing || fx.usedIcon.sprite == null)
            {
                fx.usedIcon.enabled = false;
                rect.anchoredPosition = fx.basePosition;
                rect.localScale = fx.baseScale;
            }
            else
            {
                float fade = Mathf.Clamp01((fx.age - shrinkSeconds - returnSeconds) / fadeSeconds);
                fx.usedIcon.enabled = true;
                rect.localScale = fx.baseScale * PressScale(fx.age);
                rect.anchoredPosition = fx.basePosition + Vector2.up * (usedIconRise * fade);
                fx.usedIcon.color = new Color(1f, 1f, 1f, 1f - fade);
            }
        }
        if (fx.frameFlash != null)
        {
            float a = playing ? FlashAlpha(fx.age) : 0f;
            fx.frameFlash.enabled = a > 0f;
            Color c = fx.flashColor;
            c.a *= a;
            fx.frameFlash.color = c;
        }
    }

    private void ApplyPotion()
    {
        bool playing = potionAge >= 0f;
        if (potionIcon != null) potionIcon.localScale = potionBaseScale * (playing && potionAge < SlotDuration ? PressScale(potionAge) : 1f);
        if (potionGlow != null)
        {
            float a = playing ? FlashAlpha(potionAge) : 0f;
            potionGlow.enabled = a > 0f;
            Color c = potionGlowColor;
            c.a *= a;
            potionGlow.color = c;
        }
        for (int i = 0; potionMotes != null && i < potionMotes.Length; i++)
        {
            Image mote = potionMotes[i];
            if (mote == null) continue;
            float t = playing ? Mathf.Clamp01(potionAge / moteSeconds) : 1f;
            mote.enabled = playing && t < 1f;
            if (!mote.enabled) continue;
            float travel = 1f - (1f - t) * (1f - t);
            mote.rectTransform.anchoredPosition = moteStart[i]
                + new Vector2(moteDirection[i].x * moteSpread, moteDirection[i].y * moteRise) * travel;
            mote.rectTransform.localScale = Vector3.one * Mathf.Lerp(1f, 0.5f, t);
            Color c = moteColor[i];
            c.a *= 1f - t;
            mote.color = c;
        }
    }

    private static float Smooth(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
    }
}
