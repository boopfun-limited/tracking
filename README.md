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
| `Tracking.Consent` | `Runtime/Consent` | 全部 | `ConsentFlow`（首启同意状态机：两前置条件取较晚者、REQUIRED 才弹表单、收尾无论成败都回调、请求失败退避重试）、`IConsentPlatform`（缝）、`ConsentEvents`、`UsPrivacy`、`NullConsentPlatform`。规格见 `Docs~/PRD_20260911_1509_首启同意流程SDK照oakever.md` |
| `Tracking.Consent.Android` | `Runtime/Consent/Android` | Android | `UmpConsentPlatform`——Google UMP 的 JNI 接线，接包内 Java 桥 `TrackingConsent.androidlib`（**唯一碰 `com.google.android.ump.*` 的地方**） |
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
   🔴 **还必须加 R8 keep 规则**（`useCustomProguardFile: 1` + `Assets/Plugins/Android/proguard-user.txt`）：
   本模块按**字符串类名**过 JNI，而正式包开 `AndroidMinifyRelease` 时 R8 会给**没有 consumer
   proguard 规则的库**改名——`play-services-appset` 正是这种。2026-09-09 在 arrows 正式包 dex 上实测：
   `Lcom/google/android/gms/appset/AppSet;` 命中 **0 次**，而同一份 dex 里 `Lcom/appsflyer/AppsFlyerLib;`、
   `Lcom/google/firebase/analytics/FirebaseAnalytics;` 都在。缺规则的症状与上一条一模一样、同样没有门会红。

   ```
   -keep class com.google.android.gms.appset.AppSet { *; }
   -keep interface com.google.android.gms.appset.AppSetIdClient { *; }
   -keep class com.google.android.gms.appset.AppSetIdInfo { *; }
   -keep interface com.google.android.gms.tasks.OnSuccessListener { *; }
   ```

   这两样都放不进本包：EDM4U 不扫 UPM 包目录，而 Unity 的自定义 proguard 文件是**固定路径**。
   **建完正式包务必去 dex 里验一次那四个描述符还在**——构建绿证明不了这一面。
   其余平台 `NullAnalyticsBackend.Instance`。构建脚本在定下 application id **之后**调
   `FirebaseAndroidConfig.Regenerate(id)`；不想给某一档（QA / 管理员包）配 Firebase 就在**宿主**分支里跳过它，包不认识任何游戏的包名。
4. 关卡侧：`new LevelTracker(backend, new PlayClock(() => Time.unscaledTime), gameCommons)`；
   `OnApplicationPause(paused)` → `clock.OnApplicationPause`，`Update()` → `clock.Tick()`，装载 → `clock.Restart(已玩秒数)`，
   盖住棋盘的层各占一个理由位（从 `PlayClock.Backgrounded << 1` 起）。
5. 每个方法发什么看 `LevelTrackingSchema.Methods`。带可选参数的变体是**不同的方法名**
   （`LevelStartWithPrevFails` / `LevelEndWithFailCount`），不是重载——游戏侧文档门按方法名判。
6. 用户属性走 `backend.SetUserProperty(name, value)`（例：AppsFlyer ID → `appsflyer_id`，用来和 MMP 数据 join）。
   **GA4 上限比事件参数紧得多**：名 ≤24 字符、值 ≤36 字符、每媒体资源 25 个，超限静默丢弃。
7. GA4 `user_id` **包自动设，游戏不用写一行**：`AttachWhenReady` 在接上 Firebase 之前把 `InstallId.GetOrCreate()`
   （本地生成的 GUID，存 PlayerPrefs 键 `gthbj.tracking.install_id`）设成 `user_id`，所以回放的每一条事件都带它。
   BigQuery 里落在顶层列 `user_id`（与 `user_pseudo_id` 并列），`users_*` 日表按它一人一行；备份还原把 PlayerPrefs
   带回来时它也回来，而 `user_pseudo_id` / AFID / App Set ID 都不会（2026-09-09 真机实测）——事件历史因此跟着「这份存档」。
   首会话里早于它的 `first_open` / `session_start` 不带，要在 BigQuery 里按 `user_pseudo_id` 回填。
   它不是任何 SDK 的 ID，也识别不到个人；🔴 **游戏侧不要再自己设 user_id**（接口上没有这个方法，就是为了没法设）。

## 同意模块的 Android 侧

判定（什么时候请求、失败怎么办、什么时候放行广告）在全平台的 `Tracking.Consent` 里，EditMode 可测；
`Tracking.Consent.Android` **只有接线**——它 `includePlatforms: ["Android"]`，
**EditMode 一行都编译不到**，写进去的任何判断快车道全绿也证明不了它对（同 `Tracking.Identity.Android`）。

UMP 调用走包内的 Java 桥 `Runtime/Consent/Android/TrackingConsent.androidlib`。

🔴 **桥是为 R8 存在的，不是为了好看。** UMP 的 aar 自带 `proguard.txt` 只保 proto 字段，
**没有一条保它的公开 API 类名**。

🔴 **别照着「现在没被改名」下结论**（2026-09-11 在 arrows 正式包 `mapping.txt` 实测）：
`com.google.android.ump.*` 今天确实原名保留，但保它的是 **GoogleMobileAds Unity 插件**的
`googlemobileads-unity.aar` 里那条 `-keep public class com.google.android.ump.** { public *; }`
——R8 的 `configuration.txt` 里它是**唯一来源**。而那个插件正是「AdMob 换 MAX」要移除的那个，
**water_sort 根本没有它**。所以「C# 直接按字符串名字调 UMP」这条路
**在 water_sort 上今天就是坏的、在 arrows 上从换 MAX 那天起坏**，两种都静默——
和 `AppSetIdUserProperty` 那次同一个形态。写成 Java 就与那条规则无关：那些是真引用，
R8 改名时一起改（同一份构建里的佐证：本桥的匿名内部类没被 keep，`ConsentBridge$1 -> z4.c`，桥照样工作）。

**消费方要做的三件事**：

1. **给 `new UmpConsentPlatform(admobAppId)` 传自己的 AdMob 应用 id**（`ca-app-pub-…~…`）。

   🔴 **这是 UMP 的硬要求，不是「用 AdMob 变现才需要」**：UMP 4.0.0 的 `consent_sdk.zzp.zza()`
   先读 `setAdMobAppId` 的值，为空才回落读 manifest 的 `com.google.android.gms.ads.APPLICATION_ID`，
   **两条都没有就直接 `throw zzg(3, "The UMP SDK requires a valid application ID…")`**，
   请求失败、欧洲整场没广告。用 Unity Ads / MAX 变现的游戏一样要有这个 id ——
   它只是 UMP 用来认「该显示哪个 app 的同意消息」的钥匙。

   参数**故意必填、没有默认值**：manifest 那条回落不是游戏自己的东西。arrows 今天有那一行，
   是 **GoogleMobileAds 插件**带进来的——「AdMob 换 MAX」移除插件的那天它会跟着消失，
   而症状是欧洲静默无广告。显式写一次，这个依赖就不会在别人删插件时无声断掉。

   它**不是密钥**（每个 APK 的 manifest 里都带着，是公开标识符），入库即可。
   还要去 **AdMob 控制台**给该应用建并发布 EEA 同意消息，否则表单加载不出来。



2. **声明 UMP 依赖**（放进自己仓里某个 `Editor/` 下的 `*Dependencies.xml`）：

   ```xml
   <androidPackage spec="com.google.android.ump:user-messaging-platform:4.0.0" />
   ```

   理由同 `play-services-appset` 那条——**EDM4U 不扫 UPM 包目录**。arrows 已经有这一行
   （`Assets/GoogleMobileAds/Editor/GoogleUmpDependencies.xml`，GoogleMobileAds 插件带来的）；
   哪天那个插件随「AdMob 换 MAX」被移除，**这一行要留下**，否则桥编译得过、运行期整条同意流程静默失效。

3. **每帧调一次 `consentFlow.Tick()`**（与 `PlayClock.Tick` 同一处）。跨线程回调靠它交付：
   UMP 的监听器落在 Android 主线程上，不是 Unity 主线程。不调的症状是**表单永远不弹**。

**R8 keep 规则不用游戏管**——`.androidlib` 用 `consumerProguardFiles` 把规则随模块传给 app 的 R8
（AppsFlyer / Firebase 保住名字靠的就是这个机制）。这是它与 `AppSetIdUserProperty` 那条的区别：
那边要 keep 的是**别人家**的库，只能写进游戏的 `proguard-user.txt`；这边要 keep 的是**我们自己的**桥。

🔴 **`TrackingConsent.androidlib/build.gradle` 必须手写，不能留给 Unity 生成**：
Unity 给没有 `build.gradle` 的 `.androidlib` 生成的模板里 `//java.srcDirs = ['src']` 是**注释掉的**
——它根本不编译 Java 源码，而构建照样全绿。

**验收（每次改桥或升 UMP 都要重跑一遍）**：建**正式包**（minify 开），去 dex 里验描述符还在：

```bash
unzip -o <apk> 'classes*.dex' -d /tmp/dex && \
  for d in /tmp/dex/classes*.dex; do strings -a "$d" | grep -c 'Lcom/gthbj/tracking/consent/ConsentBridge;'; done
```

命中 0 次就是 keep 规则没生效 / 模块没进构建。构建绿、安装成功、EditMode 全绿都证明不了这一面。

## 接口加成员时

`IAnalyticsBackend` 每加一个成员，**同一提交**里要一起改：`NullAnalyticsBackend`、`BufferedAnalyticsBackend`（含 `PendingCall`
回放）、Firebase 适配器，以及 **`Tests/Editor` 里每一个实现它的假件**。消费仓会把本包的 Editor 测试一起编译，
假件漏一个，两个游戏仓的 EditMode 就在 `Library/PackageCache/…` 里报 CS0535——而本仓自己没有能编译的地方，看不见。

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
