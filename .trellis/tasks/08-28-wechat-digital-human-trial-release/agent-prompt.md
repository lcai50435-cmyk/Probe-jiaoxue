Active task: .trellis/tasks/08-28-wechat-digital-human-trial-release

你负责继续完成该 Trellis 父任务下的四个修复子任务。用户已批准修复规划，但没有授权 git commit/push，也没有授权正式提审、公开发布或覆盖线上 CDN。使用简体中文汇报，称呼用户为“老板”。

先执行以下准备：

1. 运行 `python ./.trellis/scripts/task.py current`，确认父任务和四个子任务存在。
2. 阅读父任务 `prd.md`、`design.md`、`implement.md`，再按每个子任务的 `implement.jsonl -> prd.md -> design.md -> implement.md` 加载上下文。
3. 阅读并遵循 `.trellis/spec/unity/low-code.md` 与 `.trellis/spec/unity/video-intro.md`。
4. 保存当前 `git status`、M2/M3 Scene SHA256、最新 `game.js` DATA_FILE_SIZE、BuildReport 和 Android APK 时间基线。
5. 工作区已有大量未提交用户改动；禁止回退、覆盖或清理不属于当前子任务的修改。M2/M3 Scene 必须保持字节一致。

在同一个工作区按以下顺序执行，不要并行编辑重叠文件：

## 1. AI 安全边界（P0）

启动：
`python ./.trellis/scripts/task.py start .trellis/tasks/08-28-wechat-ai-security-boundary`

按该子任务规划实现并验证：WebGL 构建不包含真实 `DeepSeekConfig.asset` 或 Key；WebGL 不设置 DeepSeek Bearer、不直连 DeepSeek；有 CloudBase 代理配置时只调用代理，无代理时安全禁用问答且不阻塞教学。Android/Editor 保留现有配置行为。构建期临时资产处理必须在 `finally` 和下次启动恢复，禁止丢失本地配置。

若缺少 CloudBase 权限/端点，完成“无密钥 + 安全禁用”并明确记录外部阻断；不得伪造代理成功。不要上传体验版。

实现后运行 `trellis-check` Agent。修复检查发现的问题；更新必要规范，但未获授权不得 commit/archive。

## 2. 包体瘦身（P0）

启动：
`python ./.trellis/scripts/task.py start .trellis/tasks/08-28-wechat-package-slimming`

按该子任务规划实施：生成确定性的微信 TMP 中文子集；WebGL 构建期替换字体但不写回冻结 Scene；排除全量字体、源 TTF、TMP Examples Resources、本地视频和发布调试资源；关闭体验版 profiling/debug symbols；重新导出并解析 BuildReport。

硬门槛：`game.js` 中 `DATA_FILE_SIZE <= 41943040`，主包 `<4MB`，`wasmcode` 保留，M1-M5 静态中文无缺字，日志无 TMP `m_AtlasTextures` 异常。未达到 40MB 时停止扩大范围，给出剩余 Top-N 和远程模块组建议；未经老板确认不要引入 Addressables/远程 Scene。

实现后运行 `trellis-check` Agent；未获授权不得 commit/archive。

## 3. 数字人视觉与内存（P1）

启动：
`python ./.trellis/scripts/task.py start .trellis/tasks/08-28-wechat-digital-human-polish`

按规划修复：LumaKey 对原始采样完成颜色空间转换后只乘一次 UI tint；字幕在 Canvas/AspectRatioFitter 布局完成后按人物实际边界重算，16:9/宽屏间距至少 20px；图集稀疏末页按有效行列裁切；播放器避免每次状态切换同步触发昂贵全局卸载并处理缺页；微信保持帧图集三态，Android/Editor 保持 VideoPlayer。

重新生成图集、导出微信、构建新的 Android Debug APK。验证颜色、字幕、待机/思考/讲解、长按 QA、M1-M5 数字人和内存/卡顿。不得修改 M2/M3 Scene。

实现后运行 `trellis-check` Agent；未获授权不得 commit/archive。

## 4. 体验版与跨平台验收

只有前三项检查通过后才启动：
`python ./.trellis/scripts/task.py start .trellis/tasks/08-28-wechat-trial-device-acceptance`

先完成本地构建和设备矩阵：Android APK、微信 Android、微信 iPhone 的冷/热启动、左右横屏、M1-M5、引导颜色/字幕、常驻数字人三态、QA 和拖拽。设备、账号或 CloudBase 权限缺失时如实报告，不以开发者工具替代真机。

上传版本化 CDN、上传微信开发版本、设置体验版和生成二维码属于外部发布操作。执行前向老板明确列出版本号、目标 AppID、CDN 路径、预计影响和回滚版本，并取得本次明确授权。不得提审、备案或公开发布，也不得覆盖旧 CDN 目录。

## 全程门禁

- 每轮修改前后运行 `git diff --check`，并核对 M2/M3 SHA256。
- 先运行最小相关验证，再运行 Unity 编译、微信导出、Android 构建和真机验证。
- 区分本次错误与既有无关错误，不顺手修复范围外问题。
- 新增 runtime 脚本默认不超过 150 行；优先复用现有组件、配置和 Editor 工具。
- 不删除 Android 需要的视频、字体或美术原始资产。
- 不执行 git commit/push，除非老板在当前任务中明确授权。
- 每个子任务完成检查后更新任务证据；最终由父任务汇总四项 Acceptance Criteria。若任何 P0 或真机门禁失败，不得宣称体验版可交付。
