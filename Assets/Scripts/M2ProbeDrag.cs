using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
namespace M2
{
    public class M2ProbeDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public RectTransform probeRt, probeVisual, probeHome, railViewport, beamLine;
        public M2FlowController flow;
        public Slider angleSlider;
        public TMP_Text angleValueText, angleStatusText;
        public Color okGreen = new Color(0f, .55f, .25f);
        public Vector2 scanDirection = new Vector2(1f, 0f), probeEntryLocal = new Vector2(.5f, .25f), startLocal = new Vector2(-500f, 0f), damageUv = new Vector2(.4808f, .711f), placementTolerancePx = new Vector2(60f, 40f);
        public float hitMm = 110f, beamHitTolerancePx = 8f, visualTiltAtTarget = 10f, probeBaseAngleDeg = 0f, beamBaseAngleDeg = 0f, beamLengthZeroMm = 550f, settleDuration = .5f, beamWidthPx = 14f;
        public bool unlocked;
        public float currentDistanceMm = 150f;
        public event Action<float> OnDistanceChanged;
        public bool Placed => _placed;
        public bool AngleCorrect => flow != null && Mathf.Abs(_angleDeg - flow.targetAngle) < .5f;
        public float CurrentDistanceMm => currentDistanceMm;
        public Vector2 DamagePointInRail => _damage;
        public Vector2 ProbeEntryPointInRail => ProbeEntryWorld();
        public float PixelsPerMm => flow != null && flow.rulerDrag != null ? flow.rulerDrag.PixelsPerMm : 2.109f;
        private float _angleDeg, _settle, _spriteAspect = 1f;
        private bool _placed, _inputLocked, _dragging, _beamVisible;
        private Vector2 _probeSize, _damage, _visualBasePos;
        private Image _beamImage;
        private Vector2 EntryLocal()
        {
            var rw = Mathf.Min(_probeSize.x, _probeSize.y * _spriteAspect);
            return new Vector2((_probeSize.x - rw) * .5f + probeEntryLocal.x * rw - _probeSize.x * .5f, (_probeSize.y - rw / _spriteAspect) * .5f + probeEntryLocal.y * (rw / _spriteAspect) - _probeSize.y * .5f);
        }
        private Vector2 ScanStart => startLocal - EntryLocal();
        private Vector2 HitPoint => new Vector2(_damage.x - hitMm * PixelsPerMm, startLocal.y) - EntryLocal();
        private float StartMm => Vector2.Distance(startLocal, _damage) / PixelsPerMm;
        private float TiltAngle => flow != null && flow.targetAngle > 0f ? _angleDeg / flow.targetAngle * visualTiltAtTarget : 0f;
        public void Bind(M2FlowController owner)
        {
            flow = owner; if (probeRt == null) probeRt = transform as RectTransform; if (probeVisual == null) probeVisual = probeRt.Find("bg") as RectTransform; if (_probeSize == Vector2.zero) _probeSize = probeRt.sizeDelta;
            var probeImage = probeVisual != null ? probeVisual.GetComponent<Image>() : null; if (probeImage != null) { var sprites = Resources.LoadAll<Sprite>("probeFootage"); if (sprites != null && sprites.Length > 0) probeImage.sprite = sprites[0]; if (probeImage.sprite != null) _spriteAspect = probeImage.sprite.rect.width / probeImage.sprite.rect.height; var sh = probeImage.GetComponent<Shadow>() ?? probeImage.gameObject.AddComponent<Shadow>(); sh.effectColor = new Color(0f, 0f, 0f, .48f); sh.effectDistance = new Vector2(7f, -7f); var ol = probeImage.GetComponent<Outline>() ?? probeImage.gameObject.AddComponent<Outline>(); ol.effectColor = new Color(.1f, .12f, .15f, .6f); ol.effectDistance = new Vector2(2f, -2f); }
            CalibrateTrack(); if (beamLine != null) _beamImage = beamLine.GetComponentInChildren<Image>(); if (probeVisual != null) _visualBasePos = probeVisual.anchoredPosition;
            OnDistanceChanged -= flow.NotifyDistance; OnDistanceChanged += flow.NotifyDistance;
            if (angleSlider == null) return;
            angleSlider.onValueChanged.RemoveListener(OnAngleChanged); angleSlider.onValueChanged.AddListener(OnAngleChanged);
            _angleDeg = angleSlider.value; ApplyAngleVisual(_angleDeg); SetAngleLocked(true);
        }
        public void Unlock() => unlocked = true;
        public void ShowBeam() { _beamVisible = true; UpdateBeam(); if (beamLine != null && beamLine.parent != null) beamLine.parent.gameObject.SetActive(true); }
        public void SetAngleLocked(bool value) { if (angleSlider != null) angleSlider.interactable = !value; }
        public void SetInputLocked(bool value) => _inputLocked = value;
        public void OnAngleChanged(float degrees)
        {
            _angleDeg = degrees; flow?.idleHelp?.ResetIdle(); _settle = 0f;
            if (angleValueText != null) angleValueText.text = $"{degrees:0}°"; if (angleStatusText != null) { angleStatusText.text = AngleCorrect ? "偏角正确" : degrees < flow.targetAngle ? "请增大偏角" : "偏角过大"; angleStatusText.color = AngleCorrect ? okGreen : Color.red; }
            ApplyAngleVisual(degrees);
        }
        public void SetAngleSilently(float degrees) { _angleDeg = degrees; ApplyAngleVisual(degrees); }
        public void AutoMoveToMm(float mm) { if (!_placed) PlaceAtStart(); MoveToScan(Mathf.InverseLerp(StartMm, hitMm, mm)); }
        public void PlaceAtStart() { _placed = true; Reparent(probeRt, railViewport, new Vector2(.5f, .5f)); MoveToScan(0f); ShowBeam(); flow?.NotifyPlacementChanged(); }
        public void ResetTool()
        {
            unlocked = _inputLocked = _dragging = false; _beamVisible = false; currentDistanceMm = StartMm; _angleDeg = 0f;
            if (angleSlider != null) { angleSlider.SetValueWithoutNotify(0f); angleSlider.interactable = false; }
            if (angleStatusText != null) { angleStatusText.text = "请增大偏角"; angleStatusText.color = Color.red; } if (angleValueText != null) angleValueText.text = "0°";
            ApplyAngleVisual(0f); ReturnHome(); OnDistanceChanged?.Invoke(currentDistanceMm);
        }
        public void OnBeginDrag(PointerEventData eventData)
        {
            _dragging = unlocked && !_inputLocked && probeRt != null && railViewport != null; if (_dragging && !_placed) { Reparent(probeRt, railViewport, railViewport.pivot); if (TryGetRailPoint(eventData, out var p)) MoveToLocal(p); }
        }
        public void OnDrag(PointerEventData eventData)
        {
            if (!_dragging || !TryGetRailPoint(eventData, out var point)) return;
            flow?.idleHelp?.ResetIdle();
            if (!_placed) { MoveToLocal(point); return; } if (flow != null && flow.CurrentStage == M2FlowController.Stage.Scanning) MoveToScan(Mathf.Clamp01(Mathf.InverseLerp(ScanStart.x, HitPoint.x, point.x)));
        }
        public void OnEndDrag(PointerEventData eventData)
        {
            if (!_dragging) return;
            _dragging = false;
            if (_placed) return;
            if (TryGetRailPoint(eventData, out var point) && Vector2.Distance(point, ScanStart) <= placementTolerancePx.magnitude) PlaceAtStart(); else ReturnHome();
        }
        private void Update()
        {
            ApplyAngleVisual(_angleDeg); if (_beamVisible) UpdateBeam();
            if (_beamVisible && _beamImage != null) _beamImage.color = new Color(.3f, 1f, .55f, .55f + .25f * Mathf.Sin(Time.time * 8f));
            if (flow == null || !flow.RulerDocked || flow.AngleVerifiedByRuler || !AngleCorrect) { _settle = 0f; return; }
            if ((_settle += Time.deltaTime) >= settleDuration) flow.NotifyAngleConfirmed();
        }
        private bool TryGetRailPoint(PointerEventData data, out Vector2 local) => RectTransformUtility.ScreenPointToLocalPointInRectangle(railViewport, data.position, data.pressEventCamera, out local);
        private void MoveToScan(float t)
        {
            if (flow != null && flow.Detected) return;
            MoveToLocal(Vector2.Lerp(ScanStart, HitPoint, t)); currentDistanceMm = Vector2.Distance(ProbeEntryWorld(), _damage) / PixelsPerMm; OnDistanceChanged?.Invoke(currentDistanceMm); CheckHit();
        }
        private void MoveToLocal(Vector2 local) { probeRt.anchorMin = probeRt.anchorMax = railViewport.pivot; probeRt.anchoredPosition = local; UpdateBeam(); }
        private void CheckHit()
        {
            if (flow == null || flow.Detected || flow.CurrentStage != M2FlowController.Stage.Scanning) return;
            if (!AngleCorrect || Mathf.Abs(currentDistanceMm - hitMm) > flow.distanceToleranceMm) return;
            flow.NotifyDetected();
        }
        private void UpdateBeam()
        {
            if (beamLine == null || !_placed || !_beamVisible) return;
            if (_beamImage != null && (_beamImage.sprite == null || _beamImage.sprite.rect.height < 60f)) _beamImage.sprite = BeamGradient();
            beamLine.anchorMin = beamLine.anchorMax = railViewport.pivot;
            beamLine.pivot = new Vector2(.5f, 0f);
            beamLine.anchoredPosition = ProbeEntryWorld();
            beamLine.sizeDelta = new Vector2(beamWidthPx, Mathf.Lerp(beamLengthZeroMm, hitMm, Mathf.Clamp01(_angleDeg / (flow != null && flow.targetAngle > 0f ? flow.targetAngle : 10f))) * PixelsPerMm);
            beamLine.localRotation = Quaternion.Euler(0f, 0f, beamBaseAngleDeg + TiltAngle);
        }
        private static Sprite _beamGradient;
        private static Sprite BeamGradient()
        {
            if (_beamGradient != null) return _beamGradient;
            const int w = 32, h = 128; var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            for (var y = 0; y < h; y++) { var u = y / (float)(h - 1); var half = Mathf.Lerp(6f, 1.5f, u); var endGlow = Mathf.Exp(-Mathf.Pow((u - 1f) / .14f, 2f)); for (var x = 0; x < w; x++) { var g = Mathf.Exp(-Mathf.Pow((x - 15.5f) / (half * .6f), 2f)); var a = Mathf.Lerp(.9f, .3f, u) * g + endGlow * .85f; tex.SetPixel(x, y, new Color(.3f + .4f * endGlow, 1f, .5f + .2f * endGlow, Mathf.Clamp01(a))); } }
            tex.Apply();
            _beamGradient = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(.5f, 0f), 100f);
            return _beamGradient;
        }
        private void ApplyAngleVisual(float degrees)
        {
            if (probeVisual == null) return;
            var tilt = probeBaseAngleDeg + TiltAngle;
            probeVisual.localRotation = Quaternion.Euler(0f, 0f, tilt);
            var e = EntryLocal(); var c = Mathf.Cos(tilt * Mathf.Deg2Rad); var s = Mathf.Sin(tilt * Mathf.Deg2Rad);
            probeVisual.anchoredPosition = _visualBasePos + e - new Vector2(e.x * c - e.y * s, e.x * s + e.y * c);
        }
        private void CalibrateTrack()
        {
            var rail = flow != null && flow.railPerspective != null ? flow.railPerspective.GetComponent<RectTransform>() : null;
            if (rail == null || railViewport == null) { Debug.LogError("[M2ProbeDrag] 缺少 RailPerspective/RailViewport，几何合同不可用。", this); return; }
            _damage = railViewport.InverseTransformPoint(rail.TransformPoint(new Vector3((damageUv.x - .5f) * rail.rect.width, (damageUv.y - .5f) * rail.rect.height)));
        }
        private Vector2 ProbeEntryWorld() => railViewport.InverseTransformPoint(probeRt.TransformPoint(EntryLocal()));
        private void ReturnHome()
        {
            _placed = false; if (probeRt != null && probeHome != null) Reparent(probeRt, probeHome, new Vector2(.5f, .5f)); if (beamLine != null) beamLine.anchoredPosition = new Vector2(9999f, 9999f);
        }
        private void Reparent(RectTransform child, RectTransform parent, Vector2 anchor)
        {
            child.SetParent(parent, false); child.localScale = Vector3.one; child.anchorMin = child.anchorMax = anchor; child.pivot = new Vector2(.5f, .5f); child.anchoredPosition = Vector2.zero; if (_probeSize != Vector2.zero) child.sizeDelta = _probeSize;
        }
    }
}
