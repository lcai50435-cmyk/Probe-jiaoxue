# M2 迁移至 M3 UGUI 基线执行清单

> 2026-08-12 移交完成：本任务记录的 `presenter.qaPanel` 空引用、重置帮助未暂停及 Play Mode 门槛，已由 `08-12-m2-final-functional-closeout` 修复并自动验收；最终 M2 哈希为 `3ef75ced51304258b5bde9b43be8f354b247753801a708ae52b922b5829c990b`。

## 1. 基线与证据

- [x] 读取 M3Setup、M3 Scene 任务设计和三视口截图。
- [x] 确认 M3 与旧 M2 模板存在结构差异，不只是换色。
- [x] 记录本轮 M1/M2/M3/Build Settings 哈希。
- [x] 确认不新增 runtime 脚本、不修改 M2 业务流程和 M3 基线。

本轮迁移前哈希：

- M1：`10884e91f9436dac533ed5059da25db5dfc6a1a23e1c471cb9db9be2b393af62`
- M2：`59557bd12d542d829970134d7ec69ebb619dd7b00e9561109ffb20eae78e70e6`
- M3：`832a41ce125c1556b154e6d4d51b94eacdeef354a6bb48a34d99d34249de4ba8`
- Build Settings：`58bcbfb23da7aab5acd696e7d83e9d75a86442ed39a582159d2493049361ba28`

## 2. 实现

- [x] M2Setup 切换为 M3 Page/Surface/Ink/Muted/Primary/Accent/Screen/Grid/Wave 令牌。
- [x] Header 对齐 M3 标题与重置样式。
- [x] ToolShelf 改为左上局部约 372x88，RailViewport 保持白色主教学面。
- [x] PerspectiveBar_C 改为 364x64，运行时分段颜色同步 M3。
- [x] 波形保持 460x240 和动态数据合同，外观同步 M3。
- [x] Dock 改为浅色，保留 M2 四阶段节点、对象名和引用。
- [x] 数字人/QA/Modal 层级和视频链路保持不变。
- [x] 规范、索引、AGENTS 和任务文档改为 M3 权威结论。

## 3. Unity 验证

- [x] Unity 编译无 C# Error，Setup 保存成功。
- [x] 执行 Setup 生成 M2 Scene。
- [x] 连续双跑 Setup，最终 M2 Scene SHA-256 均为 `e76efeddec3c8e9bec77b3fcbccd865de9a645f3798ccd6bb126268f15caf735`。
- [x] 输出 1920x1080、1280x720、2436x1125 三视口截图。
- [x] 审查钢轨、局部工具架、C 控件、波形、人物舞台和浅色 Dock 无重叠/裁切/溢出。
- [x] 确认 M1/M3/Build Settings 哈希保持本轮基线。

验证在临时副本 `E:/Project/UnityGame/Probe-jiaoxue-m2-review` 执行，避免与已打开的主 Unity Editor 争用 Library。Unity 返回码为 0；Setup 与截图均成功。退出阶段报告该副本既有 TMP Font Asset `m_AtlasTextures` 未赋值异常，不影响 C# 编译、Scene 保存、双跑哈希或截图，但应在父级字体任务继续处理。

## 4. 静态检查

```bash
rg -n "M2FlowController|M2ProbeDrag|M2RulerDrag|M2IdleHelp" Assets/Editor/M2Setup.cs
rg -n "M2.*权威|DockBg|#343A40|#266AD1|深色操作带" .trellis/spec/unity/ugui-module-template.md AGENTS.md
git diff --check
```

- [x] 不存在仍生效的旧“M2 是统一视觉权威”规范。
- [x] 没有新增 M2 runtime 脚本，M2FlowController 仅改分段颜色。
- [x] `git diff --check` 通过。
- [x] Trellis 质量检查通过；视觉迁移与规范范围无中高严重度遗留，父级功能门槛保持未勾选。

## 5. 功能门槛移交结果

- [x] QAPanel 完整接入、左开布局、暂停恢复与 Presenter 引用已由最终收口任务完成；数字人实际视频画质/三态仍保留人工观察。
- [x] M2 四阶段、10°、110mm、波形、正式尺子吸附、完成和重置已由 Editor Play Mode 自动烟测通过。
