using UnityEngine;

namespace M1
{
    /// <summary>所有教学模块共用的 DeepSeek 连接配置，固定从 Resources/DeepSeekConfig 加载。</summary>
    public sealed class DeepSeekConfig : ScriptableObject
    {
        public const string ResourcePath = "DeepSeekConfig";

        [Tooltip("OpenAI 兼容端点")]
        public string baseUrl = "https://api.deepseek.com/v1";
        [Tooltip("API Key（本地资产不纳入版本控制）")]
        public string apiKey = "";
        [Tooltip("对话模型")]
        public string model = "deepseek-chat";
        [Range(0f, 2f)]
        [Tooltip("随机性")]
        public float temperature = 1f;
        [TextArea(2, 4)]
        [Tooltip("人设提示词")]
        public string systemPrompt =
            "你是“铁小探”，钢轨探伤仿真教学的 AI 讲师。请用简洁、专业的语言回答钢轨探伤原理、操作技巧、波形解读等问题。";
        [Min(1f)]
        [Tooltip("请求超时（秒）")]
        public float timeout = 30f;

        public bool IsConfigured => !string.IsNullOrWhiteSpace(baseUrl) &&
                                    !string.IsNullOrWhiteSpace(apiKey) &&
                                    !string.IsNullOrWhiteSpace(model);

        public static DeepSeekConfig Load()
        {
            return Resources.Load<DeepSeekConfig>(ResourcePath);
        }
    }
}
