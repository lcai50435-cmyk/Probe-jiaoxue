using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace M1.EditorTools
{
    /// <summary>
    /// WebGL 构建纹理平台覆盖：大尺寸教学图在微信端统一降到 1024 并压缩（ETC2_RGBA8Crunched；
    /// NPOT 纹理无法 ETC2，靠降尺寸减体积）。仅覆盖 WebGL 平台导入设置，Android/Editor 平台与
    /// Scene 序列化零改动；重跑幂等。清晰度由真机验收把关（主体图页面显示宽 ~960px，1024 足够）。
    /// </summary>
    public static class WebGlTextureOverrideTool
    {
        private class OverrideRule
        {
            public string Path;
            public int MaxSize;
            public OverrideRule(string path, int maxSize) { Path = path; MaxSize = maxSize; }
        }

        [MenuItem("Tools/WeChat/应用WebGL纹理覆盖")]
        public static void Apply()
        {
            var rules = new List<OverrideRule>();
            foreach (var f in Directory.GetFiles("Assets/InspectionToolMaterials", "*.PNG"))
                rules.Add(new OverrideRule(f.Replace('\\', '/'), 1024));
            rules.Add(new OverrideRule("Assets/probeFootage/rag.png", 1024));
            rules.Add(new OverrideRule("Assets/Resources/probe0.png", 1024));
            rules.Add(new OverrideRule("Assets/probeFootage/0度.PNG", 1024));
            rules.Add(new OverrideRule("Assets/probeFootage/K3.PNG", 1024));
            rules.Add(new OverrideRule("Assets/probeFootage/K2.5.PNG", 1024));
            rules.Add(new OverrideRule("Assets/交互动画素材/额外/大头.png", 1024));
            rules.Add(new OverrideRule("Assets/DigitalHuman/A-05 折叠态头像.PNG", 1024));
            rules.Add(new OverrideRule("Assets/Resources/DigitalHuman/折叠头像.png", 1024));
            // NPOT 纹理无法 ETC2 压缩（ES3 要求 POT），统一降到 1024（页面显示 ~960px 宽，视觉足够）
            rules.Add(new OverrideRule("Assets/probeFootage/probeFootage.png", 1024));
            rules.Add(new OverrideRule("Assets/Resources/probeFootage.png", 1024));
            rules.Add(new OverrideRule("Assets/Resources/俯视角.png", 1024));
            rules.Add(new OverrideRule("Assets/Resources/俯视角透视.png", 1024));
            rules.Add(new OverrideRule("Assets/railwayTracks_2/正视角透明.png", 1024));
            rules.Add(new OverrideRule("Assets/railwayTracks_2/正视角.png", 1024));

            var applied = 0;
            foreach (var rule in rules)
            {
                var imp = AssetImporter.GetAtPath(rule.Path) as TextureImporter;
                if (imp == null) { Debug.LogWarning("[WebGlTextureOverride] 跳过缺失纹理：" + rule.Path); continue; }
                var settings = imp.GetPlatformTextureSettings("WebGL");
                if (settings.overridden && settings.maxTextureSize == rule.MaxSize &&
                    settings.format == TextureImporterFormat.ETC2_RGBA8Crunched) continue;
                imp.SetPlatformTextureSettings(new TextureImporterPlatformSettings
                {
                    name = "WebGL", overridden = true, maxTextureSize = rule.MaxSize,
                    format = TextureImporterFormat.ETC2_RGBA8Crunched, compressionQuality = 50,
                });
                imp.SaveAndReimport();
                applied++;
            }
            Debug.Log("[WebGlTextureOverride] WebGL 纹理覆盖应用完成，本次变更 " + applied + "/" + rules.Count + " 张。");
        }
    }
}
