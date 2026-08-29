using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace M1.EditorTools
{
    /// <summary>
    /// WebGL 构建期把含密钥的 DeepSeek 配置移出 Resources（含无密钥但含端点字样的模板），
    /// Android/Editor 构建不受影响。恢复保护：构建结束立即恢复；崩溃/强退后由
    /// [InitializeOnLoad] 或下一次任意平台构建前的自愈检查恢复，禁止丢失本地配置。
    /// </summary>
    [InitializeOnLoad]
    public sealed class WebGlAiSecurityProcessor : IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        public int callbackOrder => 0;

        private static readonly string[] StripAssets =
        {
            "Assets/Resources/DeepSeekConfig.asset",
            "Assets/Resources/DeepSeekConfig.template.asset",
        };

        // TMP 示例字体目录（无场景引用，纯 Resources 规则进包）；目录级整体移出/恢复
        private const string StripDir = "Assets/TextMesh Pro/Examples & Extras/Resources/Fonts & Materials";

        private static string BackupDir => Path.Combine("Library", "AiSecurityBoundary");
        private static string MarkerPath => Path.Combine(BackupDir, "stripped-list.txt");
        private static string DirBackupPath => Path.Combine(BackupDir, "TMPExamplesFonts");

        static WebGlAiSecurityProcessor() => RestoreIfStripped();

        public void OnPreprocessBuild(BuildReport report)
        {
            RestoreIfStripped(); // 上次构建异常退出的自愈
            if (report.summary.platform != BuildTarget.WebGL) return;
            Strip();
        }

        public void OnPostprocessBuild(BuildReport report) => RestoreIfStripped();

        private static void Strip()
        {
            Directory.CreateDirectory(BackupDir);
            var moved = new List<string>();
            foreach (var asset in StripAssets)
            {
                if (!File.Exists(asset)) continue;
                var backup = Path.Combine(BackupDir, Path.GetFileName(asset));
                File.Copy(asset, backup, true);
                if (File.Exists(asset + ".meta")) File.Copy(asset + ".meta", backup + ".meta", true);
                moved.Add("FILE\t" + asset);
            }
            if (Directory.Exists(StripDir)) moved.Add("DIR\t" + StripDir);
            if (moved.Count == 0)
            {
                Debug.Log("[WebGlAiSecurityProcessor] Resources 中无 DeepSeek 配置资产，无需剥离。");
                return;
            }
            // 先写标记再删除：任意时刻崩溃，自愈路径都能凭标记+备份找回原资产
            File.WriteAllText(MarkerPath, string.Join("\n", moved) + "\n");
            foreach (var entry in moved)
            {
                var path = entry.Substring(entry.IndexOf('\t') + 1);
                if (entry.StartsWith("FILE"))
                {
                    File.Delete(path);
                    if (File.Exists(path + ".meta")) File.Delete(path + ".meta");
                }
                else
                {
                    if (File.Exists(path + ".meta")) File.Move(path + ".meta", DirBackupPath + ".meta");
                    Directory.Move(path, DirBackupPath);
                }
            }
            AssetDatabase.Refresh();
            foreach (var asset in StripAssets)
                if (AssetDatabase.LoadMainAssetAtPath(asset) != null)
                    Debug.LogError("[WebGlAiSecurityProcessor] 剥离后仍可加载（BuildReport 需人工复核）：" + asset);
            Debug.Log("[WebGlAiSecurityProcessor] WebGL 构建已剥离 DeepSeek 配置与 TMP 示例字体，备份于 " + BackupDir);
        }

        /// <summary>存在剥离标记即视为上次构建未正常恢复，按记录逐个拷回并清理备份。</summary>
        public static void RestoreIfStripped()
        {
            if (!File.Exists(MarkerPath)) return;
            var restored = new List<string>();
            foreach (var entry in File.ReadAllLines(MarkerPath))
            {
                if (string.IsNullOrWhiteSpace(entry)) continue;
                var kind = entry.Substring(0, entry.IndexOf('\t'));
                var path = entry.Substring(entry.IndexOf('\t') + 1);
                if (kind == "FILE")
                {
                    var backup = Path.Combine(BackupDir, Path.GetFileName(path));
                    if (!File.Exists(path) && File.Exists(backup))
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
                        File.Copy(backup, path);
                        if (File.Exists(backup + ".meta")) File.Copy(backup + ".meta", path + ".meta", true);
                    }
                    if (File.Exists(backup)) File.Delete(backup);
                    if (File.Exists(backup + ".meta")) File.Delete(backup + ".meta");
                }
                else if (Directory.Exists(DirBackupPath) && !Directory.Exists(path))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
                    Directory.Move(DirBackupPath, path);
                    if (File.Exists(DirBackupPath + ".meta")) File.Move(DirBackupPath + ".meta", path + ".meta");
                }
                restored.Add(path);
            }
            File.Delete(MarkerPath);
            AssetDatabase.Refresh();
            Debug.Log("[WebGlAiSecurityProcessor] 已恢复本地剥离资产：" + string.Join(", ", restored));
        }
    }
}
