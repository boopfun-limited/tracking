namespace Tracking.DailyChallenge
{
    /// <summary>
    /// 每日挑战的事件名与参数名，库拥有（D-20260925-01）。🔴 **改这里的名字 = 破坏性升版本**，同
    /// <c>LevelTrackingEvents</c>：GA4 的名字一旦发出就进了 property 的字典，改名等于新开一个事件、历史不跟。
    ///
    /// 取值集合归游戏：<see cref="Params.Source"/> 的入口名、<see cref="Params.Tier"/> 的奖励档名、<see cref="Params.ShareMethod"/> 的渠道名、
    /// <see cref="Params.SaveResult"/> 的结果名各游戏自己定。
    /// 每日挑战那一局本身不另起事件——关卡事件照走 <c>LevelTracker</c>，由游戏把
    /// <see cref="DailyChallengeTracker.LevelContext"/> 拼进它的公共参数。
    /// </summary>
    public static class DailyChallengeEvents
    {
        /// <summary>
        /// 玩家**从外面进入**每日挑战（底栏页签、首页卡片……），带 <see cref="Params.Source"/>。
        /// 🔴 每日挑战内部来回不发（对局回日历、奖杯室回日历）：它数的是「进来了几次、从哪进来」，不是页面展示次数。
        /// 入口直接开今天那局、不经过日历的，也在点入口那一刻发。
        /// </summary>
        public const string Open = "dc_open";

        /// <summary>
        /// 一局赢下后当月做满的天数跨过了一档奖励，带 <see cref="Params.Month"/> 与 <see cref="Params.Tier"/>。
        /// 🔴 报在**跨档那一刻**，不是领奖弹窗：弹窗往往要等回日历播完动画才出，玩家可能等不到。
        /// 补做往月也会跨那个月的档——事件日期晚于 <see cref="Params.Month"/> 的就是补出来的。
        /// </summary>
        public const string RewardUnlocked = "dc_reward_unlocked";

        /// <summary>
        /// 玩家点每日挑战的提醒打开 / 切回了游戏，带 <see cref="Params.ReminderIndex"/>。
        /// 🔴 同一次点击只报一次：怎么从系统拿到「是点哪条通知进来的」、怎么去重，是游戏的通知适配层的事。
        /// </summary>
        public const string ReminderOpen = "dc_reminder_open";

        /// <summary>
        /// 玩家打开了某个月奖杯的分享页，带 <see cref="Params.Month"/> 与 <see cref="Params.Tier"/>（D-20260925-02）。
        /// 与 <see cref="Share"/> 一起算「打开分享页后有多少人真的选了渠道」。
        /// </summary>
        public const string ShareOpen = "dc_share_open";

        /// <summary>
        /// 玩家在分享页选了一个渠道、图已交出去，带 <see cref="Params.Month"/>、<see cref="Params.Tier"/>、<see cref="Params.ShareMethod"/>。
        /// 🔴 报的是「交出去了」，不是「发出去了」：交给系统分享面板或别的应用之后发没发，游戏拿不到。
        /// </summary>
        public const string Share = "dc_share";

        /// <summary>
        /// 玩家把某个月的奖杯图存进相册，报存的结果，带 <see cref="Params.Month"/>、<see cref="Params.Tier"/>、<see cref="Params.SaveResult"/>。
        /// 失败也报：存相册的路径随系统版本分叉，线上失败率只能从这里看。
        /// </summary>
        public const string Save = "dc_save";

        public static class Params
        {
            /// <summary>这一局是哪一天的每日挑战，long <c>yyyymmdd</c>（20260925）。只在关卡事件上。</summary>
            public const string Date = "dc_date";

            /// <summary>
            /// 发事件那一刻，<see cref="Date"/> 离本机的「今天」几天：0 = 今天的题，&gt;0 = 补做。只在关卡事件上。
            /// 按本地日期算整天，与钟点无关；跨零点接着打的那一局，零点后的事件会变成 1。本机日期往回拨时可以为负。
            /// </summary>
            public const string DaysAgo = "dc_days_ago";

            /// <summary><see cref="Open"/> 的入口，取值归游戏（sudoku：<c>tab</c> / <c>home_card</c>）。</summary>
            public const string Source = "dc_source";

            /// <summary>奖励属于哪个月，long <c>yyyymm</c>（202609）。</summary>
            public const string Month = "dc_month";

            /// <summary>
            /// 那一档，取值归游戏（sudoku：<c>bronze</c> / <c>silver</c> / <c>gold</c>）：<see cref="RewardUnlocked"/> 是跨到的档，
            /// 分享 / 存图是那个月奖杯当前的档。
            /// </summary>
            public const string Tier = "dc_tier";

            /// <summary>点的是离开后排的第几条提醒（1 起）；「第几条」怎么排由游戏定（sudoku：离开后第 N 天那条）。</summary>
            public const string ReminderIndex = "dc_reminder_index";

            /// <summary>
            /// <see cref="Share"/> 的渠道，取值归游戏（sudoku：<c>facebook</c> 交给 Facebook 应用 / <c>facebook_fallback</c> 点了 Facebook 但没装、
            /// 改开了系统分享面板 / <c>more</c> 系统分享面板）。
            /// </summary>
            public const string ShareMethod = "dc_share_method";

            /// <summary><see cref="Save"/> 的结果，取值归游戏（sudoku：<c>ok</c> / <c>failed</c> / <c>denied</c> 没给存储权限）。</summary>
            public const string SaveResult = "dc_save_result";
        }
    }
}
