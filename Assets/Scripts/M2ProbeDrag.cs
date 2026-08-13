using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
namespace M2
{
    /// <summary>探头放置、偏角门控与扫描距离报告；不拥有流程状态。</summary>
    public class M2ProbeDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public RectTransform probeRt, probeVisual, probeHome, railViewport, beamLine;
        public M2FlowController flow;
        public Slider angleSlider;
        public TMP_Text angleValueText, angleStatusText;
        public Color okGreen = new Color(0f, .55f, .25f);
        public Vector2 scanStartLocal = new Vector2(.143f, .57f), scanEndLocal = new Vector2(.571f, .57f);
        public Vector2 placementTolerance = new Vector2(.08f, .15f);
        public float scanStartMm = 150f, scanEndMm = 100f;
        public float visualTiltAtTarget = 10f; // 俯视图代理倾斜
        private const float DamageU = 1178f / 2455f, DamageV = 190f / 608f;
        public bool unlocked;
        public float currentDistanceMm = 150f;
        public event Action<float> OnDistanceChanged;
        private float _angleDeg;
        private bool _placed, _inputLocked, _dragging;
        private Vector2 _probeSize;
        public bool Placed => _placed;
        public bool AngleCorrect => flow != null && Mathf.Abs(_angleDeg - flow.targetAngle) < .5f;
        public float CurrentDistanceMm => currentDistanceMm;
        public void Bind(M2FlowController owner)
        {
            flow = owner;
            if (probeRt == null) probeRt = transform as RectTransform;
            if (probeVisual == null && probeRt != null) probeVisual = probeRt.Find("bg") as RectTransform;
            if (probeRt != null && _probeSize == Vector2.zero) _probeSize = probeRt.sizeDelta;
            CalibrateTrack(); ConfigureBeam();
            OnDistanceChanged -= flow.NotifyDistance; OnDistanceChanged += flow.NotifyDistance;
            if (angleSlider == null) return;
            angleSlider.onValueChanged.RemoveListener(OnAngleChanged); angleSlider.onValueChanged.AddListener(OnAngleChanged);
            _angleDeg = angleSlider.value; ApplyAngleVisual(_angleDeg);
        }
        public void Unlock() => unlocked = true;
        public void SetInputLocked(bool value) { _inputLocked = value; if (angleSlider != null) angleSlider.interactable = !value; }
        public void OnAngleChanged(float degrees)
        {
            _angleDeg = degrees;
            flow?.idleHelp?.ResetIdle();
            var correct = AngleCorrect;
            if (angleValueText != null) angleValueText.text = $"{degrees:0}°";
            if (angleStatusText != null)
            {
                angleStatusText.text = correct ? "偏角正确" : degrees < flow.targetAngle ? "请增大偏角" : "偏角过大";
                angleStatusText.color = correct ? okGreen : Color.red;
            }
            ApplyAngleVisual(degrees);
            if (correct) flow?.NotifyAngleCorrect();
        }
        public void SetAngleSilently(float degrees) { _angleDeg = degrees; ApplyAngleVisual(degrees); }
        public void AutoMoveToMm(float mm) { if (!_placed) PlaceAtStart(); MoveToProgress(Mathf.InverseLerp(scanStartMm, scanEndMm, mm)); }
        public void ResetTool()
        {
            unlocked = _inputLocked = _dragging = false; currentDistanceMm = scanStartMm; _angleDeg = 0f;
            if (angleSlider != null) { angleSlider.interactable = true; angleSlider.SetValueWithoutNotify(0f); }
            if (angleValueText != null) angleValueText.text = "0°";
            if (angleStatusText != null) { angleStatusText.text = "请增大偏角"; angleStatusText.color = Color.red; }
            ApplyAngleVisual(0f); ReturnHome(); OnDistanceChanged?.Invoke(currentDistanceMm);
        }
        public void OnBeginDrag(PointerEventData eventData)
        {
            _dragging = unlocked && !_inputLocked && probeRt != null && railViewport != null;
            if (_dragging && !_placed)
            {
                Reparent(probeRt, railViewport, scanStartLocal);
                if (TryGetRailPoint(eventData, out var point)) MoveToLocal(point);
            }
        }
        public void OnDrag(PointerEventData eventData)
        {
            if (!_dragging || !TryGetRailPoint(eventData, out var point)) return;
            flow?.idleHelp?.ResetIdle();
            if (!_placed) { MoveToLocal(point); return; }
            if (!AngleCorrect) return;
            var t = Mathf.Clamp01((point.x - scanStartLocal.x) / (scanEndLocal.x - scanStartLocal.x));
            MoveToProgress(t);
        }
        public void OnEndDrag(PointerEventData eventData)
        {
            if (!_dragging) return;
            _dragging = false;
            if (_placed) return;
            if (TryGetRailPoint(eventData, out var point) &&
                Mathf.Abs(point.x - scanStartLocal.x) <= placementTolerance.x &&
                Mathf.Abs(point.y - scanStartLocal.y) <= placementTolerance.y) PlaceAtStart();
            else ReturnHome();
        }
        private bool TryGetRailPoint(PointerEventData data, out Vector2 point)
        {
            point = default;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(railViewport, data.position, data.pressEventCamera, out var local)) return false;
            point = new Vector2(local.x / railViewport.rect.width + railViewport.pivot.x,
                local.y / railViewport.rect.height + railViewport.pivot.y);
            return true;
        }
        private void PlaceAtStart() { _placed = true; Reparent(probeRt, railViewport, scanStartLocal); MoveToProgress(0f); flow?.NotifyPlacementChanged(); }
        private void ReturnHome()
        {
            _placed = false;
            if (probeRt != null && probeHome != null) Reparent(probeRt, probeHome, new Vector2(.5f, .5f));
            if (beamLine != null) { beamLine.anchorMin = beamLine.anchorMax = scanStartLocal; beamLine.anchoredPosition = Vector2.zero; }
        }
        private void MoveToProgress(float t)
        {
            var mm = Mathf.Lerp(scanStartMm, scanEndMm, t);
            if (flow != null && !flow.Detected && currentDistanceMm > flow.targetDistance && mm < flow.targetDistance)
                t = Mathf.InverseLerp(scanStartMm, scanEndMm, mm = flow.targetDistance);
            MoveToLocal(new Vector2(Mathf.Lerp(scanStartLocal.x, scanEndLocal.x, t), scanStartLocal.y));
            currentDistanceMm = mm; OnDistanceChanged?.Invoke(currentDistanceMm);
        }
        private void MoveToLocal(Vector2 point)
        {
            probeRt.anchorMin = probeRt.anchorMax = point; probeRt.anchoredPosition = Vector2.zero;
            if (_placed && beamLine != null) { beamLine.anchorMin = beamLine.anchorMax = point; beamLine.anchoredPosition = Vector2.zero; }
        }
        private void ApplyAngleVisual(float degrees)
        {
            var target = flow != null ? flow.targetAngle : 10f;
            var tilt = target > 0f ? degrees / target * visualTiltAtTarget : 0f;
            if (probeVisual != null) probeVisual.localRotation = Quaternion.Euler(0f, 0f, tilt);
            if (beamLine != null) beamLine.localRotation = Quaternion.Euler(0f, 0f, degrees);
        }
        private void CalibrateTrack()
        {
            var rail = flow != null && flow.railPerspective != null ? flow.railPerspective.GetComponent<RectTransform>() : null;
            if (rail == null || railViewport == null) return;
            var local = railViewport.InverseTransformPoint(rail.TransformPoint(new Vector3(
                (DamageU - .5f) * rail.rect.width, (.5f - DamageV) * rail.rect.height)));
            var damage = new Vector2(local.x / railViewport.rect.width + railViewport.pivot.x,
                local.y / railViewport.rect.height + railViewport.pivot.y);
            scanStartLocal.y = damage.y; scanEndLocal.y = damage.y;
            scanEndLocal.x = scanStartLocal.x + (damage.x - scanStartLocal.x) / .8f;
        }
        private void ConfigureBeam() { if (beamLine != null) { beamLine.anchorMin = beamLine.anchorMax = scanStartLocal; beamLine.pivot = new Vector2(.5f, 1f); beamLine.anchoredPosition = Vector2.zero; } }
        private void Reparent(RectTransform child, RectTransform parent, Vector2 anchor)
        {
            child.SetParent(parent, false); child.localScale = Vector3.one;
            child.anchorMin = child.anchorMax = anchor; child.pivot = new Vector2(.5f, .5f);
            child.anchoredPosition = Vector2.zero; if (_probeSize != Vector2.zero) child.sizeDelta = _probeSize;
        }
    }
}
