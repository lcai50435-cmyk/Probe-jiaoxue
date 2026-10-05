# 实现证据：微信数字人视觉与内存修复（2026-08-28）

## 基线

- Shader：第 130 行 `(tex2D + _TextureSampleAdd) * IN.color` 先乘 tint，第 134 行键控在 tint 后计算，第 146 行 WebGL 路径 `GammaToLinearSpace(srgb) * IN.color.rgb` 二次乘 tint（白 tint 下不可见，非白 tint 双重作用）。
- 字幕：`ApplyWebGlSubtitleLayout()` 仅在 Start 早期调用一次，此时 AspectRatioFitter/Canvas 尚未稳定；冲突时 `Mathf.Max(y, minBottom)` 把字幕向上压回人物。
- 图集：固定 2048² 页；thinking 末页 13 帧、speaking 末页 2 帧占完整页。
- 播放器：每次切态 `Resources.UnloadUnusedAssets()` 全局扫描。
- Android APK 基线：ProbeTeaching-debug.apk 273,190,085 bytes（2026-08-27 19:47）。
- M2 SHA256 `896370BD…`、M3 SHA256 `2565232C…`。

## 改动清单

1. **`Assets/Shaders/UI-LumaKey.shader`（tint 单次化）**：frag 重排为 ①原始纹理采样（不含 tint）→ ②sRGB 键控/绿线判定（基于原始值）→ ③显示 RGB 颜色空间转换（`_VideoInputIsSRGB=1` 时 `GammaToLinearSpace`，=0 保持线性采样值）→ ④最后统一乘一次 `IN.color`（rgb/a）→ ⑤UI clip/alpha clip。白 tint 数学上与旧行为等价（Android/Editor 无回归面），非白 tint 与 WebGL 路径不再双乘。
2. **`Assets/Scripts/M1IntroVideo.cs`（字幕几何）**：
   - `ApplyWebGlSubtitleLayoutWhenReady` 协程：等首帧布局 + `Canvas.ForceUpdateCanvases` 后应用，再晚一帧重算（幂等），覆盖 AspectRatioFitter 稳定时机。
   - 间距冲突处理：`y < webglSubtitleMinBottom` 时逐步缩小人物（0.70 → 下限 `webglIntroVideoMinScale=0.60`，每步 0.02 + ForceUpdateCanvases 重算），禁止把字幕压回人物；兜底仍贴 minBottom。
   - `CheckWebGlSubtitleRelayout()`：Update 中轮询屏幕宽高，分辨率/方向变化时幂等重算（MonoBehaviour 无 Rect 变化回调）。
3. **`Assets/Editor/DigitalHumanAtlasTool.cs`（稀疏末页裁切）**：每页按实际用到的行列取 POT 尺寸（`Mathf.NextPowerOfTwo(usedCols*cellW+Gutter) × (usedRows*cellH+Gutter)`），网格坐标不变，`BlitWithBleed`/`PageTexture` 参数化页宽高。重生成结果：idle 33帧 1 页、thinking 61帧 2 页、speaking 98帧 3 页；**speaking_2.png 2048² → 512²（3.4MB → 117KB），thinking_1.png → 2048×1024（899KB）**；meta/GUID 保持不变。
4. **`Assets/Scripts/M1DigitalHumanFramePlayer.cs`（切态内存，126 行）**：
   - 切态不再调用 `Resources.UnloadUnusedAssets()`（全局扫描）；改为 `_oldPages` 暂存 + 新页 `ApplyFrame()` 绑定后 `Resources.UnloadAsset` 逐页定向释放——无全局扫描、零空白帧、RawImage 上的兜底页不卸载。
   - 缺页：`WarnOnce` 每个缺失页只报一次明确错误，保留当前画面兜底不黑屏。
   - 切态打印页数/fps/帧数日志，供真机内存证据核对。
   **trellis-check PASS（低危项已修）**：播放中改分辨率会因 `ApplyWebGlSubtitleLayout` 末尾置 false + OnFrameReady 一次性守卫导致字幕不再显示——已改为重算后按 `_firstFrameShown` 恢复显示；兜底旧页极端泄漏路径（一页量级）与 evidence 行数误差已修正记录。
- 平台隔离不变：`M1DigitalHumanPresenter` 仅 WebGL 且配置存在时启用帧播放器；Android/Editor 保持 VideoPlayer 路径，视频素材未删。

## 验证结果

- 微信导出（wx-export-polish2.log，exit 0）：`DATA_FILE_SIZE=33,317,325`（仍 ≤40MB 门槛），`error CS`=0，`UnassignedReferenceException/m_AtlasTextures`=0，`Shader error`=0。
- 图集重建（exit 0；Temp/atlasgen2.log 被 Unity 会话清理未归档，页数/尺寸经 PNG IHDR 与 FrameAnimConfig 实测独立核验）：三状态页数与裁切尺寸如上；speaking 稀疏末页不再是 2048²。
- Android Debug APK：`Builds/Android/ProbeTeaching-debug.apk` 构建成功（196,065,369 bytes，2026-08-28 21:29，比基线 273MB 更小——本次未改 Android 平台导入设置，差异来自前一会话视频迁移等未提交改动首次进入构建，功能以真机回归为准）。
- M2/M3 Scene SHA256 与基线逐字节一致；`git diff --check` 通过。
- 字体子集、纹理覆盖等子任务 2 成果未被本任务破坏（同一次导出复测通过）。

## 待真机确认（归入子任务 4，不以开发者工具替代）

- 同关键帧帽子/肤色/工装三区颜色对比（无整体泛白）、非白 tint/淡入只作用一次。
- 16:9 与宽屏三段字幕居中、间距 ≥20px、无裁切。
- 待机/思考/讲解连续切换无空白/卡顿；引导结束 3 秒内常驻数字人出现；M1-M5 显示；长按问答联动。
- 真机内存峰值（切态日志 + Profiler）。
