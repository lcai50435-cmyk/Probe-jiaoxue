# M2 探头、字体与波形修复执行清单

> 2026-08-12 移交完成：本清单遗留的 Play Mode 项已由 `08-12-m2-final-functional-closeout` 的 Editor Play Mode 烟测覆盖；110mm 改为与透明钢轨红色伤损视觉位置校准，最终 M2 哈希为 `3ef75ced51304258b5bde9b43be8f354b247753801a708ae52b922b5829c990b`。

## 1. 实施前基线

- [x] 记录 `git status --short`，保护全部既有未提交改动。
- [x] 读取 Unity 版本并确认使用 `6000.3.21f1`。
- [x] 核对当前 M2 场景中 Probe 数量、父级、M2 组件数量和 TMP 字体 GUID。
- [x] 确认 Unity Editor 是否占用项目；占用时不并发启动批处理。

## 2. 探头暂存与放置

- [x] 修改 `M2Setup`：Ensure 唯一 Probe 初始位于 `ProbeHome`，不在 `RailViewport` 生成第二个实例。
- [x] Setup 注入 `ProbeHome`、`RailViewport`、起始位置/容差等引用与配置，并自愈旧场景 Probe。
- [x] 修改 `M2ProbeDrag`：实现未放置时跟随拖动、释放判定、命中起始区吸附、未命中回暂存位。
- [x] 0°允许放置；纵向扫描仅在 10°且已放置时允许。
- [x] 放置/调角无论先后，条件同时满足即自动进入 Step 3。
- [x] Reset 将 Probe 重挂并归位 `ProbeHome`，恢复唯一实例和初始状态。

## 3. 手动扫描坐标

- [x] 修正中心原点局部像素到 0~1 归一化锚点的换算，使用 RectTransform pivot。
- [x] 手动和 `AutoMoveToMm` 共用位置/距离报告逻辑。
- [x] 静态验证左右边界对应 150/100mm，110mm 对应 x=0.7142；快速跨越检出留待 Play Mode。

## 4. 中文字体与清晰度

- [x] 修改 Setup 的 TMP 遍历为 includeInactive，覆盖隐藏步骤容器。
- [x] 重跑 Setup 后确认 M2 场景无 `LiberationSans SDF` 文本引用（31/31 个 TMP 使用中文字体）。
- [x] 三视口静态截图无中文方框和明显溢出；1920x1080 Game View 1x 最终观感留待人工确认。
- [x] 未修改/重建全局字体资产；TMP Editor 退出告警来自项目其他既有空 atlas 字体资源。

## 5. 波形与状态

- [x] 修改 `M2WaveformGraphic`，让 125/112/110/108/100mm 配置真正参与幅度阶段计算。
- [x] 修改 Flow 的波形状态更新：未检出按距离显示基线/生长，检出后标题保持峰值锁定。
- [x] 静态验证波形关键点和 256 个绘制顶点；蜂鸣一次性与峰后实时下降留待 Play Mode。

## 6. Setup 与静态验证

- [x] 使用 Unity 6000.3.21f1 批处理运行 `M2Setup` 多次。
- [x] 最终两次 M2 哈希稳定为 `069f69e...`；Probe/ProbeHome/关键组件各 1 个，引用非空。
- [ ] 保存并重新打开 M2 后确认运行时绑定与完整交互仍有效（Play Mode）。
- [x] 无 Missing Script/编译错误；M1 哈希始终为 `10884e...`，本轮未改 M1。

## 7. Play Mode 验收

- [x] 初态探头、涂抹后定位、10°推进、150→100mm、110mm检出、峰后状态、尺子吸附、完成与重置已在最终收口自动烟测通过。
- [x] 三视口已用修正后的 GPU batchmode 截图工具检查，无空图或1280裁切。
- [ ] 拖拽手感、快速往返蜂鸣听感和30/60秒真实等待保留主编辑器人工体验检查。

## 8. 质量检查

```bash
wc -l Assets/Scripts/M2*.cs
rg -n "Resources\.Load|AssetDatabase|LoadAssetAtPath" Assets/Scripts/M2*.cs
rg -n "8f586378b4e144a9851e7b34d9b748ee" Assets/Settings/Scenes/M2.unity
git diff --check
```

- [x] M2 runtime 脚本每个不超过 150 行（最大 Flow 147、Probe 141）。
- [x] Runtime 无 Editor API/Assets 路径加载。
- [x] 最终 Unity 编译日志无 `error CS`；Setup/截图完成后仅有项目级 TMP Editor 退出回调告警。
- [x] 变更只落在本任务边界；未提交、未推送。

## 回滚点

- 探头迁移失败：恢复已批准的 M2 场景结构，只回滚 Probe 迁移与 `M2ProbeDrag` 改动。
- 字体自愈引发布局变化：保留中文字体修复，单独回滚尺寸类变更，不恢复 LiberationSans。
- 波形回归：回滚幅度函数与状态文案更新，不影响已经修好的拖拽坐标。
