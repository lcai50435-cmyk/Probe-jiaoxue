# 微信数字人视觉与内存修复

## Goal

修复微信引导人物颜色、字幕重叠和常驻数字人图集内存/切态问题，同时保持 Android/Editor 的 `VideoPlayer + RenderTexture + LumaKey` 路线。

## Confirmed Facts

- Shader 当前先将纹理乘 `IN.color`，WebGL 颜色转换后又乘一次 tint；默认白色掩盖了重复相乘。
- 当前 WebGL 字幕参数在现有 M1 几何下造成约 16px 人物/字幕重叠，且布局只在早期时机计算。
- 图集共 33/61/98 帧、6张 2048页；构建压缩约3.14MB。
- 播放器一次加载当前动作全部页；Editor 实测每页16MB，讲解态48MB。思考末页仅13帧、讲解末页仅2帧，仍占完整2048页。
- 最新 Android APK 早于本次 Shader、StreamingAssets 和图集实现。

## Requirements

1. LumaKey 对原始纹理完成键控/颜色空间转换后只乘一次 UI tint，Alpha、裁剪、绿线移除保持正确。
2. 字幕在16:9和宽屏中位于人物正下方并保留至少20px可见间距，不与人物、胶囊或手势区重叠。
3. 字幕布局必须在 Canvas/AspectRatioFitter 完成后应用，并在分辨率或方向变化时重算。
4. 图集生成器裁切稀疏末页或采用等价分页，播放器必须按每页真实尺寸正确计算 UV。
5. 状态切换不在交互关键帧同步执行昂贵的全局卸载；内存峰值和卡顿须在微信 Android/iPhone 实测。
6. 微信端保持三态和长按问答联动；Android/Editor 继续使用视频，不强制加载帧图集。
7. M2/M3 Scene 不修改；新增 runtime 脚本默认不超过150行。

## Acceptance Criteria

- [ ] 微信同关键帧与源视频/Android对比无整体泛白，非白 tint 和淡入只作用一次。
- [ ] 16:9及宽屏三段字幕均单行居中、人物间距>=20px且不换行/不裁切。
- [ ] 待机/思考/讲解三态连续切换，无空白、MissingReference或明显主线程卡顿。
- [ ] 稀疏末页尺寸按实际内容缩小；BuildReport和真机内存报告记录优化前后差异。
- [ ] 引导结束3秒内常驻数字人出现，M1-M5均可显示。
- [ ] 新构建 Android APK 的引导、常驻数字人和问答回归通过。
- [ ] M2/M3 SHA256不变，Unity与Shader编译无错误。

## Out Of Scope

- 改变数字人美术、台词或三态业务逻辑。
- 修改冻结 Scene 视觉。
