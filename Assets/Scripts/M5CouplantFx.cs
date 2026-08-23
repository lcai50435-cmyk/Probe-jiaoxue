using UnityEngine;
using UnityEngine.UI;

namespace M5
{
    /// <summary>
    /// M5 擦拭耦合剂：钢轨顶面蓝色耦合剂薄膜（初始铺满，擦拭时从左至右递减消失）。
    /// 复刻 M2CouplantFx 的 Sprite 切割思路（从普通视图铁轨图切 coverRect 子矩形，保留铁轨形状与羽化）；
    /// 状态相反——M2 是涂抹动画（0→1 铺满后淡出），M5 是初始铺满 + 随擦拭进度 fillAmount 递减。
    /// </summary>
    public class M5CouplantFx : MonoBehaviour
    {
        public RectTransform railBg, maskRt;   // 对齐基准（RailBackground）与薄膜容器（CouplantMask）
        public Image film;                     // CouplantMask 的 Image
        public CanvasGroup group;              // CouplantOverlay 的 CanvasGroup
        public Color filmColor = new Color(.55f, .8f, .96f, .45f); // 浅蓝色半透明（与 M2 同款，老板 2026-08-15 调浅）
        public Vector4 coverRect = new Vector4(.005f, .222f, .993f, .553f); // 涂抹区域（相对 railBg 底左归一化 x,y,w,h）＝铁轨主体实心块，覆盖轨顶中央大部分（老板 2026-08-18 确认口径）
        private bool _ready;
        private Sprite _filmSprite;

        /// <summary>初始铺满：切铁轨形状 Sprite + 右对齐剩余 + fillAmount=1（覆盖轨顶中央大部分）。</summary>
        public void Init()
        {
            if (_ready) return;
            if (railBg != null && maskRt != null)   // 涂抹区域 = 铁轨 rect 上的 coverRect 子矩形（底左归一化）
            {
                maskRt.pivot = railBg.pivot;
                maskRt.sizeDelta = new Vector2(coverRect.z * railBg.sizeDelta.x, coverRect.w * railBg.sizeDelta.y);
                maskRt.anchoredPosition = railBg.anchoredPosition + new Vector2(
                    (coverRect.x + coverRect.z * .5f - .5f) * railBg.sizeDelta.x,
                    (coverRect.y + coverRect.w * .5f - .5f) * railBg.sizeDelta.y);
            }
            if (film == null) { Debug.LogError("[M5CouplantFx] 薄膜 Image 缺失，无法显示耦合剂。"); return; }
            if (_filmSprite == null)
            {
                var tex = Resources.Load<Texture2D>("俯视角");
                if (tex != null)
                {
                    var px = coverRect.x * tex.width; var py = coverRect.y * tex.height;
                    var pw = Mathf.Clamp(coverRect.z * tex.width, 1f, tex.width);
                    var ph = Mathf.Clamp(coverRect.w * tex.height, 1f, tex.height);
                    _filmSprite = Sprite.Create(tex, new Rect(px, py, pw, ph), new Vector2(.5f, .5f));
                }
                else Debug.LogError("[M5CouplantFx] 俯视角 纹理加载失败。");
            }
            if (_filmSprite != null) film.sprite = _filmSprite;
            film.type = Image.Type.Filled;
            film.fillMethod = Image.FillMethod.Horizontal;
            film.fillOrigin = 1;                 // 右对齐剩余：已擦左侧消失、剩余显示在右侧，与拖动方向一致
            film.fillAmount = 1f;                // 初始铺满（轨顶中央大部分）
            film.color = filmColor;
            if (group != null) group.alpha = 1f;
            if (maskRt != null) maskRt.gameObject.SetActive(true);
            _ready = true;
        }

        /// <summary>擦拭进度 p(0~1)：已擦左侧比例消失，剩余 fillAmount = 1-p 右对齐显示。</summary>
        public void SetWipeProgress(float p)
        {
            if (!_ready) Init();
            if (film != null) film.fillAmount = Mathf.Clamp01(1f - p);
        }

        public void Reset()
        {
            _ready = false;
            if (film != null) film.fillAmount = 1f;
            if (group != null) group.alpha = 1f;
            if (maskRt != null) maskRt.gameObject.SetActive(true);
        }
    }
}
