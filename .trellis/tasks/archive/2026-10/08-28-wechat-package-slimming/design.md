# Design: 微信小游戏包体瘦身

## Strategy

按收益和风险排序执行：字体子集 > 排除无关 Resources/源字体 > 发布配置清理 > 小纹理平台覆盖。每一步都重新读取 BuildReport，达到 40MB 即停止结构性扩展。

## Font Contract

Editor 工具扫描 Build Settings 中 M1-M5 Scene、Prefab、ScriptableObject 与代码默认文案，生成确定性的字符清单和微信专用 TMP FontAsset。动态 AI 文本使用经批准的常用字符集或平台字体回退；不得为了覆盖全部 CJK 恢复当前 99MB 级全量图集。

WebGL 构建处理器只修改构建期 Scene 内存副本和运行时 Bootstrap 配置。M2/M3 文件保持字节一致。生成资产应有固定路径、可重复生成和字符清单，便于审计缺字。

## Resource Exclusion

TMP Examples & Extras 和源 TTF 只对 WebGL 构建排除。优先使用构建处理器/导出工作副本，不永久删除第三方示例目录。已有 `WebGlVideoBuildProcessor` 继续剥离 VideoClip 和输出视频目录。

关闭 `webGLDebugSymbols`、profiling 与发布不需要的开发选项；`project.config.json` 的忽略规则保留作为第二层保护，但不能代替不生成无用资产。

## Decision Gate

若低风险处理后 data 仍大于 40MB，停止扩大本子任务，输出剩余 Top-N 和最低可达估算，由父任务决定是否启动远程模块资源组。不得通过降低关键教学图片到不可辨认质量来硬过门槛。

## Rollback

字体和资源排除均只作用于 WebGL；删除生成的微信字体和恢复发布配置即可回滚。原字体、Android资源及冻结 Scene 不改。
