using System;
using UnityEngine;

namespace M1
{
    /// <summary>
    /// 预设话术库：正式提审版问答数据源（固定文案、零网络请求）。
    /// 个人主体无法办理「深度合成」类目，提审版本 qaMode=PresetLibrary 时由本库应答。
    /// 文案改动后必须重跑 Tools/WeChat/生成微信字体子集（子集工具会自动扫描本资产文字）。
    /// </summary>
    [CreateAssetMenu(fileName = "PresetQALibrary", menuName = "M1/PresetQALibrary")]
    public sealed class PresetQALibrary : ScriptableObject
    {
        public const string ResourcePath = "PresetQALibrary";

        [Serializable]
        public sealed class Entry
        {
            [Tooltip("命中关键词（玩家问题包含任一词即得分，命中最多者优先）")]
            public string[] keywords;

            [TextArea(2, 6)] public string answer;
        }

        [Tooltip("问答条目，按命中关键词数择优")] public Entry[] entries = Array.Empty<Entry>();

        [TextArea(2, 5), Tooltip("无条目命中时的兜底回复")]
        public string defaultAnswer = "这个问题可以先结合当前步骤想一想哦。你也可以问我耦合剂、探头操作、角度调整、测量、透视视图或擦拭要求。";

        [Min(0f), Tooltip("模拟思考延迟（秒），0=立即回复；问答面板暂停时仍按真实时间推进")]
        public float replyDelay = 0.6f;

        public static PresetQALibrary Load()
        {
            return Resources.Load<PresetQALibrary>(ResourcePath);
        }

        /// <summary>关键词计分匹配：命中数最高者胜出，全部未命中返回 defaultAnswer。</summary>
        public string Match(string userMessage)
        {
            if (string.IsNullOrWhiteSpace(userMessage) || entries == null) return defaultAnswer;
            var message = userMessage.ToLowerInvariant();
            var best = defaultAnswer;
            var bestScore = 0;
            foreach (var entry in entries)
            {
                if (entry?.keywords == null || string.IsNullOrEmpty(entry.answer)) continue;
                var score = 0;
                foreach (var keyword in entry.keywords)
                {
                    if (!string.IsNullOrWhiteSpace(keyword) && message.Contains(keyword.Trim().ToLowerInvariant())) score++;
                }
                if (score > bestScore)
                {
                    bestScore = score;
                    best = entry.answer;
                }
            }
            return best;
        }
    }
}
