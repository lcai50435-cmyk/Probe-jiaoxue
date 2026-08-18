using UnityEngine;
using UnityEngine.UI;

namespace M2
{
    /// <summary>探伤仪屏风格程序化波形（UGUI Graphic）：深色底 + 大/小刻度"+"网格 + 常驻绿色始波（发射脉冲尖峰）+ 底部绿色锯齿噪声基线 +
    /// 伤损波联动（160mm 短波 → 123mm 最高 → 120mm 检出锁定）。纯状态驱动无协程，QA/Modal 暂停天然冻结。
    /// M2 Scene 仍可序列化旧参数；M3/M4 等后续场景按需显式配置。</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public class M2WaveformFx : Graphic
    {
        [Header("刻度合同")] public float scanMinMm = 0f, scanMaxMm = 200f;
        public int majorDivisions = 5;
        [Header("伤损波联动")] public float appearMm = 160f, peakMm = 123f, stopMm = 120f;
        public float startStrength = .08f, peakStrength = .78f, pulseWidth = .075f;
        [Tooltip("始波峰顶高度（波形区高比例，0~1；调小可进一步防贴顶）")]
        public float startPeakHeight = .85f;
        [Header("外观")] public Color startColor = new Color(.35f, .95f, .5f);
        public Color guideColor = new Color(.3f, .85f, .95f);
        public Color gridColor = new Color(.6f, .9f, .6f, .25f);
        public Color baselineColor = new Color(.35f, .8f, .45f, .55f);
        public Color bgColor = new Color(.05f, .06f, .07f, 1f);
        public Color axisColor = new Color(0f, .47f, .78f, 1f);        // 参考图：左侧纵轴区蓝色
        public Color axisRedColor = new Color(.86f, .12f, .12f, 1f);   // 参考图：底部横轴区红色
        public Color subGridColor = new Color(.62f, .62f, .62f, .6f);  // 小刻度"+"中灰
        public float lineThickness = 2f;
        [Tooltip("伤损波噪声幅度（渲染区高比例，随峰高缩放）")]
        public float noiseAmp = .04f;
        public float Strength => _strength;   // 伤损波峰高（0~1），供烟测断言
        public float PeakU => _peakU;         // 伤损波 X 位置（0~1），供烟测断言
        public bool OutOfBounds { get; private set; } // Play 实测：任一绘制顶点超出窗口 rect（烟测断言用）
        private float _strength = .08f, _peakU = .8f;

        protected override void Awake() { raycastTarget = false; SetDistanceMm(appearMm); } // 初态=appear 处短波（M2 150 / M3 160，跟随 Scene 序列化参数）

        // 距离联动：>160 无伤损波；160→123 短波长高；123→120 保持最高左移；<120 检出锁定
        public void SetDistanceMm(float mm)
        {
            var t = mm >= peakMm ? Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(appearMm, peakMm, mm)) : 1f; // A：高度平滑（先缓后快再缓）
            _strength = mm > appearMm ? 0f : Mathf.Lerp(startStrength, peakStrength, t);
            _peakU = Mathf.InverseLerp(scanMinMm, scanMaxMm, Mathf.Clamp(mm, stopMm, appearMm));
            SetAllDirty();
        }

        public void ResetWave(float mm = 160f) => SetDistanceMm(mm);

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var r = rectTransform.rect;
            if (r.width < 2f || r.height < 2f) return;
            Fill(vh, r, bgColor);
            // 刻度条（参考图：左侧蓝色纵轴区 + 底部红色横轴区）
            var axisW = r.width * .11f; var axisH = r.height * .075f; // 蓝条加宽（容纳纵轴 100.0%）
            Fill(vh, new Rect(r.xMin, r.yMin, axisW, r.height), axisColor);
            Fill(vh, new Rect(r.xMin, r.yMin, r.width, axisH), axisRedColor);
            // 波形区（蓝条右侧、红条上方）+ 绘制内缩区（四周留白，防网格"+"或波峰贴边溢出）
            var d = new Rect(r.xMin + axisW, r.yMin + axisH, r.width - axisW, r.height - axisH);
            var g = new Rect(d.xMin + d.width * .015f, d.yMin + d.height * .04f, d.width * .97f, d.height * .92f);
            DrawGrid(vh, g);
            DrawNoise(vh, g);
            var pulseW = pulseWidth * g.width;
            // 始波：紧贴波形区左缘，无陡升前缘（直接从峰顶向下衰减，老板 2026-08-18）；伤损波保留陡升竖线（同形合同不改）
            DrawPulse(vh, g, g.xMin + pulseW * .5f, startPeakHeight, pulseW, 0f, false);
            if (_strength > .005f)
            {
                var defectCenterX = r.xMin + _peakU * r.width; // 与 Scene 中横轴刻度（按 0~200mm 比例锚定）对齐
                DrawPulse(vh, g, defectCenterX, _strength, pulseW, noiseAmp * _strength, true);
            }
            // Play 实测越界检测（烟测断言）：任一顶点超出窗口 rect 即标记
            OutOfBounds = false;
            for (var i = 0; i < vh.currentVertCount; i++)
            {
                UIVertex v = default; vh.PopulateUIVertex(ref v, i);
                var p = v.position;
                if (p.x < r.xMin - 1f || p.x > r.xMax + 1f || p.y < r.yMin - 1f || p.y > r.yMax + 1f) { OutOfBounds = true; break; }
            }
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
            const int sub = 4; // 每大格内小刻度数（参考图：40mm 大格内每 10mm）
            for (var i = 0; i <= majorDivisions; i++)
            for (var j = 0; j <= majorDivisions; j++)
                Plus(vh, r, i / (float)majorDivisions, j / (float)majorDivisions, 3.5f, 2f, gridColor); // 大刻度"+"粗长浅色
            var n = majorDivisions * sub;
            for (var i = 1; i < n; i++)
            {
                if (i % sub == 0) continue;
                for (var j = 1; j < n; j++)
                {
                    if (j % sub == 0) continue;
                    Plus(vh, r, i / (float)n, j / (float)n, 2f, 1f, subGridColor); // 小刻度"+"细短中灰
                }
            }
        }
        private static void Plus(VertexHelper vh, Rect r, float u, float v, float half, float w, Color c)
        {
            var x = r.xMin + u * r.width; var y = r.yMin + v * r.height;
            Line(vh, new Vector2(x - half, y), new Vector2(x + half, y), c, w);
            Line(vh, new Vector2(x, y - half), new Vector2(x, y + half), c, w);
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

        private void DrawPulse(VertexHelper vh, Rect r, float centerX, float heightFrac, float width, float noiseAmpFrac, bool steepRise = true)
        {
            var x0 = centerX - width * .5f;
            var w = width;
            var baseY = r.yMin + r.height * .03f;
            var peakY = r.yMin + r.height * heightFrac;
            // 伤损波：从基线陡升前缘（近垂直竖线）再衰减；始波（steepRise=false）：从峰顶直接衰减，无竖线
            var prev = new Vector2(x0, steepRise ? baseY : peakY);
            for (var i = 1; i <= 48; i++)
            {
                var u = i / 48f;
                float envelope;
                if (steepRise && u < .2f) envelope = u / .2f;                                    // 陡升前缘（图像中近垂直的尖峰前沿）
                else envelope = Mathf.Exp(-3.2f * (u - (steepRise ? .2f : 0f)) / (steepRise ? .8f : 1f)); // 快速指数衰减，不再余弦振荡
                var y = baseY + (peakY - baseY) * envelope;
                if (noiseAmpFrac > 0f) y += Noise(u * 3.7f + .5f) * noiseAmpFrac * r.height * envelope;
                y = Mathf.Clamp(y, r.yMin, r.yMax);                                  // 防止纹波超出波形窗口
                var cur = new Vector2(x0 + u * w, y);
                Line(vh, prev, cur, startColor, lineThickness);
                prev = cur;
            }
        }
    }
}
