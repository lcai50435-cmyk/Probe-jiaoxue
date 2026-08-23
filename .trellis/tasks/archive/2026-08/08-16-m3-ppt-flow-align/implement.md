# Implement：M3 轨头侧面探测按 PPT 对齐流程与波形

## 执行顺序
1. **备份/基线**
   - 记录当前 `Assets/Settings/Scenes/M3.unity` SHA-256。
   - 确认 git 工作区可回滚。

2. **Scene 修改（M3.unity）**
   - 波形区域按 design 1.1 重做：尺寸、节点增删、挂载 `M2WaveformFx`、新增 `ScaleTexts`。
   - 尺子 `bg` 换 `尺子正面.png`，删除 `ScaleText` 占位。
   - 探头起始位置/旋转按 PPT 调整（如几何标定后需要）。
   - 校验 YAML：`grep -c '^--- !u!1 &'` 与块体配对、无孤立块体；Unity 打开无报错。

3. **Runtime 脚本修改**
   - `Assets/Scripts/M3FlowController.cs`：波形引用/参数/提示/锁定。
   - `Assets/Scripts/M3ProbeDrag.cs`：160→120、像素几何、射线颜色变化、检出锁定。
   - `Assets/Scripts/M3RulerDrag.cs`：正式尺标定、PixelsPerMm、0/120 双点测量。
   - `Assets/Scripts/M3IdleHelp.cs`：自动演示适配。

4. **验收脚本更新**
   - `Assets/Editor/M3RuntimeSmoke.cs`：更新距离/波形/射线/测量断言。
   - 如需要，更新 `M3Shot.cs` 仅保证 Scene 哈希记录方式。

5. **编译/运行验证**
   - Unity batch compile（`-batchmode -quit` 或项目内现有 compile log 方式）。
   - 跑 `M3RuntimeSmoke`：全 PASS。
   - 跑 `M3Shot`：三视口 PNG 非空，Scene 哈希为修改后新哈希。

6. **文档/规范更新**
   - 更新 `.trellis/spec/unity/low-code.md` 中 M3 波形合同、射线合同、测量合同。
   - 更新 `.trellis/spec/unity/module-flow-contract.md` 或 `ugui-module-template.md` 的 M3 段落。
   - 同步 `AGENTS.md` 摘要（如涉及）。

7. **收尾**
   - 确认 M3 Scene 已保存且 Play/Scene 同步。
   - 提交前由老板/用户做最终目视确认。
   - 完成任务归档。

## 验收命令（参考）
- Unity batch compile / PlayMode smoke 使用项目现有 `M3RuntimeSmoke`。
- 截图使用 `M3Shot.CaptureAll`。
- Scene YAML 块头配对命令：
  ```bash
  grep -c '^--- !u!1 &' Assets/Settings/Scenes/M3.unity
  grep -c '^GameObject:' Assets/Settings/Scenes/M3.unity
  ```
- 波形参数断言可直接在 PlayMode 中调用 `M2WaveformFx.SetDistanceMm` 验证 Strength/PeakU。
