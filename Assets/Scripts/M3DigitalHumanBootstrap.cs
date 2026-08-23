using M1;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;

namespace M3
{
    /// <summary>
    /// M3/M4 数字人 + AI 问答运行时装配器（冻结 Scene 零改动、零新增专用组件序列化）。
    /// M3/M4 场景只有静态 FullBodyPreview 与空 QAPanel 壳，且 M3 冻结不可序列化新组件；
    /// 故场景加载后自动补全 M2 同款 QA 链路：动态构建 QAPanel 面板结构（Header/MessageList/InputRow）、
    /// 数字人 FullBodyView/AvatarView，挂载并注入 M1QAPanel / M1DeepSeekClient / M1DigitalHumanPresenter，
    /// 实现三态动画（待机/思考/讲解随问答状态切换）、短按全身/头像、长按打开对话框。
    /// 素材走 Resources（三视频 + 折叠头像），LumaKey 材质运行时创建（Shader.Find，不复制资产）。
    /// 幂等：同场景只装配一次；全部为内存态修改，不写回场景文件。
    /// 注：运行时脚本超 150 行，理由——M3 冻结无法序列化组件，M1QASetup 的 Editor 结构需运行时镜像构建。
    /// </summary>
    public static class M3DigitalHumanBootstrap
    {
        private const string SafeAreaName = "SafeArea";
        private const string QAPanelName = "QAPanel";
        private const string BlockerName = "Blocker";
        private const string StageName = "DigitalHumanStage";
        private const string PreviewName = "FullBodyPreview";
        private const string FullBodyName = "FullBodyView";
        private const string AvatarName = "AvatarView";

        // 素材（Resources 路径，与 M1QASetup 同款素材）
        private const string IdleClipRes = "DigitalHuman/待机动画";
        private const string ThinkingClipRes = "DigitalHuman/思考动画";
        private const string SpeakingClipRes = "DigitalHuman/讲解动画2";
        private const string AvatarSpriteRes = "DigitalHuman/折叠头像";

        // 布局（1920x1080 基准，与 M1QASetup 合同一致）
        private const float PanelWidth = 580f;
        private const float HeaderHeight = 110f;
        private const float InputRowHeight = 130f;
        private const float StageWidth = 320f;
        private const float StageCenterOffsetY = 30f; // M3/M4 专属标定（老板 2026-08-18：数字人 Y=30，不再用 M1 的 -248）
        private const float AvatarSize = 120f;
        private const float StageAspect = 1080f / 1450f;
        private const float HiddenOffsetX = 960f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Init()
        {
            TryBootstrap();
            SceneManager.sceneLoaded += (_, _) => TryBootstrap();
        }

        private static void TryBootstrap()
        {
            try
            {
                TryBootstrapInner();
            }
            catch (System.Exception e)
            {
                // 防装配中断静默：异常时打日志，方便排查（不阻断其余功能）
                Debug.LogError("[M3DigitalHumanBootstrap] 装配异常：" + e);
            }
        }

        private static void TryBootstrapInner()
        {
            var scene = SceneManager.GetActiveScene().name;
            if (scene != "M3" && scene != "M4" && scene != "M5") return;
            var root = GameObject.Find(SafeAreaName) ?? GameObject.Find("Canvas");
            if (root == null) return;
            if (root.GetComponent<M1QAPanel>() != null) return; // 幂等：已装配（场景重载后动态节点销毁，组件判定自然放行）

            var cnFont = root.GetComponentInChildren<TextMeshProUGUI>(true)?.font;
            if (cnFont == null) { Debug.LogError("[M3DigitalHumanBootstrap] " + scene + " 未找到 TMP 字体，装配中止。"); return; }
            var panelGo = FindDeep(root.transform, QAPanelName)?.gameObject;
            var stageGo = FindDeep(root.transform, StageName)?.gameObject;
            if (panelGo == null || stageGo == null) { Debug.LogError("[M3DigitalHumanBootstrap] " + scene + " 缺 QAPanel/DigitalHumanStage，装配中止。"); return; }

            // 1) 挡板可点击关闭（M1QAPanel 依赖 Button）
            var blockerGo = FindDeep(root.transform, BlockerName)?.gameObject;
            if (blockerGo != null && blockerGo.GetComponent<Button>() == null)
            {
                var bImg = blockerGo.GetComponent<Image>();
                var bBtn = blockerGo.AddComponent<Button>();
                if (bImg != null) bBtn.targetGraphic = bImg;
            }

            // 2) QAPanel 面板结构（M1QAPanel 路径依赖，运行时内存构建不写回）
            BuildPanel(panelGo.transform, cnFont);

            // 3) 数字人视图
            BuildViews(stageGo.transform);

            // 4) M1QAPanel + M1DeepSeekClient（挂 SafeArea：FindDeep 基于它解析路径）
            // 先建临时“数字人”占位，避免 M1QAPanel.Awake 的 bindPressTarget 默认路径查找报错，装配后移除
            var dummy = new GameObject("数字人", typeof(RectTransform));
            dummy.transform.SetParent(root.transform, false);
            dummy.SetActive(false);
            var client = root.GetComponent<M1DeepSeekClient>();
            if (client == null) client = root.AddComponent<M1DeepSeekClient>(); // Unity 6 伪 null：?? 不触发，必须 if == null 分步
            var qa = root.GetComponent<M1QAPanel>();
            if (qa == null) qa = root.AddComponent<M1QAPanel>();
            qa.panelPath = "QAPanel"; qa.blockerPath = "Blocker";
            qa.closeButtonPath = "QAPanel/Header/CloseButton";
            qa.messageContentPath = "QAPanel/MessageList/Viewport/Content";
            qa.inputFieldPath = "QAPanel/InputRow/InputField";
            qa.voiceButtonPath = "QAPanel/InputRow/VoiceButton";
            qa.sendButtonPath = "QAPanel/InputRow/SendButton";
            qa.counterTextPath = "QAPanel/InputRow/CounterText";
            qa.bindPressTarget = false; qa.hiddenOffsetX = HiddenOffsetX;
            qa.cnFont = cnFont; qa.deepSeekClient = client;
            Object.Destroy(dummy); // 占位移除（M1QAPanel 已 Awake，长按入口由数字人 Presenter 接管）

            // 5) 数字人 Presenter：Stage 先 inactive 再 AddComponent（激活时才 Awake，引用已就绪）
            stageGo.SetActive(false);
            var presenter = stageGo.GetComponent<M1DigitalHumanPresenter>();
            if (presenter == null) presenter = stageGo.AddComponent<M1DigitalHumanPresenter>(); // Unity 6 伪 null 分步
            var fb = stageGo.transform.Find(FullBodyName);
            var av = stageGo.transform.Find(AvatarName);
            presenter.qaPanel = qa;
            presenter.player = fb.GetComponent<VideoPlayer>();
            presenter.rawImage = fb.GetComponent<RawImage>();
            presenter.fullBodyView = fb.gameObject;
            presenter.avatarView = av.gameObject;
            presenter.fullBodyPress = fb.GetComponent<M1PressDetector>();
            presenter.avatarPress = av.GetComponent<M1PressDetector>();
            presenter.idleClip = Resources.Load<VideoClip>(IdleClipRes);
            presenter.thinkingClip = Resources.Load<VideoClip>(ThinkingClipRes);
            presenter.speakingClip = Resources.Load<VideoClip>(SpeakingClipRes);
            if (presenter.idleClip == null) Debug.LogWarning("[M3DigitalHumanBootstrap] 未找到待机视频：" + IdleClipRes);
            if (presenter.thinkingClip == null) Debug.LogWarning("[M3DigitalHumanBootstrap] 未找到思考视频：" + ThinkingClipRes);
            if (presenter.speakingClip == null) Debug.LogWarning("[M3DigitalHumanBootstrap] 未找到讲解视频：" + SpeakingClipRes);
            stageGo.SetActive(true);
            if (SceneManager.GetActiveScene().name == "M3") presenter.SetShortPressEnabled(false); // M3 取消点击折叠（老板 2026-08-23）：保持全身，仅长按开面板（Awake 后解绑）

            Debug.Log("[M3DigitalHumanBootstrap] " + scene + " 数字人/问答装配完成。");
        }

        // ==================== 面板构建 ====================

        private static void BuildPanel(Transform panel, TMP_FontAsset font)
        {
            // 壳修正为 M2 合同：右侧全高贴边（运行时内存态，不写回）
            var prt = panel.GetComponent<RectTransform>();
            if (prt != null)
            {
                prt.anchorMin = new Vector2(1f, 0f); prt.anchorMax = new Vector2(1f, 1f);
                prt.pivot = new Vector2(1f, 0.5f); prt.anchoredPosition = Vector2.zero;
                prt.sizeDelta = new Vector2(PanelWidth, 0f);
            }
            // 隐藏壳内旧占位文本（QAPanel 直接子级）
            foreach (Transform child in panel)
                if (child.name == "Placeholder") child.gameObject.SetActive(false);

            // Header
            var header = NewImage("Header", panel, new Color(0.93f, 0.93f, 0.93f, 1f));
            var hrt = header.GetComponent<RectTransform>();
            hrt.anchorMin = new Vector2(0f, 1f); hrt.anchorMax = new Vector2(1f, 1f);
            hrt.pivot = new Vector2(0.5f, 1f); hrt.sizeDelta = new Vector2(0f, HeaderHeight);

            var title = NewTmp("Title", header.transform, font, 34, new Color(0.15f, 0.15f, 0.15f, 1f), "向铁小探提问");
            var trt = title.GetComponent<RectTransform>();
            trt.anchorMin = new Vector2(0f, 0.5f); trt.anchorMax = new Vector2(0f, 0.5f);
            trt.pivot = new Vector2(0f, 0.5f); trt.anchoredPosition = new Vector2(28f, 0f); trt.sizeDelta = new Vector2(400f, 60f);

            var close = NewButton("CloseButton", header.transform, font, 36, "×",
                new Color(0.75f, 0.75f, 0.75f, 1f), Color.white);
            var crt = close.GetComponent<RectTransform>();
            crt.anchorMin = new Vector2(1f, 0.5f); crt.anchorMax = new Vector2(1f, 0.5f);
            crt.pivot = new Vector2(1f, 0.5f); crt.anchoredPosition = new Vector2(-20f, 0f); crt.sizeDelta = new Vector2(64f, 64f);

            // MessageList（ScrollRect）
            var list = NewGo("MessageList", panel);
            var lrt = list.GetComponent<RectTransform>();
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
            lrt.offsetMin = Vector2.zero; lrt.offsetMax = new Vector2(0f, -(HeaderHeight + InputRowHeight));
            var scroll = list.AddComponent<ScrollRect>();
            scroll.horizontal = false; scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 30f;

            var viewport = NewGo("Viewport", list.transform);
            var vrt = viewport.GetComponent<RectTransform>();
            Stretch(vrt); vrt.offsetMax = new Vector2(-14f, 0f);
            viewport.AddComponent<RectMask2D>();

            var content = NewGo("Content", viewport.transform);
            var crt2 = content.GetComponent<RectTransform>();
            crt2.anchorMin = new Vector2(0f, 1f); crt2.anchorMax = new Vector2(1f, 1f);
            crt2.pivot = new Vector2(0.5f, 1f); crt2.anchoredPosition = Vector2.zero;
            var vlg = content.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(14, 14, 12, 12); vlg.spacing = 2f;
            vlg.childControlWidth = true; vlg.childForceExpandWidth = true;
            vlg.childControlHeight = false; vlg.childForceExpandHeight = false;
            var csf = content.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = vrt; scroll.content = crt2;

            // InputRow
            var row = NewImage("InputRow", panel, new Color(0.95f, 0.95f, 0.95f, 1f));
            var rrt = row.GetComponent<RectTransform>();
            rrt.anchorMin = new Vector2(0f, 0f); rrt.anchorMax = new Vector2(1f, 0f);
            rrt.pivot = new Vector2(0.5f, 0f); rrt.sizeDelta = new Vector2(0f, InputRowHeight);

            var input = NewGo("InputField", row.transform);
            var irt = input.GetComponent<RectTransform>();
            irt.anchorMin = new Vector2(0f, 0.5f); irt.anchorMax = new Vector2(0f, 0.5f);
            irt.pivot = new Vector2(0f, 0.5f); irt.anchoredPosition = new Vector2(18f, 4f); irt.sizeDelta = new Vector2(360f, 78f);
            var iimg = input.AddComponent<Image>(); iimg.color = Color.white;
            var tmpInput = input.AddComponent<TMP_InputField>();
            var textArea = NewGo("Text Area", input.transform);
            var tart = textArea.GetComponent<RectTransform>();
            Stretch(tart); tart.offsetMin = new Vector2(12f, 6f); tart.offsetMax = new Vector2(-12f, -6f);
            textArea.AddComponent<RectMask2D>();
            var txt = NewTmp("Text", textArea.transform, font, 30, new Color(0.15f, 0.15f, 0.15f, 1f), string.Empty);
            Stretch(txt.GetComponent<RectTransform>());
            txt.alignment = TextAlignmentOptions.MidlineLeft; txt.textWrappingMode = TextWrappingModes.NoWrap;
            var ph = NewTmp("Placeholder", textArea.transform, font, 30, new Color(0.6f, 0.6f, 0.6f, 1f), "请输入问题（最多200字）");
            Stretch(ph.GetComponent<RectTransform>());
            ph.alignment = TextAlignmentOptions.MidlineLeft; ph.textWrappingMode = TextWrappingModes.NoWrap;
            tmpInput.textComponent = txt; tmpInput.placeholder = ph; tmpInput.targetGraphic = iimg;
            tmpInput.characterLimit = 200; tmpInput.fontAsset = font; tmpInput.pointSize = 30;
            tmpInput.contentType = TMP_InputField.ContentType.Standard;
            tmpInput.lineType = TMP_InputField.LineType.SingleLine;

            var voice = NewButton("VoiceButton", row.transform, font, 30, "语音",
                new Color(0.35f, 0.62f, 0.95f, 1f), Color.white);
            var vrt2 = voice.GetComponent<RectTransform>();
            vrt2.anchorMin = new Vector2(0f, 0.5f); vrt2.anchorMax = new Vector2(0f, 0.5f);
            vrt2.pivot = new Vector2(0f, 0.5f); vrt2.anchoredPosition = new Vector2(388f, 4f); vrt2.sizeDelta = new Vector2(76f, 78f);

            var send = NewButton("SendButton", row.transform, font, 32, "发送",
                new Color(0.15f, 0.42f, 0.82f, 1f), Color.white);
            var srt = send.GetComponent<RectTransform>();
            srt.anchorMin = new Vector2(0f, 0.5f); srt.anchorMax = new Vector2(0f, 0.5f);
            srt.pivot = new Vector2(0f, 0.5f); srt.anchoredPosition = new Vector2(472f, 4f); srt.sizeDelta = new Vector2(106f, 78f);

            var counter = NewTmp("CounterText", row.transform, font, 22, new Color(0.55f, 0.55f, 0.55f, 1f), "0/200");
            var crt3 = counter.GetComponent<RectTransform>();
            crt3.anchorMin = new Vector2(0f, 1f); crt3.anchorMax = new Vector2(0f, 1f);
            crt3.pivot = new Vector2(0f, 1f); crt3.anchoredPosition = new Vector2(18f, -4f); crt3.sizeDelta = new Vector2(120f, 24f);
        }

        // ==================== 数字人视图 ====================

        private static void BuildViews(Transform stage)
        {
            var preview = stage.Find(PreviewName);
            if (preview != null) preview.gameObject.SetActive(false);

            var mat = CreateLumaKey();
            // M5 数字人 = M2 合同（老板 2026-08-23：M5 是 M2 轨顶基线，数字人与 M2 一致）；M3/M4 用 StageCenterOffsetY=30 标定
            var isM5 = SceneManager.GetActiveScene().name == "M5";

            // 复用 Scene 已序列化的 FullBodyView 壳（M5：M5Setup 创建，Scene 白色长方形可调整，布局 Scene 权威）；M3/M4 无壳则运行时创建
            var existingFb = stage.Find(FullBodyName);
            GameObject fb;
            RawImage raw;
            VideoPlayer vp;
            if (existingFb != null && existingFb.GetComponent<RawImage>() != null)
            {
                fb = existingFb.gameObject;
                if (existingFb.GetComponent<AspectRatioFitter>() == null) existingFb.gameObject.AddComponent<AspectRatioFitter>();
                raw = existingFb.GetComponent<RawImage>();
                vp = existingFb.GetComponent<VideoPlayer>();
                if (vp == null) vp = existingFb.gameObject.AddComponent<VideoPlayer>(); // Unity 6 伪 null：?? 不触发会抛 MissingComponentException
                if (existingFb.GetComponent<M1PressDetector>() == null) existingFb.gameObject.AddComponent<M1PressDetector>();
            }
            else
            {
                fb = NewGo(FullBodyName, stage);
                var frt = fb.GetComponent<RectTransform>();
                var fitter = fb.AddComponent<AspectRatioFitter>();
                if (isM5)
                {
                    // M2 合同：底部全高锚定 + HeightControlsWidth（宽=高×ratio），pos (-13,-35)
                    frt.anchorMin = new Vector2(0.5f, 0f); frt.anchorMax = new Vector2(0.5f, 1f);
                    frt.pivot = new Vector2(0.5f, 0.5f);
                    frt.anchoredPosition = new Vector2(-13f, -35f);
                    frt.sizeDelta = new Vector2(320f, 0f);
                    fitter.aspectMode = AspectRatioFitter.AspectMode.HeightControlsWidth;
                }
                else
                {
                    frt.anchorMin = new Vector2(0.5f, 0.5f); frt.anchorMax = new Vector2(0.5f, 0.5f);
                    frt.pivot = new Vector2(0.5f, 0.5f);
                    frt.anchoredPosition = new Vector2(0f, StageCenterOffsetY);
                    frt.sizeDelta = new Vector2(StageWidth, 0f);
                    fitter.aspectMode = AspectRatioFitter.AspectMode.WidthControlsHeight;
                }
                fitter.aspectRatio = StageAspect;
                raw = fb.AddComponent<RawImage>();
                vp = fb.AddComponent<VideoPlayer>();
                fb.AddComponent<M1PressDetector>();
            }
            raw.raycastTarget = true;
            if (mat != null) raw.material = mat;
            vp.playOnAwake = false; vp.isLooping = true;
            vp.audioOutputMode = VideoAudioOutputMode.None; // 强制静音，与 M1/M2 一致
            vp.skipOnDrop = true;

            var av = NewGo(AvatarName, stage);
            var art = av.GetComponent<RectTransform>();
            art.anchorMin = new Vector2(0.5f, 0.5f); art.anchorMax = new Vector2(0.5f, 0.5f);
            art.pivot = new Vector2(0.5f, 0.5f);
            art.anchoredPosition = new Vector2(0f, isM5 ? -40f : StageCenterOffsetY); // M5 头像对齐 M2（pos y=-40）
            art.sizeDelta = new Vector2(AvatarSize, AvatarSize);
            var aimg = av.AddComponent<Image>();
            var sprites = Resources.LoadAll<Sprite>(AvatarSpriteRes);
            if (sprites.Length > 0) aimg.sprite = sprites[0];
            else Debug.LogWarning("[M3DigitalHumanBootstrap] 未找到折叠头像：" + AvatarSpriteRes);
            aimg.raycastTarget = true;
            av.AddComponent<M1PressDetector>();
            av.SetActive(false); // 默认全身态，头像由 Presenter 运行时切换
        }

        private static Material CreateLumaKey()
        {
            var shader = Shader.Find("UI/LumaKey");
            if (shader == null) { Debug.LogWarning("[M3DigitalHumanBootstrap] 未找到 UI/LumaKey shader。"); return null; }
            var mat = new Material(shader);
            mat.SetFloat("_KeyThreshold", 0.02f); // 常驻数字人收窄羽化（与 UI-LumaKey-DigitalHuman.mat 同参）
            mat.SetFloat("_KeySmooth", 0.006f);
            return mat;
        }

        // ==================== 工具 ====================

        private static GameObject NewGo(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        private static GameObject NewImage(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.color = color;
            return go;
        }

        private static TextMeshProUGUI NewTmp(string name, Transform parent, TMP_FontAsset font,
            float size, Color color, string text)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var tmp = go.GetComponent<TextMeshProUGUI>();
            tmp.font = font; tmp.fontSize = size; tmp.color = color; tmp.text = text;
            tmp.alignment = TextAlignmentOptions.Left;
            return tmp;
        }

        private static GameObject NewButton(string name, Transform parent, TMP_FontAsset font,
            float size, string text, Color bg, Color fg)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>(); img.color = bg;
            go.GetComponent<Button>().targetGraphic = img;
            var tmp = NewTmp("Text", go.transform, font, size, fg, text);
            Stretch(tmp.GetComponent<RectTransform>());
            tmp.alignment = TextAlignmentOptions.Center;
            return go;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }

        private static Transform FindDeep(Transform root, string name)
        {
            if (root == null) return null;
            foreach (Transform child in root)
            {
                if (child.name == name) return child;
                var hit = FindDeep(child, name);
                if (hit != null) return hit;
            }
            return null;
        }
    }
}
