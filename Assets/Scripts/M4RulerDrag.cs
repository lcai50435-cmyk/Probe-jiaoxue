using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace M4
{
    /// <summary>M4 尺子拖拽：定位阶段尺子水平放置贴探头入射点；测量阶段 0/40mm 双点校验伤损。</summary>
    public class M4RulerDrag : MonoBehaviour, IBeginDragHandler, IDragHandler
    {
        public M4FlowController flow;
        public RectTransform rulerRt, railViewport, weldLineRt, rulerHome, positioningTarget;
        public Image rulerImage;
        public Sprite positioningSprite; // 定位（校角）阶段尺子素材；null = 跟随 Scene 序列化 sprite
        public Sprite measureSprite;     // 测量阶段尺子素材；null = 跟随 Scene 序列化 sprite
        public Vector2 measureSize = new Vector2(420f, 91f);
        public Vector2 positioningStart = new Vector2(.22f, .78f), measureStartLocal = new Vector2(.5f, .78f);
        public Vector2 zeroUv = new Vector2(.005f, .038f), ruler40Uv = new Vector2(.268f, .038f), slotUv = new Vector2(.005f, .136f);
        public float snapTolerance = 24f, positioningAngle = 0f, positionedAngleDeg = 0f, measureAngleDeg = 0f, angleToleranceDeg = 6f, pointTolerancePx = 24f, measureProjectTolerancePx = 30f, retractTolerancePx = 80f;
        public event Action OnPositioned, OnAligned, OnRetracted;
        public bool unlocked, positioned, aligned;
        public float PixelsPerMm { get; private set; }
        public Vector2 ZeroAnchorLocal => _zero;
        private bool _dragging, _homeCached, _measuring, _unlockedBeforePause;
        private Vector2 _zero, _r120, _slot;
        private Vector2 _homeAnchorMin, _homeAnchorMax, _homePosition, _homeSize, _homePivot;
        private Vector3 _homeScale;
        private Quaternion _homeRotation;
        private Sprite _homeSprite; // Scene 初态 sprite（归槽恢复用）

        private void Awake() => CacheSceneHome();

        public void Bind(M4FlowController owner)
        {
            flow = owner;
            CacheSceneHome();
            if (rulerImage != null)
            {
                _homeSprite = rulerImage.sprite; // Scene 序列化 sprite 是视觉权威（老板可手工换图）
                // 两个阶段素材都未配置：优先沿用 Scene 序列化 sprite；Scene 也空才用 Resources 兜底（历史素材替换合同）
                if (positioningSprite == null && measureSprite == null && _homeSprite == null)
                {
                    var sprites = Resources.LoadAll<Sprite>("尺子正面");
                    if (sprites != null && sprites.Length > 0) positioningSprite = measureSprite = sprites[0];
                }
            }
            OnPositioned -= flow.NotifyRulerPositioned; OnPositioned += flow.NotifyRulerPositioned;
            OnAligned -= flow.NotifyMeasured; OnAligned += flow.NotifyMeasured;
            OnRetracted -= flow.NotifyRulerRetracted; OnRetracted += flow.NotifyRulerRetracted;
            unlocked = positioned = aligned = _dragging = _measuring = false;
            if (rulerRt != null) rulerRt.gameObject.SetActive(true);
            ComputeAnchors();
            if (rulerImage != null) { rulerImage.color = new Color(.55f, .57f, .6f, .62f); rulerImage.raycastTarget = true; }
        }

        private void CacheSceneHome()
        {
            if (_homeCached || rulerRt == null) return;
            if (rulerHome == null) rulerHome = FindDeep(rulerRt.root, "RulerHome") as RectTransform;
            if (rulerHome == null || rulerRt.parent != rulerHome)
            {
                Debug.LogError("[M4RulerDrag] Scene 中 Ruler 必须是 RulerHome 的子节点。", this);
                return;
            }
            _homeAnchorMin = rulerRt.anchorMin; _homeAnchorMax = rulerRt.anchorMax;
            _homePosition = rulerRt.anchoredPosition; _homeSize = rulerRt.sizeDelta; _homePivot = rulerRt.pivot;
            _homeScale = rulerRt.localScale; _homeRotation = rulerRt.localRotation;
            if (rulerImage == null) rulerImage = rulerRt.Find("bg")?.GetComponent<Image>();
            _homeSprite = rulerImage != null ? rulerImage.sprite : null;
            _homeCached = true;
        }

        /// <summary>应用当前阶段尺子素材并重算锚点（阶段切换内部用；sprite 为 null 时保持现有图）。</summary>
        private void SetPhaseSprite(bool measuring)
        {
            var sp = measuring ? measureSprite : positioningSprite;
            if (sp != null && rulerImage != null) rulerImage.sprite = sp;
            ComputeAnchors();
        }

        /// <summary>调试器用：按当前模式应用阶段素材并重摆姿态（赋值后实时生效）。</summary>
        public void ApplyPhaseSprite()
        {
            SetPhaseSprite(_measuring || (positioned && aligned));
            RefreshPose();
        }

        public void Unlock()
        {
            unlocked = true; aligned = false; _measuring = false;
            if (rulerImage != null) rulerImage.color = Color.white;
        }

        /// <summary>校角确认后解锁撤尺：玩家把尺子拖回 RulerHome 归槽。</summary>
        public void UnlockRetract() { aligned = false; unlocked = true; }

        /// <summary>拖拽到 RulerHome 附近即归槽（恢复 Home 大小/位置），与 M2 撤尺合同一致。</summary>
        public void CheckRetract()
        {
            if (rulerRt == null || rulerHome == null || railViewport == null) return;
            if (Vector2.Distance(rulerRt.anchoredPosition, railViewport.InverseTransformPoint(rulerHome.position)) > retractTolerancePx) return;
            ResetTool(); OnRetracted?.Invoke();
        }

        /// <summary>帮助演示：直接摆到 RulerHome 位置触发归槽。</summary>
        public void AutoRetract()
        {
            if (rulerRt == null || rulerHome == null || railViewport == null) return;
            rulerRt.anchoredPosition = railViewport.InverseTransformPoint(rulerHome.position);
            CheckRetract();
        }

        /// <summary>检出后进入测量待拖态：尺子留在工具架（不自动出架），玩家拖出后拖到测量初始位吸附并应用调整角度（老板 2026-08-16 定稿）。</summary>
        public void PrepareMeasure() { _measuring = true; positioned = aligned = false; unlocked = true; if (rulerImage != null) rulerImage.color = Color.white; SetPhaseSprite(true); }

        public void ShowPositioning()
        {
            SetPhaseSprite(false); // 定位阶段素材
            MoveToWork(positioningStart); _measuring = false; positioned = aligned = false; unlocked = true;
            if (rulerRt != null) rulerRt.localRotation = Quaternion.Euler(0f, 0f, positioningAngle);
            ComputeAnchors();
            // 白色点 = 校角尺子放置位置：尺子中心对准（与吸附后一致）
            if (rulerRt != null && railViewport != null) rulerRt.anchoredPosition = NormalizedToRailLocal(positioningStart);
            if (rulerImage != null) rulerImage.color = Color.white;
        }

        public void AutoPosition()
        {
            if (rulerRt == null || railViewport == null) return;
            if (flow == null || flow.probeDrag == null) return;
            SetPhaseSprite(false); // 定位阶段素材
            if (rulerRt.parent != railViewport) MoveToWork(positioningStart);
            rulerRt.localRotation = Quaternion.Euler(0f, 0f, positioningAngle); // 校角角度自动应用（以调试器预设为准）
            rulerRt.anchoredPosition = NormalizedToRailLocal(positioningStart); // 白色点 = 校角尺子放置位置：尺子中心对准
            positioned = true; unlocked = false; flow?.PlayCorrect(); OnPositioned?.Invoke();
        }

        public void Show()
        {
            SetPhaseSprite(true); // 测量阶段素材
            MoveToWork(measureStartLocal); _measuring = true; positioned = true; aligned = false; unlocked = true;
            if (rulerRt != null) rulerRt.localRotation = Quaternion.Euler(0f, 0f, measureAngleDeg);
            ComputeAnchors();
            if (rulerImage != null) rulerImage.color = Color.white;
        }

        private void MoveToWork(Vector2 start)
        {
            if (rulerRt == null || railViewport == null) return;
            rulerRt.SetParent(railViewport, false); rulerRt.anchorMin = rulerRt.anchorMax = railViewport.pivot;
            rulerRt.pivot = new Vector2(.5f, .5f);
            rulerRt.localScale = new Vector3(.6f, .6f, .6f); // 工作态 0.6 倍显示（2026-08-18 老板：M2/M4 尺子以 M3 为基准统一；ppm 不乘 scale，几何不变）
            rulerRt.anchoredPosition = NormalizedToRailLocal(start);
            rulerRt.sizeDelta = measureSize; rulerRt.gameObject.SetActive(true);
            EnsureProbeAboveRuler(); // 渲染层级合同：探头必须高于尺子（2026-08-18 老板）
        }

        /// <summary>归一化 (0~1) 坐标 → 轨道本地像素（以 railViewport pivot 为原点）。</summary>
        private Vector2 NormalizedToRailLocal(Vector2 normalized)
        {
            return new Vector2((normalized.x - railViewport.pivot.x) * railViewport.rect.width,
                (normalized.y - railViewport.pivot.y) * railViewport.rect.height);
        }

        public void SetInputLocked(bool value)
        {
            if (value) { _unlockedBeforePause = unlocked; unlocked = false; }
            else unlocked = _unlockedBeforePause;
        }

        public void Hide() => ResetTool();

        public void ResetTool()
        {
            CacheSceneHome();
            unlocked = positioned = aligned = _dragging = _measuring = false;
            if (rulerRt != null && _homeCached)
            {
                rulerRt.gameObject.SetActive(true); rulerRt.SetParent(rulerHome, false);
                rulerRt.anchorMin = _homeAnchorMin; rulerRt.anchorMax = _homeAnchorMax; rulerRt.pivot = _homePivot;
                rulerRt.anchoredPosition = _homePosition; rulerRt.sizeDelta = _homeSize;
                rulerRt.localScale = _homeScale; rulerRt.localRotation = _homeRotation;
                rulerRt.SetAsLastSibling();
            }
            ComputeAnchors();
            if (rulerImage != null) rulerImage.color = new Color(.55f, .57f, .6f, .62f);
            if (_homeSprite != null && rulerImage != null) rulerImage.sprite = _homeSprite; // 归槽恢复 Scene 初态图
        }

        private Vector2 AnchorAt(Vector2 size, Vector2 uv)
        {
            var rw = size.x; var rh = size.y;
            if (rulerImage != null && rulerImage.sprite != null && size.y > 0f)
            {
                var sr = rulerImage.sprite.rect; rw = Mathf.Min(rw, size.y * sr.width / sr.height); rh = rw * sr.height / sr.width;
            }
            return new Vector2((uv.x - .5f) * rw, (uv.y - .5f) * rh);
        }

        private void ComputeAnchors()
        {
            var size = rulerRt != null && rulerRt.sizeDelta.y > 0f ? rulerRt.sizeDelta : measureSize;
            _zero = AnchorAt(size, zeroUv); _r120 = AnchorAt(size, ruler40Uv); _slot = AnchorAt(size, slotUv);
            var m0 = AnchorAt(measureSize, zeroUv); var m40 = AnchorAt(measureSize, ruler40Uv);
            PixelsPerMm = Vector2.Distance(m0, m40) / 40f;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            _dragging = unlocked && !aligned && rulerRt != null && railViewport != null;
            if (_dragging && rulerRt.parent != railViewport &&
                RectTransformUtility.ScreenPointToLocalPointInRectangle(railViewport, eventData.position, eventData.pressEventCamera, out var local))
            {
                EnterWorkFromPointer(local);
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!_dragging || rulerRt == null || railViewport == null ||
                !RectTransformUtility.ScreenPointToLocalPointInRectangle(railViewport, eventData.position, eventData.pressEventCamera, out var local)) return;
            flow?.idleHelp?.ResetIdle();
            if (_measuring)
            {
                // 测量：尺子中心跟指针，拖到测量初始位起点吸附并应用调整角度（老板定稿：最终位置=测量初始位起点）
                rulerRt.anchoredPosition = local;
                CheckMeasurePlacement(); // 老板 2026-08-16 定稿：测量只保留位置吸附（measureStartLocal），不依赖 0/120 几何对齐（CheckAlign 已从拖动链路移除，仅烟测反射调用保留）
            }
            else
            {
                rulerRt.anchoredPosition = local; // 校角：尺子中心跟指针（中心对准白色点吸附）；校角确认后拖回 RulerHome 撤尺
                if (flow != null && flow.AngleVerifiedByRuler) CheckRetract();
                else CheckPositioning();
            }
        }

        private void EnterWorkFromPointer(Vector2 local)
        {
            if (rulerRt == null || railViewport == null) return;
            rulerRt.SetParent(railViewport, false);
            rulerRt.anchorMin = rulerRt.anchorMax = railViewport.pivot;
            rulerRt.pivot = new Vector2(.5f, .5f);
            rulerRt.localScale = new Vector3(.6f, .6f, .6f); // 工作态 0.6 倍显示（2026-08-18 老板：M2/M4 尺子以 M3 为基准统一；ppm 不乘 scale，几何不变）
            rulerRt.sizeDelta = measureSize;
            rulerRt.localRotation = Quaternion.Euler(0f, 0f, _measuring ? measureAngleDeg : positioningAngle); // 测量阶段用测量角度，校角用校角角度
            SetPhaseSprite(_measuring); // 拖入工作态即按阶段应用素材
            if (rulerImage != null) rulerImage.color = Color.white; // 工作态不置灰（老板 2026-08-16：拖出来的尺子不能半透明）
            rulerRt.anchoredPosition = local; // 尺子中心跟指针（与 OnDrag 校角一致）
            rulerRt.gameObject.SetActive(true);
            ComputeAnchors();
            EnsureProbeAboveRuler(); // 渲染层级合同：探头必须高于尺子（2026-08-18 老板）
        }

        /// <summary>渲染层级合同（2026-08-18 老板）：探头渲染层级必须高于尺子。尺子进入 railViewport 工作态时，若探头已在其中，把尺子插到探头前一位（sibling 越大渲染越靠上），保证探头盖住尺子。</summary>
        private void EnsureProbeAboveRuler()
        {
            if (rulerRt == null || railViewport == null) return;
            var probe = flow != null ? flow.probeDrag : null;
            if (probe == null || probe.probeRt == null || probe.probeRt.parent != railViewport || rulerRt.parent != railViewport) return;
            if (probe.probeRt.GetSiblingIndex() > rulerRt.GetSiblingIndex()) return; // 探头已在尺子上方
            rulerRt.SetSiblingIndex(probe.probeRt.GetSiblingIndex()); // 尺子移到探头前一位
        }

        /// <summary>拖到测量初始位起点吸附：位置固定初始位、角度变为 measureAngleDeg，吸附即判定成功（蜂鸣+完成）。
        /// 以测量阶段放置位置（measureStartLocal）为基准，吸附成功即完成测量。</summary>
        private void CheckMeasurePlacement()
        {
            if (rulerRt == null || railViewport == null || aligned) return;
            var target = NormalizedToRailLocal(measureStartLocal);
            if (Vector2.Distance(rulerRt.anchoredPosition, target) > pointTolerancePx) return;
            rulerRt.anchoredPosition = target;
            rulerRt.localRotation = Quaternion.Euler(0f, 0f, measureAngleDeg);
            positioned = true;
            aligned = true; unlocked = _dragging = false;
            OnAligned?.Invoke(); // 触发完成（蜂鸣 + Completed）
        }

        private void CheckPositioning()
        {
            if (flow == null || flow.probeDrag == null || !flow.probeDrag.Placed) return; // 流程：先放探头，再放尺子校角
            // 白色点 = 校角阶段尺子放置位置：尺子中心对准即吸附（老板定稿：中心吸附，非 0 刻度）
            var target = NormalizedToRailLocal(positioningStart);
            var center = railViewport.InverseTransformPoint(rulerRt.position);
            if (Vector2.Distance(center, target) > pointTolerancePx) return;
            // 尺子无旋转控件：工作区姿态恒为 positioningAngle（校角角度），吸附后自动应用该角度
            if (Mathf.Abs(Mathf.DeltaAngle(rulerRt.localEulerAngles.z, positioningAngle)) > angleToleranceDeg) return;
            rulerRt.localRotation = Quaternion.Euler(0f, 0f, positioningAngle); // 吸附成功自动应用校角角度（以调试器预设为准）
            rulerRt.anchoredPosition = target;
            positioned = true; unlocked = _dragging = false; flow?.PlayCorrect(); // 校角吸附成功提示音（与 M2 一致）
            OnPositioned?.Invoke();
        }

        /// <summary>调试器用：按当前模式重摆尺子姿态（角度/位置实时生效，不重置吸附状态）。</summary>
        public void RefreshPose()
        {
            if (rulerRt == null || railViewport == null) return;
            if (positioned && aligned) { SetPoseMeasure(); return; } // 测量完成：保持吸附姿态
            if (_measuring) { Show(); return; } // 测量中：重摆到测量初始位 + 测量角
            if (positioned) { rulerRt.localRotation = Quaternion.Euler(0f, 0f, positioningAngle); return; } // 校角吸附：保持吸附位置、应用校角角度
            ShowPositioning(); // Home/校角未吸附：重摆到校角初始位 + 初始角
        }

        private void CheckAlign()
        {
            if (flow == null || flow.probeDrag == null) return;
            var probe = flow.probeDrag;
            var zero = railViewport.InverseTransformPoint(rulerRt.TransformPoint(_zero));
            if (Vector2.Distance(zero, probe.ZeroAnchorWorld) > pointTolerancePx) return; // 0 刻度对齐探头 zero 锚点中心（老板合同：zero↔尺子 0 刻度）
            var dLocal = rulerRt.InverseTransformPoint(railViewport.TransformPoint(probe.DamagePointInRail));
            if (Mathf.Abs(dLocal.x - _zero.x - (_r120.x - _zero.x)) > measureProjectTolerancePx) return;
            aligned = true; unlocked = _dragging = false;
            SetPoseMeasure(); OnAligned?.Invoke();
        }

        private void SetPoseMeasure()
        {
            if (flow?.probeDrag == null || rulerRt == null) return;
            rulerRt.localRotation = Quaternion.Euler(0f, 0f, measureAngleDeg);
            rulerRt.anchoredPosition = flow.probeDrag.ZeroAnchorWorld - _zero; // 0 刻度压 zero 中心、40mm 刻度压伤损（=射线末端）
        }

        private static Transform FindDeep(Transform root, string name)
        {
            foreach (Transform child in root) { if (child.name == name) return child; var hit = FindDeep(child, name); if (hit != null) return hit; }
            return null;
        }
    }
}
