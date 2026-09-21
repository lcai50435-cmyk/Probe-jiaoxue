using UnityEngine;

namespace M1
{
    /// <summary>
    /// 微信/WebGL 端无密钥 AI 代理配置：客户端通过 WX.cloud.CallFunction 调用 CloudBase 普通云函数，
    /// Key 保存在云函数环境变量里，绝不进入小游戏包。Android/Editor 不使用本配置（仍走共享 DeepSeekConfig 直连）。
    /// 普通云函数不需要 request 合法域名，无需开启 HTTP 访问服务。
    /// </summary>
    [CreateAssetMenu(fileName = "AiProxyConfig", menuName = "M1/AiProxyConfig")]
    public sealed class AiProxyConfig : ScriptableObject
    {
        public const string ResourcePath = "AiProxyConfig";

        [Tooltip("CloudBase 云开发环境 ID（deepseek-proxy 普通云函数所在环境，如 cloud1-d6gmycfs6b37edb43）。留空表示问答未开放。")]
        public string envId = "";

        [Tooltip("普通云函数名（默认 deepseek-proxy）")]
        public string functionName = "deepseek-proxy";

        [Min(1f)]
        [Tooltip("云函数调用超时（秒）")]
        public float timeout = 40f;

        /// <summary>微信端问答数据源。</summary>
        public enum WechatQaMode
        {
            CloudProxy = 0,
            PresetLibrary = 1,
        }

        [Tooltip("问答模式：CloudProxy=云函数实时 AI（体验版）；PresetLibrary=预设话术库（正式提审版，零网络请求）。")]
        public WechatQaMode qaMode = WechatQaMode.CloudProxy;

        public bool UsePresetLibrary => qaMode == WechatQaMode.PresetLibrary;

        public bool IsConfigured => !string.IsNullOrWhiteSpace(envId) && !string.IsNullOrWhiteSpace(functionName);

        public static AiProxyConfig Load()
        {
            return Resources.Load<AiProxyConfig>(ResourcePath);
        }
    }
}
