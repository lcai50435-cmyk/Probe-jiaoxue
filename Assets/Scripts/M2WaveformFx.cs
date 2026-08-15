using UnityEngine;
using UnityEngine.UI;

namespace M2
{
    /// <summary>M2 探伤仪屏风格程序化波形（UGUI Graphic，仅 M2 运行时挂载、不写回冻结 Scene）：
    /// 深色底 + 仅大刻度主网格 + 常驻绿色始波（发射脉冲 + 青绿竖线）+ 底部绿色锯齿噪声基线 +
    /// 伤损波联动（150mm 短波 → 115mm 最高 → 110mm 检出锁定）。纯状态驱动无协程，QA/Modal 暂停天然冻结；
    /// M3 旧 M2WaveformGraphic 样式与配置零改动。</summary>
    public class M2WaveformFx : Graphic
    {
        [Header("刻度合同")] public float scanMinMm = 0f, scanMaxMm = 200f;
        public int majorDivisions = 5;
        [Header("伤损波联动")] public float appearMm = 150f, peakMm = 115f, stopMm = 110f;
        public float startStrength = .08f, peakStrength = .78f, pulseWidth = .075f;
        [Header("外观")] public Color startColor = new Color(.35f, .95f, .5f);
        public Color guideColor = new Color(.3f, .85f, .95f);
        public Color gridColor = new Color(.6f, .9f, .6f, .25f);
        public Color baselineColor = new Color(.35f, .8f, .45f, .55f);
        public Color bgColor = new Color(.05f, .06f, .07f, 1f);
        public float lineThickness = 2f;
        public float Strength => _strength;   // 伤损波峰高（0~1），供烟测断言
        public float PeakU => _peakU;         // 伤损波 X 位置（0~1），供烟测断言
        private float _strength = .08f, _peakU = .75f;

        protected override void Awake() { raycastTarget = false; SetDistanceMm(150f); }

        // 距离联动：>150 无伤损波；150→115 短波长高；115→110 保持最高左移；<110 检出锁定
        public void SetDistanceMm(float mm)
        {
            var t = mm >= peakMm ? Mathf.InverseLerp(appearMm, peakMm, mm) : 1f;
            _strength = mm > appearMm ? 0f : Mathf.Lerp(startStrength, peakStrength, t);
            _peakU = Mathf.InverseLerp(scanMinMm, scanMaxMm, Mathf.Clamp(mm, stopMm, appearMm));
            SetAllDirty();
        }

        public void ResetWave(float mm = 150f) => SetDistanceMm(mm);

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var r = rectTransform.rect;
            if (r.width < 2f || r.height < 2f) return;
            Fill(vh, r, bgColor);
            DrawGrid(vh, r);
            DrawNoise(vh, r);
            Line(vh, new Vector2(r.xMin + r.width * .02f, r.yMin), new Vector2(r.xMin + r.width * .02f, r.yMax), guideColor, 1.5f);
            DrawPulse(vh, r, pulseWidth * .5f, .95f, pulseWidth);        // 常驻始波（X 0~7.5%，峰顶 95%）
            if (_strength > .005f) DrawPulse(vh, r, _peakU, _strength, pulseWidth); // 伤损波（同形同色）
        }

        private static void Fill(VertexHelper vh, Rect r, Color c)
        {
            int i = vh.currentVertCount;
            vh.AddVert(new Vector3(r.xMin, r.yMin), c, Vector2.zero);
            vh.AddVert(new Vector3(r.xMax, r.yMin), c, Vector2.zero);
            vh.AddVert(new Vector3(r.xMax, r.yMax), c, Vector2.zero);
            vh.AddVert(new Vector3(r.xMin, r.yMax), c, Vector2.zero);
            vh.AddTriangle(i, i + 1, i + 2); vh.AddTriangle(i, i + 2, i + 3);
        }

        private static void Line(VertexHelper vh, Vector2 a, Vector2 b, Color c, float w)
        {
            var d = (b - a).normalized; var n = new Vector2(-d.y, d.x) * (w * .5f);
            int i = vh.currentVertCount;
            vh.AddVert(a + n, c, Vector2.zero); vh.AddVert(a - n, c, Vector2.zero);
            vh.AddVert(b - n, c, Vector2.zero); vh.AddVert(b + n, c, Vector2.zero);
            vh.AddTriangle(i, i + 1, i + 2); vh.AddTriangle(i, i + 2, i + 3);
        }

        private void DrawGrid(VertexHelper vh, Rect r)
        {
            for (var i = 0; i <= majorDivisions; i++)
            {
                var x = r.xMin + r.width * i / majorDivisions;
                var y = r.yMin + r.height * i / majorDivisions;
                Line(vh, new Vector2(x, r.yMin), new Vector2(x, r.yMax), gridColor, 1f);
                Line(vh, new Vector2(r.xMin, y), new Vector2(r.xMax, y), gridColor, 1f);
            }
        }

        private void DrawNoise(VertexHelper vh, Rect r)
        {
            var baseY = r.yMin + r.height * .03f;
            var amp = r.height * .015f;
            var prev = new Vector2(r.xMin, baseY + Noise(0f) * amp);
            for (var i = 1; i <= 64; i++)
            {
                var u = i / 64f;
                var cur = new Vector2(r.xMin + u * r.width, baseY + Noise(u) * amp);
                Line(vh, prev, cur, baselineColor, 1f);
                prev = cur;
            }
        }

        private static float Noise(float u) => Mathf.Sin(u * 40f) * .6f + Mathf.Sin(u * 91f) * .4f + Mathf.Sin(u * 7f) * .3f;

        private void DrawPulse(VertexHelper vh, Rect r, float centerU, float heightFrac, float widthFrac)
        {
            var x0 = r.xMin + (centerU - widthFrac * .5f) * r.width;
            var w = widthFrac * r.width;
            var baseY = r.yMin + r.height * .03f;
            var peakY = r.yMin + r.height * heightFrac;
            var prev = new Vector2(x0, baseY);
            for (var i = 1; i <= 48; i++)
            {
                var u = i / 48f;
                float y;
                if (u < .2f) y = baseY + (peakY - baseY) * (u / .2f);
                else { var t = (u - .2f) / .8f; y = baseY + (peakY - baseY) * Mathf.Exp(-2.2f * t) * Mathf.Cos(2f * Mathf.PI * 2.5f * t); }
                var cur = new Vector2(x0 + u * w, y);
                Line(vh, prev, cur, startColor, lineThickness);
                prev = cur;
            }
        }
    }
}
