# 微信小游戏转换 — 实施计划

## 📍 当前进度快照（2026-08-25 更新）

- **已完成**：阶段 0（git 安全点）+ 阶段 1（冒烟排雷）✅ —— **Unity 6000.3 直转可行已验证**
- **当前状态**：任务**挂起中**。用户决定先做游戏本体修改（文字提示 / 防挂机自动流程 / 音量），内容定稿后再继续
- **下一步**：
  1. 【游戏本体】三类修改：文字提示调整、防挂机自动流程（约 1 天，通用组件 + 各模块 FlowController 接线）、音量调整 → 用户验收定稿
  2. 【恢复本任务】从 **阶段 2（首包骨架 Boot 场景）** 开始，一口气推进到阶段 6 出体验版二维码
- **分支状态**：工作在 `minigame` 分支（tag `pre-minigame` 兜底）；manifest.json + MinigameSmokeExport.cs 改动**尚未提交**
- 恢复时无需重新冒烟，直接从阶段 2 开工；若期间 Unity 版本升级过，重跑一次 `MinigameSmokeExport.ExportBatch` 确认即可

## 前置（用户侧，与工程并行）
- [ ] 注册微信小游戏账号（个人主体）→ 拿到 AppID
- [ ] 安装微信开发者工具 Stable 版
- [ ] （可后置）MP 后台开通云开发免费环境

## 执行清单（工程侧）

> **执行节奏（2026-08-24 用户拍板）**：游戏内容还会更改，本期仅执行阶段 0 + 阶段 1 冒烟排雷；
> 阶段 2~7 等游戏定稿后继续，规划不作废。
> **补充（2026-08-25）**：定稿前的本体修改范围已明确 = 文字提示 + 防挂机自动流程 + 音量。

### 阶段 0：安全点 ✅ 已完成（2026-08-24）
- [x] git 打 tag `pre-minigame`；创建分支 `minigame`
- [x] .gitignore 覆盖确认：`Builds/` 原有条目已覆盖转换导出目录（Builds/WXExport）；云函数 node_modules 待阶段 5 建目录时补

### 阶段 1：冒烟试验（D1 决策门，~半天）✅ 已完成（2026-08-25）
- [x] Packages/manifest.json 加 git URL 安装 `minigame-tuanjie-transform-sdk`
      （实际包名 `com.qq.weixin.minigame`；github 直连可用）
- [x] WebGL 基线构建成功（转换流程内含构建，一步验证）
- [x] 转换导出小游戏工程成功（脚本化 `MinigameSmokeExport.ExportBatch`；产物 `Builds/WXExport/{minigame,webgl}`）
- [ ] 开发者工具打开 /minigame 验证能进 M1 画面 → **待用户装工具后人工验证**（不阻塞后续阶段）
- ✅ **结论：Unity 6000.3.21f1 直转成功，无需降级 2022**
- 实测数据：minigame 代码包 23.5MB（含 symbols 12.9MB 可去 + wasm.br 9.7MB 可分包 → 首包优化空间充足）；
  webgl 远程资源 314MB（data 包 124MB = 全量资产，印证阶段 2 Boot 场景 + 资源拆分必要性）

### 阶段 2：首包骨架（D6/R2）⬜ 下一阶段
- [ ] `MinigameBootSetup.cs`（Editor 幂等）：生成 Boot.unity（Logo+进度条+BootLoader）
- [ ] Build Scenes 第 0 位插入 Boot；验证首包 ≤4MB，超限则启用代码分包
- [ ] 远程资源分组配置（CDN 地址 = CloudBase 域名占位）

### 阶段 3：数字人序列帧（D2/R3）
- [ ] Editor 工具 `DigitalHumanFrameExtractor.cs`：webm→PNG 序列 + 清单 asset（参数：fps/宽度/输出目录）
- [ ] `SequenceFramePlayer.cs`（runtime ≤150 行）
- [ ] M1 数字人与 M3/M4/M5 Bootstrap 三态接入序列帧素材源
- [ ] Editor 内回归：三态切换、暂停恢复行为不变

### 阶段 4：引导视频（D3/R4）
- [ ] M1IntroVideo 播放后端抽象（VideoPlayerBackend / WxVideoBackend）
- [ ] 小游戏端 WX.video 播通；真机效果不达标 → 序列帧兜底并回填 PRD D3 结论

### 阶段 5：AI 中转（D4/R5）
- [ ] CloudBase 云函数 `deepseek-proxy`（Key 入环境变量）
- [ ] M1DeepSeekClient endpoint 注入；小游戏构建指向云函数
- [ ] 开发者工具内问答一轮对话通过

### 阶段 6：联调交付（R6）
- [ ] 上传静态托管资源；配置转换面板 CDN
- [ ] 真机测试矩阵：Android + iPhone 各一台，按验收标准全流程走查
- [ ] iPhone 5 分钟压力观察无闪退（纹理 ASTC/内存调参如需要）
- [ ] 上传体验版，生成二维码交用户分发

### 阶段 7：回归收尾（R7）
- [ ] 主线分支 APK 构建回归成功
- [ ] 冻结场景 git diff 审查（应为零改动或仅可解释的序列化噪声）

## 验证命令
- 冒烟导出（已验证可用）：Unity batchmode `-executeMethod M1.EditorTools.MinigameSmokeExport.ExportBatch -buildTarget WebGL`
- 转换导出底层：`WXEditorWin.DoExport(true)` / `WXConvertCore.DoExport(bool)`，配置经 `UnityUtil.GetEditorConf()`（ProjectConf.relativeDST 必须非空）
- 云函数本地：`tcb fn run` 或开发者工具云开发面板调试

## 风险文件与回滚点
| 文件/位置 | 风险 | 回滚 |
|---|---|---|
| Assets/Settings/Scenes/*.unity | 重序列化污染冻结视觉 | git checkout 单文件；阶段 7 diff 审查 |
| Packages/manifest.json | SDK 依赖冲突 | tag `pre-minigame` 还原 |
| M1IntroVideo / M1DeepSeekClient / 数字人相关 | 触碰存量代码 | 平台宏隔离 + 单独 commit，逐个可 revert |
| ProjectSettings（切平台） | 误提交平台切换 | 仅 minigame 分支保留，合流策略再定 |

## task.py start 前检查
- [x] prd.md 收敛（无未决 Open Questions）
- [x] design.md / implement.md 就绪
- [x] 用户对最终规划摘要明确批准（2026-08-24"先做2"= 批准冒烟范围；2026-08-25 确认挂起等游戏定稿）
