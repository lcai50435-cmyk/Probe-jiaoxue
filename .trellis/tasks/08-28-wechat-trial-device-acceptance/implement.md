# Implementation Plan: 微信体验版与跨平台验收

## Preconditions

- 三个依赖子任务检查通过并归档。
- 微信开发者工具、AppID、CloudBase权限、体验成员、Android设备和iPhone可用。
- 用户明确授权执行上传体验版；不包含提审或公开发布授权。

## Steps

1. 汇总依赖任务证据，保存Git状态、M2/M3 SHA256和版本号。
2. 运行Unity编译、微信导出；解析BuildReport、包大小、安全扫描和错误日志。
3. 构建/安装Android Debug APK，完成M1-M5及数字人/QA/拖拽回归。
4. 微信开发者工具分别在Android和iPhone执行冷/热启动与完整设备矩阵，保存截图和日志。
5. 先上传版本化CDN资源并校验，再上传匹配的小游戏开发版本。
6. 后台设置体验版、确认成员、生成二维码；至少两个受邀账号扫码验证。
7. 写验收报告：版本、尺寸、CDN、设备、结果、二维码时间、已知限制和回滚版本。
8. 核对未提审/未发布，更新父任务Acceptance Criteria。

## Validation Gates

- 主包<4MB、wasmcode存在、data<=40MB。
- 无Key/DeepSeek直连；QA按代理或明确禁用合同工作。
- Android、微信Android、微信iPhone均有真机证据。
- M2/M3 SHA256不变；日志无致命异常。
- 二维码仅受邀成员可访问。

## Stop Conditions

- 任一P0依赖失败。
- CDN数据与代码版本不一致。
- 无上传授权、AppID/CloudBase权限或iPhone设备。
- 发现Key、崩溃、严重缺字、数字人空白或data超限。
