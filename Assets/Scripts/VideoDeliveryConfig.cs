using System.IO;
using UnityEngine;

namespace M1
{
    [CreateAssetMenu(fileName = "VideoDeliveryConfig", menuName = "Probe/Video Delivery Config")]
    public sealed class VideoDeliveryConfig : ScriptableObject
    {
        public string cloudBaseUrl = "https://cloud1-d6gmycfs6b37edb43-1476749432.tcloudbaseapp.com/rail-inspection/videos";
        public string introFile = "m1-intro-faststart.mp4";
        [Tooltip("微信小游戏 videos 分包内引导视频文件路径（空=停用包内源走 CDN；文件由导出后处理复制进分包）")]
        public string wechatIntroPackageFile = "videos/m1-intro-wx.mp4";
        public string idleFile = "digital-human-idle.mp4";
        public string thinkingFile = "digital-human-thinking.mp4";
        public string speakingFile = "digital-human-speaking.mp4";
        [Min(1)] public int videoWidth = 1080;
        [Min(1)] public int videoHeight = 1450;

        public static VideoDeliveryConfig Load() => Resources.Load<VideoDeliveryConfig>("VideoDeliveryConfig");

        public bool UseRemoteVideo => Application.platform == RuntimePlatform.WebGLPlayer;

        public string IntroUrl => Resolve(introFile);
        public string IdleUrl => Resolve(idleFile);
        public string ThinkingUrl => Resolve(thinkingFile);
        public string SpeakingUrl => Resolve(speakingFile);

        private string Resolve(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return string.Empty;
            if (UseRemoteVideo)
                return cloudBaseUrl.TrimEnd('/') + "/" + fileName;
            return Path.Combine(Application.streamingAssetsPath, "videos", fileName).Replace("\\", "/");
        }
    }

    /// <summary>微信小游戏视频解码器只能安全运行一个实例时的跨组件闸门。</summary>
    public static class WebGlVideoPlaybackGate
    {
        public static bool IntroActive { get; set; }
    }
}
