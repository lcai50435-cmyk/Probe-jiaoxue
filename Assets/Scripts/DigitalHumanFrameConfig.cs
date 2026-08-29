using System;
using UnityEngine;

namespace M1
{
    /// <summary>
    /// 微信常驻数字人透明帧图集配置（Editor 工具 DigitalHumanAtlasTool 生成，Resources 加载）：
    /// 每个状态一份独立图集页清单；页按 Resources 路径字符串引用而非纹理引用，
    /// 保证运行时只 Load 当前状态的页（三个动作不同时常驻内存）。
    /// </summary>
    [CreateAssetMenu(fileName = "FrameAnimConfig", menuName = "Probe/Digital Human Frame Config")]
    public sealed class DigitalHumanFrameConfig : ScriptableObject
    {
        [Serializable]
        public sealed class State
        {
            [Tooltip("状态键：idle / thinking / speaking")]
            public string key;

            [Tooltip("播放帧率（抽帧降采样后的目标 fps）")]
            public float fps;

            public int frameCount;

            [Tooltip("单帧像素宽高与边缘外扩（gutter 防双线性渗色）")]
            public int frameWidth;
            public int frameHeight;
            public int gutter;

            [Tooltip("每页列数/行数")]
            public int cols;
            public int rows;

            [Tooltip("图集页 Resources 路径（不含扩展名，如 DigitalHuman/Frames/idle_0）")]
            public string[] pages;
        }

        public State[] states;

        public State Get(string key)
        {
            if (states == null || string.IsNullOrEmpty(key)) return null;
            foreach (var s in states)
                if (s != null && s.key == key) return s;
            return null;
        }
    }
}
