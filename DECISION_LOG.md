# 决策日志

本包的持久决策，**最新在上**。只记「不可逆取舍」与「推翻过前一版口径」的那些——
改名、破坏性接口变更、模块边界、以及会约束将来工作的口径。

不记：怎么接入（`README.md`）、边界判据与陷阱（`CLAUDE.md`）、某次实现细节（提交信息与 PR）。
**同一句话不写两遍**：这里只写「定了什么、为什么、代价」，落地细节指向那两个文件。

格式：`## D-YYYYMMDD-NN 标题` + 日期 / 状态 / 拍板人 / 模型，正文写**决策、理由、代价**三段。
状态取 `active` / `amended` / `superseded`。

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
