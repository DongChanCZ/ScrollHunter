using TMPro;
using UnityEngine;

/// <summary>영어 카드 설명이 고정 영역을 넘지 않게 조절하고, 한국어 크기는 유지한다.</summary>
public class LocalizedTextFit : MonoBehaviour
{
    private TMP_Text label;
    private float size, minimum, maximum;
    private bool automatic;
    private void Awake()
    {
        label = GetComponent<TMP_Text>(); size = label.fontSize;
        minimum = label.fontSizeMin; maximum = label.fontSizeMax; automatic = label.enableAutoSizing;
        GameText.Changed += Refresh; Refresh();
    }
    private void Refresh()
    {
        label.enableAutoSizing = GameText.English || automatic;
        label.fontSizeMin = GameText.English ? size * .78f : minimum;
        label.fontSizeMax = GameText.English ? size : maximum;
        if (!GameText.English) label.fontSize = size;
    }
    private void OnDestroy() { GameText.Changed -= Refresh; }
}
