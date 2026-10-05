# Design: 微信体验版与跨平台验收

## Release Gate

本任务是只消费已验收产物的集成门。启动前读取三个依赖子任务的Acceptance Criteria和检查报告；P0失败立即停止，不在验收任务中临时打补丁。

## Artifact Order

1. 保存M2/M3 SHA256和Git工作区清单。
2. Unity编译及WebGL/微信导出，解析BuildReport、`game.js`和包结构。
3. 构建Android Debug APK并安装到已授权设备。
4. 上传版本化CloudBase CDN数据，校验MD5/大小/HTTP状态。
5. 上传同版本小游戏代码，开发者工具预览通过后设置体验版。

CDN路径必须不可变，例如包含日期/版本或内容哈希；代码只引用本次已完成上传的数据。失败时继续保留上一体验版和旧CDN。

## Device Matrix

- Android APK：左右横屏、M1-M5、视频、QA、拖拽。
- 微信Android：冷/热启动、颜色、字幕、图集三态、QA、M1-M5。
- 微信iPhone：同上，额外记录内存警告、页面切换和后台恢复。

每项记录设备型号、OS、微信版本、构建版本、结果、截图/日志路径。无法获得iPhone或第二账号属于外部阻断，不能以开发者工具替代。

## Experience Boundary

只把开发版本设为体验版；不点击提审/发布。体验二维码和成员信息按项目内部资料保存，不扩大分发。若QA采用禁用降级，二维码说明必须明确，父任务问答验收保持未完成。

## Rollback

后台切回上一体验版；代码继续引用旧版本CDN。Android保留上一APK。任何回滚不覆盖旧资源目录。
