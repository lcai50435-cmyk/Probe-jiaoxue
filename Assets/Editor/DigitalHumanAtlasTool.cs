using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using M1;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace M1.EditorTools
{
    /// <summary>
    /// 微信常驻数字人透明帧图集生成工具（2026-08-28）：
    /// ffmpeg 从 WebM 源按目标 fps 降采样抽帧（240px 宽），按 UI-LumaKey 同款 sRGB 亮度键控合成 Alpha
    /// （源 WebM 实测无 Alpha 通道，黑底素材与现有视频观感一致），打包 2048 图集页写入
    /// Resources/DigitalHuman/Frames，生成 FrameAnimConfig 并设置压缩导入（ETC2_RGBA8Crunched）。
    /// 幂等：重跑覆盖同名页、清理多余页与临时帧；Editor 工具豁免 150 行上限。
    /// </summary>
    public static class DigitalHumanAtlasTool
    {
        private const int FrameWidth = 240;
        private const int PageMax = 2048;
        private const int Gutter = 2;
        private const float KeyThreshold = 0.02f; // 与 UI/LumaKey 材质默认一致（sRGB 亮度键控）
        private const float KeySmooth = 0.015f;
        private const string FramesDir = "Assets/Resources/DigitalHuman/Frames";
        private const string ConfigPath = "Assets/Resources/DigitalHuman/FrameAnimConfig.asset";

        // 常驻三态 + iPhone 引导：引导按 6fps 降采样，复用同一亮度键控与图集管线。
        private static readonly (string key, string webm, int fps)[] Sources =
        {
            ("idle", "Assets/DigitalHuman/A-01 待机动画/output.webm", 8),
            ("thinking", "Assets/DigitalHuman/A-03 思考动画/思考动画.webm", 10),
            ("speaking", "Assets/DigitalHuman/A-02讲解动画/讲解动画2.webm", 12),
            ("intro", "WeChatVideos/m1-intro-wx.mp4", 6),
        };

        [MenuItem("Tools/DigitalHuman/生成微信透明帧图集")]
        public static void GenerateFromMenu() => GenerateAll();

        /// <summary>批处理验证入口：打印每个图集页实际导入格式、尺寸与运行时纹理内存。</summary>
        public static void PrintRuntimeStats()
        {
            foreach (var file in Directory.GetFiles(FramesDir, "*.png"))
            {
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(file.Replace("\\", "/"));
                if (tex == null) continue;
                Debug.Log($"[DigitalHumanAtlasTool] {Path.GetFileName(file)}: format={tex.format}, " +
                          $"{tex.width}x{tex.height}, runtimeMemory={UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(tex) / (1024L * 1024L)}MB");
            }
        }

        /// <summary>批量模式入口：-executeMethod M1.EditorTools.DigitalHumanAtlasTool.GenerateAll</summary>
        public static void GenerateAll()
        {
            var ffmpeg = FindFfmpeg();
            if (string.IsNullOrEmpty(ffmpeg))
            {
                Debug.LogError("[DigitalHumanAtlasTool] 未找到 ffmpeg（可设环境变量 FFMPEG_PATH 指向 ffmpeg.exe）。");
                return;
            }
            Directory.CreateDirectory(FramesDir);
            var states = new List<DigitalHumanFrameConfig.State>();
            foreach (var (key, webm, fps) in Sources)
            {
                var state = BuildState(ffmpeg, key, webm, fps);
                if (state != null) states.Add(state);
            }
            SaveConfig(states);
            AssetDatabase.Refresh();
            Debug.Log("[DigitalHumanAtlasTool] 图集生成完成：" + states.Count + " 个状态。");
        }

        private static DigitalHumanFrameConfig.State BuildState(string ffmpeg, string key, string webm, int fps)
        {
            if (!File.Exists(webm)) { Debug.LogError("[DigitalHumanAtlasTool] 源动画不存在：" + webm); return null; }
            var tmp = Path.Combine(Path.GetTempPath(), "dh_atlas", key);
            if (Directory.Exists(tmp)) Directory.Delete(tmp, true);
            Directory.CreateDirectory(tmp);

            var args = "-y -i \"" + webm + "\" -vf fps=" + fps + ",scale=" + FrameWidth +
                       ":-2 -start_number 1 \"" + Path.Combine(tmp, "f_%04d.png") + "\"";
            var psi = new ProcessStartInfo(ffmpeg, args) { CreateNoWindow = true, UseShellExecute = false };
            using (var p = Process.Start(psi))
            {
                if (p == null || !p.WaitForExit(120000))
                {
                    if (p != null) p.Kill();
                    Debug.LogError("[DigitalHumanAtlasTool] ffmpeg 抽帧失败或超时：" + key);
                    return null;
                }
                if (p.ExitCode != 0) { Debug.LogError("[DigitalHumanAtlasTool] ffmpeg 抽帧失败：" + key); return null; }
            }

            var frameFiles = Directory.GetFiles(tmp, "f_*.png");
            System.Array.Sort(frameFiles);
            if (frameFiles.Length == 0) { Debug.LogError("[DigitalHumanAtlasTool] 未抽出任何帧：" + key); return null; }

            // 读取首帧确定帧高，计算网格
            var first = LoadFrame(frameFiles[0]);
            var frameHeight = first.height;
            var cellW = FrameWidth + Gutter * 2;
            var cellH = frameHeight + Gutter * 2;
            var cols = PageMax / cellW;
            var rows = PageMax / cellH;
            var perPage = cols * rows;
            var pageCount = (frameFiles.Length + perPage - 1) / perPage;

            var written = new List<string>();
            for (var page = 0; page < pageCount; page++)
            {
                // 稀疏末页按实际用到的行列裁切（POT 尺寸以满足 ETC2 压缩），网格坐标不变
                var framesOnPage = Mathf.Min(perPage, frameFiles.Length - page * perPage);
                var usedRows = (framesOnPage + cols - 1) / cols;
                var usedCols = Mathf.Min(cols, framesOnPage);
                var pageW = Mathf.NextPowerOfTwo(usedCols * cellW + Gutter);
                var pageH = Mathf.NextPowerOfTwo(usedRows * cellH + Gutter);
                var pixels = new Color32[pageW * pageH]; // 全透明底
                for (var i = 0; i < framesOnPage; i++)
                {
                    var index = page * perPage + i;
                    var frame = index == 0 ? first : LoadFrame(frameFiles[index]);
                    BlitWithBleed(pixels, pageW, pageH, frame, (i % cols) * cellW + Gutter, (i / cols) * cellH + Gutter);
                    if (index != 0) Object.DestroyImmediate(frame);
                }
                var path = $"{FramesDir}/{key}_{page}.png";
                File.WriteAllBytes(path, PageTexture(pixels, pageW, pageH).EncodeToPNG());
                written.Add(path);
            }
            CleanupStalePages(key, written);
            foreach (var path in written) ConfigureImport(path);
            AssetDatabase.Refresh();

            var pagePaths = new string[written.Count];
            for (var i = 0; i < written.Count; i++)
                pagePaths[i] = "DigitalHuman/Frames/" + Path.GetFileNameWithoutExtension(written[i]);
            Debug.Log($"[DigitalHumanAtlasTool] {key}: {frameFiles.Length} 帧 @ {fps}fps，{pageCount} 页" +
                      $"（cols={cols} rows={rows}，末页按实际行列裁切为 POT）。");
            return new DigitalHumanFrameConfig.State
            {
                key = key, fps = fps, frameCount = frameFiles.Length,
                frameWidth = FrameWidth, frameHeight = frameHeight, gutter = Gutter,
                cols = cols, rows = rows, pages = pagePaths,
            };
        }

        private static Texture2D LoadFrame(string path)
        {
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.LoadImage(File.ReadAllBytes(path));
            return tex;
        }

        private static Texture2D PageTexture(Color32[] pixels, int width, int height)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.SetPixels32(pixels);
            tex.Apply(false);
            return tex;
        }

        /// <summary>把帧写入页像素（左下原点），并向四周 gutter 外扩复制边缘像素（防双线性渗色）；
        /// Alpha = smoothstep(阈值, 阈值+羽化, max(r,g,b))，与 UI/LumaKey 黑底抠像同参数。</summary>
        private static void BlitWithBleed(Color32[] page, int pageW, int pageH, Texture2D frame, int ox, int oy)
        {
            var src = frame.GetPixels32();
            var w = frame.width;
            var h = frame.height;
            byte AlphaOf(Color32 c)
            {
                var lum = Mathf.Max(c.r, Mathf.Max(c.g, c.b)) / 255f;
                var t = Mathf.Clamp01((lum - KeyThreshold) / KeySmooth);
                return (byte)(255f * (t * t * (3f - 2f * t)) + 0.5f);
            }
            for (var y = 0; y < h; y++)
            {
                for (var x = 0; x < w; x++)
                {
                    var c = src[y * w + x];
                    var a = AlphaOf(c);
                    if (a == 0) c = new Color32(); // 透明区清零颜色，减小 PNG 体积且避免渗色
                    c.a = a;
                    page[(oy + y) * pageW + ox + x] = c;
                }
            }
            for (var y = 0; y < h; y++)
            {
                var row = (oy + y) * pageW;
                for (var g = 1; g <= Gutter; g++)
                {
                    page[row + ox - g] = page[row + ox];            // 左缘外扩
                    page[row + ox + w - 1 + g] = page[row + ox + w - 1]; // 右缘外扩
                }
            }
            for (var x = 0; x < w; x++)
            {
                for (var g = 1; g <= Gutter; g++)
                {
                    page[(oy - g) * pageW + ox + x] = page[oy * pageW + ox + x];               // 下缘外扩
                    page[(oy + h - 1 + g) * pageW + ox + x] = page[(oy + h - 1) * pageW + ox + x]; // 上缘外扩
                }
            }
        }

        private static void ConfigureImport(string path)
        {
            AssetDatabase.ImportAsset(path);
            if (!(AssetImporter.GetAtPath(path) is TextureImporter importer)) return;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.sRGBTexture = true;
            importer.maxTextureSize = PageMax;
            importer.textureCompression = TextureImporterCompression.Compressed;
            foreach (var platform in new[] { "WebGL", "Android" })
            {
                importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings
                {
                    name = platform,
                    overridden = true,
                    maxTextureSize = PageMax,
                    format = TextureImporterFormat.ETC2_RGBA8Crunched,
                    compressionQuality = 60,
                });
            }
            importer.SaveAndReimport();
        }

        private static void CleanupStalePages(string key, List<string> keep)
        {
            foreach (var file in Directory.GetFiles(FramesDir, key + "_*.png"))
                if (!keep.Contains(file.Replace("\\", "/")))
                    AssetDatabase.DeleteAsset(file);
        }

        private static void SaveConfig(List<DigitalHumanFrameConfig.State> states)
        {
            var config = AssetDatabase.LoadAssetAtPath<DigitalHumanFrameConfig>(ConfigPath);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<DigitalHumanFrameConfig>();
                AssetDatabase.CreateAsset(config, ConfigPath);
            }
            config.states = states.ToArray();
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
        }

        private static string FindFfmpeg()
        {
            var env = System.Environment.GetEnvironmentVariable("FFMPEG_PATH");
            if (!string.IsNullOrEmpty(env) && File.Exists(env)) return env;
            // 本项目 Python(imageio_ffmpeg) 自带 ffmpeg 二进制
            var pythonDir = Path.Combine(System.Environment.GetFolderPath(
                System.Environment.SpecialFolder.LocalApplicationData), "mise", "installs", "python");
            if (Directory.Exists(pythonDir))
                foreach (var f in Directory.GetFiles(pythonDir, "ffmpeg-*.exe", SearchOption.AllDirectories))
                    return f;
            return File.Exists("ffmpeg.exe") ? Path.GetFullPath("ffmpeg.exe") : null;
        }
    }
}
