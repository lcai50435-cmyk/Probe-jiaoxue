using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace M1
{
    /// <summary>
    /// 微信常驻数字人透明帧图集播放器：RawImage + uvRect 翻页显示。
    /// 每次只驻留当前显示页，跨页/切态均先绑定新页再定向释放旧页，避免三态图集同时占用内存。
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

        /// <summary>intro 状态键：仅海报后端（iOS/开发者工具）会播放，见 M1IntroVideo.TryPlayPosterFrameAnimation。</summary>
        private const string IntroStateKey = "intro";

        /// <summary>
        /// 进程级钉住页：intro 图集页在 FinishIntro→StopAndRelease 后会被 Resources.UnloadAsset 卸载，
        /// 同进程二次引导（M5 完成后返回重载 M1）需重新加载；微信 WebGL 上卸载后二次加载不可靠（09-05 冻结修复）。
        /// 仅 intro 状态进缓存：Android 常驻数字人三态（idle/thinking/speaking）永不进入，释放行为不变。
        /// </summary>
        private static readonly Dictionary<string, Texture2D> _pinnedPages = new Dictionary<string, Texture2D>();

        private DigitalHumanFrameConfig _config;
        private DigitalHumanFrameConfig.State _state;
        private Texture2D _page;
        private int _pageIndex = -1;
        private int _frame;
        private float _clock;
        private bool _playing;
        private readonly HashSet<string> _warnedMissing = new HashSet<string>();

        public event System.Action PlaybackFailed;

        /// <summary>最近一次画面推进的 realtime 时间戳（海报卡帧放行 watchdog 用；-1=尚未绑定过画面）。</summary>
        public float LastAdvanceRealtime { get; private set; } = -1f;

        public bool HasConfig() => Config() != null;

        /// <summary>切换到指定状态并从头播放；同一状态默认保持当前页与帧位。</summary>
        public bool PlayState(string key, bool forceReload = false)
        {
            if (!forceReload && _state != null && _state.key == key) return HasDrawableTarget();

            var state = Config() != null ? Config().Get(key) : null;
            if (!HasFirstPage(state))
            {
                WarnOnce(key, "缺少状态图集或第一页：" + key + "（请运行 Tools/DigitalHuman/生成微信透明帧图集）");
                StopAndRelease();
                return false;
            }

            // 切态期间保留旧画面；首帧的新页绑定成功后才释放旧页。
            var oldPage = _page;
            _state = state;
            _page = null;
            _pageIndex = -1;
            _frame = 0;
            _clock = 0f;
            _playing = ApplyFrame();
            if (!_playing)
            {
                ReleasePage(oldPage, _page);
                StopAndRelease();
                return false;
            }
            LastAdvanceRealtime = Time.realtimeSinceStartup;

            ReleasePage(oldPage, _page);
            Debug.Log($"[M1DigitalHumanFramePlayer] 切态 {key}：第 {_pageIndex + 1}/{state.pages.Length} 页 @ {state.fps}fps（{state.frameCount} 帧）。");
            return true;
        }

        private void Update()
        {
            if (!_playing || _state == null || _state.fps <= 0f) return;
            _clock += Time.unscaledDeltaTime;
            var frame = (int)(_clock * _state.fps);
            var nextFrame = loop ? frame % _state.frameCount : Mathf.Min(frame, _state.frameCount - 1);
            if (nextFrame == _frame) return;

            _frame = nextFrame;
            LastAdvanceRealtime = Time.realtimeSinceStartup;
            if (!ApplyFrame())
            {
                StopAndRelease();
                PlaybackFailed?.Invoke();
            }
        }

        private bool ApplyFrame()
        {
            if (target == null || _state == null) return false;
            var perPage = _state.cols * _state.rows;
            if (perPage <= 0 || _state.frameCount <= 0) return false;

            var nextPageIndex = _frame / perPage;
            var index = _frame % perPage;
            if (nextPageIndex >= _state.pages.Length) return false;

            var nextPage = _page;
            var pageChanged = _pageIndex != nextPageIndex || nextPage == null;
            if (pageChanged)
            {
                var path = _state.pages[nextPageIndex];
                nextPage = string.IsNullOrEmpty(path) ? null : LoadPage(path);
                if (nextPage == null)
                {
                    WarnOnce(_state.key + "/" + nextPageIndex, "图集页加载失败：" + path);
                    return false;
                }
            }
            if (nextPage.width <= 0 || nextPage.height <= 0) return false;

            var cellW = _state.frameWidth + _state.gutter * 2;
            var cellH = _state.frameHeight + _state.gutter * 2;
            if (cellW <= 0 || cellH <= 0) return false;

            var col = index % _state.cols;
            var row = index / _state.cols;
            var u0 = (col * (float)cellW + _state.gutter) / nextPage.width;
            var v0 = (row * (float)cellH + _state.gutter) / nextPage.height;
            var oldPage = _page;
            target.texture = nextPage;
            target.uvRect = new Rect(u0, v0, (float)_state.frameWidth / nextPage.width, (float)_state.frameHeight / nextPage.height);

            if (pageChanged)
            {
                _page = nextPage;
                _pageIndex = nextPageIndex;
                ReleasePage(oldPage, nextPage);
            }
            return true;
        }

        private DigitalHumanFrameConfig Config()
        {
            if (_config == null) _config = Resources.Load<DigitalHumanFrameConfig>(configPath);
            return _config;
        }

        /// <summary>页加载入口：intro 状态的页加载成功后进程级钉住，之后直接命中缓存不再走 Resources.Load。</summary>
        private Texture2D LoadPage(string path)
        {
            if (_pinnedPages.TryGetValue(path, out var pinned) && pinned != null) return pinned;
            var page = Resources.Load<Texture2D>(path);
            if (page != null && _state != null && _state.key == IntroStateKey) _pinnedPages[path] = page;
            return page;
        }

        /// <summary>停止播放并只释放当前图集页；不触发全局 Resources 扫描。钉住的 intro 页保留在缓存中不卸载。</summary>
        public void StopAndRelease()
        {
            _playing = false;
            _state = null;
            _frame = 0;
            _clock = 0f;
            LastAdvanceRealtime = -1f;
            var page = _page;
            _page = null;
            _pageIndex = -1;
            if (target != null)
            {
                target.texture = null;
                target.enabled = false;
            }
            ReleasePage(page, null);
        }

        private static bool HasFirstPage(DigitalHumanFrameConfig.State state)
        {
            return state != null && state.frameCount > 0 && state.cols > 0 && state.rows > 0
                && state.frameWidth > 0 && state.frameHeight > 0 && state.pages != null
                && state.pages.Length > 0 && !string.IsNullOrEmpty(state.pages[0]);
        }

        private bool HasDrawableTarget() => target != null && _page != null && target.texture == _page;

        private static void ReleasePage(Texture2D page, Texture2D keep)
        {
            if (page == null || page == keep) return;
            if (_pinnedPages.ContainsValue(page)) return; // intro 钉住页不卸载，保障同进程二次引导重播
            Resources.UnloadAsset(page);
        }

        /// <summary>同类错误只报一次，避免刷屏。</summary>
        private void WarnOnce(string id, string message)
        {
            if (_warnedMissing.Add(id))
                Debug.LogError("[M1DigitalHumanFramePlayer] " + message);
        }

        private void OnDestroy() => StopAndRelease();
    }
}
