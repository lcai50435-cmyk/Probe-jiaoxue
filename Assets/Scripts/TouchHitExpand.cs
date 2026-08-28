using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 触摸热区扩展（手机抓取辅助）：给拖拽工具节点幂等叠加一个全透明可射线子层（~HitZone），
/// 扩大 UGUI 射线命中范围。背景：工具架内尺子 Home 态经 localScale 收缩后实际热区仅约 90×19 参考像素，
/// 远低于手指触控精度（指腹 8~10mm / 安卓最小目标 48dp），手机上"拖不出来"。视觉零改动；
/// 命中后事件冒泡到宿主现有 IBeginDragHandler 等接口，拖拽逻辑不变。
/// M2/M3 Scene 冻结不可加节点，故由各模块 Bind 时运行时动态绑定。
/// </summary>
public static class TouchHitExpand
{
    private const string ZoneName = "~HitZone";

    /// <summary>padding 为四周外扩量（参考分辨率像素）。横向小值防止跨到相邻槽位误抓，纵向大值补偿指尖遮挡偏移。</summary>
    public static void Ensure(RectTransform owner, Vector2 padding)
    {
        if (owner == null) return;
        var zone = owner.Find(ZoneName) as RectTransform; // 缺失时为 Unity 伪 null，必须 == null 分步判空（禁止 ??）
        if (zone == null)
        {
            var go = new GameObject(ZoneName, typeof(RectTransform), typeof(Image));
            zone = go.GetComponent<RectTransform>();
            zone.SetParent(owner, false);
            zone.anchorMin = Vector2.zero; // stretch 宿主 rect，用 offset 向四周外扩
            zone.anchorMax = Vector2.one;
            zone.pivot = new Vector2(.5f, .5f);
            go.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f); // 全透明，仅承接射线（Image 默认按 rect 全域命中）
        }
        else
        {
            var image = zone.GetComponent<Image>();
            if (image == null) image = zone.gameObject.AddComponent<Image>(); // 分步判空：Unity 6 伪 null 不能走 ??
            image.color = new Color(0f, 0f, 0f, 0f);
        }
        zone.offsetMin = -padding;
        zone.offsetMax = padding;
        zone.localScale = Vector3.one;
        zone.SetAsLastSibling(); // 同矩形内层级最深、渲染最上 → 射线优先命中热区而非 bg
    }
}
