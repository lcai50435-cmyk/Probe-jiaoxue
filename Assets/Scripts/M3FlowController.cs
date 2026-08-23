using M1;
using M2;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace M3
{   /// <summary>M3 流程唯一状态所有者：定位 → 扫描 → 测距 → 完成（不再播放自动耦合剂 Intro）。
    /// 2026-08-16 按 PPT 对齐：扫描 160→120mm，波形复用 M2WaveformFx，目标以伤损为主。</summary>
    public class M3FlowController : MonoBehaviour
    {
        public enum Stage { Intro, Positioning, Scanning, Measuring, Completed } // Intro 保留兼容旧引用，运行时不再进入
        public M3ProbeDrag probeDrag;
        public M3RulerDrag rulerDrag;
        public M2WaveformFx waveformFx;
        public GameObject beamLayer, railPerspective, damageMarker, detectionBanner, completionPanel, measurementBubble;
        public RectTransform couplantOverlay, railBg;
        public Image normalBtnImg, perspectiveBtnImg;
        public TMP_Text instructionText, stepProgressText, completionText;
        public Button resetButton, enterNextButton;
        public AudioSource sfx;
        public AudioClip beepClip, correctClip;
        [Tooltip("音效播放音量（2026-08-18 老板要求整体调小）")]
        public float sfxVolume = 0.4f;
        public M3IdleHelp idleHelp;
        [System.NonSerialized] public ModuleSpeechBubble speechBubble; // 数字人台词气泡（运行时挂载）
        public float introDuration = 2f, targetAngle = 13f, targetDistance = 120f, peakTolerance = 1f;
        /// <summary>伤损波移动速度倍率：2 = 探头移动 1mm 伤损波在波形 X 轴移动 2mm（老板 2026-08-16 定稿，可调）。</summary>
        public float waveformSpeed = 2f;
        public string[] stepHints = { "放置探头并调整偏角至向下 13°", "向前移动探头（55→40mm）", "拖动尺子：0 刻度对齐探头入射点，120mm 对齐伤损" };
        public UnityEvent onCompleted;
        /// <summary>M3 通关 → M4（老板 2026-08-18；M3 冻结 Scene 未序列化，代码默认生效）。</summary>
        public string nextSceneName = "M4";
        public Stage CurrentStage { get; private set; } = Stage.Positioning;
        public bool Detected, Measured, PerspectiveOn, RulerDocked, AngleVerifiedByRuler;
        public float distanceToleranceMm = 2f;
        public bool PositioningRulerInPlace => rulerDrag != null && rulerDrag.positioned;
        private float _prevMm = 55f;
        private Sprite _damageMarkerSprite; // 伤损橙标记（椭圆）
        private static readonly string[] DefaultHints = {
            "将探头放在轨头侧面，利用多功能尺将探头向下偏转13°", // Slide 8-【3】
            "将探头以13度偏角向前移动，注意观察波形变化",       // Slide 9-【3】
            "将定位尺0刻度对准探头入射点，进行测量",           // Slide 10-【3】
            "轨头侧面探测完成"
        };
        private static readonly string[] StageNames = { "探头偏角", "移动探测", "测距确认", "完成" }; // 步骤名（2026-08-23 按 台词.pptx：步骤1：探头偏角/步骤2：移动探测/步骤3：测距确认）
        // 数字人台词气泡（2026-08-23 按 台词.pptx Slide 8-11）
        private static readonly string[] SpeechLines = {
            "把探头放在轨头侧面，准备进行探测",                 // 初始定位（Slide 8-【1】）
            "角度正确！可以向前移动探头啦",                     // 校角确认（Slide 9-【1】）
            "很棒，在轨头侧面也探测到了伤损！用多功能尺测量确认一下出波位置吧" // 检出（Slide 10-【1】）
        };
        private static readonly string[] FinalSpeech = { // 测量完成 + 完成引导（分段展示）
            "探头入射点距离本侧焊缝熔合线120mm，这说明我们在轨头侧面也探测到了伤损！",
            "点击透视视图看看超声波传播路径",
            "轨头侧面伤损探测完成，点击进入轨腰部位探测吧" // Slide 11-【1】
        };

        private void Awake()
        {
            Bind(resetButton, ShowResetDialog); Bind(enterNextButton, EnterNextModule);
            Bind(FindButton("ConfirmButton"), ResetAll); Bind(FindButton("CancelButton"), HideResetDialog); Bind(FindButton("NormalButton"), SetNormalView); Bind(FindButton("PerspectiveButton"), SetPerspectiveView);
            // 先绑定尺子（提供 PixelsPerMm），再绑定探头（几何标定依赖 PixelsPerMm）
            rulerDrag?.Bind(this); probeDrag?.Bind(this);
            EnableRaycast(probeDrag?.probeVisual); EnableRaycast(rulerDrag?.rulerImage); EnableRaycast(probeDrag?.angleSlider);
            EnableRaycast(resetButton); EnableRaycast(enterNextButton); EnableRaycast(FindButton("ConfirmButton"));
            EnableRaycast(FindButton("CancelButton")); EnableRaycast(FindButton("NormalButton")); EnableRaycast(FindButton("PerspectiveButton"));
            if (sfx != null) sfx.spatialBlend = 0f;
            if (waveformFx != null)
            {
                waveformFx.scanMinMm = 0f; waveformFx.scanMaxMm = 200f;
                // 老板 2026-08-16 最终定稿：伤损波 160mm 短波出现 → 122-123mm 最高 → 120mm 停止；
                // 伤损波最高时与始波同高（peakStrength = startPeakHeight）；初态即 160mm 短波小波形，扫描平移时随距离变化。
                waveformFx.appearMm = 160f; waveformFx.peakMm = 123f; waveformFx.stopMm = 120f;
                waveformFx.peakStrength = waveformFx.startPeakHeight; // 伤损波峰值=始波高度
                waveformFx.noiseAmp = .012f; // 伤损波噪声调小，峰顶毛刺不抬高（2026-08-18 老板：与始波视觉等高，M2/M4 同款）
                waveformFx.SetDistanceMm(160f);
                foreach (Transform child in waveformFx.transform) child.gameObject.SetActive(false);
            }
            ApplyView(false);
            EnterPositioning();
            // 数字人台词气泡（PPT）：M3 不创建独立云朵，文字放场景 dialog 节点（老板后续自行添加）
            speechBubble = gameObject.AddComponent<ModuleSpeechBubble>();
            if (instructionText != null) speechBubble.SetFont(instructionText.font);
            var dialog = FindDeep(transform, "DigitalHumanStage/dialog");
            if (dialog != null)
            {
                speechBubble.SetAnchor(dialog);
                speechBubble.useExistingCloud = true;
                speechBubble.anchorOffset = new Vector2(-339f, 30f); // 对齐云朵（dialog/bg）中心，与 M2 合同一致（老板 2026-08-23）
                speechBubble.bubbleSize = new Vector2(264f, 198f);   // 云朵内部文字区（dialog 局部像素）
                speechBubble.Show(SpeechLines[0]);
            }
            else speechBubble.createOnlyWhenAnchored = true; // 云朵节点就位前不显示
        }
        private static void Bind(Button button, UnityAction action) { if (!button) return; button.onClick.RemoveListener(action); button.onClick.AddListener(action); }
        private static void EnableRaycast(Component comp) { if (!comp) return; foreach (var img in comp.GetComponentsInChildren<Image>(true)) img.raycastTarget = true; }
        private Button FindButton(string name) { foreach (var b in GetComponentsInChildren<Button>(true)) if (b.name == name) return b; return null; }
        /// <summary>递归查找子物体（含未激活；支持斜杠路径）。</summary>
        private static Transform FindDeep(Transform root, string path)
        {
            if (root == null) return null;
            if (path.Contains("/"))
            {
                var cur = root;
                foreach (var p in path.Split('/')) { cur = FindChildByName(cur, p); if (cur == null) return null; }
                return cur;
            }
            return FindChildByName(root, path);
        }
        private static Transform FindChildByName(Transform parent, string name)
        {
            foreach (Transform child in parent)
            {
                if (child.name == name) return child;
                var hit = FindChildByName(child, name);
                if (hit != null) return hit;
            }
            return null;
        }

        private void EnterPositioning()
        {
            // M3 不再播放自动耦合剂 Intro，直接进入定位，避免开场延迟。
            if (couplantOverlay != null) couplantOverlay.gameObject.SetActive(false);
            RulerDocked = AngleVerifiedByRuler = false;
            SetBusinessButtons(true); probeDrag?.SetInputLocked(false); probeDrag?.Unlock(); rulerDrag?.Unlock();
            probeDrag?.SetAngleLocked(true); // 流程：先放探头 → 放尺子校角吸附 → 尺子到位后才解锁角度滑块
            Go(Stage.Positioning);
        }
        private void SetBusinessButtons(bool value) {
            if (resetButton != null) resetButton.interactable = value; var normal = FindButton("NormalButton"); if (normal != null) normal.interactable = value;
            var perspective = FindButton("PerspectiveButton"); if (perspective != null) perspective.interactable = value; }
        public void NotifyPlacementChanged() { } // 探头就位仅解锁尺子吸附流程（扫描由撤尺进入）
        public void NotifyRulerPositioned()
        {
            if (CurrentStage != Stage.Positioning) return;
            RulerDocked = true;
            probeDrag?.SetAngleLocked(false); // 尺子校角吸附成功 → 解锁角度滑块
        }
        /// <summary>校角稳定确认（M2 同款：13° 稳定停留 0.5s）→ 锁角度 + 正确音 + 解锁撤尺。</summary>
        public void NotifyAngleConfirmed()
        {
            if (CurrentStage != Stage.Positioning || !RulerDocked || AngleVerifiedByRuler) return;
            AngleVerifiedByRuler = true;
            probeDrag?.SetAngleLocked(true);
            PlayCorrect();
            rulerDrag?.UnlockRetract();
            speechBubble?.Show(SpeechLines[1]); // 角度正确（Slide 9-【1】）
        }
        /// <summary>尺子拖回 RulerHome 归槽（恢复 Home 初态）→ 进入扫描，解锁探头平移。</summary>
        public void NotifyRulerRetracted() { if (CurrentStage == Stage.Positioning && AngleVerifiedByRuler) Go(Stage.Scanning); }
        public void NotifyDistance(float mm)
        {
            // 老板 2026-08-16 定稿：波形只在扫描平移阶段变化（放置/校角阶段保持初态）；
            // 波形 mm 与扫描 mm 解耦——扫描起点对应波形 160mm 短波、扫描终点对应 120mm 停止；
            // 伤损波移动速度 = waveformSpeed 倍于探头移动（不触碰扫描/探头/尺子 Scene 值）。
            if (CurrentStage == Stage.Scanning && probeDrag != null && waveformFx != null)
            {
                var t = Mathf.InverseLerp(probeDrag.scanStartMm, probeDrag.scanEndMm, mm) * waveformSpeed;
                var wmm = Mathf.Lerp(160f, 120f, Mathf.Clamp01(t));
                waveformFx.SetDistanceMm(wmm);
            }
            // 检出 = 扫描中 && 角度正确 && 射线末端实际照射到伤损点（末端照到伤损才触发蜂鸣）。
            if (!Detected && CurrentStage == Stage.Scanning && probeDrag != null && probeDrag.AngleCorrect && probeDrag.BeamHitsDamage) NotifyDetected();
            _prevMm = mm;
        }
        private void NotifyDetected()
        {
            if (Detected || CurrentStage != Stage.Scanning) return;
            Detected = true;
            probeDrag?.SetInputLocked(true);
            if (sfx != null && beepClip != null) sfx.PlayOneShot(beepClip, sfxVolume);
            // 老板 2026-08-16 定稿：检出后尺子不自动出架，等玩家拖到测量初始位吸附并应用调整角度；钢轨红椭圆变橙（射线保持绿色）。
            rulerDrag?.PrepareMeasure();
            RefreshDamageMarker(); // 老板 2026-08-23：伤损变色仅透视可见（PerspectiveOn && Detected），检出本身只报警
            Go(Stage.Measuring);
            idleHelp?.ResetIdle();
            speechBubble?.Show(SpeechLines[2]); // 检出（Slide 10-【1】）
        }
        /// <summary>检出反馈：钢轨红椭圆（伤损）变橙色——竖椭圆、半透明橙、对齐伤损中心（老板 2026-08-16 定稿）。</summary>
        private void ShowDamageMarker()
        {
            if (damageMarker == null) return;
            var img = damageMarker.GetComponent<Image>();
            if (img != null) img.sprite = M2ProbeDrag.GetEllipseSprite(new Color(1f, .55f, .1f, .45f), ref _damageMarkerSprite); // 半透明橙
            var rt = damageMarker.GetComponent<RectTransform>();
            if (rt != null && probeDrag != null && probeDrag.railViewport != null)
            {
                rt.anchorMin = rt.anchorMax = probeDrag.railViewport.pivot; rt.pivot = new Vector2(.5f, .5f);
                rt.sizeDelta = new Vector2(16f, 36f); // 竖椭圆（贴合红椭圆竖向形状）
                rt.localScale = Vector3.one;
                rt.anchoredPosition = probeDrag.DamageEllipsePointInRail; // 对齐红椭圆中心（判定区域，2026-08-18 老板：M3/M4 统一）
            }
            damageMarker.SetActive(true);
        }
        /// <summary>伤损标记显隐统一入口：透视开且已检出才显示（老板 2026-08-23：未开透视仅报警，透视才能看到伤损变色）。</summary>
        private void RefreshDamageMarker()
        {
            if (damageMarker == null) return;
            if (PerspectiveOn && Detected) ShowDamageMarker();
            else damageMarker.SetActive(false);
        }
        public void NotifyMeasured()
        {
            if (Measured) return;
            Measured = true;
            if (measurementBubble != null) measurementBubble.SetActive(true);
            PlayCorrect();
            Go(Stage.Completed);
            speechBubble?.ShowSegments(FinalSpeech); // 测量完成 120mm 结论 + 进入下一模块引导（分段）
        }
        /// <summary>正确提示音（探头放置成功 / 尺子校角吸附 / 测量完成共用，与 M2 一致）。</summary>
        public void PlayCorrect() { if (sfx != null && correctClip != null) sfx.PlayOneShot(correctClip, sfxVolume); }
        public void EnterNextModule()
        {
            onCompleted?.Invoke();
            if (!string.IsNullOrEmpty(nextSceneName)) UnityEngine.SceneManagement.SceneManager.LoadScene(nextSceneName); // M3 通关 → 进入 M4（与 M2 同款）；2026-08-18：不再先 ResetTool 归位，直接切场景
        }
        public void ShowResetDialog() => SetDialog(true);
        public void HideResetDialog() => SetDialog(false);
        private void SetDialog(bool visible)
        {
            transform.Find("ModalLayer")?.gameObject.SetActive(visible); idleHelp?.SetPaused(visible); probeDrag?.SetInputLocked(visible); rulerDrag?.SetInputLocked(visible); SetBusinessButtons(!visible);
            if (!visible && (!RulerDocked || AngleVerifiedByRuler)) probeDrag?.SetAngleLocked(true); // 关闭对话框后恢复流程锁：吸附前/校角确认后角度锁定
        }
        public void SetNormalView() => ApplyView(false);
        public void SetPerspectiveView() => ApplyView(true);
        private void ApplyView(bool on)
        {
            PerspectiveOn = on;
            if (railBg != null) railBg.gameObject.SetActive(!on);
            if (railPerspective != null) railPerspective.SetActive(on);
            RefreshDamageMarker(); // 伤损标记仅透视+检出可见（老板 2026-08-23：未开透视仅报警）
            var selected = new Color(.08f, .42f, .66f); var idle = new Color(.58f, .61f, .65f);
            if (normalBtnImg != null) { normalBtnImg.color = on ? idle : selected; SetButtonText(normalBtnImg, on ? new Color(.12f, .15f, .18f) : Color.white); }
            if (perspectiveBtnImg != null) { perspectiveBtnImg.color = on ? selected : idle; SetButtonText(perspectiveBtnImg, on ? Color.white : new Color(.12f, .15f, .18f)); }
        }
        private static void SetButtonText(Image image, Color color) { var text = image ? image.GetComponentInChildren<TMP_Text>(true) : null; if (text) text.color = color; }
        public void ResetAll()
        {
            Detected = Measured = RulerDocked = AngleVerifiedByRuler = false; _prevMm = 55f;
            StopAllCoroutines();
            if (damageMarker != null) { damageMarker.SetActive(false); damageMarker.GetComponent<Image>().color = Color.red; }
            if (detectionBanner != null) detectionBanner.SetActive(false);
            if (measurementBubble != null) measurementBubble.SetActive(false);
            SetDialog(false); probeDrag?.ResetTool(); rulerDrag?.Hide();
            waveformFx?.ResetWave(160f);
            idleHelp?.ResetAll(); ApplyView(false);
            EnterPositioning();
            speechBubble?.Show(SpeechLines[0]); // 重置：气泡回到初始引导
        }
        private void Go(Stage stage)
        {
            CurrentStage = stage;
            if (stage == Stage.Scanning)
            {
                if (rulerDrag != null) rulerDrag.unlocked = false;
                probeDrag?.SetAngleLocked(true); // 校角完成：角度锁定到 Reset
            }
            idleHelp?.ResetIdle(); UpdateUi();
        }
        private void UpdateUi()
        {
            var i = Mathf.Clamp((int)CurrentStage - 1, 0, 3);
            if (instructionText != null && i < DefaultHints.Length) instructionText.text = DefaultHints[i];
            if (stepProgressText != null) stepProgressText.text = $"步骤{Mathf.Clamp(i + 1, 1, 3)}：{StageNames[i]}"; // 2026-08-23 按 台词.pptx：去 /3、中文冒号，改“步骤X：阶段名”
            var done = CurrentStage == Stage.Completed;
            if (completionPanel != null) completionPanel.SetActive(done);
            if (enterNextButton != null) enterNextButton.gameObject.SetActive(done);
            if (done && completionText != null) completionText.text = !string.IsNullOrEmpty(nextSceneName) || (onCompleted != null && onCompleted.GetPersistentEventCount() > 0) ? "轨头侧面探测完成" : "下一模块待接入";
        }
    }
}
