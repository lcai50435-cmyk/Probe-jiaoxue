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
        public M2WaveformFx waveformFx; // Scene 序列化引用（波形窗口区域已解冻直做）
        public GameObject couplantMask, beamLayer, railPerspective, detectionBanner, completionPanel, measurementBubble;
        public RectTransform couplantOverlay, railBg;
        public Image normalBtnImg, perspectiveBtnImg;
        public TMP_Text instructionText, stepProgressText, applyButtonText, completionText;
        public Button applyButton, enterNextButton, resetButton, nextButton;
        public GameObject[] stepPanels;
        public AudioSource sfx;
        public AudioClip beepClip, correctClip;
        public M2IdleHelp idleHelp;
        public float targetAngle = 10f, targetDistance = 110f, distanceToleranceMm = 2f;
        public UnityEvent onCompleted;
        public Stage CurrentStage { get; private set; } = Stage.Couplant;
        public bool CouplantApplied, Detected, Measured, PerspectiveOn, AngleVerifiedByRuler, RulerDocked;
        private bool _applying; private float _timeScaleBeforeDialog = 1f;
        private TMP_Text _bubbleText;
        private Image _damageMarker; private Sprite _damageMarkerSprite; // 伤损橙标记（运行时椭圆，检出时显示）
        private static readonly string[] DefaultHints = { "请先涂抹耦合剂", "放置探头，用定位尺把偏角校到 10°", "保持 10° 向前移动探头至入射点距红色损伤 110mm", "0mm 对准探头入射点，110mm 对准红色损伤", "轨头顶面探测完成" };
        private static readonly string[] StageNames = { "涂抹耦合剂", "探头定位与偏角", "移动探测", "尺子测距", "完成" };
        private void Awake()
        {
            Bind(applyButton, ApplyCouplant); Bind(resetButton, ShowResetDialog); Bind(enterNextButton, EnterNextModule);
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
                waveformFx.SetDistanceMm(150f);
            }
            _bubbleText = measurementBubble != null ? measurementBubble.GetComponentInChildren<TMP_Text>(true) : null;
            if (couplantMask != null && couplantOverlay != null)
            {
                couplantFx = gameObject.AddComponent<M2CouplantFx>();
                couplantFx.Bind(railBg, couplantMask.GetComponent<RectTransform>(), couplantOverlay.GetComponentInChildren<Image>(true), couplantOverlay.GetComponent<CanvasGroup>());
            }
        }
        private Button FindButton(string name) => GetComponentsInChildren<Button>(true).FirstOrDefault(b => b.name == name);
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
        }
        public void NotifyPlacementChanged() { if (CurrentStage == Stage.Positioning && probeDrag != null && probeDrag.Placed) rulerDrag?.ShowAngleGuide(); }
        public void NotifyRulerAligned() { if (CurrentStage == Stage.Positioning) { RulerDocked = true; probeDrag?.SetAngleLocked(false); } }
        public void NotifyAngleConfirmed()
        {
            if (CurrentStage != Stage.Positioning || !RulerDocked) return;
            AngleVerifiedByRuler = true;
            probeDrag?.SetAngleLocked(true);
            if (sfx != null && correctClip != null) sfx.PlayOneShot(correctClip);
            rulerDrag?.UnlockRetract();
        }
        public void NotifyRulerRetracted() { if (CurrentStage == Stage.Positioning && AngleVerifiedByRuler) Go(Stage.Scanning); }
        public void NotifyDistance(float mm) { waveformFx?.SetDistanceMm(mm); }
        public void NotifyDetected()
        {
            if (Detected || CurrentStage != Stage.Scanning) return;
            Detected = true;
            probeDrag?.SetInputLocked(true);
            if (sfx != null && beepClip != null) sfx.PlayOneShot(beepClip);
            if (nextButton != null) nextButton.gameObject.SetActive(false); // 老板定稿：检出即测距，无"下一步"门控（与 M3 一致）
            rulerDrag?.ShowMeasure(); // 直接解锁尺子，玩家可拖 0→110 双点测量
            ShowDamageMarker(); // 老板 2026-08-16 定稿：射线保持绿色，钢轨红椭圆（伤损）变橙
            Go(Stage.Measuring);
            idleHelp?.ResetIdle();
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
        public void NotifyMeasured()
        {
            if (Measured) return;
            Measured = true; if (measurementBubble != null) measurementBubble.SetActive(true);
            if (_bubbleText != null) _bubbleText.text = "测量完成"; // 删序列化「110mm」字样（2026-08-14 老板确认，不写回）
            if (sfx != null && correctClip != null) sfx.PlayOneShot(correctClip); Go(Stage.Completed);
        }
        public void EnterNextModule() { rulerDrag?.ResetTool(); onCompleted?.Invoke(); }
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
            Color selected = new Color(.08f, .42f, .66f), idle = new Color(.58f, .61f, .65f);
            if (normalBtnImg != null) { normalBtnImg.color = on ? idle : selected; SetButtonText(normalBtnImg, on ? new Color(.12f, .15f, .18f) : Color.white); }
            if (perspectiveBtnImg != null) { perspectiveBtnImg.color = on ? selected : idle; SetButtonText(perspectiveBtnImg, on ? Color.white : new Color(.12f, .15f, .18f)); }
        }
        private static void SetButtonText(Image image, Color color) { if (image == null) return; var text = image.GetComponentInChildren<TMP_Text>(true); if (text != null) text.color = color; }
        public void ResetAll()
        {
            CouplantApplied = Detected = Measured = AngleVerifiedByRuler = RulerDocked = _applying = false; StopAllCoroutines();
            couplantFx?.Reset();
            if (couplantMask != null) couplantMask.SetActive(false); if (detectionBanner != null) detectionBanner.SetActive(false); if (measurementBubble != null) measurementBubble.SetActive(false);
            if (_damageMarker != null) _damageMarker.gameObject.SetActive(false);
            if (nextButton != null) nextButton.gameObject.SetActive(false);
            if (applyButton != null) applyButton.interactable = true; if (applyButtonText != null) applyButtonText.text = "涂抹耦合剂";
            probeDrag?.ResetTool(); rulerDrag?.ResetTool(); waveformFx?.ResetWave(150f); idleHelp?.ResetAll(); ApplyView(false); SetDialog(false); Go(Stage.Couplant);
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
            if (instructionText != null) instructionText.text = DefaultHints[i];
            if (stepProgressText != null) stepProgressText.text = $"步骤 {Mathf.Min(i + 1, 4)}/4 · {StageNames[i]}";
            foreach (var panel in stepPanels) if (panel != null) panel.SetActive(i < stepPanels.Length && panel == stepPanels[i]);
            var done = CurrentStage == Stage.Completed;
            if (completionPanel != null) completionPanel.SetActive(done); if (enterNextButton != null) enterNextButton.gameObject.SetActive(done);
            if (done && completionText != null) completionText.text = onCompleted != null && onCompleted.GetPersistentEventCount() > 0 ? "轨头顶面探测完成" : "下一模块待接入";
        }
    }
}
