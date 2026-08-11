using M1;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace M1.EditorTools
{
    /// <summary>
    /// 回滚工具：Tools/M1/还原 M1 界面布局
    /// 撤销「优化 M1 界面布局」的全部改动，把场景恢复到美化前的参数：
    ///   - CanvasScaler matchWidthOrHeight 0 → 0.5
    ///   - 标题栏 120→160、标题字号 40→50、标题回到 anchor(0.5,0.5) 原位置
    ///   - 卡片 372→356、PreserveAspect 关、容器回到 anchor(0.5,0.5) 原位置
    ///   - QAPanel 700→580（与 M1QASetup 一致）、Header 100→110、Header 标题 40→34
    ///   - 输入行：输入框 420→360、语音 110→76、发送 110→106 字号 32、计数器回 InputRow 顶部
    ///   - 输入行排布按 580 面板宽度（与 M1QASetup 一致：输入 18..378、语音 388..464、发送 472..578）
    /// 幂等：重复执行结果不变。
    /// 注意：恢复完成后本工具与 M1QASetup 的新常量将被移除，请勿再次运行优化工具。
    /// </summary>
    public static class M1LayoutPolish
    {
        private const string ScenePath = "Assets/Settings/Scenes/M1.unity";
        private const string BoardName = "画板";

        private static readonly string[] ToolCardNames =
        {
            "超声波焊缝探伤仪", "手推式钢轨探伤仪", "双轨式探伤仪",
            "轨距尺", "钢轨打磨机", "内燃威客镐"
        };

        [MenuItem("Tools/M1/还原 M1 界面布局")]
        public static void PolishLayout()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var board = GameObject.Find(BoardName);
            if (board == null)
            {
                Debug.LogError("[M1LayoutPolish] 未找到场景物体：" + BoardName);
                return;
            }

            Undo.RegisterFullObjectHierarchyUndo(board, "M1 Layout Restore");
            int changed = 0;

            // 1) CanvasScaler：0.5 宽高各半匹配
            var scaler = board.GetComponent<CanvasScaler>();
            if (scaler != null && !Mathf.Approximately(scaler.matchWidthOrHeight, 0.5f))
            {
                scaler.matchWidthOrHeight = 0.5f;
                changed++;
            }

            // 2) 标题栏：120 → 160，中心回到 y=-80
            var bar = FindIncludingInactive(board.transform, "标题栏")?.GetComponent<RectTransform>();
            if (bar != null)
            {
                bar.anchoredPosition = new Vector2(bar.anchoredPosition.x, -80f);
                bar.sizeDelta = new Vector2(bar.sizeDelta.x, 160f);
                changed++;
            }

            // 3) 左侧标题：字号 40→50，回到 anchor(0.5,0.5) 原位置
            var title = FindIncludingInactive(board.transform, "标题")?.GetComponent<RectTransform>();
            if (title != null)
            {
                title.anchorMin = new Vector2(0.5f, 0.5f);
                title.anchorMax = new Vector2(0.5f, 0.5f);
                title.pivot = new Vector2(0.5f, 0.5f);
                title.anchoredPosition = new Vector2(-405.01282f, 0.000016928f);
                title.sizeDelta = new Vector2(1002.4791f, 93.9398f);
                var tmp = title.GetComponent<TextMeshProUGUI>();
                if (tmp != null) { tmp.fontSize = 50; tmp.alignment = TextAlignmentOptions.Center; }
                changed++;
            }

            // 4) 卡片容器：回到 anchor(0.5,0.5) 原位置原尺寸
            var items = FindIncludingInactive(board.transform, "白板背景/M1物品")?.GetComponent<RectTransform>();
            if (items != null)
            {
                items.anchorMin = new Vector2(0.5f, 0.5f);
                items.anchorMax = new Vector2(0.5f, 0.5f);
                items.pivot = new Vector2(0.5f, 0.5f);
                items.anchoredPosition = new Vector2(-394f, -73f);
                items.sizeDelta = new Vector2(1110.841f, 781.4125f);
                changed++;
            }

            // 5) 6 张工具卡片：356、原位置、PreserveAspect 关闭
            foreach (var name in ToolCardNames)
            {
                var card = FindIncludingInactive(board.transform, name)?.GetComponent<RectTransform>();
                if (card == null) { Debug.LogWarning("[M1LayoutPolish] 未找到卡片：" + name); continue; }
                card.sizeDelta = new Vector2(356.4242f, 356.4242f);
                var pos = CardPosForAnchor(card.anchorMin, card.anchorMax);
                card.anchoredPosition = pos;
                var img = card.GetComponent<Image>();
                if (img != null) img.preserveAspect = false;
                changed++;
            }

            // 6) QAPanel：700 → 580（与 M1QASetup 一致）
            var panel = FindIncludingInactive(board.transform, "QAPanel")?.GetComponent<RectTransform>();
            if (panel != null)
            {
                panel.sizeDelta = new Vector2(580f, panel.sizeDelta.y);
                changed++;
            }

            // 7) Header：100 → 110；标题字号 40 → 34
            var header = FindIncludingInactive(board.transform, "QAPanel/Header")?.GetComponent<RectTransform>();
            if (header != null)
            {
                header.sizeDelta = new Vector2(header.sizeDelta.x, 110f);
                changed++;
            }
            var headerTitle = FindIncludingInactive(board.transform, "QAPanel/Header/Title")?.GetComponent<TextMeshProUGUI>();
            if (headerTitle != null) { headerTitle.fontSize = 34; changed++; }

            // 8) MessageList：offsetMax.y 回到 -240（110+130）
            var list = FindIncludingInactive(board.transform, "QAPanel/MessageList")?.GetComponent<RectTransform>();
            if (list != null)
            {
                list.offsetMax = new Vector2(list.offsetMax.x, -240f);
                changed++;
            }

            // 9) InputRow：120 → 130
            var row = FindIncludingInactive(board.transform, "QAPanel/InputRow")?.GetComponent<RectTransform>();
            if (row != null)
            {
                row.sizeDelta = new Vector2(row.sizeDelta.x, 130f);
                changed++;
            }

            // 10) 输入框：420 → 360（580 面板）；Text Area 右边距 -80 → -12
            var input = FindIncludingInactive(board.transform, "QAPanel/InputRow/InputField")?.GetComponent<RectTransform>();
            if (input != null)
            {
                input.sizeDelta = new Vector2(360f, input.sizeDelta.y);
                changed++;
            }
            var textArea = FindIncludingInactive(board.transform, "QAPanel/InputRow/InputField/Text Area")?.GetComponent<RectTransform>();
            if (textArea != null)
            {
                textArea.offsetMax = new Vector2(-12f, textArea.offsetMax.y);
                changed++;
            }

            // 11) 语音 110→76 @pos 388；发送 110→106 @pos 472 字号 32（580 面板排布）
            var voice = FindIncludingInactive(board.transform, "QAPanel/InputRow/VoiceButton")?.GetComponent<RectTransform>();
            if (voice != null)
            {
                voice.anchoredPosition = new Vector2(388f, voice.anchoredPosition.y);
                voice.sizeDelta = new Vector2(76f, voice.sizeDelta.y);
                changed++;
            }
            var send = FindIncludingInactive(board.transform, "QAPanel/InputRow/SendButton")?.GetComponent<RectTransform>();
            if (send != null)
            {
                send.anchoredPosition = new Vector2(472f, send.anchoredPosition.y);
                send.sizeDelta = new Vector2(106f, send.sizeDelta.y);
                changed++;
            }
            var sendText = FindIncludingInactive(board.transform, "QAPanel/InputRow/SendButton/Text")?.GetComponent<TextMeshProUGUI>();
            if (sendText != null) { sendText.fontSize = 32; changed++; }

            // 12) 字数计数：从输入框内部迁回 InputRow 顶部（幂等：已迁回则原地重设）
            var counter = FindIncludingInactive(board.transform, "CounterText")?.GetComponent<RectTransform>();
            if (counter != null)
            {
                if (row != null) counter.SetParent(row, false);
                counter.anchorMin = new Vector2(0f, 1f);
                counter.anchorMax = new Vector2(0f, 1f);
                counter.pivot = new Vector2(0f, 1f);
                counter.anchoredPosition = new Vector2(18f, -4f);
                counter.sizeDelta = new Vector2(120f, 24f);
                var ctmp = counter.GetComponent<TextMeshProUGUI>();
                if (ctmp != null) { ctmp.fontSize = 22; ctmp.alignment = TextAlignmentOptions.Left; }
                changed++;
            }

            // 13) 运行时组件路径还原（真实层级：QAPanel 下无虚构 Panel 中间层）
            var qa = board.GetComponent<M1QAPanel>();
            if (qa != null && qa.counterTextPath != "QAPanel/InputRow/CounterText")
            {
                qa.counterTextPath = "QAPanel/InputRow/CounterText";
                EditorUtility.SetDirty(qa);
                changed++;
            }

            EditorUtility.SetDirty(board);
            EditorSceneManager.MarkSceneDirty(scene);
            var saved = EditorSceneManager.SaveScene(scene);
            Debug.Log($"[M1LayoutPolish] 还原完成：改动 {changed} 处，场景保存={saved}（幂等，可重复执行）。");
        }

        /// <summary>按卡片锚点计算 356×356 卡片的中心位置（容器内，间隙约 21/69）。</summary>
        private static Vector2 CardPosForAnchor(Vector2 anchorMin, Vector2 anchorMax)
        {
            const float half = 178.2121f; // 356.4242 / 2
            float x = Mathf.Approximately(anchorMin.x, 1f) ? -half
                    : Mathf.Approximately(anchorMin.x, 0.5f) ? 0f : half;
            float y = Mathf.Approximately(anchorMax.y, 1f) ? -half : half;
            return new Vector2(x, y);
        }

        private static Transform FindIncludingInactive(Transform root, string path)
        {
            Transform cur = root;
            foreach (var part in path.Split('/'))
            {
                cur = FindChild(cur, part);
                if (cur == null) return null;
            }
            return cur;
        }

        private static Transform FindChild(Transform parent, string name)
        {
            foreach (Transform child in parent)
            {
                if (child.name == name) return child;
                var hit = FindChild(child, name);
                if (hit != null) return hit;
            }
            return null;
        }
    }
}
