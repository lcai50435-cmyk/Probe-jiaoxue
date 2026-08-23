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
        private Vector2 _dragStartLocal;   // 拖动手势起点（railViewport 局部）
        private float _dragStartX;         // 手势起点对应的抹布 x（拖出=擦拭区间左端；工作态继续拖=当前 x）
        private Vector2 _homeAnchorMin, _homeAnchorMax, _homePosition, _homeSize, _homePivot;
        private Vector3 _homeScale;
        private Quaternion _homeRotation;

        private void Awake() { CacheSceneHome(); }

        public void Bind(M5FlowController owner)
        {
            flow = owner; CacheSceneHome();
            if (ragImage == null) ragImage = GetComponentInChildren<Image>(true);
            unlocked = true; _dragging = _inputLocked = false; // M5 单步交互：擦拭布初始即可拖（Home 置灰仅视觉，非锁定）
        }

        public void Unlock() { unlocked = true; if (ragImage != null) ragImage.color = Color.white; }

        public void SetInputLocked(bool locked) { _inputLocked = locked; if (locked) _dragging = false; }

        public void ResetTool()
        {
            CacheSceneHome();
            unlocked = true; _dragging = _inputLocked = false; ModeNow = Mode.Home; WipeProgress = 0f; // Reset 后仍可拖（单步交互）
            if (ragRt != null && _homeCached)
            {
                ragRt.gameObject.SetActive(true);
                ragRt.SetParent(ragHome, false);
                ragRt.anchorMin = _homeAnchorMin; ragRt.anchorMax = _homeAnchorMax; ragRt.pivot = _homePivot;
                ragRt.anchoredPosition = _homePosition; ragRt.sizeDelta = _homeSize;
                ragRt.localScale = _homeScale; ragRt.localRotation = _homeRotation;
                ragRt.SetAsLastSibling();
            }
            if (ragImage != null) ragImage.color = Color.white; // Home 态清晰显示（老板 2026-08-23：不置灰半透明）
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (_inputLocked || !unlocked || ragRt == null || railViewport == null) return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(railViewport, eventData.position,
                    eventData.pressEventCamera, out var local)) return;
            if (ragRt.parent != railViewport)
            {
                EnterWorkFromPointer(local); // 从工具架拖出：吸附钢轨最左端（擦拭起点）
                _dragStartX = WipeBounds().left;
            }
            else _dragStartX = ragRt.anchoredPosition.x; // 工作态继续拖：从当前位置跟随
            _dragStartLocal = local;
            _dragging = ragRt.parent == railViewport;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!_dragging || _inputLocked || ragRt == null || railViewport == null ||
                !RectTransformUtility.ScreenPointToLocalPointInRectangle(railViewport, eventData.position,
                    eventData.pressEventCamera, out var local)) return;
            var (left, right, y) = WipeBounds();
            // 相对手势偏移跟随（老板 2026-08-23：拖出吸附最左后，从最左起点随拖动偏移，不落鼠标位置）
            var x = Mathf.Clamp(_dragStartX + (local.x - _dragStartLocal.x), left, right);
            ragRt.anchoredPosition = new Vector2(x, y);
            WipeProgress = Mathf.Clamp01((x - left) / Mathf.Max(.01f, right - left));
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
            ragRt.anchoredPosition = new Vector2(left, y); // 老板 2026-08-23：拖出直接吸附钢轨最左边（擦拭起点），不跟鼠标位置
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
