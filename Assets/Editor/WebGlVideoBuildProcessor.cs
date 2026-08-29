using M1;
using TMPro;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Video;

namespace M1.EditorTools
{
    /// <summary>WebGL 构建时在场景内存副本中剥离 VideoClip，避免视频被写入小游戏数据包。</summary>
    public sealed class WebGlVideoBuildProcessor : IProcessSceneWithReport, IPostprocessBuildWithReport
    {
        public int callbackOrder => 0;

        private static TMP_FontAsset _subsetFont;

        public void OnProcessScene(Scene scene, BuildReport report)
        {
            if (report.summary.platform != BuildTarget.WebGL) return;

            var removed = 0;
            var swapped = 0;
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var player in root.GetComponentsInChildren<VideoPlayer>(true))
                    if (player.clip != null) { player.clip = null; removed++; }
                foreach (var presenter in root.GetComponentsInChildren<M1DigitalHumanPresenter>(true))
                {
                    presenter.idleClip = null;
                    presenter.thinkingClip = null;
                    presenter.speakingClip = null;
                }
                // 构建期清空旧迁移字段（如 M2 冻结场景序列化的 legacyBaseUrl 端点字样），仅内存副本
                foreach (var client in root.GetComponentsInChildren<M1DeepSeekClient>(true))
                    client.ClearLegacyConfiguration();

                // 构建期把全部 TMP 文本换成微信静态子集字体（内存副本，Scene 文件不动）
                var subset = _subsetFont != null
                    ? _subsetFont
                    : (_subsetFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(WeChatFontSubsetTool.FontAssetPath));
                if (subset == null)
                    Debug.LogError("[WebGlVideoBuildProcessor] 微信字体子集缺失，先运行 Tools/WeChat/生成微信字体子集；本次构建仍含全量字体。");
                else
                {
                    foreach (var tmp in root.GetComponentsInChildren<TMP_Text>(true))
                        if (tmp.font != subset) { tmp.font = subset; swapped++; }
                    // TMP_InputField 不是 TMP_Text，但其 m_GlobalFontAsset 也会把旧全量字体拉进包
                    foreach (var input in root.GetComponentsInChildren<TMP_InputField>(true))
                        if (input.fontAsset != subset) { input.fontAsset = subset; swapped++; }
                    // M1/M2 场景序列化的问答面板字体是旧字体的最后一个引用点
                    foreach (var panel in root.GetComponentsInChildren<M1QAPanel>(true))
                        if (panel.cnFont != subset) { panel.cnFont = subset; swapped++; }
                }
            }
            if (removed > 0) Debug.Log("[WebGlVideoBuildProcessor] " + scene.name + " 已剥离 " + removed + " 个内嵌视频引用。");
            if (swapped > 0) Debug.Log("[WebGlVideoBuildProcessor] " + scene.name + " 已替换 " + swapped + " 个 TMP 字体引用为微信子集。");
        }

        public void OnPostprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.WebGL) return;
            var videos = System.IO.Path.Combine(report.summary.outputPath, "StreamingAssets", "videos");
            if (!System.IO.Directory.Exists(videos)) return;
            System.IO.Directory.Delete(videos, true);
            Debug.Log("[WebGlVideoBuildProcessor] 已移除 WebGL 输出中的本地视频目录。");
        }
    }
}
