using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace M3
{
    /// <summary>M3 数字人轻量播放器：运行时用现有 FullBodyPreview 的位置/尺寸创建 RawImage+VideoPlayer，
    /// 播放待机视频，隐藏静态预览图。不依赖 QA 面板，仅让数字人先动起来。</summary>
    public class M3DigitalHumanVideo : MonoBehaviour
    {
        public VideoClip idleClip;
        public Material lumaKeyMaterial;
        public Vector2 fallbackSize = new Vector2(304f, 430f);

        private VideoPlayer _player;
        private RenderTexture _rt;
        private RawImage _raw;

        private void Awake()
        {
            var preview = transform.Find("FullBodyPreview");
            RectTransform previewRt = null;
            if (preview != null)
            {
                previewRt = preview.GetComponent<RectTransform>();
                preview.gameObject.SetActive(false);
            }

            var go = new GameObject("FullBodyView", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            go.transform.SetParent(transform, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(.5f, 0f);
            rt.anchorMax = new Vector2(.5f, 1f);
            rt.pivot = new Vector2(.5f, .5f);
            rt.anchoredPosition = previewRt != null ? previewRt.anchoredPosition : Vector2.zero;
            rt.sizeDelta = previewRt != null ? previewRt.sizeDelta : fallbackSize;

            _raw = go.GetComponent<RawImage>();
            if (lumaKeyMaterial != null) _raw.material = lumaKeyMaterial;
            _raw.raycastTarget = false;

            _player = go.AddComponent<VideoPlayer>();
            _player.playOnAwake = false;
            _player.isLooping = true;
            _player.renderMode = VideoRenderMode.RenderTexture;
            _player.audioOutputMode = VideoAudioOutputMode.None;
            _player.skipOnDrop = true;
            _player.clip = idleClip;
            if (idleClip != null)
            {
                _rt = new RenderTexture((int)idleClip.width, (int)idleClip.height, 0)
                { useMipMap = true, autoGenerateMips = false };
                _player.targetTexture = _rt;
                _raw.texture = _rt;
                _player.Play();
            }
        }

        private void Update()
        {
            if (_rt != null && _rt.IsCreated()) _rt.GenerateMips();
        }

        private void OnDestroy()
        {
            if (_player != null) _player.Stop();
            if (_rt != null) { _rt.Release(); Destroy(_rt); }
        }
    }
}
