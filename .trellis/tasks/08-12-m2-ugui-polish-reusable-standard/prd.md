# M2 迁移至 M3 UGUI 基线与复用规范

> **冻结通知（2026-08-12，覆盖下文旧 Setup/Scene 修改要求）**：当前 `Assets/Settings/Scenes/M2.unity` 与 `M3.unity` 已由老板批准并冻结。不得通过程序、Setup 或 Agent 修改、创建、重生成或保存覆盖；只有老板可在 Unity Scene 中手工改视觉。两份 Setup 仅为只读打开器。

## 目标

以已审核的 M3 静态场景布局和画风为统一权威，更新 M2「轨头顶面探测」的 UGUI，并形成 M4、M5 可直接采用的规范。M2 保留四阶段流程、交互坐标、110mm 检出、波形数据合同、QA 暂停和全身视频数字人链路。

## 最新产品决定

- 2026-08-12 最新决定覆盖此前“M2 是统一视觉模板”的结论：M3 的布局和画风更好，统一基线改为 M3。
- 权威证据为 `Assets/Editor/M3Setup.cs`、`Assets/Settings/Scenes/M3.unity` 和 `Logs/m3-shot_*.png`。
- M3 基线包括浅灰页面、白色教学面、青蓝主色、橙色教学强调、浅色 Dock、左上局部工具架、左下 364x64 分段控件、右上人物与右下 460x240 波形上下组合。
- M2 同步视觉和构图，但不复制 M3 静态占位逻辑，不修改 M2 业务状态机。
- 数字人保持 `M1DigitalHumanPresenter + VideoPlayer + RenderTexture + UI-LumaKey-DigitalHuman`，不得替换为静态图片。

## 需求

1. M2 页面令牌和主要布局对齐 M3；Header 高 80px、Dock 高 176px、SupportArea 576px。
2. ToolShelf 改为 RailArea 左上局部约 372x88，不横贯教学区。
3. RailViewport 保持白色无装饰教学面；M2 俯视钢轨尺寸、归一化拖拽坐标和素材不变。
4. PerspectiveBar_C 改为 364x64；选中使用 M3 Primary，未选使用中性灰，运行时同步背景与文字色。
5. 波形固定 460x240、SupportArea 底部右对齐，采用 M3 Screen/ScreenGrid/Wave 外观；M2 的动态波形、150→100mm 和 110mm 合同不变。
6. DigitalHumanStage 保持约 320px、SupportArea 上部右对齐；QAPanel 继续从人物左侧展开。
7. ControlDock_D 使用 M3 浅色 Surface、Ink/Muted 文本、Primary 主操作和 Accent 教学强调；保留 M2 四阶段节点和引用。
8. Setup 幂等自愈布局、字号、颜色、顺序和组件，不新增 runtime 脚本。
9. 更新 Unity UGUI 规范、索引和 AGENTS 摘要，删除“M2 是视觉权威”和深色 Dock 标准。
10. 输出并审核 1920x1080、1280x720、2436x1125 三视口；另保留 Play Mode 功能回归门槛。
11. 不修改 M1/M3 Scene、M3Setup、Build Settings、问答网络逻辑或 M2 业务流程。

## 验收标准

- [x] M2 三视口符合 M3 画风，无重叠、裁切、方框字或文字溢出。
- [x] M2 为局部工具架、白色教学面、364x64 分段、浅色 Dock，人物和波形上下排列。
- [x] 波形 460x240，动态曲线、150→100mm、110mm 和当前距离清晰可读。
- [x] 普通/透视、重置和步骤主操作有效触控尺寸不小于 64px。
- [x] `M2Setup` 连续执行两次后 Scene SHA-256 不变，只保存 M2。
- [x] Unity 编译无 C# Error，`git diff --check` 通过；临时副本退出阶段仍有既有 TMP Atlas 异常。
- [x] M1/M3/Build Settings 相对本次迁移基线无新增变化。
- [x] 规范明确 M3 是权威来源，M2 是兼容迁移，M4/M5 默认采用 M3 基线。
- [ ] Play Mode 数字人、QA 和完整四阶段行为无回归（父级 M2 功能任务验收）。

## 范围外

- 修改 M3 已审核场景或 M3Setup。
- 改变 M2 教学流程、角度、目标距离、坐标或波形数据模型。
- 替换数字人视频、LumaKey 材质或问答链路。
- M4/M5 runtime 实现与 Build Settings 串联。
- 外部素材搜索或正式美术制作。
