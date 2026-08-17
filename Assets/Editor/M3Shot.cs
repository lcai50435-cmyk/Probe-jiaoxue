using System;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using M2;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace M3.EditorTools
{
    /// <summary>以三种审核视口渲染已保存的 M3 静态场景。</summary>
    public static class M3Shot
    {
        private const string ScenePath = "Assets/Settings/Scenes/M3.unity";
        private static readonly (int Width, int Height, string Name)[] Targets =
        {
            (1920, 1080, "1920x1080"),
            (1280, 720, "1280x720"),
            (2436, 1125, "2436x1125"),
        };

        [MenuItem("Tools/M3/Capture Review Shots %#&8")]
        public static void CaptureAll()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
                throw new FileNotFoundException("请先运行 M3 Setup。", ScenePath);

            var sceneHash = ComputeHash(ScenePath);
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var canvas = UnityEngine.Object.FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
            if (canvas == null) throw new InvalidOperationException("[M3Shot] 未找到 Canvas。");
            var waveFx = UnityEngine.Object.FindFirstObjectByType<M2WaveformFx>(FindObjectsInactive.Include);
            if (waveFx != null) waveFx.SetDistanceMm(160f);

            var savedMode = canvas.renderMode;
            var savedCamera = canvas.worldCamera;
            var cameraObject = new GameObject("M3ShotCamera", typeof(Camera));
            var camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.925f, 0.935f, 0.945f);
            cameraObject.transform.position = new Vector3(0f, 0f, -100f);
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;

            try
            {
                var output = Path.Combine(Directory.GetCurrentDirectory(), "Logs");
                Directory.CreateDirectory(output);
                foreach (var target in Targets)
                    Capture(camera, canvas, target.Width, target.Height,
                        Path.Combine(output, "m3-shot_" + target.Name + ".png"));
            }
            finally
            {
                canvas.renderMode = savedMode;
                canvas.worldCamera = savedCamera;
                UnityEngine.Object.DestroyImmediate(cameraObject);
            }

            if (ComputeHash(ScenePath) != sceneHash)
                throw new InvalidOperationException("[M3Shot] 截图前后 M3 Scene 哈希发生变化。");
            Debug.Log("[M3Shot] 三视口非空截图完成，Scene 哈希不变。");
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        private static void Capture(Camera camera, Canvas canvas, int width, int height, string path)
        {
            var renderTexture = new RenderTexture(width, height, 24);
            var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
            var previousActive = RenderTexture.active;
            try
            {
                renderTexture.Create();
                camera.targetTexture = renderTexture;
                canvas.worldCamera = camera;
                ForceWaveMesh();
                Canvas.ForceUpdateCanvases();
                camera.Render();
                RenderTexture.active = renderTexture;
                texture.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
                texture.Apply(false);
                AssertNonBlank(texture);
                File.WriteAllBytes(path, texture.EncodeToPNG());
                Debug.Log("[M3Shot] 已保存 " + width + "x" + height + " -> " + path);
            }
            finally
            {
                RenderTexture.active = previousActive;
                camera.targetTexture = null;
                renderTexture.Release();
                UnityEngine.Object.DestroyImmediate(texture);
                UnityEngine.Object.DestroyImmediate(renderTexture);
            }
        }

        private static void ForceWaveMesh()
        {
            var fx = UnityEngine.Object.FindFirstObjectByType<M2WaveformFx>(FindObjectsInactive.Include);
            if (fx == null) return;
            fx.SetDistanceMm(160f);
            fx.Rebuild(CanvasUpdate.PreRender);
            var method = typeof(M2WaveformFx).GetMethod("OnPopulateMesh", BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(VertexHelper) }, null);
            var helper = new VertexHelper();
            method?.Invoke(fx, new object[] { helper });
            Debug.Log($"[M3Shot] wave vertices={helper.currentVertCount}");
        }

        private static void AssertNonBlank(Texture2D texture)
        {
            var first = texture.GetPixel(0, 0);
            for (var y = 0; y < texture.height; y += Mathf.Max(1, texture.height / 18))
                for (var x = 0; x < texture.width; x += Mathf.Max(1, texture.width / 32))
                    if (ColorDistance(first, texture.GetPixel(x, y)) > .03f) return;
            throw new InvalidOperationException("[M3Shot] 截图像素近似纯色，判定渲染失败。");
        }

        private static float ColorDistance(Color a, Color b) =>
            Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b);

        private static string ComputeHash(string path)
        {
            using var sha = SHA256.Create();
            using var stream = File.OpenRead(path);
            return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
        }
    }
}
