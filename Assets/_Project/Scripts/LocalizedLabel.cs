using TMPro;
using UnityEngine;

/// <summary>고정 UI 문구. 실행 중 다른 내용으로 갱신된 라벨은 덮어쓰지 않는다.</summary>
public class LocalizedLabel : MonoBehaviour
{
    [SerializeField, TextArea] private string source;
    private TMP_Text label;
    private string last;
    public void Initialize(string value) { source = last = value; if (label == null) label = GetComponent<TMP_Text>(); Refresh(); }
    private void Awake() { label = GetComponent<TMP_Text>(); last = source; GameText.Changed += Refresh; Refresh(); }
    private void OnDestroy() { GameText.Changed -= Refresh; }
    private void Refresh()
    {
        if (label == null || (label.text != last && label.text != source)) return;
        last = GameText.T(source);
        label.text = last;
    }
}
