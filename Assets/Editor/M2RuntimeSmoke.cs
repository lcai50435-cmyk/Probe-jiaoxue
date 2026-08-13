using System;
using System.Reflection;
using M1;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace M2.EditorTools
{
    /// <summary>隔离副本 Play Mode 验收器；只驱动公开运行时 API，不保存 Scene。</summary>
    [InitializeOnLoad]
    public static class M2RuntimeSmoke
    {
        private const string ScenePath = "Assets/Settings/Scenes/M2.unity";
        private const string PendingKey = "M2RuntimeSmoke.Pending";
        private const string LinkKey = "M2RuntimeSmoke.Link";
        private static double _nextAt;
        private static int _step;
        private static M2FlowController _flow;
        private static M1QAPanel _qa;
        private static Vector3 _rulerStartWorld;
        private static Vector2 _rulerStartSize;

        static M2RuntimeSmoke()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        public static void RunBatch()
        {
            if (EditorApplication.isPlaying) return;
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            SessionState.SetBool(PendingKey, true);
            SessionState.SetBool(LinkKey, false);
            EditorApplication.EnterPlaymode();
        }

        public static void RunM1ToM2Batch()
        {
            if (EditorApplication.isPlaying) return;
            EditorSceneManager.OpenScene("Assets/Settings/Scenes/M1.unity", OpenSceneMode.Single);
            SessionState.SetBool(PendingKey, true);
            SessionState.SetBool(LinkKey, true);
            EditorApplication.EnterPlaymode();
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(PendingKey, false)) return;
            try
            {
                if (SessionState.GetBool(LinkKey, false))
                {
                    var selection = UnityEngine.Object.FindFirstObjectByType<M1ToolSelection>();
                    Require(selection != null && selection.nextSceneName == "M2", "M1 下一场景配置错误");
                    var qa = UnityEngine.Object.FindFirstObjectByType<M1QAPanel>();
                    Require(qa != null && qa.deepSeekClient != null, "M1 QA 引用回归");
                    typeof(M1ToolSelection).GetMethod("OnStartClicked", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(selection, null);
                    _step = -1;
                    _nextAt = EditorApplication.timeSinceStartup + 1.5;
                    EditorApplication.update += Tick;
                    return;
                }
                _flow = UnityEngine.Object.FindFirstObjectByType<M2FlowController>();
                _qa = UnityEngine.Object.FindFirstObjectByType<M1QAPanel>();
                Require(_flow != null, "缺少 M2FlowController");
                Require(_qa != null, "缺少 M1QAPanel");
                Require(_flow.rulerDrag != null && _flow.rulerDrag.rulerRt.gameObject.activeSelf, "正式尺子初态不可见");
                Require(!_flow.rulerDrag.unlocked, "正式尺子初态未锁定");
                Require(_flow.rulerDrag.rulerHome != null && _flow.rulerDrag.rulerRt.parent == _flow.rulerDrag.rulerHome,
                    "正式尺子初态未归入 RulerHome");
                Require(_flow.rulerDrag.rulerRt.GetSiblingIndex() == _flow.rulerDrag.rulerHome.childCount - 1,
                    "正式尺子不是 RulerHome 内最高渲染层");
                Require(_flow.rulerDrag.rulerRt.anchorMin == new Vector2(.5f, .5f) &&
                    _flow.rulerDrag.rulerRt.anchorMax == new Vector2(.5f, .5f),
                    "正式尺子初态锚点与 Scene 不一致");
                _rulerStartWorld = _flow.rulerDrag.rulerRt.position;
                _rulerStartSize = _flow.rulerDrag.rulerRt.sizeDelta;
                Require(_flow.rulerDrag.rulerImage != null && _flow.rulerDrag.rulerImage.sprite != null, "正式尺子 Sprite/引用缺失");
                var rulerRect = _flow.rulerDrag.rulerRt.rect;
                var spriteRect = _flow.rulerDrag.rulerImage.sprite.rect;
                var renderedWidth = Mathf.Min(rulerRect.width, rulerRect.height * spriteRect.width / spriteRect.height);
                Require(Mathf.Abs(_flow.rulerDrag.zeroAnchorLocal.x - (rulerRect.center.x - renderedWidth * .5f)) < .01f,
                    "尺子零点未对齐实际渲染图像左缘");
                Require(_flow.CurrentStage == M2FlowController.Stage.Couplant, "初始阶段错误");
                _step = 0;
                _nextAt = EditorApplication.timeSinceStartup + .2;
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
                    case -1:
                        Require(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "M2", "M1 开始探测未加载 M2");
                        _flow = UnityEngine.Object.FindFirstObjectByType<M2FlowController>();
                        Require(_flow != null && _flow.CurrentStage == M2FlowController.Stage.Couplant, "串联 M2 初态错误");
                        Require(_flow.rulerDrag.rulerRt.gameObject.activeSelf && !_flow.rulerDrag.unlocked, "串联 M2 尺子初态错误");
                        Pass("M1→M2 实际场景加载与 M2 初态通过。");
                        break;
                    case 0:
                        var before = Time.timeScale;
                        _qa.Open();
                        Require(Time.timeScale == 0f, "QA 打开未暂停游戏");
                        Require(GameObject.Find("QALayer") != null, "QALayer 缺失");
                        _qa.Close();
                        Require(Mathf.Approximately(Time.timeScale, before), "QA 关闭未恢复 timeScale");
                        _flow.couplantAnimDuration = 0f;
                        _flow.ApplyCouplant();
                        _nextAt = EditorApplication.timeSinceStartup + .4;
                        break;
                    case 1:
                        Require(_flow.CurrentStage == M2FlowController.Stage.Positioning, "耦合剂后未进入定位阶段");
                        _flow.probeDrag.AutoMoveToMm(150f);
                        _flow.probeDrag.OnAngleChanged(10f);
                        Require(_flow.CurrentStage == M2FlowController.Stage.Scanning, "10 度后未进入扫描阶段");
                        Require(Mathf.Abs(_flow.probeDrag.probeVisual.localEulerAngles.z - 10f) < .1f, "探头 10 度视觉反馈错误");
                        Require(Mathf.Abs(_flow.probeDrag.beamLine.localEulerAngles.z - 10f) < .1f, "声束 10 度反馈错误");
                        _flow.SetPerspectiveView();
                        Require(_flow.beamLayer.activeSelf && _flow.railPerspective.activeSelf, "透视模式未显示声束/透明钢轨");
                        _flow.probeDrag.AutoMoveToMm(100f);
                        Require(_flow.Detected, "跨越 110mm 未检出");
                        Require(Mathf.Abs(_flow.probeDrag.CurrentDistanceMm - 110f) < .01f, "首次跨越未钳在 110mm 峰值位置");
                        Require(Mathf.Abs(Mathf.InverseLerp(_flow.probeDrag.scanStartLocal.x, _flow.probeDrag.scanEndLocal.x,
                            _flow.probeDrag.probeRt.anchorMin.x) - .8f) < .001f, "110mm 未处于 80% 线性进度");
                        _flow.probeDrag.AutoMoveToMm(100f);
                        Require(_flow.Detected && Mathf.Abs(_flow.probeDrag.CurrentDistanceMm - 100f) < .01f, "越过峰值后状态错误");
                        _flow.NextToMeasure();
                        Require(_flow.CurrentStage == M2FlowController.Stage.Measuring && _flow.rulerDrag.unlocked, "尺子阶段未解锁");
                        Require(_flow.rulerDrag.rulerRt.parent == _flow.rulerDrag.railViewport &&
                            Vector2.Distance(_flow.rulerDrag.rulerRt.sizeDelta, _flow.rulerDrag.measureSize) < .01f,
                            "尺子测量态父级/尺寸错误");
                        AlignRuler();
                        Require(_flow.CurrentStage == M2FlowController.Stage.Completed && _flow.Measured, "尺子吸附后未完成");
                        _flow.ShowResetDialog();
                        _flow.HideResetDialog();
                        _flow.ResetAll();
                        Require(_flow.CurrentStage == M2FlowController.Stage.Couplant, "重置未回初态");
                        Require(_flow.rulerDrag.rulerRt.gameObject.activeSelf && !_flow.rulerDrag.unlocked &&
                            _flow.rulerDrag.rulerRt.parent == _flow.rulerDrag.rulerHome, "重置后尺子状态错误");
                        Require(Vector3.Distance(_flow.rulerDrag.rulerRt.position, _rulerStartWorld) < .01f &&
                            Vector2.Distance(_flow.rulerDrag.rulerRt.sizeDelta, _rulerStartSize) < .01f,
                            "重置后尺子未回到 Scene 起始布局");
                        Require(Mathf.Abs(_flow.probeDrag.probeVisual.localEulerAngles.z) < .1f, "重置后探头角度非 0");
                        Pass("QA 暂停、四阶段、10°反馈、110mm、尺子吸附、重置均通过。");
                        break;
                }
            }
            catch (Exception e) { Fail(e); }
        }

        private static void AlignRuler()
        {
            var drag = _flow.rulerDrag;
            var weld = drag.railViewport.InverseTransformPoint(drag.weldLineRt.position);
            drag.rulerRt.anchoredPosition = new Vector2(weld.x - drag.zeroAnchorLocal.x, weld.y - drag.zeroAnchorLocal.y);
            typeof(M2RulerDrag).GetMethod("CheckAlign", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(drag, null);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("[M2RuntimeSmoke] " + message);
        }

        private static void Pass(string detail)
        {
            Cleanup();
            Debug.Log("[M2RuntimeSmoke] PASS：" + detail);
            EditorApplication.ExitPlaymode();
            EditorApplication.delayCall += () => EditorApplication.Exit(0);
        }

        private static void Fail(Exception e)
        {
            Cleanup();
            Debug.LogException(e);
            if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode();
            EditorApplication.delayCall += () => EditorApplication.Exit(1);
        }

        private static void Cleanup()
        {
            EditorApplication.update -= Tick;
            SessionState.SetBool(PendingKey, false);
            SessionState.SetBool(LinkKey, false);
            Time.timeScale = 1f;
        }
    }
}
