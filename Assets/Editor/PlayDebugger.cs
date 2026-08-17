// Play Mode 实时数值调试器（M2/M3 通用版）：Play Mode 下输入/拖拽数值立即写回运行时字段并重算几何，
// 游戏画面实时变化；可点“确定并保存到 Scene”，退出 Play 后自动写回场景，无需手动回填 Inspector。
// Editor 工具，豁免 150 行限制；不直接保存 Scene（写回由老板点击确认后触发）。
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using M3;

namespace M2.EditorTools
{
    public class PlayDebugger : EditorWindow
    {
        private M2ProbeDrag _m2Probe;
        private M2RulerDrag _m2Ruler;
        private M3ProbeDrag _m3Probe;
        private M3RulerDrag _m3Ruler;
        private bool _m3ShowAnchors = true;
        private Texture2D _m3DotTex;
        private Sprite _m3DotSprite;
        private Texture2D _m3SolidTex;
        private Sprite _m3SolidSprite;
        private RectTransform _m3RulerPlacementRect;
        private RectTransform _m3VizRail;
        private RectTransform _m3VizRuler;
        private Canvas _m3OverlayCanvas;
        private RectTransform _m3OverlayRt;
        private Font _m3LabelFont;
        private readonly List<M3AnchorMarker> _m3AnchorMarkers = new List<M3AnchorMarker>();
        private bool _m3ShowReflectedRay;
        private bool _m3ShowAdvancedUv;
        private bool _m3KeyParams = true;
        private Vector2 _scrollPos;
        private bool _applyRequested;
        private readonly List<PendingApply> _pendingApplies = new List<PendingApply>();

        // 进入 Play 时缓存 M3 Scene 原始值，供“恢复默认”一键复位（避免误拖 UV 字段后数值爆炸）。
        private bool _m3ProbeDefaultsCached, _m3RulerDefaultsCached;
        private Vector2 _m3ProbeEntryDefault, _m3PlacementToleranceDefault, _m3IncidentBeamSizeDefault, _m3ReflectedBeamSizeDefault;
        private float _m3ScanStartDefault, _m3ScanEndDefault, _m3ScanStartYDefault, _m3VisualTiltDefault, _m3InitialAngleDefault, _m3TargetAngleDefault;
        private Color _m3BeamColorDefault, _m3BeamDetectedColorDefault;
        private Vector2 _m3MeasureSizeDefault, _m3PositioningStartDefault, _m3MeasureStartDefault, _m3ZeroUvDefault, _m3Ruler120UvDefault, _m3SlotUvDefault;
        private float _m3SnapToleranceDefault, _m3PositioningAngleDefault, _m3PositionedAngleDefault, _m3MeasureAngleDefault, _m3AngleToleranceDefault, _m3PointToleranceDefault, _m3MeasureProjectToleranceDefault;

        [MenuItem("Tools/PlayMode 实时调试器")]
        public static void Open() => GetWindow<PlayDebugger>("实时数值调试器");

        [MenuItem("Tools/M2/PlayMode 实时调试器")] // 旧入口兼容，转发同一窗口
        public static void OpenLegacy() => Open();

        private void OnEnable() { EditorApplication.update += OnEditorUpdate; EditorApplication.playModeStateChanged += OnPlayModeStateChanged; }
        private void OnDisable() { EditorApplication.update -= OnEditorUpdate; EditorApplication.playModeStateChanged -= OnPlayModeStateChanged; DestroyM3AnchorViz(); }
        private void OnEditorUpdate()
        {
            if (!EditorApplication.isPlaying) return;
            if (_m3ShowAnchors)
            {
                try
                {
                    if (_m3Probe == null) _m3Probe = FindFirstObjectByType<M3ProbeDrag>();
                    if (_m3Ruler == null) _m3Ruler = FindFirstObjectByType<M3RulerDrag>();
                    if (_m3Probe != null || _m3Ruler != null)
                    {
                        if (!IsM3AnchorVizCurrent()) EnsureM3AnchorViz();
                        if (_m3AnchorMarkers.Count > 0) { RefreshM3AnchorViz(); Canvas.ForceUpdateCanvases(); } // 每帧跟随数值更新，不依赖 OnGUI 重绘
                        if (_m3Probe != null)
                        {
                            _m3Probe.showReflectedBeam = _m3ShowReflectedRay;
                            if (_m3Probe.reflectedBeam != null) _m3Probe.reflectedBeam.gameObject.SetActive(_m3ShowReflectedRay); // 反射射线默认关，且不被 ShowBeam 重新打开
                        }
                    }
                }
                catch { DestroyM3AnchorViz(); }
            }
            Repaint();
        }
        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                _m3ProbeDefaultsCached = _m3RulerDefaultsCached = false;
                _m3ShowReflectedRay = false;
                _applyRequested = false;
                _pendingApplies.Clear();
            }
            if (state == PlayModeStateChange.ExitingPlayMode) DestroyM3AnchorViz();
            if (state == PlayModeStateChange.EnteredEditMode && _applyRequested)
            {
                try { ApplyPendingToScene(); }
                catch (Exception e) { Debug.LogException(e); }
                _applyRequested = false;
                Repaint();
            }
        }

        private void OnGUI()
        {
            if (!EditorApplication.isPlaying)
            {
                EditorGUILayout.HelpBox("进入 Play Mode 后，此处直接改数值（拖拽或输入）→ 画面实时变化。\n调好后点“确定并保存到 Scene”，退出 Play 后自动写回场景，不用再手动回填 Inspector。", MessageType.Info);
                return;
            }
            if (_m2Probe == null) _m2Probe = FindFirstObjectByType<M2ProbeDrag>();
            if (_m2Ruler == null) _m2Ruler = FindFirstObjectByType<M2RulerDrag>();
            if (_m3Probe == null) _m3Probe = FindFirstObjectByType<M3ProbeDrag>();
            if (_m3Ruler == null) _m3Ruler = FindFirstObjectByType<M3RulerDrag>();
            if (!_m3ProbeDefaultsCached) CacheM3ProbeDefaults();
            if (!_m3RulerDefaultsCached) CacheM3RulerDefaults();
            if (_m2Probe == null && _m2Ruler == null && _m3Probe == null && _m3Ruler == null)
            {
                EditorGUILayout.HelpBox("未找到 M2/M3 探头或尺子组件（是否在 M2/M3 场景？）", MessageType.Error);
                return;
            }
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);
            DrawApplyBar();
            if (_m2Probe != null || _m2Ruler != null) DrawM2();
            if (_m3Probe != null || _m3Ruler != null) DrawM3();
            EditorGUILayout.EndScrollView();
        }

        private void DrawApplyBar()
        {
            EditorGUILayout.Space();
            if (_applyRequested)
            {
                EditorGUILayout.HelpBox("已标记保存：退出 Play 后自动写回 Scene；如果又改了数值，请先取消再重新点确定。保存场景(Ctrl+S)后即为最终文件。", MessageType.Warning);
                if (GUILayout.Button("取消保存标记", GUILayout.Height(24)))
                {
                    _applyRequested = false;
                    _pendingApplies.Clear();
                }
            }
            else if (GUILayout.Button("✔ 确定并保存到 Scene（自动退出 Play 写回并保存）", GUILayout.Height(28)))
            {
                CapturePending();
                _applyRequested = true;
                // 老板要求一步到位：点按钮即自动退出 Play → EnteredEditMode 写回场景 → 自动保存文件，无需手动切场景/Ctrl+S。
                EditorApplication.ExitPlaymode();
            }
            EditorGUILayout.Space();
        }

        private void DrawM2()
        {
            if (_m2Probe != null)
            {
                EditorGUILayout.LabelField("【M2 探头几何】（Probe · M2ProbeDrag）", EditorStyles.boldLabel);
                EditorGUI.BeginChangeCheck();
                _m2Probe.probeEntryLocal = EditorGUILayout.Vector2Field("入射点/发射面锚点 (probeEntryLocal)", _m2Probe.probeEntryLocal);
                _m2Probe.damageUv = EditorGUILayout.Vector2Field("损伤点UV·扫描线高度 (damageUv)", _m2Probe.damageUv);
                _m2Probe.startLocal = EditorGUILayout.Vector2Field("扫描起点 (startLocal)", _m2Probe.startLocal);
                _m2Probe.probeBaseAngleDeg = EditorGUILayout.FloatField("探头图片基准角 (probeBaseAngleDeg)", _m2Probe.probeBaseAngleDeg);
                _m2Probe.beamBaseAngleDeg = EditorGUILayout.FloatField("射线基准角 (beamBaseAngleDeg)", _m2Probe.beamBaseAngleDeg);
                _m2Probe.beamLengthZeroMm = EditorGUILayout.FloatField("射线长度基准mm (beamLengthZeroMm)", _m2Probe.beamLengthZeroMm);
                _m2Probe.visualTiltAtTarget = EditorGUILayout.FloatField("10°视觉倾斜 (visualTiltAtTarget)", _m2Probe.visualTiltAtTarget);
                if (EditorGUI.EndChangeCheck())
                {
                    InvokePrivate(_m2Probe, "CalibrateTrack"); // 重算 _damage
                    if (_m2Probe.Placed) _m2Probe.AutoMoveToMm(_m2Probe.CurrentDistanceMm); // 保持距离，重定位到新扫描线
                }
                EditorGUILayout.Space();
            }
            if (_m2Ruler != null)
            {
                EditorGUILayout.LabelField("【M2 尺子】（Ruler · M2RulerDrag）", EditorStyles.boldLabel);
                EditorGUI.BeginChangeCheck();
                _m2Ruler.slotUv = EditorGUILayout.Vector2Field("10°槽锚点·校角吸附位置 (slotUv)", _m2Ruler.slotUv);
                _m2Ruler.zeroUv = EditorGUILayout.Vector2Field("0mm锚点 (zeroUv)", _m2Ruler.zeroUv);
                _m2Ruler.ruler110Uv = EditorGUILayout.Vector2Field("110mm刻线锚点 (ruler110Uv)", _m2Ruler.ruler110Uv);
                _m2Ruler.measureStartLocal = EditorGUILayout.Vector2Field("工作态初始位置 (measureStartLocal)", _m2Ruler.measureStartLocal);
                _m2Ruler.measureAngleDeg = EditorGUILayout.FloatField("测量态角度° (measureAngleDeg)", _m2Ruler.measureAngleDeg);
                _m2Ruler.measureOffset = EditorGUILayout.Vector2Field("测量态位置偏移 (measureOffset)", _m2Ruler.measureOffset);
                _m2Ruler.pointTolerancePx = EditorGUILayout.FloatField("吸附容差px (pointTolerancePx)", _m2Ruler.pointTolerancePx);
                _m2Ruler.angleToleranceDeg = EditorGUILayout.FloatField("平行角容差° (angleToleranceDeg)", _m2Ruler.angleToleranceDeg);
                _m2Ruler.retractTolerancePx = EditorGUILayout.FloatField("归槽容差px (retractTolerancePx)", _m2Ruler.retractTolerancePx);
                if (EditorGUI.EndChangeCheck())
                {
                    InvokePrivate(_m2Ruler, "ComputeAnchors"); // 重算锚点缓存
                    _m2Ruler.RefreshPose(); // 按当前模式重摆尺子：校角/测量中改位置角度实时生效，吸附后重摆锚点（老板 2026-08-16）
                }
                EditorGUILayout.Space();
            }
            if (_m2Probe != null)
            {
                EditorGUILayout.LabelField("【M2 关键几何实况】", EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"入射点 entry = {_m2Probe.ProbeEntryPointInRail}");
                EditorGUILayout.LabelField($"损伤点 damage = {_m2Probe.DamagePointInRail}");
                EditorGUILayout.LabelField($"当前距离 = {_m2Probe.CurrentDistanceMm:F1}mm   ppm = {_m2Probe.PixelsPerMm:F3}");
                EditorGUILayout.HelpBox("提示：拖拽数值可快速调（按住 Shift 更精细）。探头/尺子已在位时改动不会自动移动旧位置，重新拖一下即可看新姿态。测量角度/偏移过大会导致 0/110 锚点超出吸附容差（24px）而无法完成测量，建议小值微调。", MessageType.Info);
                EditorGUILayout.Space();
            }
        }

        private void DrawM3()
        {
            if (GUILayout.Button("恢复 M3 场景默认值（误拖后点这里）", GUILayout.Height(24))) RestoreM3Defaults();

            // ===== 关键参数（老板常用 6 项，默认展开，实时生效）=====
            _m3KeyParams = EditorGUILayout.Foldout(_m3KeyParams, "【M3 关键参数（常用，实时生效）】", true);
            if (_m3KeyParams)
            {
                EditorGUI.indentLevel++;
                if (_m3Probe != null)
                {
                    EditorGUILayout.LabelField("— 探头 —", EditorStyles.miniBoldLabel);
                    EditorGUI.BeginChangeCheck();
                    var keyScanStart = EditorGUILayout.Vector2Field("探头放置位置 (轨道本地px, x→mm y→扫描线Y)", _m3Probe.ScanStartLocal);
                    var keyScanStartChanged = keyScanStart != _m3Probe.ScanStartLocal;
                    var keyEntryRaw = EditorGUILayout.Vector2Field("射线入射点 (probeEntryLocal UV 0~1)", _m3Probe.probeEntryLocal);
                    var keyEntryHealed = !Is01(keyEntryRaw);
                    _m3Probe.probeEntryLocal = keyEntryHealed ? _m3ProbeEntryDefault : keyEntryRaw;
                    _m3Probe.beamLengthZeroMm = EditorGUILayout.FloatField("射线0°基准长度mm（13°时自动=120mm）", _m3Probe.beamLengthZeroMm);
                    _m3Probe.beamHitRadiusPx = EditorGUILayout.FloatField("射线命中伤损半径px", _m3Probe.beamHitRadiusPx);
                    if (EditorGUI.EndChangeCheck() || keyEntryHealed || keyScanStartChanged)
                    {
                        if (keyScanStartChanged) SetM3ScanStartLocal(keyScanStart);
                        InvokePrivate(_m3Probe, "CalibrateTrack"); // 重算扫描线/损伤点
                        if (_m3Probe.Placed) _m3Probe.AutoMoveToMm(_m3Probe.CurrentDistanceMm); // 保持距离，重定位到新扫描线
                        if (_m3AnchorMarkers.Count > 0) { RefreshM3AnchorViz(); Canvas.ForceUpdateCanvases(); }
                    }
                    EditorGUILayout.Space();
                }
                if (_m3Ruler != null)
                {
                    EditorGUILayout.LabelField("— 尺子 —", EditorStyles.miniBoldLabel);
                    EditorGUI.BeginChangeCheck();
                    var keyPosStart = EditorGUILayout.Vector2Field("校角尺子放置位置·中心对准 (轨道本地px, 白色点)", GetM3PositioningStartLocal());
                    var keyPosStartChanged = keyPosStart != GetM3PositioningStartLocal();
                    if (keyPosStartChanged) _m3Ruler.positioningStart = M3RailLocalToNormalized(_m3Ruler, keyPosStart);
                    var keyMeasStart = EditorGUILayout.Vector2Field("测量阶段放置位置 (轨道本地px)", GetM3MeasureStartLocal());
                    var keyMeasStartChanged = keyMeasStart != GetM3MeasureStartLocal();
                    if (keyMeasStartChanged) _m3Ruler.measureStartLocal = M3RailLocalToNormalized(_m3Ruler, keyMeasStart);
                    _m3Ruler.positioningAngle = EditorGUILayout.FloatField("校角角度°（吸附后自动应用）", _m3Ruler.positioningAngle);
                    _m3Ruler.measureAngleDeg = EditorGUILayout.FloatField("测量角度°（吸附后自动应用）", _m3Ruler.measureAngleDeg);
                    if (EditorGUI.EndChangeCheck() || keyPosStartChanged || keyMeasStartChanged)
                    {
                        _m3Ruler.RefreshPose(); // 按当前模式重摆（校角/测量位置与角度实时生效）
                        if (_m3AnchorMarkers.Count > 0) { RefreshM3AnchorViz(); Canvas.ForceUpdateCanvases(); }
                    }
                }
                EditorGUI.indentLevel--;
                EditorGUILayout.Space();
            }

            // ===== 高级参数（全部字段，默认折叠）=====
            _m3ShowAdvancedUv = EditorGUILayout.Foldout(_m3ShowAdvancedUv, "高级参数（全部字段，一般不用改）");
            if (_m3Probe != null)
            {
                EditorGUILayout.LabelField("【M3 探头几何】（Probe · M3ProbeDrag）", EditorStyles.boldLabel);
                EditorGUI.BeginChangeCheck();
                _m3Probe.scanStartMm = EditorGUILayout.FloatField("扫描起点mm (scanStartMm)", _m3Probe.scanStartMm);
                _m3Probe.scanEndMm = EditorGUILayout.FloatField("扫描终点mm (scanEndMm)", _m3Probe.scanEndMm);
                _m3Probe.scanStartY = EditorGUILayout.FloatField("扫描线Y (scanStartY, 轨道本地px)", _m3Probe.scanStartY);
                _m3Probe.visualTiltAtTarget = EditorGUILayout.FloatField("角度视觉倾斜 (visualTiltAtTarget)", _m3Probe.visualTiltAtTarget);
                _m3Probe.settleDuration = EditorGUILayout.FloatField("校角稳定确认秒 (settleDuration)", _m3Probe.settleDuration);
                _m3Probe.beamWidthPx = EditorGUILayout.FloatField("射线粗px (beamWidthPx)", _m3Probe.beamWidthPx);
                var toleranceRaw = EditorGUILayout.Vector2Field("放置容差px (placementTolerancePx)", _m3Probe.placementTolerancePx);
                var toleranceHealed = !IsRange(toleranceRaw, 1f, 200f);
                _m3Probe.placementTolerancePx = toleranceHealed ? _m3PlacementToleranceDefault : toleranceRaw;
                _m3Probe.beamColor = EditorGUILayout.ColorField("射线颜色 (beamColor)", _m3Probe.beamColor);
                _m3Probe.beamDetectedColor = EditorGUILayout.ColorField("检出射线颜色 (beamDetectedColor)", _m3Probe.beamDetectedColor);
                if (EditorGUI.EndChangeCheck() || toleranceHealed)
                {
                    InvokePrivate(_m3Probe, "CalibrateTrack"); // 重算扫描线/损伤点
                    if (_m3Probe.Placed) _m3Probe.AutoMoveToMm(_m3Probe.CurrentDistanceMm); // 保持距离，重定位到新扫描线
                    SetPrivateField(_m3Probe, "_beamSprite", null); // 射线颜色改变后清缓存，下一帧按新颜色重建 Sprite
                    SetPrivateField(_m3Probe, "_beamDetectedSprite", null);
                    if (_m3AnchorMarkers.Count > 0) { RefreshM3AnchorViz(); Canvas.ForceUpdateCanvases(); } // 改完立即把锚点标记移到新位置
                }
                EditorGUILayout.Space();

                EditorGUILayout.LabelField("【M3 角度】（角度初值 / 最终摆放角）", EditorStyles.boldLabel);
                EditorGUI.BeginChangeCheck();
                if (_m3Probe.angleSlider != null)
                {
                    _m3Probe.angleSlider.minValue = EditorGUILayout.FloatField("角度滑块最小角° (angleSlider.minValue)", _m3Probe.angleSlider.minValue);
                    _m3Probe.angleSlider.maxValue = EditorGUILayout.FloatField("角度滑块最大角° (angleSlider.maxValue)", _m3Probe.angleSlider.maxValue);
                }
                _m3Probe.initialAngleDeg = EditorGUILayout.FloatField("探头初始角° (initialAngleDeg, Reset 后生效)", _m3Probe.initialAngleDeg);
                if (_m3Probe.flow != null)
                    _m3Probe.flow.targetAngle = EditorGUILayout.FloatField("探头最终可摆放角° (flow.targetAngle)", _m3Probe.flow.targetAngle);
                if (EditorGUI.EndChangeCheck())
                {
                    if (_m3Probe.angleSlider != null) _m3Probe.OnAngleChanged(_m3Probe.angleSlider.value); // 按新目标角刷新“偏角正确/偏角过大”
                    if (_m3AnchorMarkers.Count > 0) { RefreshM3AnchorViz(); Canvas.ForceUpdateCanvases(); }
                }
                EditorGUILayout.Space();

                EditorGUILayout.LabelField("【M3 射线】（只保留入射射线；反射射线默认关闭）", EditorStyles.boldLabel);
                _m3ShowReflectedRay = EditorGUILayout.Toggle("显示反射射线", _m3ShowReflectedRay);
                _m3Probe.showReflectedBeam = _m3ShowReflectedRay;
                if (_m3Probe.reflectedBeam != null) _m3Probe.reflectedBeam.gameObject.SetActive(_m3ShowReflectedRay);
                if (_m3Probe.beamLine != null)
                    EditorGUILayout.LabelField($"入射射线角度 = {_m3Probe.beamLine.localEulerAngles.z:F1}°（由角度滑块驱动；长度在上方关键参数里调）");
                EditorGUILayout.HelpBox("扫描终点 = 探头继续移动到 120mm（检出锁定）时，探头中心所在的位置；不是初始放置点。", MessageType.Info);
                EditorGUILayout.Space();

                EditorGUILayout.LabelField("【M3 关键几何实况】", EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"入射点 entry = {_m3Probe.ProbeEntryPointInRail}");
                EditorGUILayout.LabelField($"损伤点 damage = {_m3Probe.DamagePointInRail}");
                EditorGUILayout.LabelField($"扫描起点 local = {_m3Probe.ScanStartLocal}");
                EditorGUILayout.LabelField($"扫描终点 local = {_m3Probe.ScanEndLocal}");
                EditorGUILayout.LabelField($"Placed={_m3Probe.Placed}  AngleCorrect={_m3Probe.AngleCorrect}  当前距离={_m3Probe.CurrentDistanceMm:F1}mm  ppm={_m3Probe.PixelsPerMm:F3}");
                EditorGUILayout.HelpBox("M3 探头提示：初始放置位置(ScanStartLocal)直接改轨道本地像素坐标，x 会换算成 scanStartMm、y 会写入 scanStartY；也可直接用 mm/扫描线Y 调。probeEntryLocal 是高级 UV 参数，一般不用改。探头已放置时自动保持当前 mm 距离重定位。调好点上方“确定并保存到 Scene”，退出 Play 自动写回。", MessageType.Info);
                EditorGUILayout.Space();
            }
            if (_m3Ruler != null)
            {
                EditorGUILayout.LabelField("【M3 尺子】（Ruler · M3RulerDrag；放置位置/角度在上方关键参数里调）", EditorStyles.boldLabel);
                EditorGUI.BeginChangeCheck();
                _m3Ruler.measureSize = EditorGUILayout.Vector2Field("测量尺寸 (measureSize)", _m3Ruler.measureSize);
                bool zeroHealed = false, r120Healed = false, slotHealed = false;
                if (_m3ShowAdvancedUv)
                {
                    var zeroRaw = EditorGUILayout.Vector2Field("0mm锚点 (zeroUv, 0~1)", _m3Ruler.zeroUv);
                    zeroHealed = !Is01(zeroRaw);
                    _m3Ruler.zeroUv = zeroHealed ? _m3ZeroUvDefault : zeroRaw;
                    var r120Raw = EditorGUILayout.Vector2Field("120mm刻线锚点 (ruler120Uv, 0~1)", _m3Ruler.ruler120Uv);
                    r120Healed = !Is01(r120Raw);
                    _m3Ruler.ruler120Uv = r120Healed ? _m3Ruler120UvDefault : r120Raw;
                    var slotRaw = EditorGUILayout.Vector2Field("定位槽锚点 (slotUv, 0~1)", _m3Ruler.slotUv);
                    slotHealed = !Is01(slotRaw);
                    _m3Ruler.slotUv = slotHealed ? _m3SlotUvDefault : slotRaw;
                }
                _m3Ruler.snapTolerance = EditorGUILayout.FloatField("定位吸附容差px (snapTolerance)", _m3Ruler.snapTolerance);
                _m3Ruler.angleToleranceDeg = EditorGUILayout.FloatField("定位平行角容差° (angleToleranceDeg)", _m3Ruler.angleToleranceDeg);
                _m3Ruler.pointTolerancePx = EditorGUILayout.FloatField("0点吸附容差px (pointTolerancePx)", _m3Ruler.pointTolerancePx);
                _m3Ruler.measureProjectTolerancePx = EditorGUILayout.FloatField("120mm投影容差px (measureProjectTolerancePx)", _m3Ruler.measureProjectTolerancePx);
                _m3Ruler.retractTolerancePx = EditorGUILayout.FloatField("撤尺归槽容差px (retractTolerancePx)", _m3Ruler.retractTolerancePx);
                if (EditorGUI.EndChangeCheck() || zeroHealed || r120Healed || slotHealed)
                {
                    InvokePrivate(_m3Ruler, "ComputeAnchors"); // 重算 0/120/槽锚点缓存
                    // 工作态重摆：测量完成态回测量位（align 重置属预期副作用），否则回校角位；容差类参数下次拖拽生效
                    if (_m3Ruler.positioned && _m3Ruler.aligned) _m3Ruler.Show();
                    else if (_m3Ruler.positioned) _m3Ruler.ShowPositioning();
                    if (_m3AnchorMarkers.Count > 0) { RefreshM3AnchorViz(); Canvas.ForceUpdateCanvases(); } // 改完立即把锚点标记移到新位置
                }
                EditorGUILayout.LabelField($"zeroAnchorLocal={_m3Ruler.ZeroAnchorLocal}  PixelsPerMm={_m3Ruler.PixelsPerMm:F3}  unlocked={_m3Ruler.unlocked} positioned={_m3Ruler.positioned} aligned={_m3Ruler.aligned}");
                EditorGUILayout.HelpBox("M3 尺子提示：白色点=校角阶段尺子放置位置，尺子中心对准即吸附（自动应用校角角度°）；测量阶段 0 刻度吸探头入射点、120 刻度吸伤损（双点固定目标）。定位/测量初始位置、两个角度都实时生效；容差类参数下次拖拽生效。流程：先放探头 → 尺子中心放白色点吸附 → 解锁角度滑块 → 调 13° → 进入扫描。调好点上方“确定并保存到 Scene”，退出 Play 自动写回。", MessageType.Info);
                EditorGUILayout.Space();
            }

            _m3ShowAnchors = EditorGUILayout.Toggle("M3 锚点可视化（Game 视图临时圆点）", _m3ShowAnchors);
            if (_m3ShowAnchors)
            {
                if (!IsM3AnchorVizCurrent()) EnsureM3AnchorViz();
                RefreshM3AnchorViz();
                EditorGUILayout.HelpBox("锚点可视化（屏幕像素定位，非相对坐标）：黄=放置点（探头中心拖到这里才算放置成功），青=入射点（放置后），红=损伤点，品红=扫描终点；绿=尺子0mm，蓝=尺子120mm，橙=尺子槽点，白=尺子初始定位（半透明矩形是尺子初始摆放位置和角度）。标记是 Play 内临时物体，退出 Play 自动消失，不会写回 Scene。", MessageType.Info);
            }
            else
            {
                DestroyM3AnchorViz();
            }
        }

        private void CacheM3ProbeDefaults()
        {
            if (_m3ProbeDefaultsCached || _m3Probe == null) return;
            // UV/容差字段若已误拖成非法值，回退到项目验收合同默认值（Scene 中 probeEntryLocal=0.89,0.04）。
            _m3ProbeEntryDefault = Is01(_m3Probe.probeEntryLocal) ? _m3Probe.probeEntryLocal : new Vector2(.89f, .04f);
            _m3PlacementToleranceDefault = IsRange(_m3Probe.placementTolerancePx, 1f, 200f) ? _m3Probe.placementTolerancePx : new Vector2(60f, 40f);
            _m3ScanStartDefault = _m3Probe.scanStartMm > 0f && _m3Probe.scanStartMm < 1000f ? _m3Probe.scanStartMm : 160f;
            _m3ScanEndDefault = _m3Probe.scanEndMm > 0f && _m3Probe.scanEndMm < 1000f ? _m3Probe.scanEndMm : 120f;
            _m3ScanStartYDefault = Mathf.Abs(_m3Probe.scanStartY) < 10000f ? _m3Probe.scanStartY : 107f;
            _m3VisualTiltDefault = Mathf.Abs(_m3Probe.visualTiltAtTarget) < 90f ? _m3Probe.visualTiltAtTarget : 13f;
            _m3InitialAngleDefault = Mathf.Abs(_m3Probe.initialAngleDeg) < 90f ? _m3Probe.initialAngleDeg : 0f;
            _m3TargetAngleDefault = _m3Probe.flow != null && Mathf.Abs(_m3Probe.flow.targetAngle) < 90f ? _m3Probe.flow.targetAngle : 13f;
            _m3BeamColorDefault = _m3Probe.beamColor;
            _m3BeamDetectedColorDefault = _m3Probe.beamDetectedColor;
            _m3IncidentBeamSizeDefault = _m3Probe.beamLine != null ? _m3Probe.beamLine.sizeDelta : Vector2.zero;
            _m3ReflectedBeamSizeDefault = _m3Probe.reflectedBeam != null ? _m3Probe.reflectedBeam.sizeDelta : Vector2.zero;
            _m3ProbeDefaultsCached = true;
        }

        private void CacheM3RulerDefaults()
        {
            if (_m3RulerDefaultsCached || _m3Ruler == null) return;
            _m3MeasureSizeDefault = _m3Ruler.measureSize;
            _m3PositioningStartDefault = _m3Ruler.positioningStart;
            _m3MeasureStartDefault = _m3Ruler.measureStartLocal;
            _m3ZeroUvDefault = Is01(_m3Ruler.zeroUv) ? _m3Ruler.zeroUv : new Vector2(.005f, .038f);
            _m3Ruler120UvDefault = Is01(_m3Ruler.ruler120Uv) ? _m3Ruler.ruler120Uv : new Vector2(.807f, .038f);
            _m3SlotUvDefault = Is01(_m3Ruler.slotUv) ? _m3Ruler.slotUv : new Vector2(.005f, .136f);
            _m3SnapToleranceDefault = _m3Ruler.snapTolerance;
            _m3PositioningAngleDefault = _m3Ruler.positioningAngle;
            _m3PositionedAngleDefault = _m3Ruler.positionedAngleDeg;
            _m3MeasureAngleDefault = _m3Ruler.measureAngleDeg;
            _m3AngleToleranceDefault = _m3Ruler.angleToleranceDeg;
            _m3PointToleranceDefault = _m3Ruler.pointTolerancePx;
            _m3MeasureProjectToleranceDefault = _m3Ruler.measureProjectTolerancePx;
            _m3RulerDefaultsCached = true;
        }

        private void RestoreM3Defaults()
        {
            _applyRequested = false;
            _pendingApplies.Clear();
            if (_m3Probe != null && _m3ProbeDefaultsCached)
            {
                _m3Probe.probeEntryLocal = _m3ProbeEntryDefault;
                _m3Probe.placementTolerancePx = _m3PlacementToleranceDefault;
                _m3Probe.scanStartMm = _m3ScanStartDefault;
                _m3Probe.scanEndMm = _m3ScanEndDefault;
                _m3Probe.scanStartY = _m3ScanStartYDefault;
                _m3Probe.visualTiltAtTarget = _m3VisualTiltDefault;
                _m3Probe.initialAngleDeg = _m3InitialAngleDefault;
                if (_m3Probe.flow != null) _m3Probe.flow.targetAngle = _m3TargetAngleDefault;
                _m3Probe.beamColor = _m3BeamColorDefault;
                _m3Probe.beamDetectedColor = _m3BeamDetectedColorDefault;
                if (_m3Probe.beamLine != null) _m3Probe.beamLine.sizeDelta = _m3IncidentBeamSizeDefault;
                if (_m3Probe.reflectedBeam != null) _m3Probe.reflectedBeam.sizeDelta = _m3ReflectedBeamSizeDefault;
                SetPrivateField(_m3Probe, "_beamSprite", null);
                SetPrivateField(_m3Probe, "_beamDetectedSprite", null);
                InvokePrivate(_m3Probe, "CalibrateTrack");
                if (_m3Probe.Placed) _m3Probe.AutoMoveToMm(_m3Probe.CurrentDistanceMm);
            }
            _m3ShowReflectedRay = false;
            if (_m3Probe != null)
            {
                _m3Probe.showReflectedBeam = false;
                if (_m3Probe.reflectedBeam != null) _m3Probe.reflectedBeam.gameObject.SetActive(false);
            }
            if (_m3Ruler != null && _m3RulerDefaultsCached)
            {
                _m3Ruler.measureSize = _m3MeasureSizeDefault;
                _m3Ruler.positioningStart = _m3PositioningStartDefault;
                _m3Ruler.measureStartLocal = _m3MeasureStartDefault;
                _m3Ruler.zeroUv = _m3ZeroUvDefault;
                _m3Ruler.ruler120Uv = _m3Ruler120UvDefault;
                _m3Ruler.slotUv = _m3SlotUvDefault;
                _m3Ruler.snapTolerance = _m3SnapToleranceDefault;
                _m3Ruler.positioningAngle = _m3PositioningAngleDefault;
                _m3Ruler.positionedAngleDeg = _m3PositionedAngleDefault;
                _m3Ruler.measureAngleDeg = _m3MeasureAngleDefault;
                _m3Ruler.angleToleranceDeg = _m3AngleToleranceDefault;
                _m3Ruler.pointTolerancePx = _m3PointToleranceDefault;
                _m3Ruler.measureProjectTolerancePx = _m3MeasureProjectToleranceDefault;
                InvokePrivate(_m3Ruler, "ComputeAnchors");
                if (_m3Ruler.positioned && _m3Ruler.aligned) _m3Ruler.Show();
                else if (_m3Ruler.positioned) _m3Ruler.ShowPositioning();
            }
            if (_m3AnchorMarkers.Count > 0) { RefreshM3AnchorViz(); Canvas.ForceUpdateCanvases(); }
        }

        private static bool Is01(Vector2 v) => v.x >= 0f && v.x <= 1f && v.y >= 0f && v.y <= 1f;

        private static bool IsRange(Vector2 v, float min, float max) => v.x >= min && v.x <= max && v.y >= min && v.y <= max;

        private bool IsM3AnchorVizCurrent()
        {
            return _m3AnchorMarkers.Count > 0 && _m3OverlayCanvas != null && _m3Probe != null && _m3VizRail == _m3Probe.railViewport && (_m3Ruler == null || (_m3VizRuler == _m3Ruler.rulerRt && _m3RulerPlacementRect != null));
        }

        private void EnsureM3AnchorViz()
        {
            DestroyM3AnchorViz();
            if (_m3Probe == null || _m3Probe.railViewport == null) return;

            // 独立 Overlay Canvas，sortingOrder 9999：无论原 UI 层级怎么排，锚点标记都在最上层。
            var overlayGo = new GameObject("~M3AnchorOverlay", typeof(Canvas));
            overlayGo.hideFlags = HideFlags.DontSave;
            overlayGo.layer = 5;
            _m3OverlayCanvas = overlayGo.GetComponent<Canvas>();
            _m3OverlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _m3OverlayCanvas.sortingOrder = 9999;
            _m3OverlayRt = (RectTransform)overlayGo.transform; // 不要改 root Canvas 的 anchor/sizeDelta，否则 rect 会变成 0，点位整体偏移半个屏幕

            _m3DotTex = CreateDotTexture();
            _m3DotTex.hideFlags = HideFlags.DontSave;
            _m3DotSprite = Sprite.Create(_m3DotTex, new Rect(0f, 0f, 32f, 32f), new Vector2(.5f, .5f), 100f);
            _m3DotSprite.hideFlags = HideFlags.DontSave;
            _m3SolidTex = CreateSolidTexture();
            _m3SolidTex.hideFlags = HideFlags.DontSave;
            _m3SolidSprite = Sprite.Create(_m3SolidTex, new Rect(0f, 0f, 4f, 4f), new Vector2(.5f, .5f), 100f);
            _m3SolidSprite.hideFlags = HideFlags.DontSave;
            _m3VizRail = _m3Probe.railViewport;
            _m3VizRuler = _m3Ruler != null ? _m3Ruler.rulerRt : null;

            AddM3AnchorMarker("ProbeEntry", "入射点(放置后)", new Color(0f, 1f, 1f, 1f), 20f, () => RailToScreen(_m3Probe.ScanStartLocal + GetM3ProbeEntryLocal()));
            AddM3AnchorMarker("Damage", "损伤点", new Color(1f, 0f, 0f, 1f), 20f, () => RailToScreen(_m3Probe.DamagePointInRail));
            AddM3AnchorMarker("ScanStart", "放置点(探头中心拖到这里)", new Color(1f, 1f, 0f, 1f), 22f, () => RailToScreen(_m3Probe.ScanStartLocal));
            AddM3AnchorMarker("ScanEnd", "扫描终点", new Color(1f, 0f, 1f, 1f), 16f, () => RailToScreen(_m3Probe.ScanEndLocal));
            if (_m3VizRuler != null && _m3Ruler != null)
            {
                AddM3AnchorMarker("RulerZero", "尺子0mm", new Color(0f, 1f, 0f, 1f), 18f, () => RulerToScreen(_m3Ruler.ZeroAnchorLocal));
                AddM3AnchorMarker("Ruler120", "尺子120mm", new Color(.4f, .6f, 1f, 1f), 16f, () => RulerToScreen(GetPrivateField<Vector2>(_m3Ruler, "_r120")));
                AddM3AnchorMarker("RulerSlot", "尺子槽点", new Color(1f, .6f, 0f, 1f), 16f, () => RulerToScreen(GetPrivateField<Vector2>(_m3Ruler, "_slot")));
                CreateM3RulerPlacementRect();
                AddM3AnchorMarker("RulerPositioningStart", "校角尺子放置位置(中心对准)", new Color(1f, 1f, 1f, 1f), 18f, () => RailToScreen(GetM3PositioningStartLocal()));
                AddM3AnchorMarker("RulerMeasureStart", "测量初始位(起点)", new Color(.7f, .9f, 1f, 1f), 14f, () => RailToScreen(GetM3MeasureStartLocal()));
            }
        }

        private void CreateM3RulerPlacementRect()
        {
            if (_m3Ruler == null || _m3OverlayRt == null || _m3OverlayCanvas == null || _m3SolidSprite == null) return;
            var go = new GameObject("~M3Anchor_RulerPlacementRect", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.hideFlags = HideFlags.DontSave;
            go.layer = _m3OverlayCanvas.gameObject.layer;
            var rt = (RectTransform)go.transform;
            rt.SetParent(_m3OverlayRt, false);
            rt.SetAsLastSibling();
            rt.anchorMin = rt.anchorMax = Vector2.zero;
            rt.pivot = new Vector2(.5f, .5f);
            rt.sizeDelta = _m3Ruler.measureSize;
            rt.anchoredPosition = RailToScreen(GetM3PositioningStartLocal());
            rt.localRotation = Quaternion.Euler(0f, 0f, _m3Ruler.positioningAngle);
            var img = go.GetComponent<Image>();
            img.sprite = _m3SolidSprite;
            img.color = new Color(1f, 1f, 1f, .18f);
            img.raycastTarget = false;
            _m3RulerPlacementRect = rt;
        }

        private Vector2 RailToScreen(Vector2 railLocal)
        {
            var vp = _m3Probe != null && _m3Probe.railViewport != null ? _m3Probe.railViewport : (_m3Ruler != null ? _m3Ruler.railViewport : null);
            return vp != null ? RectTransformUtility.WorldToScreenPoint(null, vp.TransformPoint(railLocal)) : Vector2.zero;
        }

        private Vector2 GetM3ProbeEntryLocal()
        {
            var m = typeof(M3ProbeDrag).GetMethod("EntryLocal", BindingFlags.NonPublic | BindingFlags.Instance);
            if (m != null) return (Vector2)m.Invoke(_m3Probe, null);
            return Vector2.zero;
        }

        private Vector2 RulerToScreen(Vector2 rulerLocal)
        {
            return _m3Ruler != null && _m3Ruler.rulerRt != null
                ? RectTransformUtility.WorldToScreenPoint(null, _m3Ruler.rulerRt.TransformPoint(rulerLocal))
                : Vector2.zero;
        }

        private void AddM3AnchorMarker(string name, string label, Color color, float size, Func<Vector2> getScreen)
        {
            var go = new GameObject("~M3Anchor_" + name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.hideFlags = HideFlags.DontSave;
            go.layer = _m3OverlayCanvas.gameObject.layer;
            var rt = (RectTransform)go.transform;
            rt.SetParent(_m3OverlayRt, false);
            rt.SetAsLastSibling();
            rt.anchorMin = rt.anchorMax = Vector2.zero; // Overlay Canvas 无 Scaler：1 单位 = 1 屏幕像素
            rt.pivot = new Vector2(.5f, .5f);
            rt.sizeDelta = new Vector2(size, size);
            rt.anchoredPosition = getScreen();
            var img = go.GetComponent<Image>();
            img.enabled = true;
            img.sprite = _m3DotSprite;
            img.color = color;
            img.raycastTarget = false;

            var marker = new M3AnchorMarker { rt = rt, getLocal = getScreen };
            var offset = new Vector2(size * .7f, size * .7f);
            var font = GetM3LabelFont();
            if (font != null)
            {
                var labelGo = new GameObject("~M3AnchorLabel_" + name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
                labelGo.hideFlags = HideFlags.DontSave;
                labelGo.layer = _m3OverlayCanvas.gameObject.layer;
                var lrt = (RectTransform)labelGo.transform;
                lrt.SetParent(_m3OverlayRt, false);
                lrt.SetAsLastSibling();
                lrt.anchorMin = lrt.anchorMax = Vector2.zero;
                lrt.pivot = new Vector2(0f, .5f);
                lrt.sizeDelta = new Vector2(140f, 26f);
                lrt.anchoredPosition = getScreen() + offset;
                var text = labelGo.GetComponent<Text>();
                text.font = font;
                text.text = label;
                text.fontSize = 18;
                text.color = Color.white;
                text.alignment = TextAnchor.MiddleLeft;
                text.horizontalOverflow = HorizontalWrapMode.Overflow;
                text.verticalOverflow = VerticalWrapMode.Overflow;
                text.raycastTarget = false;
                var outline = labelGo.AddComponent<Outline>();
                outline.effectColor = Color.black;
                outline.effectDistance = new Vector2(1f, -1f);
                marker.labelRt = lrt;
                marker.labelOffset = offset;
            }
            _m3AnchorMarkers.Add(marker);
        }

        private void RefreshM3AnchorViz()
        {
            foreach (var m in _m3AnchorMarkers)
            {
                if (m.rt == null) continue;
                var p = m.getLocal();
                m.rt.anchoredPosition = p;
                if (m.labelRt != null) m.labelRt.anchoredPosition = p + m.labelOffset;
            }
            if (_m3RulerPlacementRect != null && _m3Ruler != null)
            {
                _m3RulerPlacementRect.anchoredPosition = RailToScreen(GetM3PositioningStartLocal());
                _m3RulerPlacementRect.localRotation = Quaternion.Euler(0f, 0f, _m3Ruler.positioningAngle);
                _m3RulerPlacementRect.sizeDelta = _m3Ruler.measureSize;
            }
        }

        private void DestroyM3AnchorViz()
        {
            _m3AnchorMarkers.Clear();
            if (_m3OverlayCanvas != null)
            {
                if (Application.isPlaying) Destroy(_m3OverlayCanvas.gameObject);
                else DestroyImmediate(_m3OverlayCanvas.gameObject);
            }
            _m3OverlayCanvas = null; _m3OverlayRt = null; _m3RulerPlacementRect = null;
            if (_m3DotSprite != null)
            {
                if (Application.isPlaying) Destroy(_m3DotSprite);
                else DestroyImmediate(_m3DotSprite);
            }
            if (_m3DotTex != null)
            {
                if (Application.isPlaying) Destroy(_m3DotTex);
                else DestroyImmediate(_m3DotTex);
            }
            if (_m3SolidSprite != null)
            {
                if (Application.isPlaying) Destroy(_m3SolidSprite);
                else DestroyImmediate(_m3SolidSprite);
            }
            if (_m3SolidTex != null)
            {
                if (Application.isPlaying) Destroy(_m3SolidTex);
                else DestroyImmediate(_m3SolidTex);
            }
            _m3DotSprite = null; _m3DotTex = null; _m3SolidSprite = null; _m3SolidTex = null; _m3VizRail = null; _m3VizRuler = null;
        }

        private Font GetM3LabelFont()
        {
            if (_m3LabelFont == null)
            {
                try { _m3LabelFont = Font.CreateDynamicFontFromOSFont("Microsoft YaHei", 18); }
                catch { _m3LabelFont = null; }
                if (_m3LabelFont == null)
                {
                    try { _m3LabelFont = Font.CreateDynamicFontFromOSFont("SimHei", 18); }
                    catch { _m3LabelFont = null; }
                }
                if (_m3LabelFont == null) _m3LabelFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }
            return _m3LabelFont;
        }

        private static Texture2D CreateDotTexture()
        {
            const int n = 32;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
            var c = (n - 1) * .5f;
            var r = n * .5f;
            for (var y = 0; y < n; y++)
            {
                for (var x = 0; x < n; x++)
                {
                    var dx = x - c; var dy = y - c;
                    var d = Mathf.Sqrt(dx * dx + dy * dy) / r;
                    var a = d <= .85f ? 1f : d <= 1f ? Mathf.Lerp(1f, 0f, (d - .85f) / .15f) : 0f;
                    var core = d <= .65f ? 1f : 0f; // 白色中心（被 Image.color 染成锚点色）+ 黑色外环
                    tex.SetPixel(x, y, new Color(core, core, core, a));
                }
            }
            tex.Apply();
            return tex;
        }

        private Vector2 GetM3PositioningStartLocal()
        {
            return _m3Ruler != null && _m3Ruler.railViewport != null ? M3NormalizedToRailLocal(_m3Ruler, _m3Ruler.positioningStart) : Vector2.zero;
        }

        private Vector2 GetM3MeasureStartLocal()
        {
            return _m3Ruler != null && _m3Ruler.railViewport != null ? M3NormalizedToRailLocal(_m3Ruler, _m3Ruler.measureStartLocal) : Vector2.zero;
        }

        private Vector2 M3NormalizedToRailLocal(M3RulerDrag ruler, Vector2 normalized)
        {
            if (ruler == null || ruler.railViewport == null) return normalized;
            var rect = ruler.railViewport.rect;
            var pivot = ruler.railViewport.pivot;
            return new Vector2((normalized.x - pivot.x) * rect.width, (normalized.y - pivot.y) * rect.height);
        }

        private Vector2 M3RailLocalToNormalized(M3RulerDrag ruler, Vector2 local)
        {
            if (ruler == null || ruler.railViewport == null) return local;
            var rect = ruler.railViewport.rect;
            var pivot = ruler.railViewport.pivot;
            if (rect.width < 0.0001f || rect.height < 0.0001f) return local;
            return new Vector2(local.x / rect.width + pivot.x, local.y / rect.height + pivot.y);
        }

        private void SetM3ScanStartLocal(Vector2 local)
        {
            if (_m3Probe == null || _m3Probe.railViewport == null) return;
            var ppm = _m3Probe.PixelsPerMm;
            if (ppm <= 0.01f) return;
            var entry = GetM3ProbeEntryLocal();
            var damage = _m3Probe.DamagePointInRail;
            _m3Probe.scanStartMm = Mathf.Clamp((damage.x - (local.x + entry.x)) / ppm, 0f, 1000f);
            _m3Probe.scanStartY = local.y + entry.y;
        }

        private void CapturePending()
        {
            _pendingApplies.Clear();
            if (_m2Probe != null)
            {
                var p = new PendingApply { ComponentType = typeof(M2ProbeDrag) };
                p.Fields["probeEntryLocal"] = _m2Probe.probeEntryLocal;
                p.Fields["damageUv"] = _m2Probe.damageUv;
                p.Fields["startLocal"] = _m2Probe.startLocal;
                p.Fields["probeBaseAngleDeg"] = _m2Probe.probeBaseAngleDeg;
                p.Fields["beamBaseAngleDeg"] = _m2Probe.beamBaseAngleDeg;
                p.Fields["beamLengthZeroMm"] = _m2Probe.beamLengthZeroMm;
                p.Fields["visualTiltAtTarget"] = _m2Probe.visualTiltAtTarget;
                _pendingApplies.Add(p);
            }
            if (_m2Ruler != null)
            {
                var p = new PendingApply { ComponentType = typeof(M2RulerDrag) };
                p.Fields["slotUv"] = _m2Ruler.slotUv;
                p.Fields["zeroUv"] = _m2Ruler.zeroUv;
                p.Fields["ruler110Uv"] = _m2Ruler.ruler110Uv;
                p.Fields["measureStartLocal"] = _m2Ruler.measureStartLocal;
                p.Fields["measureAngleDeg"] = _m2Ruler.measureAngleDeg;
                p.Fields["measureOffset"] = _m2Ruler.measureOffset;
                p.Fields["pointTolerancePx"] = _m2Ruler.pointTolerancePx;
                p.Fields["angleToleranceDeg"] = _m2Ruler.angleToleranceDeg;
                p.Fields["retractTolerancePx"] = _m2Ruler.retractTolerancePx;
                _pendingApplies.Add(p);
            }
            if (_m3Probe != null)
            {
                var p = new PendingApply { ComponentType = typeof(M3ProbeDrag) };
                p.Fields["probeEntryLocal"] = _m3Probe.probeEntryLocal;
                p.Fields["placementTolerancePx"] = _m3Probe.placementTolerancePx;
                p.Fields["scanStartMm"] = _m3Probe.scanStartMm;
                p.Fields["scanEndMm"] = _m3Probe.scanEndMm;
                p.Fields["scanStartY"] = _m3Probe.scanStartY;
                p.Fields["visualTiltAtTarget"] = _m3Probe.visualTiltAtTarget;
                p.Fields["initialAngleDeg"] = _m3Probe.initialAngleDeg;
                p.Fields["beamColor"] = _m3Probe.beamColor;
                p.Fields["beamDetectedColor"] = _m3Probe.beamDetectedColor;
                p.Fields["settleDuration"] = _m3Probe.settleDuration;
                p.Fields["beamLengthZeroMm"] = _m3Probe.beamLengthZeroMm;
                p.Fields["beamWidthPx"] = _m3Probe.beamWidthPx;
                p.Fields["beamHitRadiusPx"] = _m3Probe.beamHitRadiusPx;
                if (_m3Probe.flow != null) p.Fields["flow.targetAngle"] = _m3Probe.flow.targetAngle;
                _pendingApplies.Add(p);
            }
            if (_m3Ruler != null)
            {
                var p = new PendingApply { ComponentType = typeof(M3RulerDrag) };
                p.Fields["measureSize"] = _m3Ruler.measureSize;
                p.Fields["positioningStart"] = _m3Ruler.positioningStart;
                p.Fields["measureStartLocal"] = _m3Ruler.measureStartLocal;
                p.Fields["zeroUv"] = _m3Ruler.zeroUv;
                p.Fields["ruler120Uv"] = _m3Ruler.ruler120Uv;
                p.Fields["slotUv"] = _m3Ruler.slotUv;
                p.Fields["snapTolerance"] = _m3Ruler.snapTolerance;
                p.Fields["angleToleranceDeg"] = _m3Ruler.angleToleranceDeg;
                p.Fields["pointTolerancePx"] = _m3Ruler.pointTolerancePx;
                p.Fields["measureProjectTolerancePx"] = _m3Ruler.measureProjectTolerancePx;
                p.Fields["retractTolerancePx"] = _m3Ruler.retractTolerancePx;
                p.Fields["positioningAngle"] = _m3Ruler.positioningAngle;
                p.Fields["measureAngleDeg"] = _m3Ruler.measureAngleDeg;
                _pendingApplies.Add(p);
            }
        }

        private void ApplyPendingToScene()
        {
            var dirtyScenes = new HashSet<Scene>();
            foreach (var pending in _pendingApplies)
            {
                var target = FindFirstComponent(pending.ComponentType);
                if (target == null) continue;
                Undo.RecordObject(target, "PlayDebugger Apply");
                foreach (var kv in pending.Fields)
                    SetFieldByPath(target, kv.Key, kv.Value);
                EditorUtility.SetDirty(target);
                EditorSceneManager.MarkSceneDirty(target.gameObject.scene);
                if (target.gameObject.scene.IsValid() && target.gameObject.scene.isLoaded) dirtyScenes.Add(target.gameObject.scene);
            }
            _pendingApplies.Clear();
            // 自动保存场景文件（老板要求点一下完成；保存后 M2/M3 Scene 哈希会变，属老板授权的手工调参操作）。
            foreach (var s in dirtyScenes) EditorSceneManager.SaveScene(s);
            if (dirtyScenes.Count > 0) Debug.Log($"[PlayDebugger] 已写回并保存场景：{string.Join(", ", dirtyScenes)}，重新进入 Play 即生效。");
        }

        private static Component FindFirstComponent(Type type)
        {
            if (type == typeof(M2ProbeDrag)) return UnityEngine.Object.FindFirstObjectByType<M2ProbeDrag>();
            if (type == typeof(M2RulerDrag)) return UnityEngine.Object.FindFirstObjectByType<M2RulerDrag>();
            if (type == typeof(M3ProbeDrag)) return UnityEngine.Object.FindFirstObjectByType<M3ProbeDrag>();
            if (type == typeof(M3RulerDrag)) return UnityEngine.Object.FindFirstObjectByType<M3RulerDrag>();
            return null;
        }

        private static void SetFieldByPath(object target, string path, object value)
        {
            var parts = path.Split('.');
            object obj = target;
            for (var i = 0; i < parts.Length - 1; i++)
            {
                if (obj == null) return;
                var f = obj.GetType().GetField(parts[i], BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (f == null) return;
                obj = f.GetValue(obj);
            }
            if (obj == null) return;
            var last = obj.GetType().GetField(parts[parts.Length - 1], BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (last != null)
            {
                if (!ReferenceEquals(obj, target) && obj is UnityEngine.Object)
                    Undo.RecordObject((UnityEngine.Object)obj, "PlayDebugger Apply");
                last.SetValue(obj, value);
                if (!ReferenceEquals(obj, target) && obj is UnityEngine.Object)
                    EditorUtility.SetDirty((UnityEngine.Object)obj);
            }
        }

        private class PendingApply
        {
            public Type ComponentType;
            public readonly Dictionary<string, object> Fields = new Dictionary<string, object>();
        }

        private static Texture2D CreateSolidTexture()
        {
            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            var c = Color.white;
            for (var y = 0; y < 4; y++)
                for (var x = 0; x < 4; x++)
                    tex.SetPixel(x, y, c);
            tex.Apply();
            return tex;
        }

        private static T GetPrivateField<T>(object target, string field)
        {
            var f = target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance);
            if (f != null) return (T)f.GetValue(target);
            return default;
        }

        private class M3AnchorMarker
        {
            public RectTransform rt;
            public RectTransform labelRt;
            public Vector2 labelOffset;
            public Func<Vector2> getLocal;
        }

        private static void InvokePrivate(object target, string method)
        {
            var m = target.GetType().GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance);
            if (m != null) m.Invoke(target, null);
        }

        private static void SetPrivateField(object target, string field, object value)
        {
            var f = target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance);
            if (f != null) f.SetValue(target, value);
        }
    }
}

// compile-check 1786953905
