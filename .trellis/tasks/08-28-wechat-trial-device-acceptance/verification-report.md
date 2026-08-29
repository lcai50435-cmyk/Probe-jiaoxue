# 体验版与跨平台验收报告（2026-08-28）

## 一、本地构建与门槛终验（已完成，wx-export-final.log）

| 门槛 | 结果 |
|---|---|
| `DATA_FILE_SIZE` | **33,317,448 ≤ 41,943,040（40MB）** ✓ |
| 主包（按 project.config.json ignore 规则） | **0.94MB < 4MB** ✓ |
| `wasmcode` 分包 | 存在（8MB，game.json 已注册）✓ |
| 客户端密钥/直连扫描（Key、api.deepseek.com、Bearer） | **全导出目录 CLEAN** ✓ |
| 构建日志（error CS / Shader error / TMP m_AtlasTextures） | **全部为 0** ✓ |
| M2/M3 Scene SHA256 | `896370BD…` / `2565232C…` 与基线一致 ✓ |
| Android Debug APK | `Builds/Android/ProbeTeaching-debug.apk`，196,065,369 bytes，2026-08-28 21:29（含全部修复）✓ |

当前产物 = 最新源码（AI 安全边界 + 包体瘦身 + 数字人修复 + U+200B 字符补充 + 字幕分辨率切换修复）。

## 二、真机验收清单（需真机，当前环境不可用——如实报告，不以开发者工具替代）

本环境无 adb、无连接的 Android/iPhone 设备，无法执行：

1. Android APK 安装回归：M1-M5、引导视频、常驻数字人、长按问答、拖拽。
2. 微信 Android 真机：冷启动 / 二次缓存启动、左右横屏、M1-M5 全流程、引导颜色与字幕间距、三态切换、M2-M5 数字人。
3. 微信 iPhone 真机：同上矩阵 + 内存/卡顿观测（切态日志已内置：`[M1DigitalHumanFramePlayer] 切态 …`）。

**需要老板安排**：至少一台 Android + 一台 iPhone（已加入体验成员的微信号），安装 APK / 扫体验版二维码执行上述清单并截图留证。

## 三、体验版发布操作（外部操作——需老板明确授权后才执行）

按 PRD 约定，以下操作执行前需要本次明确授权：

| 项目 | 建议值（可改） |
|---|---|
| 目标 AppID | `wxcc66e002c508ed83`（当前 project.config.json） |
| 版本号 | `20260828-1`（建议格式：日期-序号） |
| 上传说明 | "数字人修复+包体瘦身+AI安全边界；问答暂禁用" |
| CDN 数据路径 | `…/rail-inspection/20260828-1/`（**版本化新目录，不覆盖旧目录**） |
| 回滚版本 | 上传前记录后台当前体验版版本号；回滚 = 后台重新指定上一版本为体验版 + CDN 指回旧目录 |
| 预计影响 | 仅受邀体验成员；data 体积 87.6MB→33.3MB，首启下载显著加快；问答入口显示"AI 问答暂未开放" |

授权后执行顺序（防新旧不一致）：① 上传版本化 CDN 数据并校验 200 → ② 微信开发者工具（本机已有：`E:\LSoft\WeChatDev\微信web开发者工具\cli.bat`）上传开发版本 → ③ 后台"版本管理"设为体验版 → ④ 成员管理确认体验成员有效 → ⑤ 生成二维码交付老板。

**明确不做**：正式提审、备案、公开发布、覆盖旧 CDN 目录。

## 四、已知限制（如实记录）

1. **问答暂禁用**：CloudBase `deepseek-proxy` 未部署（权限未取得，见 `文档/CloudBase代理部署/部署说明.md`）。体验版问答入口返回"AI 问答暂未开放，敬请期待。"，零网络请求——符合"代理未就绪时安全禁用"合同；问答不可列为已验收。
2. **Key 轮换未执行**：旧 DeepSeek Key 已进过历史构建产物，必须在 DeepSeek 平台作废重建，新 Key 只存云函数环境变量。
3. **真机项未验**：颜色对比、字幕间距、三态、内存等代码合同已通过静态检查与构建验证，真机证据待设备到位后补充。
4. 微信开发者工具模拟器可作为快速烟测（非替代真机）：上传前可用其打开 `Builds/WXExport/minigame` 验证启动与 M1 流程。

## 五、2026-08-29 开发者工具二轮修复

- 真机截图确认第二段字幕发生自动换行；WebGL 字幕已改为宽 1700、高 52、最大字号 30/最小 24 自动缩小、`NoWrap` 强制单行，Android 运行时兜底恢复 y=16。
- 微信 SDK 启动时默认关闭 `WebGLInput.mobileKeyboardSupport`，且旧触摸兜底只识别 `UnityEngine.UI.Text`；`M1QAPanel` 现于打开输入面板时显式开启、关闭/销毁时恢复，M1-M5 共用修复。
- 最新导出日志：`logs/wx-export-input-fix.log`，`Build Finished, Result: Success` / `SUCCEED`，无 C#、Shader、TMP 致命错误。
- 首轮单行改造导出的 `abc626...` 存在字幕布局协程晚于首帧回调、再次隐藏字幕的竞态，已废弃不得上传。
- 最终修复：`ApplyWebGlSubtitleLayout()` 按 `_firstFrameShown` 恢复可见性，首帧前隐藏、首帧后所有布局重算保持显示；最新 data 为 `e8a68f578f45f170.webgl.data.unityweb.bin.txt`，33,317,641 bytes。旧 `955dd...`/`abc626...` 均不包含完整修复。
- M2/M3 Scene SHA256 继续与基线一致。
- 当前 `AiProxyConfig.proxyUrl` 仍为空：本轮只修复输入能力，AI 返回链路仍需接入实际 CloudBase HTTP 代理或另行实现 `wx.cloud.callFunction` 桥接。

## 六、版本归档信息

- 三个依赖子任务检查均 PASS（证据见各自 evidence.md + logs/）。
- 微信代码包：`Builds/WXExport/minigame`（导出日志 wx-export-final.log）。
- CDN 当前线上数据文件：`b70a84f15e9a32e3.webgl.data.unityweb.bin.txt`（旧版本，回滚目标）。
