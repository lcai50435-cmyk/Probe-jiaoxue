using System;
using M1;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace M5.EditorTools
{
    /// <summary>M5 擦拭耦合剂 Play Mode 验收器；只驱动公开运行时 API，不保存 Scene。</summary>
    [InitializeOnLoad]
    public static class M5RuntimeSmoke
    {
        private const string ScenePath = "Assets/Settings/Scenes/M5.unity";
        private const string PendingKey = "M5RuntimeSmoke.Pending";
        private static double _nextAt;
        private static int _step;
        private static M5FlowController _flow;
        private static M5CouplantFx _fx;
        private static M5RagDrag _rag;
        private static M1QAPanel _qa;
        private static int _failures;

        static M5RuntimeSmoke()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        public static void RunBatch()
        {
            if (EditorApplication.isPlaying) return;
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            SessionState.SetBool(PendingKey, true);
            EditorApplication.EnterPlaymode();
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(PendingKey, false)) return;
            _failures = 0;
            try
            {
                _flow = UnityEngine.Object.FindFirstObjectByType<M5FlowController>();
                _fx = UnityEngine.Object.FindFirstObjectByType<M5CouplantFx>();
                _rag = UnityEngine.Object.FindFirstObjectByType<M5RagDrag>();
                _qa = UnityEngine.Object.FindFirstObjectByType<M1QAPanel>();
                Require(_flow != null, "缺少 M5FlowController");
                Require(_fx != null && _fx.film != null, "缺少 M5CouplantFx/薄膜 Image");
                Require(_rag != null && _rag.ragRt != null, "缺少 M5RagDrag/擦拭布");
                Require(_qa != null, "缺少 M1QAPanel（数字人/问答装配失败）");
                _step = 0;
                _nextAt = EditorApplication.timeSinceStartup + .3;
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
                    case 0: // 初态：耦合剂铺满、擦拭布在工具架置灰锁定、阶段 Wipe
                        Require(Mathf.Approximately(_fx.film.fillAmount, 1f), "初态耦合剂未铺满");
                        Require(_fx.film.type == Image.Type.Filled && _fx.film.fillMethod == Image.FillMethod.Horizontal && _fx.film.fillOrigin == 1, "薄膜未配置右对齐剩余（擦拭方向）");
                        Require(_fx.film.sprite != null, "薄膜未切出铁轨形状 sprite");
                        Require(Mathf.Abs(_fx.maskRt.sizeDelta.x - _flow.railBg.sizeDelta.x * _fx.coverRect.z) < 1f &&
                            Mathf.Abs(_fx.maskRt.sizeDelta.y - _flow.railBg.sizeDelta.y * _fx.coverRect.w) < 1f, "薄膜覆盖区域与 coverRect 不符（轨顶中央大部分）");
                        Require(_rag.ragRt.parent == _rag.ragHome, "擦拭布初态未归入 RagHome");
                        Require(_rag.unlocked, "擦拭布初态可拖（M5 单步交互，置灰仅视觉）");
                        Require(_rag.ragImage != null && _rag.ragImage.sprite != null, "擦拭布 Sprite 缺失（rag.png）");
                        Require(Mathf.Approximately(_rag.ragImage.color.a, 1f), "擦拭布初态清晰（不置灰，老板 2026-08-23）");
                        Require(_flow.CurrentStage == M5FlowController.Stage.Wipe && !_flow.Wiped, "初始阶段错误");
                        Require(_flow.completionPanel == null || !_flow.completionPanel.activeSelf, "完成面板初态不应显示");
                        Pass("初态通过：耦合剂铺满 / 擦拭布归槽锁定 / Wipe 阶段");
                        _nextAt = EditorApplication.timeSinceStartup + .1;
                        break;

                    case 1: // 擦拭进度：拖动回调驱动耦合剂递减（跟手）
                        _rag.Unlock();
                        Require(_rag.unlocked, "解锁失败");
                        _flow.NotifyWipeProgress(.5f);
                        Require(Mathf.Abs(_fx.film.fillAmount - .5f) < .02f, "擦拭 50% 耦合剂应剩 50%");
                        Require(_flow.CurrentStage == M5FlowController.Stage.Wipe && !_flow.Wiped, "50% 不应完成");
                        _flow.NotifyWipeProgress(0f);
                        Require(Mathf.Abs(_fx.film.fillAmount - 1f) < .02f, "回拖 0% 耦合剂应恢复铺满（进度双向）");
                        // 视图切换：透视隐藏耦合剂层、普通恢复
                        _flow.SetPerspectiveView();
                        Require(_flow.railPerspective.activeSelf, "透视视图未显示透明钢轨");
                        Require(!_flow.railBg.gameObject.activeSelf, "透视视图普通钢轨未隐藏");
                        Require(!_flow.couplantOverlay.gameObject.activeSelf, "透视视图耦合剂层未隐藏");
                        _flow.SetNormalView();
                        Require(_flow.railBg.gameObject.activeSelf && !_flow.railPerspective.activeSelf, "普通视图未恢复");
                        Require(_flow.couplantOverlay.gameObject.activeSelf, "普通视图耦合剂层未恢复");
                        Pass("擦拭进度跟手 + 普通/透视切换通过");
                        _nextAt = EditorApplication.timeSinceStartup + .1;
                        break;

                    case 2: // 拖拽模拟：工具架拖出进入工作态（EventSystem 路径）
                        var evt = new PointerEventData(EventSystem.current) { position = new Vector2(960f, 540f) }; // pressEventCamera 只读，默认 null（ScreenSpaceOverlay 合法）
                        _rag.OnBeginDrag(evt);
                        Require(_rag.ragRt.parent == _rag.railViewport, "拖出后擦拭布未挂 RailViewport");
                        Require(_rag.ModeNow == M5RagDrag.Mode.Wiping, "拖出后未进入工作态");
                        Pass("擦拭布拖出进入工作态通过");
                        _nextAt = EditorApplication.timeSinceStartup + .1;
                        break;

                    case 3: // 擦完 100%：锁定 + 完成面板 + 结束模块文案
                        _flow.NotifyWipeProgress(1f);
                        Require(_flow.Wiped, "100% 未标记完成");
                        Require(_flow.CurrentStage == M5FlowController.Stage.Completed, "100% 未进入完成阶段");
                        Require(_flow.completionPanel == null || !_flow.completionPanel.activeSelf, "完成态不显示完成面板（老板 2026-08-23：完成文案不出现）");
                        Require(_flow.completionText != null && _flow.completionText.text == "M5 擦拭耦合剂完成", "完成文案错误");
                        Require(_flow.enterNextButton == null || !_flow.enterNextButton.gameObject.activeSelf, "结束模块不应有下一模块按钮");
                        Pass("擦完通过 + 完成面板（结束模块无下一模块）");
                        _nextAt = EditorApplication.timeSinceStartup + .1;
                        break;

                    case 4: // Reset：耦合剂铺满、擦拭布归槽、阶段回 Wipe
                        _flow.ResetAll();
                        Require(Mathf.Approximately(_fx.film.fillAmount, 1f), "Reset 后耦合剂未恢复铺满");
                        Require(_rag.ragRt.parent == _rag.ragHome, "Reset 后擦拭布未归槽");
                        Require(_rag.unlocked, "Reset 后擦拭布可拖（单步交互）");
                        Require(_flow.CurrentStage == M5FlowController.Stage.Wipe && !_flow.Wiped, "Reset 后阶段未回 Wipe");
                        Require(_flow.completionPanel == null || !_flow.completionPanel.activeSelf, "Reset 后完成面板未隐藏");
                        // QA 暂停契约
                        var before = Time.timeScale;
                        _qa.Open();
                        Require(Time.timeScale == 0f, "QA 打开未暂停游戏");
                        _qa.Close();
                        Require(Mathf.Approximately(Time.timeScale, before), "QA 关闭未恢复 timeScale");
                        Pass("Reset 恢复 + QA 暂停/恢复通过");
                        _nextAt = EditorApplication.timeSinceStartup + .1;
                        break;

                    default:
                        EditorApplication.update -= Tick;
                        if (_failures == 0) { Debug.Log("[M5RuntimeSmoke] ✅ M5 全部验收通过。"); EditorApplication.ExitPlaymode(); }
                        else { Debug.LogError($"[M5RuntimeSmoke] ❌ {_failures} 项断言失败。"); EditorApplication.ExitPlaymode(); }
                        break;
                }
            }
            catch (Exception e) { Fail(e); }
        }

        private static void Require(bool condition, string message)
        {
            if (condition) return;
            throw new InvalidOperationException("[M5RuntimeSmoke] " + message);
        }

        private static void Pass(string message) => Debug.Log("[M5RuntimeSmoke] ✅ " + message);

        private static void Fail(Exception e)
        {
            _failures++;
            Debug.LogError("[M5RuntimeSmoke] ❌ " + e.Message);
            EditorApplication.update -= Tick;
            EditorApplication.ExitPlaymode();
        }
    }
}
