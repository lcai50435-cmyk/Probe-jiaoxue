# Design: 微信小游戏 AI 安全边界

## Platform Boundary

`M1DeepSeekClient` 保留统一入口，但按平台选择后端：Android/Editor 使用现有 `DeepSeekConfig`；WebGL 只读取无密钥代理配置并向 CloudBase 发送用户问题。WebGL 编译路径不得引用或读取 `apiKey`。

## Build Boundary

扩展现有 WebGL 构建处理能力，在构建前将实际 `Assets/Resources/DeepSeekConfig.asset` 连同 `.meta` 安全移出 Resources，构建结束后恢复。处理器需记录原路径、检测残留临时文件，并在下一次构建前执行恢复检查。不得清空或覆盖用户本地资产内容。

若 Unity 构建回调难以覆盖进程崩溃，优先采用可追踪的无密钥模板资产作为 WebGL 输入，并让导出入口在 `finally` 中恢复实际资产。任何方案都必须用 BuildReport 和产物扫描证明，而不是只依赖预处理日志。

## Proxy Contract

请求体只包含模型允许的公开参数、system prompt 和用户消息；鉴权由 CloudBase 登录态或云函数上下文完成。DeepSeek Key 仅从 CloudBase 环境变量读取。响应转换为客户端现有的成功/失败回调合同。

代理配置缺失时，`IsConfigured` 在 WebGL 返回 false，QA 面板显示固定不可用提示并不发起请求。体验版“问答通过”依赖真实代理端点；没有 CloudBase 权限时本子任务可完成安全禁用，但最终验收子任务保持阻塞。

## Compatibility And Rollback

- 不修改 M2/M3 Scene；Bootstrap 继续复用同一客户端。
- Android 编译宏路径和本地资产行为保持不变。
- 回滚只移除 WebGL 代理分支和构建剥离处理；不得恢复客户端密钥直连。
