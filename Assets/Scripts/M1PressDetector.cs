using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace M1
{
    /// <summary>
    /// 按压检测器：挂在数字人视图上，按住超过 holdDuration 触发 OnLongPress，
    /// 未超时抬手触发 OnShortPress（长按已触发则抑制本次抬手，避免长短按互斥误触发）。
    /// 用于「长按数字人 → 打开 AI 提问面板」「短按 → 全身/头像切换」。
    /// </summary>
    public class M1PressDetector : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        [Tooltip("长按触发时长（秒）")]
        public float holdDuration = 0.5f;

        /// <summary>短按触发事件（按压未达长按时长即抬手）。</summary>
        public event Action OnShortPress;

        /// <summary>长按触发事件。</summary>
        public event Action OnLongPress;

        private float _downTime;
        private bool _holding;
        private bool _longTriggered;

        public void OnPointerDown(PointerEventData eventData)
        {
            _holding = true;
            _longTriggered = false;
            _downTime = Time.unscaledTime;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            // 长按已触发则不再发短按（R4：长短按互斥）；指针移出后不结算短按
            if (_holding && !_longTriggered) OnShortPress?.Invoke();
            _holding = false;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _holding = false;
        }

        private void Update()
        {
            if (_holding && Time.unscaledTime - _downTime >= holdDuration)
            {
                _holding = false;
                _longTriggered = true;
                OnLongPress?.Invoke();
            }
        }
    }
}
