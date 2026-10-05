# 实现证据：微信小游戏包体瘦身（2026-08-28）

## 基线 → 结果

| 指标 | 基线 | 结果 |
|---|---|---|
| `game.js` DATA_FILE_SIZE | 87,590,834 | **33,316,344（31.8MB ≤ 40MB 门槛）** |
| 主包（按 project.config.json ignore 规则） | ~0.98MB | **0.94MB < 4MB** |
| wasmcode 分包 | 10.17MB | 8MB（去除 `--profiling-funcs` 后缩小），继续存在 |
| BuildReport 全量中文字体 48.5MB | 存在 | **移除** |
| 源 TTF 21.5MB | 存在 | **移除** |
| TMP Examples & Extras Resources（~11MB） | 存在 | **移除** |

## 改动清单

1. **微信 TMP 子集字体（新增 `Assets/Editor/WeChatFontSubsetTool.cs`，菜单 Tools/WeChat）**：
   - 确定性字符集 = GB2312 一级常用字 3755 + 项目场景/预制体/Resources 资产/运行时脚本扫描（3892 字，含 ASCII 与标点），清单落盘 `Assets/font/WeChatSubset/charset.txt` 可审计。trellis-check 审计补充：M1/M2 场景存在 YAML `\uXXXX` 转义的 U+200B 零宽空格占位符（字面扫描抓不到），已在 Punctuation 中显式补入并重生成。
   - 生成 `SarasaWeChatSubset.asset`：**静态模式**（不携带源 TTF，21.5MB 出包）、4 页 2048² 图集、ETC2_RGBA8Crunched（WebGL 平台覆盖，每页压缩后约 550-586KB）。
   - AI 动态文本缺字策略：GB2312 一级覆盖绝大多数回复用字；真机若现方框，扩充字符集重跑工具即可（工具幂等）。
2. **构建期字体替换（`WebGlVideoBuildProcessor` 扩展）**：WebGL 构建内存副本中替换全部 `TMP_Text`、`TMP_InputField`（m_GlobalFontAsset）、`M1QAPanel.cnFont` 字体引用——三类引用齐全后旧字体资产+TTF 才彻底出包（前两轮导出 88.9MB/64.2MB 的归因教训）。Scene 文件零改动。
3. **构建期目录剥离（`WebGlAiSecurityProcessor` 扩展）**：WebGL 构建前整体移出 `TextMesh Pro/Examples & Extras/Resources/Fonts & Materials`，构建后恢复；标记+备份同 AI 资产机制。
4. **WebGL 纹理平台覆盖（新增 `WebGlTextureOverrideTool.cs`，20 条规则）**：工具卡/头像/探头角度图/主体图（probeFootage×2、俯视角×2、正视角×2、rag、probe0、大头、折叠头像×2、InspectionToolMaterials 全部 6 张）设置 WebGL 平台 maxTextureSize=1024 + ETC2_RGBA8Crunched q50。Android/Editor 平台设置零改动。注：NPOT 纹理无法 ETC2（ES3 要求 POT），降 1024 后仍显著缩小。
5. **发布配置**：`profilingFuncs 1→0`（MiniGameConfig）、导出 Runner 清理历史累积的 `--profiling-funcs` 参数（已生效，wasmcode 10.17→8MB）。**debug symbols 口径修正（trellis-check 复核发现）**：WX SDK 在 PackageCache 内每次导出无条件强制 `debugSymbolMode=External`（Unity 侧开关不可用），`webGLDebugSymbols` 的 YAML 修改会被导出覆写；产物 `*.symbols*`（约 13.5MB）由 `project.config.json` 的 packOptions.ignore 排除在微信上传包外，主包/40MB 门槛不受影响，本地保留用于崩溃符号化（开发排错用途保留）。
6. **TMP 构建异常修复**：`sarasa-gothic-sc-light SDF` 与 `sarasa-gothic-sc-regular SDF` 两个**零引用**资产存在历史动态清空残留（atlasTextures 全为 fileID:0），导致 TMP_PreBuildProcessor 抛 UnassignedReferenceException。修复其 `m_ClearDynamicDataOnBuild 1→0`（YAML 单行，可逆），导出日志 TMP 异常归零。尝试的 `ClearFontAssetData` 修复路径因批处理不落盘（依赖 Inspector 订阅）未生效，已在 `fontrepair2.log` 记录。

## 验证结果（wx-export-slim5.log，批处理 exit 0）

- `DATA_FILE_SIZE=33,316,344`；主包 0.94MB；wasmcode 8MB 存在。
- BuildReport：48.5MB/21.5MB 字体、TMP 示例字体、11MB 示例 Resources 全部不在列表；子集字体 4 页共约 2.1MB（压缩后）。
- `UnassignedReferenceException`/`m_AtlasTextures` 计数 **0**；`error CS` 计数 0。
- 静态文案无缺字（字符集由场景文本确定性扫描生成，含全部 900 个项目非 ASCII 字符）；真机抽查归入子任务 4。
- M2/M3 Scene SHA256 与基线一致（本次改动不触碰场景文件，字体替换仅构建内存副本）。
- `git diff --check` 无错误（仅既有 CRLF 警告）。

## 迭代过程记录（三轮导出归因）

1. slim1：88.9MB——InputField/面板字体引用残留，旧字体仍进包。
2. slim2：64.2MB——补齐 TMP_InputField + M1QAPanel.cnFont 替换后老字体应出包；同时发现 NPOT 纹理 ETC2 失效。
3. slim4/slim5：**33.3MB**——cnFont 修复落位 + 纹理覆盖全量应用 + TMP 异常修复。

## 待真机确认（归入子任务 4）

- 子集字体静态文案逐场景抽查（M1-M5）、AI 问答回复用字覆盖。
- 1024 纹理在真机的清晰度（钢轨/探头主体显示宽 ~960px，1024 纹理恰好覆盖）。
