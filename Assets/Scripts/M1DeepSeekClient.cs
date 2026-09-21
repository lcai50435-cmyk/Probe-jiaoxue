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
#if UNITY_WEBGL && !UNITY_EDITOR
                var proxy = AiProxyConfig.Load();
                if (proxy != null && proxy.UsePresetLibrary) return PresetQALibrary.Load() != null;
                return proxy != null && proxy.IsConfigured;
#else
                var config = DeepSeekConfig.Load();
                return config != null && config.IsConfigured;
#endif
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
#if UNITY_WEBGL && !UNITY_EDITOR
            var proxy = AiProxyConfig.Load();
            if (proxy != null && proxy.UsePresetLibrary)
                return ChatPresetAsync(userMessage, onSuccess, onError);
            return ChatViaProxyAsync(userMessage, onSuccess, onError);
#else
            return ChatDirectAsync(userMessage, onSuccess, onError);
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        /// <summary>预设话术模式（正式提审版）：零网络请求、零生成式内容；话术库缺失按未开放处理，绝不回落云函数。</summary>
        private IEnumerator ChatPresetAsync(string userMessage, Action<string> onSuccess, Action<string> onError)
        {
            var library = PresetQALibrary.Load();
            if (library == null)
            {
                onError?.Invoke("AI 问答暂未开放，敬请期待。");
                yield break;
            }
            if (library.replyDelay > 0f)
            {
                var doneAt = Time.unscaledTime + library.replyDelay; // 问答面板暂停（timeScale=0）时仍按真实时间推进
                while (Time.unscaledTime < doneAt) yield return null;
            }
            onSuccess?.Invoke(library.Match(userMessage));
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        /// <summary>微信端通过 WX.cloud.CallFunction 调用无密钥 CloudBase 普通云函数（Key 在云函数环境变量，客户端零凭据）；代理未配置时不发任何请求。</summary>
        private IEnumerator ChatViaProxyAsync(string userMessage, Action<string> onSuccess, Action<string> onError)
        {
            var proxy = AiProxyConfig.Load();
            if (proxy == null || !proxy.IsConfigured)
            {
                onError?.Invoke("AI 问答暂未开放，敬请期待。");
                yield break;
            }

            var done = false;
            var timedOut = false;
            var reply = string.Empty;
            var errMsg = string.Empty;
            var startedAt = Time.unscaledTime; // QA 面板暂停（timeScale=0）时仍按真实时间超时

            try
            {
                var cloudConfig = new WeChatWASM.ICloudConfig { env = proxy.envId };
                WeChatWASM.WX.cloud.Init(cloudConfig);
                WeChatWASM.WX.cloud.CallFunction(new WeChatWASM.CallFunctionParam
                {
                    name = proxy.functionName,
                    data = new ProxyRequestBody(userMessage),
                    slow = true,
                    config = cloudConfig,
                    success = res =>
                    {
                        reply = res.result ?? string.Empty; // 云函数返回对象 JSON 序列化字符串
                        done = true;
                    },
                    fail = res =>
                    {
                        errMsg = res.errMsg ?? string.Empty;
                        done = true;
                    },
                });
            }
            catch (Exception e)
            {
                Debug.LogError("[M1DeepSeekClient] 云函数调用初始化失败：" + e.Message);
                onError?.Invoke("AI 服务暂不可用，请稍后再试。");
                yield break;
            }

            while (!done)
            {
                if (Time.unscaledTime - startedAt >= proxy.timeout)
                {
                    timedOut = true;
                    break;
                }
                yield return null;
            }
            if (timedOut)
            {
                onError?.Invoke("AI 回答超时，请稍后重试。");
                yield break;
            }
            if (!string.IsNullOrEmpty(errMsg))
            {
                Debug.LogWarning("[M1DeepSeekClient] 云函数调用失败：" + errMsg);
                onError?.Invoke("AI 服务暂不可用，请稍后再试。");
                yield break;
            }
            if (string.IsNullOrEmpty(reply))
            {
                onError?.Invoke("AI 返回内容为空，请换个问法试试。");
                yield break;
            }

            var response = JsonUtility.FromJson<ChatResponse>(reply);
            if (response == null || response.choices == null || response.choices.Length == 0 ||
                string.IsNullOrEmpty(response.choices[0].message.content))
            {
                onError?.Invoke(!string.IsNullOrEmpty(response?.error)
                    ? response.error
                    : "AI 返回内容为空，请换个问法试试。");
                yield break;
            }

            onSuccess?.Invoke(response.choices[0].message.content);
        }

        [Serializable]
        private class ProxyRequestBody
        {
            public string message;

            public ProxyRequestBody(string message) { this.message = message; }
        }
#else
        /// <summary>Android/Editor 保持既有直连共享 DeepSeekConfig 的行为，不做平台迁移。</summary>
        private IEnumerator ChatDirectAsync(string userMessage, Action<string> onSuccess, Action<string> onError)
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
#endif

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
            public string error;

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
