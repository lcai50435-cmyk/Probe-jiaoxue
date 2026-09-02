using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>探测模块运行时提示的公共 UI 适配，避免冻结场景写回和模块间重复布局代码。</summary>
public static class ModuleHintOverlay
{
    public static void MoveHelpPanel(GameObject panel)
    {
        if (panel == null) return;
        var rt = panel.GetComponent<RectTransform>();
        if (rt == null) return;
        rt.anchorMin = rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(1f, 0f);
        rt.anchoredPosition = new Vector2(-340f, 196f);
        rt.sizeDelta = new Vector2(400f, 150f);
        var text = panel.transform.Find("HelpText")?.GetComponent<TMP_Text>();
        if (text == null)
            foreach (var candidate in panel.GetComponentsInChildren<TMP_Text>(true))
                if (candidate.transform.parent == panel.transform) { text = candidate; break; }
        if (text != null)
        {
            var textRt = text.rectTransform;
            textRt.anchorMin = new Vector2(0f, 1f); textRt.anchorMax = new Vector2(1f, 1f);
            textRt.pivot = new Vector2(.5f, 1f); textRt.anchoredPosition = new Vector2(0f, -10f);
            textRt.sizeDelta = new Vector2(-32f, 54f); text.alignment = TextAlignmentOptions.Center;
        }
        var yes = panel.transform.Find("AutoDemoButton") ?? panel.transform.Find("NeedButton");
        var no = panel.transform.Find("TryAgainButton") ?? panel.transform.Find("NoNeedButton");
        LayoutHelpButton(yes as RectTransform, new Vector2(-215f, 18f));
        LayoutHelpButton(no as RectTransform, new Vector2(-75f, 18f));
        panel.transform.SetAsLastSibling();
    }

    private static void LayoutHelpButton(RectTransform rt, Vector2 position)
    {
        if (rt == null) return;
        rt.anchorMin = rt.anchorMax = new Vector2(1f, 0f); rt.pivot = new Vector2(.5f, 0f);
        rt.anchoredPosition = position; rt.sizeDelta = new Vector2(120f, 52f);
    }

    public static void SetHelpButtonText(Button button, string text)
    {
        if (button == null) return;
        var label = button.GetComponentInChildren<TMP_Text>(true);
        if (label != null) label.text = text;
    }

    public static void ConfigureInstruction(TMP_Text text)
    {
        if (text == null) return;
        text.color = new Color(.12f, .15f, .18f);
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.raycastTarget = false;
    }

    public static TMP_Text EnsureActionHint(Transform parent, TMP_FontAsset font)
    {
        if (parent == null) return null;
        var existing = parent.Find("~ActionHint")?.GetComponent<TMP_Text>();
        if (existing != null) { if (font != null) existing.font = font; return existing; }
        var go = new GameObject("~ActionHint", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.hideFlags = HideFlags.DontSave; go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(.5f, 1f); rt.pivot = new Vector2(.5f, 1f);
        rt.anchoredPosition = new Vector2(-280f, -145f); rt.sizeDelta = new Vector2(600f, 78f);
        var text = go.GetComponent<TextMeshProUGUI>(); text.font = font; text.fontSize = 30f;
        text.alignment = TextAlignmentOptions.Center; text.color = new Color(.12f, .15f, .18f);
        text.raycastTarget = false; return text;
    }

    public static void SyncFont(TMP_Text target, TMP_Text source)
    {
        if (target != null && source != null && source.font != null) target.font = source.font;
    }

    public static void PositionAngleHint(TMP_Text text, bool angleStage)
    {
        if (text == null) return;
        var rt = text.rectTransform;
        if (angleStage)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(.5f, 0f); rt.pivot = new Vector2(.5f, .5f);
            rt.anchoredPosition = new Vector2(120f, 260f);
        }
        else
        {
            rt.anchorMin = rt.anchorMax = new Vector2(.5f, 1f); rt.pivot = new Vector2(.5f, 1f);
            rt.anchoredPosition = new Vector2(-280f, -145f);
        }
    }

}
