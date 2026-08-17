// ============================================================================
// 关键 UI 素材导入设置修复：maxTextureSize=4096 + Uncompressed（全平台显式覆盖）
// ----------------------------------------------------------------------------
// 起因：probeFootage.png(2610x906) 被 maxTextureSize=2048 降采样损失约 21%
//       分辨率，尺子/探头又走 Compressed 压缩，精细刻线与图内文字出现块状
//       模糊（2026-08-14 老板反馈「不如原图清晰」）。
// 教训：仅设置顶层字段会写入 DefaultTexturePlatform 且 overridden=0，不生效
//       （首次修复 Standalone/WebGL 仍为 2048+Compressed）；必须对每个
//       buildTarget 显式 SetPlatformTextureSettings(overridden=true)。
// 方案：只改导入设置（.meta），不修改素材本体、不触碰任何 Scene 文件。
// 幂等：重复运行已符合的素材直接跳过，不重复修改。
// ============================================================================
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace M2.EditorTools
{
    /// <summary>UI 素材清晰度修复：4096 + Uncompressed（全平台显式覆盖）。</summary>
    public static class TextureImportSettings
    {
        private const int MaxSize = 4096;

        // 新增素材时在此追加路径即可；Resources 版与 Scene 序列化版是同一张图，需一并处理。
        private static readonly string[] Targets =
        {
            "Assets/probeFootage/probeFootage.png",
            "Assets/Resources/尺子正面.png",
            "Assets/Ruler/尺子正面.png",
        };

        // 全平台显式覆盖，防止 overridden=0 导致设置不生效。
        private static readonly string[] BuildTargets =
        {
            "DefaultTexturePlatform", "Standalone", "Android", "iPhone", "WebGL", "WindowsStoreApps",
        };

        [MenuItem("Tools/M2/修复 UI 素材清晰度 (4096+Uncompressed)")]
        public static void FixFromMenu()
        {
            int changed = Fix();
            EditorUtility.DisplayDialog("素材清晰度", $"已修改 {changed} 个素材为 4096 + Uncompressed（全平台）", "确定");
        }

        /// <summary>batchmode 入口：Unity -executeMethod M2.EditorTools.TextureImportSettings.FixBatch</summary>
        public static void FixBatch()
        {
            int changed = Fix();
            Debug.Log($"[TextureFix] batch 完成，修改 {changed} 个素材");
        }

        private static int Fix()
        {
            int changed = 0;
            foreach (var path in Targets)
            {
                if (!(AssetImporter.GetAtPath(path) is TextureImporter importer))
                {
                    Debug.LogWarning($"[TextureFix] 找不到素材：{path}");
                    continue;
                }

                bool dirty = false;
                if (importer.maxTextureSize != MaxSize)
                {
                    importer.maxTextureSize = MaxSize;
                    dirty = true;
                }
                if (importer.textureCompression != TextureImporterCompression.Uncompressed)
                {
                    importer.textureCompression = TextureImporterCompression.Uncompressed;
                    dirty = true;
                }

                foreach (var bt in BuildTargets)
                {
                    var settings = importer.GetPlatformTextureSettings(bt);
                    if (settings.maxTextureSize != MaxSize ||
                        settings.textureCompression != TextureImporterCompression.Uncompressed ||
                        !settings.overridden)
                    {
                        settings.overridden = true;
                        settings.maxTextureSize = MaxSize;
                        settings.textureCompression = TextureImporterCompression.Uncompressed;
                        importer.SetPlatformTextureSettings(settings);
                        dirty = true;
                    }
                }

                if (!dirty)
                {
                    Debug.Log($"[TextureFix] {path} 已符合 4096+Uncompressed（全平台），跳过");
                    continue;
                }

                importer.SaveAndReimport();
                changed++;
                Debug.Log($"[TextureFix] {path} → 已保存（maxTextureSize={importer.maxTextureSize}, compression={importer.textureCompression}）");
            }
            return changed;
        }
    }
}
#endif
