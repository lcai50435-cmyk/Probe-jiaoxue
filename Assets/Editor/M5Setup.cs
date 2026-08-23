using System.IO;
using System.Linq;
using M2;
using M5;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace M5.EditorTools
{
    /// <summary>
    /// M5 擦拭耦合剂场景搭建（M5 未冻结模块，Setup 可自由生成/重建）。
    /// 参照 M2 UGUI 骨架与视觉令牌（ugui-module-template.md）：SafeArea/HeaderBar/MainScene(RailArea)/ControlDock_D/QALayer/DigitalHumanStage/ModalLayer。
    /// 复用 M2 素材（俯视角钢轨）与耦合剂视觉（M5CouplantFx）；新增擦拭布工具（rag.png）。
    /// 幂等：已存在节点不重复创建，连跑两次 Scene 哈希一致。
    /// </summary>
    public static class M5Setup
    {
        private const string ScenePath = "Assets/Settings/Scenes/M5.unity";
        private const string FontAssetPath = "Assets/font/sarasa-gothic-sc-regular/sarasa-gothic-sc-regular_cn.asset";
        private const string CorrectClipPath = "Assets/Audio/E-01 正确提示音/正确音2.mp3";
        private const string RailNormalPath = "Assets/Resources/俯视角.png";
        private const string RailPerspectivePath = "Assets/Resources/俯视角透视.png";
        private const string RagPath = "Assets/probeFootage/rag.png";
        private const string ProbePath = "Assets/probeFootage/probeFootage.png";
        private const string RulerPath = "Assets/Ruler/尺子正面.png";

        // M2 冻结视觉令牌（ugui-module-template.md §4）
        private static readonly Color PageColor = new Color(.925f, .935f, .945f);
        private static readonly Color SurfaceColor = new Color(.975f, .98f, .985f);
        private static readonly Color InkColor = new Color(.12f, .15f, .18f);
        private static readonly Color MutedColor = new Color(.38f, .42f, .46f);
        private static readonly Color PrimaryColor = new Color(.08f, .42f, .66f);
        private static readonly Color AccentColor = new Color(.93f, .55f, .12f);
        private static readonly Color RagLockedColor = new Color(.45f, .47f, .5f, .9f); // 浅色 rag 置灰需加深+高不透明（0.62 会融入浅背景像透明）
        private static readonly Color M2LockedColor = new Color(.55f, .57f, .6f, .62f); // M2 同款置灰（探头/尺子有深色细节，可见）

        public static void SetupM5Batch()
        {
            SetupM5();
            Debug.Log("[M5Setup] Batch 完成：" + ScenePath);
        }

        [MenuItem("Tools/M5/Setup M5 场景 %#&5")]
        public static void SetupM5()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[M5Setup] 请先退出 Play 模式再运行 Setup。");
                return;
            }
            var cnFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            if (cnFont == null)
            {
                Debug.LogError("[M5Setup] 未找到中文字体资产：" + FontAssetPath + "，请先运行 Tools/字体/重新生成中文字体资产。");
                return;
            }
            var existed = File.Exists(ScenePath);
            var scene = existed
                ? EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single)
                : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var canvas = EnsureCanvas(cnFont);
            AdaptFromM2Baseline(canvas);          // M2 复制基线：清理 M2 探测节点/组件并修复结构（幂等）
            EnsureSafeArea(canvas, cnFont);       // 幂等补齐 M5 层级（M2 复制版 Canvas 已存在时 EnsureCanvas 不建结构）
            EnsureAll(canvas, cnFont);
            EditorSceneManager.MarkSceneDirty(scene);
            var saved = EditorSceneManager.SaveScene(scene, existed ? null : ScenePath);
            Debug.Log($"[M5Setup] 场景{(existed ? "重建" : "新建")}完成：{ScenePath}（保存={saved}）");
        }

        private static Transform EnsureCanvas(TMP_FontAsset font)
        {
            var canvasGo = GameObject.Find("Canvas");
            if (canvasGo != null) return canvasGo.transform;
            canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = .5f;
            var flow = canvasGo.AddComponent<M5FlowController>();
            flow.sfx = EnsureSafeArea(canvasGo.transform, font).GetComponent<AudioSource>();
            EnsureEventSystem(canvasGo.transform);
            return canvasGo.transform;
        }

        private static void EnsureEventSystem(Transform root)
        {
            var es = UnityEngine.EventSystems.EventSystem.current;
            if (es != null) return;
            if (GameObject.Find("EventSystem") != null) return;
            // 项目 Active Input Handling = Input System Package：必须用 InputSystemUIInputModule（旧 StandaloneInputModule 每帧抛 InvalidOperationException）
            var go = new GameObject("EventSystem",
                typeof(UnityEngine.EventSystems.EventSystem),
                typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
            go.transform.SetParent(root, false);
        }

        private static Transform EnsureSafeArea(Transform canvas, TMP_FontAsset font)
        {
            var sa = EnsureGo(canvas, "SafeArea");
            SetRect(sa, new Vector2(0, 0), new Vector2(1, 1), Vector2.zero, Vector2.zero, new Vector2(.5f, .5f));
            if (sa.GetComponent<AudioSource>() == null) sa.gameObject.AddComponent<AudioSource>();

            // 层级顺序：Background < HeaderBar < MainScene < ControlDock_D < CompletionPanel < QALayer < DigitalHumanStage < ModalLayer
            var bg = EnsureImage(sa, "Background", PageColor); Stretch(bg);
            var header = EnsureImage(sa, "HeaderBar", SurfaceColor);
            SetRect(header, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -64), new Vector2(-48, 80), new Vector2(.5f, .5f));
            EnsureHeader(header, font);
            var main = EnsureImage(sa, "MainScene", SurfaceColor);
            SetRect(main, new Vector2(0, 0), new Vector2(1, 1), new Vector2(0, 48), new Vector2(-48, -336), new Vector2(.5f, .5f));
            EnsureRailArea(main, font);
            var dock = EnsureImage(sa, "ControlDock_D", SurfaceColor);
            SetRect(dock, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 112), new Vector2(-48, 176), new Vector2(.5f, .5f));
            EnsureDock(dock, font);
            var completion = EnsureGo(sa, "CompletionPanel");
            Stretch(completion); completion.gameObject.SetActive(false);
            EnsureCompletion(completion, font);
            var qaLayer = EnsureGo(sa, "QALayer"); Stretch(qaLayer);
            EnsureQa(qaLayer, font);
            var stage = EnsureImage(sa, "DigitalHumanStage", new Color(0, 0, 0, .02f));
            SetRect(stage, new Vector2(1, 0), new Vector2(1, 1), new Vector2(-184, 257), new Vector2(320, -584), new Vector2(.5f, .5f));
            EnsureStage(stage, font);
            var modal = EnsureGo(sa, "ModalLayer"); Stretch(modal); modal.gameObject.SetActive(false);
            EnsureModal(modal, font);
            // 顺序自愈：QALayer 在 DigitalHumanStage 之前、ModalLayer 最后
            qaLayer.SetSiblingIndex(completion.GetSiblingIndex() + 1);
            stage.SetAsLastSibling();
            modal.SetAsLastSibling();
            return sa;
        }

        private static void EnsureHeader(Transform header, TMP_FontAsset font)
        {
            var title = EnsureTmp(header, "ModuleTitle", "M5 擦拭耦合剂", 36, PrimaryColor);
            SetRect(title, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(24, 0), new Vector2(600, 64), new Vector2(0, .5f));
            title.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Left;
            var reset = EnsureButton(header, "ResetButton", "重置流程", 26, SurfaceColor, InkColor);
            SetRect(reset, new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(-20, 0), new Vector2(168, 64), new Vector2(1, .5f));
        }

        private static void EnsureRailArea(Transform main, TMP_FontAsset font)
        {
            var railArea = EnsureGo(main, "RailArea"); Stretch(railArea);
            var shelf = EnsureImage(railArea, "ToolShelf", new Color(0, 0, 0, .03f));
            SetRect(shelf, new Vector2(0, 1), new Vector2(0, 1), new Vector2(202, -52), new Vector2(570, 88), new Vector2(.5f, .5f)); // 三槽位加宽（M2 两槽 372）
            shelf.GetComponent<Image>().raycastTarget = false; // 工具架层不拦截拖拽
            EnsureToolShelf(shelf);
            var viewport = EnsureGo(railArea, "RailViewport");
            SetRect(viewport, new Vector2(0, 0), new Vector2(1, 1), new Vector2(0, 0), new Vector2(-32, -32), new Vector2(.5f, .5f));
            EnsureViewport(viewport, font);
        }

        private static void EnsureToolShelf(Transform shelf)
        {
            // 探头（静态展示，与 M2 同款槽位，不可交互）
            var probeHome = EnsureGo(shelf, "ProbeHome");
            SetRect(probeHome, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(88, 0), new Vector2(176, 88), new Vector2(.5f, .5f));
            var probe = EnsureImage(probeHome, "Probe", M2LockedColor);
            SetRect(probe, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(166, 117), new Vector2(.5f, .5f));
            probe.GetComponent<Image>().sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ProbePath);
            probe.GetComponent<Image>().preserveAspect = true;
            probe.GetComponent<Image>().raycastTarget = false;
            // 尺子（静态展示，与 M2 同款槽位，不可交互）
            var rulerHome = EnsureGo(shelf, "RulerHome");
            SetRect(rulerHome, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(282, 0), new Vector2(176, 88), new Vector2(.5f, .5f));
            var ruler = EnsureImage(rulerHome, "Ruler", M2LockedColor);
            SetRect(ruler, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0, 10), new Vector2(150, 32), new Vector2(.5f, .5f));
            ruler.GetComponent<Image>().sprite = AssetDatabase.LoadAssetAtPath<Sprite>(RulerPath);
            ruler.GetComponent<Image>().preserveAspect = true;
            ruler.GetComponent<Image>().raycastTarget = false;
            // 擦拭布（可拖）：RagHome 紧邻 RulerHome 右侧
            var home = EnsureGo(shelf, "RagHome");
            SetRect(home, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(476, 0), new Vector2(176, 88), new Vector2(.5f, .5f));
            var rag = EnsureImage(home, "Rag", RagLockedColor);
            SetRect(rag, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(132, 132), new Vector2(.5f, .5f));
            rag.GetComponent<Image>().sprite = AssetDatabase.LoadAssetAtPath<Sprite>(RagPath);
            if (rag.GetComponent<Image>().sprite != null) rag.GetComponent<Image>().preserveAspect = true;
            rag.GetComponent<Image>().raycastTarget = true;
            var outline = rag.GetComponent<Outline>();
            if (outline == null) outline = rag.gameObject.AddComponent<Outline>(); // 浅色 rag 与浅背景分离（伪 null 不能走 ??）
            outline.effectColor = new Color(.2f, .22f, .25f, .6f);
            outline.effectDistance = new Vector2(2, -2);
            rag.SetAsLastSibling();
            var drag = rag.GetComponent<M5RagDrag>();
            if (drag == null) drag = rag.gameObject.AddComponent<M5RagDrag>(); // 伪 null 不能走 ??（Unity 6 对象语义）
            drag.ragRt = rag as RectTransform; drag.ragImage = rag.GetComponent<Image>();
        }

        private static void EnsureViewport(Transform viewport, TMP_FontAsset font)
        {
            var railBg = EnsureImage(viewport, "RailBackground", Color.white);
            // 钢轨布局 Scene 权威：老板手工调整的位置/尺寸不被 Setup 重置（仅新建/空布局时设置默认）
            if (railBg.sizeDelta.sqrMagnitude < 1f)
                SetRect(railBg, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(-24, -32.979f), new Vector2(960.523f, 285.958f), new Vector2(.5f, .5f));
            SetRailSprite(railBg, RailNormalPath);
            railBg.SetAsFirstSibling();

            var overlay = EnsureGo(viewport, "CouplantOverlay"); Stretch(overlay);
            var cg = overlay.GetComponent<CanvasGroup>();
            if (cg == null) cg = overlay.gameObject.AddComponent<CanvasGroup>(); // 伪 null 不能走 ??（Unity 6 对象语义）
            cg.blocksRaycasts = false; // 耦合剂层不拦截拖拽
            if (overlay.GetComponent<Image>() != null)
            {
                // M2 复制基线：CouplantOverlay 自身即薄膜（Image Filled + CanvasGroup），直接配置
                ConfigureFilm(overlay.GetComponent<Image>());
            }
            else
            {
                // 从零生成：overlay 容器 + CouplantMask 薄膜子节点
                var maskImg = EnsureImage(overlay, "CouplantMask", new Color(.55f, .8f, .96f, .45f));
                SetRect(maskImg, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero,
                    new Vector2(.993f * 960.523f, .553f * 285.958f), new Vector2(.5f, .5f)); // 与 M5CouplantFx coverRect 一致（轨顶中央大部分）
                ConfigureFilm(maskImg.GetComponent<Image>());
            }

            var perspective = EnsureImage(viewport, "RailPerspective", Color.white);
            Stretch(perspective); perspective.gameObject.SetActive(false);
            SetRailSprite(perspective, RailPerspectivePath);

            var bar = EnsureImage(viewport, "PerspectiveBar_C", SurfaceColor);
            SetRect(bar, new Vector2(0, 0), new Vector2(0, 0), new Vector2(200, 52), new Vector2(364, 64), new Vector2(.5f, .5f));
            var normal = EnsureButtonDeep(bar, "NormalButton", "普通视图", 24, PrimaryColor, Color.white);
            SetRect(normal, new Vector2(0, 0), new Vector2(.5f, 1), new Vector2(0, 0), new Vector2(0, 0), new Vector2(.5f, .5f));
            var persp = EnsureButtonDeep(bar, "PerspectiveButton", "透视视图", 24, new Color(.58f, .61f, .65f), InkColor);
            SetRect(persp, new Vector2(.5f, 0), new Vector2(1, 1), new Vector2(0, 0), new Vector2(0, 0), new Vector2(.5f, .5f));
        }

        private static void EnsureDock(Transform dock, TMP_FontAsset font)
        {
            var inst = EnsureGo(dock, "InstructionArea");
            SetRect(inst, new Vector2(0, 0), new Vector2(.3f, 1), new Vector2(8, 0), new Vector2(-32, 0), new Vector2(.5f, .5f));
            var it = EnsureTmp(inst, "InstructionText", "请将擦拭布拖至钢轨顶面，由左至右擦拭", 26, InkColor);
            Stretch(it); it.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.MidlineLeft;
            var step = EnsureGo(dock, "StepProgress");
            SetRect(step, new Vector2(.78f, 0), new Vector2(1, 1), new Vector2(-8, 0), new Vector2(-32, 0), new Vector2(.5f, .5f));
            var st = EnsureTmp(step, "StepProgressText", "步骤 1/1 · 擦拭耦合剂", 24, MutedColor);
            Stretch(st); st.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.MidlineRight;
        }

        private static void EnsureCompletion(Transform panel, TMP_FontAsset font)
        {
            var text = EnsureTmp(panel, "CompletionText", "M5 擦拭耦合剂完成", 42, PrimaryColor);
            Stretch(text); text.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Center;
        }

        private static void EnsureQa(Transform qaLayer, TMP_FontAsset font)
        {
            if (FindDeep(qaLayer, "QAPanel") != null) return; // M2 复制基线：QA 面板已装配（M1QAPanel 链路），跳过占位创建
            var blocker = EnsureImage(qaLayer, "Blocker", new Color(0, 0, 0, .3f));
            Stretch(blocker);
            if (blocker.GetComponent<Button>() == null)
            {
                var btn = blocker.gameObject.AddComponent<Button>();
                btn.targetGraphic = blocker.GetComponent<Image>();
            }
            var panel = EnsureGo(qaLayer, "QAPanel");
            SetRect(panel, new Vector2(1, 0), new Vector2(1, 1), Vector2.zero, new Vector2(580, 0), new Vector2(1, .5f));
            var placeholder = EnsureTmp(panel, "Placeholder", "（问答面板由运行时装配）", 22, MutedColor);
            Stretch(placeholder); placeholder.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Center;
        }

        private static void EnsureStage(Transform stage, TMP_FontAsset font)
        {
            if (FindDeep(stage, "FullBodyView") != null || FindDeep(stage, "FullBodyPreview") != null) return; // M2 复制基线：数字人舞台已存在
            var preview = EnsureImage(stage, "FullBodyPreview", new Color(.9f, .92f, .95f));
            Stretch(preview); preview.gameObject.SetActive(false);
        }

        private static void EnsureModal(Transform modal, TMP_FontAsset font)
        {
            if (FindDeep(modal, "ResetConfirmDialog") != null || FindDeep(modal, "Dialog") != null) return; // M2 复制基线：重置确认弹窗已存在
            var dim = EnsureImage(modal, "DialogDim", new Color(0, 0, 0, .35f));
            Stretch(dim);
            var dialog = EnsureImage(modal, "Dialog", SurfaceColor);
            SetRect(dialog, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(480, 260), new Vector2(.5f, .5f));
            var text = EnsureTmp(dialog, "DialogText", "确定要重置流程吗？", 28, InkColor);
            SetRect(text, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -40), new Vector2(400, 60), new Vector2(.5f, 1));
            text.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Center;
            var confirm = EnsureButton(dialog, "ConfirmButton", "确认重置", 26, AccentColor, Color.white);
            SetRect(confirm, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(-110, 36), new Vector2(200, 64), new Vector2(.5f, 0));
            var cancel = EnsureButton(dialog, "CancelButton", "取消", 26, PrimaryColor, Color.white);
            SetRect(cancel, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(110, 36), new Vector2(200, 64), new Vector2(.5f, 0));
        }

        // ==================== M2 复制基线适配 ====================

        /// <summary>
        /// M2 复制基线适配（老板定稿：M5 = M2 UGUI 骨架 + 删除 M2 探测流程节点）。
        /// 幂等：M2 专属节点/组件已删除后第二次跑全部跳过。
        /// 1) 移除 M2 运行时组件（M2FlowController/M2ProbeDrag/M2RulerDrag/M2IdleHelp）与失效脚本引用；M2WaveformFx 保留（波形窗口静态视觉）
        /// 2) 删除 M2 探测专属节点：WeldLine/BeamLayer/MeasurementBubble/StepControlArea（波形窗口 SupportArea 保留，老板 2026-08-23 定稿）
        /// 3) 耦合剂层：删空壳 CouplantMask，CouplantOverlay（Image+CanvasGroup）提为 RailViewport 直接子节点作薄膜
        /// 4) ToolShelf：删除旧 RagHome（名字可能带引号/尾随空格），由 EnsureToolShelf 重建正规节点
        /// 5) 文案与命名：ModuleTitle 改 M5 标题；InstructionArea/Text、StepProgress/Text 改名供 Ensure 命中
        /// 6) PerspectiveBar_C 从 RailArea 提入 RailViewport
        /// </summary>
        private static void AdaptFromM2Baseline(Transform canvas)
        {
            var safeArea = canvas.Find("SafeArea");
            if (safeArea == null) return;
            var main = safeArea.Find("MainScene");
            var railArea = main != null ? main.Find("RailArea") : null;
            var viewport = railArea != null ? railArea.Find("RailViewport") : null;
            var dock = safeArea.Find("ControlDock_D");

            // 1) M2 运行时组件（会初始化 M2 探测流程，必须移除；幂等）。M2WaveformFx 保留——波形窗口视觉由它程序化绘制，
            //    独立于 M2 流程（初始即画深底/网格/始波/噪声线），M5 无外部 SetDistanceMm 驱动即静态呈现，与 M2 窗口一致且无实际作用
            foreach (var c in canvas.GetComponentsInChildren<M2FlowController>(true).ToArray()) UnityEngine.Object.DestroyImmediate(c);
            foreach (var c in canvas.GetComponentsInChildren<M2ProbeDrag>(true).ToArray()) UnityEngine.Object.DestroyImmediate(c);
            foreach (var c in canvas.GetComponentsInChildren<M2RulerDrag>(true).ToArray()) UnityEngine.Object.DestroyImmediate(c);
            foreach (var c in canvas.GetComponentsInChildren<M2IdleHelp>(true).ToArray()) UnityEngine.Object.DestroyImmediate(c);
            RemoveMissingScripts(canvas);

            // 2) 删除 M2 探测流程专属节点（幂等：不存在即跳过）
            if (viewport != null)
            {
                DestroyNamed(viewport, "WeldLine");
                DestroyNamed(viewport, "BeamLayer");
                DestroyNamed(viewport, "MeasurementBubble");
            }
            // SupportArea（M2 波形窗口）保留：老板 2026-08-23 要求 M5 保留波形窗口静态视觉（无实际作用）
            if (dock != null) DestroyNamed(dock, "StepControlArea");
            // 波形窗口补回 M2WaveformFx：早期 Setup 曾删除该组件且不会自愈，补回后程序化绘制与 M2 同款波形（深底/网格/始波/噪声线）
            if (main != null)
            {
                var support = main.Find("SupportArea");
                var waveGrid = support != null ? FindDeep(support, "WaveGrid") : null;
                if (waveGrid != null && waveGrid.GetComponent<M2WaveformFx>() == null)
                {
                    var fx = waveGrid.gameObject.AddComponent<M2WaveformFx>();
                    fx.appearMm = 150f; fx.peakMm = 115f; fx.stopMm = 110f; // M2 Scene 序列化参数（老板 2026-08-15 定稿），初始波形与 M2 一致
                }
            }

            // 3) 耦合剂层修复
            if (viewport != null)
            {
                // M2 railViewport 白底子节点：删除（M5 MainScene 已是 SurfaceColor 白底，避免盖住钢轨图）
                for (int i = viewport.childCount - 1; i >= 0; i--)
                    if (viewport.GetChild(i).name == "bg") UnityEngine.Object.DestroyImmediate(viewport.GetChild(i).gameObject);
                var overlay = FindDeep(viewport, "CouplantOverlay");
                var mask = FindDeep(viewport, "CouplantMask");
                if (overlay != null)
                {
                    if (overlay.parent != viewport) overlay.SetParent(viewport, false);
                    // 删空壳 CouplantMask（无 Image 的父壳；M2 薄膜即 CouplantOverlay 自身）
                    if (mask != null && mask != overlay && mask.GetComponent<Image>() == null)
                        UnityEngine.Object.DestroyImmediate(mask.gameObject);
                    // 清理 M2 涂抹耦合剂旧视觉子节点（M5 用自身切出的铁轨形状薄膜，避免叠加）
                    for (int i = overlay.childCount - 1; i >= 0; i--)
                        if (overlay.GetChild(i).name == "bg") UnityEngine.Object.DestroyImmediate(overlay.GetChild(i).gameObject);
                }
            }

            // 4) ToolShelf：重建正规 RagHome（旧节点名可能带引号/尾随空格，EnsureToolShelf 会按正规名重建）
            var shelf = railArea != null ? railArea.Find("ToolShelf") : null;
            if (shelf != null)
            {
                for (int i = shelf.childCount - 1; i >= 0; i--)
                {
                    var child = shelf.GetChild(i);
                    if (child.name.Trim().Trim('\'') == "RagHome") UnityEngine.Object.DestroyImmediate(child.gameObject);
                }
            }

            // 5) 文案与命名
            var header = safeArea.Find("HeaderBar");
            var title = header != null ? FindDeep(header, "ModuleTitle") : null;
            var titleTmp = title != null ? title.GetComponent<TMP_Text>() : null;
            if (titleTmp != null) titleTmp.text = "M5 擦拭耦合剂";
            var instArea = safeArea.Find("ControlDock_D/InstructionArea");
            var stepArea = safeArea.Find("ControlDock_D/StepProgress");
            RenameText(instArea, "InstructionText");
            RenameText(stepArea, "StepProgressText");
            SetTmpText(instArea != null ? instArea.Find("InstructionText") : null, "请将擦拭布拖至钢轨顶面，由左至右擦拭");
            SetTmpText(stepArea != null ? stepArea.Find("StepProgressText") : null, "步骤 1/1 · 擦拭耦合剂");

            // 6) PerspectiveBar_C 提入 viewport（M5 布局预期在 viewport 内）
            if (viewport != null && railArea != null)
            {
                var bar = railArea.Find("PerspectiveBar_C");
                if (bar != null && bar.parent != viewport) bar.SetParent(viewport, false);
            }
        }

        private static void DestroyNamed(Transform root, string name)
        {
            var t = FindDeep(root, name);
            if (t != null) UnityEngine.Object.DestroyImmediate(t.gameObject);
        }

        private static void RenameText(Transform container, string newName)
        {
            if (container == null || container.Find(newName) != null) return;
            var old = FindDeep(container, "Text");
            if (old != null) old.name = newName;
        }

        private static void SetTmpText(Transform t, string text)
        {
            var tmp = t != null ? t.GetComponent<TMP_Text>() : null;
            if (tmp != null) tmp.text = text;
        }

        /// <summary>清理失效脚本引用（Missing Script）：m_Component 数组中 objectReferenceValue 为 null 的项。</summary>
        private static void RemoveMissingScripts(Transform root)
        {
            var so = new SerializedObject(root.gameObject);
            var comps = so.FindProperty("m_Component");
            for (int i = comps.arraySize - 1; i >= 0; i--)
            {
                var el = comps.GetArrayElementAtIndex(i);
                if (el.propertyType == SerializedPropertyType.ObjectReference && el.objectReferenceValue == null)
                    comps.DeleteArrayElementAtIndex(i);
            }
            so.ApplyModifiedProperties();
            for (int i = 0; i < root.childCount; i++) RemoveMissingScripts(root.GetChild(i));
        }

        // ==================== 组件注入（幂等） ====================

        private static void EnsureAll(Transform canvas, TMP_FontAsset font)
        {
            var safeArea = canvas.Find("SafeArea");
            if (safeArea == null) return;
            var flow = canvas.GetComponent<M5FlowController>();
            if (flow == null) flow = canvas.gameObject.AddComponent<M5FlowController>();
            var couplant = canvas.GetComponent<M5CouplantFx>();
            if (couplant == null) couplant = canvas.gameObject.AddComponent<M5CouplantFx>();

            var viewport = safeArea.Find("MainScene/RailArea/RailViewport");
            var railBg = viewport != null ? viewport.Find("RailBackground") : null;
            var overlay = viewport != null ? viewport.Find("CouplantOverlay") : null;
            var mask = overlay != null ? (overlay.Find("CouplantMask") ?? overlay) : null; // M2 复制基线：薄膜即 CouplantOverlay 自身
            var perspective = viewport != null ? viewport.Find("RailPerspective") : null;
            var bar = viewport != null ? viewport.Find("PerspectiveBar_C") : null;
            var rag = safeArea.Find("MainScene/RailArea/ToolShelf/RagHome/Rag");
            var ragHome = safeArea.Find("MainScene/RailArea/ToolShelf/RagHome");

            if (rag != null)
            {
                var drag = rag.GetComponent<M5RagDrag>();
                if (drag != null) { drag.flow = flow; drag.railViewport = viewport as RectTransform; drag.railBg = railBg as RectTransform; drag.ragHome = ragHome as RectTransform; }
            }
            if (couplant != null && railBg != null && mask != null && overlay != null)
            {
                couplant.railBg = railBg as RectTransform;
                couplant.maskRt = mask as RectTransform;
                couplant.film = mask.GetComponent<Image>();
                couplant.group = overlay.GetComponent<CanvasGroup>();
            }
            if (flow != null)
            {
                flow.ragDrag = rag != null ? rag.GetComponent<M5RagDrag>() : null;
                flow.couplantFx = couplant;
                flow.railBg = railBg as RectTransform;
                flow.couplantOverlay = overlay as RectTransform;
                flow.railPerspective = perspective != null ? perspective.gameObject : null;
                flow.normalBtnImg = bar != null ? (FindDeep(bar, "NormalButton")?.GetComponent<Image>()) : null; // M2 复制基线：按钮在 ViewModeSegment 下
                flow.perspectiveBtnImg = bar != null ? (FindDeep(bar, "PerspectiveButton")?.GetComponent<Image>()) : null;
                flow.instructionText = safeArea.Find("ControlDock_D/InstructionArea")?.GetComponentInChildren<TextMeshProUGUI>(true); // M2 复制基线：文字节点名可能为 Text
                flow.stepProgressText = safeArea.Find("ControlDock_D/StepProgress")?.GetComponentInChildren<TextMeshProUGUI>(true);
                flow.completionPanel = safeArea.Find("CompletionPanel")?.gameObject;
                flow.completionText = safeArea.Find("CompletionPanel/CompletionText")?.GetComponent<TextMeshProUGUI>();
                flow.resetButton = safeArea.Find("HeaderBar/ResetButton")?.GetComponent<Button>();
                flow.sfx = safeArea.GetComponent<AudioSource>();
                if (flow.correctClip == null) flow.correctClip = LoadClip(CorrectClipPath);
                flow.stepPanels = new GameObject[0];
            }
            // 音效注入（幂等：仅当字段为空）
            var src = safeArea.GetComponent<AudioSource>();
            if (src != null) { src.playOnAwake = false; src.spatialBlend = 0f; }
        }

        // ==================== 辅助 ====================

        private static AudioClip LoadClip(string path)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip == null) Debug.LogWarning("[M5Setup] 未找到音效：" + path);
            return clip;
        }

        private static Transform EnsureGo(Transform parent, string name)
        {
            var hit = parent.Find(name);
            if (hit != null) return hit;
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        private static RectTransform EnsureImage(Transform parent, string name, Color color)
        {
            var hit = parent.Find(name);
            if (hit != null)
            {
                // M2 复制基线：已有节点可能缺 Image（如 ToolShelf 纯容器）——补组件，避免伪 null NRE；幂等覆盖颜色
                var img = hit.GetComponent<Image>();
                if (img == null) img = hit.gameObject.AddComponent<Image>();
                img.color = color;
                return hit as RectTransform;
            }
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = color;
            return go.GetComponent<RectTransform>();
        }

        private static RectTransform EnsureTmp(Transform parent, string name, string text, float size, Color color)
        {
            var hit = parent.Find(name);
            if (hit != null) return hit as RectTransform;
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<TextMeshProUGUI>();
            t.text = text; t.fontSize = size; t.color = color;
            t.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            return go.GetComponent<RectTransform>();
        }

        private static RectTransform EnsureButton(Transform parent, string name, string text, float size, Color bg, Color fg)
        {
            var hit = parent.Find(name);
            if (hit != null) return hit as RectTransform;
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>(); img.color = bg;
            var btn = go.GetComponent<Button>(); btn.targetGraphic = img;
            var tmp = EnsureTmp(go.transform, "Text", text, size, fg);
            Stretch(tmp); tmp.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Center;
            return go.GetComponent<RectTransform>();
        }

        private static void ConfigureFilm(Image img)
        {
            img.type = Image.Type.Filled;
            img.fillMethod = Image.FillMethod.Horizontal;
            img.fillOrigin = 1; // 右对齐剩余：已擦左侧消失、剩余显示在右侧，与拖动方向一致
            img.fillAmount = 1f;
            img.raycastTarget = false;
            img.color = new Color(.55f, .8f, .96f, .45f); // 浅蓝色半透明（与 M2 同款，老板 2026-08-15 调浅）
        }

        /// <summary>M2 复制基线：按钮可能在 ViewModeSegment 子容器下，FindDeep 命中后幂等覆盖文案/配色，不重复创建。</summary>
        private static RectTransform EnsureButtonDeep(Transform parent, string name, string text, float size, Color bg, Color fg)
        {
            var hit = FindDeep(parent, name);
            if (hit != null)
            {
                var tmp = hit.GetComponentInChildren<TMP_Text>(true);
                if (tmp != null) { tmp.text = text; tmp.fontSize = size; tmp.color = fg; }
                var img = hit.GetComponent<Image>();
                if (img != null) img.color = bg;
                return hit as RectTransform;
            }
            return EnsureButton(parent, name, text, size, bg, fg);
        }

        private static Transform FindDeep(Transform root, string name)
        {
            foreach (Transform child in root)
            {
                if (child.name == name) return child;
                var hit = FindDeep(child, name);
                if (hit != null) return hit;
            }
            return null;
        }

        /// <summary>M2 复制基线：钢轨图 Image 可能在容器子节点（RailBackground/bg），容器自身无 Image；伪 null 上设置会静默失败。</summary>
        private static void SetRailSprite(RectTransform container, string path)
        {
            var img = container.GetComponent<Image>();
            if (img == null) img = container.GetComponentInChildren<Image>(true);
            if (img == null) return;
            img.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            img.preserveAspect = true;
            img.raycastTarget = false;
        }

        private static void Stretch(Transform t)
        {
            var rt = t as RectTransform;
            if (rt == null) return;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }

        private static void SetRect(Transform t, Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 size, Vector2 pivot)
        {
            var rt = t as RectTransform;
            if (rt == null) return;
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
            rt.anchoredPosition = pos; rt.sizeDelta = size; rt.pivot = pivot;
        }
    }
}
