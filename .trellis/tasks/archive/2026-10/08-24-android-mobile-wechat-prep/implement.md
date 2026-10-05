# Implementation Plan: Android 真机发布与微信小游戏接入准备

## Preconditions

- 首包为 Debug APK，无需 keystore。
- 目标 Android 设备需在 `adb devices -l` 中显示为单一 `device`。
- 构建电脑需提供 Unity `6000.3.21f1` 对应 Editor、Android Build Support、Android SDK/NDK/OpenJDK 和 Android Platform Tools。

## Steps

1. 定位 Unity Editor、Android SDK 和 adb；记录路径及 USB 设备状态。若缺失，不安装全局工具或修改系统环境变量，明确列为外部阻断。
2. 修改 PlayerSettings：启用左右横屏自动旋转、禁用竖屏、关闭 Android 渲染到安全区外；修正 `MobileCanvasAdapt`，在保留设计高度的同时固定并居中 1920x1080 业务内容根，避免宽屏扩展 RailViewport。
3. 扩展 `BuildScenesSetup`，幂等确保 M1-M5 教学场景顺序；Android 构建器显式只传入 M1-M5。
4. 新增 Editor Android Debug APK 构建入口，输出到 `Builds/Android/ProbeTeaching-debug.apk`；构建前后校验场景清单、BuildReport 和输出文件。
5. 在 Unity 批处理/Editor 中执行最小编译和构建；验证 Build Settings、21:9 内容根/按钮/尺子布局合同与 M2/M3 场景哈希。
6. 通过 adb 安装 Debug APK，启动应用并收集 `logcat` 中的 Unity 异常。
7. 真机横屏左右方向验收 M1-M5、视频、问答和拖拽；阻断项记录到任务结果。
8. 写入微信小游戏接入准备说明：官方 Unity 微信小游戏 SDK、微信 AppID、微信开发者工具、转换产物与 Android APK 的边界、VideoPlayer/网络请求风险。

## Validation Commands

```powershell
adb devices -l
adb install -r Builds/Android/ProbeTeaching-debug.apk
adb shell monkey -p com.xinyuwu.railinspectiontraining 1
adb logcat -d -s Unity AndroidRuntime
```

```text
Unity -batchmode -quit -projectPath <project> -executeMethod M1.EditorTools.AndroidDebugBuild.BuildBatch -logFile Logs/android-build.log
```

## Review Gates

- 每次构建前 `git diff --check`。
- Android 构建器必须只传入 M1-M5，且 M2/M3 哈希不得变化。
- 不接受逐模块硬编码按钮或尺子补偿；21:9 适配必须保持 1920x1080 Play 模式业务坐标，额外宽度不得参与 RailViewport 归一化换算。
- 未发现 Unity/SDK/adb 时停止在工具链阻断，不伪造 APK 或安装结果。
- 微信 SDK/AppID/开发者工具缺失时只交付准备文档，不写兼容性猜测代码。

## Rollback Points

- PlayerSettings：恢复原横屏和安全区字段。
- Editor build tool：删除新增文件并还原 `BuildScenesSetup`；不影响场景。
- `Builds/` 是生成目录，不纳入版本控制。
