# CLAUDE.md

**通用协议（东西放哪、git、什么该记）见 `AGENTS.md`，决策见 `DECISION_LOG.md`，本文件不重复**，
只写模块边界与踩过的坑。

`com.gthbj.tracking`：埋点公共库。消费者是 `gthbj/arrows` 与 `gthbj/water_sort`，都按 40 位 commit SHA 钉本仓。
**2026-09-09 由 `com.gthbj.level-tracking` 改名而来**（旧名下的传输层与关卡域混在一个程序集里，
名字只说了后者）。改名连同把内容切成四个程序集，依赖单向：域模块 → `Tracking`，域模块之间互不引用。

- **改名字（`LevelTrackingEvents`）= 破坏性升版本**：GA4 的事件名一旦发出就进了 property 的字典，改名等于新开事件、历史不跟；自定义维度不回填。改之前先问 owner。**程序集 / 命名空间改名不在此列**——消费方改几行 `using` 就行，线上数据不受影响。
- **`LevelTrackingSchema.Methods` 是机器真源**：一行一个发射方法，形状固定（`new MethodSpec(nameof(LevelTracker.X), LevelTrackingEvents.Y, playTime: bool, Params…)`）。两游戏的文档工具按 SHA 读这张表（路径 `Runtime/Level/LevelTrackingSchema.cs`），别在这里写表达式。加发射方法就加一行，`LevelTrackerEmitsExactlyItsSchema` 双向钉着。
- **App Set ID 那条链，连真机都不够——还得是 Play 装的包**：作用域由 Play 服务判定，侧载 / `adb install` 的包恒为 `SCOPE_APP`，只有经 Google Play 安装才是 `SCOPE_DEVELOPER`。所以 `app_set_id_scope` 这条属性**不是冗余**：没有它，测试包上「跨 App 的值没出现」和「这段代码根本没跑」分不出来。做跨 App join 时必须先按它过滤 `= "developer"`。
- **`Tracking.Firebase.Android` 只有 APK 能编译**（`includePlatforms: ["Android"]`，EditMode 不编译它）：改它之后的证据只能来自消费方的 Android 构建，别拿 EditMode 全绿当证据。判定一律留在 `Tracking`（全平台）里。
- **`.androidlib` 的 `build.gradle` 必须写 `minSdk`**：不写不是「跟随 app」而是 1，清单合并器会给**每个消费方**隐含 `READ_PHONE_STATE` 与读写外部存储三条危险权限，构建全绿、只在 Play 的权限列表上露出来（两个桥都中过，2026-09-19 修）。新桥照抄 `TrackingConsent.androidlib/build.gradle`，取值理由在它的注释里；验法是消费方构建后 merger 报告里没有 `targetSdkVersion < 4`。
- **加新的域模块**（广告 / IAP…）放 `Runtime/<域>/`，asmdef 只引 `Tracking`。边界判据是「机制进库，词汇留在游戏」：事件名与标准参数、时序状态机、去重进库；placement 名字、何时展示的判定、**以及广告 / 支付 SDK 本体与其后端**留在游戏。🔴 加事件前先查 GA4 自动采集里有没有同名的（AdMob 关联后 Firebase 自己发 `ad_impression`），撞名会静默混数据。
- 包不认识任何游戏的包名 / 路径以外的事：`FirebaseAndroidConfig` 只按传入的 application id 生成，跳不跳是宿主的分支。
- 测试在 `Tests/Editor`，由消费方的 `manifest.json` `testables` 带起来跑；本仓没有独立的 Unity 工程。
- 来路与设计取舍：`Docs~/DESIGN_ORIGIN_arrows_PRD_20260906_1854.md`（arrows 仓 `docs/prd/PRD_20260906_1854_…`）。

> 文档维护：Claude Opus 5（2026-09-09 改名 `level-tracking` → `tracking`，切四个程序集）；Claude Fable 5.1（2026-09-06 建仓）
