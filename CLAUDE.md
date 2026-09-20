# CLAUDE.md

**通用协议（东西放哪、git、什么该记）见 `AGENTS.md`，决策见 `DECISION_LOG.md`，本文件不重复**，
只写模块边界与踩过的坑。

`com.gthbj.tracking`：埋点公共库。消费者是 `gthbj/arrows`、`gthbj/water_sort` 与 `gthbj/arrows-3d`（2026-09-19 接入），都按 40 位 commit SHA 钉本仓。
**2026-09-09 由 `com.gthbj.level-tracking` 改名而来**（旧名下的传输层与关卡域混在一个程序集里，
名字只说了后者）。改名连同把内容切成四个程序集，依赖单向：域模块 → `Tracking`，域模块之间互不引用。

- **改名字（`LevelTrackingEvents`）= 破坏性升版本**：GA4 的事件名一旦发出就进了 property 的字典，改名等于新开事件、历史不跟；自定义维度不回填。改之前先问 owner。**程序集 / 命名空间改名不在此列**——消费方改几行 `using` 就行，线上数据不受影响。
- **`LevelTrackingSchema.Methods` 是机器真源**：一行一个发射方法，形状固定（`new MethodSpec(nameof(LevelTracker.X), LevelTrackingEvents.Y, playTime: bool, Params…)`）。arrows 与 water_sort 的文档工具（各自的 `tools/analytics_doc.py`）按 SHA 读这张表（路径 `Runtime/Level/LevelTrackingSchema.cs`），别在这里写表达式。加发射方法就加一行，`LevelTrackerEmitsExactlyItsSchema` 双向钉着。
- **App Set ID 那条链，连真机都不够——还得是 Play 装的包**：作用域由 Play 服务判定，侧载 / `adb install` 的包恒为 `SCOPE_APP`，只有经 Google Play 安装才是 `SCOPE_DEVELOPER`。所以 `app_set_id_scope` 这条属性**不是冗余**：没有它，测试包上「跨 App 的值没出现」和「这段代码根本没跑」分不出来。做跨 App join 时必须先按它过滤 `= "developer"`。
- **`Tracking.Firebase.Android` 只有 APK 能编译**（`includePlatforms: ["Android"]`，EditMode 不编译它）：改它之后的证据只能来自消费方的 Android 构建，别拿 EditMode 全绿当证据。判定一律留在 `Tracking`（全平台）里。
- **`.androidlib` 的 `build.gradle` 必须写 `minSdk`**：不写不是「跟随 app」而是 1，清单合并器会给**每个消费方**隐含 `READ_PHONE_STATE` 与读写外部存储三条危险权限，构建全绿、只在 Play 的权限列表上露出来（两个桥都中过，2026-09-19 修）。新桥照抄 `TrackingConsent.androidlib/build.gradle`，取值理由在它的注释里；验法是消费方构建后 merger 报告里没有 `targetSdkVersion < 4`。🔴 **`.androidlib` 不受 asmdef 引用约束**：「只引 `LevelTracking` 就把别的挡在编译期外」只对 C# 成立，`.androidlib` 钉了包就进 APK，消费方一行不调桥也一样（arrows-3d 2026-09-19 实证：没接 Firebase / UMP，包照样带着那三条权限）。
- **加新的域模块**（广告 / IAP…）放 `Runtime/<域>/`，asmdef 只引 `Tracking`。边界判据是「机制进库，词汇留在游戏」：事件名与标准参数、时序状态机、去重进库；placement 名字、何时展示的判定、**以及广告 / 支付 SDK 本体与其后端**留在游戏。🔴 加事件前先查 GA4 自动采集里有没有同名的（AdMob 关联后 Firebase 自己发 `ad_impression`），撞名会静默混数据。
- 包不认识任何游戏的包名 / 路径以外的事：`FirebaseAndroidConfig` 只按传入的 application id 生成，跳不跳是宿主的分支。
- 测试在 `Tests/Editor`，由消费方的 `manifest.json` `testables` 带起来跑；本仓没有独立的 Unity 工程。
  **但不必为了跑一遍就去开消费方工程**：全平台程序集（`Tracking` / `Tracking.Consent` / `Tracking.Ads` /
  `LevelTracking`）是纯 .NET，用 Unity 自带的 Roslyn 直接编译、拿 Unity 自带的 NUnit 反射跑 `[Test]` 即可，
  秒级。直推 `main` 成了默认（D-20260920-01）之后这就是推之前唯一那道门，别省。
  ```bash
  U=/Applications/Unity/Hub/Editor/*/Unity.app/Contents        # 装了哪个版本都行
  R=$(ls -d $U/Resources/Scripting/NetCoreRuntime/shared/Microsoft.NETCore.App/*)
  $U/Resources/Scripting/NetCoreRuntime/dotnet $U/Resources/Scripting/DotNetSdkRoslyn/csc.dll \
    -target:exe -nostdlib $(ls $R/*.dll | sed 's/^/-r:/') \
    -r:$U/Resources/PackageManager/BuiltInPackages/com.unity.ext.nunit/net40/unity-custom/nunit.framework.dll \
    <要编的 .cs> <一个反射调 [Test] 的 Main>
  ```
  🔴 **`-r:` 必须是 `$R/*.dll` 整份**，少了 `mscorlib.dll` 那个门面就报「类型 Attribute 在未引用的程序集中定义」——
  Unity 那份 NUnit 是 net40 的。要引 `UnityEngine` 的文件（`InstallId`、`PlayerPrefsTermsStore`）**编得过、跑不了**
  （没有 Player Loop），只能拿它验编译；`*.Android` 连编都编不了，见上一条。跑绿之后**再做一次变异检查**：
  把被测的那行判定摘掉、重跑，必须恰好红对应那条用例——不然绿的可能只是「用例没碰到它」。
- 来路与设计取舍：`Docs~/DESIGN_ORIGIN_arrows_PRD_20260906_1854.md`（arrows 仓 `docs/prd/PRD_20260906_1854_…`）。

> 文档维护：Claude Opus 5（2026-09-20 补「不开消费方工程也能跑全平台程序集的测试」）；Claude Opus 5（2026-09-09 改名 `level-tracking` → `tracking`，切四个程序集）；Claude Fable 5.1（2026-09-06 建仓）
