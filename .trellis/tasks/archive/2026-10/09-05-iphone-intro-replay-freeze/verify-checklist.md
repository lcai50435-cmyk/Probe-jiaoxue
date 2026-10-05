# 验证清单 — iPhone 重进引导数字人静止修复

修复提交：`M1DigitalHumanFramePlayer.cs`（intro 页进程级钉住 + `LastAdvanceRealtime`）、`M1IntroVideo.cs`（海报卡帧放行 watchdog + 失败埋点）、`ExperienceReplayOnResume.cs`（B 路径触发日志）。

## 0. 修复原理速记

- **B 路径根因假设**：M5 完成→hide→show 同进程重载 M1 时，上次引导已 `UnloadAsset` intro 图集页，二次 `Resources.Load` 在微信 WebGL 不可靠 → 钉住后 intro 页永不卸载，直接命中缓存。
- **A 路径兜底**：冷启动重进若仍卡死（平台层），watchdog 在 6 秒无帧推进后自动放行进游戏，不再干等 15.3 秒。
- **Android 隔离**：`intro` 状态仅海报后端播放（Android 不可达）；watchdog 以 `!_webGlPosterOnly` 排除 Android 降级海报路径；Android 视频/常驻数字人路径行为零变化。

## 1. Editor 侧（2026-09-16 已执行）

- [x] Unity batchmode 编译零 error（6000.3.21f1，`Temp/compile-check.log` return code 0）
- [x] `M5.EditorTools.M5RuntimeSmoke.RunBatch` **全部验收通过**（覆盖 M5 完成→MarkCompleted 链路与 QA 暂停/恢复合同）
- [x] M2/M3/M4.unity SHA-256 与改动前基线一致（全程零场景写回）：
  - M2 `36d66b2d…` M3 `77a44cc5…` M4 `e0359ea4…`
- [x] **M2/M3/M4 冒烟失败归属已对照实验排除本修复**：撤掉本修复三个文件后 M3 冒烟以完全相同断言失败（`123mm 最高波状态错误`）。三项失败均为工作区既有问题，属 in-progress 的 `09-05-module-action-hints` 未提交改动域：
  - M2：`正式尺子不是 RulerHome 内最高渲染层`
  - M3：`123mm 最高波状态错误`
  - M4：`波形参数未按 PPT 设置`
  - （三项断言在 HEAD 均已存在；修复提交不被它们阻塞，但 hints 任务收口前 M2/M3/M4 冒烟不可作回归闸门）
- [ ] ~~`M2/M3/M4RuntimeSmoke` PASS~~ → 见上，既有失败不归属本修复

## 2. 微信开发者工具（逻辑层，与 iPhone 同海报后端）

工具内 `platform='devtools'` → 注入 JS 写 `__wxIntroVideoPlatform='unknown'` → `_webGlPosterOnly=true`，即 iPhone 同款后端。

### 2.0 步骤〇（每次导出必做）：上传 Unity 数据文件到 CDN

包内不含 Unity 数据（`loadDataPackageFromSubpackage=false`），启动时按 `DATA_CDN + <md5>.webgl.data.unityweb.bin.txt` 下载；**文件名带构建哈希，每次导出都会变，必须先上传**：

- 本地文件：`Builds/WXExport/webgl/d1166cbb0aa065f3.webgl.data.unityweb.bin.txt`（本次构建）
- 上传位置：腾讯云开发控制台 → 静态网站托管 → 环境 `cloud1-d6gmycfs6b37edb43-1476749432` → `rail-inspection/` 目录根下，文件名保持不变
- 不要删除线上旧文件 `e8a68f578f45f170...`（当前线上版本，回滚目标）
- 传完验证：`curl -sI https://cloud1-d6gmycfs6b37edb43-1476749432.tcloudbaseapp.com/rail-inspection/d1166cbb0aa065f3.webgl.data.unityweb.bin.txt` 返回 200

（2026-09-16 复盘：devtools「普通编译报资源下载失败」即漏了此步；旧构建能跑是因为其数据文件已在 CDN。）

### 2.1 A 路径：冷启动重进（做两遍）

1. 清缓存（工具「清缓存→清除全部缓存」）→ 重新编译并启动。
2. 首次进入：引导遮罩出现，人物动画播放、三段字幕依次走完、约 15.3 秒遮罩释放进入 M1。
3. 再次清缓存 + 重新编译（模拟二次冷启动；此时 `M1_Intro_Seen=1` 已持久，右上角出现跳过按钮属正常）：引导同样重播、人物同样动。

**预期日志（Console）**：
- `[M1IntroVideo] 启用海报+字幕兜底引导（15.3 秒）`
- `[M1DigitalHumanFramePlayer] 切态 intro：第 1/4 页 @ 10fps（152 帧）`
- **不得出现**：`图集页加载失败` / `缺少状态图集` / `intro 帧图集启动失败` / `卡帧放行`

### 2.2 B 路径：玩完重进（同进程重载）

1. 从 M1 完整玩到 M5，完成擦拭（100%）。
2. 点工具的「后台/切后台」按钮模拟切出，再切回前台。
3. 应自动回到 M1 并重播引导：人物动、字幕走完、遮罩释放。

**预期日志**：
- `[ExperienceReplayOnResume] M5 完成后返回，清除首次标记并重载 M1 重播引导。`
- 随后出现与 2.1 相同的两条 intro 日志，且无加载失败/卡帧告警。

### 2.3 常驻数字人回归（验证非 intro 状态不受钉住影响）

1. 引导结束后：全身待机动画正常循环。
2. 长按数字人开问答面板 → 提问一次：思考态→讲解态→回答后回待机，三态切换动画均正常。
3. 期间 Console 不得出现 `图集页加载失败`。

### 2.4 若仍复现人物静止

保存 Console 全量日志（含 Warning/Error）连同复现步骤（A 或 B 路径）反馈：
- 见 `intro 帧图集启动失败` + `缺少状态图集` → 图集资产未进包（构建问题，找 AI）。
- 见 `切态 intro` 但人物不动、随后 `卡帧放行` → 帧推进层问题（找 AI，附日志）。
- 什么日志都没有、整屏冻结 → 平台层（iOS WebGL 上下文恢复），转真机日志定位。

## 3. Android 真机回归（老板/体验成员扫码体验版）

- [ ] 首次进入：引导 MP4 正常播放（分包或 CDN），字幕正常，播完进 M1。
- [ ] 常驻数字人三态动画正常（待机/思考/讲解）。
- [ ] M1→M2→M3→M4→M5 全流程可通。
- [ ] 退出小游戏再进入一次：回到原模块、无异常（M5 已完成时重开 M1 属预期）。

## 4. iPhone 真机验收（老板扫码，PRD 遗留验收项）

- [ ] 首次进入：引导人物动画正常。
- [ ] 杀掉小游戏（上滑关闭）后冷启动重进：引导人物恢复动画（A 路径）。
- [ ] 玩完全部模块退出后再进：回 M1 且引导人物动画正常（B 路径）。
- [ ] （如仍卡死）vConsole / 反馈日志导出交 AI 定位。

## 5. 回滚

单提交纯 runtime 三文件，`git revert <commit>` 即净回滚；无场景/资产变更。
