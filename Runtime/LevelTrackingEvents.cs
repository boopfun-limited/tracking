namespace LevelTracking
{
    /// <summary>
    /// 库发出的事件名与参数名的唯一来源（从 arrows 的 <c>AnalyticsEvents</c> 搬来，
    /// PRD_20260906_1854 阶段 2）。🔴 **改这里的任何名字都是破坏性升版本**：GA4 的事件名
    /// 一旦发出就进了 property 的字典，改名等于新开一个事件、历史不跟；自定义维度不回填。
    /// 两游戏各自决定何时升 SHA。
    ///
    /// 命名照 GA4 约束：≤40 字符、字母开头、只含小写字母数字与下划线，
    /// 且不得用 <c>firebase_</c> / <c>google_</c> / <c>ga_</c> 前缀（包内测试钉着）。
    /// 事件 → 标准参数的映射不在这里，在 <see cref="LevelTrackingSchema"/>。
    /// </summary>
    public static class LevelTrackingEvents
    {
        /// <summary>
        /// 玩家开始**一次尝试**。重开也算一次（重开会重载关卡、清零
        /// <see cref="Params.FailCount"/>），所以这个事件的计数是「尝试数」，
        /// **不是**「进入这一关的次数」。算通过率时注意分母是哪个。
        ///
        /// 怎么开始的记在 <see cref="Params.StartReason"/>，被放弃掉的那次失败几次记在
        /// <see cref="Params.PrevFailCount"/>。
        ///
        /// 🔴 **这里曾经有过一个 `level_restart` 事件，2026-09-01 由 owner 拍板并入本事件。
        /// 不要加回来。** 三条理由：
        /// <list type="number">
        /// <item>GA4 的游戏**推荐事件**只有 `level_start` / `level_end` 一对，"为什么开始"
        /// 属于参数而不是新事件名——同一个理由已经让 `level_complete` 改成了
        /// <see cref="LevelEnd"/>；</item>
        /// <item>它和它触发的 `level_start` 在**同一帧**发出，BigQuery 里分段时那一行总是落在
        /// 错误的一段里（它属于被放弃的那次，却排在新一段的开头），每条查询都要单独扳；</item>
        /// <item>它带的 `fail_count` 必须取自清零**之前**，而那个"必须报在重载之前"的不变量
        /// 只写在注释里、没有任何门——挪一下位置就恒为 0，且报表上看不出来。</item>
        /// </list>
        ///
        /// 管理员入口的装载（跳关 / 切 DDA 档 / 切关卡配置）同样发这个事件，带
        /// <c>AnalyticsEvents.StartReasons.AdminPanel</c>。它今天到不了 GA4，带上是当哨兵用——
        /// 理由见 <c>AnalyticsEvents.StartReasons.AdminPanel</c>。
        /// </summary>
        public const string LevelStart = "level_start";

        /// <summary>
        /// 一局的结局，成功与失败共用，靠 <see cref="Params.Success"/> 区分。
        ///
        /// 🔴 **与 <see cref="LevelStart"/> 不是 1:1**：复活是「接着打当前盘面」（不重载、
        /// 不再发 `level_start`），所以一次 start 之后可以有多个 end——失败一次报一次，
        /// 最后要么以 `success=1` 收尾、要么玩家重开（那会开启新的一次 start）。
        /// 算通过率要用 `level_end(success=1)` ÷ `level_start`，不能用 end 的总数。
        ///
        /// 🔴 名字取 `level_end` 而不是自定义的 `level_complete`，是为了对齐 GA4 的游戏类
        /// **推荐事件**（`level_start` / `level_end` + `success`），内置报表与自动洞察认这套。
        /// 2026-09-01 迁移（owner 拍板）：迁移时线上数据近乎为零，代价最低；GA4 改事件名
        /// 历史数据不会跟过来，晚迁就会在报表上断成两截。
        /// </summary>
        public const string LevelEnd = "level_end";

        /// <summary>
        /// 玩家复活（失败页「继续游戏」，补心接着打当前盘面）。
        ///
        /// 🔴 **这个事件是给激励广告留的**。复活现在无限且免费——`GameState.FreeRevives = 1`
        /// 与 `NextReviveIsFree` 只计数、不拦截（闸没装，见 `TODO.md` 的「复活接激励广告」）。
        /// 闸装上之后 <see cref="Params.ReviveIndex"/> ≥ 2 的那些就是看广告换来的。
        /// 现在埋，接广告时才有 before/after 基线——事后补埋是没有对照组的。
        /// </summary>
        public const string LevelRevive = "level_revive";

        /// <summary>
        /// 玩家从局中回主页（设置列最后那一格）。
        ///
        /// 🔴 **这不是 <see cref="LevelEnd"/>，别拿它当一局的结束。** 回主页**不结束这一次尝试**：
        /// 已玩时长、失败数、复活数都随局中进度快照带回来，回来也**不**发新的
        /// <see cref="LevelStart"/>（`ResumingDoesNotOpenASecondAttemptInAnalytics` 钉着）。
        /// 把它算进 end 会同时污染通过率的分子和分母。
        ///
        /// 它回答的是「玩家在什么状态下中途走人」：<see cref="Params.PlayTimeSeconds"/> 说玩了多久，
        /// <see cref="Params.FailCount"/> 说走之前卡了几次，<c>AnalyticsEvents.Params.ArrowsLeft</c> 说
        /// 盘子解到哪一步了。
        ///
        /// 🔴 **切后台不发这条。** 那是另一回事——Firebase 自带 `session_start` /
        /// `user_engagement`，而且切后台虽然也落盘，玩家并没有"回主页"。
        ///
        /// 🔴 **上报点没有状态闸，这是有理由的**：赢了走 `ShowWonSettlement`、输了走失败分支，
        /// 两条都会 `hudView.SetActionsInteractable(false)`，设置列因此点不到——`OnMenuClicked`
        /// 只可能在**局中**（`GameStatus.Playing`）进来。哪天把 HUD 在那两种状态下放回来，
        /// 这里要重新看一遍：那时才需要闸，现在加就是一道永远不触发的门。
        /// </summary>
        public const string LevelExit = "level_exit";

        /// <summary>
        /// 玩家回来接着打——主页 CONTINUE 装载时，局中进度快照**装上了**。
        ///
        /// 🔴 **它与 <see cref="LevelStart"/> 互斥，且不是它的替身。** 恢复接着打的是**同一次
        /// 尝试**，那次尝试开始时已经报过一条 `level_start` 了；再补一条就把同名事件从
        /// 「一次尝试」改成「一次进入」，而通过率的分母正是它（DECISION-20260901-08 要求
        /// 不产生同名事件的语义静默漂移，完整理由见 <c>AnalyticsEvents.StartReasons</c> 开头那段）。
        /// 所以恢复要有自己的事件名，**不是** `start_reason` 的一个取值。
        ///
        /// 🔴 **它是 <see cref="LevelExit"/> 的镜像**——同样六个参数、同样的口径。于是
        /// 「离开了没回来」＝ 有 exit 没有配对的 resume，直接数得出来；而配得上的那一对，
        /// 两边的 <see cref="Params.PlayTimeSeconds"/> 应当是同一个数（两边都取那份快照里的
        /// 已玩秒数，差别只到浮点往返的毫秒位）。对不上就是存档或计时坏了。
        ///
        /// 🔴 **只在真的装上了才发。** 快照在、但三重身份校验认不出来时这一条**不发**，发的是
        /// 一条带 <c>AnalyticsEvents.StartReasons.ResumeLost</c> 的 `level_start`——那确实是全新一局。
        /// 两者分开，「玩家回来了」与「玩家回来但进度没了」才分得出来。
        ///
        /// 🔴 **切后台再切回来不发这一条**：那时场景还在、控制器还在，压根没有装载。要走到这里
        /// 必须真的离开过游戏场景——回主页，或进程被系统收走之后重新启动。
        /// </summary>
        public const string LevelResume = "level_resume";

        /// <summary>
        /// 玩家用掉一件道具（**用掉**，不是点了按钮）。三件共用这一个事件，
        /// 靠 <see cref="Params.Booster"/> 分档。
        ///
        /// 🔴 **上报点是「真的生效了」那一刻，不是按钮回调的开头**——三件道具是三种形状，
        /// 埋在回调顶部会得到三个各自错法不同的数字：
        /// <list type="bullet">
        /// <item>提示有两个提前 return（用不了 / 盘上没有可提示的箭），且重复点同一支箭
        /// **不重复计数**（`TryApplyHint` 的返回值就是这个意思，判据
        /// `GameControllerStateTests` 已钉住同一支箭第二次返回 false）；</item>
        /// <item>橡皮那颗按钮只是**武装**，再点一次是取消；真正擦掉发生在 `UseEraserOn`；</item>
        /// <item>魔法棒一次消掉**一串**，且 `chain.Count == 0` 时整个动作不发生。</item>
        /// </list>
        ///
        /// 🔴 **代码里这三件叫 `Prop`（`GameState.RemoveForProp` / `CanUsePropNow` /
        /// `propDissolving`），数据侧叫 `booster`——这个分歧是有意的**，别"统一"掉其中一边：
        /// <list type="bullet">
        /// <item>`booster` 是休闲解谜品类里这类道具的行业通名，做投放 / 变现分析的人（包括将来
        /// 外部请的）一看就懂，而 `prop` 在英文里也常是 property 的缩写——GA4 自己把媒体资源
        /// 就叫 property，两个词在报表与 BigQuery 导出里挨着放很容易看岔。</item>
        /// <item>选 `booster` 而不是 `prop` 的判据是 owner 2026-09-01 拍板的"**这三件之后不会是
        /// 免费的**"。booster 在行业里特指要花钱 / 攒资源换的消耗品，这个语义与商业化方向一致。</item>
        /// <item>🔴 **改名的窗口只有发版前**：GA4 的维度参数名一改，旧数据不会跟过来
        /// （`level_complete` → `level_end` 那次就是这个代价），所以现在定死。</item>
        /// </list>
        /// </summary>
        public const string BoosterUsed = "booster_used";

        /// <summary>玩家被判定卡死且引导已展示。什么算卡死由游戏证明（water_sort：严格死局判定）；卡在哪、引导指向什么都是游戏的 extra。</summary>
        public const string LevelStuck = "level_stuck";

        public static class Params
        {
            /// <summary>
            /// 关卡号。🔴 **是玩家看到的产品关号，不是源关号**——`GameController.LoadLevel` 传给
            /// `LoadLevelData` 的是 `level.id`，而解码结果的 `id` 恰恰是被产品关号覆盖过的那个。
            /// （2026-08-31 真机实测：玩家打第 7、8 关，事件报 `level_number=7` / `8`。）
            ///
            /// 🔴 **所以它不能用来判断「哪一关的内容太难」**。配置 1 走 101 同款 DDA，同一个产品关号
            /// 发给不同玩家的源关卡不是同一张；这个字段回答的是「玩家的第几关」（漏斗 / 留存要的正是
            /// 这个），不是「哪张图」。要按内容归因得另报源关号——那是独立的一次取舍，见 `TODO.md`。
            /// </summary>
            public const string LevelNumber = "level_number";

            /// <summary>
            /// 这一局成没成功：<c>1</c> / <c>0</c>。GA4 推荐事件 `level_end` 的规范参数。
            /// 🔴 写成 0/1 的整数而不是布尔——`AnalyticsParameter` 只有字符串 / 整数 / 浮点
            /// 三种值类型（下游 GA4 就认这三种），布尔在这里没有对应形态。
            /// </summary>
            public const string Success = "success";

            /// <summary>
            /// 本次 <see cref="LevelStart"/> 以来失败了几次（首次失败报 1）。
            ///
            /// 成功那条 `level_end` 上它是**通关前失败了几次**（一次没败报 0）——那是比
            /// 「有没有失败过」信息量大得多的难度量：「这关平均失败 2.7 次」和
            /// 「这关 60% 的人失败过」是两个不同的东西。
            ///
            /// 🔴 重开会重置它（重开是新的一次尝试），复活**不**重置（同一次尝试还在继续）。
            /// 被重置掉的那个数不会丢——它进了新一条 `level_start` 的
            /// <see cref="PrevFailCount"/>。
            /// </summary>
            public const string FailCount = "fail_count";

            /// <summary>
            /// 这一次 <see cref="LevelStart"/> 是怎么开始的，取值见 <c>AnalyticsEvents.StartReasons</c>。
            ///
            /// 🔴 全新开局**显式**报 <c>AnalyticsEvents.StartReasons.Fresh</c>，不是省略这个参数：
            /// 「参数没有」和「参数本该有但掉了」在 GA4 里都显示成 `(not set)`，长得一模一样。
            /// 显式给值的话，`fresh` 消失就等于埋点坏了，一眼可见。
            /// </summary>
            public const string StartReason = "start_reason";

            /// <summary>
            /// 被这次重开**放弃掉**的那一次尝试失败了几次（全新开局报 0）。
            ///
            /// 🔴 **刻意不复用 <see cref="FailCount"/> 这个名字**：同名不同义会让「平均失败
            /// 次数」这类聚合把两拨数据混着算——`level_end` 上的是「本次尝试」，这里是
            /// 「上一次」。名字分开，聚合就不会静默串台。
            ///
            /// 🔴 值必须取自装载路径清零**之前**：由 `GameController` 的调用点捕获后传入
            /// （同 <c>AnalyticsEvents.Params.ArrowsLeft</c> 的契约），不在装载里现读——现读恒为 0，
            /// 而恒为 0 在报表上看着完全正常。
            /// </summary>
            public const string PrevFailCount = "prev_fail_count";

            /// <summary>
            /// 这一关第几次复活（首次报 1）。接激励广告后 ≥2 的那些即为看广告换来的。
            /// 与 <see cref="FailCount"/> 一样按 <see cref="LevelStart"/> 重置。
            /// </summary>
            public const string ReviveIndex = "revive_index";

            /// <summary>
            /// 这一次尝试**到此已经玩了多久**（秒，小数三位＝毫秒精度）。
            ///
            /// 🔴 **不是「本关用时」**——它是发事件那一刻现算的 `当前时刻 − 本次 level_start 的时刻`，
            /// 所以关卡没结束也有定义：`booster_used` 上它回答的正是「卡了多久才伸手求助」。
            ///
            /// 由 `ReportLevelEvent` 统一补，不是逐事件加的（逐个加漏掉一个不会有任何东西报错），
            /// 但 🔴 **`level_start` 上不带**：计时原点就是在它那一刻设的，带上恒为 0、是纯占位
            /// （owner 2026-09-01 要求去掉）。所以**除 `level_start` 外的关卡事件都带**——
            /// 🔴 这里刻意不列名单：`ReportLevelEvent` 的判据写成「等于 `LevelStart`」而不是
            /// 白名单，新增事件默认带上，那么在这儿抄一份事件名就只会过期（本段上一版列的
            /// 「三个」正是漏了 `level_exit`）。`theme_changed` 是平事件、不走
            /// `ReportLevelEvent`，更不带。
            ///
            /// 🔴 **单位写在名字里**是有意的：时长字段最经典的事故就是「这个数是秒还是毫秒」，
            /// 而两种读法都说得通、都不会报错。
            ///
            /// 🔴 **量的是「真正在玩的时间」，靠一套显式的停表机制，不是靠时钟本身。**
            /// 两种情况停表：app 切到后台 / 锁屏 / 接电话，以及失败页开着的时候
            /// （owner 2026-09-01 拍板两个都停）。玩家把手机放下 40 分钟回来接着打，
            /// 报的不会是 40 分钟。机制见 `GameController` 的 `PlayClockStop`。
            ///
            /// 🔴 **这里曾经写着「切后台 Unity 循环停了，时钟也就不走」——那是错的，
            /// 别照那个直觉再改回去。** 循环确实停了，但 `Time.unscaledTime` 是在帧边界上
            /// 采样墙上时间累加出来的：不渲染帧不等于不计时，只是把整段攒到恢复后的第一帧
            /// 一次性补上。Unity 文档明写 `unscaledTime` **不**受 `Time.maximumDeltaTime`
            /// 夹制——被那个夹子夹住的是 `Time.deltaTime`，直觉八成是从那儿串过来的。
            ///
            /// 🔴 **起点是这一次 `level_start`**，与 <see cref="FailCount"/> 同一个生命周期：
            /// 重开重置（新的一次尝试），**复活不重置**（同一次尝试还在继续）。
            ///
            /// 🔴 **失败页上犹豫的那段不计**：失败到点「继续游戏」之间玩家盯着失败页想
            /// 「要不要复活」的时间被停表扣掉，所以 `level_revive` 上这个数是**真正在解题的
            /// 时长**，不含做决定花的时间。要看「犹豫了多久才决定复活」得另埋一个参数。
            ///
            /// 🔴 **结算页从来就不在里面**，而且不是靠停表：`level_end(success=1)` 报在
            /// `settlementView.ShowWithButton` **之前**，结算页之后的下一个事件是新一关的
            /// `level_start`（它重置原点、且不带这个参数）。所以那段时间进不了任何事件。
            /// 别为它再加一个停表理由，那是一道永远不会生效的门。
            ///
            /// 🔴 **本参数不登记为 GA4 自定义维度**（owner 2026-09-01：埋点只经 BigQuery 消费）。
            /// BigQuery 导出带的是原始 `event_params`，登不登记都在；而登记成维度反而是错的
            /// ——时长取值无界，会撞上事件范围维度的基数上限、被归进 `(other)`。
            /// 登记脚本里有一条 `BIGQUERY_ONLY` 豁免记着这件事。
            /// </summary>
            public const string PlayTimeSeconds = "play_time_seconds";

            /// <summary>用的哪件道具，取值见 <c>AnalyticsEvents.Boosters</c>。</summary>
            public const string Booster = "booster";

            /// <summary>
            /// 这一次道具**处理掉了几支箭**。提示与橡皮恒为 1，魔法棒是它消掉的那一串的长度。
            ///
            /// 🔴 口径统一成「几支箭」而不是各道具各记各的，三件才可比：
            /// 「橡皮用了 40 次 × 1 支」与「魔法棒用了 3 次 × 8 支」是同一把尺子上的两个数，
            /// 而这正是「哪件道具值钱、该不该收费」要看的东西。
            /// </summary>
            public const string BoosterAmount = "booster_amount";
        }
    }
}
