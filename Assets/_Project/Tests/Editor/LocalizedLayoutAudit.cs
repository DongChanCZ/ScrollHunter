using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;

/// <summary>현재 화면에 보이는 글자를 5개 언어로 바꿔 보며, 한국어보다 상자를 더 넘는 글자만 보고한다(Play 중).</summary>
public static class LocalizedLayoutAudit
{
    [MenuItem("Tools/Scroll Hunter/Audit Localized Layout (Play)")]
    public static void RunMenu() => Debug.Log("[Layout Audit] " + Audit());

    public static string Audit()
    {
        if (!Application.isPlaying) throw new InvalidOperationException("Play required");
        var original = GameText.Current;
        var texts = UnityEngine.Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
            .Where(t => t.isActiveAndEnabled && t.rectTransform.rect.width > 4f).ToList();
        var korean = new Dictionary<TMP_Text, Vector2>();
        var report = new StringBuilder();
        int issues = 0;
        try
        {
            foreach (GameLanguage language in Enum.GetValues(typeof(GameLanguage)))
            {
                GameText.SetLanguage(language);
                // HUD는 언어 이벤트 대신 LateUpdate에서 갱신하므로 측정 전에 같은 표시 갱신을 수행한다.
                foreach (var view in new MonoBehaviour[] { UnityEngine.Object.FindFirstObjectByType<CombatUI>(), UnityEngine.Object.FindFirstObjectByType<CombatInfoUI>(), UnityEngine.Object.FindFirstObjectByType<StatsPanelUI>(), UnityEngine.Object.FindFirstObjectByType<BattleRewardUI>() })
                    if (view != null) view.GetType().GetMethod("LateUpdate", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(view, null);
                Canvas.ForceUpdateCanvases();
                foreach (var text in texts)
                {
                    text.ForceMeshUpdate(true, true);
                    var rect = text.rectTransform.rect;
                    // 버튼 글자 상자가 버튼 그림보다 넓은 경우가 있어 버튼 크기를 기준으로 잰다.
                    var button = text.GetComponentInParent<UnityEngine.UI.Selectable>(true);
                    if (button != null && button.transform == text.transform.parent)
                    {
                        var frame = ((RectTransform)button.transform).rect;
                        rect = new Rect(0, 0, Mathf.Min(rect.width, frame.width), Mathf.Min(rect.height, frame.height));
                    }
                    var over = new Vector2(text.renderedWidth - rect.width, text.renderedHeight - rect.height);
                    if (language == GameLanguage.Korean) { korean[text] = over; continue; }
                    var baseline = korean[text];
                    if (System.Text.RegularExpressions.Regex.IsMatch(text.text, "[가-힣]") && !GameText.NativeNames.Contains(text.text))
                    {
                        issues++;
                        report.AppendLine(language + " | untranslated " + text.transform.parent.name + "/" + text.name + " | " + text.text);
                    }
                    if ((over.x > 2f && over.x > baseline.x + 2f) || (over.y > 2f && over.y > baseline.y + 2f))
                    {
                        issues++;
                        report.AppendLine(language + " | " + text.transform.parent.name + "/" + text.name + " rect " + rect.width.ToString("0") + "x" + rect.height.ToString("0")
                            + " over " + over.x.ToString("0") + "," + over.y.ToString("0") + " (ko " + baseline.x.ToString("0") + "," + baseline.y.ToString("0") + ") | "
                            + text.text.Replace("\n", "⏎").Substring(0, Math.Min(80, text.text.Length)));
                    }
                }
            }
        }
        finally { GameText.SetLanguage(original); }
        return "texts " + texts.Count + ", issues " + issues + "\n" + report;
    }
}
