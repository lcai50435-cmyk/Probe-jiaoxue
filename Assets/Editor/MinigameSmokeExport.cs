using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using WeChatWASM;

namespace M1.EditorTools
{
    /// <summary>微信小游戏冒烟导出：验证当前引擎版本能否走通 WebGL 构建 + 小游戏转换管线。</summary>
    public static class MinigameSmokeExport
    {
        private const string RelativeDst = "Builds/WXExport";

        [MenuItem("Tools/Build/Minigame Smoke Export")]
        public static void ExportFromMenu() => Finish(Run());

        /// <summary>批处理入口：Unity -executeMethod M1.EditorTools.MinigameSmokeExport.ExportBatch。</summary>
        public static void ExportBatch()
        {
            Finish(Run());
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        private static WXConvertCore.WXExportError Run()
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.WebGL, BuildTarget.WebGL))
                throw new InvalidOperationException("未安装 Unity WebGL Build Support。");

            var config = UnityUtil.GetEditorConf();
            if (string.IsNullOrEmpty(config.ProjectConf.relativeDST))
            {
                config.ProjectConf.relativeDST = RelativeDst;
                config.ProjectConf.DST = Path.GetFullPath(RelativeDst);
                EditorUtility.SetDirty(config);
                AssetDatabase.SaveAssets();
            }
            return WXEditorWin.DoExport(true);
        }

        private static void Finish(WXConvertCore.WXExportError error)
        {
            if (error != WXConvertCore.WXExportError.SUCCEED)
                throw new InvalidOperationException("小游戏冒烟导出失败：" + error);
            Debug.Log("[MinigameSmokeExport] 导出成功：" + Path.GetFullPath(RelativeDst));
        }
    }
}
