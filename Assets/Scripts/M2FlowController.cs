using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace M2
{
    /// <summary>M2 四阶段流程唯一状态所有者，含检出、重置和出口。</summary>
    public class M2FlowController : MonoBehaviour
    {
        public enum Stage { Couplant, Positioning, Scanning, Measuring, Completed }
        public M2ProbeDrag probeDrag;
        public M2RulerDrag rulerDrag;
        public M2WaveformGraphic waveform;
        public GameObject couplantMask, beamLayer, railPerspective, detectionBanner, completionPanel, measurementBubble;
        public RectTransform couplantOverlay, railBg;
        public Image normalBtnImg, perspectiveBtnImg;
        public TMP_Text waveStateText, currentDistanceText, instructionText, stepProgressText, applyButtonText, completionText;
        public Button applyButton, enterNextButton, resetButton, nextButton;
        public GameObject[] stepPanels;
        public AudioSource sfx;
        public AudioClip beepClip, correctClip;
        public M2IdleHelp idleHelp;
        public float couplantAnimDuration = 2f;
        public float targetAngle = 10f, targetDistance = 110f, peakTolerance = 1f;
        public string[] stepHints = { "请先涂抹耦合剂", "放置探头并调整偏角至 10°", "向前移动探头（150→100mm）", "拖动尺子：0 刻度对齐焊缝熔合线", "轨头顶面探测完成" };
        public Color hitYellow = new Color(.9f, .5f, .1f);
        public UnityEvent onCompleted;
        public Stage CurrentStage { get; private set; } = Stage.Couplant;
        public bool CouplantApplied, Detected, Measured, PerspectiveOn;
        private float _prevMm = 150f;
        private bool _applying;
        private static readonly string[] StageNames = { "涂抹耦合剂", "探头定位与偏角", "移动探测", "尺子测距", "完成" };

        private void Awake()
        {
            Bind(applyButton, ApplyCouplant); Bind(resetButton, ShowResetDialog); Bind(enterNextButton, EnterNextModule); Bind(nextButton, NextToMeasure);
            Bind(FindButton("ConfirmButton"), ResetAll); Bind(FindButton("CancelButton"), HideResetDialog);
            Bind(FindButton("NormalButton"), SetNormalView); Bind(FindButton("PerspectiveButton"), SetPerspectiveView);
            probeDrag?.Bind(this); rulerDrag?.Bind(this);
            if (completionPanel != null && enterNextButton != null && enterNextButton.transform.parent != completionPanel.transform)
                enterNextButton.transform.SetParent(completionPanel.transform, false);
            ApplyView(false); waveform?.SetDistanceMm(150f); UpdateUi();
        }
        private Button FindButton(string name)
        {
            foreach (var button in GetComponentsInChildren<Button>(true)) if (button.name == name) return button;
            return null;
        }
        private static void Bind(Button button, UnityAction action)
        {
            if (button == null) return;
            button.onClick.RemoveListener(action); button.onClick.AddListener(action);
        }
        public void ApplyCouplant()
        {
            if (_applying || CouplantApplied) return;
            _applying = true; if (applyButton != null) applyButton.interactable = false;
            if (couplantMask != null) couplantMask.SetActive(true); StartCoroutine(CouplantAnim());
        }
        private IEnumerator CouplantAnim()
        {
            if (couplantOverlay != null)
            {
                var group = couplantOverlay.GetComponent<CanvasGroup>();
                for (var t = 0f; t < couplantAnimDuration; t += Time.deltaTime)
                {
                    var p = Mathf.Clamp01(t / couplantAnimDuration); couplantOverlay.localScale = new Vector3(Mathf.Lerp(.05f, 1f, p), 1f, 1f);
                    if (group != null) group.alpha = p < .8f ? 1f : (1f - p) * 5f;
                    yield return null;
                }
            }
            yield return new WaitForSeconds(.2f);
            if (couplantMask != null) couplantMask.SetActive(false);
            _applying = false; CouplantApplied = true; if (applyButtonText != null) applyButtonText.text = "已涂抹";
            probeDrag?.Unlock(); Go(Stage.Positioning);
        }
        public void NotifyPlacementChanged() => TryEnterScanning();
        public void NotifyAngleCorrect() => TryEnterScanning();
        private void TryEnterScanning()
        {
            if (CurrentStage == Stage.Positioning && probeDrag != null && probeDrag.Placed && probeDrag.AngleCorrect) Go(Stage.Scanning);
        }
        public void NotifyDistance(float mm)
        {
            if (currentDistanceText != null) currentDistanceText.text = $"{mm:0}mm";
            waveform?.SetDistanceMm(mm);
            if (!Detected && waveStateText != null)
                waveStateText.text = waveform != null && mm <= waveform.growthStartMm ? "波峰生长" : "平直基线";
            if (!Detected && CurrentStage == Stage.Scanning && ((_prevMm > targetDistance && mm <= targetDistance) || Mathf.Abs(mm - targetDistance) <= peakTolerance)) NotifyDetected();
            _prevMm = mm;
        }
        private void NotifyDetected()
        {
            if (Detected || CurrentStage != Stage.Scanning) return;
            Detected = true;
            if (detectionBanner != null) detectionBanner.SetActive(true); if (waveStateText != null) waveStateText.text = "峰值锁定";
            if (sfx != null && beepClip != null) sfx.PlayOneShot(beepClip); if (nextButton != null) nextButton.gameObject.SetActive(true); idleHelp?.ResetIdle();
        }
        public void NextToMeasure() { if (Detected && CurrentStage == Stage.Scanning) { rulerDrag?.Show(); Go(Stage.Measuring); } }
        public void NotifyMeasured()
        {
            if (Measured) return;
            Measured = true; if (measurementBubble != null) measurementBubble.SetActive(true);
            if (sfx != null && correctClip != null) sfx.PlayOneShot(correctClip); Go(Stage.Completed);
        }
        public void EnterNextModule() { rulerDrag?.ResetTool(); onCompleted?.Invoke(); }
        public void ShowResetDialog() => SetDialog(true);
        public void HideResetDialog() => SetDialog(false);
        private void SetDialog(bool visible)
        {
            transform.Find("ModalLayer")?.gameObject.SetActive(visible);
            idleHelp?.SetPaused(visible);
        }
        public void SetNormalView() => ApplyView(false);
        public void SetPerspectiveView() => ApplyView(true);
        private void ApplyView(bool on)
        {
            PerspectiveOn = on; if (railBg != null) railBg.gameObject.SetActive(!on);
            if (railPerspective != null) railPerspective.SetActive(on);
            if (beamLayer != null) beamLayer.SetActive(on);
            var selected = new Color(.08f, .42f, .66f); var idle = new Color(.58f, .61f, .65f);
            if (normalBtnImg != null) { normalBtnImg.color = on ? idle : selected; SetButtonText(normalBtnImg, on ? new Color(.12f, .15f, .18f) : Color.white); }
            if (perspectiveBtnImg != null) { perspectiveBtnImg.color = on ? selected : idle; SetButtonText(perspectiveBtnImg, on ? Color.white : new Color(.12f, .15f, .18f)); }
        }
        private static void SetButtonText(Image image, Color color)
        {
            var text = image != null ? image.GetComponentInChildren<TMP_Text>(true) : null;
            if (text != null) text.color = color;
        }
        public void ResetAll()
        {
            CouplantApplied = Detected = Measured = _applying = false; _prevMm = 150f; StopAllCoroutines();
            if (couplantMask != null) couplantMask.SetActive(false); if (detectionBanner != null) detectionBanner.SetActive(false); if (measurementBubble != null) measurementBubble.SetActive(false);
            if (waveStateText != null) waveStateText.text = "平直基线"; if (nextButton != null) nextButton.gameObject.SetActive(false);
            if (applyButton != null) applyButton.interactable = true; if (applyButtonText != null) applyButtonText.text = "涂抹耦合剂";
            probeDrag?.ResetTool(); rulerDrag?.Hide(); waveform?.SetDistanceMm(150f); idleHelp?.ResetAll(); ApplyView(false); SetDialog(false); Go(Stage.Couplant);
        }
        private void Go(Stage stage) { CurrentStage = stage; idleHelp?.ResetIdle(); UpdateUi(); }
        private void UpdateUi()
        {
            var i = Mathf.Min((int)CurrentStage, 4); if (instructionText != null) instructionText.text = stepHints != null && i < stepHints.Length ? stepHints[i] : "";
            if (stepProgressText != null) stepProgressText.text = $"步骤 {Mathf.Min(i + 1, 4)}/4 · {StageNames[i]}";
            foreach (var panel in stepPanels) if (panel != null) panel.SetActive(i < stepPanels.Length && panel == stepPanels[i]);
            var done = CurrentStage == Stage.Completed; if (completionPanel != null) completionPanel.SetActive(done); if (enterNextButton != null) enterNextButton.gameObject.SetActive(done);
            if (done && completionText != null) completionText.text = onCompleted != null && onCompleted.GetPersistentEventCount() > 0 ? "轨头顶面探测完成" : "下一模块待接入";
        }
    }
}
