using M1;
using M2;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace M4
{   /// <summary>M4 流程唯一状态所有者：定位 → 扫描 → 测距 → 完成（无耦合剂开场，与 M3 一致）。
    /// 2026-08-17 按 M4 PPT 对齐：扫描 55→40mm，波形复用 M2WaveformFx（55→45→40），目标以伤损为主。</summary>
    public class M4FlowController : MonoBehaviour
    {
        public enum Stage { Intro, Positioning, Scanning, Measuring, Completed } // Intro 保留兼容旧引用，运行时不再进入
        public M4ProbeDrag probeDrag;
        public M4RulerDrag rulerDrag;
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
        public M4IdleHelp idleHelp;
        public float introDuration = 2f, targetAngle = 10f, targetDistance = 40f, peakTolerance = 1f;
        /// <summary>伤损波移动速度倍率：2 = 探头移动 1mm 伤损波在波形 X 轴移动 2mm（老板 2026-08-16 定稿，可调）。</summary>
        public float waveformSpeed = 2f;
        public string[] stepHints = { "放置探头并调整偏角至向上 10°", "向前移动探头（55→40mm）", "拖动尺子：0 刻度对齐探头入射点，40mm 对齐伤损" };
        public UnityEvent onCompleted;
        public Stage CurrentStage { get; private set; } = Stage.Positioning;
        public bool Detected, Measured, PerspectiveOn, RulerDocked, AngleVerifiedByRuler;
        public float distanceToleranceMm = 2f;
        public bool PositioningRulerInPlace => rulerDrag != null && rulerDrag.positioned;
        private float _prevMm = 55f;
        private Sprite _damageMarkerSprite; // 伤损橙标记（椭圆）
        private static readonly string[] DefaultHints = {
            "将探头放置在轨腰左侧最上端，无偏角",
            "用定位尺（水平放置）将探头向上偏转 10°",
            "向前移动探头至入射点距伤损 40mm",
            "拖动尺子：0 刻度对齐探头入射点，40mm 对齐伤损"
        };
        private static readonly string[] StageNames = { "探头定位与偏角", "移动探测", "尺子测距", "完成" };

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
                // M4 PPT 定稿：伤损波 65mm 短波出现 → 55mm 最高 → 50mm 停止（2026-08-18 右移 10mm 避开始波重叠，终点对齐 50mm 刻度）；
                // 伤损波最高时视觉与始波同高；初态即 65mm 短波小波形，扫描平移时随距离变化。
                waveformFx.appearMm = 65f; waveformFx.peakMm = 55f; waveformFx.stopMm = 50f;
                // 伤损波最高时与始波直接等高（peakStrength = startPeakHeight；伤损波噪声已调小避免峰顶毛刺抬高，2026-08-18 老板）
                waveformFx.peakStrength = waveformFx.startPeakHeight;
                waveformFx.SetDistanceMm(65f);
                foreach (Transform child in waveformFx.transform) child.gameObject.SetActive(false);
            }
            ApplyView(false);
            EnterPositioning();
        }
        private static void Bind(Button button, UnityAction action) { if (!button) return; button.onClick.RemoveListener(action); button.onClick.AddListener(action); }
        private static void EnableRaycast(Component comp) { if (!comp) return; foreach (var img in comp.GetComponentsInChildren<Image>(true)) img.raycastTarget = true; }
        private Button FindButton(string name) { foreach (var b in GetComponentsInChildren<Button>(true)) if (b.name == name) return b; return null; }

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
                var wmm = Mathf.Lerp(65f, 50f, Mathf.Clamp01(t));
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
        }
        /// <summary>正确提示音（探头放置成功 / 尺子校角吸附 / 测量完成共用，与 M2 一致）。</summary>
        public void PlayCorrect() { if (sfx != null && correctClip != null) sfx.PlayOneShot(correctClip, sfxVolume); }
        public void EnterNextModule() { onCompleted?.Invoke(); } // 2026-08-18：不再先 ResetTool 归位，等下一模块接入后在此 LoadScene
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
            waveformFx?.ResetWave(65f);
            idleHelp?.ResetAll(); ApplyView(false);
            EnterPositioning();
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
            if (stepProgressText != null) stepProgressText.text = $"步骤 {Mathf.Clamp(i + 1, 1, 3)}/3 · {StageNames[i]}";
            var done = CurrentStage == Stage.Completed;
            if (completionPanel != null) completionPanel.SetActive(done);
            if (enterNextButton != null) enterNextButton.gameObject.SetActive(done);
            if (done && completionText != null) completionText.text = onCompleted != null && onCompleted.GetPersistentEventCount() > 0 ? "轨腰部位探测完成" : "下一模块待接入";
        }
    }
}
