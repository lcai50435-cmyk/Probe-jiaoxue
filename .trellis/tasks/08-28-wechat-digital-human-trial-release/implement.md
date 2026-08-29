# Implementation Plan: 微信小游戏数字人真机修复与体验版分包发布

## Parent Role

本任务现在是集成父任务，不直接承载后续修复代码。已有R1-R3实验实现保留在工作区，未通过验收的部分由子任务修正。禁止回退用户未提交改动或修改冻结M2/M3 Scene。

## Child Execution Order

1. `08-28-wechat-ai-security-boundary`（P0）
   - WebGL移除实际DeepSeek配置和客户端Bearer直连。
   - 有CloudBase代理则验证一轮问答；无代理则安全禁用并记录最终阻断。
2. `08-28-wechat-package-slimming`（P0）
   - 生成微信字体子集，排除全量字体/源TTF/TMP示例和发布调试资源。
   - 重新导出并达到 `webgl.data <= 40MB`；未达到则回父任务评审远程模块组。
3. `08-28-wechat-digital-human-polish`（P1）
   - 修复Shader tint、字幕布局、稀疏图集页和切态卸载。
   - 重新构建Android APK并验证平台后端隔离。
4. `08-28-wechat-trial-device-acceptance`（最终门禁）
   - 只在前三项检查通过后启动。
   - 完成微信Android/iPhone、Android APK、CDN版本、体验版二维码和受邀账号验收。

前三个子任务可以由不同Agent并行执行，但共享工作区时必须先读取最新Git状态并协调重叠文件：`WebGlVideoBuildProcessor.cs`、`WeChatMinigameExportRunner.cs`、`M3DigitalHumanBootstrap.cs`。同一文件不得并行覆盖。

## Parent Validation Gates

- 两个P0子任务完成且无例外；data不高于40MB，客户端无Key/DeepSeek直连。
- 数字人修复在微信Android、微信iPhone和Android APK均有真机证据。
- 主包<4MB、wasmcode存在、Unity/Shader/TMP日志无致命错误。
- M2/M3 SHA256始终与基线一致。
- 体验版仅受邀成员可访问；未提审、未公开发布。

## Finish Sequence

每个子任务执行 `trellis-implement -> trellis-check -> 必要时trellis-update-spec -> commit/archive`。最后由父任务汇总四个子任务证据，更新PRD勾选项并执行一次全范围 `trellis-check`，再决定是否归档父任务。
