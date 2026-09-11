# PRD_20260911_1509 首启同意流程 SDK：照 oakever，只修好广告那一条

> rev.1（2026-09-11）。落点：本包新模块 `Runtime/Consent`；消费方 `gthbj/arrows`、`gthbj/water_sort`。
>
> 🔴 **行为真源是竞品实测文档，不是本文**：`package-analysis` 仓
> `arrows/com.oakever.arrows/docs/1.29.1/PRIVACY_CMP.md`（下称**原包文档**；地址取自 1.29.1 静态反汇编，
> 行为取自 chopin 1.28.0 root 真机）。本文只写「照它哪一条做、落在哪个类」，**不复述原包的地址、行号与日志**——
> 那些随版本移动，抄进来就会悄悄过期。每条规格后面的「原包文档 X」是章节指针。

## 1. 目标与非目标

**目标**：把 oakever（Amaze GO! / Arrows GO!，Learnings UniKit）的首启同意流程做成两款游戏共用的模块，
使 EEA / UK / CH 扩区时「Google 认证 CMP + 各 SDK 同意信号」这一前置成立（发布 runbook §5）。

**非目标**：不做我们自己的同意界面（UMP 表单就是界面）、不做 iOS ATT（原包在 Android 恒不触发）、
不改两款游戏现有的条款弹窗形态。

## 2. owner 拍板记录（2026-09-11 本对话）

| # | 原话 | 含义 |
|---|---|---|
| 1 | 「做成和 oakever 一模一样就行」 | 针对一张列了 5 处「oakever 做法 vs 建议改法」的对照表，选原样 |
| 2 | 「我之后也会把 admob 直连换成 max」 | 同意侧不得依赖具体广告 SDK，换 MAX 时不应改动（§7） |
| 3 | 「把重试那条改正过来，保证广告展示更多。别的广告展示会减少，就保持和 oakever 一致」 | **唯一偏离**：UMP 请求顺序与失败重试（§6）；其余四条照原包（§5、§10） |
| 4 | 「PRD 写进 tracking 这个仓库」「名字不用改」 | 本文件落在本包 `Docs~/`；包名 `com.gthbj.tracking` 不变 |

🔴 **第 3 条的取舍是清醒做出的**：其余四条（写死 GRANTED、分析归因先于同意启动、首屏不给「不同意」、
拒绝不额外转发给 Meta / AppsFlyer）在数据与个性化上**只增不减**，在合规上弱于我们自己的政策承诺。
owner 在看过逐条方向后选择与原包一致，见 §10-1（**§2、§10、`DECISION_LOG` 三处的「四条」是同一份名单**）。

## 3. 决策碰撞：部分推翻 arrows DECISION-20260831-05

按 `AGENTS.md` §二「撞线把最优方案 / 撞哪条 / 绕开代价列出来」：

| -05 原句 | 原包做法（原包文档） | 本 PRD | 代价 |
|---|---|---|---|
| 采集默认关闭（AndroidManifest） | 清单里 `firebase_analytics_collection_enabled=false`，但 `onCreate` 立刻 `setAnalyticsCollectionEnabled(true)` + `setConsent` 四项 GRANTED（二·0、五） | 照原包 | 用户做选择之前、以及之后每次冷启约 5.5 秒，Firebase 处在「已同意」 |
| CMP 完成后才初始化**任何**三方 SDK | 只有**广告**等 UMP；分析 / 归因 `onCreate` 就起（二、四.4） | 照原包：只有广告等 CMP | 同上；且 MAX 官方要求「同意流程结束前不要初始化三方 SDK」在分析侧不满足 |
| CMP 选型 = MAX 自带的 Google UMP | UniKit **直调** UMP，没开 MAX 的 CMP 流程（一末） | 直调 UMP | 无（反而让 AdMob→MAX 切换不动同意侧，满足 §2-2） |

-05 的「同意流程与广告接入是一个战役、不拆」**不变**。

**落地方式**：本 PRD 获批后，在 **arrows 仓**登记决策（部分推翻 -05），并同步 arrows `TODO.md` 的
「同意流程 + 广告接入」条目与发布 runbook §5 里「MAX 接入后采用其 UMP 集成」那一句。
本包的 `DECISION_LOG.md` 只记与本包实现相关的那一半。**两处不写同一句话。**

## 4. 两款游戏现状（2026-09-11 盘点，各自 `main`）

| | arrows | water_sort |
|---|---|---|
| 条款弹窗 | `FirstRunConsentDialog`，一颗 Accept、无拒绝，**阻塞冷启协程**（`BootBootstrap`）；存档键 `legalConsentAccepted` | `FirstRunConsentUI`，同形；PlayerPrefs `WaterSort.LegalConsent.v1` |
| 三方 SDK 启动 | `AndroidPlatformBootstrap.StartThirdPartySdks`（`BeforeSceneLoad`）：Firebase → AppsFlyer → App Set ID → Unity Ads | `LevelTrackingBootstrap.Install`（`BeforeSceneLoad`）：同四样 |
| 等不等弹窗 | **不等**（源码注释已写「那一天要改的只该是这一个函数」） | **不等**；且 `level_start` 在弹窗之前就发了 |
| 在投的广告 | **Unity Ads 直连**；`AdMobAdBackend` 存在但无人构造 | **Unity Ads 直连**；无 AdMob |
| UMP | 库已在（`user-messaging-platform:4.0.0` 在 `mainTemplate.gradle`），**无任何调用** | **完全没有** |
| Firebase 同意调用 | 无；清单也无 consent-mode 默认值 | 无 |
| 设置页法律入口 | 齿轮列第 5 格 → `LegalInfoPanelUI`（顶部有一颗外链按钮） | 功能面板底部链接 → `LegalInfoPanelUI` |

⟹ 两款游戏**现在都没有任何同意信号**：不是「实现得不对」，是整条链还不存在。本 PRD 是从零接。

## 5. 行为规格：照 oakever

### 5.1 进程启动（原包文档 二·0）

1. 制裁国检查：SIM 国家（取不到用系统 locale）∈ {`CU`,`IR`} ⟹ 不起分析 / 推送并挡住界面。**留在游戏侧**（§7）。
2. 否则立刻起分析 / 归因，**不等任何同意**：
   - Firebase：`setAnalyticsCollectionEnabled(true)` + `setConsent` 四项（ad_storage / analytics_storage /
     ad_user_data / ad_personalization）全 `GRANTED`；
   - AppsFlyer：按国家表判 GDPR 与否给同意数据，随后 `start`。国家表照原包（**含它把希腊写成 `EL`、
     英国写成 `UK`、缺 IS/LI/NO、取不到国家按 GDPR 处理**，原包文档 六）。
     🔴 我们插件里 `ForGDPRUser` / `ForNonGDPRUser` 已废弃，用等价的非废弃构造，签名在实现 PR 按插件源码核。
     国家表里的错码要不要照抄，见 §10-1e。
3. 同意之前的 Unity 侧普通打点**丢弃**（不是缓存补报），只有条款弹窗那两条直发（原包文档 三末）。
   - arrows 天然满足（弹窗挡在主菜单之前）；**water_sort 需要改**：`level_start` 现在先于弹窗。

### 5.2 条款弹窗（原包文档 三）

全球新装都弹、一颗同意按钮、无拒绝、不点不放行；老用户视为已同意。**两款游戏现有实现已同形，界面与文案不动。**
点下之后：记已同意 → 再设一次 Firebase 四项 `GRANTED` → 给同意流程发「条款已同意」信号。

### 5.3 UMP（原包文档 四.1–四.6）

| 项 | 规格 |
|---|---|
| 触发时点 | 「条款已同意」与「游戏放行」两个条件都满足时触发，**取较晚者，且只触发一次**。原包的「游戏放行」是冷启协程走到加载阶段；我方 arrows 放在过了弹窗之后，water_sort 放在同意回调之后 |
| 请求参数 | 默认参数：**无 debug geography、无未成年标记** |
| 分支 | 状态 = REQUIRED ⟹ 加载表单 → 显示（没有有效 Activity 就等下一次 `onResume`）；其余状态 / 请求失败 / 表单加载失败 ⟹ 直接进收尾 |
| 收尾 | `canRequestAds()` 为真才初始化广告 SDK；**无论成败都回调游戏** |
| 等待 | 在独立协程里逐帧等、**无超时**，不挡加载；广告位等广告 SDK 初始化完成才拉 |
| 失败 | 见 §6（**唯一偏离**） |

`canRequestAds()` 的语义（原包文档 四.4）：`is_pub_misconfigured` ∨ 状态 ∈ {NOT_REQUIRED, OBTAINED}，
**且只在本进程调过 `requestConsentInfoUpdate` 之后才读得到**，持久化默认值是「未知」。这条语义是 §6 的全部依据。

### 5.4 拒绝之后谁收得到（原包文档 五，chopin 1.28.0 实测）

**本包不额外做转发**，照原包。谁收得到是各 SDK 自己的行为，不是本包的决定：

- **实测收得到**：Firebase（UMP 自己经反射写）、MAX（自带 TcfManager 按 Google AC 串改写）、
  IronSource / Chartboost（自读 TCF，同意 `true`/`1`、拒绝 `false`/`0`）。
- **原包代码不转发、运行时未核**：Meta（原包只 `setDataProcessingOptions([])`；按 AppLovin 隐私文档
  MAX 不代 Meta 处理同意值，但 MAX 的 Meta adapter 会不会把 TCF 传过去，原包**没测**）、
  AppsFlyer（原包写死「已同意」且未开 `enableTCFDataCollection`；日志里无同意行，**未核**）。
  🔴 别把这一行写成「收不到」——那是过冲，原包文档标的是未核。
- 拒绝后**广告 SDK 照常初始化**（两条路径 UMP 状态都是 OBTAINED）。

### 5.5 设置页入口（原包文档 四.7）

「隐私偏好」入口显示**当且仅当** `getPrivacyOptionsRequirementStatus() == REQUIRED`；
点了走 `showPrivacyOptionsForm`，出错弹 toast。

🔴 **这张表和首启那张不是同一张**（原包文档 四末）：首启 REQUIRED 那张第一屏只有「同意 / 管理选项」两颗，
从隐私入口重开的那张 Google 强制给「不同意 / 同意 / 管理选项」三颗。首屏给不给「不同意」是**发布方在 AdMob
后台按国家配的**，不是代码——照原包 = 不勾（§10-1d）。

### 5.6 美国（原包文档 四.9）

`IsUsPrivacyAgreed = (IABGPP_HDR_GppString != "DBABL~BVQVAAAAAg")`，**整串精确比较**。
结果供广告后端用（MAX 期的 `setDoNotSell`）。

### 5.7 打点（原包文档 四.8）

六条 UMP 事件：`ump_consent_info_request_start` / `ump_consent_form_load_start` / `ump_consent_form_try_show` /
`ump_consent_form_show_success`（带 `purpose` = `IABTCF_PurposeConsents`）/ `ump_privacy_form_try_show` /
`ump_privacy_form_show_success`。加上条款弹窗两条（弹出 / 点击）。

🔴 **名字归本包**（`ConsentEvents`），与 `LevelTrackingEvents` 同一纪律：GA4 事件名发出即进 property 字典，
改名等于新开事件、历史不跟 —— 定稿前与 owner 过一次（§10-4）。

## 6. 唯一偏离：UMP 请求顺序与失败重试

**原包怎么坏的**（原包文档 四.4 第三点）：它写了「沿用上次同意、并行初始化广告」这条快速路径，
但把它排在 `requestConsentInfoUpdate` **之前**。按 §5.3 末尾那条语义，那一刻 `canRequestAds()` 恒为假，
**这条路径永不触发**。后果两条：每次冷启广告都要等服务端回话；请求失败则整场无广告且不重试
（任务闸只放行一次）。

**我们怎么做**（Google 官方推荐顺序）：

1. 先调 `requestConsentInfoUpdate`（异步）；
2. **调用返回后立刻**（不等网络回话）查 `canRequestAds()` —— 此时读到的是**上次会话存下来的**同意状态；
   为真就**并行初始化广告**；
3. 回话到达后走 §5.3 的分支；若广告尚未初始化且此时 `canRequestAds()` 为真，再初始化；
4. **请求失败则重试**（指数退避，次数与间隔做成旋钮，初值在实现 PR 定并标明「非契约」）。
   重试只重发**信息更新请求**；若重试成功且判定 REQUIRED 而表单尚未展示过，按 §5.3 的「等有效 Activity」
   规则择机展示。

**为什么这条不动合规**（三条一起成立才算）：

- 新用户（本地没有上次的选择）在两种做法下**完全一样**：本地无记录 ⟹ 第 2 步的 `canRequestAds()` 为假 ⟹
  仍然等回话、该弹表单照弹。欧洲新用户不会在同意之前看到广告。
- 第 2 步用的是**用户自己上次做过的选择**，不是替他假设。
- 广告的个性化与否由各 SDK 在**每次请求广告时**读本地同意记录决定，与初始化时点无关。

**收益方向**（对照 §10 其余四条只减不增）：老用户开局即可出广告；请求失败的那一局不再整场无广告。
量级取决于请求失败率，**该值未知**；上线后用 `ump_consent_info_request_start` 与后续事件的缺口估算。

## 7. 架构落点

### 7.1 进包 vs 留游戏

按本包 `CLAUDE.md` 的「机制进库，词汇留在游戏」：

| 进包 | 留在游戏 |
|---|---|
| 流程状态机：两个前置条件取较晚者、只跑一次 | 条款弹窗的界面、文案、存档键 |
| UMP 调用：请求 / 表单 / 隐私选项表单 / 查状态 | 设置页「隐私偏好」那一行的位置与样式 |
| `canRequestAds()` 门与「广告可以初始化了」回调 | **广告 SDK 本体与其后端**（Unity Ads 现状、AdMob、将来的 MAX，含 MAX 的两行同意值） |
| 隐私入口可见性判定（REQUIRED 才显示） | AppsFlyer 后端与它的启动时点 |
| 美国 GPP 串整串比较（§5.6） | 制裁国封禁页（与追踪无关，只是一个国家码判断） |
| `ConsentEvents` 六 + 二个事件名 | 各游戏自己的事件与参数 |
| Firebase 写死 GRANTED 那一步 → 放进**已有的** `Tracking.Firebase.Android`（全项目唯一碰 `Firebase.*` 的地方） | `google-services.json`、Firebase SDK 本体 |

🔴 **包不认识任何广告 SDK**：它只发「可以初始化广告了」这个信号，谁来接是游戏的事。
这正是 §2-2 要的——AdMob 直连换 MAX 时，同意侧一行不用改。

### 7.2 程序集

- `Tracking.Consent`（`Runtime/Consent`，全平台）：状态机、门、可见性判定、GPP 比较、事件名。**只引 `Tracking` 核心**，
  与 `LevelTracking` 互不引用（本包依赖单向的既有纪律）。
- `Tracking.Consent.Android`（`Runtime/Consent/Android`，`includePlatforms: ["Android"]`）：UMP 调用。

### 7.3 UMP 怎么调

**直调 UMP 的 Java API**，不经 MAX 的 CMP 流程，也不依赖 Google Mobile Ads Unity 插件的 C# 封装
（换 MAX 后那个插件可能整个移除；water_sort 本来就没有）。实现取向：包内放 Java 桥，Unity 会把包里的
`.java` 编进 Gradle 工程，R8 因此看得见对 UMP 的引用。

两条**已经交过学费**的接入约束（本包 README 有原文）：

1. **依赖声明归游戏**：`com.google.android.ump:user-messaging-platform` 要写在**游戏仓**某个 `Editor/` 下的
   `*Dependencies.xml` —— EDM4U 不扫 UPM 包目录。arrows 已经有这一行（因 GoogleMobileAds 插件）；
   **water_sort 是新增第三方库**，按它自己仓的规矩要 owner 批准（§10-3）。
2. **R8 keep 规则归游戏**：桥按字符串类名过 JNI，正式包会被改名，且**失败形态是静默的**
   （同 App Set ID 那次：构建绿、真机上属性无声消失）。验收必须去 dex 里验描述符还在（§9-6）。

## 8. 分 PR 战役

| PR | 仓 | 内容 | 门禁 |
|---|---|---|---|
| **1** | 本包 | `Runtime/Consent` 两个程序集 + Java 桥 + EditMode 测试；`Tracking.Firebase.Android` 补写死 GRANTED 那一步 | 消费方快车道（`testables`）+ 消费方 Android APK（Android 程序集只在 APK 编译） |
| **2** | arrows | 接入：广告启动从 `StartThirdPartySdks` 移到同意回调；条款弹窗后放行 UMP；`LegalInfoPanelUI` 加「隐私偏好」；AppsFlyer 同意数据 + 国家表；八条事件；钉新 SHA；`PlatformHostContractTests` 的漏斗断言随之改 | 快车道 + Android APK + 真机（§9） |
| **3** | water_sort | 同上；外加**同意前丢弃 Unity 侧事件**（`level_start` 现在先于弹窗）、新增 UMP 依赖与 AdMob 应用 id | 同上 |
| **4** | arrows | 随 MAX 切换：广告后端加 `setHasUserConsent(true)` / `setDoNotSell(!IsUsPrivacyAgreed)` | 随 MAX 那个 PR |
| **owner** | — | AdMob 后台欧洲消息配置（§5.5）；water_sort 的 AdMob 应用；隐私政策「撤回入口」与 data-safety 按「已接 CMP」更新（`gthbj/boopfun-legal`） | — |

PR-1 可以现在就做（不改两款游戏的行为）。PR-2/3 的**实现时点**见 §10-2。

## 9. 验收与门禁

真机测法照原包文档「设备实测二」那一节（`su -c 'pm clear'` → 逐步读 `shared_prefs`）；
🔴 **期望值点名真源**：TCF / Firebase / MAX 各键的期望取值以**原包文档五末那张表**为准，本文不抄数字。
其中供应商家数取决于 AdMob 消息里的供应商清单，**非契约、仅示意**。

1. EEA 出口 + 全新安装：条款弹窗 → 同意 → UMP 表单第一屏两颗。
   - 点「同意」：TCF 用途全 1、Firebase 全同意、广告初始化。
   - 点「管理选项 → 确认」（开关不动）：用途全 0、**合法权益照留**、Firebase 全拒绝 + npa、**广告照常初始化**。
2. 隐私入口：EEA 下显示且第一屏三颗；非 EEA 不显示。
3. 非 EEA 全新安装：不弹表单；广告在回调后初始化。
4. **老用户冷启（§6 的收益）**：已选过的用户冷启，广告初始化**不等**服务端回话。
5. **请求失败**：断网冷启 → 重试；新用户仍不出广告、不弹表单（合规不变）。
6. **正式包（minify）dex 里**桥类与 UMP 入口类的描述符都在。
7. 拒绝态冷启：Firebase 先回到「已同意」约 5.5 秒再被 UMP 改回——**这是照抄的行为，不是缺陷**，
   出现即符合预期（原包文档 五）。

工具：`package-analysis` 仓 `scripts/consent_coldstart_sample.sh`，改包名与 prefs 文件名即可复用。

## 10. 待 owner 拍板

1. **照原包的四条，逐条确认**（与 §2、`DECISION_LOG` 同一份名单；默认全部照原包，即 owner 2026-09-11 的选择）：
   a. Firebase 在 `onCreate` 写死 GRANTED，每次冷启约 5.5 秒窗口；
   b. 分析 / 归因先于同意启动，用户做选择前已记安装与首次打开；
   c. 首启表单不勾「不同意」（AdMob 后台配置）；
   d. 拒绝不额外转发给 Meta / AppsFlyer（§5.4；原包运行时未核）。

   **另一条 owner 没在对照表里见过、写作时才浮出的**，需要单独拍板：
   e. AppsFlyer 国家表照抄，**含它的错码**（希腊写成 `EL`、英国写成 `UK`、缺 IS/LI/NO），
      这两国 SIM 会落进非 GDPR 分支。默认照抄（「一模一样」）；改成正确 ISO 码是一行的事，但那就不是原包行为了。
2. **实现时点**：PR-2/3 默认随扩区战役。要提前的话，非 EEA 用户只有一处可感变化——广告改为等
   UMP 回调后初始化（老用户因 §6 基本无感）。
3. **water_sort 一起接吗**：默认接。它要新增 UMP 依赖（新第三方库，按其仓规矩批准）并在 AdMob 建应用拿 id
   （UMP 靠它取后台消息配置）。
4. **八个事件名**：默认照原包命名法。GA4 名字发出即不可逆。

## 11. 刻意不做

- MAX 自带的 CMP 流程 / `TermsAndPrivacyPolicyFlowSettings`（原包也没开）。
- 美国州法消息（原包只读 GPP 串算 `doNotSell`，不弹美国消息）。
- iOS ATT（原包 Android 侧恒不触发）。
- 自研同意界面、自研 TCF 串解析。
- 把制裁国判定搬进包（与追踪无关，留游戏）。

> 文档维护：Claude Fable 5.1（2026-09-11 rev.2，按 PR #9 review：§2 / §10 / `DECISION_LOG` 的「四条」对成同一份名单，「只有 Firebase / MAX 收得到」改为「不额外转发 Meta / AppsFlyer」；§5.4 把 Meta / AppsFlyer 从「收不到」降回原包文档的「代码不转发、运行时未核」；国家表错码单列 §10-1e）；Claude Opus 4.8（2026-09-11 建档）
