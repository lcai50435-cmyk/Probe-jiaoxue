# iPhone 退出重进引导数字人静止修复与验证

父任务：`09-03-trial-check-regression-fix`（R3 的独立交付子任务）。

## Goal

修复 iPhone 微信小游戏「退出再次进入后引导数字人不动」问题（Android 正常）。修复以微信开发者工具验证逻辑层；iPhone 真机扫码回归列为验收清单，由老板/体验成员延后执行。

## Background（父 prd 已查证，补充本任务视角）

- 平台 = iPhone 微信小游戏（体验版扫码）；Android 正常（2026-09-03 老板确认）。
- iPhone 微信端不走 VideoPlayer（WKVideo 合同 2026-08-30：真机只交付静帧 + M1 卸载叠加 RT 释放会闪退）。`M1IntroVideo` 对非 Android WebGL 走「半黑遮罩 + 海报 + intro 帧图集动画 + realtime 字幕」后端（`_webGlPosterOnly`，M1IntroVideo.cs Awake 内 `if (_webGlPosterOnly) { … StartPosterFallback(); return; }`）。
- 引导「动」= `TryPlayPosterFrameAnimation()` → `M1DigitalHumanFramePlayer.PlayState("intro")`（M1IntroVideo.cs TryPlayPosterFrameAnimation）。图集缺失/加载失败 → 回退静态海报 → 人物不动。
- 方向（老板选定 = 假设 b）：iPhone 首次正常、冷启动/重进后引导画面出现但人物静止 → 二次进入链路问题。
- 相关组件：
  - `M1IntroVideo`：seenPrefsKey=`M1_Intro_Seen`（:35）；Start 里 SetInt(1)+Save；`FinishIntro` 里 `posterFramePlayer.StopAndRelease()`（卸载页 + target.enabled=false）→ 二次 `PlayState("intro")` 需重新 Resources.Load。
  - `ExperienceReplayOnResume`：仅 M5 完成 + hide→show 才 `DeleteKey(M1_Intro_Seen)` + `SceneManager.LoadScene("M1")`（同进程重播 = B 路径）；冷启动（A 路径）不经过它，靠 PlayerPrefs 持久（非首次仍播引导）。
  - `M1DigitalHumanFramePlayer`：Resources.Load 图集页，StopAndRelease 释放当前页。

## Requirements

### 修复目标（拆两条子路径）

- **A) 冷启动重进**：杀进程后从聊天/扫码重新进入（全新进程，PlayerPrefs `M1_Intro_Seen`=1 持久 → 非首次但仍显示引导）：引导必须正常重播 intro 帧动画、人物动、字幕走、到时释放遮罩。
- **B) M5 完成后 hide→show**：`ExperienceReplayOnResume` 清标记 + LoadScene M1 同进程重播：同样必须正常动。
- 修复不得破坏 Android/Editor 既有 VideoPlayer 引导路径（`_urlPlayback`/分包/CDN 链路）。

### 实现期需回答的关键问题（根因确认）

1. 二次 `PlayState("intro")` 前，`FinishIntro→StopAndRelease` 释放页后，同页二次 Resources.Load 在 WebGL 是否可靠；是否需要 `forceReload` / 不卸载保留 / 重建 posterFramePlayer。
2. A 路径：全新进程是否真的会重进 `TryPlayPosterFrameAnimation`（`_firstTime=false` 分支是否跳过/海报路径差异）；`M1_Intro_Seen` 持久值对 `_firstTime`、canSkip、skipButton 的影响。
3. B 路径：同进程 LoadScene M1 后，场景内旧 M1IntroVideo 是否被正确销毁/重建；`posterFramePlayer`/RawImage/遮罩状态是否复位；与 `M1DigitalHumanPresenter` 常驻数字人（也走帧图集）是否互相释放页冲突。
4. `RestoreWhilePlaying` / `ResumeAfterIntro` 恢复常驻数字人后，intro 与 idle 状态页是否串页（StopAndRelease 只放一页，切态逻辑）。

### 验证策略（老板确认 = 选项 A）

- 主验证在**微信开发者工具**：工具内 `WechatPlatformKey` 非 "android" → `_webGlPosterOnly=true` → 与 iPhone 同走海报+帧图集后端；可覆盖「二次进入重播、PlayState 触发、Resume 状态复位」；用清缓存/重新编译模拟冷启动 A 路径；B 路径在工具内完成 M5 后 hide→show 模拟（或代码注入状态）。
- 每次改动后编译微信 WebGL 目标产物零 error；记录日志验证 `[M1IntroVideo]`/`[M1DigitalHumanFramePlayer]` 关键路径日志。
- 需要与 08-28-wechat-trial-device-acceptance 的现有体验版构建流程衔接（构建工具/导出脚本在 Assets/Editor/，如 WxVideoSubpackageInstaller）。

## Acceptance Criteria

- [ ] 根因确认：实现期在代码/日志层面确认 A 或 B（或两者）具体断点，修正 prd/design 中假设与实际不符处。
- [ ] A 路径逻辑层 PASS：开发者工具清缓存冷启动重进，intro 帧动画正常播放、人物动、字幕走完、遮罩按时释放。
- [ ] B 路径逻辑层 PASS：M5 完成后 hide→show 回 M1 重播，intro 帧动画正常。
- [ ] Android/Editor 引导回归：本地 VideoPlayer 路径（非 WebGL）与微信 Android 分包路径不受影响（代码零改动面外行为不变）。
- [ ] 微信 WebGL 构建零 C#/资源致命错误；关键路径日志齐全。
- [ ] 冻结 M1/M2/M3/M4/M5 Scene 零写回（若有 Scene 改动必须先在任务中说明并获批准）。
- [ ] 【延后 · 真机层，老板/体验成员扫码验收】iPhone 首次进入正常动；杀掉小游戏冷启动重进后引导人物恢复动画（本子任务完成归档不阻塞于此项，但要写进父任务遗留验收清单）。

## Out Of Scope

- iPhone 原生构建/真机自动化（无环境，验证走开发者工具 + 老板延后扫码）。
- 问题 1/2（提示修复）与问题 4（防再犯）——见兄弟子任务。
- 引导素材/台词/字幕内容调整。
