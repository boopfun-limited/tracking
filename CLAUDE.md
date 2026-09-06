# CLAUDE.md

`com.gthbj.level-tracking`：关卡埋点公共库。消费者是 `gthbj/arrows` 与 `gthbj/water_sort`，都按 40 位 commit SHA 钉本仓。

- **改名字（`LevelTrackingEvents`）= 破坏性升版本**：GA4 的事件名一旦发出就进了 property 的字典，改名等于新开事件、历史不跟；自定义维度不回填。改之前先问 owner。
- **`LevelTrackingSchema.Methods` 是机器真源**：一行一个发射方法，形状固定（`new MethodSpec(nameof(LevelTracker.X), LevelTrackingEvents.Y, playTime: bool, Params…)`）。两游戏的文档工具按 SHA 读这张表，别在这里写表达式。加发射方法就加一行，`LevelTrackerEmitsExactlyItsSchema` 双向钉着。
- **`LevelTracking.Android` 只有 APK 能编译**（`includePlatforms: ["Android"]`，EditMode 不编译它）：改它之后的证据只能来自消费方的 Android 构建，别拿 EditMode 全绿当证据。判定一律留在 `LevelTracking`（全平台）里。
- 包不认识任何游戏的包名 / 路径以外的事：`FirebaseAndroidConfig` 只按传入的 application id 生成，跳不跳是宿主的分支。
- 测试在 `Tests/Editor`，由消费方的 `manifest.json` `testables` 带起来跑；本仓没有独立的 Unity 工程。
- 来路与设计取舍：`Docs~/DESIGN_ORIGIN_arrows_PRD_20260906_1854.md`（arrows 仓 `docs/prd/PRD_20260906_1854_…`）。

> 文档维护：Claude Fable 5.1（2026-09-06 建仓）
