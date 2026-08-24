# Design: Android 真机发布与微信小游戏接入准备

## Scope

本任务的直接交付是 Android Debug APK 的可重复构建路径与 Android 横屏安全区配置；微信小游戏仅输出接入准备文档。微信 SDK、AppID 和微信开发者工具缺失时不生成或宣称生成微信产物。

## Android Configuration Contract

`ProjectSettings/ProjectSettings.asset` 是移动端配置权威：

- `defaultScreenOrientation = AutoRotation`；仅 `allowedAutorotateToLandscapeLeft/Right = 1`，两种 Portrait 均为 `0`。
- `androidRenderOutsideSafeArea = 0`，由 Android 系统将应用内容约束到可用安全区。
- `MobileCanvasAdapt` 在运行时遍历 1920x1080 的 `CanvasScaler`，完整保留设计高度，并把每个 Canvas 下的 1920x1080 内容根约束为固定设计尺寸后居中。设备宽于 16:9 时，额外宽度只作为两侧留白/背景空间，不进入业务根、RailViewport 或尺子归一化坐标；它随屏幕旋转重新计算，不写入 M2/M3 或其他场景序列化字段。
- 保持 ARM64、minSdk 25、M1-M5 Build Settings 场景顺序，并使用 Debug 包名 `com.xinyuwu.railinspectiontraining`。

仅改 PlayerSettings 无法解决 21:9 下的 CanvasScaler 高度裁切，故使用通用 `MobileCanvasAdapt`。真机回归证明只覆盖缩放维度会扩大逻辑宽度，破坏 Scene 定稿坐标，因此适配器还需在内存中约束 1920x1080 内容根；不得逐模块修按钮或尺子，也不得写回场景。微信小游戏后续若其运行容器不能提供同等系统 inset，再扩展同一组件读取平台安全区。

## Build Contract

扩展既有 `Assets/Editor/BuildScenesSetup.cs`，确保 M1-M5 位于固定教学流程顺序，保留其对无效场景的清理、对其他有效场景的兼容和幂等性质。Android 构建器显式只传入 M1-M5，因此 APK 内容不受额外 Build Settings 场景影响。

新增精简 Editor 构建入口：

- 目标：`Builds/Android/ProbeTeaching-debug.apk`。
- 使用 `BuildTarget.Android`、`BuildOptions.Development | BuildOptions.AllowDebugging`。
- 在构建前调用构建场景整理，构建后检查 `BuildReport.summary.result == Succeeded` 和输出文件存在。
- 不修改签名设置，不执行 adb 安装；缺 Unity Android Build Support、SDK 或 adb 时给出明确阻断日志。

实际安装只在 `adb devices -l` 显示单一 `device` 状态后执行 `adb install -r`。多设备、`unauthorized` 或离线设备均视为阻断，不猜测目标设备。

## Validation Boundaries

1. 静态：Build Settings 含 M1-M5，移动 PlayerSettings 与场景顺序符合合同，M2/M3 文件未变；21:9 模拟下业务内容根仍为 1920x1080，视图按钮和尺子使用的 RailViewport 尺寸不漂移。
2. Unity：批处理执行 Android Build 入口，产出 Debug APK。
3. 设备：adb 安装、启动、检查横屏两方向和安全区；完成 M1→M5、问答、拖拽、视频链路。
4. 微信：只审查转换先决条件。目标产物必须来自官方 Unity 微信小游戏转换 SDK，不接受 APK 或裸 WebGL 作为替代。

## Rollback

移动适配为 PlayerSettings 与 Editor 工具变更，回滚相应字段和构建脚本即可；不涉及冻结场景、资源重编码、签名证书或设备系统配置。
