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
        public M2WaveformGraphic waveform;
        [System.NonSerialized] public M2WaveformFx waveformFx; // 运行时挂载，防止 Unity 写回冻结 Scene
        public GameObject couplantMask, beamLayer, railPerspective, detectionBanner, completionPanel, measurementBubble;
        public RectTransform couplantOverlay, railBg;
        public Image normalBtnImg, perspectiveBtnImg;
        public TMP_Text waveStateText, currentDistanceText, instructionText, stepProgressText, applyButtonText, completionText;
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
        private static readonly string[] DefaultHints = { "请先涂抹耦合剂", "放置探头，用定位尺把偏角校到 10°", "保持 10° 向前移动探头至入射点距红色损伤 110mm", "0mm 对准探头入射点，110mm 对准红色损伤", "轨头顶面探测完成" };
        private static readonly string[] StageNames = { "涂抹耦合剂", "探头定位与偏角", "移动探测", "尺子测距", "完成" };
        private void Awake()
        {
            Bind(applyButton, ApplyCouplant); Bind(resetButton, ShowResetDialog); Bind(enterNextButton, EnterNextModule); Bind(nextButton, NextToMeasure);
            Bind(FindButton("ConfirmButton"), ResetAll); Bind(FindButton("CancelButton"), HideResetDialog); Bind(FindButton("NormalButton"), SetNormalView); Bind(FindButton("PerspectiveButton"), SetPerspectiveView);
            rulerDrag?.Bind(this); probeDrag?.Bind(this);
            if (completionPanel != null && enterNextButton != null && enterNextButton.transform.parent != completionPanel.transform) enterNextButton.transform.SetParent(completionPanel.transform, false);
            SwapRailSprites(); ApplyView(false);
            if (waveform != null) waveform.enabled = false; // 禁用旧 M2WaveformGraphic（M3 节点不受影响）
            var waveArea = waveform != null ? waveform.transform.parent : null; // WaveGraphic 直接父为 WaveformArea_B
            var waveGrid = waveArea != null ? waveArea.Find("WaveGrid") : null; // RectTransform-only 节点，无 Graphic 冲突
            if (waveform != null && waveGrid == null) Debug.LogError("[M2Flow] 未找到 WaveGrid 节点，波形不可用");
            waveformFx = waveGrid != null ? waveGrid.gameObject.AddComponent<M2WaveformFx>() : null; // 运行时挂载新波形（不写回冻结 Scene）
            var areaRt = waveArea as RectTransform; // WaveformArea_B
            if (areaRt != null) { areaRt.sizeDelta = new Vector2(460f, 345f); var ap = areaRt.anchoredPosition; ap.y = 172.5f; areaRt.anchoredPosition = ap; } // 波形窗口 4:3 保下缘贴底
            waveformFx?.SetDistanceMm(150f); UpdateUi();
            if (waveStateText != null) waveStateText.gameObject.SetActive(false); // 波形提示词隐藏（2026-08-14 PPT 要求）
            if (currentDistanceText != null) currentDistanceText.gameObject.SetActive(false);
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
            if (detectionBanner != null) detectionBanner.SetActive(true);
            if (sfx != null && beepClip != null) sfx.PlayOneShot(beepClip);
            if (nextButton != null) nextButton.gameObject.SetActive(true);
            idleHelp?.ResetIdle();
        }
        public void NextToMeasure() { Debug.Log($"[M2Flow] NextToMeasure: Detected={Detected} Stage={CurrentStage}"); if (!Detected || CurrentStage != Stage.Scanning) return; rulerDrag?.ShowMeasure(); Go(Stage.Measuring); }
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
