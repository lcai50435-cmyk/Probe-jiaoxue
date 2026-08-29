using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace M1
{
    /// <summary>
    /// 微信常驻数字人透明帧图集播放器（2026-08-28，替代第二个 VideoPlayer——微信解码器只支持一个实例）：
    /// RawImage + uvRect 翻页显示；一次只加载当前状态图集页，切换状态后逐页定向释放旧页
    /// （Resources.UnloadAsset，不触发全局 UnloadUnusedAssets 扫描，避免问答切态主线程卡顿）；
    /// unscaled 计时，问答面板全局暂停（timeScale=0）不影响动画。挂在数字人 RawImage 同节点，由 Presenter 自动创建。
    /// </summary>
    [RequireComponent(typeof(RawImage))]
    public sealed class M1DigitalHumanFramePlayer : MonoBehaviour
    {
        [Tooltip("Resources 下的配置资产路径（不含扩展名）")]
        public string configPath = "DigitalHuman/FrameAnimConfig";

        [Tooltip("显示帧的 RawImage（Presenter 注入）")]
        public RawImage target;

        private DigitalHumanFrameConfig _config;
        private DigitalHumanFrameConfig.State _state;
        private Texture2D[] _pages;
        private Texture2D[] _oldPages; // 上一状态页，新页绑定成功后定向释放
        private int _frame;
        private float _clock;
        private bool _playing;
        private readonly HashSet<string> _warnedMissing = new HashSet<string>();

        public bool HasConfig() => Config() != null;

        /// <summary>切换到指定状态并从头循环播放；相同状态重复调用忽略。</summary>
        public void PlayState(string key)
        {
            if (_playing && _state != null && _state.key == key) return;
            var state = Config() != null ? Config().Get(key) : null;
            if (state == null || state.pages == null || state.pages.Length == 0)
            {
                WarnOnce(key, "缺少状态图集：" + key + "（请运行 Tools/DigitalHuman/生成微信透明帧图集）");
                return;
            }
            // 不先解绑旧页：切换期间 RawImage 保留上一画面作兜底（零空白帧），新页就绪后定向释放旧页
            _oldPages = _pages;
            _state = state;
            _pages = new Texture2D[state.pages.Length];
            for (var i = 0; i < state.pages.Length; i++)
            {
                _pages[i] = Resources.Load<Texture2D>(state.pages[i]);
                if (_pages[i] == null)
                    WarnOnce(key + "/" + i, "图集页加载失败：" + state.pages[i] + "（保留当前画面兜底）");
            }
            _frame = 0;
            _clock = 0f;
            _playing = true;
            ApplyFrame();
            FlushOldPages();
            Debug.Log($"[M1DigitalHumanFramePlayer] 切态 {key}：{_pages.Length} 页 @ {state.fps}fps（{state.frameCount} 帧），旧页已定向释放。");
        }

        private void Update()
        {
            if (!_playing || _state == null || _state.fps <= 0f) return;
            _clock += Time.unscaledDeltaTime;
            var f = (int)(_clock * _state.fps) % _state.frameCount;
            if (f != _frame) { _frame = f; ApplyFrame(); }
        }

        private void ApplyFrame()
        {
            if (target == null || _pages == null || _state == null) return;
            var perPage = _state.cols * _state.rows;
            var page = _frame / perPage;
            var index = _frame % perPage;
            if (page >= _pages.Length || _pages[page] == null) return;
            var tex = _pages[page];
            var col = index % _state.cols;
            var row = index / _state.cols;
            var cellW = _state.frameWidth + _state.gutter * 2;
            var cellH = _state.frameHeight + _state.gutter * 2;
            // 像素/UV 均以纹理左下为原点（Unity 惯例），帧内容按同方向写入图集，直接换算
            var u0 = (col * (float)cellW + _state.gutter) / tex.width;
            var v0 = (row * (float)cellH + _state.gutter) / tex.height;
            target.texture = tex;
            target.uvRect = new Rect(u0, v0, (float)_state.frameWidth / tex.width, (float)_state.frameHeight / tex.height);
        }

        private DigitalHumanFrameConfig Config()
        {
            if (_config == null) _config = Resources.Load<DigitalHumanFrameConfig>(configPath);
            return _config;
        }

        /// <summary>定向释放上一状态页：Resources.UnloadAsset 逐页卸载（无全局扫描）；
        /// 仍在 RawImage 上显示的页保留作兜底画面。</summary>
        private void FlushOldPages()
        {
            if (_oldPages == null) return;
            foreach (var p in _oldPages)
            {
                if (p == null) continue;
                if (target != null && target.texture == p) continue;
                Resources.UnloadAsset(p);
            }
            _oldPages = null;
        }

        /// <summary>同类错误只报一次，避免刷屏。</summary>
        private void WarnOnce(string id, string message)
        {
            if (_warnedMissing.Add(id))
                Debug.LogError("[M1DigitalHumanFramePlayer] " + message);
        }

        private void OnDestroy() => ReleaseAll();

        private void ReleaseAll()
        {
            if (_pages != null) foreach (var p in _pages) if (p != null) Resources.UnloadAsset(p);
            _pages = null;
            FlushOldPages();
            _state = null;
            _playing = false;
        }
    }
}
