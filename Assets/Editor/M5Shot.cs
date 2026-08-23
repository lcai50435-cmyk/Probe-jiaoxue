using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace M5.EditorTools
{
    /// <summary>
    /// M5 三视口截图工具：临时将 Canvas 切到 Screen Space Camera，用 RenderTexture 渲染
    /// 指定分辨率后保存 PNG（不依赖 GameView 内部 API，batchmode 可用）。
    /// 输出：Logs/m5-shot_1920x1080.png / _1280x720.png / _2436x1125.png
    /// 保存前断言像素存在颜色差异（纯色图直接抛错）；finally 恢复 Canvas 状态，不保存 Scene。
    /// </summary>
    public static class M5Shot
    {
        private const string ScenePath = "Assets/Settings/Scenes/M5.unity";
        private static readonly (int w, int h, string name)[] Targets =
        {
            (1920, 1080, "1920x1080"),
            (1280, 720, "1280x720"),
            (2436, 1125, "2436x1125"),
        };

        [MenuItem("Tools/M5/Capture Review Shots")]
        public static void CaptureAll()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var canvas = UnityEngine.Object.FindFirstObjectByType<Canvas>();
            var scaler = canvas != null ? canvas.GetComponent<CanvasScaler>() : null;
            if (canvas == null || scaler == null)
                throw new InvalidOperationException("[M5Shot] 未找到 Canvas/CanvasScaler");
            var savedMode = canvas.renderMode;
            var savedCam = canvas.worldCamera;
            var canvasRt = canvas.GetComponent<RectTransform>();
            var savedScale = canvasRt.localScale;
            var savedPosition = canvasRt.localPosition;
            var savedPivot = canvasRt.pivot;
            var savedSize = canvasRt.sizeDelta;
            var savedScalerEnabled = scaler != null && scaler.enabled;

            var camGo = new GameObject("M5ShotCam");
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(.925f, .935f, .945f);
            try
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = cam;
                foreach (var (w, h, name) in Targets)
                {
                    canvasRt.localScale = Vector3.one;
                    canvasRt.localPosition = Vector3.zero;
                    canvasRt.pivot = new Vector2(.5f, .5f);
                    canvasRt.sizeDelta = new Vector2(1920f, 1080f);
                    if (scaler != null) scaler.enabled = false; // 截图时禁用 Scaler，逻辑画布固定 1920x1080
                    var rt = new RenderTexture(w, h, 24);
                    cam.targetTexture = rt;
                    cam.Render();
                    var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
                    var prev = RenderTexture.active;
                    RenderTexture.active = rt;
                    tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                    tex.Apply();
                    RenderTexture.active = prev;
                    cam.targetTexture = null;
                    rt.Release();
                    if (!HasColorVariation(tex))
                        throw new InvalidOperationException($"[M5Shot] {name} 纯色/无差异，截图失败");
                    var png = tex.EncodeToPNG();
                    var dir = "Logs";
                    if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                    var path = Path.Combine(dir, $"m5-shot_{name}.png");
                    File.WriteAllBytes(path, png);
                    Debug.Log($"[M5Shot] {name} 已保存：{path}（{w}x{h}，差异像素通过）");
                    UnityEngine.Object.DestroyImmediate(tex);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(camGo);
                canvas.renderMode = savedMode;
                canvas.worldCamera = savedCam;
                canvasRt.localScale = savedScale;
                canvasRt.localPosition = savedPosition;
                canvasRt.pivot = savedPivot;
                canvasRt.sizeDelta = savedSize;
                if (scaler != null) scaler.enabled = savedScalerEnabled;
                // 不保存 Scene
            }
        }

        /// <summary>采样像素断言存在颜色差异（纯色图直接失败，不报告“已保存”）。</summary>
        private static bool HasColorVariation(Texture2D tex)
        {
            var w = tex.width; var h = tex.height;
            var a = tex.GetPixel(0, 0);
            var b = tex.GetPixel(w - 1, h - 1);
            var c = tex.GetPixel(w / 2, h / 2);
            var d = tex.GetPixel(w / 4, h / 3);
            return Vector3.Distance(new Vector3(a.r, a.g, a.b), new Vector3(b.r, b.g, b.b)) > .02f ||
                   Vector3.Distance(new Vector3(a.r, a.g, a.b), new Vector3(c.r, c.g, c.b)) > .02f ||
                   Vector3.Distance(new Vector3(c.r, c.g, c.b), new Vector3(d.r, d.g, d.b)) > .02f;
        }
    }
}
