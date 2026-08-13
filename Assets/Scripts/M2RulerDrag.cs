using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace M2
{
    /// <summary>尺子拖拽与零刻度自动吸附；只报告对齐完成。</summary>
    public class M2RulerDrag : MonoBehaviour, IBeginDragHandler, IDragHandler
    {
        public M2FlowController flow;
        public RectTransform rulerRt;
        public RectTransform railViewport;
        public RectTransform weldLineRt;
        public RectTransform rulerHome;
        public Image rulerImage;
        public Vector2 measureSize = new Vector2(420f, 91f);
        public Vector2 measureStartLocal = new Vector2(.5f, .78f);
        [Tooltip("0 刻度锚点（尺子本地偏移，像素）")]
        public Vector2 zeroAnchorLocal = new Vector2(-415.5f, 0f);
        public float snapTolerance = 24f;
        public bool lockAfterSnap = true;
        public event Action OnAligned;
        public bool unlocked;
        public bool aligned;
        private bool _dragging, _homeCached;
        private Vector2 _homeAnchorMin, _homeAnchorMax, _homePosition, _homeSize, _homePivot;
        private Vector3 _homeScale;
        private Quaternion _homeRotation;

        private void Awake() => CacheSceneHome();

        public void Bind(M2FlowController owner)
        {
            flow = owner;
            CacheSceneHome();
            OnAligned -= flow.NotifyMeasured; OnAligned += flow.NotifyMeasured;
            unlocked = aligned = _dragging = false;
            if (rulerRt != null) rulerRt.gameObject.SetActive(true);
            if (rulerImage != null) rulerImage.color = new Color(.55f, .57f, .6f, .62f);
        }
        private void CacheSceneHome()
        {
            if (_homeCached || rulerRt == null) return;
            if (rulerHome == null) rulerHome = FindDeep(rulerRt.root, "RulerHome") as RectTransform;
            if (rulerHome == null || rulerRt.parent != rulerHome || rulerRt.GetSiblingIndex() != rulerHome.childCount - 1)
            {
                Debug.LogError("[M2RulerDrag] Scene 中 Ruler 必须是 RulerHome 的最后子节点。", this);
                return;
            }
            _homeAnchorMin = rulerRt.anchorMin; _homeAnchorMax = rulerRt.anchorMax;
            _homePosition = rulerRt.anchoredPosition; _homeSize = rulerRt.sizeDelta; _homePivot = rulerRt.pivot;
            _homeScale = rulerRt.localScale; _homeRotation = rulerRt.localRotation;
            if (rulerImage == null) rulerImage = rulerRt.Find("bg")?.GetComponent<Image>();
            zeroAnchorLocal = new Vector2(GetRenderedImageLeft(), 0f);
            _homeCached = true;
        }
        public void Unlock()
        {
            unlocked = true; aligned = false;
            if (rulerImage != null) rulerImage.color = Color.white;
        }
        public void Show()
        {
            if (rulerRt != null && railViewport != null)
            {
                rulerRt.SetParent(railViewport, false); rulerRt.anchorMin = rulerRt.anchorMax = railViewport.pivot;
                rulerRt.pivot = new Vector2(.5f, .5f);
                rulerRt.anchoredPosition = new Vector2((measureStartLocal.x - railViewport.pivot.x) * railViewport.rect.width,
                    (measureStartLocal.y - railViewport.pivot.y) * railViewport.rect.height);
                rulerRt.sizeDelta = measureSize;
                zeroAnchorLocal = new Vector2(GetRenderedImageLeft(), 0f); rulerRt.gameObject.SetActive(true);
            }
            Unlock();
        }
        public void Hide() => ResetTool();
        public void ResetTool()
        {
            CacheSceneHome();
            unlocked = aligned = _dragging = false;
            if (rulerRt != null && _homeCached)
            {
                rulerRt.gameObject.SetActive(true); rulerRt.SetParent(rulerHome, false);
                rulerRt.anchorMin = _homeAnchorMin; rulerRt.anchorMax = _homeAnchorMax; rulerRt.pivot = _homePivot;
                rulerRt.anchoredPosition = _homePosition; rulerRt.sizeDelta = _homeSize;
                rulerRt.localScale = _homeScale; rulerRt.localRotation = _homeRotation;
                rulerRt.SetAsLastSibling(); zeroAnchorLocal = new Vector2(GetRenderedImageLeft(), 0f);
            }
            if (rulerImage != null) rulerImage.color = new Color(.55f, .57f, .6f, .62f);
        }
        private float GetRenderedImageLeft()
        {
            var rect = rulerRt.rect;
            if (rulerImage == null || rulerImage.sprite == null || !rulerImage.preserveAspect || rect.height <= 0f) return rect.xMin;
            var spriteRect = rulerImage.sprite.rect;
            var renderedWidth = Mathf.Min(rect.width, rect.height * spriteRect.width / spriteRect.height);
            return rect.center.x - renderedWidth * .5f;
        }
        private static Transform FindDeep(Transform root, string name)
        {
            foreach (Transform child in root)
            {
                if (child.name == name) return child;
                var hit = FindDeep(child, name); if (hit != null) return hit;
            }
            return null;
        }
        public void OnBeginDrag(PointerEventData eventData) => _dragging = unlocked && !aligned;
        public void OnDrag(PointerEventData eventData)
        {
            if (!_dragging || rulerRt == null || railViewport == null ||
                !RectTransformUtility.ScreenPointToLocalPointInRectangle(railViewport, eventData.position, eventData.pressEventCamera, out var local)) return;
            flow?.idleHelp?.ResetIdle();
            rulerRt.anchoredPosition = local - zeroAnchorLocal;
            CheckAlign();
        }
        private void CheckAlign()
        {
            if (weldLineRt == null) return;
            var weld = railViewport.InverseTransformPoint(weldLineRt.position);
            var zero = railViewport.InverseTransformPoint(rulerRt.TransformPoint(zeroAnchorLocal));
            if (Vector2.Distance(weld, zero) > snapTolerance) return;
            aligned = true; _dragging = false;
            rulerRt.anchoredPosition = new Vector2(weld.x - zeroAnchorLocal.x, weld.y - zeroAnchorLocal.y);
            OnAligned?.Invoke();
        }
    }
}
