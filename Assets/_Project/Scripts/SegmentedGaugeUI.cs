using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>현재/최대 값을 채우는 가로 게이지와 단위 눈금(코스트 1 단위). 최대치가 바뀌면 눈금을 다시 놓는다.</summary>
public class SegmentedGaugeUI : MonoBehaviour
{
    [Tooltip("오른쪽 앵커로 채운다. 소수 값도 그대로 반영한다")]
    [SerializeField] private RectTransform fill;
    [Tooltip("눈금 원본. 게이지 폭 기준 위치에 복제한다")]
    [SerializeField] private RectTransform tickTemplate;
    [SerializeField, Min(0.01f)] private float unit = 1f;

    private readonly List<RectTransform> ticks = new List<RectTransform>();
    private float shownMax = -1f;

    /// <summary>지금 보이는 눈금 수(최대치 − 1). 재사용하려고 남겨 둔 비활성 눈금은 세지 않는다.</summary>
    public int TickCount
    {
        get { int n = 0; foreach (RectTransform tick in ticks) if (tick != null && tick.gameObject.activeSelf) n++; return n; }
    }
    public float FillRatio => fill != null ? fill.anchorMax.x : 0f;

    private void Awake()
    {
        if (tickTemplate != null) tickTemplate.gameObject.SetActive(false);
    }

    public void Set(float current, float max)
    {
        if (fill != null)
        {
            Vector2 anchor = fill.anchorMax;
            anchor.x = max > 0f ? Mathf.Clamp01(current / max) : 0f;
            fill.anchorMax = anchor;
        }
        if (!Mathf.Approximately(max, shownMax)) BuildTicks(max);
    }

    private void BuildTicks(float max)
    {
        shownMax = max;
        if (tickTemplate == null) return;
        int count = max > 0f ? Mathf.Max(0, Mathf.CeilToInt(max / unit - 0.0001f) - 1) : 0;
        while (ticks.Count < count)
        {
            RectTransform tick = Instantiate(tickTemplate, tickTemplate.parent);
            tick.name = tickTemplate.name + ticks.Count;
            ticks.Add(tick);
        }
        for (int i = 0; i < ticks.Count; i++)
        {
            bool show = i < count;
            ticks[i].gameObject.SetActive(show);
            if (!show) continue;
            float x = (i + 1) * unit / max;
            ticks[i].anchorMin = new Vector2(x, ticks[i].anchorMin.y);
            ticks[i].anchorMax = new Vector2(x, ticks[i].anchorMax.y);
            ticks[i].anchoredPosition = new Vector2(0f, ticks[i].anchoredPosition.y);
        }
    }
}
