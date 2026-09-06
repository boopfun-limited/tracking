# level-tracking

Unity 休闲解谜游戏的关卡埋点公共库（`com.gthbj.level-tracking`）。从 `gthbj/arrows` 抽出（来路见
`Docs~/DESIGN_ORIGIN_arrows_PRD_20260906_1854.md`），第二个消费者是 `gthbj/water_sort`。

## 包里有什么

| 程序集 | 平台 | 内容 |
|---|---|---|
| `LevelTracking` | 全部 | `IAnalyticsBackend` / `AnalyticsParameter`（三种值类型）、`BufferedAnalyticsBackend`（就绪前按序缓存）、`NullAnalyticsBackend`、**`PlayClock`**（停表语义：理由位集合、停表期间读数冻结、后台段在真实帧结算）、**`LevelTracker`** 门面、`LevelTrackingEvents`（库发出的名字）、`LevelTrackingSchema`（方法 → 事件 → 标准参数的机器真源） |
| `LevelTracking.Android` | Android | `FirebaseAnalyticsBackend.AttachWhenReady`（precompiled 引用 `Firebase.App.dll` / `Firebase.Analytics.dll` / `Firebase.TaskExtension.dll`——**SDK 本体由游戏自己导入**） |
| `LevelTracking.Editor` | Editor | `FirebaseAndroidConfig.Regenerate(applicationId)`：`google-services.json` → androidlib，显式用 `python3` 调 Google 的脚本 |
| `LevelTracking.Tests.EditMode` | Editor | 包内三道门：`LevelTrackerEmitsExactlyItsSchema`（表 == 行为）、`PlayClockTests`（三条不变量）、名字合规 |

## 接入（每个游戏）

1. `Packages/manifest.json`：`"com.gthbj.level-tracking": "https://github.com/gthbj/level-tracking.git#<40 位 commit SHA>"`；
   要跑包内测试再加 `"testables": ["com.gthbj.level-tracking"]`。
2. 导入 Firebase Unity SDK 的 `FirebaseAnalytics.unitypackage`（可删桌面 / iOS 原生库），`Assets/google-services.json` 入库。
3. 装配根：Android 上 `var buffer = new BufferedAnalyticsBackend(); FirebaseAnalyticsBackend.AttachWhenReady(buffer);`，
   其余平台 `NullAnalyticsBackend.Instance`。构建脚本在定下 application id **之后**调
   `FirebaseAndroidConfig.Regenerate(id)`；不想给某一档（QA / 管理员包）配 Firebase 就在**宿主**分支里跳过它，包不认识任何游戏的包名。
4. 关卡侧：`new LevelTracker(backend, new PlayClock(() => Time.unscaledTime), gameCommons)`；
   `OnApplicationPause(paused)` → `clock.OnApplicationPause`，`Update()` → `clock.Tick()`，装载 → `clock.Restart(已玩秒数)`，
   盖住棋盘的层各占一个理由位（从 `PlayClock.Backgrounded << 1` 起）。
5. 每个方法发什么看 `LevelTrackingSchema.Methods`。带可选参数的变体是**不同的方法名**
   （`LevelStartWithPrevFails` / `LevelEndWithFailCount`），不是重载——游戏侧文档门按方法名判。

## 名字归谁

库发出的事件名与参数名（`LevelTrackingEvents`）由库拥有，**改名 = 破坏性升版本**：GA4 的事件名一旦发出就进了
property 的字典，改名等于新开一个事件，历史不跟；自定义维度不回填。两游戏各自决定何时升 SHA。
游戏专属名字（平事件、专属参数、取值集合）留在各游戏自己的 `AnalyticsEvents.cs`。

## 版本

只按 commit SHA 钉；`package.json` 的 `version` 是 UPM 的形式要求，不承载升级语义。
