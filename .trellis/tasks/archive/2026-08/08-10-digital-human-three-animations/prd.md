# 数字人三视频静音导入配置

## 目标

仅关闭三个指定 MP4 的 Unity VideoClip 音频导入，确保这些 VideoClip 不携带可播放音轨。不接入场景、状态机或 UI。

## 已确认事实

- 三个目标 MP4 的 Unity `.meta` 均为 `VideoClipImporter` 且当前 `importAudio: 1`。
- 将 `.meta` 中的 `importAudio` 改为 `0` 后，Unity 重新导入时不再为对应 VideoClip 导入音频数据。
- 黑底抠像由播放时的 `UI/LumaKey` Shader 完成，不能在不重编码视频且不接入显示层的前提下写入 MP4 本身；不属于本任务。

## 素材范围

| 动画 | 仅修改的导入元数据 |
|---|---|
| 待机 | `Assets/DigitalHuman/A-01 待机动画/待机动画.mp4.meta` |
| 讲解 | `Assets/DigitalHuman/A-02讲解动画/讲解动画2.mp4.meta` |
| 思考 | `Assets/DigitalHuman/A-03 思考动画/思考动画.mp4.meta` |

## 要求

1. 仅将上述三个 `.mp4.meta` 的 `VideoClipImporter.importAudio` 设为 `0`。
2. 不修改 MP4/WebM 原始视频文件。
3. 不修改其他 VideoClip 的导入配置，包括对应 WebM 和 M1 开场引导视频。
4. 不修改场景、Editor Setup、运行时脚本、Shader、材质或 AI 问答流程。

## 验收标准

- [ ] 三个指定 `.mp4.meta` 均包含 `importAudio: 0`。
- [ ] 三个原始 MP4 文件未改动。
- [ ] 除任务规划文件外，Git 变更仅包含这三个 `.mp4.meta` 文件。

## 范围外

- 黑底抠像或透明视频输出。
- 视频重编码、转码、剪辑或删除。
- 对应 WebM 的静音配置。
- 场景接入、VideoPlayer 配置、UI、脚本、Shader、材质和 AI 动画状态切换。
