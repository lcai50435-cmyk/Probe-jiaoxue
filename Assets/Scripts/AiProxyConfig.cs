using UnityEngine;

namespace M1
{
    /// <summary>
    /// 微信/WebGL 端无密钥 AI 代理配置：客户端只把用户问题 POST 给 CloudBase 云函数，
    /// Key 保存在云函数环境变量里，绝不进入小游戏包。Android/Editor 不使用本配置（仍走共享 DeepSeekConfig 直连）。
    /// </summary>
    [CreateAssetMenu(fileName = "AiProxyConfig", menuName = "M1/AiProxyConfig")]
    public sealed class AiProxyConfig : ScriptableObject
    {
        public const string ResourcePath = "AiProxyConfig";

        [Tooltip("CloudBase 云函数 HTTP 访问地址（POST JSON {\"message\":\"...\"}，返回 OpenAI 兼容 choices）。留空表示问答未开放。")]
        public string proxyUrl = "";

        [Min(1f)]
        [Tooltip("请求超时（秒）")]
        public float timeout = 30f;

        public bool IsConfigured => !string.IsNullOrWhiteSpace(proxyUrl) && proxyUrl.StartsWith("https://");

        public static AiProxyConfig Load()
        {
            return Resources.Load<AiProxyConfig>(ResourcePath);
        }
    }
}
