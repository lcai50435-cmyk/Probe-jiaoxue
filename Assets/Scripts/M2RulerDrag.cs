using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
namespace M2
{
    public class M2RulerDrag : MonoBehaviour, IBeginDragHandler, IDragHandler
    {
        public enum Mode { Home, AngleGuide, DistanceMeasure }
        public M2FlowController flow;
        public RectTransform rulerRt, railViewport, weldLineRt, rulerHome;
        public Image rulerImage;
        public Vector2 measureSize = new Vector2(320f, 57f), angleGuideSize = new Vector2(240f, 42f), measureStartLocal = new Vector2(.5f, .78f), measureOffset = Vector2.zero;
        public Vector2 zeroUv = new Vector2(.005f, .038f), ruler110Uv = new Vector2(.73f, .038f), slotUv = new Vector2(.005f, .136f);
        public float angleToleranceDeg = 6f, pointTolerancePx = 24f, retractTolerancePx = 80f, measureAngleDeg = 0f, measureProjectTolerancePx = 30f;
        public event Action OnAngleAligned, OnDistanceAligned, OnAngleRetracted;
        public bool unlocked, aligned;
        public Mode ModeNow { get; private set; } = Mode.Home;
        public float PixelsPerMm { get; private set; }
        private Vector2 _zero, _r110, _slot;
        private bool _dragging, _homeCached;
        private Vector2 _homeAnchorMin, _homeAnchorMax, _homePosition, _homeSize, _homePivot, _bgPos;
        private Vector3 _homeScale, _bgScale;
        private Quaternion _homeRotation;
        private void Awake()
        {
            CacheSceneHome();
            // 老板 2026-08-16：测量角度/偏移以 Scene/Inspector 值为准（可在 Scene 定稿固定），不再 Awake 强制归零
        }
        public void Bind(M2FlowController owner)
        {
            flow = owner; CacheSceneHome(); if (rulerImage != null) { var sprites = Resources.LoadAll<Sprite>("尺子正面"); if (sprites != null && sprites.Length > 0) rulerImage.sprite = sprites[0]; }
            OnAngleAligned -= flow.NotifyRulerAligned; OnAngleAligned += flow.NotifyRulerAligned;
            OnDistanceAligned -= flow.NotifyMeasured; OnDistanceAligned += flow.NotifyMeasured;
            OnAngleRetracted -= flow.NotifyRulerRetracted; OnAngleRetracted += flow.NotifyRulerRetracted;
            unlocked = aligned = _dragging = false;
            ComputeAnchors();
        }
        public void Unlock() { unlocked = true; aligned = false; if (rulerImage != null) rulerImage.color = Color.white; }
        public void UnlockRetract() => aligned = false;
        public void ShowAngleGuide() { if (EnterWorkMode(measureSize)) { ModeNow = Mode.AngleGuide; Unlock(); } } // 校角与测量统一 measureSize（PPT 合同，与 M3 一致）
        /// <summary>检出后进入测量待拖态：尺子留在工具架（不自动出架），玩家自己拖出到测量放置位置吸附并应用测量角度（老板 2026-08-16，与 M3 一致）。</summary>
        public void PrepareMeasure() { ModeNow = Mode.DistanceMeasure; aligned = false; unlocked = true; if (rulerImage != null) rulerImage.color = Color.white; }
        public void ShowMeasure() { if (EnterWorkMode(measureSize)) { ModeNow = Mode.DistanceMeasure; OrientMeasure(); Unlock(); } }
        public void ResetTool()
        {
            CacheSceneHome();
            unlocked = aligned = _dragging = false; ModeNow = Mode.Home;
            if (rulerRt != null && _homeCached)
            {
                rulerRt.gameObject.SetActive(true); rulerRt.SetParent(rulerHome, false);
                rulerRt.anchorMin = _homeAnchorMin; rulerRt.anchorMax = _homeAnchorMax; rulerRt.pivot = _homePivot;
                rulerRt.anchoredPosition = _homePosition; rulerRt.sizeDelta = _homeSize;
                rulerRt.localScale = _homeScale; rulerRt.localRotation = _homeRotation;
                rulerRt.SetAsLastSibling();
            }
            if (rulerImage != null) { rulerImage.color = new Color(.55f, .57f, .6f, .62f); rulerImage.rectTransform.localScale = _bgScale; rulerImage.rectTransform.anchoredPosition = _bgPos; }
        }
        private bool EnterWorkMode(Vector2 size)
        {
            if (rulerRt == null || railViewport == null) return false;
            rulerRt.SetParent(railViewport, false);
            rulerRt.anchorMin = rulerRt.anchorMax = railViewport.pivot;
            rulerRt.pivot = new Vector2(.5f, .5f); rulerRt.localRotation = Quaternion.identity; // 尺子大小以 Scene 手工值（0.8）为准，不再强制 1（老板 2026-08-16）
            rulerRt.sizeDelta = size;
            rulerRt.anchoredPosition = new Vector2((measureStartLocal.x - railViewport.pivot.x) * railViewport.rect.width, (measureStartLocal.y - railViewport.pivot.y) * railViewport.rect.height);
            // bg 同以 Scene 值（scale 0.8 / pos 不变）为准，不覆盖（老板 2026-08-16）
            ComputeAnchors(); rulerRt.gameObject.SetActive(true);
            return true;
        }
        private Vector2 AnchorAt(Vector2 size, Vector2 uv)
        {
            var rw = size.x; var rh = size.y;
            if (rulerImage != null && rulerImage.sprite != null && size.y > 0f) { var sr = rulerImage.sprite.rect; rw = Mathf.Min(rw, size.y * sr.width / sr.height); rh = rw * sr.height / sr.width; }
            return new Vector2((uv.x - .5f) * rw, (uv.y - .5f) * rh);
        }
        private void ComputeAnchors()
        {
            var size = rulerRt != null && rulerRt.sizeDelta.y > 0f ? rulerRt.sizeDelta : measureSize;
            _zero = AnchorAt(size, zeroUv); _r110 = AnchorAt(size, ruler110Uv); _slot = AnchorAt(size, slotUv);
            var mz = AnchorAt(measureSize, zeroUv); var m110 = AnchorAt(measureSize, ruler110Uv);
            // 尺子视觉缩放（老板定稿 0.8）：ppm 按实际渲染比例折算，保证探头扫描/射线/尺子刻度视觉一致（2026-08-16）
            PixelsPerMm = Vector2.Distance(mz, m110) / 110f * Mathf.Max(.01f, rulerRt.localScale.x);
        }
        public void OnBeginDrag(PointerEventData eventData)
        {
            _dragging = unlocked && !aligned && rulerRt != null && railViewport != null;
            if (_dragging && rulerRt.parent != railViewport &&
                RectTransformUtility.ScreenPointToLocalPointInRectangle(railViewport, eventData.position, eventData.pressEventCamera, out var local))
            {
                EnterWorkFromPointer(local); // 从工具架拖出进入工作态（老板 2026-08-16：测量尺需玩家手动拖出）
            }
        }
        private void EnterWorkFromPointer(Vector2 local)
        {
            if (rulerRt == null || railViewport == null) return;
            rulerRt.SetParent(railViewport, false);
            rulerRt.anchorMin = rulerRt.anchorMax = railViewport.pivot;
            rulerRt.pivot = new Vector2(.5f, .5f);
            rulerRt.sizeDelta = measureSize;
            rulerRt.localRotation = Quaternion.Euler(0f, 0f, ModeNow == Mode.DistanceMeasure ? measureAngleDeg : 0f); // 测量态应用测量角度（老板 measureAngleDeg）
            rulerRt.anchoredPosition = local; // 尺子中心跟指针（OnDrag 继续）
            if (rulerImage != null) rulerImage.color = Color.white; // 工作态不置灰
            rulerRt.gameObject.SetActive(true);
            ComputeAnchors();
        }
        public void OnDrag(PointerEventData eventData)
        {
            if (!_dragging || rulerRt == null || railViewport == null ||
                !RectTransformUtility.ScreenPointToLocalPointInRectangle(railViewport, eventData.position, eventData.pressEventCamera, out var local)) return;
            flow?.idleHelp?.ResetIdle();
            var s = Mathf.Max(.01f, rulerRt.localScale.x); // 尺子视觉缩放（Scene 0.8）：锚点世界偏移 = 局部 × s
            var grab = ModeNow == Mode.AngleGuide ? _slot : _zero; var offset = rulerRt.localRotation * (Vector3)(grab * s);
            if (ModeNow == Mode.AngleGuide) { rulerRt.anchoredPosition = local - new Vector2(offset.x, offset.y); if (flow != null && flow.AngleVerifiedByRuler) { CheckRetract(); return; } CheckAngleGuide(); }
            else { rulerRt.anchoredPosition = local; CheckMeasurePlacement(); } // 测量：中心跟手，拖到 measureStartLocal 吸附（老板 2026-08-16）
        }
        public void CheckAngleGuide()
        {
            if (flow == null || flow.probeDrag == null) return;
            var probe = flow.probeDrag;
            var slot = railViewport.InverseTransformPoint(rulerRt.TransformPoint(_slot));
            if (Vector2.Distance(slot, probe.ProbeEntryPointInRail) > pointTolerancePx) return;
            if (Mathf.Abs(Vector2.SignedAngle(rulerRt.TransformVector(Vector2.right), railViewport.TransformVector(probe.scanDirection))) > angleToleranceDeg) return;
            aligned = true; _dragging = false;
            rulerRt.localRotation = Quaternion.identity;
            rulerRt.anchoredPosition = probe.ProbeEntryPointInRail - _slot * Mathf.Max(.01f, rulerRt.localScale.x);
            flow?.PlayCorrect(); // 校角吸附成功提示音（与 M3 一致，2026-08-16 老板）
            OnAngleAligned?.Invoke(); // 吸附成夹具，保留现场
        }
        /// <summary>拖到测量放置位置（measureStartLocal）吸附：位置固定、角度变为 measureAngleDeg，吸附即完成（老板 2026-08-16：最终吸附位置 = measureStartLocal + measureAngleDeg）。</summary>
        private void CheckMeasurePlacement()
        {
            if (rulerRt == null || railViewport == null || aligned) return;
            var target = NormalizedToRailLocal(measureStartLocal);
            if (Vector2.Distance(rulerRt.anchoredPosition, target) > pointTolerancePx) return;
            rulerRt.anchoredPosition = target;
            rulerRt.localRotation = Quaternion.Euler(0f, 0f, measureAngleDeg);
            aligned = true; unlocked = _dragging = false;
            OnDistanceAligned?.Invoke(); // 触发完成（蜂鸣 + Completed）
        }
        /// <summary>归一化 (0~1) 坐标 → 轨道本地像素（以 railViewport pivot 为原点）。</summary>
        private Vector2 NormalizedToRailLocal(Vector2 normalized)
        {
            return new Vector2((normalized.x - railViewport.pivot.x) * railViewport.rect.width,
                (normalized.y - railViewport.pivot.y) * railViewport.rect.height);
        }
        public void CheckMeasure()
        {
            if (flow == null || flow.probeDrag == null) return;
            var probe = flow.probeDrag;
            var zero = railViewport.InverseTransformPoint(rulerRt.TransformPoint(_zero));
            var r110 = railViewport.InverseTransformPoint(rulerRt.TransformPoint(_r110));
            if (Vector2.Distance(zero, probe.ProbeEntryPointInRail) > pointTolerancePx) return;
            if (Vector2.Distance(r110, probe.DamagePointInRail) > pointTolerancePx) return;
            aligned = true; _dragging = false;
            SetPoseMeasure(); OnDistanceAligned?.Invoke();
        }
        private void OrientMeasure()
        {
            // 老板 2026-08-16：测量角度由 measureAngleDeg 控制（默认 0=水平，Play 调试器可实时调）；不再自动斜定向（FromToRotation）
            if (rulerRt != null) rulerRt.localRotation = Quaternion.Euler(0f, 0f, measureAngleDeg);
        }
        public void SetPoseMeasure()
        {
            if (flow?.probeDrag == null || rulerRt == null) return; OrientMeasure();
            var s = Mathf.Max(.01f, rulerRt.localScale.x);
            var zero = rulerRt.localRotation * (Vector3)(_zero * s);
            rulerRt.anchoredPosition = flow.probeDrag.ProbeEntryPointInRail - new Vector2(zero.x, zero.y);
        }
        public void SetPoseAngleGuide() { if (flow?.probeDrag == null || rulerRt == null) return; rulerRt.localRotation = Quaternion.identity; rulerRt.anchoredPosition = flow.probeDrag.ProbeEntryPointInRail - _slot * Mathf.Max(.01f, rulerRt.localScale.x); }
        /// <summary>调试器用：按当前模式重摆尺子姿态（位置/角度实时生效，PlayDebugger 调整时调用；老板 2026-08-16）。</summary>
        public void RefreshPose()
        {
            if (rulerRt == null || railViewport == null) return;
            if (ModeNow == Mode.DistanceMeasure) { if (aligned) SetPoseMeasure(); else ShowMeasure(); }        // 测量：吸附后重摆 zero 位置；测量中重摆初始位+角度
            else if (ModeNow == Mode.AngleGuide) { if (aligned) SetPoseAngleGuide(); else ShowAngleGuide(); } // 校角：吸附后重摆 slot 位置；校角中重摆初始位
            else ShowAngleGuide();                                                                             // Home：默认校角工作态
        }
        public void SetPoseRetract() { if (rulerRt == null || rulerHome == null || railViewport == null) return; rulerRt.anchoredPosition = railViewport.InverseTransformPoint(rulerHome.position); }
        public void CheckRetract() { if (rulerRt == null || rulerHome == null || railViewport == null) return; if (Vector2.Distance(rulerRt.anchoredPosition, railViewport.InverseTransformPoint(rulerHome.position)) > retractTolerancePx) return; ResetTool(); OnAngleRetracted?.Invoke(); }
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
            var bg = rulerRt.Find("bg") as RectTransform; if (bg != null) { _bgScale = bg.localScale; _bgPos = bg.anchoredPosition; }
            if (rulerImage == null) rulerImage = bg != null ? bg.GetComponent<Image>() : null;
            if (rulerImage == null || rulerImage.sprite == null) Debug.LogError("[M2RulerDrag] 缺少正式尺 Sprite，mm 标定不可用。", this);
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
