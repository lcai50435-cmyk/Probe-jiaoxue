# 实现证据：微信小游戏 AI 安全边界（2026-08-28）

## 基线（实现前采集）

- `Assets/Resources/DeepSeekConfig.asset` SHA256 `C0C8041C1A02255D22B8C74F37F78B4B42004DB5415AF7F7952AF871B0176DD3`，apiKey 非空（35 字符），baseUrl=api.deepseek.com。
- 该资产与无 Key 模板 `DeepSeekConfig.template.asset`（含端点字样）均进入旧 WebGL 构建（`Temp/wx-export.log:4002/4004`）。
- M2 SHA256 `896370BD…`、M3 SHA256 `2565232C…`（完整值见父任务基线）。

## 改动清单（全部为本次新增/修改，未回退既有未提交改动）

1. `Assets/Scripts/M1DeepSeekClient.cs`：`ChatAsync` 按平台分派——`UNITY_WEBGL && !UNITY_EDITOR` 走 `ChatViaProxyAsync`（仅 POST 用户 message 到 `AiProxyConfig.proxyUrl`，无 Authorization/Bearer，不触碰 `DeepSeekConfig`/`apiKey`）；其余平台走 `ChatDirectAsync`（原逻辑原样保留）。
2. `Assets/Scripts/AiProxyConfig.cs`（新增，38 行）+ `Assets/Resources/AiProxyConfig.asset`（可入库、无密钥，proxyUrl 暂空）：WebGL 专用无密钥代理配置；未配置时 `IsConfigured=false`。
3. `Assets/Scripts/M1QAPanel.cs`：未配置提示分平台——WebGL 显示"AI 问答暂未开放，敬请期待。"，不暴露本地资产路径；Android/Editor 提示不变。未配置路径不发网络请求、面板可关闭、教学不暂停（原有逻辑保持）。
4. `Assets/Scripts/DeepSeekConfig.cs`：C# 默认 baseUrl 清空（避免端点字面量进入 WebGL wasm）；既有资产序列化值不受影响。
5. `Assets/Editor/WebGlAiSecurityProcessor.cs`（新增）：WebGL 构建前把 `DeepSeekConfig.asset` 与 `DeepSeekConfig.template.asset`（含 .meta）备份到 `Library/AiSecurityBoundary/` 并移出 Resources；`OnPostprocessBuild` 立即恢复；崩溃/强退后由 `[InitializeOnLoad]` 与下一次任意平台构建前自愈恢复；恢复后清理备份。
6. `文档/CloudBase代理部署/`（新增文档）：`index.js` 云函数参考实现（Key 只读环境变量、限流、不落日志）+ `部署说明.md`（部署步骤、合法域名、Key 轮换与验证清单）。

## 验证结果

- Unity 批处理编译：exit 0，`error CS` 计数 0（`Temp/compile-ai-check.log`）。
- 微信导出（`M1.EditorTools.WeChatMinigameExportRunner.Run`）：exit 0（`Temp/wx-export-ai.log`）。
  - 日志 621 行：剥离 2 个配置资产；3785 行：构建完成后全部恢复。
  - 新 BuildReport 不再列出 `Assets/Resources/DeepSeekConfig.asset` 与模板（仅剩 `Assets/Scripts/DeepSeekConfig.cs` 脚本源引用行，属正常）。
- 导出目录二进制扫描（`Builds/WXExport/minigame` 全文件）：
  - Key 值：**未出现**；`api.deepseek.com`：**未出现**；`Bearer`：**未出现**。
  - `Authorization` 字样仅出现在 Unity 框架 `webgl.wasm.framework.unityweb.js`（通用 HTTP 头机制）与微信 SDK `authorize.js`（隐私授权回调），与 DeepSeek 无关。
- 构建后恢复校验：资产回到原路径，SHA256 与基线一致；`Library/AiSecurityBoundary/` 已清理。
- M2/M3 Scene SHA256 与基线逐字节一致。
- `DATA_FILE_SIZE=87,590,720`（体积属子任务 2 范围，本次仅防回归核对）。

## 行为矩阵

- WebGL 无代理（当前 `proxyUrl` 为空）：问答入口返回本地提示、零网络请求；教学流程、数字人、引导不受影响。
- WebGL 有代理（部署后填入 `proxyUrl`）：仅访问该 https 地址，请求体仅含 `{"message":…}`。
- Android/Editor：`IsConfigured`/直连行为与改前完全一致（同代码路径原样保留）。

## trellis-check 结论（2026-08-28，PASS 附修复）

- 检查代理总体 PASS：WebGL 分支无 Key/Bearer、零直连；Android/Editor 逐行等价；剥离/恢复三重保险实测有效；M2/M3 哈希一致；云函数文档无密钥。
- **已修复 1**：`Builds/WXExport/webgl/Build/webgl.data`（CDN 实际发布的数据文件）残留 `api.deepseek.com` 字样，来源为冻结 M2 场景 `M1DeepSeekClient` 旧序列化字段 `legacyBaseUrl`（无凭据伴随）。场景文件不可改，改为在 `WebGlVideoBuildProcessor.OnProcessScene` 构建期内存副本中调用既有 `ClearLegacyConfiguration()` 清空（与视频剥离同一模式，Scene 文件字节不变），重新导出后复核。
- **已修复 2**：`WebGlAiSecurityProcessor.Strip()` 改为先写恢复标记再删除原文件，消除"删除后、写标记前"崩溃导致的无法自愈窗口。
- **已修复 3**：验证日志复制到 `logs/`（Temp 目录会被 Unity 清理，wx-export-ai.log 含完整编译与剥离/恢复证据；compile-ai-check.log 已被 Temp 清理，编译无错结论由 wx-export 日志复核）。
- **记录不修复**（范围外）：① 代理失败合同 `{choices:[],error}` 的中文错误暂不透传给用户（客户端统一提示"AI 返回内容为空"），体验问题非安全问题；② 工作区根部存在误创建的 Windows 保留名文件 `nul`（既有，与本次无关）。
- **需老板决策/外部动作**：旧 Key 必须在 DeepSeek 平台作废轮换；CloudBase 代理部署待授权（见 `文档/CloudBase代理部署/部署说明.md`）。

## 外部阻断（如实记录，不伪造成功）

- CloudBase 权限/端点未取得：`deepseek-proxy` 未部署，微信端问答保持安全禁用。
- 旧 Key 已进入过客户端构建产物，**必须在 DeepSeek 平台作废轮换**；新 Key 只存云函数环境变量（见 `文档/CloudBase代理部署/部署说明.md`）。
- Android 真机问答回归：代码路径未变且编译通过，真机矩阵回归归入子任务 4 设备验收执行。
