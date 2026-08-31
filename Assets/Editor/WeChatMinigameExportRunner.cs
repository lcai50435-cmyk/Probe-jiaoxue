using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace M1.EditorTools
{
    /// <summary>批处理导出微信小游戏（含 WebGL 构建）：-executeMethod M1.EditorTools.WeChatMinigameExportRunner.Run。
    /// 失败时以退出码 2 结束，便于 CI/脚本判断。</summary>
    public static class WeChatMinigameExportRunner
    {
        public static void Run()
        {
            // 体验版发布配置：剥离历史导出累积的 wasm 函数名剖析参数（可逆，MiniGameConfig.profilingFuncs 控制是否追加）
            PlayerSettings.WebGL.emscriptenArgs =
                PlayerSettings.WebGL.emscriptenArgs.Replace(" --profiling-funcs ", " ");
            AssetDatabase.SaveAssets();

            var err = WeChatWASM.WXConvertCore.DoExport(true);
            Debug.Log("[WeChatMinigameExportRunner] 导出结果：" + err);
            if (err != WeChatWASM.WXConvertCore.WXExportError.SUCCEED)
            {
                EditorApplication.Exit(2);
                return;
            }
            RemoveStaleWasmCodeFiles();
            CloudFunctionExporter.Install();
            WxVideoSubpackageInstaller.Install();
        }

        private static void RemoveStaleWasmCodeFiles()
        {
            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrEmpty(projectRoot))
            {
                Debug.LogWarning("[WeChatMinigameExportRunner] 无法定位项目根目录，跳过 wasm 历史文件清理。");
                return;
            }

            var minigameDirectory = Path.GetFullPath(Path.Combine(projectRoot, "Builds", "WXExport", "minigame"));
            var wasmcodeDirectory = Path.GetFullPath(Path.Combine(minigameDirectory, "wasmcode"));
            var gameJsPath = Path.Combine(minigameDirectory, "game.js");
            if (!wasmcodeDirectory.StartsWith(minigameDirectory + Path.DirectorySeparatorChar) ||
                !File.Exists(gameJsPath) || !Directory.Exists(wasmcodeDirectory))
            {
                Debug.LogWarning("[WeChatMinigameExportRunner] 导出目录、game.js 或 wasmcode 缺失，跳过 wasm 历史文件清理。");
                return;
            }

            var match = Regex.Match(File.ReadAllText(gameJsPath), "\\bCODE_FILE_MD5\\s*:\\s*['\\\"](?<hash>[0-9a-fA-F]+)['\\\"]");
            if (!match.Success)
            {
                Debug.LogWarning("[WeChatMinigameExportRunner] game.js 未包含 CODE_FILE_MD5，跳过 wasm 历史文件清理。");
                return;
            }

            var currentFileName = match.Groups["hash"].Value + ".webgl.wasm.code.unityweb.wasm.br";
            var currentFilePath = Path.GetFullPath(Path.Combine(wasmcodeDirectory, currentFileName));
            if (!currentFilePath.StartsWith(wasmcodeDirectory + Path.DirectorySeparatorChar) || !File.Exists(currentFilePath))
            {
                Debug.LogWarning("[WeChatMinigameExportRunner] 当前 CODE_FILE_MD5 对应 wasm 缺失，跳过 wasm 历史文件清理。");
                return;
            }

            var deleted = 0;
            foreach (var filePath in Directory.GetFiles(wasmcodeDirectory, "*.webgl.wasm.code.unityweb.wasm.br", SearchOption.TopDirectoryOnly))
            {
                if (string.Equals(Path.GetFileName(filePath), currentFileName, System.StringComparison.Ordinal))
                    continue;
                if ((File.GetAttributes(filePath) & FileAttributes.ReparsePoint) != 0)
                {
                    Debug.LogWarning("[WeChatMinigameExportRunner] 跳过 wasmcode 中的重解析点：" + filePath);
                    continue;
                }

                File.Delete(filePath);
                deleted++;
            }

            Debug.Log($"[WeChatMinigameExportRunner] 清理历史 wasm：删除 {deleted} 个，保留 {currentFileName} 和 game.js。");
        }
    }
}
