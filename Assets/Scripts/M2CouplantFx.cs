using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace M2
{
    /// <summary>M2 涂抹耦合剂：蓝色铁轨形状薄膜动画（冻结 Scene 运行时特效，不写回 Scene）。</summary>
    public class M2CouplantFx : MonoBehaviour
    {
        public RectTransform railBg, maskRt;   // 对齐基准（RailBackground）与薄膜容器（CouplantMask）
        public Image film;                     // CouplantOverlay/bg 的 Image
        public CanvasGroup group;              // CouplantOverlay 的 CanvasGroup
        public Color filmColor = new Color(.55f, .8f, .96f, .45f); // 浅蓝色半透明（老板 2026-08-15 调浅）
        public Vector4 coverRect = new Vector4(.005f, .222f, .993f, .553f); // 涂抹区域（相对 railBg 底左归一化：x,y,w,h）＝铁轨主体实心块，四边贴钢轨/铁轨边缘
        public float animDuration = 2f, holdDuration = .5f, fadeDuration = .5f; // 出现/停留/淡出
        private bool _playing;
        private Sprite _filmSprite;

        public void Bind(RectTransform rail, RectTransform mask, Image image, CanvasGroup cg)
        { railBg = rail; maskRt = mask; film = image; group = cg; }
        public void Play(Action onDone)
        {
            if (_playing) return;
            _playing = true;
            Setup();
            StartCoroutine(Anim(onDone));
        }
        public void Reset()
        {
            _playing = false; StopAllCoroutines();
            if (film != null) film.fillAmount = 0f;
            if (group != null) group.alpha = 1f;
            if (maskRt != null) maskRt.gameObject.SetActive(false);
        }
        private void Setup()
        {
            if (railBg != null && maskRt != null)   // 涂抹区域 = 铁轨 rect 上的 coverRect 子矩形（底左归一化）
            { maskRt.pivot = railBg.pivot; maskRt.sizeDelta = new Vector2(coverRect.z * railBg.sizeDelta.x, coverRect.w * railBg.sizeDelta.y); maskRt.anchoredPosition = railBg.anchoredPosition + new Vector2((coverRect.x + coverRect.z * .5f - .5f) * railBg.sizeDelta.x, (coverRect.y + coverRect.w * .5f - .5f) * railBg.sizeDelta.y); }
            if (film == null) { Debug.LogError("[M2CouplantFx] 薄膜 Image 缺失，无法显示耦合剂。"); return; }
            if (_filmSprite == null)
            {
                var tex = Resources.Load<Texture2D>("俯视角");   // 从铁轨图切出中间带子 Sprite，保留铁轨形状与边缘羽化
                if (tex != null)
                { var px = coverRect.x * tex.width; var py = coverRect.y * tex.height; var pw = Mathf.Clamp(coverRect.z * tex.width, 1f, tex.width); var ph = Mathf.Clamp(coverRect.w * tex.height, 1f, tex.height); _filmSprite = Sprite.Create(tex, new Rect(px, py, pw, ph), new Vector2(.5f, .5f)); }
                else Debug.LogError("[M2CouplantFx] 俯视角 纹理加载失败。");
            }
            if (_filmSprite != null) film.sprite = _filmSprite;
            film.type = Image.Type.Filled; film.fillMethod = Image.FillMethod.Horizontal;
            film.fillOrigin = 0; film.fillAmount = 0f; film.color = filmColor;
            if (group != null) group.alpha = 1f;
        }
        private IEnumerator Anim(Action onDone)
        {
            if (maskRt != null) maskRt.gameObject.SetActive(true);
            for (var t = 0f; t < animDuration; t += Time.deltaTime)
            { if (film != null) film.fillAmount = Mathf.Clamp01(t / animDuration); yield return null; }   // 从左至右铺满
            if (film != null) film.fillAmount = 1f;
            yield return new WaitForSeconds(holdDuration);   // 完整覆盖停留 0.5s（scaled，暂停挂起）
            for (var t = 0f; t < fadeDuration; t += Time.deltaTime)
            { if (group != null) group.alpha = 1f - Mathf.Clamp01(t / fadeDuration); yield return null; } // 淡化消失
            if (group != null) group.alpha = 0f;
            if (maskRt != null) maskRt.gameObject.SetActive(false);
            _playing = false;
            onDone?.Invoke();
        }
    }
}
