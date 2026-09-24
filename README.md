# tracking

Unity 休闲解谜游戏的埋点公共库（`com.gthbj.tracking`）。从 `boopfun-limited/arrows` 抽出（来路见
`Docs~/DESIGN_ORIGIN_arrows_PRD_20260906_1854.md`），第二个消费者是 `boopfun-limited/water_sort`。

**2026-09-09 由 `level-tracking` 改名而来**：包里从一开始就不只有关卡——整条 Firebase 传输通道
（就绪缓冲、Firebase 墙、`google-services.json` 构建步骤）都在里面，而它们跟关卡没有关系。
名字改成它实际是的东西，同时把三块切成**各自的程序集**，好让「只要关卡不要别的」在编译期成立。

## 包里有什么

| 程序集 | 目录 | 平台 | 内容 |
|---|---|---|---|
| `Tracking` | `Runtime/Core` | 全部 | `IAnalyticsBackend`（缝）/ `AnalyticsParameter`（三种值类型）、`BufferedAnalyticsBackend`（后端就绪前把**事件与用户属性排进同一条队列**按原序补报）、`NullAnalyticsBackend` |
| `Tracking.Firebase.Android` | `Runtime/Firebase` | Android | `FirebaseAnalyticsBackend.AttachWhenReady`——**全项目唯一碰 `Firebase.*` 的地方**（Java 侧的同意值另经 `TrackingFirebase.androidlib` 桥写；precompiled 引用 `Firebase.App.dll` / `Firebase.Analytics.dll` / `Firebase.TaskExtension.dll`；SDK 本体由游戏自己导入） |
| `Tracking.Identity.Android` | `Runtime/Identity` | Android | `AppSetIdUserProperty.SetWhenReady`——异步取 **App Set ID**（Google 对标 IDFV 的开发者范围标识符）并写成 GA4 用户属性 `app_set_id` + `app_set_id_scope` |
| `Tracking.Consent` | `Runtime/Consent` | 全部 | `ConsentFlow`（首启同意状态机：两前置条件取较晚者、REQUIRED 才弹表单、收尾无论成败都回调、请求失败退避重试）、`IConsentPlatform`（缝）、`ConsentEvents`、`UsPrivacy`、`NullConsentPlatform`、`TermsGate`（首启条款弹窗的**判定**：弹不弹、两条事件、同意落盘、放行流程——**界面留在游戏**）+ `ITermsStore`（缝）/ `PlayerPrefsTermsStore`。规格见 `Docs~/PRD_20260911_1509_首启同意流程SDK照oakever.md` |
| `Tracking.Consent.Android` | `Runtime/Consent/Android` | Android | `UmpConsentPlatform`——Google UMP 的 JNI 接线，接包内 Java 桥 `TrackingConsent.androidlib`（**唯一碰 `com.google.android.ump.*` 的地方**） |
| `Tracking.Editor` | `Editor` | Editor | `FirebaseAndroidConfig.Regenerate(applicationId)`：`google-services.json` → androidlib |
| `LevelTracking` | `Runtime/Level` | 全部 | **`PlayClock`**（停表语义：理由位集合、停表期间读数冻结、后台段在真实帧结算）、**`LevelTracker`** 门面、`LevelTrackingEvents`（库发出的名字）、`LevelTrackingSchema`（方法 → 事件 → 标准参数的机器真源） |
| `LevelTracking.Tests.EditMode` | `Tests/Editor` | Editor | `LevelTrackerEmitsExactlyItsSchema`（表 == 行为）、`PlayClockTests`（三条不变量）、名字合规 |

🔴 **依赖是单向的**：`LevelTracking` → `Tracking`，`Tracking.Firebase.Android` → `Tracking`，`Tracking.Identity.Android` → `Tracking`。
关卡模块与将来的其它域模块（广告、IAP…）**互不引用**，各自只认核心。
只要关卡的游戏就只在 asmdef 里引 `LevelTracking`，编译期就把别的挡在外面。

## 接入（每个游戏）

1. `Packages/manifest.json`：`"com.gthbj.tracking": "https://github.com/boopfun-limited/tracking.git#<40 位 commit SHA>"`；
   要跑包内测试再加 `"testables": ["com.gthbj.tracking"]`。
   本仓已从 `gthbj` 迁入 `boopfun-limited` 组织（旧地址由 GitHub 重定向）：现行文档与配置一律写 `boopfun-limited/<仓>`；
   `DECISION_LOG.md` 与 `Docs~/` 里的 `gthbj/<仓>` 是当时的记录，不改写；`com.gthbj.*` 是包名不是地址，不随迁移改。
2. 导入 Firebase Unity SDK 的 `FirebaseAnalytics.unitypackage`（可删桌面 / iOS 原生库），`Assets/google-services.json` 入库。
3. 装配根：Android 上 `var buffer = new BufferedAnalyticsBackend(); FirebaseAnalyticsBackend.AttachWhenReady(buffer);`，
   要跨 App 归因再加一行 `AppSetIdUserProperty.SetWhenReady(buffer);`（**传 buffer 不传已 Attach 的后端**——
   属性与事件共用同一条有序队列，补报时才落在「当时那一刻」）。
   🔴 **`AttachWhenReady` 一进来就同步开采集、把四项同意写死 GRANTED**（照 oakever 在 `onCreate` 里写的时点，D-20260911-01 / D-20260916-01）；
   玩家在 UMP 里的真实选择由 UMP 回话后自己经反射推给 Firebase，写死的值只管「本次冷启到 UMP 回话」这一段。
   写法是包内 Java 桥 `TrackingFirebase.androidlib`（keep 规则随模块走），**不等** C# 侧的依赖检查——那要一两秒，
   而老玩家的同意请求在首个场景头几帧就发（arrows 真机实测进程起来约 0.9 秒、UMP 再约 0.6 秒回话），晚写会把拒绝盖回一整场。
   唯一前提：`AttachWhenReady` 在游戏发同意信号之前调——放在装配根里就天然满足。建完正式包在 dex 里验
   `Lcom/gthbj/tracking/firebase/FirebaseConsentBridge;` 还在。
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

## 条款弹窗（首启）

**界面留在游戏**，包只管判定（`TermsGate`，D-20260920-02）。规格是 PRD §5.2：全球新装都弹、
一颗同意按钮、无拒绝、不点不放行；挡住什么由游戏自己定（挡整条冷启协程还是只挡点击）。

```csharp
// 装配根；backend 是上面第 3 步那个，consentFlow 不接广告的游戏传 null
var gate = new TermsGate(new PlayerPrefsTermsStore("<游戏自己的键>"), backend, consentFlow);
if (gate.ShowIfNeeded(BuildMyFullScreenDialog))
{
    // 弹了：把自己的开屏挡住；那颗唯一的按钮上接 gate.Accept
}
```

🔴 **已经上线过条款弹窗的游戏必须把自己原来那个键传进来**（arrows 是存档字段 `legalConsentAccepted`、
water_sort 是 `WaterSort.LegalConsent.v1`、arrows-3d 是 `arrows3d.legal.accepted.v1`、
boopdoku 是 `boopdoku.Legal.ConsentAccepted`）；落点不是 PlayerPrefs 的（arrows 在存档里）自己实现
`ITermsStore` 那两个成员，一共十来行。换成包自带的新键 = **所有同意过的老玩家冷启再吃一道全屏闸**，
而且没有任何门会红。

🔴 **老玩家那条路也要走 `ShowIfNeeded`**：不弹归不弹，`ConsentFlow` 的「条款已同意」信号照样得发，
否则 UMP 请求永远不发、欧洲整场没广告、一声不响——这是这个类存在的首要理由，不是顺手加的。

两条事件（`dlg_show_law` / `btn_click_law`）由 `TermsGate` 发。**在它之前包里只有名字、没有发射点**：
GA4 里钉上本 SHA 之前一条都没有，那段空白不是「没人看弹窗」。

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

### 🔴 验欧洲那条路：必须用 `SetDebugGeography`，挂 VPN 不算

同意表单那三个桥方法（`loadConsentForm` / `showConsentForm` / `showPrivacyOptionsForm`）只在
**服务端判定为受管辖区**时才会被走到。**挂欧洲节点不足以触发**——2026-09-11 在 water_sort 实测：

- 手机在法国 OVH 节点上，**应用自己 uid 的 socket** 出口也确认是 `5.135.5.129`
  （`adb shell 'su -c "su <uid> -c \"curl -s -H Host:ifconfig.me http://34.160.111.145/ip\""'`；
  别用域名，uid 切换后 DNS 解析不到，那是 `su` 的副作用不是应用的行为）；
- UMP 仍然返回 `consent_status=1`（NOT_REQUIRED）、`stored_info` 为**空集合**、
  `is_pub_misconfigured=false`（读
  `/data/data/<pkg>/shared_prefs/__GOOGLE_FUNDING_CHOICE_SDK_INTERNAL__.xml`）。

数据中心 IP 拿不到受管辖区判定。正路是 UMP 自己的测试通道：

```csharp
#if ADMIN   // 或本仓等价的构建期 define——不要靠人记得删
Tracking.Consent.Android.UmpConsentPlatform.SetDebugGeography(
    "<logcat 里那一串>", Tracking.Consent.Android.DebugGeography.Eea);
#endif
var consent = new ConsentFlow(new UmpConsentPlatform(AdMobApplicationId), …);
```

那一串**不用自己算 MD5**，UMP 第一次跑完就打在 logcat 里：

```bash
adb logcat -d | grep 'addTestDeviceHashedId'
```

它是**那台机器**的标识，属于设备数据，**不要写进仓库**——调用点从环境变量 / 构建参数拿，
或者就地临时改、验完撤掉。哈希对不上时整个调试设置**静默无效**
（`ConsentDebugSettings.Builder.build()` 的字节码：只有「列表含本机哈希」或 `setForceTesting(true)`
能把 `isTestDevice` 置真），症状与「Google 就是判非欧洲」一模一样，所以设完要先确认
`isConsentFormRequired` 真的翻了。

## 接口加成员时

`IAnalyticsBackend` 每加一个成员，**同一提交**里要一起改：`NullAnalyticsBackend`、`BufferedAnalyticsBackend`（含 `PendingCall`
回放）、Firebase 适配器，以及 **`Tests/Editor` 里每一个实现它的假件**。消费仓会把本包的 Editor 测试一起编译，
假件漏一个，两个游戏仓的 EditMode 就在 `Library/PackageCache/…` 里报 CS0535——而本仓自己没有能编译的地方，看不见。

## 名字归谁

库发出的事件名与参数名（`LevelTrackingEvents`）由库拥有，**改名 = 破坏性升版本**：GA4 的事件名一旦发出就进了
property 的字典，改名等于新开一个事件，历史不跟；自定义维度不回填。各消费仓各自决定何时升 SHA。
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


## Ads：广告全流程埋点

引用 `Tracking.Ads` 程序集与命名空间，构造 `AdTracker(IAnalyticsBackend)`。不依赖 MAX、Firebase 或关卡模块，SDK 回调与广告策略由宿主接线。

```csharp
var ads = new AdTracker(analytics);
var load = ads.Request(AdEvents.Formats.Rewarded, unitId); // 紧邻 SDK 加载调用
load.Fill(true); // 加载回调；失败用 Fill(false, error parameters)
var flow = ads.Opportunity(AdEvents.Formats.Rewarded, "revive",
    AnalyticsParameter.Of("level_number", levelNumber));
flow.Decision(AdEvents.Results.Show, "ad_ready");
flow.ShowRequest(load.RequestId); // 紧邻 SDK 展示调用
flow.ShowResult(true); // 展示成功回调
flow.RewardEarned(); // SDK 奖励回调
flow.Close(); // SDK 关闭回调
flow.RewardResult(true, "granted"); // 游戏实际发奖后
```

| 事件 | 发射入口 | 口径 |
|---|---|---|
| `ad_opportunity` | `AdTracker.Opportunity` | 真实业务操作产生机会；不从 IsReady / UI 刷新上报 |
| `ad_decision` | `AdFlow.Decision` | `result=show/free/skip`，`reason` 由宿主词汇定义 |
| `ad_request` | `AdTracker.Request` | 显式 SDK 加载请求 |
| `ad_fill` | `AdLoad.Fill` / `AdTracker.AutomaticFill` | `result=success/failure`；失败附 `error_code` / `reason` |
| `ad_show_request` | `AdFlow.ShowRequest` | 实际展示调用；携带对应预加载的 `request_id` |
| `ad_show_result` | `AdFlow.ShowResult` | `result=success/failure`；表示展示结果，不是收入 |
| `ad_clicked` | `AdFlow.Click` / `AdTracker.Click` | SDK 点击回调；避开 Firebase 保留名 `ad_click` |
| `ad_closed` | `AdFlow.Close` | SDK 关闭回调；`reward_earned=0/1` 是关闭时已知状态 |
| `ad_reward_earned` | `AdFlow.RewardEarned` | SDK 确认奖励资格 |
| `ad_reward_result` | `AdFlow.RewardResult` | `result=granted/not_granted`；游戏权益实际交付结果 |
| `ad_impression` | `AdTracker.Impression` | 展示级收入；零收入有效，负值 / NaN / Infinity 不发 |

`AdLoad` 每次请求生成 `request_id`，`AdFlow` 每次业务机会生成 `ad_flow_id`，不使用设备标识。
业务上下文复制到 flow，不在异步回调中重新读取关号。一次流程的 decision、show request/result、close、reward earned/result 各最多发一次；点击可多次，收入由 SDK 每次收入回调分别提交，不按业务 flow 去重（横幅有多次刷新收入）。奖励资格与关闭不强制顺序，未获得资格不报告奖励到账。

`duration_ms`：fill 为请求到结果；show result 为展示调用到结果；closed 为 SDK 展示成功到关闭的经过时间，**不是视频播放时长**。未观察到展示成功时不编造关闭时长。缺失回调保留未闭合，不推断失败。
横幅自动刷新只有回调时用 `AutomaticFill`，标 `load_origin=sdk_auto`，不虚构 `request_id` 或耗时；显式加载标 `load_origin=explicit`。客户端显式加载成功率按同一批 `request_id` 的 success / request 计算，不把横幅自动刷新混进分子，也不等同 MAX 网络竞价填充率。

错误参数传数值码与归一化原因，不发送可能含请求数据的原始 SDK error message。`request_id` / `ad_flow_id` 用于原始数据关联，不注册为高基数 GA4 自定义维度。

Firebase 保留名依据：https://firebase.google.com/docs/reference/kotlin/com/google/firebase/analytics/FirebaseAnalytics.Event 。公共库使用 `ad_clicked`；收入 `ad_impression` 是 Firebase 支持的标准事件。

> 文档维护：Claude Opus 5（2026-09-20，条款弹窗判定进包）；GPT-6（2026-09-19，广告模块接入说明）
