using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>확정된 적 피해·차단 성공을 표시한다. 피해 계산·치명타 추첨에는 관여하지 않는다.</summary>
public class DamageNumberUI : MonoBehaviour
{
    [SerializeField] private TMP_Text numberTemplate;
    [SerializeField] private Camera worldCamera;
    [SerializeField] private CombatMetrics metrics;
    [Header("숫자 연출 (Canvas 기준)")]
    [SerializeField] private Color normalColor = new Color(1f, 0.85f, 0.15f, 1f);
    [SerializeField] private Color criticalColor = new Color(1f, 0.5f, 0.5f, 1f);
    [SerializeField, Min(0.01f)] private float duration = 0.95f;
    [SerializeField, Min(0f)] private float riseDistance = 100f;
    [SerializeField, Range(0f, 0.99f)] private float fadeStart = 0.25f;
    [SerializeField] private Vector3 worldOffset = new Vector3(0f, 1.05f, 0f);
    [SerializeField] private Vector2 screenOffset = new Vector2(0f, 155f);
    [Tooltip("피격된 적 기준 숫자의 좌우 무작위 범위 (Canvas 기준)")]
    [SerializeField, Min(0f)] private float horizontalSpread = 24f;
    [SerializeField, Min(0f)] private float stackSpacing = 45f;

    [Header("차단 성공 표시")]
    [SerializeField] private string interruptText = "차단!";
    [SerializeField] private Color interruptColor = new Color(0.75f, 0.35f, 1f, 1f);
    [SerializeField, Min(1f)] private float interruptSizeMultiplier = 1.25f;

    private sealed class Number
    {
        public TMP_Text text;
        public Vector2 origin;
        public Vector3 source;
        public float age;
        public Color color;
    }

    private readonly List<Number> numbers = new List<Number>();
    // 연출용 난수는 전투의 UnityEngine.Random 상태를 바꾸지 않는다.
    private readonly System.Random positionRandom = new System.Random();
    private RectTransform root;
    private Canvas canvas;

    public int ActiveCount => numbers.Count;

    private void Awake()
    {
        root = (RectTransform)transform;
        canvas = GetComponentInParent<Canvas>();
        if (worldCamera == null) worldCamera = Camera.main;
        if (metrics == null) metrics = FindFirstObjectByType<CombatMetrics>();
        if (numberTemplate != null) numberTemplate.gameObject.SetActive(false);
    }

    private void OnEnable()
    {
        Enemy.DamageTaken += Show;
        Enemy.InterruptSucceeded += ShowInterrupt;
    }

    private void OnDisable()
    {
        Enemy.DamageTaken -= Show;
        Enemy.InterruptSucceeded -= ShowInterrupt;
        Clear();
    }

    private void Show(Vector3 position, int amount, bool isCritical)
    {
        if (amount <= 0) return;
        ShowText(position, amount.ToString(System.Globalization.CultureInfo.InvariantCulture),
            isCritical ? criticalColor : normalColor, 1f);
    }

    private void ShowInterrupt(Vector3 position)
        => ShowText(position, interruptText, interruptColor, Mathf.Max(1f, interruptSizeMultiplier));

    private void ShowText(Vector3 position, string label, Color color, float sizeMultiplier)
    {
        if (numberTemplate == null || canvas == null || worldCamera == null) return;
        Vector3 screen = worldCamera.WorldToScreenPoint(position + worldOffset);
        if (screen.z <= 0f) return;
        Camera uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screen, uiCamera, out Vector2 local)) return;
        local += screenOffset;
        local.x += ((float)positionRandom.NextDouble() * 2f - 1f) * Mathf.Max(0f, horizontalSpread);

        // 같은 적의 연속 표시는 이전 글자를 위로 밀어 겹침을 줄인다.
        foreach (Number previous in numbers)
            if (previous.source == position) previous.origin.y += stackSpacing;

        TMP_Text text = Instantiate(numberTemplate, root);
        text.text = label;
        text.fontSize = numberTemplate.fontSize * sizeMultiplier;
        text.raycastTarget = false;
        text.color = color;
        text.rectTransform.anchoredPosition = local;
        text.gameObject.SetActive(true);
        numbers.Add(new Number { text = text, origin = local, source = position, color = text.color });
    }

    private void Update()
    {
        // 정보 확인 정지는 함께 멈추고, 전투 종료의 마지막 피해는 끝까지 사라진다.
        Advance(metrics != null && metrics.Ended ? Time.unscaledDeltaTime : Time.deltaTime);
    }

    private void Advance(float deltaTime)
    {
        if (deltaTime <= 0f) return;
        float lifetime = Mathf.Max(0.01f, duration);
        for (int i = numbers.Count - 1; i >= 0; i--)
        {
            Number number = numbers[i];
            number.age += deltaTime;
            if (number.text == null || number.age >= lifetime)
            {
                if (number.text != null) Remove(number.text);
                numbers.RemoveAt(i);
                continue;
            }
            float t = number.age / lifetime;
            float rise = 1f - (1f - t) * (1f - t);
            number.text.rectTransform.anchoredPosition = number.origin + Vector2.up * (riseDistance * rise);
            Color color = number.color;
            color.a *= 1f - Mathf.InverseLerp(Mathf.Clamp(fadeStart, 0f, 0.99f), 1f, t);
            number.text.color = color;
        }
    }

    public void Clear()
    {
        foreach (Number number in numbers)
            if (number.text != null) Remove(number.text);
        numbers.Clear();
    }

    private static void Remove(TMP_Text text)
    {
        text.gameObject.SetActive(false);
        if (Application.isPlaying) Destroy(text.gameObject);
        else DestroyImmediate(text.gameObject);
    }
}
