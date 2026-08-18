using System;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using M2;
using M4;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace M4.EditorTools
{
    /// <summary>隔离副本 Play Mode 验收器；只驱动公开运行时 API，不保存 Scene；退出时校验 Scene 哈希不变。
    /// 2026-08-17 M4（M3 复制基线）：55→40mm、M2WaveformFx（55/45/40）、40mm 双点测量、检出锁定、伤损变橙；无 Intro/耦合剂；检出即直接进入测距。</summary>
    [InitializeOnLoad]
    public static class M4RuntimeSmoke
    {
        private const string ScenePath = "Assets/Settings/Scenes/M4.unity";
        private const string RequestPath = "Temp/M4RuntimeSmoke.request";
        private const string PendingKey = "M4RuntimeSmoke.Pending";
        private const string HashKey = "M4RuntimeSmoke.SceneHash";
        private static double _nextAt;
        private static int _step;
        private static M4FlowController _flow;
        private static M4ProbeDrag _probe;
        private static M4RulerDrag _ruler;
        private static M2WaveformFx _waveFx;
        private static string _sceneHash;
        private static Vector3 _probeStartWorld;
        private static Vector2 _probeStartPos, _scanStart;

        static M4RuntimeSmoke()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            if (File.Exists(RequestPath)) { File.Delete(RequestPath); EditorApplication.delayCall += RunBatch; }
        }

        [MenuItem("Tools/M4/Runtime Smoke (Play Mode) %#&9")]
        public static void RunBatch()
        {
            if (EditorApplication.isPlaying) return;
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            _sceneHash = ComputeHash(ScenePath);
            SessionState.SetString(HashKey, _sceneHash);
            SessionState.SetBool(PendingKey, true);
            EditorApplication.EnterPlaymode();
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode)
            {
                var expected = SessionState.GetString(HashKey, "");
                if (expected.Length > 0 && ComputeHash(ScenePath) != expected)
                    Debug.LogError("[M4RuntimeSmoke] FAIL：M4 Scene 哈希已变化。");
                _sceneHash = null;
            }
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(PendingKey, false)) return;
            try
            {
                _sceneHash = SessionState.GetString(HashKey, "");
                Require(_sceneHash.Length > 0, "缺少冻结 Scene 哈希基线");
                _flow = UnityEngine.Object.FindFirstObjectByType<M4FlowController>();
                Require(_flow != null, "缺少 M4FlowController");
                _probe = _flow.probeDrag; _ruler = _flow.rulerDrag;
                Require(_probe != null && _ruler != null, "probeDrag/rulerDrag 引用缺失");
                Require(_flow.CurrentStage == M4FlowController.Stage.Positioning, "初始阶段错误");
                Require(!_flow.beamLayer.activeSelf, "进入 Play 时 BeamLayer/射线不应提前显示");
                if (_flow.couplantOverlay != null) Require(!_flow.couplantOverlay.gameObject.activeSelf, "初始不应展示耦合剂薄膜");
                Require(_probe.probeVisual != null && _probe.probeVisual.GetComponent<Image>().raycastTarget, "探头射线未恢复");
                Require(_ruler.rulerImage != null && _ruler.rulerImage.raycastTarget, "尺子射线未恢复");
                Require(_ruler.rulerImage.sprite != null, "尺子未替换正式尺素材");
                Require(_probe.angleSlider != null && !_probe.angleSlider.interactable, "初始角度滑块应锁定（尺子校角吸附后才可调）");
                Require(_probe.unlocked, "初始探头未解锁");
                Require(_ruler.unlocked && !_flow.PositioningRulerInPlace, "初始定位尺应可拖拽且未到位");
                _waveFx = _flow.waveformFx;
                Require(_waveFx != null, "M4 未挂载 M2WaveformFx");
                Require(Mathf.Abs(_waveFx.appearMm - 55f) < .01f && Mathf.Abs(_waveFx.peakMm - 45f) < .01f && Mathf.Abs(_waveFx.stopMm - 40f) < .01f, "波形参数未按 PPT 设置");
                _probeStartWorld = _probe.probeRt.position; _probeStartPos = _probe.probeRt.anchoredPosition; _scanStart = _probe.ScanStartLocal;
                _step = 0; _nextAt = EditorApplication.timeSinceStartup + .2;
                EditorApplication.update += Tick;
            }
            catch (Exception e) { Fail(e); }
        }

        private static void Tick()
        {
            if (EditorApplication.timeSinceStartup < _nextAt) return;
            try
            {
                switch (_step++)
                {
                    case 0:
                        _nextAt = EditorApplication.timeSinceStartup + .2;
                        break;
                    case 1:
                        Require(_flow.CurrentStage == M4FlowController.Stage.Positioning, "初始阶段错误");
                        if (_flow.couplantOverlay != null) Require(!_flow.couplantOverlay.gameObject.activeSelf, "耦合剂薄膜未隐藏");
                        Require(_probe.unlocked && !_probe.angleSlider.interactable, "初始探头应解锁、角度滑块应锁定");
                        Require(_ruler.unlocked && !_ruler.positioned && _ruler.rulerRt.parent == _ruler.rulerHome, "初始定位尺应留在 RulerHome 且可拖拽");
                        Require(Mathf.Abs(Mathf.DeltaAngle(_probe.probeVisual.localEulerAngles.z, _probe.probeBaseAngleDeg)) < .1f, "初始探头未保持平放基准角（bg z=15）");
                        _flow.resetButton.onClick.Invoke();
                        Require(!_probe.angleSlider.interactable && !_ruler.unlocked, "重置按钮未打开模态锁定");
                        Click("CancelButton");
                        Require(_probe.unlocked && _ruler.unlocked && !_probe.angleSlider.interactable, "关闭重置对话框后角度滑块应保持锁定");
                        Require(Vector2.Distance(_probe.ScanStartLocal, _scanStart) < .001f, "扫描起点在初始定位后发生漂移");
                        _probe.AutoMoveToMm(55f); // 放探头（0°）
                        Require(_flow.CurrentStage == M4FlowController.Stage.Positioning, "仅探头就位不应进入扫描");
                        _probe.OnAngleChanged(10f);
                        Require(_flow.CurrentStage == M4FlowController.Stage.Positioning, "尺子未吸附时角度正确仍不应进入扫描");
                        _ruler.AutoPosition(); // 尺子中心吸白色点 → 解锁角度滑块
                        Require(_ruler.rulerRt.parent == _ruler.railViewport, "定位尺未进入 RailViewport");
                        Require(_probe.angleSlider.interactable, "尺子吸附后角度滑块应解锁");
                        Require(_flow.RulerDocked && !_flow.AngleVerifiedByRuler, "尺子吸附后 RulerDocked 应为真、校角未确认");
                        _probe.OnAngleChanged(10f); // 重新触发稳定计时（滑块从 0 动画到 13 后停住）
                        _nextAt = EditorApplication.timeSinceStartup + 1f;
                        break;
                    case 2: // 等待 0.5s 稳定确认
                        Require(_flow.AngleVerifiedByRuler, "13° 稳定 0.5s 后校角未确认");
                        Require(!_probe.angleSlider.interactable, "校角确认后角度滑块应锁定");
                        Require(_flow.CurrentStage == M4FlowController.Stage.Positioning, "校角确认后不应直接进入扫描（需撤尺）");
                        _ruler.AutoRetract(); // 撤尺归槽 → 进入扫描
                        Require(_flow.CurrentStage == M4FlowController.Stage.Scanning, "撤尺后未进入扫描");
                        Require(_ruler.rulerRt.parent == _ruler.rulerHome && !_ruler.positioned, "撤尺后尺子未归槽 Home");
                        Require(Mathf.Abs(Mathf.DeltaAngle(_probe.probeVisual.localEulerAngles.z, _probe.probeBaseAngleDeg + _probe.visualTiltAtTarget)) < .1f, "探头 10° 视觉反馈错误");
                        Require(Mathf.Abs(Mathf.DeltaAngle(_probe.beamLine.localEulerAngles.z, 10f)) < .1f, "入射声束角度错误");
                        _flow.perspectiveBtnImg.GetComponent<Button>().onClick.Invoke();
                        Require(_flow.beamLayer.activeSelf && _flow.railPerspective.activeSelf && !_flow.railBg.gameObject.activeSelf, "透视按钮未切换显示");
                        Require(_flow.CurrentStage == M4FlowController.Stage.Scanning, "视图切换改变流程状态");
                        // 波形三态：160 短波 / 123 最高 / 120 锁定
                        _waveFx.SetDistanceMm(55f);
                        Require(Mathf.Abs(_waveFx.Strength - .08f) < .02f && Mathf.Abs(_waveFx.PeakU - .275f) < .01f, "55mm 短波状态错误");
                        _waveFx.SetDistanceMm(45f);
                        Require(Mathf.Abs(_waveFx.Strength - .78f) < .02f && Mathf.Abs(_waveFx.PeakU - .225f) < .01f, "45mm 最高波状态错误");
                        _waveFx.SetDistanceMm(40f);
                        Require(Mathf.Abs(_waveFx.Strength - .78f) < .02f && Mathf.Abs(_waveFx.PeakU - .2f) < .01f, "40mm 停止波状态错误");
                        _nextAt = EditorApplication.timeSinceStartup + .2f;
                        break;
                    case 3:
                        // 检出：从扫描起点按 0.5mm 步进推进，射线末端进入红椭圆区域（椭圆判定）即检出；不跳终点（会错过触发点）
                        for (var mm = _probe.scanStartMm; mm >= _probe.scanEndMm && !_flow.Detected; mm -= .5f)
                            _probe.AutoMoveToMm(mm);
                        Require(_flow.Detected, $"未检出：BeamHit={_probe.BeamHitsDamage} AngleCorrect={_probe.AngleCorrect} 距离={_probe.CurrentDistanceMm:F1}");
                        Require(_probe.CurrentDistanceMm < _probe.scanStartMm, "检出未发生在扫描推进过程中");
                        Require(_flow.damageMarker.activeSelf, "检出后伤损标记（DamageMarker）应显示为橙色");
                        var dmgColor = _flow.damageMarker.GetComponent<Image>().color;
                        Require(dmgColor.a > .3f, "伤损标记未变橙");
                        Require(!_flow.detectionBanner.activeSelf, "检出后 DetectionBanner 应保持隐藏");
                        Require(_flow.CurrentStage == M4FlowController.Stage.Measuring, "检出后应直接进入测距（无需下一步门控）");
                        Require(_ruler.unlocked && _ruler.rulerRt.parent == _ruler.railViewport, "检出后尺子应直接出架进测量");
                        var lockedPos = _probe.probeRt.anchoredPosition;
                        _probe.AutoMoveToMm(_probe.scanEndMm);
                        Require(_probe.probeRt.anchoredPosition == lockedPos, "检出后探头未锁定");
                        _nextAt = EditorApplication.timeSinceStartup + .2f;
                        break;
                    case 4:
                        AlignRuler();
                        Require(_flow.CurrentStage == M4FlowController.Stage.Completed && _flow.Measured, "尺子吸附后未完成");
                        Require(_flow.completionText.text.Contains("下一模块待接入"), "M4 出口文案错误");
                        _flow.ResetAll();
                        Require(_flow.CurrentStage == M4FlowController.Stage.Positioning, "重置未回定位阶段");
                        if (_flow.couplantOverlay != null) Require(!_flow.couplantOverlay.gameObject.activeSelf, "重置后耦合剂薄膜未隐藏");
                        Require(_probe.unlocked && !_probe.angleSlider.interactable, "重置后探头应解锁、角度滑块应锁定");
                        Require(_ruler.unlocked, "重置后尺子未解锁");
                        Require(!_flow.damageMarker.activeSelf, "重置后 DamageMarker 未隐藏");
                        Require(!_flow.beamLayer.activeSelf, "重置后 BeamLayer/射线未隐藏");
                        Require(_ruler.rulerRt.parent == _ruler.rulerHome, "重置后尺子未回架");
                        Require(Vector3.Distance(_probe.probeRt.position, _probeStartWorld) < .01f && Vector2.Distance(_probe.probeRt.anchoredPosition, _probeStartPos) < .01f, "重置后探头未回 Scene 初态");
                        Require(Mathf.Abs(Mathf.DeltaAngle(_probe.probeVisual.localEulerAngles.z, _probe.probeBaseAngleDeg)) < .1f, "重置后探头未保持平放基准角（bg z=15）");
                        Pass("放探头→尺子中心吸白色点→10°稳定确认→撤尺→射线照伤损检出→直接测距→双点测距→重置 全链路通过。");
                        break;
                }
            }
            catch (Exception e) { Fail(e); }
        }

        private static void Click(string name)
        {
            foreach (var button in _flow.GetComponentsInChildren<Button>(true))
                if (button.name == name) { button.onClick.Invoke(); return; }
            throw new InvalidOperationException("[M4RuntimeSmoke] 缺少按钮：" + name);
        }

        private static void AlignRuler()
        {
            var drag = _ruler;
            var anchor = _probe.ZeroAnchorWorld; // 0 刻度对齐探头 zero 锚点中心（老板合同：zero↔尺子 0 刻度、伤损↔40mm 刻度）
            drag.rulerRt.anchoredPosition = anchor - drag.ZeroAnchorLocal;
            typeof(M4RulerDrag).GetMethod("CheckAlign", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(drag, null);
        }

        private static string ComputeHash(string path)
        {
            using var sha = SHA256.Create();
            using var fs = File.OpenRead(path);
            return BitConverter.ToString(sha.ComputeHash(fs)).Replace("-", "");
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("[M4RuntimeSmoke] " + message);
        }

        private static void Pass(string detail)
        {
            Cleanup();
            if (ComputeHash(ScenePath) != _sceneHash) { Fail(new InvalidOperationException("[M4RuntimeSmoke] Scene 哈希变化")); return; }
            Debug.Log("[M4RuntimeSmoke] PASS：" + detail);
            EditorApplication.ExitPlaymode();
            if (Application.isBatchMode) EditorApplication.delayCall += () => EditorApplication.Exit(0);
        }

        private static void Fail(Exception e)
        {
            Cleanup();
            Debug.LogException(e);
            if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode();
            if (Application.isBatchMode) EditorApplication.delayCall += () => EditorApplication.Exit(1);
        }

        private static void Cleanup()
        {
            EditorApplication.update -= Tick;
            SessionState.SetBool(PendingKey, false);
            Time.timeScale = 1f;
        }
    }
}
