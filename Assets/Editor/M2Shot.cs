using System;
using System.IO;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace M2.EditorTools
{
    /// <summary>
    /// 里程碑一布局截图工具：临时将 Canvas 切到 Screen Space Camera，用 RenderTexture
    /// 渲染指定分辨率后保存 PNG（不依赖 GameView 内部 API，batchmode 可用）。
    /// 用法：Unity -batchmode -projectPath ... -executeMethod M2.EditorTools.M2Shot.CaptureAll -logFile ...
    /// 输出：Logs/m2-shot_1920x1080.png / _1280x720.png / _2436x1125.png
    /// </summary>
    public static class M2Shot
    {
        private const string ScenePath = "Assets/Settings/Scenes/M2.unity";
        private static readonly (int w, int h, string name)[] Targets =
        {
            (1920, 1080, "1920x1080"),
            (1280, 720, "1280x720"),
            (2436, 1125, "2436x1125"),
        };

        [MenuItem("Tools/M2/Capture Review Shots")]
        public static void CaptureAll()
        {
            var sceneHashBefore = Sha256(ScenePath);
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var canvas = UnityEngine.Object.FindFirstObjectByType<Canvas>();
            var scaler = canvas != null ? canvas.GetComponent<CanvasScaler>() : null;
            if (canvas == null || scaler == null)
                throw new InvalidOperationException("[M2Shot] 未找到 Canvas/CanvasScaler");
            var savedMode = canvas.renderMode;
            var savedCam = canvas.worldCamera;
            var canvasRt = canvas.GetComponent<RectTransform>();
            var savedScale = canvasRt.localScale;
            var savedPosition = canvasRt.localPosition;
            var savedPivot = canvasRt.pivot;
            var savedSize = canvasRt.sizeDelta;
            var savedScalerEnabled = scaler != null && scaler.enabled;

            // Editor 截图验证：手动触发 M2 波形初始绘制（无 Play 时 Awake 不执行）
            var waveGraphic = UnityEngine.Object.FindFirstObjectByType<M2WaveformGraphic>();
            if (waveGraphic != null)
            {
                waveGraphic.ResetWave(150f); // 强制重建（SetAllDirty）
                // 直接调用 OnPopulateMesh 验证顶点产出（Editor 截图通道）
                var helper = new VertexHelper();
                var methods = waveGraphic.GetType().GetMethods(
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                System.Reflection.MethodInfo method = null;
                foreach (var m in methods)
                {
                    if (m.Name == "OnPopulateMesh")
                    {
                        var ps = m.GetParameters();
                        if (ps.Length == 1 && ps[0].ParameterType == typeof(VertexHelper)) { method = m; break; }
                    }
                }
                if (method != null)
                {
                    method.Invoke(waveGraphic, new object[] { helper });
                    Debug.Log($"[M2Shot] wave vertices={helper.currentVertCount}");
                }
            }

            // Editor 未播放时数字人 RawImage 无视频帧（LumaKey 材质下显示为白块）：
            // 截图期间临时禁用该 Graphic，finally 中恢复；不替换组件、素材或保存场景。
            // 完整人物表现由 Play Mode 截图/人工检查验收。
            var stageRaw = FindIncludingInactive(canvas.transform, "DigitalHumanStage")?
                .transform.Find("FullBodyView")?.GetComponent<RawImage>();
            var savedRawEnabled = stageRaw != null && stageRaw.enabled;
            if (stageRaw != null) stageRaw.enabled = false;

            var ruler = UnityEngine.Object.FindFirstObjectByType<M2RulerDrag>(FindObjectsInactive.Include);
            var rulerRt = ruler != null ? ruler.rulerRt : null;
            var savedRulerParent = rulerRt != null ? rulerRt.parent : null;
            var savedRulerAnchorMin = rulerRt != null ? rulerRt.anchorMin : Vector2.zero;
            var savedRulerAnchorMax = rulerRt != null ? rulerRt.anchorMax : Vector2.zero;
            var savedRulerPivot = rulerRt != null ? rulerRt.pivot : Vector2.zero;
            var savedRulerPosition = rulerRt != null ? rulerRt.anchoredPosition : Vector2.zero;
            var savedRulerSize = rulerRt != null ? rulerRt.sizeDelta : Vector2.zero;
            var savedRulerScale = rulerRt != null ? rulerRt.localScale : Vector3.one;
            var savedRulerRotation = rulerRt != null ? rulerRt.localRotation : Quaternion.identity;
            var savedRulerSibling = rulerRt != null ? rulerRt.GetSiblingIndex() : 0;
            var savedRulerActive = rulerRt != null && rulerRt.gameObject.activeSelf;
            var savedRulerUnlocked = ruler != null && ruler.unlocked;
            var savedRulerAligned = ruler != null && ruler.aligned;
            var savedRulerColor = ruler != null && ruler.rulerImage != null ? ruler.rulerImage.color : Color.white;

            var camGo = new GameObject("M2ShotCam");
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.1f, 0.1f, 0.12f);
            cam.orthographic = true;
            cam.nearClipPlane = .1f;
            cam.farClipPlane = 2000f;
            camGo.transform.position = new Vector3(0f, 0f, -1000f);

            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = cam;
            if (scaler != null) scaler.enabled = false;
            canvasRt.localPosition = Vector3.zero;
            canvasRt.localScale = Vector3.one;
            canvasRt.pivot = new Vector2(.5f, .5f);

            try
            {
                foreach (var t in Targets)
                {
                    var widthScale = t.w / scaler.referenceResolution.x;
                    var heightScale = t.h / scaler.referenceResolution.y;
                    var scaleFactor = Mathf.Pow(2f, Mathf.Lerp(Mathf.Log(widthScale, 2f), Mathf.Log(heightScale, 2f), scaler.matchWidthOrHeight));
                    canvasRt.sizeDelta = new Vector2(t.w / scaleFactor, t.h / scaleFactor);
                    cam.aspect = (float)t.w / t.h;
                    cam.orthographicSize = canvasRt.sizeDelta.y * .5f;
                    var rt = new RenderTexture(t.w, t.h, 24);
                    rt.Create();
                    cam.targetTexture = rt;
                    canvas.worldCamera = cam;
                    Canvas.ForceUpdateCanvases(); // 触发 Graphic OnPopulateMesh（Editor 截图关键）
                    cam.Render();
                    var tex = new Texture2D(t.w, t.h, TextureFormat.RGB24, false);
                    var oldActive = RenderTexture.active;
                    RenderTexture.active = rt;
                    tex.ReadPixels(new Rect(0, 0, t.w, t.h), 0, 0);
                    RenderTexture.active = oldActive;
                    var dir = Path.Combine(Directory.GetCurrentDirectory(), "Logs");
                    Directory.CreateDirectory(dir);
                    var file = Path.Combine(dir, $"m2-shot_{t.name}.png");
                    if (!HasVisibleContent(tex))
                        throw new InvalidOperationException($"[M2Shot] {t.name} 截图为空，请检查 Canvas/Camera 渲染链路。");
                    File.WriteAllBytes(file, tex.EncodeToPNG());
                    Debug.Log($"[M2Shot] 已保存 {t.name} -> {file}");
                    UnityEngine.Object.DestroyImmediate(tex);
                    cam.targetTexture = null;
                    rt.Release();
                    UnityEngine.Object.DestroyImmediate(rt);
                }
            }
            finally
            {
                if (stageRaw != null) stageRaw.enabled = savedRawEnabled;
                if (rulerRt != null)
                {
                    rulerRt.SetParent(savedRulerParent, false); rulerRt.anchorMin = savedRulerAnchorMin; rulerRt.anchorMax = savedRulerAnchorMax;
                    rulerRt.pivot = savedRulerPivot; rulerRt.anchoredPosition = savedRulerPosition; rulerRt.sizeDelta = savedRulerSize;
                    rulerRt.localScale = savedRulerScale; rulerRt.localRotation = savedRulerRotation; rulerRt.SetSiblingIndex(savedRulerSibling);
                    ruler.unlocked = savedRulerUnlocked; ruler.aligned = savedRulerAligned;
                    if (ruler.rulerImage != null) ruler.rulerImage.color = savedRulerColor;
                    rulerRt.gameObject.SetActive(savedRulerActive);
                }
                canvas.renderMode = savedMode;
                canvas.worldCamera = savedCam;
                canvasRt.localPosition = savedPosition;
                canvasRt.localScale = savedScale;
                canvasRt.pivot = savedPivot;
                canvasRt.sizeDelta = savedSize;
                if (scaler != null) scaler.enabled = savedScalerEnabled;
                UnityEngine.Object.DestroyImmediate(camGo);
            }
            var sceneHashAfter = Sha256(ScenePath);
            if (sceneHashAfter != sceneHashBefore)
                throw new InvalidOperationException($"[M2Shot] 冻结 Scene 哈希变化：{sceneHashBefore} -> {sceneHashAfter}");
            Debug.Log($"[M2Shot] 全部截图完成，Scene SHA-256={sceneHashAfter}");
            if (Application.isBatchMode) EditorApplication.Exit(0); // 菜单调用不退出编辑器
        }

        private static string Sha256(string path)
        {
            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(File.ReadAllBytes(path));
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }

        private static bool HasVisibleContent(Texture2D tex)
        {
            var pixels = tex.GetPixels32();
            var first = pixels[0];
            for (var i = 1; i < pixels.Length; i += Mathf.Max(1, pixels.Length / 4096))
            {
                var p = pixels[i];
                if (Mathf.Abs(p.r - first.r) + Mathf.Abs(p.g - first.g) + Mathf.Abs(p.b - first.b) > 8) return true;
            }
            return false;
        }

        private static Transform FindIncludingInactive(Transform root, string name)
        {
            foreach (Transform child in root)
            {
                if (child.name == name) return child;
                var hit = FindIncludingInactive(child, name);
                if (hit != null) return hit;
            }
            return null;
        }
    }
}
