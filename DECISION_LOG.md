# 决策日志

本包的持久决策，**最新在上**。只记「不可逆取舍」与「推翻过前一版口径」的那些——
改名、破坏性接口变更、模块边界、以及会约束将来工作的口径。

不记：怎么接入（`README.md`）、边界判据与陷阱（`CLAUDE.md`）、某次实现细节（提交信息与 PR）。
**同一句话不写两遍**：这里只写「定了什么、为什么、代价」，落地细节指向那两个文件。

格式：`## D-YYYYMMDD-NN 标题` + 日期 / 状态 / 拍板人 / 模型，正文写**决策、理由、代价**三段。
状态取 `active` / `amended` / `superseded`。

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
