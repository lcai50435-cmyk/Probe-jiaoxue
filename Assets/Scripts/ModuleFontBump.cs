using TMPro;
using UnityEngine;

/// <summary>
/// 2026-09-19 字体审计第一批放大（.trellis/tasks/09-19-font-bump-batch1）：
/// 只改 TMP 字号，不写回 Scene、不动 RectTransform/文案/颜色/素材。
/// M2/M3 Scene 冻结，M1/M4/M5 也走运行时覆盖保持单一机制（与 stepHints/重置弹窗同模式，
/// Scene 序列化旧值不写回）。各模块在 FlowController / M1ToolSelection 的 Awake 里调用一次。
/// 目标节点按名字定位（各 Scene 内唯一），缺失时静默跳过；最长文案 vs 容器的验算见审计报告。
/// </summary>
public static class ModuleFontBump
{
    /// <summary>M2~M5 共用：模块标题、重置按钮、重置弹窗按钮、视图切换。</summary>
    public static void ApplyShared(Transform root)
    {
        SetSize(root, "ModuleTitle", null, 40f);
        SetSize(root, "ResetButton", "Text", 30f);
        SetSize(root, "PerspectiveBar_C", "NormalButton/Text", 26f);
        SetSize(root, "PerspectiveBar_C", "PerspectiveButton/Text", 26f);
        SetSize(root, "ResetConfirmDialog", "ConfirmButton/Text", 32f);
        SetSize(root, "ResetConfirmDialog", "CancelButton/Text", 32f);
    }

    public static void ApplyM1(Transform root)
    {
        SetSize(root, "标题栏", "标题", 56f);
        SetSize(root, "开始探测", "Text", 40f);
        SetSize(root, "点击继续", "Text", 40f);
        SetSize(root, "引导遮罩", "跳过引导/Text", 36f);
        SetSize(root, "QAPanel", "Header/Title", 38f);
        SetSize(root, "QAPanel", "SendButton/Text", 36f);
        SetSize(root, "QAPanel", "VoiceButton/Text", 32f);
    }

    public static void ApplyM2(Transform root)
    {
        ApplyShared(root);
        SetSize(root, "StepProgress", "Text", 30f);
        SetSize(root, "InstructionArea", "Text", 28f);
        SetSize(root, "ApplyButton", "Text", 32f);
        // 审计❌项：EnterNextButton“进入轨头侧面探测”8 字 @30 即顶满 242px 框，保持 28 不放大。
        SetSize(root, "QAPanel", "Header/Title", 38f);
        SetSize(root, "QAPanel", "SendButton/Text", 36f);
        SetSize(root, "QAPanel", "VoiceButton/Text", 32f);
    }

    public static void ApplyM3(Transform root)
    {
        ApplyShared(root);
        SetSize(root, "StepProgress", "Text", 30f);
        SetSize(root, "CompletionPanel", "EnterNextButton/Text", 32f);
    }

    public static void ApplyM4(Transform root)
    {
        ApplyShared(root);
        SetSize(root, "StepProgress", "Text", 30f);
        SetSize(root, "CompletionPanel", "EnterNextButton/Text", 34f);
    }

    public static void ApplyM5(Transform root)
    {
        ApplyShared(root);
        SetSize(root, "StepProgressText", null, 30f);
    }

    /// <summary>按容器路径 + 叶子路径定位节点：节点上有 TMP 直接改，否则找子级 Text。</summary>
    private static void SetSize(Transform root, string containerPath, string leafPath, float size)
    {
        var node = FindPath(root, containerPath);
        if (node == null) return;
        if (!string.IsNullOrEmpty(leafPath))
        {
            node = FindPath(node, leafPath);
            if (node == null) return;
        }
        var tmp = node.GetComponent<TMP_Text>();
        if (tmp == null)
        {
            var textNode = FindDeep(node, "Text");
            if (textNode != null) tmp = textNode.GetComponent<TMP_Text>();
        }
        if (tmp != null) tmp.fontSize = size;
    }

    private static Transform FindPath(Transform root, string path)
    {
        var cur = root;
        foreach (var seg in path.Split('/'))
        {
            cur = FindDeep(cur, seg);
            if (cur == null) return null;
        }
        return cur;
    }

    private static Transform FindDeep(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name) return child;
            var hit = FindDeep(child, name);
            if (hit != null) return hit;
        }
        return null;
    }
}
