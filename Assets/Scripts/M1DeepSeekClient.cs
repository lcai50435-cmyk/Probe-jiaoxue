using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Serialization;

namespace M1
{
    /// <summary>
    /// DeepSeek API 客户端（OpenAI 兼容，非流式）。
    /// 连接配置统一从 Resources/DeepSeekConfig 加载；所有模块复用同一资产。
    /// 由 M1QASetup 挂到 M1 画板，M3DigitalHumanBootstrap 运行时挂到后续模块。
    /// </summary>
    public class M1DeepSeekClient : MonoBehaviour
    {
        // 仅用于从旧场景字段迁移到共享资产；运行时请求绝不读取这些值。
        [SerializeField, HideInInspector, FormerlySerializedAs("baseUrl")] private string legacyBaseUrl;
        [SerializeField, HideInInspector, FormerlySerializedAs("apiKey")] private string legacyApiKey;
        [SerializeField, HideInInspector, FormerlySerializedAs("model")] private string legacyModel;
        [SerializeField, HideInInspector, FormerlySerializedAs("temperature")] private float legacyTemperature;
        [SerializeField, HideInInspector, FormerlySerializedAs("systemPrompt")] private string legacySystemPrompt;
        [SerializeField, HideInInspector, FormerlySerializedAs("timeout")] private float legacyTimeout;

        public bool IsConfigured
        {
            get
            {
                var config = DeepSeekConfig.Load();
                return config != null && config.IsConfigured;
            }
        }

        /// <summary>仅供 M1QASetup 在非冻结 M1 场景中执行旧配置迁移。</summary>
        public bool MigrateLegacyConfiguration(DeepSeekConfig config)
        {
            if (config == null) return false;
            var changed = false;
            if (string.IsNullOrWhiteSpace(config.apiKey) && !string.IsNullOrWhiteSpace(legacyApiKey))
            {
                config.baseUrl = legacyBaseUrl;
                config.apiKey = legacyApiKey;
                config.model = legacyModel;
                config.temperature = legacyTemperature;
                config.systemPrompt = legacySystemPrompt;
                config.timeout = legacyTimeout;
                changed = true;
            }
            ClearLegacyConfiguration();
            return changed;
        }

        public void ClearLegacyConfiguration()
        {
            legacyBaseUrl = legacyApiKey = legacyModel = legacySystemPrompt = string.Empty;
            legacyTemperature = legacyTimeout = 0f;
        }

        /// <summary>发起对话请求。成功回调回复文本；失败回调中文错误提示。协程需要外部 StartCoroutine 驱动。</summary>
        public IEnumerator ChatAsync(string userMessage, Action<string> onSuccess, Action<string> onError)
        {
            var config = DeepSeekConfig.Load();
            if (config == null || !config.IsConfigured)
            {
                onError?.Invoke("尚未配置 AI 服务：请在 Assets/Resources/DeepSeekConfig.asset 中填写一次。");
                yield break;
            }

            var body = JsonUtility.ToJson(new RequestBody(config.model, config.temperature, config.systemPrompt, userMessage));
            using var req = new UnityWebRequest(config.baseUrl.TrimEnd('/') + "/chat/completions", "POST");
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("Authorization", "Bearer " + config.apiKey);
            req.timeout = Mathf.RoundToInt(config.timeout);

            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                onError?.Invoke("网络连接失败，请检查网络后重试。");
                yield break;
            }

            var response = JsonUtility.FromJson<ChatResponse>(req.downloadHandler.text);
            if (response.choices == null || response.choices.Length == 0 ||
                string.IsNullOrEmpty(response.choices[0].message.content))
            {
                onError?.Invoke("AI 返回内容为空，请换个问法试试。");
                yield break;
            }

            onSuccess?.Invoke(response.choices[0].message.content);
        }

        [Serializable]
        private class RequestBody
        {
            public string model;
            public float temperature;
            public Message[] messages;

            public RequestBody(string model, float temperature, string system, string user)
            {
                this.model = model;
                this.temperature = temperature;
                messages = new[] { new Message("system", system), new Message("user", user) };
            }
        }

        [Serializable]
        private class Message
        {
            public string role;
            public string content;

            public Message(string role, string content)
            {
                this.role = role;
                this.content = content;
            }
        }

        [Serializable]
        private class ChatResponse
        {
            public Choice[] choices;

            [Serializable]
            public class Choice
            {
                public Msg message;
            }

            [Serializable]
            public class Msg
            {
                public string content;
            }
        }
    }
}
