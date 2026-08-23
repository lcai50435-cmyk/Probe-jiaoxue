using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace M5
{
    /// <summary>
    /// M5 擦拭布拖拽：工具架(Home) → 拖到钢轨顶面(工作态)，玩家左右拖动控制擦拭范围（进度跟手）。
    /// 参照 M2RulerDrag 拖拽模式（IBeginDragHandler/IDragHandler、Home 缓存初态、工作态挂 RailViewport）。
    /// 进度 = 擦拭布中心 x 在钢轨顶面擦拭区间 [left,right] 的归一化位置；y 锁定钢轨顶面中心线。
    /// </summary>
    public class M5RagDrag : MonoBehaviour, IBeginDragHandler, IDragHandler
    {
        public enum Mode { Home, Wiping }
        public M5FlowController flow;
        public RectTransform ragRt, railViewport, railBg, ragHome;
        public Image ragImage;
        public Vector2 ragSize = new Vector2(150f, 150f); // 工作态尺寸（preserveAspect，Inspector 可调）
        public Vector4 wipeRect = new Vector4(.005f, .222f, .993f, .553f); // 擦拭区间（相对 railBg 底左归一化 x,y,w,h，同 M5CouplantFx coverRect）
        public float wipeYOffset = 0f;                    // 擦拭 y 微调（相对 railBg 中心线，Inspector 可调）
        public bool unlocked;
        public Mode ModeNow { get; private set; } = Mode.Home;
        public float WipeProgress { get; private set; }
        private bool _dragging, _homeCached, _inputLocked;
        private Vector2 _homeAnchorMin, _homeAnchorMax, _homePosition, _homeSize, _homePivot;
        private Vector3 _homeScale;
        private Quaternion _homeRotation;

        private void Awake() { CacheSceneHome(); }

        public void Bind(M5FlowController owner)
        {
            flow = owner; CacheSceneHome();
            if (ragImage == null) ragImage = GetComponentInChildren<Image>(true);
            unlocked = _dragging = _inputLocked = false;
        }

        public void Unlock() { unlocked = true; if (ragImage != null) ragImage.color = Color.white; }

        public void SetInputLocked(bool locked) { _inputLocked = locked; if (locked) _dragging = false; }

        public void ResetTool()
        {
            CacheSceneHome();
            unlocked = _dragging = _inputLocked = false; ModeNow = Mode.Home; WipeProgress = 0f;
            if (ragRt != null && _homeCached)
            {
                ragRt.gameObject.SetActive(true);
                ragRt.SetParent(ragHome, false);
                ragRt.anchorMin = _homeAnchorMin; ragRt.anchorMax = _homeAnchorMax; ragRt.pivot = _homePivot;
                ragRt.anchoredPosition = _homePosition; ragRt.sizeDelta = _homeSize;
                ragRt.localScale = _homeScale; ragRt.localRotation = _homeRotation;
                ragRt.SetAsLastSibling();
            }
            if (ragImage != null) ragImage.color = new Color(.45f, .47f, .5f, .9f); // 浅色 rag 置灰需加深+高不透明（与 M5Setup RagLockedColor 一致）
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (_inputLocked || !unlocked || ragRt == null || railViewport == null) return;
            if (ragRt.parent != railViewport &&
                RectTransformUtility.ScreenPointToLocalPointInRectangle(railViewport, eventData.position,
                    eventData.pressEventCamera, out var local))
            {
                EnterWorkFromPointer(local); // 从工具架拖出进入工作态
            }
            _dragging = ragRt.parent == railViewport;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!_dragging || _inputLocked || ragRt == null || railViewport == null ||
                !RectTransformUtility.ScreenPointToLocalPointInRectangle(railViewport, eventData.position,
                    eventData.pressEventCamera, out var local)) return;
            var (left, right, y) = WipeBounds();
            ragRt.anchoredPosition = new Vector2(Mathf.Clamp(local.x, left, right), y);
            WipeProgress = Mathf.Clamp01((ragRt.anchoredPosition.x - left) / Mathf.Max(.01f, right - left));
            flow?.NotifyWipeProgress(WipeProgress);
        }

        /// <summary>擦拭区间（railViewport 局部像素）：x = railBg 上 wipeRect 的 x 范围，y = railBg 中心线 + 偏移。</summary>
        public (float left, float right, float y) WipeBounds()
        {
            if (railBg == null) return (0f, 0f, 0f);
            var left = railBg.anchoredPosition.x + (wipeRect.x - .5f) * railBg.sizeDelta.x;
            var right = railBg.anchoredPosition.x + (wipeRect.x + wipeRect.z - .5f) * railBg.sizeDelta.x;
            return (left, right, railBg.anchoredPosition.y + wipeYOffset);
        }

        private void EnterWorkFromPointer(Vector2 local)
        {
            if (ragRt == null || railViewport == null) return;
            ragRt.SetParent(railViewport, false);
            ragRt.anchorMin = ragRt.anchorMax = railViewport.pivot;
            ragRt.pivot = new Vector2(.5f, .5f);
            ragRt.sizeDelta = ragSize;
            ragRt.localRotation = Quaternion.identity;
            var (left, right, y) = WipeBounds();
            ragRt.anchoredPosition = new Vector2(Mathf.Clamp(local.x, left, right), y);
            if (ragImage != null) ragImage.color = Color.white; // 工作态不置灰
            ragRt.gameObject.SetActive(true);
            ModeNow = Mode.Wiping;
        }

        private void CacheSceneHome()
        {
            if (_homeCached || ragRt == null) return;
            if (ragHome == null) ragHome = FindDeep(ragRt.root, "RagHome") as RectTransform;
            if (ragHome == null || ragRt.parent != ragHome || ragRt.GetSiblingIndex() != ragHome.childCount - 1)
            {
                Debug.LogError("[M5RagDrag] Scene 中 Rag 必须是 RagHome 的最后子节点。", this);
                return;
            }
            _homeAnchorMin = ragRt.anchorMin; _homeAnchorMax = ragRt.anchorMax;
            _homePosition = ragRt.anchoredPosition; _homeSize = ragRt.sizeDelta; _homePivot = ragRt.pivot;
            _homeScale = ragRt.localScale; _homeRotation = ragRt.localRotation;
            if (ragImage == null) ragImage = GetComponentInChildren<Image>(true);
            _homeCached = true;
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
    }
}
