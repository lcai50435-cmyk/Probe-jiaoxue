# Implementation Plan: 微信小游戏包体瘦身

## Steps

1. 保存当前 `game.js`、BuildReport Top-N、M2/M3 SHA256 和字体引用图。
2. 提取 M1-M5 静态字符集，生成可重复的微信 TMP 子集字体及字符清单。
3. 在 WebGL Scene 处理和 `M3DigitalHumanBootstrap` 运行时装配中使用微信字体，不保存冻结 Scene。
4. WebGL 构建期排除全量字体源 TTF、TMP Examples Resources 和其他确认未引用的示例资源。
5. 关闭体验版 profiling/debug symbols；保持 Android 设置与素材不变。
6. Unity 编译、微信导出并解析 `Library/LastBuild.buildreport`、`Temp/wx-export.log` 和 `game.js`。
7. 逐场景检查静态文案及 AI 常用中文；修复 TMP atlas 异常。
8. 达到 40MB 即停止；未达到则提交剩余体积归因和远程组启动建议，不擅自扩大范围。

## Validation

- `DATA_FILE_SIZE <= 41943040`。
- 主包 `<4MB`，`wasmcode` 存在。
- BuildReport 不含全量字体、源 TTF 和 Examples Resources。
- Unity/Shader 无编译错误，TMP 日志无 `m_AtlasTextures` 异常。
- M2/M3 SHA256 不变；Android 字体与视频正常。

## Rollback Points

- 字体缺字：扩充确定性字符集，不直接恢复全量字体。
- 排除误伤：恢复单项排除并用 BuildReport 定位依赖。
- 构建回调异常：确保临时移动资产恢复后再退出。
