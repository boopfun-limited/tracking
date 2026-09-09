# tracking

Unity 休闲解谜游戏的埋点公共库（`com.gthbj.tracking`）。从 `gthbj/arrows` 抽出（来路见
`Docs~/DESIGN_ORIGIN_arrows_PRD_20260906_1854.md`），第二个消费者是 `gthbj/water_sort`。

**2026-09-09 由 `level-tracking` 改名而来**：包里从一开始就不只有关卡——整条 Firebase 传输通道
（就绪缓冲、Firebase 墙、`google-services.json` 构建步骤）都在里面，而它们跟关卡没有关系。
名字改成它实际是的东西，同时把三块切成**各自的程序集**，好让「只要关卡不要别的」在编译期成立。

## 包里有什么

| 程序集 | 目录 | 平台 | 内容 |
|---|---|---|---|
| `Tracking` | `Runtime/Core` | 全部 | `IAnalyticsBackend`（缝）/ `AnalyticsParameter`（三种值类型）、`BufferedAnalyticsBackend`（后端就绪前把**事件与用户属性排进同一条队列**按原序补报）、`NullAnalyticsBackend` |
| `Tracking.Firebase.Android` | `Runtime/Firebase` | Android | `FirebaseAnalyticsBackend.AttachWhenReady`——**全项目唯一碰 `Firebase.*` 的地方**（precompiled 引用 `Firebase.App.dll` / `Firebase.Analytics.dll` / `Firebase.TaskExtension.dll`；SDK 本体由游戏自己导入） |
| `Tracking.Identity.Android` | `Runtime/Identity` | Android | `AppSetIdUserProperty.SetWhenReady`——异步取 **App Set ID**（Google 对标 IDFV 的开发者范围标识符）并写成 GA4 用户属性 `app_set_id` + `app_set_id_scope` |
| `Tracking.Editor` | `Editor` | Editor | `FirebaseAndroidConfig.Regenerate(applicationId)`：`google-services.json` → androidlib |
| `LevelTracking` | `Runtime/Level` | 全部 | **`PlayClock`**（停表语义：理由位集合、停表期间读数冻结、后台段在真实帧结算）、**`LevelTracker`** 门面、`LevelTrackingEvents`（库发出的名字）、`LevelTrackingSchema`（方法 → 事件 → 标准参数的机器真源） |
| `LevelTracking.Tests.EditMode` | `Tests/Editor` | Editor | `LevelTrackerEmitsExactlyItsSchema`（表 == 行为）、`PlayClockTests`（三条不变量）、名字合规 |

🔴 **依赖是单向的**：`LevelTracking` → `Tracking`，`Tracking.Firebase.Android` → `Tracking`，`Tracking.Identity.Android` → `Tracking`。
关卡模块与将来的其它域模块（广告、IAP…）**互不引用**，各自只认核心。
只要关卡的游戏就只在 asmdef 里引 `LevelTracking`，编译期就把别的挡在外面。

## 接入（每个游戏）

1. `Packages/manifest.json`：`"com.gthbj.tracking": "https://github.com/gthbj/tracking.git#<40 位 commit SHA>"`；
   要跑包内测试再加 `"testables": ["com.gthbj.tracking"]`。
2. 导入 Firebase Unity SDK 的 `FirebaseAnalytics.unitypackage`（可删桌面 / iOS 原生库），`Assets/google-services.json` 入库。
3. 装配根：Android 上 `var buffer = new BufferedAnalyticsBackend(); FirebaseAnalyticsBackend.AttachWhenReady(buffer);`，
   要跨 App 归因再加一行 `AppSetIdUserProperty.SetWhenReady(buffer);`（**传 buffer 不传已 Attach 的后端**——
   属性与事件共用同一条有序队列，补报时才落在「当时那一刻」）。
   🔴 用 `AppSetIdUserProperty` 的游戏**必须自己声明** `com.google.android.gms:play-services-appset`
   （放进自己仓里某个 `Editor/` 下的 `*Dependencies.xml`）：**EDM4U 不扫 UPM 包目录**，
   放在本包里的依赖声明它看不见（2026-09-09 实测：解析器跑了、文件在 `Library/PackageCache/…/Editor/` 里、
   回执与 `mainTemplate.gradle` 里都没有它）。这些类今天已被 Firebase / AppsFlyer 传递带进 APK，
   所以不声明**也能跑**——正因如此才要显式声明：哪天上游把这条传递依赖丢了，症状是 JNI 抛
   `ClassNotFoundException`、被 catch 成一条 `LogWarning`、属性无声消失，没有任何门会红。
   其余平台 `NullAnalyticsBackend.Instance`。构建脚本在定下 application id **之后**调
   `FirebaseAndroidConfig.Regenerate(id)`；不想给某一档（QA / 管理员包）配 Firebase 就在**宿主**分支里跳过它，包不认识任何游戏的包名。
4. 关卡侧：`new LevelTracker(backend, new PlayClock(() => Time.unscaledTime), gameCommons)`；
   `OnApplicationPause(paused)` → `clock.OnApplicationPause`，`Update()` → `clock.Tick()`，装载 → `clock.Restart(已玩秒数)`，
   盖住棋盘的层各占一个理由位（从 `PlayClock.Backgrounded << 1` 起）。
5. 每个方法发什么看 `LevelTrackingSchema.Methods`。带可选参数的变体是**不同的方法名**
   （`LevelStartWithPrevFails` / `LevelEndWithFailCount`），不是重载——游戏侧文档门按方法名判。
6. 用户属性走 `backend.SetUserProperty(name, value)`（例：AppsFlyer ID → `appsflyer_id`，用来和 MMP 数据 join）。
   **GA4 上限比事件参数紧得多**：名 ≤24 字符、值 ≤36 字符、每媒体资源 25 个，超限静默丢弃。

## 名字归谁

库发出的事件名与参数名（`LevelTrackingEvents`）由库拥有，**改名 = 破坏性升版本**：GA4 的事件名一旦发出就进了
property 的字典，改名等于新开一个事件，历史不跟；自定义维度不回填。两游戏各自决定何时升 SHA。
游戏专属名字（平事件、专属参数、取值集合）留在各游戏自己的 `AnalyticsEvents.cs`。

🔴 **程序集与命名空间改名不算 breaking 语义**（消费方改几行 `using` 与 asmdef 引用即可），
**发出去的事件名改名才是**——这次改名一个事件名、参数名、类型名都没动。

## 加一个新的域模块（广告 / IAP…）

放 `Runtime/<域>/`，asmdef 只引用 `Tracking`，**不引用别的域模块**。判据仍是「机制进库，词汇留在游戏」：
事件名与标准参数、时序状态机、去重这类三个游戏同形的东西进库；placement 的具体名字、
何时展示的判定、以及**广告 / 支付 SDK 本体与它们的后端**留在游戏——那些是「怎么请求和展示」的适配器，
不是「埋点往哪儿送」的适配器，混进来这个包就变成中台了。

🔴 加事件前先查 **GA4 自动采集的同名事件**（如 AdMob 关联后 Firebase 自己会发 `ad_impression`）：
撞名会让两份数据混进同一个事件且字段对不齐，**而且是静默的**。

## 版本

只按 commit SHA 钉；`package.json` 的 `version` 是 UPM 的形式要求，不承载升级语义。
