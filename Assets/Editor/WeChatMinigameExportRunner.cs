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
                EditorApplication.Exit(2);
        }
    }
}
