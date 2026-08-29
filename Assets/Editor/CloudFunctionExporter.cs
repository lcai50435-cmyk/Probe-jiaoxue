using System.IO;
using UnityEditor;
using UnityEngine;

namespace M1.EditorTools
{
    /// <summary>
    /// 微信导出后把可追踪的云函数源码部署到 minigame/cloudfunctions 并在 project.config.json 声明 cloudfunctionRoot，
    /// 使开发者工具可识别并右键上传。源码唯一位置 Assets/Editor/CloudFunctions/deepseek-proxy（含 index.js/package.json），
    /// 不含任何密钥。幂等：重复导出先清旧目录再复制。
    /// </summary>
    public static class CloudFunctionExporter
    {
        public const string SourceDir = "Assets/Editor/CloudFunctions/deepseek-proxy";
        public const string FunctionName = "deepseek-proxy";

        public static void Install()
        {
            var dstRoot = Path.Combine(Application.dataPath, "..", "Builds", "WXExport", "minigame");
            if (!Directory.Exists(dstRoot))
            {
                Debug.LogWarning("[CloudFunctionExporter] minigame 输出目录不存在，跳过云函数部署：" + Path.GetFullPath(dstRoot));
                return;
            }

            var src = Path.Combine(Application.dataPath, "Editor", "CloudFunctions", FunctionName);
            if (!File.Exists(Path.Combine(src, "index.js")) || !File.Exists(Path.Combine(src, "package.json")))
            {
                Debug.LogError("[CloudFunctionExporter] 云函数源码缺失：" + src + "（需含 index.js 与 package.json）");
                return;
            }

            var dst = Path.Combine(dstRoot, "cloudfunctions", FunctionName);
            if (Directory.Exists(dst)) Directory.Delete(dst, true);
            Directory.CreateDirectory(dst);
            foreach (var fileName in new[] { "index.js", "package.json" })
                File.Copy(Path.Combine(src, fileName), Path.Combine(dst, fileName));
            Debug.Log("[CloudFunctionExporter] 已部署云函数：" + Path.GetFullPath(dst));

            EnsureCloudFunctionRoot(Path.Combine(dstRoot, "project.config.json"));
        }

        /// <summary>在 project.config.json 顶层写入 cloudfunctionRoot（保留现有全部字段；无该文件则跳过）。</summary>
        private static void EnsureCloudFunctionRoot(string projectConfigPath)
        {
            if (!File.Exists(projectConfigPath))
            {
                Debug.LogWarning("[CloudFunctionExporter] project.config.json 不存在，跳过 cloudfunctionRoot 写入。");
                return;
            }
            var text = File.ReadAllText(projectConfigPath);
            if (text.Contains("\"cloudfunctionRoot\""))
            {
                Debug.Log("[CloudFunctionExporter] project.config.json 已含 cloudfunctionRoot。");
                return;
            }
            // 在顶层 compileType 字段行前插入（保留 SDK/开发者工具写入的其他字段）
            const string marker = "\"compileType\"";
            var idx = text.IndexOf(marker);
            if (idx < 0)
            {
                Debug.LogWarning("[CloudFunctionExporter] project.config.json 未找到 compileType 字段，跳过。");
                return;
            }
            var lineStart = text.LastIndexOf('\n', idx) + 1;
            text = text.Insert(lineStart, "  \"cloudfunctionRoot\": \"cloudfunctions/\",\n");
            File.WriteAllText(projectConfigPath, text);
            Debug.Log("[CloudFunctionExporter] 已写入 cloudfunctionRoot 到 " + projectConfigPath);
        }
    }
}
