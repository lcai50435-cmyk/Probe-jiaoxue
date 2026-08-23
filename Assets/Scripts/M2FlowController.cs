using System.Collections;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
namespace M2
{
    public class M2FlowController : MonoBehaviour
    {
        public enum Stage { Couplant, Positioning, Scanning, Measuring, Completed }
        public M2ProbeDrag probeDrag;
        public M2RulerDrag rulerDrag;
        [System.NonSerialized] public M2CouplantFx couplantFx; // 运行时挂载，防止 Unity 写回冻结 Scene
        [System.NonSerialized] public ModuleSpeechBubble speechBubble; // 数字人台词气泡（运行时挂载）
        public M2WaveformFx waveformFx; // Scene 序列化引用（波形窗口区域已解冻直做）
        public GameObject couplantMask, beamLayer, railPerspective, detectionBanner, completionPanel, measurementBubble;
        public RectTransform couplantOverlay, railBg;
        public Image normalBtnImg, perspectiveBtnImg;
        public TMP_Text instructionText, stepProgressText, applyButtonText, completionText;
        public Button applyButton, enterNextButton, resetButton, nextButton;
        public GameObject[] stepPanels;
        public AudioSource sfx;
        public AudioClip beepClip, correctClip;
        [Tooltip("音效播放音量（2026-08-18 老板要求整体调小）")]
        public float sfxVolume = 0.4f;
        public M2IdleHelp idleHelp;
        public float targetAngle = 10f, targetDistance = 110f, distanceToleranceMm = 2f;
        public UnityEvent onCompleted;
        [Tooltip("点击下一模块加载的场景名（Inspector 可配置；空则不跳转，保持占位）")]
        public string nextSceneName = "M3"; // M2 通关 → M3（老板 2026-08-16）
        public Stage CurrentStage { get; private set; } = Stage.Couplant;
        public bool CouplantApplied, Detected, Measured, PerspectiveOn, AngleVerifiedByRuler, RulerDocked;
        private bool _applying; private float _timeScaleBeforeDialog = 1f;
        private bool _perspectiveHintShown; // 首次点透视提示已显示（老板 2026-08-23：只第一次出现）
        private TMP_Text _bubbleText;
        private Image _damageMarker; private Sprite _damageMarkerSprite; // 伤损橙标记（运行时椭圆，检出时显示）
        private static readonly string[] DefaultHints = { "", "将探头放置在轨头顶面，用多功能尺将探头向内偏转10°", "将探头以10度偏角向前移动，注意观察波形变化", "将定位尺0刻度对准探头入射点，进行测量", "轨头顶面探测完成" }; // 2026-08-23 按 台词.pptx；[0] 涂耦合剂提示已删（改由数字人气泡承载）
        private static readonly string[] StageNames = { "涂抹耦合剂", "探头偏角", "移动探测", "测距确认", "完成" }; // 步骤名（2026-08-23 按 台词.pptx：步骤1：涂抹耦合剂/步骤2：探头偏角/步骤3：移动探测/步骤4：测距确认）
        // 数字人台词气泡（2026-08-23 按 台词.pptx Slide 3-6）
        private static readonly string[] SpeechLines = {
            "探测前先在所有探测面均匀涂抹耦合剂，让超声波束更好传播", // 初始
            "还没涂抹耦合剂呢！",                                     // 未涂就拖探头
            "耦合剂涂好啦！现在拿起探头，放到轨头顶面，进行探测",     // 涂好
            "角度正确！向前移动探头进行探测吧",                       // 校角确认
            "非常好，成功在轨头顶面探测到伤损了！接下来用多功能尺确认一下具体出波位置" // 检出
        };
        private static readonly string[] FinalSpeech = { // 测量完成（三段一体，段间 1 秒；老板 2026-08-23 定稿）
            "可以看到探头入射点距离本侧焊缝熔合线正好也是110mm",
            "这就说明我们在轨头顶面利用新工艺成功捕捉到了伤损！",
            "可以点击透视视图查看超声波传播路径"
        };
        private const string PerspectiveHint = "看！绿色光束就是超声波束，遇到红色伤损就会中断传播发生反射！"; // 首次点透视（Slide 7-【1】，仅第一次+探头已放置）
        private void Awake()
        {
            Bind(applyButton, ApplyCouplant); Bind(resetButton, ShowResetDialog); Bind(enterNextButton, EnterNextModule);
            // 完成按钮文案（老板 2026-08-23：进入下一模块 → 进入轨头侧面探测；运行时覆盖冻结 Scene 旧文本）
            if (enterNextButton != null)
            {
                var nextText = enterNextButton.GetComponentInChildren<TMP_Text>(true);
                if (nextText != null) nextText.text = "进入轨头侧面探测";
            }
            Bind(FindButton("ConfirmButton"), ResetAll); Bind(FindButton("CancelButton"), HideResetDialog); Bind(FindButton("NormalButton"), SetNormalView); Bind(FindButton("PerspectiveButton"), SetPerspectiveView);
            rulerDrag?.Bind(this); probeDrag?.Bind(this);
            if (completionPanel != null && enterNextButton != null && enterNextButton.transform.parent != completionPanel.transform) enterNextButton.transform.SetParent(completionPanel.transform, false);
            SwapRailSprites(); ApplyView(false);
            waveformFx?.SetDistanceMm(150f); UpdateUi(); // 波形窗口已 Scene 直做（4:3/刻度/点状网格/序列化挂载）
            if (waveformFx != null)
            {
                // 老板 2026-08-16：M2 参照 M3 制作波纹移动——初态即有 150mm 短波伤损波；
                // 伤损波 150mm 短波出现 → 115mm 最高 → 110mm 停止；最高时与始波同高（peakStrength=startPeakHeight）。
                waveformFx.appearMm = 150f; waveformFx.peakMm = 115f; waveformFx.stopMm = 110f; // M2 合同（与 Scene 序列化一致，防御覆盖）
                waveformFx.peakStrength = waveformFx.startPeakHeight; // 伤损波峰值=始波高度（与 M3 同款）
                waveformFx.noiseAmp = .012f; // 伤损波噪声调小，峰顶毛刺不抬高（2026-08-18 老板：与始波视觉等高，M4 同款）
                waveformFx.SetDistanceMm(150f);
            }
            _bubbleText = measurementBubble != null ? measurementBubble.GetComponentInChildren<TMP_Text>(true) : null;
            if (couplantMask != null && couplantOverlay != null)
            {
                couplantFx = gameObject.AddComponent<M2CouplantFx>();
                couplantFx.Bind(railBg, couplantMask.GetComponent<RectTransform>(), couplantOverlay.GetComponentInChildren<Image>(true), couplantOverlay.GetComponent<CanvasGroup>());
            }
            // 数字人台词气泡（PPT：替换底部提示；运行时创建，冻结 Scene 不改）
            speechBubble = gameObject.AddComponent<ModuleSpeechBubble>();
            speechBubble.segmentInterval = 1f; // 老板 2026-08-23：分段台词一句话放完停留 1 秒
            if (instructionText != null) speechBubble.SetFont(instructionText.font);
            // 老板定稿：场景已自带云朵（dialog/bg 节点），只创建文字，文字区对齐云朵中心（不新建云朵 Image）
            var dialog = FindDeep(transform, "DigitalHumanStage/dialog");
            if (dialog != null)
            {
                speechBubble.SetAnchor(dialog);
                speechBubble.useExistingCloud = true;
                speechBubble.anchorOffset = new Vector2(-339f, 30f); // 对齐云朵（dialog/bg）中心；y=30（老板定稿）
                speechBubble.bubbleSize = new Vector2(264f, 198f);   // 云朵内部文字区（dialog 局部像素）
            }
            speechBubble.Show(SpeechLines[0]);
            // Slide 5/6【4】删掉：场景静态 Hint 提示（冻结 Scene 不删节点，运行时隐藏）
            foreach (var t in GetComponentsInChildren<TMP_Text>(true)) if (t.name == "Hint") t.gameObject.SetActive(false);
        }
        private Button FindButton(string name) => GetComponentsInChildren<Button>(true).FirstOrDefault(b => b.name == name);
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
        private static void Bind(Button button, UnityAction action) { if (button != null) { button.onClick.RemoveListener(action); button.onClick.AddListener(action); } }
        public void ApplyCouplant()
        {
            if (_applying || CouplantApplied) return;
            _applying = true; if (applyButton != null) applyButton.interactable = false;
            couplantFx?.Play(OnCouplantDone);
        }
        private void OnCouplantDone()
        {
            _applying = false; CouplantApplied = true; if (applyButtonText != null) applyButtonText.text = "已涂抹";
            probeDrag?.Unlock(); Go(Stage.Positioning);
            speechBubble?.Show(SpeechLines[2]); // 涂好：引导拿探头
        }
        /// <summary>未涂耦合剂就尝试拖探头：数字人气泡提示（台词.pptx Slide 3-【5】）。</summary>
        public void NotifyBlockedDrag()
        {
            if (!CouplantApplied && CurrentStage == Stage.Couplant) speechBubble?.Show(SpeechLines[1]);
        }
        public void NotifyPlacementChanged() { if (CurrentStage == Stage.Positioning && probeDrag != null && probeDrag.Placed) rulerDrag?.ShowAngleGuide(); }
        public void NotifyRulerAligned() { if (CurrentStage == Stage.Positioning) { RulerDocked = true; probeDrag?.SetAngleLocked(false); } }
        /// <summary>正确提示音（尺子校角吸附 / 校角确认 / 测量完成共用，与 M3 一致）。</summary>
        public void PlayCorrect() { if (sfx != null && correctClip != null) sfx.PlayOneShot(correctClip, sfxVolume); }
        public void NotifyAngleConfirmed()
        {
            if (CurrentStage != Stage.Positioning || !RulerDocked) return;
            AngleVerifiedByRuler = true;
            probeDrag?.SetAngleLocked(true);
            if (sfx != null && correctClip != null) sfx.PlayOneShot(correctClip, sfxVolume);
            rulerDrag?.UnlockRetract();
            speechBubble?.Show(SpeechLines[3]); // 角度正确
        }
        public void NotifyRulerRetracted() { if (CurrentStage == Stage.Positioning && AngleVerifiedByRuler) Go(Stage.Scanning); }
        public void NotifyDistance(float mm) { waveformFx?.SetDistanceMm(mm); }
        public void NotifyDetected()
        {
            if (Detected || CurrentStage != Stage.Scanning) return;
            Detected = true;
            probeDrag?.SetInputLocked(true);
            if (sfx != null && beepClip != null) sfx.PlayOneShot(beepClip, sfxVolume);
            if (nextButton != null) nextButton.gameObject.SetActive(false); // 老板定稿：检出即测距，无"下一步"门控（与 M3 一致）
            rulerDrag?.PrepareMeasure(); // 老板 2026-08-16：尺子不自动出架，玩家自己从工具架拖到测量放置位置吸附
            RefreshDamageMarker(); // 老板 2026-08-23：伤损变色仅透视可见（PerspectiveOn && Detected），检出本身只报警
            Go(Stage.Measuring);
            idleHelp?.ResetIdle();
            speechBubble?.Show(SpeechLines[4]); // 检出
        }
        /// <summary>检出反馈：钢轨红椭圆（伤损）变橙色——竖椭圆、半透明橙、对齐伤损中心（老板 2026-08-16 定稿）。</summary>
        private void ShowDamageMarker()
        {
            var probe = probeDrag; if (probe == null || probe.railViewport == null) return;
            if (_damageMarker == null)
            {
                var go = new GameObject("~M2DamageMarker", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                go.hideFlags = HideFlags.DontSave;
                go.transform.SetParent(probe.railViewport, false);
                var rt = (RectTransform)go.transform;
                rt.anchorMin = rt.anchorMax = probe.railViewport.pivot; rt.pivot = new Vector2(.5f, .5f);
                rt.sizeDelta = new Vector2(16f, 36f); // 竖椭圆（贴合红椭圆竖向形状）
                _damageMarker = go.GetComponent<Image>();
                _damageMarker.raycastTarget = false;
            }
            _damageMarker.sprite = M2ProbeDrag.GetEllipseSprite(new Color(1f, .55f, .1f, .45f), ref _damageMarkerSprite); // 半透明橙
            _damageMarker.color = Color.white;
            var rt2 = (RectTransform)_damageMarker.transform;
            rt2.localScale = Vector3.one;
            rt2.anchoredPosition = probe.DamagePointInRail; // 对齐伤损中心
            _damageMarker.gameObject.SetActive(true);
        }
        /// <summary>伤损标记显隐统一入口：透视开且已检出才显示（老板 2026-08-23：未开透视仅报警，透视才能看到伤损变色）。</summary>
        private void RefreshDamageMarker()
        {
            if (!PerspectiveOn || !Detected) { if (_damageMarker != null) _damageMarker.gameObject.SetActive(false); return; }
            ShowDamageMarker();
        }
        public void NotifyMeasured()
        {
            if (Measured) return;
            Measured = true; // 老板 2026-08-16：M2 通过后不显示“测量完成”提示气泡（measurementBubble 不再激活）
            if (sfx != null && correctClip != null) sfx.PlayOneShot(correctClip, sfxVolume); Go(Stage.Completed);
            speechBubble?.ShowSegments(FinalSpeech); // 测量完成：110mm 结论（分段展示）
        }
        public void EnterNextModule()
        {
            onCompleted?.Invoke();
            if (!string.IsNullOrEmpty(nextSceneName)) UnityEngine.SceneManagement.SceneManager.LoadScene(nextSceneName); // 老板 2026-08-16：M2 通关 → 进入 M3；2026-08-18：不再先 ResetTool 归位，直接切场景
        }
        public void ShowResetDialog() => SetDialog(true);
        public void HideResetDialog() => SetDialog(false);
        private void SetDialog(bool visible) { var modal = transform.Find("ModalLayer")?.gameObject; var wasOpen = modal != null && modal.activeSelf; if (visible && !wasOpen) { _timeScaleBeforeDialog = Time.timeScale; Time.timeScale = 0f; } else if (!visible && wasOpen) Time.timeScale = _timeScaleBeforeDialog; if (modal != null) modal.SetActive(visible); idleHelp?.SetPaused(visible); }
        public void SetNormalView() => ApplyView(false);
        public void SetPerspectiveView() => ApplyView(true);
        private void SwapRailSprites()
        {
            SwapSprite(railBg != null ? railBg.GetComponentInChildren<Image>(true) : null, "俯视角");
            SwapSprite(railPerspective != null ? railPerspective.GetComponentInChildren<Image>(true) : null, "俯视角透视");
            if (railBg != null) { railBg.anchoredPosition = new Vector2(Mathf.Round(railBg.anchoredPosition.x), Mathf.Round(railBg.anchoredPosition.y)); railBg.sizeDelta = new Vector2(Mathf.Round(railBg.sizeDelta.x), Mathf.Round(railBg.sizeDelta.y)); } // 铁轨非整数 Rect 像素对齐，消除 Play 线条模糊
        }
        private static void SwapSprite(Image image, string name) { if (image == null) return; var s = Resources.LoadAll<Sprite>(name); if (s != null && s.Length > 0) image.sprite = s[0]; }
        private void ApplyView(bool on)
        {
            PerspectiveOn = on;
            if (railBg != null) railBg.gameObject.SetActive(!on); if (railPerspective != null) railPerspective.SetActive(on);
            // 首次点击透视视图（且探头已放置、分段台词未在播）：数字人云朵气泡提示（老板 2026-08-23，Slide 7-【1】）
            if (on && !_perspectiveHintShown && probeDrag != null && probeDrag.Placed && speechBubble != null && !speechBubble.Busy)
            {
                _perspectiveHintShown = true;
                speechBubble.Show(PerspectiveHint);
            }
            Color selected = new Color(.08f, .42f, .66f), idle = new Color(.58f, .61f, .65f);
            if (normalBtnImg != null) { normalBtnImg.color = on ? idle : selected; SetButtonText(normalBtnImg, on ? new Color(.12f, .15f, .18f) : Color.white); }
            if (perspectiveBtnImg != null) { perspectiveBtnImg.color = on ? selected : idle; SetButtonText(perspectiveBtnImg, on ? Color.white : new Color(.12f, .15f, .18f)); }
            RefreshDamageMarker(); // 伤损标记仅透视+检出可见（老板 2026-08-23：未开透视仅报警）
        }
        private static void SetButtonText(Image image, Color color) { if (image == null) return; var text = image.GetComponentInChildren<TMP_Text>(true); if (text != null) text.color = color; }
        public void ResetAll()
        {
            CouplantApplied = Detected = Measured = AngleVerifiedByRuler = RulerDocked = _applying = false; _perspectiveHintShown = false; StopAllCoroutines();
            couplantFx?.Reset();
            if (couplantMask != null) couplantMask.SetActive(false); if (detectionBanner != null) detectionBanner.SetActive(false); if (measurementBubble != null) measurementBubble.SetActive(false);
            if (_damageMarker != null) _damageMarker.gameObject.SetActive(false);
            if (nextButton != null) nextButton.gameObject.SetActive(false);
            if (applyButton != null) applyButton.interactable = true; if (applyButtonText != null) applyButtonText.text = "涂抹耦合剂";
            probeDrag?.ResetTool(); rulerDrag?.ResetTool(); waveformFx?.ResetWave(150f); idleHelp?.ResetAll(); ApplyView(false); SetDialog(false); Go(Stage.Couplant);
            speechBubble?.Show(SpeechLines[0]); // 重置：气泡回到初始引导
        }
        private void Go(Stage stage)
        {
            CurrentStage = stage;
            if (stage == Stage.Scanning) { probeDrag?.SetAngleLocked(true); probeDrag?.ShowBeam(); }
            idleHelp?.ResetIdle(); UpdateUi();
        }
        private void UpdateUi()
        {
            var i = Mathf.Min((int)CurrentStage, 4);
            var done = CurrentStage == Stage.Completed;
            if (instructionText != null) instructionText.text = DefaultHints[i];
            if (stepProgressText != null) stepProgressText.text = done ? string.Empty : $"步骤{Mathf.Min(i + 1, 4)}：{StageNames[i]}"; // 2026-08-23 按 台词.pptx：去 /4、中文冒号；完成阶段不显示“步骤X：完成”（老板 2026-08-23）
            foreach (var panel in stepPanels) if (panel != null) panel.SetActive(i < stepPanels.Length && panel == stepPanels[i]);
            if (completionPanel != null) completionPanel.SetActive(done); if (enterNextButton != null) enterNextButton.gameObject.SetActive(done);
            if (done && completionText != null) completionText.text = string.Empty; // 老板 2026-08-23：完成阶段不显示“轨头顶面探测完成”绿色文字（保留按钮）
        }
    }
}
