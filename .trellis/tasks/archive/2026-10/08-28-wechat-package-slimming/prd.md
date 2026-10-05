# 微信小游戏包体瘦身

## Goal

在保持 M1-M5 教学内容和现有主包/`wasmcode` 结构的前提下，将微信首启 `webgl.data` 从 87,591,052 bytes 降至不高于 40MB。

## Confirmed Facts

- 当前 `game.js` 记录 `DATA_FILE_SIZE=87591052`。
- 当前主包约 0.98MB，`wasmcode` 约 10.17MB；二者结构已满足现阶段要求。
- BuildReport 中 `sarasa-gothic-sc-regular_cn.asset` 约 48.5MB、源 TTF 约 21.5MB，是首要体积来源。
- `Assets/TextMesh Pro/Examples & Extras/Resources` 会因 Resources 规则进入构建。
- 三态数字人图集压缩构建占用约 3.14MB，不是首要体积来源。

## Requirements

1. 生成微信专用中文 TMP 字体，只包含 M1-M5 静态文案、数字、标点、单位和经确认的 AI 常用字符集。
2. WebGL 构建期在内存场景/运行时装配路径中替换字体，不写回 M2/M3 Scene。
3. 排除源 TTF、TMP Examples & Extras、WebGL 本地视频、symbols 和无关示例资源；不得删除 Android 所需素材。
4. 关闭体验版不需要的 profiling/debug symbol 配置，并保留开发排错所需的可逆开关。
5. 重新导出并产出 Top-N BuildReport 对比；`webgl.data <= 40MB`。
6. 只有完成上述低风险瘦身仍超限时，才返回父任务提出远程模块资源组设计，不在本子任务预先引入 Addressables。
7. 所有 M1-M5 中文静态文本无方框、缺字或字体回退异常；AI 动态文本缺字风险必须有明确策略。

## Acceptance Criteria

- [ ] 最新 `game.js` 中 `DATA_FILE_SIZE <= 41943040`。
- [ ] 主包 `<4MB`，`wasmcode` 分包继续存在且可加载。
- [ ] BuildReport 不再包含 48.5MB 全量中文字体、21.5MB 源 TTF 和 TMP Examples Resources。
- [ ] M1-M5 静态文案逐场景抽查无缺字；问答回复覆盖常用中文测试集。
- [ ] WebGL 导出无 TMP `m_AtlasTextures` 异常。
- [ ] Android 资源和字体回归通过，M2/M3 Scene SHA256 不变。

## Out Of Scope

- 未经体积复测直接引入远程场景或 Addressables。
- 删除项目原始字体、美术或 Android 视频素材。
