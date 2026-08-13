using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace M2
{
    /// <summary>
    /// 程序化单条实时波形（UGUI Graphic）：由归一化距离驱动平直基线/波峰生长/峰值/峰后下降。
    /// 只消费距离与显示参数，不拥有流程状态；距离或尺寸变化时重建顶点，避免每帧分配。
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public class M2WaveformGraphic : Graphic
    {
        [Header("距离契约（Inspector 配置）")]
        [Tooltip("扫描起点距离（mm）")]
        public float scanStartMm = 150f;
        public float scanEndMm = 100f;
        [Tooltip("波峰开始生长距离（mm）")]
        public float growthStartMm = 125f;
        [Tooltip("峰值区间上界（mm）")]
        public float peakWindowMaxMm = 112f;
        [Tooltip("峰值区间下界（mm）")]
        public float peakWindowMinMm = 108f;
        [Tooltip("峰值目标距离（mm）")]
        public float peakTargetMm = 110f;

        [Header("外观")]
        public float lineThickness = 3f;
        public int segments = 64;
        public Color waveColor = new Color(0.3f, 1f, 0.5f);

        private float _currentMm = 150f;

        protected override void OnEnable()
        {
            base.OnEnable();
            if (_currentMm > 0f) SetVerticesDirty();
        }

        public void SetDistanceMm(float mm)
        {
            if (Mathf.Approximately(_currentMm, mm)) return;
            _currentMm = mm;
            SetAllDirty();
        }

        public void ResetWave(float mm = 150f)
        {
            _currentMm = mm;
            SetAllDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var rect = rectTransform.rect;
            if (rect.width < 2f || rect.height < 2f) return;

            var pts = BuildPoints(rect, PeakStrength(_currentMm));
            BuildMesh(vh, pts, rect);
        }

        private List<Vector3> BuildPoints(Rect rect, float strength)
        {
            var pts = new List<Vector3>(segments + 1);
            for (int i = 0; i <= segments; i++)
            {
                var u = i / (float)segments;
                var x = rect.xMin + u * rect.width;
                var y = Amplitude(u, strength);
                pts.Add(new Vector3(x, rect.yMin + y * rect.height, 0f));
            }
            return pts;
        }

        private float PeakStrength(float mm)
        {
            if (mm >= growthStartMm) return 0f;
            if (mm > peakWindowMaxMm)
                return Mathf.SmoothStep(0f, .8f, Mathf.InverseLerp(growthStartMm, peakWindowMaxMm, mm));
            if (mm >= peakTargetMm)
                return Mathf.SmoothStep(.8f, 1f, Mathf.InverseLerp(peakWindowMaxMm, peakTargetMm, mm));
            if (mm >= peakWindowMinMm)
                return Mathf.SmoothStep(1f, .8f, Mathf.InverseLerp(peakTargetMm, peakWindowMinMm, mm));
            return Mathf.SmoothStep(.8f, 0f, Mathf.InverseLerp(peakWindowMinMm, scanEndMm, mm));
        }

        private float Amplitude(float u, float strength)
        {
            var baseAmp = 0.06f;
            var peakU = Mathf.InverseLerp(scanStartMm, scanEndMm, peakTargetMm);
            var envelope = Mathf.Clamp01(1f - Mathf.Abs(u - peakU) * 4f);
            return baseAmp + (0.85f - baseAmp) * envelope * strength;
        }

        private void BuildMesh(VertexHelper vh, List<Vector3> pts, Rect rect)
        {
            var color = waveColor;
            for (int i = 0; i < pts.Count - 1; i++)
            {
                var a = pts[i];
                var b = pts[i + 1];
                var dir = (b - a).normalized;
                var normal = new Vector3(-dir.y, dir.x, 0f) * (lineThickness * 0.5f);
                var v0 = a + normal;
                var v1 = a - normal;
                var v2 = b - normal;
                var v3 = b + normal;
                int idx = vh.currentVertCount;
                vh.AddVert(v0, color, Vector2.zero);
                vh.AddVert(v1, color, Vector2.zero);
                vh.AddVert(v2, color, Vector2.zero);
                vh.AddVert(v3, color, Vector2.zero);
                vh.AddTriangle(idx, idx + 1, idx + 2);
                vh.AddTriangle(idx, idx + 2, idx + 3);
            }
        }
    }
}
