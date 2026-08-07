using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace M1
{
    /// <summary>
    /// 长按检测器：挂在数字人头像上，按住超过 holdDuration 触发 OnLongPress。
    /// 用于「长按数字人 → 打开 AI 提问面板」（AI 新增版规格书 3.2 入口）。
    /// </summary>
    public class M1PressDetector : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        [Tooltip("长按触发时长（秒）")]
        public float holdDuration = 0.5f;

        /// <summary>长按触发事件。</summary>
        public event Action OnLongPress;

        private float _downTime;
        private bool _holding;

        public void OnPointerDown(PointerEventData eventData)
        {
            _holding = true;
            _downTime = Time.unscaledTime;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
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
                OnLongPress?.Invoke();
            }
        }
    }
}
