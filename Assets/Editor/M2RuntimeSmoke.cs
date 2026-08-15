using System;
using System.Reflection;
using M1;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

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
        private static double _linkDeadline;
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
                    _linkDeadline = EditorApplication.timeSinceStartup + 15;
                    _nextAt = EditorApplication.timeSinceStartup + .5;
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
                Require(_flow.rulerDrag.rulerImage != null && _flow.rulerDrag.rulerImage.sprite != null, "正式尺 Sprite/引用缺失");
                var rulerSprites = Resources.LoadAll<Sprite>("尺子正面");
                Require(rulerSprites != null && rulerSprites.Length > 0 && _flow.rulerDrag.rulerImage.sprite == rulerSprites[0], "正式尺未换成尺子正面素材");
                Require(Mathf.Abs(_flow.rulerDrag.PixelsPerMm - 2.883f) < .05f, "尺子 0→110 标定比例错误"); // 待老板确认 Scene 锚点 ruler110Uv=0.76 为最终校准
                Require(Mathf.Abs(_flow.rulerDrag.PixelsPerMm * 110f - 317.1f) < 2f, "尺子 0→110 跨度错误");
                Require(Mathf.Abs(_flow.rulerDrag.zeroUv.y - _flow.rulerDrag.ruler110Uv.y) < .001f && _flow.rulerDrag.zeroUv.y < .1f, "0/110 锚点未位于同一可见底边基线");
                var probeImage = _flow.probeDrag.probeVisual != null ? _flow.probeDrag.probeVisual.GetComponent<Image>() : null;
                var probeSprites = Resources.LoadAll<Sprite>("probeFootage");
                Require(probeImage != null && probeImage.sprite != null && probeSprites != null && probeSprites.Length > 0 && probeImage.sprite == probeSprites[0], "探头未换成 probeFootage 素材");
                var railNormal = Resources.LoadAll<Sprite>("俯视角");
                var railPersp = Resources.LoadAll<Sprite>("俯视角透视");
                Require(railNormal != null && railNormal.Length > 0 && _flow.railBg.GetComponentInChildren<Image>(true).sprite == railNormal[0], "钢轨普通视图未换成俯视角 v2");
                Require(railPersp != null && railPersp.Length > 0 && _flow.railPerspective.GetComponentInChildren<Image>(true).sprite == railPersp[0], "钢轨透视视图未换成俯视角透视 v2");
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
                        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "M2")
                        {
                            if (EditorApplication.timeSinceStartup > _linkDeadline) throw new InvalidOperationException("[M2RuntimeSmoke] M1 开始探测超时未加载 M2");
                            _step = -1; _nextAt = EditorApplication.timeSinceStartup + .5;
                            return;
                        }
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
                        _flow.ShowResetDialog(); Require(Time.timeScale == 0f, "重置确认未暂停游戏");
                        _flow.HideResetDialog(); Require(Mathf.Approximately(Time.timeScale, before), "重置确认关闭未恢复 timeScale");
                        _flow.couplantFx.animDuration = _flow.couplantFx.holdDuration = _flow.couplantFx.fadeDuration = 0f;
                        _flow.ApplyCouplant();
                        _nextAt = EditorApplication.timeSinceStartup + .4;
                        break;
                    case 1:
                        Require(_flow.CurrentStage == M2FlowController.Stage.Positioning, "耦合剂后未进入定位阶段");
                        var fx = _flow.couplantFx;
                        Require(fx != null && fx.film != null && fx.film.sprite != null, "薄膜未设置铁轨形状 sprite");
                        Require(fx.film.type == Image.Type.Filled && fx.film.fillMethod == Image.FillMethod.Horizontal && fx.film.fillOrigin == 0, "薄膜未配置从左至右揭示");
                        Require(fx.film.color.a < 1f, "薄膜应为半透明蓝");
                        Require(Mathf.Abs(fx.maskRt.sizeDelta.x - _flow.railBg.sizeDelta.x * fx.coverRect.z) < 1f &&
                            Mathf.Abs(fx.maskRt.sizeDelta.y - _flow.railBg.sizeDelta.y * fx.coverRect.w) < 1f, "薄膜覆盖区域与 coverRect 不符");
                        Require(fx.film.fillAmount >= .99f, "动画完成后薄膜未铺满");
                        var probe = _flow.probeDrag;
                        probe.PlaceAtStart();                           // 钢轨左侧中心线 0° 放置
                        Require(probe.Placed, "探头未完成放置");
                        Require(!probe.angleSlider.interactable, "放置后 Slider 应锁定（尺子吸附前）");
                        probe.OnAngleChanged(10f);
                        Require(_flow.CurrentStage == M2FlowController.Stage.Positioning, "无夹具时 Slider 单独 10° 不应进入扫描");
                        _flow.rulerDrag.ShowAngleGuide();
                        Require(Vector2.Distance(_flow.rulerDrag.rulerRt.sizeDelta, _flow.rulerDrag.measureSize) < .01f, "校角态尺子尺寸未统一为 measureSize（PPT 两步骤同尺寸）");
                        _flow.rulerDrag.SetPoseAngleGuide();            // 摆到夹具姿态
                        _flow.rulerDrag.CheckAngleGuide();              // 几何吸附成夹具
                        Require(_flow.RulerDocked, "尺子未吸附成夹具");
                        Require(_flow.CurrentStage == M2FlowController.Stage.Positioning, "夹具吸附后不应直接进入扫描");
                        Require(_flow.rulerDrag.rulerRt.parent == _flow.rulerDrag.railViewport, "吸附后尺子应保留现场");
                        Require(probe.angleSlider.interactable, "吸附后 Slider 应解锁");
                        Require(probe.AngleCorrect, "角度未保持 10°");
                        _nextAt = EditorApplication.timeSinceStartup + .7f; // 等 10° 稳定 0.5s
                        break;
                    case 2:
                        Require(_flow.AngleVerifiedByRuler, "10° 稳定未确认");
                        Require(!_flow.probeDrag.angleSlider.interactable, "确认后 Slider 未锁定");
                        _flow.rulerDrag.UnlockRetract();
                        _flow.rulerDrag.SetPoseRetract();               // 拖回工具架
                        _flow.rulerDrag.CheckRetract();                 // 撤尺
                        Require(_flow.CurrentStage == M2FlowController.Stage.Scanning, "撤尺后未进入扫描");
                        Require(_flow.rulerDrag.rulerRt.parent == _flow.rulerDrag.rulerHome, "撤尺后尺子未归槽");
                        Require(Mathf.Abs(_flow.probeDrag.probeVisual.localEulerAngles.z - (_flow.probeDrag.probeBaseAngleDeg + 10f)) < .1f, "探头 10° 视觉反馈错误");
                        var ppm = _flow.rulerDrag.PixelsPerMm;
                        var damage = _flow.probeDrag.DamagePointInRail;
                        var entry0 = _flow.probeDrag.ProbeEntryPointInRail;
                        Require(Mathf.Abs(entry0.y - damage.y) < 1f, "150mm 起点入射点与损伤未同线（PPT 尺子水平前提）");
                        Require(entry0.x < damage.x, "起始位置应在红色损伤左侧");
                        _flow.SetPerspectiveView();
                        Require(_flow.railPerspective.activeSelf, "透视模式未显示透明钢轨");
                        Require(_flow.beamLayer.activeSelf, "扫描阶段检测束层未激活");
                        var initialBeam = _flow.probeDrag.beamLine.GetComponentInChildren<Image>();
                        Require(initialBeam != null && initialBeam.sprite != null && _flow.probeDrag.beamLine.sizeDelta.x <= 16f, "扫描首帧仍显示旧粗矩形束");
                        _flow.SetNormalView();
                        Require(_flow.beamLayer.activeSelf, "普通视图检测束层被错误隐藏");
                        _flow.probeDrag.AutoMoveToMm(110f);             // 几何路径到 110mm
                        Require(_flow.Detected, "110mm 几何位置未检出");
                        var entry = _flow.probeDrag.ProbeEntryPointInRail;
                        Require(Mathf.Abs(Vector2.Distance(entry, damage) - 110f * ppm) < 1f, "110mm 检出间距错误（应距红色损伤）");
                        Require(Vector2.Distance(entry, damage) > 80f, "110mm 时探头入射点与损伤不应重合");
                        Require(Mathf.Abs(entry.y - damage.y) < 1f, "110mm 检出点与损伤未同线");
                        Require(_flow.waveStateText != null && !_flow.waveStateText.gameObject.activeSelf, "波形状态提示未隐藏（PPT 删提示词）");
                        Require(_flow.currentDistanceText != null && !_flow.currentDistanceText.gameObject.activeSelf, "波形距离读数未隐藏");
                        Require(_flow.waveformFx != null && _flow.waveform != null && !_flow.waveform.enabled, "新波形组件未挂载或旧组件未禁用");
                        var areaRt = _flow.waveform.transform.parent as RectTransform;
                        Require(areaRt != null && Mathf.Abs(areaRt.sizeDelta.x - 460f) < 1f && Mathf.Abs(areaRt.sizeDelta.y - 345f) < 1f, "波形窗口未改 4:3（460×345）");
                        Require(areaRt != null && Mathf.Abs(areaRt.anchoredPosition.y - 172.5f) < 1f, "波形窗口下缘未贴屏幕底");
                        var wfx = _flow.waveformFx;
                        wfx.SetDistanceMm(150f);
                        Require(Mathf.Abs(wfx.Strength - .08f) < .02f && Mathf.Abs(wfx.PeakU - .75f) < .01f, "150mm 短波初态错误（Strength/PeakU）");
                        wfx.SetDistanceMm(115f);
                        Require(Mathf.Abs(wfx.Strength - .78f) < .02f && Mathf.Abs(wfx.PeakU - .575f) < .01f, "115mm 最高波状态错误");
                        wfx.SetDistanceMm(110f);
                        Require(Mathf.Abs(wfx.Strength - .78f) < .02f && Mathf.Abs(wfx.PeakU - .55f) < .01f, "110mm 检出波状态错误");
                        var lockedStrength = wfx.Strength; var lockedPeakU = wfx.PeakU;
                        wfx.SetDistanceMm(100f);
                        Require(wfx.Strength == lockedStrength && wfx.PeakU == lockedPeakU, "检出后波形应锁定不变");
                        var beamImg = _flow.probeDrag.beamLine.GetComponentInChildren<Image>();
                        Require(beamImg != null && beamImg.sprite != null && beamImg.sprite.rect.height >= 60f, "检测束渐变 Sprite 未绑定（旧粗矩形）");
                        var beamTex = beamImg.sprite.texture; var beamY = beamTex.height / 3;
                        Require(beamTex.GetPixel(beamTex.width / 2, beamY).a > beamTex.GetPixel(0, beamY).a * 4f, "检测束仍是等宽实心矩形");
                        Require(_flow.probeDrag.beamLine.sizeDelta.x <= 16f, "检测束宽度过宽");
                        Require(Vector2.Distance(_flow.probeDrag.beamLine.anchoredPosition, entry) < 1f, "检测束起点不在探头入射点");
                        Require(_flow.nextButton.gameObject.activeSelf, "检出后未显示下一步按钮");
                        var posBefore = _flow.probeDrag.probeRt.anchoredPosition;
                        _flow.probeDrag.AutoMoveToMm(110f);
                        Require(_flow.probeDrag.probeRt.anchoredPosition == posBefore, "检出后探头未锁定");
                        Require(_flow.Detected, "重复触发改变了检出状态");
                        _flow.NextToMeasure();
                        Require(_flow.CurrentStage == M2FlowController.Stage.Measuring && _flow.rulerDrag.unlocked, "尺子测量阶段未解锁");
                        Require(_flow.rulerDrag.rulerRt.parent == _flow.rulerDrag.railViewport &&
                            Vector2.Distance(_flow.rulerDrag.rulerRt.sizeDelta, _flow.rulerDrag.measureSize) < .01f,
                            "尺子测量态父级/尺寸错误");
                        var rz = _flow.rulerDrag.rulerRt.localEulerAngles.z;
                        Require(Mathf.Abs(rz) < .5f, "测量尺未水平放置（当前角度 " + rz + "°）");
                        Require(_flow.rulerDrag.rulerImage != null && Mathf.Abs(_flow.rulerDrag.rulerImage.rectTransform.localScale.y - 1f) < .01f, "尺子工作态 bg 未归一化");
                        break;
                    case 3:
                        var drag = _flow.rulerDrag;
                        drag.rulerRt.localRotation *= Quaternion.Euler(0f, 0f, 180f); // 尺子方向反向：0/110 锚点互换错位
                        drag.CheckMeasure();
                        Require(!_flow.Measured && _flow.CurrentStage == M2FlowController.Stage.Measuring, "尺子反向不应完成测量");
                        drag.SetPoseMeasure(); drag.rulerRt.anchoredPosition += new Vector2(60f, 0f);
                        drag.CheckMeasure();
                        Require(!_flow.Measured, "尺子错位不应完成测量");
                        drag.ShowMeasure(); // 真实入口自动定向，玩家只平移 0mm 到入射点
                        var pointer = new PointerEventData(EventSystem.current) { position = RectTransformUtility.WorldToScreenPoint(null, drag.railViewport.TransformPoint(_flow.probeDrag.ProbeEntryPointInRail)) };
                        drag.OnBeginDrag(pointer); drag.OnDrag(pointer);
                        Require(_flow.Measured && _flow.CurrentStage == M2FlowController.Stage.Completed, "真实拖拽路径未完成双点测量");
                        _flow.ResetAll();
                        Require(_flow.CurrentStage == M2FlowController.Stage.Couplant, "重置未回初态");
                        Require(_flow.rulerDrag.rulerRt.gameObject.activeSelf && !_flow.rulerDrag.unlocked &&
                            _flow.rulerDrag.rulerRt.parent == _flow.rulerDrag.rulerHome, "重置后尺子状态错误");
                        Require(Vector3.Distance(_flow.rulerDrag.rulerRt.position, _rulerStartWorld) < .01f &&
                            Vector2.Distance(_flow.rulerDrag.rulerRt.sizeDelta, _rulerStartSize) < .01f,
                            "重置后尺子未回到 Scene 起始布局");
                        Require(Mathf.Abs(_flow.probeDrag.probeVisual.localEulerAngles.z - _flow.probeDrag.probeBaseAngleDeg) < .1f, "重置后探头角度非 0");
                        Require(_flow.rulerDrag.rulerImage != null && Mathf.Abs(_flow.rulerDrag.rulerImage.rectTransform.localScale.y - 1.3417f) < .01f, "重置后尺子 bg 未恢复 Scene 缩放");
                        Require(!_flow.AngleVerifiedByRuler && !_flow.Detected, "重置后流程状态未清空");
                        break;
                    case 4:
                        // Reset 后完整复跑
                        _flow.couplantFx.animDuration = _flow.couplantFx.holdDuration = _flow.couplantFx.fadeDuration = 0f;
                        _flow.ApplyCouplant();
                        _nextAt = EditorApplication.timeSinceStartup + .4;
                        break;
                    case 5:
                        _flow.idleHelp.AutoDemo();           // 30s 帮助完整演示：放置→10°→尺子校角→归槽
                        _nextAt = EditorApplication.timeSinceStartup + 2.5f;
                        break;
                    case 6:
                        Require(_flow.CurrentStage == M2FlowController.Stage.Scanning, "自动帮助未完成校角门控");
                        Require(_flow.AngleVerifiedByRuler, "自动帮助未记录校角状态");
                        Require(_flow.rulerDrag.rulerRt.parent == _flow.rulerDrag.rulerHome, "自动帮助后尺子未归槽");
                        _flow.probeDrag.AutoMoveToMm(110f);
                        Require(_flow.Detected, "复跑检出失败");
                        _flow.NextToMeasure();
                        _flow.rulerDrag.SetPoseMeasure(); _flow.rulerDrag.CheckMeasure();
                        Require(_flow.Measured && _flow.CurrentStage == M2FlowController.Stage.Completed, "复跑测量失败");
                        Pass("QA 暂停、门控、110mm 几何、检出锁定、双点测量、自动帮助、重置复跑均通过。");
                        break;
                }
            }
            catch (Exception e) { Fail(e); }
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
