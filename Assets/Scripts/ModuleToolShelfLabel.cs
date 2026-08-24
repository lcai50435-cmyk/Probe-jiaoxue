using System;
using Object = UnityEngine.Object;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace M2
{
    /// <summary>
    /// 工具架槽位文字标注（台词.pptx Slide 3-【6】）：ProbeHome 槽下方加「K2.5探头」、RulerHome 槽下方加「多功能尺」。
    /// 冻结 Scene 不可建节点，故场景加载后自动装配（DontSave 内存态，幂等：已标注则跳过）。
    /// 通用：M2/M3/M4/M5 的 ToolShelf 同款结构自动生效；RagHome 仅 M5 有（标注「擦拭布」，老板 2026-08-23）。
    /// M2/M5 同时隐藏槽位内 Chip 标签（M5 为老板手工添加的空 Chip），统一槽底 ~ToolLabel，位置与 M3/M4 一致。
    /// </summary>
    public static class ModuleToolShelfLabel
    {
        private const string ShelfName = "ToolShelf"; // M2/M3/M4 工具架（Setup/手工 ToolShelf）
        private const string ShelfNameAlt = "Tool"; // M5 方案 B：老板手工添加的 Tool（M2 样式三槽位）
        private const string ProbeHomeName = "ProbeHome";
        private const string RulerHomeName = "RulerHome";
        private const string RagHomeName = "RagHome"; // M5 独有（擦拭布槽位）
        private const string LabelSuffix = "~ToolLabel";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Init()
        {
            TryLabelAll();
            SceneManager.sceneLoaded += (_, _) => TryLabelAll(); // 场景切换（M2→M3→M4→M5）后同样生效
        }

        private static TMP_FontAsset _cnFont;

        /// <summary>场景中文 SDF 字体（Sarasa）；缓存。
        /// 乱码根因：Chip/标注的 TMP 用了 TMP 内置默认字体（无中文字形），运行时统一替换（不写回冻结 Scene）。</summary>
        private static TMP_FontAsset FindCnFont()
        {
            if (_cnFont != null) return _cnFont;
            foreach (var tmp in Object.FindObjectsByType<TextMeshProUGUI>(FindObjectsSortMode.None))
            {
                if (tmp.font != null && tmp.font.name.IndexOf("Sarasa", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    _cnFont = tmp.font;
                    break;
                }
            }
            return _cnFont;
        }

        private static void TryLabelAll()
        {
            var sceneName = SceneManager.GetActiveScene().name;
            var isM2 = sceneName == "M2";
            // M5 对齐 M2/M3/M4（老板 2026-08-23）：隐藏空 Chip，统一槽底 ~ToolLabel（位置与 M2/M3/M4 一致）
            var hideChips = isM2 || sceneName == "M5";
            foreach (var rt in Object.FindObjectsByType<RectTransform>(FindObjectsSortMode.None))
            {
                if (rt.name != ShelfName && rt.name != ShelfNameAlt) continue;
                FixChipTextFonts(rt); // Chip 下 Text (TMP) 乱码：默认字体无中文 → 换中文 SDF
                if (hideChips)
                {
                    HideChipNodes(rt);
                }
                var probe = FindChild(rt, ProbeHomeName);
                var ruler = FindChild(rt, RulerHomeName);
                var rag = FindChild(rt, RagHomeName);
                if (hideChips)
                {
                    NormalizeM2SlotBackground(probe);
                    NormalizeM2SlotBackground(ruler);
                    NormalizeM2SlotBackground(rag);
                }
                if (probe != null) EnsureLabel(probe, "K2.5探头", "K2.5");
                if (ruler != null) EnsureLabel(ruler, "多功能尺", "多功能尺");
                if (rag != null) EnsureLabel(rag, "擦拭布", "擦拭布"); // M5 独有（老板 2026-08-23：rag 工具名称="擦拭布"）；M2/M3/M4 无 RagHome 自动跳过
            }
        }

        /// <summary>冻结 M2 的槽位 bg 是 Sliced 内置图；运行时改为 M3 同款无 Sprite 白底。</summary>
        private static void NormalizeM2SlotBackground(Transform slot)
        {
            var image = slot != null ? slot.Find("bg")?.GetComponent<Image>() : null;
            if (image == null) return;
            foreach (var outline in image.GetComponents<Outline>()) outline.enabled = false;
            image.sprite = null;
            image.type = Image.Type.Simple;
            image.color = new Color(1f, 1f, 1f, .9f);
        }

        /// <summary>递归隐藏 ToolShelf/Tool 下所有 Chip 节点（M2/M5 共用：对齐 M3 槽底 ~ToolLabel 样式，空 Chip 不保留）。</summary>
        private static void HideChipNodes(RectTransform root)
        {
            foreach (RectTransform child in root)
            {
                if (child == null) continue;
                if (child.name == "Chip") child.gameObject.SetActive(false);
                HideChipNodes(child);
            }
        }

        /// <summary>修复 ToolShelf 下所有名字为“Text (TMP)”的节点字体（老板创建的 Chip 标注，默认字体导致中文乱码）。</summary>
        private static void FixChipTextFonts(RectTransform shelf)
        {
            var cn = FindCnFont();
            if (cn == null) return;
            foreach (var tmp in shelf.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                if (tmp.name == "Text (TMP)" && tmp.font != null && tmp.font != cn) tmp.font = cn;
            }
        }

        private static Transform FindChild(Transform root, string name)
        {
            foreach (Transform child in root)
            {
                if (child.name == name) return child;
                var hit = FindChild(child, name);
                if (hit != null) return hit;
            }
            return null;
        }

        /// <summary>槽位底部创建浅色文字标注（幂等；锚槽位底边下方）。
        /// 槽位内已有同名内嵌文字（如 M2 Chip 按钮内的「K2.5 探头」/「多功能尺子」，老板手工加）时跳过，避免同一工具两个名字。</summary>
        private static void EnsureLabel(Transform slot, string text, string keyword)
        {
            if (slot == null) return;
            var existing = FindChild(slot, LabelSuffix);
            if (existing != null) return;
            if (HasEmbeddedName(slot, keyword)) return; // Chip 内已有工具名 → 一个就够了

            var go = new GameObject(LabelSuffix, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            go.hideFlags = HideFlags.DontSave;
            go.transform.SetParent(slot, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(.5f, 0f);
            rt.anchorMax = new Vector2(.5f, 0f);
            rt.pivot = new Vector2(.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -10f);
            rt.sizeDelta = new Vector2(160f, 30f);

            var tmp = go.GetComponent<TextMeshProUGUI>();
            tmp.font = FindCnFont(); // 中文 SDF（避免默认字体无中文字形导致乱码）
            if (tmp.font == null) { var any = Object.FindFirstObjectByType<TextMeshProUGUI>(); if (any != null) tmp.font = any.font; }
            tmp.fontSize = 24f;
            tmp.color = new Color(.3f, .34f, .38f, 1f); // 浅灰深字（浅色工具架底）
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.text = text;
        }

        /// <summary>槽位子树内是否已有激活的、包含关键词的 TMP 文本（含 Chip 按钮内嵌工具名；隐藏的 Chip 不算，M2 对齐 M3 用）。</summary>
        private static bool HasEmbeddedName(Transform slot, string keyword)
        {
            if (string.IsNullOrEmpty(keyword)) return false;
            foreach (var tmp in slot.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                if (!tmp.gameObject.activeInHierarchy) continue; // M2 隐藏 Chip 后不再拦截 ~ToolLabel
                if (!string.IsNullOrEmpty(tmp.text) && tmp.text.Contains(keyword)) return true;
            }
            return false;
        }
    }
}
