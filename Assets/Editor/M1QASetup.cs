using M1;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace M1.EditorTools
{
    /// <summary>
    /// 编辑器一键配置：Tools/M1/Setup AI 提问面板
    /// 在 "画板" 下创建/维护：
    ///   Blocker（全屏半透明挡板，点击关闭）
    ///   ChatArea（全屏，右侧为数字人舞台预留空间；QAPanel 重挂其下）
    ///   QAPanel（右侧抽屉：Header + MessageList + InputRow，宽 580）
    ///   DigitalHumanStage（根级、置于最后：盖过 Blocker/QAPanel，不被压暗或拦截）
    ///     FullBodyView（RawImage+VideoPlayer+PressDetector，全身三态视频，UI-LumaKey 抠像、强制静音）
    ///     AvatarView（A-05 折叠头像 + PressDetector）
    /// 并挂载 M1QAPanel / M1DigitalHumanPresenter 运行时脚本，注入中文字体、DeepSeek 客户端、
    /// 三个指定 MP4（不加载对应 WebM）、常驻数字人专用 LumaKey 材质（同 shader 收窄羽化，不碰开场引导）。
    /// 幂等：重复执行不重复创建；重挂/自愈布局；仅当素材字段为空时注入（不覆盖用户替换）。
    /// </summary>
    public static class M1QASetup
    {
        private const string ScenePath = "Assets/Settings/Scenes/M1.unity";
        private const string BoardName = "画板";
        private const string FontAssetPath =
            "Assets/font/sarasa-gothic-sc-regular/sarasa-gothic-sc-regular_cn.asset";
        private const string PanelName = "QAPanel";
        private const string BlockerName = "Blocker";
        private const string ChatAreaName = "ChatArea";
        private const string StageName = "DigitalHumanStage";
        private const string FullBodyName = "FullBodyView";
        private const string AvatarName = "AvatarView";
        private const string OldFaceName = "背景圆";

        // 布局（1920x1080 基准；窄屏由 CanvasScaler 整体缩放）
        private const float PanelWidth = 580f;           // 问答面板宽度（设计区间 560-600）
        private const float ChatAreaRightOffset = 344f;  // 右侧预留 = 舞台 320 + 安全边距 24
        private const float StageWidth = 320f;
        private const float StageMargin = 24f;
        private const float StageCenterOffsetY = -248f; // 全身/头像共用舞台视觉中心（Setup 为真源，重跑自愈）
        private const float AvatarSize = 120f;
        private const float StageAspect = 1080f / 1450f; // 三个 MP4 均为 1080x1450 竖屏
        private const float HiddenOffsetX = 960f;        // 关闭时滑出距离（> 舞台预留+面板宽，保证完全离屏）

        private const float HeaderHeight = 110f;
        private const float InputRowHeight = 130f;
        private const float InputFieldWidth = 360f;      // 输入框/语音/发送按 580 面板重新排布
        private const float VoiceWidth = 76f;
        private const float SendWidth = 106f;
        private const float MessageSpacing = 2f;         // 消息行基础间距（微信风格：连续消息紧凑）
        private const float ScrollbarWidth = 10f;        // 滚动条宽度
        private const float ScrollbarReserve = 14f;      // Viewport 右侧为滚动条让位宽度

        // 素材（仅用户指定的三个 MP4 与折叠头像；不加载对应 WebM）
        private const string IdleClipPath = "Assets/DigitalHuman/A-01 待机动画/待机动画.mp4";
        private const string SpeakingClipPath = "Assets/DigitalHuman/A-02讲解动画/讲解动画2.mp4";
        private const string ThinkingClipPath = "Assets/DigitalHuman/A-03 思考动画/思考动画.mp4";
        private const string AvatarSpritePath = "Assets/DigitalHuman/A-05 折叠态头像.PNG";
        private const string LumaKeyMatPath = "Assets/Shaders/UI-LumaKey.mat";
        // 常驻数字人专用材质：同 shader，收窄 KeySmooth 让缩小后的白描边更锐利（人物暗部 sRGB ≥8/255 仍远高于羽化上界，不误抠）
        private const string ResidentLumaKeyMatPath = "Assets/Shaders/UI-LumaKey-DigitalHuman.mat";
        private const float ResidentKeyThreshold = 0.02f;
        private const float ResidentKeySmooth = 0.006f;

        [MenuItem("Tools/M1/Setup AI 提问面板 %#&q")]
        public static void SetupQAPanel()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            SetupCore();
            Debug.Log("[M1QASetup] Setup AI 提问面板完成，场景：" + scene.name);
        }

        /// <summary>批处理入口（项目未被编辑器占用时供无人值守执行）。</summary>
        public static void SetupQAPanelBatch()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            SetupCore();
            Debug.Log("[M1QASetup] Batch 完成，场景：" + ScenePath);
        }

        private static void SetupCore()
        {
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

            // 1) 全屏挡板（先创建，渲染在下层；舞台会置于其后）
            var blockerGo = EnsureBlocker(board);

            // 2) ChatArea（右侧预留 344px），QAPanel 重挂其下
            var chatAreaGo = EnsureChatArea(board);
            var panelGo = FindIncludingInactive(board.transform, PanelName)?.gameObject;
            if (panelGo == null) panelGo = CreatePanel(chatAreaGo, cnFont);
            else
            {
                panelGo.transform.SetParent(chatAreaGo.transform, false);
                RefreshPanelLayout(panelGo.transform);
            }

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
            comp.bindPressTarget = false; // 输入统一由数字人 Presenter 处理（两个显示形态）
            comp.hiddenOffsetX = HiddenOffsetX;
            comp.cnFont = cnFont;

            // 4) DeepSeek 客户端（apiKey 由用户手填，Setup 不写值，重复执行保留）
            var client = board.GetComponent<M1DeepSeekClient>();
            if (client == null) client = board.AddComponent<M1DeepSeekClient>();
            comp.deepSeekClient = client;

            // 5) 数字人舞台（根级，置于最后）+ 全身/头像视图
            var stageGo = EnsureDigitalHumanStage(board);
            var fullBodyGo = EnsureFullBodyView(stageGo);
            var avatarGo = EnsureAvatarView(stageGo);

            // 6) 数字人 Presenter 注入（素材引用仅当字段为空时赋值，不覆盖用户替换）
            var presenter = stageGo.GetComponent<M1DigitalHumanPresenter>();
            if (presenter == null) presenter = stageGo.AddComponent<M1DigitalHumanPresenter>();
            presenter.qaPanel = comp;
            presenter.player = fullBodyGo.GetComponent<VideoPlayer>();
            presenter.rawImage = fullBodyGo.GetComponent<RawImage>();
            presenter.fullBodyView = fullBodyGo;
            presenter.avatarView = avatarGo;
            presenter.fullBodyPress = fullBodyGo.GetComponent<M1PressDetector>();
            presenter.avatarPress = avatarGo.GetComponent<M1PressDetector>();
            if (presenter.idleClip == null) presenter.idleClip = LoadClip(IdleClipPath, "待机动画");
            if (presenter.thinkingClip == null) presenter.thinkingClip = LoadClip(ThinkingClipPath, "思考动画");
            if (presenter.speakingClip == null) presenter.speakingClip = LoadClip(SpeakingClipPath, "讲解动画2");

            // 7) 隐藏旧静态形象（背景圆/大头），保留 对话框 及其路径（M1ToolSelection.aiAnswerPath 依赖）
            var oldFace = FindIncludingInactive(board.transform, OldFaceName)?.gameObject;
            if (oldFace != null && oldFace.activeSelf) oldFace.SetActive(false);

            // 8) 舞台置于最后：盖过 Blocker/QAPanel，数字人不被压暗或拦截
            stageGo.transform.SetAsLastSibling();

            EditorUtility.SetDirty(client);
            EditorUtility.SetDirty(comp);
            EditorUtility.SetDirty(presenter);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            var saved = EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            Debug.Log($"[M1QASetup] 完成：面板={panelGo.name} 挡板={blockerGo.name} 舞台={stageGo.name} " +
                      $"挂载 {comp.GetType().Name}+{presenter.GetType().Name} 场景保存={saved}");
        }

        // ==================== 结构 Ensure（幂等，重复执行自愈布局） ====================

        private static GameObject EnsureBlocker(GameObject board)
        {
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
            return blockerGo;
        }

        private static GameObject EnsureChatArea(GameObject board)
        {
            var chatArea = FindIncludingInactive(board.transform, ChatAreaName)?.gameObject;
            if (chatArea == null)
            {
                chatArea = new GameObject(ChatAreaName, typeof(RectTransform));
                chatArea.transform.SetParent(board.transform, false);
            }
            var rt = chatArea.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = new Vector2(-ChatAreaRightOffset, 0f); // 右侧为数字人舞台预留
            return chatArea;
        }

        private static GameObject EnsureDigitalHumanStage(GameObject board)
        {
            var stage = FindIncludingInactive(board.transform, StageName)?.gameObject;
            if (stage == null)
            {
                stage = new GameObject(StageName, typeof(RectTransform));
                stage.transform.SetParent(board.transform, false);
            }
            var srt = stage.GetComponent<RectTransform>();
            srt.anchorMin = new Vector2(1f, 0f);
            srt.anchorMax = new Vector2(1f, 1f);
            srt.pivot = new Vector2(1f, 0.5f);
            srt.anchoredPosition = new Vector2(-StageMargin, 0f);
            srt.sizeDelta = new Vector2(StageWidth, 0f);
            return stage;
        }

        private static GameObject EnsureFullBodyView(GameObject stage)
        {
            var fb = FindIncludingInactive(stage.transform, FullBodyName)?.gameObject;
            if (fb == null)
            {
                // 单一 Graphic（RawImage）自承接点击：同一物体双 Graphic 共享单 CanvasRenderer 会互相覆盖 mesh
                fb = new GameObject(FullBodyName,
                    typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage),
                    typeof(AspectRatioFitter), typeof(VideoPlayer), typeof(M1PressDetector));
                fb.transform.SetParent(stage.transform, false);
            }
            else
            {
                // 自愈：移除旧版遗留的透明点击 Image（与 RawImage 共享 CanvasRenderer，渲染不可靠）
                var extraImg = fb.GetComponent<Image>();
                if (extraImg != null) Object.DestroyImmediate(extraImg);
            }
            var frt = fb.GetComponent<RectTransform>();
            frt.anchorMin = new Vector2(0.5f, 0.5f);
            frt.anchorMax = new Vector2(0.5f, 0.5f);
            frt.pivot = new Vector2(0.5f, 0.5f);
            frt.anchoredPosition = new Vector2(0f, StageCenterOffsetY); // 与 AvatarView 同中心（R13）
            frt.sizeDelta = new Vector2(StageWidth, 0f); // 高度由 AspectRatioFitter 按比例推算

            var fitter = fb.GetComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.WidthControlsHeight;
            fitter.aspectRatio = StageAspect;

            var raw = fb.GetComponent<RawImage>();
            raw.raycastTarget = true; // 由 RawImage 自身承接按压检测（整矩形区域，含 LumaKey 透明区）
            // 常驻数字人专用材质（同 shader 收窄羽化）；自愈：缺失/shader 不符/仍指向开场引导材质时修复，不覆盖用户其他同 shader 调参实例
            var introMat = AssetDatabase.LoadAssetAtPath<Material>(LumaKeyMatPath);
            var residentMat = EnsureResidentLumaKeyMaterial();
            if (residentMat == null) Debug.LogWarning("[M1QASetup] 未找到/创建常驻 LumaKey 材质：" + ResidentLumaKeyMatPath);
            else if (raw.material == null || raw.material.shader != residentMat.shader || raw.material == introMat)
                raw.material = residentMat;

            var vp = fb.GetComponent<VideoPlayer>();
            vp.playOnAwake = false;
            vp.isLooping = true;
            vp.audioOutputMode = VideoAudioOutputMode.None; // 强制静音，不依赖导入配置
            vp.skipOnDrop = true;
            var idle = LoadClip(IdleClipPath, "待机动画");
            if (idle != null && vp.clip == null) vp.clip = idle;

            return fb;
        }

        private static GameObject EnsureAvatarView(GameObject stage)
        {
            var av = FindIncludingInactive(stage.transform, AvatarName)?.gameObject;
            if (av == null)
            {
                av = new GameObject(AvatarName,
                    typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(M1PressDetector));
                av.transform.SetParent(stage.transform, false);
            }
            var art = av.GetComponent<RectTransform>();
            art.anchorMin = new Vector2(0.5f, 0.5f);
            art.anchorMax = new Vector2(0.5f, 0.5f);
            art.pivot = new Vector2(0.5f, 0.5f);
            art.anchoredPosition = new Vector2(0f, StageCenterOffsetY); // 与 FullBodyView 同视觉中心（R13），不置右上角
            art.sizeDelta = new Vector2(AvatarSize, AvatarSize);

            var img = av.GetComponent<Image>();
            var spr = AssetDatabase.LoadAssetAtPath<Sprite>(AvatarSpritePath);
            if (spr == null) Debug.LogWarning("[M1QASetup] 未找到折叠头像：" + AvatarSpritePath);
            else if (img.sprite == null) img.sprite = spr;
            img.raycastTarget = true;
            av.SetActive(false); // 默认全身态，头像由 Presenter 运行时切换

            return av;
        }

        // ==================== 面板创建 ====================

        private static GameObject CreatePanel(GameObject chatArea, TMP_FontAsset cnFont)
        {
            // ---------- 抽屉根（锚定 ChatArea 右侧） ----------
            var panelGo = new GameObject(PanelName,
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            panelGo.transform.SetParent(chatArea.transform, false);
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
            trt.sizeDelta = new Vector2(400f, 60f);

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
            vrt.offsetMax = new Vector2(-ScrollbarReserve, 0f); // 右侧为滚动条让位

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
            vlg.spacing = MessageSpacing;
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
            EnsureScrollbar(listGo.transform, scroll);

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
            irt.sizeDelta = new Vector2(InputFieldWidth, 78f);
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
            vrt2.anchoredPosition = new Vector2(388f, 4f);
            vrt2.sizeDelta = new Vector2(VoiceWidth, 78f);

            // 发送按钮
            var sendGo = CreateButton("SendButton", rowGo.transform, "发送", cnFont, 32,
                new Color(0.15f, 0.42f, 0.82f, 1f));
            var srt = sendGo.GetComponent<RectTransform>();
            srt.anchorMin = new Vector2(0f, 0.5f);
            srt.anchorMax = new Vector2(0f, 0.5f);
            srt.pivot = new Vector2(0f, 0.5f);
            srt.anchoredPosition = new Vector2(472f, 4f);
            srt.sizeDelta = new Vector2(SendWidth, 78f);

            // 字数计数
            var counterGo = CreateText("CounterText", rowGo.transform, "0/200", cnFont, 22,
                new Color(0.55f, 0.55f, 0.55f, 1f));
            var ctrt = counterGo.GetComponent<RectTransform>();
            ctrt.anchorMin = new Vector2(0f, 1f);
            ctrt.anchorMax = new Vector2(0f, 1f);
            ctrt.pivot = new Vector2(0f, 1f);
            ctrt.anchoredPosition = new Vector2(18f, -4f);
            ctrt.sizeDelta = new Vector2(120f, 24f);

            panelGo.SetActive(false); // 初始隐藏（与 Blocker 一致）：运行时由长按数字人打开
            return panelGo;
        }

        /// <summary>自愈既有面板布局（面板宽度、Header/MessageList/InputRow、输入行排布），保证 Setup 重入不漂移。</summary>
        private static void RefreshPanelLayout(Transform panelGo)
        {
            var prt = panelGo.GetComponent<RectTransform>();
            if (prt != null) prt.sizeDelta = new Vector2(PanelWidth, prt.sizeDelta.y);

            var header = panelGo.Find("Header");
            if (header != null)
                header.GetComponent<RectTransform>().sizeDelta =
                    new Vector2(header.GetComponent<RectTransform>().sizeDelta.x, HeaderHeight);

            var list = panelGo.Find("MessageList");
            if (list != null)
            {
                var lrt = list.GetComponent<RectTransform>();
                lrt.offsetMin = new Vector2(lrt.offsetMin.x, 0f);
                lrt.offsetMax = new Vector2(lrt.offsetMax.x, -(HeaderHeight + InputRowHeight));
                var viewport = list.Find("Viewport");
                if (viewport != null) viewport.GetComponent<RectTransform>().offsetMax = new Vector2(-ScrollbarReserve, 0f);
                var vlg = viewport != null ? viewport.Find("Content")?.GetComponent<VerticalLayoutGroup>() : null;
                if (vlg != null) vlg.spacing = MessageSpacing;
                EnsureScrollbar(list, list.GetComponent<ScrollRect>());
            }

            var row = panelGo.Find("InputRow");
            if (row != null)
            {
                row.GetComponent<RectTransform>().sizeDelta =
                    new Vector2(row.GetComponent<RectTransform>().sizeDelta.x, InputRowHeight);
                var input = row.Find("InputField")?.GetComponent<RectTransform>();
                if (input != null) { input.anchoredPosition = new Vector2(18f, 4f); input.sizeDelta = new Vector2(InputFieldWidth, 78f); }
                var voice = row.Find("VoiceButton")?.GetComponent<RectTransform>();
                if (voice != null) { voice.anchoredPosition = new Vector2(388f, 4f); voice.sizeDelta = new Vector2(VoiceWidth, 78f); }
                var send = row.Find("SendButton")?.GetComponent<RectTransform>();
                if (send != null) { send.anchoredPosition = new Vector2(472f, 4f); send.sizeDelta = new Vector2(SendWidth, 78f); }
            }

            var counter = panelGo.Find("InputRow/CounterText")?.GetComponent<RectTransform>();
            if (counter != null)
            {
                counter.anchorMin = new Vector2(0f, 1f);
                counter.anchorMax = new Vector2(0f, 1f);
                counter.pivot = new Vector2(0f, 1f);
                counter.anchoredPosition = new Vector2(18f, -4f);
                counter.sizeDelta = new Vector2(120f, 24f);
            }
        }

        // ==================== 通用工具 ====================

        /// <summary>
        /// 常驻数字人专用 LumaKey 材质（同 shader，收窄 KeySmooth 让缩小后白描边更锐利）。
        /// 幂等：存在则加载（保留用户调参）；不存在则从共享 intro 材质迁移创建并独立保存。不影响开场引导 UI-LumaKey.mat。
        /// </summary>
        private static Material EnsureResidentLumaKeyMaterial()
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(ResidentLumaKeyMatPath);
            if (mat != null) return mat; // 已存在：保留用户调参
            var introMat = AssetDatabase.LoadAssetAtPath<Material>(LumaKeyMatPath);
            if (introMat == null)
            {
                Debug.LogWarning("[M1QASetup] 未找到开场引导 LumaKey 材质：" + LumaKeyMatPath + "，无法创建常驻数字人材质。");
                return null;
            }
            // 从共享 intro 材质迁移创建（继承 Color/stencil 等全部属性，仅收窄羽化），独立保存，不影响开场引导
            mat = new Material(introMat) { name = "UI-LumaKey-DigitalHuman" };
            mat.SetFloat("_KeyThreshold", ResidentKeyThreshold);
            mat.SetFloat("_KeySmooth", ResidentKeySmooth);
            AssetDatabase.CreateAsset(mat, ResidentLumaKeyMatPath);
            Debug.Log("[M1QASetup] 已创建常驻数字人 LumaKey 材质：" + ResidentLumaKeyMatPath);
            return mat;
        }

        private static VideoClip LoadClip(string path, string label)
        {
            var clip = AssetDatabase.LoadAssetAtPath<VideoClip>(path);
            if (clip == null) Debug.LogWarning("[M1QASetup] 未找到视频：" + label + "：" + path);
            return clip;
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

        /// <summary>消息列表滚动条（AutoHide：内容不足自动隐藏，超出自动出现）。幂等：已存在则复用。</summary>
        private static void EnsureScrollbar(Transform listGo, ScrollRect scroll)
        {
            var sbGo = FindIncludingInactive(listGo, "Scrollbar")?.gameObject;
            if (sbGo == null)
            {
                sbGo = new GameObject("Scrollbar",
                    typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Scrollbar));
                sbGo.transform.SetParent(listGo, false);
                var srt = sbGo.GetComponent<RectTransform>();
                srt.anchorMin = new Vector2(1f, 0f);
                srt.anchorMax = new Vector2(1f, 1f);
                srt.pivot = new Vector2(1f, 0.5f);
                srt.anchoredPosition = new Vector2(-2f, 0f);
                srt.sizeDelta = new Vector2(ScrollbarWidth, 0f);
                var sImg = sbGo.GetComponent<Image>();
                sImg.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
                sImg.type = Image.Type.Sliced;
                sImg.color = new Color(0.55f, 0.55f, 0.55f, 0.35f);

                var handleGo = new GameObject("Handle",
                    typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                handleGo.transform.SetParent(sbGo.transform, false);
                var hrt = handleGo.GetComponent<RectTransform>();
                hrt.anchorMin = Vector2.zero;
                hrt.anchorMax = Vector2.one;
                hrt.offsetMin = new Vector2(1f, 1f);
                hrt.offsetMax = new Vector2(-1f, -1f);
                var hImg = handleGo.GetComponent<Image>();
                hImg.sprite = sImg.sprite;
                hImg.type = Image.Type.Sliced;
                hImg.color = new Color(0.45f, 0.45f, 0.45f, 0.9f);

                var sb = sbGo.GetComponent<Scrollbar>();
                sb.direction = Scrollbar.Direction.BottomToTop;
                sb.handleRect = hrt;
                sb.targetGraphic = hImg;
            }

            if (scroll != null && scroll.verticalScrollbar == null)
                scroll.verticalScrollbar = sbGo.GetComponent<Scrollbar>();
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
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
