# 数字人全身头像与问答动画联动 - 实施计划

## 前置门

- 确认 `.trellis/tasks/08-10-digital-human-three-animations` 当前改动，只消费其三个 MP4 静音元数据，不重复编辑或回滚。
- 记录相关文件工作树 diff，避免覆盖用户在 M1 场景、QAPanel 和 Setup 中的未提交改动。
- Phase 2 开始时加载 `trellis-before-dev`，再次核对 unity/low-code 与 video-intro 规范。

## 实施清单

1. **扩展输入检测**
   - 修改 `Assets/Scripts/M1PressDetector.cs`，增加 `OnShortPress` 和长按抑制标志。
   - 验证 PointerExit、短按、长按三个路径不会重复发事件。

2. **补充 QAPanel 生命周期事件**
   - 修改 `Assets/Scripts/M1QAPanel.cs`，增加 `OnPanelVisibilityChanged(bool)` 与 `bindPressTarget`。
   - Open 开始发布 true；Close 滑出完成、对象隐藏后发布 false。
   - 保持 DeepSeek、气泡、逐字显示和 AnswerState 时机不变。

3. **新增数字人 Presenter**
   - 新建 `Assets/Scripts/M1DigitalHumanPresenter.cs` 及 `.meta`，默认 <=150 行。
   - Inspector 引用：QAPanel、VideoPlayer、RawImage、FullBodyView、AvatarView、两个 PressDetector、Idle/Thinking/Speaking clips。
   - 订阅/解除订阅输入、面板和 AnswerState 事件。
   - 管理 FullBody/Avatar、自动展开、延迟恢复、三态循环与 RenderTexture 生命周期。
   - FullBodyView 每次重新激活时按当前 AnswerState 恢复播放；RenderTexture 使用高质量缩小过滤。
   - 强制 `audioOutputMode=None`，不依赖 VideoClip 是否带音轨。

4. **重构 QA Setup 的结构所有权**
   - 修改 `Assets/Editor/M1QASetup.cs`，Ensure `ChatArea` 并把既有 QAPanel 重挂到其下。
   - 设置 ChatArea 右侧预留和 QAPanel 560-600px 宽；不重建既有消息子树。
   - Ensure 根级 `DigitalHumanStage`、FullBodyView、AvatarView，并 SetAsLastSibling。
   - FullBodyView 配置 RawImage、AspectRatioFitter、VideoPlayer、透明点击承载与 PressDetector；AvatarView 配置 A-05 图片与 PressDetector。
   - 仅加载用户指定的待机、讲解2、思考三个 MP4，以及数字人专用 LumaKey 材质和头像；不加载对应 WebM；Presenter 非空字段不覆盖。
   - 数字人专用材质从既有 LumaKey 创建但独立保存，收窄平滑参数且不影响开场引导；头像与 FullBodyView 使用相同中心锚点/偏移。
   - 隐藏旧 `白板背景/数字人/背景圆`，保留 `对话框/AI回答`。
   - 设置 QAPanel `bindPressTarget=false` 并注入 Presenter。

5. **消除 Setup 漂移**
   - 修改 `Assets/Editor/M1LayoutPolish.cs` 中受影响的 `QAPanel/Panel/...` 历史路径，确保不会把路径字段改回虚构层级。
   - 搜索所有 QAPanel、数字人和 pressTargetPath 路径引用，逐项核对真实层级。

6. **生成场景**
   - 在 Unity 可用且项目未被占用时运行 `Tools/M1/Setup AI 提问面板`；否则提供明确人工执行步骤，不手写大段场景 YAML。
   - 连续运行两次，确认无重复对象/组件且 Inspector 替换引用不被覆盖。

7. **静态验证**
   - 检查新 runtime 脚本行数 <=150。
   - 检查三个状态引用严格指向用户指定 MP4，头像和 LumaKey 引用可解析，场景不引用对应 WebM。
   - 检查状态 VideoPlayer 为 None，开场引导 VideoPlayer 仍为 Direct。
   - 检查本任务未修改原始媒体文件及前置任务负责的导入元数据。

8. **人工 PlayMode 验证**
   - 默认全身待机；短按折叠/展开；长按不误触短按。
   - 头像长按自动展开；成功回答 Thinking -> Speaking -> Idle；关闭后恢复头像。
   - 全身长按后关闭保持全身。
   - 请求中关闭：完成前保持全身，Idle 后恢复头像。
   - 空 Key/断网/空回复回 Idle。
   - 1280x720、1920x1080、2436x1125 下无遮挡、黑底、拉伸和意外音轨。

## 验证命令

```bash
rg -n "OnShortPress|OnPanelVisibilityChanged|M1DigitalHumanPresenter|VideoAudioOutputMode.None" Assets/Scripts Assets/Editor
wc -l Assets/Scripts/M1DigitalHumanPresenter.cs
git diff --check
rg -n "QAPanel/Panel|pressTargetPath" Assets/Scripts Assets/Editor Assets/Settings/Scenes/M1.unity
rg -n "audioOutputMode|m_AudioOutputMode" Assets/Editor/M1QASetup.cs Assets/Settings/Scenes/M1.unity
```

若 Unity 命令行可用且项目未锁定，再运行最小批处理编译/Setup；本机既有规范说明 batchmode PlayMode 不可靠，因此最终视觉与视频播放采用编辑器人工验收。

## 风险与回滚点

- `M1QAPanel.cs`、`M1QASetup.cs`、`M1LayoutPolish.cs` 和 `M1.unity` 均有既有/用户改动，任何编辑必须定点合并，不做整文件回退。
- 场景结构只通过 Setup 生成；若生成异常，先修 Setup 再重跑，不手工修补 YAML。
- 视频状态逻辑异常时可暂时禁用 `M1DigitalHumanPresenter`，既有 DeepSeek 面板仍可独立工作。
- 不执行 git commit/push；完成后先报告验证结果和需用户在 Unity 中确认的视觉项。

## 评审门

- PRD、design、implement 和两个 JSONL manifest 经用户确认后运行 `task.py start`。
- 启动后由 `trellis-implement` 实施，再由 `trellis-check` 做独立规格与质量复核。
