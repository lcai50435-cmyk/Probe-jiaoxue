using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace M2
{
    /// <summary>防卡死帮助：只读 Flow 阶段并编排自动演示。</summary>
    public class M2IdleHelp : MonoBehaviour
    {
        public M2FlowController flow;
        public GameObject helpPanel;
        public TMP_Text helpText;
        public Button autoDemoButton, tryAgainButton;
        public M2ProbeDrag probeDrag;
        public float angleIdleTimeout = 30f;
        public float scanIdleTimeout = 60f;
        public float autoDemoDuration = 1f;
        private float _idle;
        private bool _paused, _demoRunning;

        private void Awake()
        {
            Bind(autoDemoButton, AutoDemo);
            Bind(tryAgainButton, TryAgain);
        }
        private static void Bind(Button button, UnityAction action)
        {
            if (button == null) return;
            button.onClick.RemoveListener(action);
            button.onClick.AddListener(action);
        }
        private void Update()
        {
            if (_paused || _demoRunning || flow == null) return;
            _idle += Time.deltaTime;
            if (flow.CurrentStage == M2FlowController.Stage.Positioning && _idle >= angleIdleTimeout) ShowHelp("需要帮助调整到 10° 吗？");
            else if (flow.CurrentStage == M2FlowController.Stage.Scanning && _idle >= scanIdleTimeout) ShowHelp("即将演示目标点探测");
        }
        public void ResetIdle() => _idle = 0f;
        public void SetPaused(bool paused) => _paused = paused;
        public void ResetAll()
        {
            StopAllCoroutines(); _demoRunning = false; _idle = 0f; HideHelp(); probeDrag?.SetInputLocked(false);
        }
        private void ShowHelp(string text)
        {
            if (helpPanel == null || helpPanel.activeSelf) return;
            if (helpText != null) helpText.text = text;
            helpPanel.SetActive(true); _idle = 0f;
        }
        private void HideHelp() { if (helpPanel != null) helpPanel.SetActive(false); }
        public void TryAgain() { HideHelp(); ResetIdle(); }
        public void AutoDemo()
        {
            if (!_demoRunning && flow != null) StartCoroutine(DemoRoutine());
        }
        private IEnumerator DemoRoutine()
        {
            _demoRunning = true; HideHelp(); probeDrag?.SetInputLocked(true);
            var stage = flow.CurrentStage;
            if (stage == M2FlowController.Stage.Positioning && probeDrag != null)
            {
                probeDrag.AutoMoveToMm(probeDrag.scanStartMm);
                var slider = probeDrag.angleSlider;
                if (slider == null) { probeDrag.SetAngleSilently(flow.targetAngle); flow.NotifyAngleCorrect(); }
                else for (var t = 0f; t < autoDemoDuration; t += Time.deltaTime) { slider.SetValueWithoutNotify(Mathf.Lerp(slider.value, flow.targetAngle, t / autoDemoDuration)); probeDrag.OnAngleChanged(slider.value); yield return null; }
                if (slider != null) { slider.SetValueWithoutNotify(flow.targetAngle); probeDrag.OnAngleChanged(flow.targetAngle); }
            }
            else if (stage == M2FlowController.Stage.Scanning && probeDrag != null)
            {
                for (float t = 0f, from = probeDrag.CurrentDistanceMm; t < autoDemoDuration; t += Time.deltaTime) { probeDrag.AutoMoveToMm(Mathf.Lerp(from, flow.targetDistance, t / autoDemoDuration)); yield return null; }
                probeDrag.AutoMoveToMm(flow.targetDistance);
            }
            probeDrag?.SetInputLocked(false); _demoRunning = false; ResetIdle();
        }
    }
}
