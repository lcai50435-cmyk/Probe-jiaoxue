# Design: 微信数字人视觉与内存修复

## Shader Pipeline

采样纹理和 `_TextureSampleAdd` 后先得到未 tint 的输入颜色。根据 `_VideoInputIsSRGB` 计算键控用 sRGB 和显示用 Linear RGB，完成亮度键控及绿线判断；最后统一乘一次 `IN.color`，再执行 UI clip/alpha clip。白色、半透明和非白 tint 都必须覆盖测试。

## Subtitle Geometry

字幕位置以人物实际 `RectTransform.GetWorldCorners()` 转换到字幕父级坐标为准。布局在至少一个 `Canvas.ForceUpdateCanvases`/布局帧后应用，并在尺寸变化时幂等重算。若20px间距与底部安全区冲突，优先缩小微信人物到可配置下限，再调整字幕框；禁止把字幕向上压回人物。

## Atlas Paging

生成器保持固定帧单元与 gutter，但每页纹理尺寸按该页实际最大列/行向上取可编码尺寸，不再固定2048²。配置记录每页资源路径，播放器继续依据实际 `tex.width/height` 计算 UV，因此完整页与裁切页兼容。

播放器只保留当前动作页。状态切换先解绑旧纹理，再加载新状态；卸载采用受控延迟/合并策略，避免每次问答状态切换同步触发全局扫描。页面加载失败保留可见兜底并输出一次明确错误。

## Platform Compatibility

编译宏/后端选择保持：微信 WebGL使用帧图集，Android/Editor使用URL或本地VideoPlayer。M3-M5继续由Bootstrap内存装配，不保存M2/M3。

## Rollback

Shader开关、微信布局参数和图集配置可独立回滚；Android视频素材和路径不删除。旧图集页在新配置验证成功后才清理。
