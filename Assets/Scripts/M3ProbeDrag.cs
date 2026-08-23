using System;
using M2;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace M3
{
    /// <summary>M3 探头拖拽：160→120mm 像素几何、13° 视觉、检出射线恒绿（无绿→橙，2026-08-23）。</summary>
    public class M3ProbeDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public RectTransform probeRt, probeVisual, railViewport, beamLine, reflectedBeam, zeroAnchor, redLine; // zeroAnchor=探头 0 刻度锚点（不可见，尺子 0 刻度对齐其中心）；redLine=老板参考线（射线末端高度线）
        public M3FlowController flow;
        public Slider angleSlider;
        public TMP_Text angleValueText, angleStatusText;
        public Color okGreen = new Color(0f, .55f, .25f);
        public Color beamColor = new Color(.3f, 1f, .5f);
        public Color beamDetectedColor = new Color(1f, .45f, .05f);
        public Vector2 placementTolerancePx = new Vector2(60f, 40f);
        public Vector2 probeEntryLocal = new Vector2(.89f, .04f);
        public float scanStartMm = 160f, scanEndMm = 120f, scanStartY = 107f, visualTiltAtTarget = 13f, initialAngleDeg = 0f, probeBaseAngleDeg = 15f;
        public float settleDuration = .5f, beamLengthZeroMm = 550f, beamWidthPx = 14f, beamHitRadiusPx = 60f; // 校角稳定计时 / 0°基准射线长度 / 射线粗 / 命中半径
        public bool showReflectedBeam = false; // 反射射线默认关闭；老场景仍保留节点，但不再显示
        /// <summary>伤损标定 UV（正视角透明.png 2292×740）：x=红椭圆中心 1073.5；y=0.2121 对应椭圆中心（=red 上边缘线高度，老板参考线对齐处）。</summary>
        public Vector2 damageUv = new Vector2(1073.5f / 2292f, .2121f);
        /// <summary>红椭圆（伤损）中心 UV（正视角透明.png 像素采样：中心 x=1071、y=172）；检出判定基准（2026-08-18 与 M4 统一：末端进入红椭圆区域即成功接触）。</summary>
        public Vector2 damageEllipseUv = new Vector2(1071f / 2292f, 172f / 740f);
        public float ellipseHalfWidthPx = 13f / 2292f * 1000f, ellipseHalfHeightPx = 40f / 740f * 323f; // 红椭圆半轴（RailViewport 局部 px，Inspector 可调判定区大小）
        public bool unlocked;
        public float currentDistanceMm = 160f;
        public event Action<float> OnDistanceChanged;
        private float _angleDeg, _spriteAspect = 1f, _settle;
        private bool _placed, _beamVisible, _inputLocked, _dragging, _homeCached;
        private Vector2 _probeSize, _damageLocal, _ellipseLocal, _scanStartLocal, _scanEndLocal, _homeAnchor, _homePos, _homeSize, _homePivot;
        private Vector3 _homeScale;
        private Quaternion _homeRot;
        private Transform _homeParent;
        private Image _beamImage, _reflectedImage;
        private Sprite _beamSprite, _beamDetectedSprite;

        public bool Placed => _placed;
        public bool AngleCorrect => flow != null && Mathf.Abs(_angleDeg - flow.targetAngle) < .5f;
        public float CurrentDistanceMm => currentDistanceMm;
        public Vector2 ScanStartLocal => _scanStartLocal;
        public Vector2 ScanEndLocal => _scanEndLocal;
        public float PixelsPerMm => flow != null && flow.rulerDrag != null ? flow.rulerDrag.PixelsPerMm : 2.768f;
        public Vector2 DamagePointInRail => _damageLocal;
        public Vector2 DamageEllipsePointInRail => _ellipseLocal; // 红椭圆中心（检出判定区域，橙色标记对齐处）
        public Vector2 ProbeEntryPointInRail => railViewport != null ? railViewport.InverseTransformPoint(probeRt.TransformPoint(EntryLocal())) : Vector2.zero;
        /// <summary>探头 zero 锚点中心世界位置（RailViewport 局部）：扫描终点时与伤损同水平线、水平距伤损 120mm（尺子 0 刻度对齐处）。</summary>
        public Vector2 ZeroAnchorWorld => railViewport != null && zeroAnchor != null ? railViewport.InverseTransformPoint(zeroAnchor.position) : ProbeEntryPointInRail;

        /// <summary>射线末端是否实际照射到伤损（老板 2026-08-18：末端进入红椭圆区域即判定成功接触，与 M4 统一）：
        /// 射线末端（entry + 方向×当前长度）在红椭圆归一化坐标下 dx²/a²+dy²/b² ≤ 1（含边缘），与视觉“碰到红椭圆”一致。</summary>
        public bool BeamHitsDamage
        {
            get
            {
                if (!_placed || railViewport == null) return false;
                var entry = ProbeEntryPointInRail;
                var dir = new Vector2(Mathf.Cos(-_angleDeg * Mathf.Deg2Rad), Mathf.Sin(-_angleDeg * Mathf.Deg2Rad)); // M3 向下：-角度
                var end = entry + dir * BeamLenPx(_angleDeg);
                var dx = (end.x - _ellipseLocal.x) / Mathf.Max(.1f, ellipseHalfWidthPx);
                var dy = (end.y - _ellipseLocal.y) / Mathf.Max(.1f, ellipseHalfHeightPx);
                return dx * dx + dy * dy <= 1f;
            }
        }

        public void Bind(M3FlowController owner)
        {
            flow = owner;
            // Scene 若被误存了非法 UV（例如 probeEntryLocal=231,268），按验收合同迁移回 0.89,0.04；0~1 的合法调值不覆盖。
            if (probeEntryLocal.x < 0f || probeEntryLocal.x > 1f || probeEntryLocal.y < 0f || probeEntryLocal.y > 1f)
                probeEntryLocal = new Vector2(.89f, .04f);
            if (probeRt == null) probeRt = transform as RectTransform;
            if (probeVisual == null && probeRt != null) probeVisual = probeRt.Find("bg") as RectTransform;
            if (reflectedBeam == null && beamLine != null && beamLine.parent != null) reflectedBeam = beamLine.parent.Find("ReflectedBeam") as RectTransform;
            if (probeVisual != null && probeBaseAngleDeg == 0f && Mathf.Abs(probeVisual.localEulerAngles.z) > .01f) probeBaseAngleDeg = probeVisual.localEulerAngles.z; // 兼容旧 Scene 未序列化该字段：以 bg 当前 z 为平放基准
            if (reflectedBeam != null) reflectedBeam.gameObject.SetActive(false);
            if (probeRt != null && !_homeCached) CacheHome();
            if (probeVisual != null)
            {
                var img = probeVisual.GetComponent<Image>();
                if (img != null && img.sprite != null) _spriteAspect = img.sprite.rect.width / img.sprite.rect.height;
                if (img != null)
                {
                    // 与 M2 探头同款阴影+描边（2026-08-16 老板：M3 探头统一 M2 视觉）
                    var sh = img.GetComponent<Shadow>() ?? img.gameObject.AddComponent<Shadow>();
                    sh.effectColor = new Color(0f, 0f, 0f, .48f); sh.effectDistance = new Vector2(7f, -7f);
                    var ol = img.GetComponent<Outline>() ?? img.gameObject.AddComponent<Outline>();
                    ol.effectColor = new Color(.1f, .12f, .15f, .6f); ol.effectDistance = new Vector2(2f, -2f);
                }
            }
            if (_probeSize == Vector2.zero && probeRt != null) _probeSize = probeRt.sizeDelta;
            if (zeroAnchor == null && probeRt != null) zeroAnchor = probeRt.Find("zero") as RectTransform;
            if (redLine == null) redLine = FindDeep(transform.root, "red") as RectTransform; // 老板参考线（红椭圆伤损所在区域）
            // 注意：scanStartMm/scanEndMm/beamLengthZeroMm 等以 Scene 中老板手工调值为准，运行时不再覆盖。
            if (damageEllipseUv.x < 0f || damageEllipseUv.x > 1f || damageEllipseUv.y < 0f || damageEllipseUv.y > 1f)
                damageEllipseUv = new Vector2(1071f / 2292f, 172f / 740f); // 非法 UV 迁移回红椭圆中心
            CalibrateTrack(); CalibrateEllipse(); ConfigureBeam(); HideBeam();
            OnDistanceChanged -= flow.NotifyDistance; OnDistanceChanged += flow.NotifyDistance;
            if (angleSlider != null) { angleSlider.onValueChanged.RemoveListener(OnAngleChanged); angleSlider.onValueChanged.AddListener(OnAngleChanged); _angleDeg = angleSlider.value; initialAngleDeg = angleSlider.value; } // 初始角以 Scene 中滑块当前值为准
            if (angleValueText != null) angleValueText.text = $"{_angleDeg:0}°";
            currentDistanceMm = scanStartMm;
            ApplyAngleVisual(_angleDeg);
        }

        public void Unlock() => unlocked = true;
        public void SetInputLocked(bool value) { _inputLocked = value; if (angleSlider != null) angleSlider.interactable = !value; }
        /// <summary>只锁角度滑块（尺子校角吸附前不可调角；不锁探头拖拽）。</summary>
        public void SetAngleLocked(bool value) { if (angleSlider != null) angleSlider.interactable = !value; }

        public void OnAngleChanged(float degrees)
        {
            _angleDeg = degrees;
            flow?.idleHelp?.ResetIdle();
            _settle = 0f; // 角度变动重置稳定计时（M2 同款：稳定停留才确认校角）
            var correct = AngleCorrect;
            if (angleValueText != null) angleValueText.text = $"{degrees:0}°";
            if (angleStatusText != null) { angleStatusText.text = correct ? "偏角正确" : degrees < flow.targetAngle ? "请增大偏角" : "偏角过大"; angleStatusText.color = correct ? okGreen : Color.red; }
            ApplyAngleVisual(degrees);
            if (_placed && _beamVisible) UpdateBeam();
        }

        public void AutoMoveToMm(float mm)
        {
            if (!_placed) PlaceAtStart();
            MoveToProgress(Mathf.InverseLerp(scanStartMm, scanEndMm, mm));
        }

        public void ResetTool()
        {
            unlocked = _inputLocked = _dragging = false; currentDistanceMm = scanStartMm; _angleDeg = initialAngleDeg; _settle = 0f;
            if (angleSlider != null) { angleSlider.interactable = true; angleSlider.SetValueWithoutNotify(initialAngleDeg); }
            if (angleValueText != null) angleValueText.text = $"{initialAngleDeg:0}°";
            if (angleStatusText != null) { angleStatusText.text = "请增大偏角"; angleStatusText.color = Color.red; }
            ReturnHome(); HideBeam(); ApplyAngleVisual(initialAngleDeg); OnDistanceChanged?.Invoke(currentDistanceMm);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            _dragging = unlocked && !_inputLocked && probeRt != null && railViewport != null;
            if (_dragging && !_placed && TryGetRailPoint(eventData, out var local)) { Reparent(probeRt, railViewport, local); ApplyAngleVisual(_angleDeg); MoveToLocal(local); }
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!_dragging || !TryGetRailPoint(eventData, out var local)) return;
            flow?.idleHelp?.ResetIdle();
            if (!_placed) { MoveToLocal(local); return; }
            if (!AngleCorrect) return;
            var t = Mathf.InverseLerp(_scanStartLocal.x, _scanEndLocal.x, local.x);
            MoveToProgress(t);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!_dragging) return;
            _dragging = false;
            if (_placed) return;
            if (TryGetRailPoint(eventData, out var local) && Mathf.Abs(local.x - _scanStartLocal.x) <= placementTolerancePx.x && Mathf.Abs(local.y - _scanStartLocal.y) <= placementTolerancePx.y) PlaceAtStart();
            else ReturnHome();
        }

        private bool TryGetRailPoint(PointerEventData data, out Vector2 local)
        {
            local = default;
            return RectTransformUtility.ScreenPointToLocalPointInRectangle(railViewport, data.position, data.pressEventCamera, out local);
        }

        private void CacheHome()
        {
            _homeParent = probeRt.parent; _homeAnchor = probeRt.anchorMin; _homePos = probeRt.anchoredPosition; _homeCached = true;
            _homeSize = probeRt.sizeDelta; _homePivot = probeRt.pivot; _homeScale = probeRt.localScale;
            _homeRot = probeRt.localRotation; // Probe 根旋转保持 Scene 原值；平放基准由 bg 的 z=15 承担（probeBaseAngleDeg）
            if (_probeSize == Vector2.zero) _probeSize = probeRt.sizeDelta;
        }

        private void PlaceAtStart()
        {
            _placed = true; Reparent(probeRt, railViewport, _scanStartLocal); ApplyAngleVisual(_angleDeg); MoveToProgress(0f); ShowBeam(); flow?.NotifyPlacementChanged(); flow?.PlayCorrect(); // 放置成功提示音（与 M2 校角确认同款 correctClip）
        }
        public void ShowBeam()
        {
            _beamVisible = true;
            if (beamLine != null && beamLine.parent != null) beamLine.parent.gameObject.SetActive(true);
            if (beamLine != null) beamLine.gameObject.SetActive(true);
            if (reflectedBeam != null) reflectedBeam.gameObject.SetActive(showReflectedBeam);
            UpdateBeam(); ApplyBeamColor();
        }
        private void HideBeam()
        {
            _beamVisible = false;
            if (beamLine != null) beamLine.gameObject.SetActive(false);
            if (reflectedBeam != null) reflectedBeam.gameObject.SetActive(false);
            if (beamLine != null && beamLine.parent != null) beamLine.parent.gameObject.SetActive(false);
        }

        private void ReturnHome()
        {
            _placed = false;
            if (probeRt != null && _homeCached)
            {
                probeRt.SetParent(_homeParent, false); probeRt.anchorMin = probeRt.anchorMax = _homeAnchor;
                probeRt.anchoredPosition = _homePos; probeRt.sizeDelta = _homeSize; probeRt.pivot = _homePivot;
                probeRt.localScale = _homeScale; probeRt.localRotation = _homeRot;
            }
            if (beamLine != null) AnchorBeam(beamLine, _scanStartLocal); if (reflectedBeam != null) AnchorBeam(reflectedBeam, _scanStartLocal);
        }

        private void MoveToProgress(float t)
        {
            t = Mathf.Clamp01(t);
            if (flow != null && flow.Detected) return;
            // 不钳制到 targetDistance：扫描范围完全由 Scene 的 scanStartMm→scanEndMm 决定（老板手工调值），检出由射线末端照到伤损触发后锁定。
            var mm = Mathf.Lerp(scanStartMm, scanEndMm, t);
            MoveToLocal(Vector2.Lerp(_scanStartLocal, _scanEndLocal, t));
            currentDistanceMm = mm; OnDistanceChanged?.Invoke(currentDistanceMm);
        }

        private void MoveToLocal(Vector2 local)
        {
            probeRt.anchorMin = probeRt.anchorMax = railViewport.pivot; probeRt.anchoredPosition = local;
            if (!_placed) return;
            UpdateBeam();
        }

        private void Update()
        {
            ApplyAngleVisual(_angleDeg);
            if (_placed && _beamVisible) { UpdateBeam(); ApplyBeamColor(); }
            // 校角稳定计时（与 M2 同款）：尺子已吸附 && 角度正确 && 未确认 → 累积 0.5s → 确认校角
            if (flow == null || !flow.RulerDocked || flow.AngleVerifiedByRuler || !AngleCorrect) { _settle = 0f; return; }
            if ((_settle += Time.deltaTime) >= settleDuration) flow.NotifyAngleConfirmed();
        }

        private void UpdateBeam()
        {
            if (!_placed || !_beamVisible || railViewport == null) return;
            var entry = ProbeEntryPointInRail;
            if (beamLine != null)
            {
                beamLine.anchorMin = beamLine.anchorMax = railViewport.pivot;
                beamLine.anchoredPosition = entry;
                // 射线长度：默认 550mm 不变，仅当射线方向会碰到/超出 red 对象下边缘时才缩到"刚好打到下边缘"（老板 2026-08-16 定稿）
                beamLine.sizeDelta = new Vector2(BeamLenPx(_angleDeg), beamWidthPx);
            }
            if (reflectedBeam != null && showReflectedBeam)
            {
                reflectedBeam.anchorMin = reflectedBeam.anchorMax = railViewport.pivot;
                reflectedBeam.anchoredPosition = entry;
            }
        }

        private void ApplyBeamColor()
        {
            // 老板 2026-08-16 定稿：射线保持绿色（检出后不变色），伤损红椭圆变橙由 FlowController 处理。
            var targetSprite = M2ProbeDrag.GetBeamSpriteHorizontal(beamColor, ref _beamSprite);
            if (_beamImage != null)
            {
                if (_beamImage.sprite != targetSprite) _beamImage.sprite = targetSprite;
                _beamImage.color = new Color(beamColor.r, beamColor.g, beamColor.b, .55f + .25f * Mathf.Sin(Time.time * 8f));
            }
            if (_reflectedImage != null)
            {
                if (_reflectedImage.sprite != targetSprite) _reflectedImage.sprite = targetSprite;
                _reflectedImage.color = new Color(beamColor.r, beamColor.g, beamColor.b, .55f + .25f * Mathf.Sin(Time.time * 8f));
            }
        }

        private void ApplyAngleVisual(float degrees)
        {
            var target = flow != null ? flow.targetAngle : 13f;
            var tilt = target > 0f ? degrees / target * visualTiltAtTarget : 0f;
            if (probeVisual != null) probeVisual.localRotation = Quaternion.Euler(0f, 0f, probeBaseAngleDeg - tilt); // bg 的 Scene 旋转（15°）是“平放”基准，Play 下保持并叠加角度视觉
            if (beamLine != null) beamLine.localRotation = Quaternion.Euler(0f, 0f, -degrees); // 射线相对探头 90°：0° 时平，随角度同步下偏（老板定稿）
            if (reflectedBeam != null) reflectedBeam.localRotation = Quaternion.Euler(0f, 0f, degrees);
        }

        private Vector2 EntryLocal()
        {
            var rw = Mathf.Min(_probeSize.x, _probeSize.y * _spriteAspect);
            return new Vector2(
                (_probeSize.x - rw) * .5f + probeEntryLocal.x * rw - _probeSize.x * .5f,
                (_probeSize.y - rw / _spriteAspect) * .5f + probeEntryLocal.y * (rw / _spriteAspect) - _probeSize.y * .5f);
        }

        /// <summary>红椭圆中心换算 RailViewport 局部（检出判定基准，独立于 damageUv 的扫描几何）。</summary>
        private void CalibrateEllipse()
        {
            var rail = flow != null && flow.railPerspective != null ? flow.railPerspective.GetComponent<RectTransform>() : null;
            if (rail == null || railViewport == null) { _ellipseLocal = _damageLocal; return; }
            _ellipseLocal = railViewport.InverseTransformPoint(rail.TransformPoint(new Vector3((damageEllipseUv.x - .5f) * rail.rect.width, (.5f - damageEllipseUv.y) * rail.rect.height)));
        }

        private void CalibrateTrack()
        {
            var rail = flow != null && flow.railPerspective != null ? flow.railPerspective.GetComponent<RectTransform>() : null;
            if (rail == null || railViewport == null) { Debug.LogError("[M3ProbeDrag] 缺少 RailPerspective/RailViewport，几何合同不可用。", this); return; }
            _damageLocal = railViewport.InverseTransformPoint(rail.TransformPoint(new Vector3((damageUv.x - .5f) * rail.rect.width, (.5f - damageUv.y) * rail.rect.height)));
            var ppm = PixelsPerMm > 0.01f ? PixelsPerMm : 2.768f;
            // 用户确认：入射点起始位置为 RailViewport 局部 (x, scanStartY)，对应伤损左侧 scanStartMm；
            // 这里保存的是探头 Rect 中心位置，保证 ProbeEntryPointInRail 落在该入射点。
            var entryStart = new Vector2(_damageLocal.x - scanStartMm * ppm, scanStartY);
            var entryEnd = new Vector2(_damageLocal.x - scanEndMm * ppm, scanStartY);
            _scanStartLocal = entryStart - EntryLocal();
            _scanEndLocal = entryEnd - EntryLocal();
            // 注意：zero 锚点位置是 Scene 中老板手工调的值，运行时不再覆盖（尊重手工调参）。
        }

        /// <summary>red 对象下边缘在 RailViewport 局部的 y（射线末端目标线，实时跟随老板手工移动 red；
        /// 老板 2026-08-16 确认：射线截断以 red 对象为参考，移动 red 截断处实时变化）。</summary>
        private float RedBottomY()
        {
            if (redLine == null || railViewport == null) return scanStartY - 50f; // fallback：近似当前高度差
            var bottom = redLine.TransformPoint(new Vector3(0f, -redLine.rect.height * redLine.pivot.y, 0f)); // 下边缘局部点（含层级/缩放）
            return railViewport.InverseTransformPoint(bottom).y;
        }

        /// <summary>射线长度（px）：默认 beamLengthZeroMm（200mm）不变；仅当射线方向会碰到/超出红椭圆下边缘时才缩到"刚好打到下边缘"（drop/sin）。
        /// min 语义天然连续（angle→0 时 drop/sin→∞）：前 ~5° 长度完全不变，临界后平滑缩到末端精确落在红椭圆下边缘，无突变。</summary>
        private float BeamLenPx(float angleDeg)
        {
            var maxPx = beamLengthZeroMm * PixelsPerMm;
            var drop = ProbeEntryPointInRail.y - RedBottomY();
            if (drop <= 1f) return maxPx; // 入射点不高于红椭圆下边缘：向下射线够不到，保持默认长度
            var sin = Mathf.Sin(angleDeg * Mathf.Deg2Rad);
            if (sin <= .001f) return maxPx; // 近水平：drop/sin 发散（等价 min 取 maxPx），防除零
            return Mathf.Min(maxPx, drop / sin); // 够得到才缩，末端刚好打在红椭圆下边缘
        }

        private static Transform FindDeep(Transform root, string name)
        {
            foreach (Transform child in root) { if (child.name == name) return child; var hit = FindDeep(child, name); if (hit != null) return hit; }
            return null;
        }

        private void ConfigureBeam()
        {
            if (railViewport == null) return;
            if (beamLine != null)
            {
                beamLine.anchorMin = beamLine.anchorMax = railViewport.pivot; beamLine.pivot = new Vector2(0f, .5f); beamLine.anchoredPosition = _scanStartLocal;
                _beamImage = beamLine.GetComponent<Image>();
                if (_beamImage != null) _beamImage.sprite = M2ProbeDrag.GetBeamSpriteHorizontal(beamColor, ref _beamSprite);
            }
            if (reflectedBeam != null)
            {
                reflectedBeam.anchorMin = reflectedBeam.anchorMax = railViewport.pivot; reflectedBeam.pivot = new Vector2(0f, .5f); reflectedBeam.anchoredPosition = _scanStartLocal;
                _reflectedImage = reflectedBeam.GetComponent<Image>();
                if (_reflectedImage != null) _reflectedImage.sprite = M2ProbeDrag.GetBeamSpriteHorizontal(beamColor, ref _beamSprite);
            }
        }

        private static void AnchorBeam(RectTransform beam, Vector2 p)
        {
            beam.anchorMin = beam.anchorMax = new Vector2(.5f, .5f); beam.anchoredPosition = p;
        }

        private void Reparent(RectTransform child, RectTransform parent, Vector2 localPos)
        {
            child.SetParent(parent, false); child.localScale = Vector3.one; child.localRotation = Quaternion.identity;
            child.anchorMin = child.anchorMax = parent.pivot; child.pivot = new Vector2(.5f, .5f); child.anchoredPosition = localPos;
            if (_probeSize != Vector2.zero) child.sizeDelta = _probeSize;
        }
    }
}
