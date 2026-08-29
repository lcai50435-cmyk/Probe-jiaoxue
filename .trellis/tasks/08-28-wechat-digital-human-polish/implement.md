# Implementation Plan: 微信数字人视觉与内存修复

## Steps

1. 保存M2/M3 SHA256、Shader截图、字幕几何、图集页尺寸/内存和Android APK基线。
2. 重排 `UI-LumaKey.shader` 采样、颜色转换和tint顺序；验证Alpha、绿线与UI裁剪。
3. 把WebGL字幕布局改为布局完成后重算，建立人物间距和安全区约束。
4. 修改 `DigitalHumanAtlasTool`，按每页有效行列生成裁切纹理；清理临时Texture对象并保持幂等。
5. 调整 `M1DigitalHumanFramePlayer` 切态和卸载策略，补缺页兜底、内存/切态日志；保持<=150行或拆出通用配置职责。
6. Unity编译、重新生成图集、导出微信并记录BuildReport。
7. 重新构建Android APK；微信Android/iPhone分别验证颜色、字幕、三态、长按和M1-M5显示。
8. 核对M2/M3 SHA256和工作区用户改动未被覆盖。

## Validation

- Shader编译无错误，同帧颜色截图通过。
- 字幕间距>=20px，16:9/宽屏均无重叠或裁切。
- speaking稀疏末页不再是2048²；UV和帧序正确。
- 待机/思考/讲解快速往返无空白、异常或明显卡顿。
- Android新APK使用VideoPlayer路径；微信使用图集路径。
- M2/M3 SHA256不变。

## Rollback Points

- Shader异常可关闭WebGL输入开关，但不得恢复发白输出作为交付。
- 裁切图集异常先保留旧配置与页，不覆盖唯一可运行资产。
- 真机内存仍高时优先降低末页尺寸/分页驻留，不降低核心可读性。
