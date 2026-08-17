# M3 静态 Scene 与 UGUI 线框执行清单

## 1. 基线

- [x] 记录工作区和 M1/M2 Scene 哈希。
- [x] 确认 Unity 6000.3.21f1、中文字体、正视钢轨、K2.5 和全身数字人 PNG 可读取。
- [x] 确认本轮不新增 runtime 脚本、不修改 Build Settings。

## 2. Editor 实现

- [x] 新增 `M3Setup.cs`，提供菜单和 `SetupM3Batch`。
- [x] Scene 不存在时创建 Canvas/EventSystem；存在时幂等自愈。
- [x] 生成 Header、MainScene、RailArea/SupportArea、RailViewport、B/C/D、QA/人物/模态层；旧 TeachingArea 幂等迁移并清理。
- [x] 注入正视普通/透明钢轨、K2.5、全身数字人和中文字体。
- [x] 生成独立探头、尺子、13°指示、伤损、声束和静态波形占位。
- [x] 新增 `M3Shot.cs`，输出三视口截图。

## 3. Setup 验证

- [x] Unity 编译无 Error。
- [x] 从无 M3 Scene 状态执行 Setup 成功。
- [x] 初版连续执行 Setup 两次哈希一致；按老板要求迁移为 M2 同构布局后再次两次一致，当前 Scene SHA-256 为 `832a41ce125c1556b154e6d4d51b94eacdeef354a6bb48a34d99d34249de4ba8`。
- [x] M3 层级名称唯一，无 Missing Script，CanvasScaler 正确。
- [x] M3 Setup/Shot 未打开或保存 M1/M2；审查代理验证时 M1/M2/Build Settings 哈希保持该轮基线。工作区另有并发 M2 改动，不归因于本任务。

## 4. 截图验证

- [x] 输出 `Logs/m3-shot_1920x1080.png`。
- [x] 输出 `Logs/m3-shot_1280x720.png`。
- [x] 输出 `Logs/m3-shot_2436x1125.png`。
- [x] 检查钢轨主体、B 区、C 区、D 区、数字人无重叠和裁切；数字人在 SupportArea 上部、波形 460x240 在同区底部右对齐，与 M2 布局一致。
- [x] 检查标题、13°、120mm、步骤文字无方框或溢出。

## 5. 静态质量检查

```bash
rg -n "M2FlowController|M2ProbeDrag|M2RulerDrag|M2IdleHelp" Assets/Editor/M3*.cs Assets/Settings/Scenes/M3.unity
git diff --check
```

- [x] M3 Editor 文件不接 M2 runtime 组件。
- [x] 没有新增 `Assets/Scripts/M3*.cs`。
- [x] 本轮新增仅含 M3 任务文档、M3 Editor 文件和 M3 Scene/Meta；截图生成在现有忽略目录 `Logs/`。正视钢轨素材为本轮依赖但仍处于未跟踪状态，正式交付前需纳管。

## 审核门槛

- [ ] 老板审核并批准三视口静态线框后，才创建后续 M3 runtime 实现任务。

## 回滚

- Setup 编译或创建失败：删除本轮新建的 M3 Scene/Editor 文件，不触碰 M1/M2。
- 布局审核未通过：只调整 `M3Setup` 并重新生成 M3，不进入 runtime 实现。
