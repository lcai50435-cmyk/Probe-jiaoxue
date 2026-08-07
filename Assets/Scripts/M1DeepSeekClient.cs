using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace M1
{
    /// <summary>
    /// DeepSeek API 客户端（OpenAI 兼容，非流式）。
    /// 配置驱动：全部参数走 Inspector；M2/M3 模块可复用。
    /// 由 M1QASetup 挂到 "画板" 上并注入引用。
    /// </summary>
    public class M1DeepSeekClient : MonoBehaviour
    {
        [Header("DeepSeek API 配置")]
        [Tooltip("OpenAI 兼容端点")]
        public string baseUrl = "https://api.deepseek.com/v1";
        [Tooltip("API Key（platform.deepseek.com 申请；留空则不发起请求）")]
        public string apiKey = "";
        [Tooltip("对话模型")]
        public string model = "deepseek-chat";
        [Tooltip("随机性 0~2")]
        public float temperature = 1.0f;
        [Tooltip("人设提示词")]
        [TextArea(2, 4)]
        public string systemPrompt =
            "你是“铁小探”，钢轨探伤仿真教学的 AI 讲师。请用简洁、专业的语言回答钢轨探伤原理、操作技巧、波形解读等问题。";
        [Tooltip("请求超时（秒）")]
        public float timeout = 30f;

        /// <summary>发起对话请求。成功回调回复文本；失败回调中文错误提示。协程需要外部 StartCoroutine 驱动。</summary>
        public IEnumerator ChatAsync(string userMessage, Action<string> onSuccess, Action<string> onError)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                onError?.Invoke("尚未配置 API Key：请在画板 Inspector 的 M1DeepSeekClient 中填写。");
                yield break;
            }

            var body = JsonUtility.ToJson(new RequestBody(model, temperature, systemPrompt, userMessage));
            using var req = new UnityWebRequest(baseUrl.TrimEnd('/') + "/chat/completions", "POST");
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("Authorization", "Bearer " + apiKey);
            req.timeout = Mathf.RoundToInt(timeout);

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
