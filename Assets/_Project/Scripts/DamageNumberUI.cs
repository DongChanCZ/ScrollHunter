using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>확정된 적 피해를 표시한다. 피해 계산·치명타 추첨에는 관여하지 않는다.</summary>
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
    [SerializeField] private Vector2 screenOffset = new Vector2(45f, 155f);
    [SerializeField, Min(0f)] private float stackSpacing = 45f;

    private sealed class Number
    {
        public TMP_Text text;
        public Vector2 origin;
        public Vector3 source;
        public float age;
        public Color color;
    }

    private readonly List<Number> numbers = new List<Number>();
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

    private void OnEnable() => Enemy.DamageTaken += Show;

    private void OnDisable()
    {
        Enemy.DamageTaken -= Show;
        Clear();
    }

    private void Show(Vector3 position, int amount, bool isCritical)
    {
        if (amount <= 0 || numberTemplate == null || canvas == null || worldCamera == null) return;
        Vector3 screen = worldCamera.WorldToScreenPoint(position + worldOffset);
        if (screen.z <= 0f) return;
        Camera uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screen, uiCamera, out Vector2 local)) return;
        local += screenOffset;

        // 같은 적의 연속 피해는 이전 숫자를 위로 밀어 겹침을 줄인다.
        foreach (Number previous in numbers)
            if (previous.source == position) previous.origin.y += stackSpacing;

        TMP_Text text = Instantiate(numberTemplate, root);
        text.text = amount.ToString(System.Globalization.CultureInfo.InvariantCulture);
        text.raycastTarget = false;
        text.color = isCritical ? criticalColor : normalColor;
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
