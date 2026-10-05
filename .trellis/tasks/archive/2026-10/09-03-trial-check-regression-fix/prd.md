# 体验版检查回归：M2/M3/M4 操作提示缺失、退出重进引导数字人不动与防反复机制

## Goal

老板体验版检查发现四项问题，需修复并在交付前确认不再回归：
1. M2 涂抹耦合剂按钮阶段旁缺「点击这里」提示。
2. M3/M4 校角阶段需拖多功能尺到探头，缺「拖动多功能尺至探头处」提示。
3. iPhone 扫码体验退出后再次进入，引导数字人不动（Android 正常）。
4. 反复出现「修好又被其他改动改坏」的回归现象，需根因分析与防再犯机制。

## Requirements（2026-09-03 逐项澄清定稿）

### R1（问题 1）：M2 Couplant 阶段「点击这里」提示

- 进入 M2 后（Couplant 阶段），在「涂抹耦合剂」按钮上方/旁创建 **DontSave TMP 文本「点击这里」**。
- 显示时机：M2 进入/ResetAll 后即出现；玩家点击「涂抹耦合剂」按钮后消失（点击瞬间即可，不必等动画完成）。
- 冻结 M2 Scene 零写回：按钮旁提示用运行时 DontSave 节点，按钮定位用运行时取 `applyButton` 位置。
- 不采用金色脉动（M1 PulseHighlight 是防卡死自动演示，语义不同）。

### R2（问题 2）：M3/M4 校角阶段「拖动多功能尺至探头处」提示

- 根因（已查证）：M3/M4 `NotifyPlacementChanged()` 是空实现（M3FlowController.cs:150 / M4FlowController.cs:150），探头放置成功不触发 `UpdateUi()`，actionHint 停在「拖动探头」，中间「拖动多功能尺至探头处」阶段不显示；M2 同函数会 `UpdateUi()`（M2FlowController.cs:154）。
- 修复：M3/M4 `NotifyPlacementChanged()` 改为触发 `UpdateUi()`（对齐 M2），使 `CurrentActionHint()` 的 Positioning 分支正常流转：`拖动探头 → 拖动多功能尺至探头处 → 滑动此处调整偏角 → 拖动多功能尺返回工具栏`。
- M3/M4 Scene 零改动。

### R3（问题 3）：iPhone 退出重进引导数字人不动

- 【2026-09-03 老板确认】平台 = **iPhone 微信小游戏（体验版扫码）**；Android 正常。iPhone 微信端不走 VideoPlayer（WKVideo 合同 2026-08-30），引导「动」= intro **帧图集动画**（`TryPlayPosterFrameAnimation` → `M1DigitalHumanFramePlayer.PlayState("intro")` + realtime 字幕）。
- 方向（老板选定 = 假设 b）：iPhone 首次正常、冷启动/重进后引导画面出现但人物静止 → 二次进入链路问题：`M1_Intro_Seen`=1 已持久化（非首次可跳过），引导仍应显示并重播 intro 帧动画；图集页被 `FinishIntro→StopAndRelease` 卸载后二次 `Resources.Load` 失败，或 `ExperienceReplayOnResume` 重载 M1 时播放器/隐藏状态未复位。
- 实现期拆两条子路径分别修复/验证：**A) 冷启动重进**（全新进程，PlayerPrefs 持久 → 非首次但引导仍播）；**B) M5 完成后 hide→show**（`ExperienceReplayOnResume` 清标记 + LoadScene M1 同进程重播）。
- **验证策略（老板确认 = 选项 A）**：修复以 **微信开发者工具**验证逻辑层——工具内 `WechatPlatformKey` 非 "android" → `_webGlPosterOnly=true`，与 iPhone 同走海报+帧图集后端，可覆盖「二次进入重播、PlayState 触发、Resume 状态复位」；支持清缓存模拟冷启动。**iPhone 真机扫码回归列入验收清单，由老板/体验成员用现有体验版流程延后执行**（平台特有 `Resources.Load` 行为差异仅真机能覆盖）。

### R4（问题 4）：反复回归的根因解释与轻量防再犯

- 【老板确认深度 = 解释 + 轻量落地】交付两部分：
  1. 根因解释（证据链汇总，写进收尾 spec）——多源：冻结 Scene 被意外写回（M2.unity 现有未提交改动即活例）、并行会话改同一批共享文件（journal-1.md 多次记录、AGENTS.md 要求串行协调）、修复靠人工 Play 验收无自动化护栏、共享脚本改动影响面未回归。
  2. 轻量防再犯落地：为本次各项修复补**可重复验收命令/断言**；经 `trellis-update-spec` 沉淀「冻结哈希校验 + 全模块 Play 冒烟」规范条目。
- 不建 CI/自动化跑批（如需可作后续独立任务）。

## Acceptance Criteria

- [ ] R1：M2 Couplant 阶段按钮旁可见「点击这里」；点击涂抹按钮后消失；ResetAll 重现；M2 Scene 零写回。
- [ ] R2：M3/M4 探头放置成功后 actionHint 立即切「拖动多功能尺至探头处」；尺子吸附后切「滑动此处调整偏角」；M3/M4 Scene 零改动。
- [ ] R3（逻辑层）：微信开发者工具验证 A（清缓存冷启动重进）与 B（M5 完成 hide→show 重播）两条路径 intro 帧动画均正常播放、字幕走完、遮罩按时释放；修复代码不破坏 Android/Editor 既有 VideoPlayer 路径。
- [ ] R3（真机层，延后由老板扫码验收）：iPhone 首次进入正常动；杀掉小游戏冷启动重进后引导人物恢复动画。
- [ ] R4：产出根因解释 + 本次修复的可重复验收命令/断言清单；冻结哈希校验与冒烟约定已沉淀进 spec。
- [ ] 冻结 M2/M3/M4 Scene 的 SHA-256 校验：除已审查并确认归属的未提交改动外，本次任务不得新增 Scene 写回。

## Out Of Scope

- 冻结 M2/M3/M4 Scene 视觉改动与结构重排。
- 未授权历史未提交改动清理：M2.unity（presenter/probeDrag 字段序列化）与 `sarasa-gothic-sc-regular_cn.asset`（2.5 万行字距表）先审查来源与归属，不擅自回退。
- CI/自动化跑批搭建、正式提审发布。

## 子任务图与总收口

| 子任务 | 需求 | 交付物 | 完成判定 |
|---|---|---|---|
| `09-05-module-action-hints` | R1（M2 点击这里）+ R2（M3/M4 拖尺提示） | 运行时提示修复 | Editor Play 验收；Scene 零写回 |
| `09-05-iphone-intro-replay-freeze` | R3（iPhone 重进引导静止） | 引导二次进入链路修复 + 开发者工具验证 | 逻辑层 PASS；真机扫码遗留清单 |
| `09-05-regression-prevention` | R4（反复回归根因+防再犯） | 根因文档 + 验收命令/断言 + spec 沉淀 | spec 落地；依赖前两组验收结论 |

- 执行顺序：先 `module-action-hints`（可立即 Editor 验收）→ 再 `iphone-intro-replay-freeze`（需微信构建/开发者工具）→ 最后 `regression-prevention`（沉淀前两组验收结论）。无强依赖阻塞，可并行但推荐按序归档。
- **跨子任务验收（父任务收口）**：① 四项问题修复证据齐备；② 冻结 M2/M3/M4 Scene 哈希对比无新增写回（R1/R2 组验收已含）；③ iPhone 真机扫码回归项写入遗留清单并转交老板；④ 防再犯约定已沉淀 spec。
