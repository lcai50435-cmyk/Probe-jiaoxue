using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace M3
{
    /// <summary>防卡死帮助：只读 Flow 阶段并编排自动演示（160→120mm）。</summary>
    public class M3IdleHelp : MonoBehaviour
    {
        public M3FlowController flow;
        public GameObject helpPanel;
        public TMP_Text helpText;
        public Button autoDemoButton, tryAgainButton;
        public M3ProbeDrag probeDrag;
        public float angleIdleTimeout = 30f, scanIdleTimeout = 60f, autoDemoDuration = 1f;
        private float _idle;
        private bool _paused, _demoRunning;

        private void Awake()
        {
            Bind(autoDemoButton, AutoDemo);
            Bind(tryAgainButton, TryAgain);
        }
        private static void Bind(Button button, UnityAction action) { if (button == null) return; button.onClick.RemoveListener(action); button.onClick.AddListener(action); }
        private void Update()
        {
            if (_paused || _demoRunning || flow == null) return;
            _idle += Time.deltaTime;
            if (flow.CurrentStage == M3FlowController.Stage.Positioning && _idle >= angleIdleTimeout) ShowHelp("需要帮助调整到向下 13° 吗？");
            else if (flow.CurrentStage == M3FlowController.Stage.Scanning && _idle >= scanIdleTimeout) ShowHelp("即将演示移动到 120mm 检出");
        }
        public void ResetIdle() { _idle = 0f; HideHelp(); }
        public void SetPaused(bool paused) => _paused = paused;
        public void ResetAll() { StopAllCoroutines(); _demoRunning = false; _idle = 0f; HideHelp(); probeDrag?.SetInputLocked(false); }
        private void ShowHelp(string text) { if (helpPanel == null || helpPanel.activeSelf) return; if (helpText != null) helpText.text = text; helpPanel.SetActive(true); _idle = 0f; }
        private void HideHelp() { if (helpPanel != null) helpPanel.SetActive(false); }
        public void TryAgain() { HideHelp(); ResetIdle(); }
        public void AutoDemo() { if (!_demoRunning && flow != null) StartCoroutine(DemoRoutine()); }
        private IEnumerator DemoRoutine()
        {
            _demoRunning = true; HideHelp(); probeDrag?.SetInputLocked(true);
            var stage = flow.CurrentStage;
            if (stage == M3FlowController.Stage.Positioning && probeDrag != null)
            {
                probeDrag.AutoMoveToMm(probeDrag.scanStartMm); // 放探头（0°）
                flow.rulerDrag?.AutoPosition(); // 尺子中心吸白色点 → 解锁角度滑块
                var slider = probeDrag.angleSlider;
                if (slider == null) probeDrag.OnAngleChanged(flow.targetAngle);
                else for (var t = 0f; t < autoDemoDuration; t += Time.deltaTime) { slider.SetValueWithoutNotify(Mathf.Lerp(slider.value, flow.targetAngle, t / autoDemoDuration)); probeDrag.OnAngleChanged(slider.value); yield return null; }
                if (slider != null) { slider.SetValueWithoutNotify(flow.targetAngle); probeDrag.OnAngleChanged(flow.targetAngle); }
                yield return new WaitForSeconds(1f); // 等 0.5s 稳定确认校角
                flow.rulerDrag?.AutoRetract(); // 撤尺归槽（恢复 Home 初态）→ 进入扫描
            }
            else if (stage == M3FlowController.Stage.Scanning && probeDrag != null)
            {
                for (float t = 0f, from = probeDrag.CurrentDistanceMm; t < autoDemoDuration; t += Time.deltaTime) { probeDrag.AutoMoveToMm(Mathf.Lerp(from, flow.targetDistance, t / autoDemoDuration)); yield return null; }
                probeDrag.AutoMoveToMm(flow.targetDistance);
            }
            if (flow != null)
            {
                probeDrag?.SetInputLocked(flow.Detected);
                if (flow.CurrentStage != M3FlowController.Stage.Positioning) probeDrag?.SetAngleLocked(true); // 扫描后角度锁定
            }
            _demoRunning = false; ResetIdle();
        }
    }
}
