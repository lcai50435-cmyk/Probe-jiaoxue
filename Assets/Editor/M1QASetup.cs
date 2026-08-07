using M1;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace M1.EditorTools
{
    /// <summary>
    /// 编辑器一键配置：Tools/M1/Setup AI 提问面板
    /// 在 "画板" 下创建右侧抽屉式 AI 提问面板（原型）：
    ///   Blocker（全屏半透明挡板，点击关闭）
    ///   QAPanel（右侧抽屉：Header + MessageList + InputRow）
    /// 并挂载 M1QAPanel 运行时脚本、注入中文字体与 DeepSeek 客户端。
    /// 幂等：重复执行不会重复创建。
    /// </summary>
    public static class M1QASetup
    {
        private const string ScenePath = "Assets/Settings/Scenes/M1.unity";
        private const string BoardName = "画板";
        private const string FontAssetPath =
            "Assets/font/sarasa-gothic-sc-regular/sarasa-gothic-sc-regular_cn.asset";
        private const string PanelName = "QAPanel";
        private const string BlockerName = "Blocker";
        private const float PanelWidth = 780f;
        private const float HeaderHeight = 110f;
        private const float InputRowHeight = 130f;

        [MenuItem("Tools/M1/Setup AI 提问面板 %#&q")]
        public static void SetupQAPanel()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var board = GameObject.Find(BoardName);
            if (board == null)
            {
                Debug.LogError("[M1QASetup] 未找到场景物体：" + BoardName);
                return;
            }

            var cnFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            if (cnFont == null)
            {
                Debug.LogError("[M1QASetup] 未找到中文字体资产：" + FontAssetPath +
                               "，请先运行 Tools/字体/重新生成中文字体资产 (Sarasa Gothic)。");
                return;
            }

            Undo.RegisterFullObjectHierarchyUndo(board, "M1 QA Panel");

            // 1) 全屏挡板（先创建，渲染在下层）
            var blockerGo = FindIncludingInactive(board.transform, BlockerName)?.gameObject;
            if (blockerGo == null)
            {
                blockerGo = new GameObject(BlockerName,
                    typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
                blockerGo.transform.SetParent(board.transform, false);
                var brt = blockerGo.GetComponent<RectTransform>();
                brt.anchorMin = Vector2.zero;
                brt.anchorMax = Vector2.one;
                brt.offsetMin = Vector2.zero;
                brt.offsetMax = Vector2.zero;
                var bimg = blockerGo.GetComponent<Image>();
                bimg.color = new Color(0f, 0f, 0f, 0.55f);
                blockerGo.SetActive(false);
            }

            // 2) 抽屉面板
            var panelGo = FindIncludingInactive(board.transform, PanelName)?.gameObject;
            if (panelGo == null) panelGo = CreatePanel(board, cnFont);

            // 3) 运行时脚本
            var comp = board.GetComponent<M1QAPanel>();
            if (comp == null) comp = board.AddComponent<M1QAPanel>();
            comp.panelPath = PanelName;
            comp.blockerPath = BlockerName;
            comp.closeButtonPath = PanelName + "/Header/CloseButton";
            comp.messageContentPath = PanelName + "/MessageList/Viewport/Content";
            comp.inputFieldPath = PanelName + "/InputRow/InputField";
            comp.voiceButtonPath = PanelName + "/InputRow/VoiceButton";
            comp.sendButtonPath = PanelName + "/InputRow/SendButton";
            comp.counterTextPath = PanelName + "/InputRow/CounterText";
            comp.cnFont = cnFont;

            // 4) DeepSeek 客户端（apiKey 由用户手填，Setup 不写值，重复执行保留）
            var client = board.GetComponent<M1DeepSeekClient>();
            if (client == null) client = board.AddComponent<M1DeepSeekClient>();
            comp.deepSeekClient = client;
            EditorUtility.SetDirty(client);
            EditorUtility.SetDirty(comp);

            EditorSceneManager.MarkSceneDirty(scene);
            var saved = EditorSceneManager.SaveScene(scene);
            Debug.Log($"[M1QASetup] 完成：面板={panelGo.name} 挡板={blockerGo.name} 挂载 {comp.GetType().Name} 场景保存={saved}");
        }

        private static GameObject CreatePanel(GameObject board, TMP_FontAsset cnFont)
        {
            // ---------- 抽屉根 ----------
            var panelGo = new GameObject(PanelName,
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            panelGo.transform.SetParent(board.transform, false);
            var prt = panelGo.GetComponent<RectTransform>();
            prt.anchorMin = new Vector2(1f, 0f);
            prt.anchorMax = new Vector2(1f, 1f);
            prt.pivot = new Vector2(1f, 0.5f);
            prt.anchoredPosition = Vector2.zero;
            prt.sizeDelta = new Vector2(PanelWidth, 0f);
            var pimg = panelGo.GetComponent<Image>();
            pimg.color = new Color(0.985f, 0.985f, 0.985f, 1f);

            // ---------- Header ----------
            var headerGo = new GameObject("Header",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            headerGo.transform.SetParent(panelGo.transform, false);
            var hrt = headerGo.GetComponent<RectTransform>();
            hrt.anchorMin = new Vector2(0f, 1f);
            hrt.anchorMax = new Vector2(1f, 1f);
            hrt.pivot = new Vector2(0.5f, 1f);
            hrt.anchoredPosition = Vector2.zero;
            hrt.sizeDelta = new Vector2(0f, HeaderHeight);
            var himg = headerGo.GetComponent<Image>();
            himg.color = new Color(0.93f, 0.93f, 0.93f, 1f);

            var titleGo = CreateText("Title", headerGo.transform, "向铁小探提问", cnFont, 34,
                new Color(0.15f, 0.15f, 0.15f, 1f));
            var trt = titleGo.GetComponent<RectTransform>();
            trt.anchorMin = new Vector2(0f, 0.5f);
            trt.anchorMax = new Vector2(0f, 0.5f);
            trt.pivot = new Vector2(0f, 0.5f);
            trt.anchoredPosition = new Vector2(28f, 0f);
            trt.sizeDelta = new Vector2(500f, 60f);

            var closeGo = new GameObject("CloseButton",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            closeGo.transform.SetParent(headerGo.transform, false);
            var crt = closeGo.GetComponent<RectTransform>();
            crt.anchorMin = new Vector2(1f, 0.5f);
            crt.anchorMax = new Vector2(1f, 0.5f);
            crt.pivot = new Vector2(1f, 0.5f);
            crt.anchoredPosition = new Vector2(-20f, 0f);
            crt.sizeDelta = new Vector2(64f, 64f);
            var cimg = closeGo.GetComponent<Image>();
            cimg.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            cimg.type = Image.Type.Sliced;
            cimg.color = new Color(0.75f, 0.75f, 0.75f, 1f);
            closeGo.GetComponent<Button>().targetGraphic = cimg;
            var closeText = CreateText("Text", closeGo.transform, "×", cnFont, 36, Color.white);
            Stretch(closeText.GetComponent<RectTransform>());
            closeText.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Center;

            // ---------- MessageList（ScrollRect） ----------
            var listGo = new GameObject("MessageList",
                typeof(RectTransform), typeof(ScrollRect));
            listGo.transform.SetParent(panelGo.transform, false);
            var lrt = listGo.GetComponent<RectTransform>();
            lrt.anchorMin = new Vector2(0f, 0f);
            lrt.anchorMax = new Vector2(1f, 1f);
            lrt.offsetMin = Vector2.zero;
            lrt.offsetMax = new Vector2(0f, -(HeaderHeight + InputRowHeight));

            var viewportGo = new GameObject("Viewport",
                typeof(RectTransform), typeof(RectMask2D));
            viewportGo.transform.SetParent(listGo.transform, false);
            var vrt = viewportGo.GetComponent<RectTransform>();
            Stretch(vrt);

            var contentGo = new GameObject("Content",
                typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            contentGo.transform.SetParent(viewportGo.transform, false);
            var crt2 = contentGo.GetComponent<RectTransform>();
            crt2.anchorMin = new Vector2(0f, 1f);
            crt2.anchorMax = new Vector2(1f, 1f);
            crt2.pivot = new Vector2(0.5f, 1f);
            crt2.anchoredPosition = Vector2.zero;
            crt2.sizeDelta = new Vector2(0f, 0f);
            var vlg = contentGo.GetComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(14, 14, 12, 12);
            vlg.spacing = 14f;
            vlg.childControlWidth = true;
            vlg.childForceExpandWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandHeight = false;
            var csf = contentGo.GetComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = listGo.GetComponent<ScrollRect>();
            scroll.viewport = vrt;
            scroll.content = crt2;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;

            // ---------- InputRow ----------
            var rowGo = new GameObject("InputRow",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            rowGo.transform.SetParent(panelGo.transform, false);
            var rrt = rowGo.GetComponent<RectTransform>();
            rrt.anchorMin = new Vector2(0f, 0f);
            rrt.anchorMax = new Vector2(1f, 0f);
            rrt.pivot = new Vector2(0.5f, 0f);
            rrt.anchoredPosition = Vector2.zero;
            rrt.sizeDelta = new Vector2(0f, InputRowHeight);
            var rimg = rowGo.GetComponent<Image>();
            rimg.color = new Color(0.95f, 0.95f, 0.95f, 1f);

            // 输入框
            var inputGo = new GameObject("InputField",
                typeof(RectTransform), typeof(Image), typeof(TMP_InputField));
            inputGo.transform.SetParent(rowGo.transform, false);
            var irt = inputGo.GetComponent<RectTransform>();
            irt.anchorMin = new Vector2(0f, 0.5f);
            irt.anchorMax = new Vector2(0f, 0.5f);
            irt.pivot = new Vector2(0f, 0.5f);
            irt.anchoredPosition = new Vector2(18f, 4f);
            irt.sizeDelta = new Vector2(470f, 78f);
            var iimg = inputGo.GetComponent<Image>();
            iimg.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            iimg.type = Image.Type.Sliced;
            iimg.color = new Color(1f, 1f, 1f, 1f);

            var textAreaGo = new GameObject("Text Area",
                typeof(RectTransform), typeof(RectMask2D));
            textAreaGo.transform.SetParent(inputGo.transform, false);
            var tart = textAreaGo.GetComponent<RectTransform>();
            tart.anchorMin = Vector2.zero;
            tart.anchorMax = Vector2.one;
            tart.offsetMin = new Vector2(12f, 6f);
            tart.offsetMax = new Vector2(-12f, -6f);

            var textGo = CreateText("Text", textAreaGo.transform, string.Empty, cnFont, 30,
                new Color(0.15f, 0.15f, 0.15f, 1f));
            var txtRt = textGo.GetComponent<RectTransform>();
            Stretch(txtRt);
            var tmp = textGo.GetComponent<TextMeshProUGUI>();
            tmp.alignment = TextAlignmentOptions.MidlineLeft;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;

            var phGo = CreateText("Placeholder", textAreaGo.transform, "请输入问题（最多200字）", cnFont, 30,
                new Color(0.6f, 0.6f, 0.6f, 1f));
            var phRt = phGo.GetComponent<RectTransform>();
            Stretch(phRt);
            var phTmp = phGo.GetComponent<TextMeshProUGUI>();
            phTmp.alignment = TextAlignmentOptions.MidlineLeft;
            phTmp.textWrappingMode = TextWrappingModes.NoWrap;

            var input = inputGo.GetComponent<TMP_InputField>();
            input.textComponent = tmp;
            input.placeholder = phTmp;
            input.targetGraphic = iimg;
            input.characterLimit = 200;
            input.contentType = TMP_InputField.ContentType.Standard;
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.fontAsset = cnFont;
            input.pointSize = 30;

            // 语音按钮（占位）
            var voiceGo = CreateButton("VoiceButton", rowGo.transform, "语音", cnFont, 30,
                new Color(0.35f, 0.62f, 0.95f, 1f));
            var vrt2 = voiceGo.GetComponent<RectTransform>();
            vrt2.anchorMin = new Vector2(0f, 0.5f);
            vrt2.anchorMax = new Vector2(0f, 0.5f);
            vrt2.pivot = new Vector2(0f, 0.5f);
            vrt2.anchoredPosition = new Vector2(500f, 4f);
            vrt2.sizeDelta = new Vector2(76f, 78f);

            // 发送按钮
            var sendGo = CreateButton("SendButton", rowGo.transform, "发送", cnFont, 32,
                new Color(0.15f, 0.42f, 0.82f, 1f));
            var srt = sendGo.GetComponent<RectTransform>();
            srt.anchorMin = new Vector2(0f, 0.5f);
            srt.anchorMax = new Vector2(0f, 0.5f);
            srt.pivot = new Vector2(0f, 0.5f);
            srt.anchoredPosition = new Vector2(588f, 4f);
            srt.sizeDelta = new Vector2(130f, 78f);

            // 字数计数
            var counterGo = CreateText("CounterText", rowGo.transform, "0/200", cnFont, 22,
                new Color(0.55f, 0.55f, 0.55f, 1f));
            var ctrt = counterGo.GetComponent<RectTransform>();
            ctrt.anchorMin = new Vector2(0f, 1f);
            ctrt.anchorMax = new Vector2(0f, 1f);
            ctrt.pivot = new Vector2(0f, 1f);
            ctrt.anchoredPosition = new Vector2(18f, -4f);
            ctrt.sizeDelta = new Vector2(120f, 24f);

            panelGo.SetActive(false); // 初始隐藏（与 Blocker 一致）：运行时由长按数字人打开，避免编辑模式下误以为面板已可用
            return panelGo;
        }

        private static GameObject CreateText(string name, Transform parent, string text,
            TMP_FontAsset font, float fontSize, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var tmp = go.GetComponent<TextMeshProUGUI>();
            tmp.font = font;
            tmp.fontSize = fontSize;
            tmp.color = color;
            tmp.text = text;
            tmp.alignment = TextAlignmentOptions.Left;
            return go;
        }

        private static GameObject CreateButton(string name, Transform parent, string text,
            TMP_FontAsset font, float fontSize, Color color)
        {
            var go = new GameObject(name,
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            img.type = Image.Type.Sliced;
            img.color = color;
            go.GetComponent<Button>().targetGraphic = img;
            var textGo = CreateText("Text", go.transform, text, font, fontSize, Color.white);
            var trt = textGo.GetComponent<RectTransform>();
            Stretch(trt);
            var tmp = textGo.GetComponent<TextMeshProUGUI>();
            tmp.alignment = TextAlignmentOptions.Center;
            return go;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static Transform FindIncludingInactive(Transform root, string name)
        {
            foreach (Transform child in root)
            {
                if (child.name == name) return child;
                var hit = FindIncludingInactive(child, name);
                if (hit != null) return hit;
            }
            return null;
        }
    }
}
