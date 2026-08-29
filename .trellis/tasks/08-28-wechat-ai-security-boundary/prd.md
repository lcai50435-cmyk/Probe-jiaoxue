# 微信小游戏 AI 安全边界

## Goal

确保微信小游戏客户端不包含 DeepSeek 密钥、不直接调用 DeepSeek；代理未就绪时安全禁用问答，不阻塞其余教学流程。

## Confirmed Facts

- `Assets/Resources/DeepSeekConfig.asset` 被 Git 忽略，但 `Temp/wx-export.log:4002` 证明它进入 WebGL 构建。
- `M1DeepSeekClient.ChatAsync()` 当前从该资产读取 Key，并向 DeepSeek 设置 `Authorization: Bearer`。
- Android/Editor 仍需要现有本地配置路径；微信端必须平台隔离。
- 仓库尚无可验证的 CloudBase `deepseek-proxy` 实现、环境变量或已部署端点。

## Requirements

1. WebGL/微信构建不得包含真实 `DeepSeekConfig.asset`、API Key 或直连 DeepSeek 的可用凭据。
2. WebGL 客户端只允许调用无密钥的 CloudBase 代理地址；不得设置 DeepSeek Bearer Header。
3. 代理地址未配置时，问答入口应明确禁用或返回本地不可用提示，不发起网络请求。
4. Android/Editor 保留现有共享 `DeepSeekConfig` 行为，不修改 M2/M3 Scene。
5. 构建期临时剥离必须在成功、失败和异常退出后可恢复源资产，禁止丢失本地配置。
6. 已进入客户端风险范围的现有 Key 由服务端轮换；新 Key 只存 CloudBase 环境变量。

## Acceptance Criteria

- [ ] 最新 WebGL BuildReport 不再列出实际 `DeepSeekConfig.asset`，导出目录扫描不含 Key、`api.deepseek.com` 或客户端 Bearer 配置。
- [ ] WebGL 有代理配置时可完成一轮问答；网络请求只到已批准的 CloudBase 域名。
- [ ] WebGL 无代理配置时不发请求，教学流程和数字人待机仍可使用。
- [ ] Android/Editor 的现有问答回归通过。
- [ ] M2/M3 Scene SHA256 前后不变。
- [ ] 记录 Key 轮换和 CloudBase 环境变量配置结果；不把密钥写入日志。

## Out Of Scope

- 正式版提审和公开上线。
- 在 Unity 中实现通用密钥托管系统。
- 修改教学问答业务内容。
