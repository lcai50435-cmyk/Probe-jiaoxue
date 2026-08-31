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

        [Tooltip("是否循环播放；关闭时停留在最后一帧")]
        public bool loop = true;

        private DigitalHumanFrameConfig _config;
        private DigitalHumanFrameConfig.State _state;
        private Texture2D[] _pages;
        private Texture2D[] _oldPages; // 上一状态页，新页绑定成功后定向释放
        private int _frame;
        private float _clock;
        private bool _playing;
        private readonly HashSet<string> _warnedMissing = new HashSet<string>();

        public bool HasConfig() => Config() != null;

        /// <summary>切换到指定状态并从头循环播放；返回是否已有可绘制帧。相同状态默认不重复加载。</summary>
        public bool PlayState(string key, bool forceReload = false)
        {
            if (!forceReload && _state != null && _state.key == key) return HasDrawableTarget();
            var state = Config() != null ? Config().Get(key) : null;
            if (state == null || state.pages == null || state.pages.Length == 0)
            {
                WarnOnce(key, "缺少状态图集：" + key + "（请运行 Tools/DigitalHuman/生成微信透明帧图集）");
                StopAndHide();
                return false;
            }

            var pages = new Texture2D[state.pages.Length];
            var validPages = 0;
            for (var i = 0; i < state.pages.Length; i++)
            {
                pages[i] = Resources.Load<Texture2D>(state.pages[i]);
                if (pages[i] == null)
                    WarnOnce(key + "/" + i, "图集页加载失败：" + state.pages[i]);
                else
                    validPages++;
            }
            if (validPages != pages.Length)
            {
                UnloadPages(pages, _pages);
                StopAndHide();
                return false;
            }

            // 不先解绑旧页：切换期间 RawImage 保留上一画面作兜底，新页绑定成功后定向释放旧页。
            _oldPages = _pages;
            _state = state;
            _pages = pages;
            _frame = FirstFrameOnLoadedPage();
            _clock = _state.fps > 0f ? _frame / _state.fps : 0f;
            _playing = ApplyFrame();
            if (!_playing)
            {
                StopAndHide();
                return false;
            }
            FlushOldPages();
            Debug.Log($"[M1DigitalHumanFramePlayer] 切态 {key}：{_pages.Length} 页 @ {state.fps}fps（{state.frameCount} 帧），旧页已定向释放。");
            return true;
        }

        private void Update()
        {
            if (!_playing || _state == null || _state.fps <= 0f) return;
            _clock += Time.unscaledDeltaTime;
            var frame = (int)(_clock * _state.fps);
            var f = loop ? frame % _state.frameCount : Mathf.Min(frame, _state.frameCount - 1);
            if (f != _frame)
            {
                _frame = f;
                if (!ApplyFrame()) StopAndHide();
            }
        }

        private bool ApplyFrame()
        {
            if (target == null || _pages == null || _state == null) return false;
            var perPage = _state.cols * _state.rows;
            if (perPage <= 0 || _state.frameCount <= 0) return false;
            var page = _frame / perPage;
            var index = _frame % perPage;
            if (page >= _pages.Length || _pages[page] == null) return false;
            var tex = _pages[page];
            if (tex.width <= 0 || tex.height <= 0) return false;
            var col = index % _state.cols;
            var row = index / _state.cols;
            var cellW = _state.frameWidth + _state.gutter * 2;
            var cellH = _state.frameHeight + _state.gutter * 2;
            if (cellW <= 0 || cellH <= 0) return false;
            // 像素/UV 均以纹理左下为原点（Unity 惯例），帧内容按同方向写入图集，直接换算。
            var u0 = (col * (float)cellW + _state.gutter) / tex.width;
            var v0 = (row * (float)cellH + _state.gutter) / tex.height;
            target.texture = tex;
            target.uvRect = new Rect(u0, v0, (float)_state.frameWidth / tex.width, (float)_state.frameHeight / tex.height);
            return true;
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
            UnloadPages(_oldPages, _pages);
            _oldPages = null;
        }

        private int FirstFrameOnLoadedPage()
        {
            var perPage = _state.cols * _state.rows;
            if (perPage <= 0) return 0;
            for (var i = 0; i < _pages.Length; i++)
                if (_pages[i] != null && i * perPage < _state.frameCount) return i * perPage;
            return 0;
        }

        private bool HasDrawableTarget() => target != null && target.texture != null;

        private void StopAndHide()
        {
            _playing = false;
            _state = null;
            _frame = 0;
            _clock = 0f;
            if (target != null)
            {
                target.texture = null;
                target.enabled = false;
            }
            UnloadPages(_oldPages, _pages);
            UnloadPages(_pages, null);
            _pages = null;
            _oldPages = null;
        }

        private static void UnloadPages(Texture2D[] pages, Texture2D[] keep)
        {
            if (pages == null) return;
            foreach (var page in pages)
            {
                if (page == null || Contains(keep, page)) continue;
                Resources.UnloadAsset(page);
            }
        }

        private static bool Contains(Texture2D[] pages, Texture2D page)
        {
            if (pages == null) return false;
            foreach (var candidate in pages)
                if (candidate == page) return true;
            return false;
        }

        /// <summary>同类错误只报一次，避免刷屏。</summary>
        private void WarnOnce(string id, string message)
        {
            if (_warnedMissing.Add(id))
                Debug.LogError("[M1DigitalHumanFramePlayer] " + message);
        }

        private void OnDestroy() => ReleaseAll();

        private void ReleaseAll() => StopAndHide();
    }
}
