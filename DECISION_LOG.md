# 决策日志

本包的持久决策，**最新在上**。只记「不可逆取舍」与「推翻过前一版口径」的那些——
改名、破坏性接口变更、模块边界、以及会约束将来工作的口径。

不记：怎么接入（`README.md`）、边界判据与陷阱（`CLAUDE.md`）、某次实现细节（提交信息与 PR）。
**同一句话不写两遍**：这里只写「定了什么、为什么、代价」，落地细节指向那两个文件。

格式：`## D-YYYYMMDD-NN 标题` + 日期 / 状态 / 拍板人 / 模型，正文写**决策、理由、代价**三段。
状态取 `active` / `amended` / `superseded`。

---

## D-20260925-01 每日挑战埋点进包（`Tracking.DailyChallenge`）：三个事件 + 盖在关卡事件上的两个参数

日期：2026-09-25　状态：active　拍板人：owner（「每日挑战的埋点可以考虑进 tracking，之后很多游戏都会加每日挑战」「按这个设计做吧」）　模型：Claude Opus 5.5

**决策**：新增域模块 `Tracking.DailyChallenge`（`Runtime/DailyChallenge`，只引 `Tracking`，纯 .NET）。库拥有这些名字：
`dc_open`（`dc_source`）、`dc_reward_unlocked`（`dc_month`、`dc_tier`）、`dc_reminder_open`（`dc_reminder_index`），以及每日挑战那一局
盖在关卡事件上的 `dc_date`、`dc_days_ago`（`LevelContext`，游戏拼进 `LevelTracker` 的公共参数）。每日挑战的局**不另起关卡事件**。
入口名、奖励档名、何时算跨档、通知怎么排怎么读留在游戏。口径逐条写在 `DailyChallengeEvents` 的注释与 `README.md`「DailyChallenge」一节。

**理由**：owner 要把每日挑战做成各游戏通用的功能，埋点名字统一了，跨游戏的查询和看板才能共用。两家竞品（Oakever 5.77.0、Easybrain 7.9.0）
都在对局事件上标每日挑战的日期、都记日历入口的来源，Easybrain 还记推送打开——这几块就是本模块的范围。
「今天的题 / 补做」用离今天的天数而不是只给日期：BigQuery 里没有玩家本地的「今天」，事后按时区推算容易错。
领奖**档**用跨档那一刻报，不用领奖弹窗：弹窗在回日历的动画之后才出，玩家可能等不到。

**代价**：
- 名字发版后改不了（同 `LevelTrackingEvents`），改名要先问 owner。
- 页内点击（点日期、切月、看奖杯、玩法说明）不进包；各游戏页面不同，要看某页时游戏自己加平事件。
- 没有 `LevelTrackingSchema` 那样的机器表：现在没有游戏的文档工具读它，每个方法发什么由 `DailyChallengeTrackerTests` 直接钉；
  哪个游戏要按表生成埋点文档时再补。

---

## D-20260924-01 首启条款弹窗的**字与下划线链接**进包（`Tracking.Consent.UI`），皮仍留在游戏

日期：2026-09-24　状态：active　拍板人：owner（「这个有没有办法在 tracking sdk 中加入这个逻辑。也就是首启弹窗的文字内容，包括按钮内容和下划线标注等」
「按这个做，先都不用换」）　模型：Claude Opus 5.5

**决策**：新增可选程序集 `Tracking.Consent.UI`（`Runtime/Consent/UI`）。`TermsCopy` 带首启条款弹窗的标题、正文、同意键、两个文件名，
14 种语言一份，照 arrows `Assets/Resources/Localization/UiCopy.json` 的同名字段逐字取（arrows `cd6ab4e3`）；`TextLinks` 给 uGUI
正文里的两个文件名画下划线、做热区，点了回调游戏。游戏传自己界面语言的名字，没有的落英文。**卡片、压暗层、颜色、字体、字号、
按钮样式仍留在游戏。** 这推翻 D-20260920-02 里「文案、语言、排版留在游戏」那一半（那一半当时是 agent 的判断，owner 未表态）；
「判定进包、键归游戏、皮留在游戏」不变。各游戏暂不切换（owner：「先都不用换」）。

**理由**：owner 要 sudoku 的首启弹窗「箭头写的什么本项目就写什么」（sudoku DECISION-20260924-08），两款游戏于是各抄一份同样的字、
同样的链接代码。arrows 这套措辞不提广告，离线游戏也能用，D-20260920-02 那时「四份措辞实质不同」的前提对这套字不再成立。
下划线链接本身是纯机制（按字符下标去排版结果里量字盒），本来就符合「机制进库」。

**代价**：
- 包第一次引 uGUI。新程序集单独一个、可选，只用埋点的游戏不引它；但 `package.json` 声明了 `com.unity.ugui` 依赖，
  json 放在 `Resources` 里，**钉了包就进 APK**（几 KB），不受 asmdef 引用约束。
- 它不是纯 .NET，**不在 `CLAUDE.md` 那道 Roslyn 秒级门里**，只能在消费方工程里跑测试。
- **字体**：游戏的界面字体多是按自家文案裁的子集，切过来的游戏要把这份 json 算进字体子集与字形覆盖测试；这份文案改措辞，各游戏都要重出字体。
- 游戏切过来之前，arrows、sudoku 各有一份同样的字，加上本包是三份；改措辞时三处一起改，或者先让游戏切过来。

---

## D-20260920-02 条款弹窗的**判定**进包、**界面**不进；同意状态的键归游戏

日期：2026-09-20　状态：amended（弹窗上的字与下划线链接由 D-20260924-01 改为进包；判定进包、键归游戏、皮留在游戏不变）　拍板人：owner（「把首起隐私协议弹窗加进来」）；**「界面不进」这一半是 agent 的判断**，
owner 未表态，不同意就推翻这条　模型：Claude Opus 5

**决策**：`Tracking.Consent` 新增 `TermsGate` + `ITermsStore` + `PlayerPrefsTermsStore`，接手首启条款弹窗的
**判定**：弹不弹、两条事件发在哪、同意怎么落盘、点完之后谁被放行。**界面、文案、语言、排版仍留在游戏侧。**
同意状态的**键由游戏传**，包**不自带默认键**。这推翻了 PRD §2 非目标里「不改两款游戏现有的条款弹窗形态」的一半
（PRD 是冻住的文档，不改它，推翻处就记在这里）。

**理由**：本条之前四款游戏各写过一遍（arrows 的 `FirstRunConsentDialog`、water_sort 的 `FirstRunConsentUI`、
arrows-3d 的 `GameHud.Legal`、boopdoku 的 `BoopdokuApp.Legal`），第五款在路上。逐条比下来，**共有的只有三件事，
而且三件都是写错了还静默**：① 老玩家（已同意过的）也必须给 `ConsentFlow` 发「条款已同意」，漏了就是 UMP 请求
永不发出、欧洲整场没广告，水位线上只看得到「欧洲 eCPM 没了」——water_sort 当初是自己发现后补的那一行，
arrows 是靠无条件补发绕过的；② 两条事件的位置（名字 2026-09-11 建模块时就定了，**至今零发射点**）；
③ 同意只落盘一次、且立刻落盘。**不共有的恰恰是界面**：四套主题、四套各自手写的 uGUI、四份措辞实质不同的正文
（两款有广告有归因、两款纯单机离线），共用一份界面是把四张皮塞进一个接口。判据是 `CLAUDE.md` 那条
「机制进库，词汇留在游戏」——弹窗的皮是词汇。

**代价**：
- 🔴 **键归游戏这条一旦有人图省事违反**（给 `PlayerPrefsTermsStore` 传一个「包的统一键」），后果是**所有已经
  同意过的老玩家冷启再吃一道全屏闸**；本仓没有 CI，消费方也没有任何门能发现——只有玩家看得见。
  所以键是必填参数、没有默认值，理由同 `UmpConsentPlatform` 的 AdMob 应用 id（D-20260911-02 的代价一节）。
- 两条事件从此才开始有数据：各游戏钉上带本条的 SHA 之前，GA4 里 `dlg_show_law` / `btn_click_law` 是**真空白**，
  不是「弹窗没弹」。做同意漏斗时分母别跨那条线。
- 包里多了一个游戏可以不用的类；`Tracking.Consent` 也因此第一次引到 `UnityEngine`（`PlayerPrefsTermsStore`
  一处，其余仍是纯 .NET、EditMode 可测）。
- 四款游戏各自的那份判定**没有就地替换**——换过去要一个个改并在各自的门禁上验，属消费方的工作，本条不代它决定。

---

## D-20260920-01 改动默认直接推 `main`，PR 只作评审载体——要不要评审由动手的 agent 评估

日期：2026-09-20　状态：active　拍板人：owner（「项目规则改成可以直推 main 且 agent 评估不需要审查的话就可以直推」）　模型：Claude Fable 5.1

**决策**：撤销 `AGENTS.md` §二「分支 + PR 合 `main`，不直推」。动手的 agent 自己评估这次改动要不要评审：
不需要的，自验后直接推 `main`、不开 PR；需要的，走分支 + PR，评审意见写在 PR 上。落地措辞见 `AGENTS.md` §二。
不变的：`CLAUDE.md` 里「改之前先问 owner」的事照旧先问；提交信息、署名 trailer、什么该记进本文件，都不变。

**理由**：PR 在本仓多数时候不承担评审——#1–#18 里有正式 review 的只有 #9 一个，13 个既无 review 也无评论，
剩下的是「开 PR → 等 owner 说合并」这一轮；2026-09-19 / 20 的 #17（两行 `minSdk`）与 #18（四处文档）各等了一轮。
两点让直推在本仓比在别处更便宜：① 消费方按 40 位 SHA 钉本仓，`main` 上的一次提交**不会自己流进任何游戏**，
要有人去升钉、在消费仓跑过门禁才会；② 直推的提交 SHA 就是消费方要钉的 SHA——squash 合并会换 SHA
（#17 分支上是 `73674e1`，合并后是 `69701e8`），「PR 里报一个、合并后再报一个」这件事随之消失。
消费仓已是同一口径：arrows DECISION-20260915-01、water_sort DECISION-20260915-185，arrows-3d 2026-09-19 接入协议时即如此。

**代价**：评估错了没有任何门会拦——本仓没有 CI，该评审而没评审的改动会原样进 `main`，要等某个消费仓升钉、
编译或构建到它才露出来（`IAnalyticsBackend` 加成员而假件没跟上、`.androidlib` 的 gradle 写错，都是这个形态）。
owner 这次**没有给「哪类改动该评审」的清单**，判定全在 agent；哪天想要一个评估基准，由 owner 加，agent 不自拟。
开了 PR 的由谁合并，这次没谈。

---

## D-20260916-01 Firebase 写死 GRANTED 改为 `AttachWhenReady` 一进来经 Java 桥同步写

日期：2026-09-16　状态：active　拍板人：owner（arrows：「按你说的改。然后打普通测试包」）　模型：Claude Opus 5

**决策**：D-20260911-01「写死 GRANTED」那一步从 `AttachWhenReady` 的依赖就绪回调（#14）挪到方法**一进来**，经新的
`Runtime/Firebase/TrackingFirebase.androidlib`（`com.gthbj.tracking.firebase.FirebaseConsentBridge.grantAll`）同步写：
开采集 + 四项同意 GRANTED，与原包 oakever 在 `Application.onCreate` 里写的时点一致。

**理由**：#14 写在回调里时假设「游戏的同意请求比 Firebase 就绪晚」。arrows 真机逐帧录冷启证伪了它：首个场景第一帧
在进程起来约 0.9 秒，老玩家的同意请求就在那一刻发，UMP 再约 0.6 秒回话并把真实选择推给 Firebase；而 C# 侧的依赖检查
慢机上要一两秒——GRANTED 落在推送之后，拒绝过的玩家会被盖回「已同意」一整场，原包没有这个问题。C# 的 Firebase API
要等依赖检查，所以只能走 Java（Java 侧 Firebase 在 `FirebaseInitProvider` 里就起好了）；碰三方 SDK 走包内 Java 桥是
D-20260911-02 的既有口径。

**代价**：C# 按名字调桥，keep 规则随模块走，正式包要在 dex 里验 `Lcom/gthbj/tracking/firebase/FirebaseConsentBridge;`。
`compileOnly` 钉的 firebase-analytics 版本只影响编译；消费方运行期低于 21.5.0 时桥会抛、被 C# 接住记一行警告、这一场不写死。

---

## D-20260911-03 欧洲那条路只认 `SetDebugGeography` 验证，旋钮放包里、设备哈希不入库

日期：2026-09-11　状态：active　模型：Claude Opus 5

**决策**：同意表单那条路（`loadConsentForm` / `showConsentForm` / `showPrivacyOptionsForm`）的验证，
**一律走 UMP 自己的 `ConsentDebugSettings`**，旋钮做成桥上的 `setDebugGeography` / C# 的
`UmpConsentPlatform.SetDebugGeography`，**放在包里、每个游戏共用**。两条附带口径：
① 调用点必须被**构建期 define** 圈住（water_sort 是 `ADMIN`），不靠人记得删；
② 测试设备哈希是**那台机器的标识**，不进任何仓库，从构建参数取或就地临时改。

**理由**：挂欧洲 VPN **不足以**触发受管辖区判定——2026-09-11 在 water_sort 实测，
手机连**应用自己 uid 的 socket** 出口都是法国 OVH 的 `5.135.5.129`，UMP 照样回
`consent_status=1`、`stored_info` 空、`is_pub_misconfigured=false`（数据中心 IP 拿不到判定）。
没有这个旋钮，那三个方法在国内**一次都跑不到**，而「没验过」与「验过是对的」在所有门禁上都是绿的。
放包里而不是每个游戏各写一份，是因为它要碰 `ConsentRequestParameters.Builder`——
那正是桥存在的理由（D-20260911-02），从 C# 直接碰会被 R8 改名。

**代价**：桥多一个公开静态方法与两个静态字段，正式包里也带着。用没带 define 的调用点误开的后果是
**欧洲判定被强制**，所以桥每次被调都打一条 `Log.w` 留痕。旋钮本身无法被 EditMode 覆盖
（`Tracking.Consent.Android` 只有 APK 能编译），它的正确性只能靠真机跑一次表单来证。

---

## D-20260911-02 碰第三方 Android SDK 一律经包内 Java 桥，keep 规则随 `.androidlib` 走

日期：2026-09-11　状态：active　模型：Claude Opus 5

**决策**：本包再要调某个第三方 Android SDK（这次是 Google UMP），**不从 C# 直接按字符串类名 JNI**，
而是在包里放一个 `.androidlib` 模块，用 Java 调它，C# 只按名字找**我们自己的桥**；桥的 keep 规则写进
该模块的 `proguard-consumer-rules.pro`，由 `consumerProguardFiles` **随包传给消费方 app 的 R8**。
落地细节见 `README.md`「同意模块的 Android 侧」。

**理由**：判据是**「要 keep 的是谁家的类」**。`AppSetIdUserProperty` 那条（README 里写着「规则必须放游戏仓」）
keep 的是 `com.google.android.gms.appset.*`——别人家的库，包管不着，只能每个游戏各抄一份。
这次不一样：写成 Java 之后对 UMP 的引用由 R8 自己保持一致，**根本不需要 keep**；
剩下要 keep 的只有我们自己的桥，而自己的东西可以自带规则。两条并不矛盾，
别照着前一条给 UMP 也去游戏仓抄一堆 keep。

🔴 **这里差点写错一个前提，记下来**：初稿写的是「UMP 的类在正式包里必然被改名」。
2026-09-11 在 arrows 正式包 `mapping.txt` 上实测**不是**——`com.google.android.ump.*` 原名保留。
查 R8 的 `configuration.txt`，保它的是 **GoogleMobileAds Unity 插件**自带的
`-keep public class com.google.android.ump.** { public *; }`，全工程唯一来源。
所以正确的说法是：**arrows 今天靠的是一条随时会被移除的外部规则**（那个插件正是要换成 MAX 的），
而 **water_sort 根本没有那个插件**。结论不变（要桥），但理由从「必然坏」改成
「在一款游戏上今天就坏、在另一款上换 MAX 那天起坏」——这个差别决定了将来有人问
「能不能省掉这层桥」时该怎么答。

**代价**：① `.androidlib/build.gradle` 必须手写并自己钉 `compileSdk`——Unity 给没有 build.gradle 的
`.androidlib` 生成的模板里 `//java.srcDirs = ['src']` 是注释掉的，**根本不编译 Java 而构建照样全绿**；
和消费方的 SDK 版本对不上时会红，但是**响亮**地红。② 运行期依赖仍要消费方在自己仓的
`*Dependencies.xml` 里声明（EDM4U 不扫 UPM 包目录，这条绕不过）。③ 这一面 EditMode 一行都编译不到，
每次改桥或升 UMP 都要建**正式包**（minify 开）去 dex 里验描述符还在——验法写在 README 里。

---

## D-20260911-01 同意流程模块照 oakever，只把「广告等 UMP」那条修好

日期：2026-09-11　状态：active　拍板人：owner（「做成和 oakever 一模一样就行」「把重试那条改正过来，
保证广告展示更多。别的广告展示会减少，就保持和 oakever 一致」）　模型：Claude Opus 5

**决策**：新增 `Runtime/Consent` 模块，行为逐条对齐竞品 oakever（Learnings UniKit + Google UMP），
**唯一偏离**是 UMP 请求顺序与失败重试改成 Google 官方推荐做法。规格与逐条出处见
`Docs~/PRD_20260911_1509_首启同意流程SDK照oakever.md`；竞品行为真源是 `package-analysis` 仓的
`arrows/com.oakever.arrows/docs/1.29.1/PRIVACY_CMP.md`。

**理由**：偏离的那一条只影响广告展示量、不影响合规——它用的是用户**上次自己做过的**选择，
且新用户在两种做法下完全一致（本地无记录时 `canRequestAds()` 为假，照样等回话、照样弹表单）。
其余四条（写死 GRANTED、分析归因先于同意启动、首屏不给「不同意」、拒绝不额外转发给 Meta / AppsFlyer）
在数据与个性化上只增不减、在合规上更弱，owner 在看过逐条方向后选择与竞品一致。
这四条与 PRD §2、§10-1 是**同一份名单**；AppsFlyer 国家表要不要连错码一起照抄，owner 还没拍（PRD §10-1e）。

**代价**：用户做选择之前、以及此后每次冷启约 5.5 秒，Firebase 处在「已同意」；拒绝由各 SDK 自己去读
TCF——实测 Firebase / MAX / IronSource / Chartboost 收得到，Meta 与 AppsFlyer 本包不转发（原包运行时未核），
且两边都不报错。这是 owner 明示接受的取舍，不是遗漏——**后来者不要当 bug 顺手"修好"**，
要改先回到 owner。消费方 `gthbj/arrows` 侧还要部分推翻它自己的 DECISION-20260831-05（采集默认关、
CMP 完成前不起三方 SDK），那半边记在 arrows 仓，本条不复述。

---

## D-20260909-02 安装标识进包：`AttachWhenReady` 自动设 GA4 `user_id`，接口撤回 `SetUserId`

日期：2026-09-09　状态：active　模型：Claude Fable 5.1（见 `f3954ea`、`8ead591`）

**决策**：`user_id` 由包自己生成并在后端就绪时自动设，`IAnalyticsBackend` 上刚加的 `SetUserId` 随即撤回。

**理由**：两款游戏都要同一个「跟着存档走的安装标识」，放在游戏侧等于把同一段逻辑写两遍，
且谁漏了都不报错。

**代价**：接口在同一天加了又撤，消费方跟着改了两次。**破坏性变更**：包内测试假件必须与接口同提交跟上，
否则两个消费仓的 EditMode 一起 `CS0535`（`3e64caa` 实证）。

---

## D-20260909-01 `level-tracking` 改名 `tracking`，按域切成四个程序集

日期：2026-09-09　状态：active　模型：Claude Opus 5（见 `b370e96`）

**决策**：包名 `com.gthbj.level-tracking` → `com.gthbj.tracking`；内容切成 `Tracking`（传输层）/
`Tracking.Firebase.Android` / `Tracking.Identity.Android` / `LevelTracking`（关卡域），依赖单向：域模块 → 核心，
域模块之间互不引用。

**理由**：包里从一开始就不只有关卡——整条 Firebase 传输通道与关卡无关。切程序集让「只要关卡不要别的」
在编译期成立。

**代价**：两个消费仓的 `manifest.json` 钉版本、asmdef 引用与 `using` 都要跟着改。
**事件名 / 参数名 / 类型名一个没动**，所以线上数据不受影响——程序集与命名空间改名不算破坏性升版本，
`LevelTrackingEvents` 那类**事件名**改名才算（`CLAUDE.md` 有这条纪律）。

---

## D-20260906-01 从 `gthbj/arrows` 抽出本包，仓库公开

日期：2026-09-06　状态：active　拍板人：owner　模型：Claude Fable 5.1（见 `3310112`）

**决策**：关卡埋点机制从 arrows 抽成独立包，第二个消费者是 `gthbj/water_sort`；仓库公开。

**理由**：两款游戏要同一套事件语义与同一条补报队列；共享注册表会让一边改名损坏另一边的 GA4 历史，
所以**库只拥有它自己发出的名字**，游戏专属词汇留在游戏。

**代价**：消费方按 40 位 commit SHA 钉本仓，每次升级要各自改钉。设计取舍原文在
`Docs~/DESIGN_ORIGIN_arrows_PRD_20260906_1854.md`（来路是 arrows 仓的 PRD_20260906_1854）。
