# Android 真机发布与微信小游戏接入准备

## Goal

生成可安装到已通过 USB 调试连接手机的 Android 包，完成横屏手机的最低必要适配验证；为后续接入微信小游戏明确转换路径、依赖和阻断条件。

## Confirmed Facts

- Build Settings 已包含 `M1`、`M2`、`M3`、`M4`、`M5`；M4 完成后运行时加载 M5。
- 所有模块使用 1920x1080、CanvasScaler `Scale With Screen Size`、Match 0.5 的横屏 UGUI 基线。
- Android 配置为 ARM64、最低 API 25、最高屏幕比例 2.4；老板已确认改为仅左右横屏自动旋转，且需关闭 `androidRenderOutsideSafeArea`。Debug 包名固定为 `com.xinyuwu.railinspectiontraining`。
- 真机 21:9 截图确认：原 `CanvasScaler Match=0.5` 会将 1080 设计高度缩至约 972 逻辑像素，造成 M1 底部工具选项裁切；仅改为按高度匹配又会把画布逻辑宽度扩至约 2380，导致 M2-M5 视图按钮左偏，并使按 `RailViewport` 归一化坐标摆放的 M2-M4 尺子偏离 Unity 1920x1080 Play 模式定稿位置。
- Unity `6000.3.21f1`、Android Build Support 和内置 adb 已确认可用；USB 真机 `RMX5010` 已授权（Android 16，1264×2780，横屏两侧手势保护区约 105px、挖孔安全区 140px）。当前 Unity Editor 正打开项目，需释放项目锁后运行批处理构建。
- 工程没有微信小游戏 SDK、转换配置或微信开发者工具集成。Android APK 不能直接作为微信小游戏发布包。

## Requirements

1. 定位或补齐本机 Unity Android Build Support、Android SDK Platform Tools/adb，并识别 USB 调试手机。
2. 输出一个无需 keystore 的 Debug APK，安装到已连接手机并保留构建/安装日志。
3. 以低代码优先完成 Android 横屏适配：安全区不遮挡业务 UI；允许双向横屏；不得改变 M2/M3 冻结场景。宽屏额外空间不得改变 1920x1080 内容根、RailViewport、视图分段按钮或尺子的设计坐标与相对位置。
4. 在真机上验证 M1→M5 场景链路、长按问答入口、探头/尺子/擦拭布拖拽、M1 引导及常驻数字人视频。
5. 为微信小游戏记录转换目标、所需官方 SDK/开发者工具、兼容性风险和后续接入步骤；本任务不在缺少 SDK/AppID 的条件下伪造微信发布包。

## Acceptance Criteria

- [x] `adb devices -l` 显示目标手机为 `device` 状态。
- [x] Android 包成功构建，包含 M1-M5，并成功安装到目标手机。
- [ ] 真机横屏下 M1-M5 的边缘 UI 不被刘海、挖孔或手势区遮挡；左右横屏均可用。
- [ ] 21:9 真机上 M2-M5 的“普通视图/透视视图”分段控件与 Unity 1920x1080 Play 模式一致居中；M2-M4 校角与测量尺保持 Scene/Inspector 定稿相对位置，不因宽屏逻辑宽度改变。
- [ ] 真机完成 M1→M2→M3→M4→M5 主流程，M4 点击“下一模块”后可操作 M5。
- [ ] 真机验证数字人视频可播放、无黑框，问答面板、长按和拖拽可用。
- [x] 形成微信小游戏接入准备清单，明确缺失的 SDK/AppID/开发者工具和 Android/微信产物边界。

## Constraints

- M2/M3 Scene 冻结：不修改其场景 YAML 或视觉序列化字段。
- 手机适配优先使用 PlayerSettings 和已有 UGUI 容器；仅当配置无法满足安全区合同才新增通用 runtime 组件。
- 微信小游戏实际转换与发布依赖官方 SDK、微信 AppID、微信开发者工具和相应账号权限，缺失时只完成准备与可验证阻断说明。
- Android 包使用 Debug APK，不使用发布签名；后续需要分发或上架时再接入 keystore 和 Release APK/AAB 流程。
