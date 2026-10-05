# 微信小游戏上传规则合规核查与整改

## Goal

对照微信小游戏现行上传规则，逐条核查本工程与最新导出产物，给出两级明确结论：**体验版阶段**（当前）与**正式提审/发布阶段**是否合规、缺什么。工程内可闭环的缺口落地为自动断言与核对单；后台材料类缺口（备案/软著/隐私指引等）整理成老板可执行清单。本任务不执行提审、不备案、不公开发布、不覆盖线上 CDN。

## Background

- 账号：个人主体，AppID `wxcc66e002c508ed83`；当前体验版 `1.0.1`（2026-08-29 上传），未提审、未发布（08-28/09-03 任务均明确「未执行正式提审、备案或公开发布」）。
- 路线决策（08-24-wechat-minigame PRD）：先体验版验收，后正式上线；「备案提交、提审材料、类目资质」被列为 B 阶段另起任务——即本任务。
- 官方规则依据（2026-09 检索核对）：
  - 分包限制：主包+分包总大小 ≤ 30M，主包 ≤ 4M，单个普通分包不限，独立分包 ≤ 4M（developers.weixin.qq.com/minigame/dev/guide/base-ability/subPackage/useSubPackage.html）。
  - 2023 年起小程序/小游戏强制 ICP 备案，发布上线前必须备案通过（可先提审后备案）。
  - 个人主体无内购（IAA）可免版号，但必须软著 + 游戏自审自查报告 + 备案。
  - 处理用户信息需配置《用户隐私保护指引》；隐私接口（getUserInfo 等）需声明后使用。

## Confirmed Facts（2026-09-16 审计快照，产物为 Builds/WXExport/minigame 2026-09-05 导出）

| # | 规则 | 现状 | 结论 |
|---|---|---|---|
| 1 | 主包 ≤ 4M | 主包 ≈ 1.2MB（framework.js 549K + unity-sdk 396K + weapp-adapter 75K + 其他零星；symbols 13.2MB 已被 packOptions.ignore 排除） | ✅ 合规 |
| 2 | 主包+分包 ≤ 30M；普通分包单包不限 | wasmcode 7.8MB + videos 1.6MB + data-package 空，总代码包 ≈ 10.6MB | ✅ 合规 |
| 3 | 远程资源无包体限制 | webgl.data 35,488,358 bytes 走 CloudBase 静态托管 CDN | ✅ 合规（首启流量需关注，非规则项） |
| 4 | game.json 结构 / 插件 | subpackages(wasmcode/data-package/videos) + parallelPreload + UnityPlugin 1.3.7 + workers，体验版可运行证明插件已授权 | ✅ 合规 |
| 5 | 分包根 game.js（本项目回归合同） | videos/game.js、wasmcode/game.js、data-package/game.js 均存在 | ✅ 合规 |
| 6 | request/downloadFile 合法域名 | `cloud1-d6gmycfs6b37edb43-1476749432.tcloudbaseapp.com` 已配置，体验版已无 `url not in domain list`；腾讯自有域名自带 ICP | ✅ 合规 |
| 7 | 客户端不得含密钥/直连第三方 AI | DeepSeek Key 仅存云函数环境变量，客户端走 `WX.cloud.CallFunction`；导出目录扫描无 Key、无 api.deepseek.com（08-28-wechat-ai-security-boundary 已验收） | ✅ 合规 |
| 8 | 隐私接口使用 | 游戏业务代码未调用 getUserInfo/getLocation 等隐私 API（仅 SDK 适配层封装，未触发） | ✅ 当前合规 |
| 9 | 小程序/小游戏 ICP 备案 | 未办理（任务记录明确「未执行备案」） | ❌ 发布前必须 |
| 10 | 软著 + 游戏自审自查报告 | 未办理 | ❌ 发布前必须（个人主体免版号的替代条件） |
| 11 | 《用户隐私保护指引》后台配置 | 未核对（云函数按 OPENID 限流属处理用户信息） | ⚠️ 后台核对项 |
| 12 | AI 生成内容口径 | 问答内容由 DeepSeek 生成并展示给用户；个人主体办不了深度合成类目，正式提审不能带实时生成问答（体验版不受影响） | ❌ 发布前提审版本需切预设话术/隐藏入口（见 R4 方案 A） |
| 13 | 内购/虚拟支付 | 无支付代码；个人主体本不可开通 | ✅ 合规 |

## Requirements

- **R1 审计报告落档**：把上表扩充为 evidence.md，每条附证据（文件/命令输出/截图/官方文档链接），规则更新时刷新快照。
- **R2 导出合规自动断言**：在现有导出链（`WeChatMinigameExportRunner` / `MinigameSmokeExport`）追加核验步骤，任一失败即导出报错：
  1. 主包 ≤ 4M、主包+分包 ≤ 30M（按 packOptions.ignore 后的实际上传集合计算）；
  2. 每个已声明分包根存在 game.js（videos 回归合同）；
  3. 导出目录无 `DeepSeekConfig.asset`、无 `api.deepseek.com`、无 Key 明文（复用 08-28 扫描逻辑，勿重复造轮子）；
  4. game.json 的 subpackages/parallelPreload 与导出目录一致性。
- **R3 体验版上传前核对单**：checklist.md——合规断言通过、域名变更核对、版本号/备注、回滚版本记录、M2/M3 Scene 哈希核对；由执行上传的会话逐项勾选后归档到任务 logs/。
- **R4 提审材料与风险清单（老板后台操作，本任务只出文档）**：
  1. 小程序 ICP 备案流程入口与时效（平台初审 1-2 工作日、管局 1-20 工作日）；
  2. 软著申请 + 《游戏自审自查报告》模板要点；
  3. 《用户隐私保护指引》需声明项核对（OPENID/设备信息等，按实际处理情况最小声明）；
  4. AI 问答口径评估（2026-09-16 已核实结论）：**备案与 AI 功能无关，个人主体可正常办理 ICP 备案、可正常上传体验版**；卡点仅在正式提审——带生成式 AI 功能须加「深度合成」类目，需自研算法备案或与已备案技术方的合作协议，而算法备案仅对企业法人开放，微信云开发官方文档明确「个人主体小程序无法申请此类目」。老板已拍板方案 A，实施子任务 `09-16-wechat-qa-preset-switch`（qaMode 开关 + 预设话术库）：
     - **方案 A（已拍板实施）**：`AiProxyConfig.qaMode` 开关——体验版 CloudProxy 保留 DeepSeek 实时问答；正式提审版切 PresetLibrary 预设话术库（零网络请求），文案不出现「AI」字样。
     - 方案 B：升级企业主体（个体工商户/公司）→ 办理算法备案或签已备案第三方（云开发/千问等）合作协议 → 正式版保留实时 AI。成本：注册主体 + 备案材料 + 周期。
     - 方案 C：提审版直接隐藏 AI 问答入口（最简，但正式版无问答功能）。
  5. 类目选择与名称、主体一致性提醒（备案与提审信息不一致会被驳回）。

## Acceptance Criteria

- [ ] evidence.md 落档：13 条规则逐条附证据与结论。
- [ ] R2 断言接入导出链：人为构造超限/删分包 game.js/塞入测试 Key 时导出必须报错；正常导出通过。
- [x] checklist.md 落档（2026-09-21）：.trellis/tasks/09-16-wechat-upload-compliance/checklist.md；待下次实际上传时试跑。
- [x] R4 提审材料清单落档（2026-09-21）：文档/微信小游戏提审材料清单.md（电子版权认证替代软著口径 + 时间线 + 排除项）。
- [ ] M2/M3 Scene SHA256 全程不变（`896370bd…` / `2565232c…` 基线，参照 08-29 文档）。

## Out of Scope

- 实际提审、备案、软著办理、隐私指引后台配置（老板操作，本任务只出清单）。
- AI 问答功能改造（R4 老板拍板后另起任务）。
- 修改教学内容、Scene 视觉、CDN 线上文件、体验版本设置。
- 首屏加载性能优化（非规则项，如需另起任务）。
