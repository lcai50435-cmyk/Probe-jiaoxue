# Implementation Plan: 微信小游戏 AI 安全边界

## Steps

1. 保存 `DeepSeekConfig.asset` 内容哈希、M2/M3 SHA256 和当前 WebGL BuildReport 基线。
2. 增加无密钥 WebGL 代理配置与平台分支；WebGL 请求不得访问 `apiKey` 或设置 DeepSeek Bearer Header。
3. 为未配置代理实现安全禁用状态，保持 QA 面板可关闭、教学流程不暂停卡死。
4. 扩展 WebGL 构建/导出处理器，构建期排除实际 `DeepSeekConfig.asset`，用 `finally` 和下次启动恢复检查保护本地资产。
5. Unity 编译并重新导出微信产物；检查 BuildReport、字符串、域名和请求头。
6. 有 CloudBase 权限时部署/验证代理并轮换 Key；没有权限时记录外部阻断，最终体验版不得宣称问答可用。
7. 回归 Android/Editor 问答和 M2/M3 Scene 哈希。

## Validation

```text
rg -n "api.deepseek.com|Authorization|Bearer|<known-key-fragment>" Builds/WXExport/minigame Builds/WXExport/webgl
rg -n "DeepSeekConfig.asset" Temp/wx-export.log
```

- Unity WebGL 导出 `BuildResult.Succeeded`。
- WebGL 无代理时抓包为零请求；有代理时仅访问 CloudBase。
- Android 问答成功；M2/M3 SHA256 不变。

## Rollback Points

- 构建处理器任何异常先恢复实际配置资产再退出。
- 代理不可用时切到安全禁用，不回退到前端直连。
