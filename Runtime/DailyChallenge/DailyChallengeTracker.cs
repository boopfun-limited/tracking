using System;

namespace Tracking.DailyChallenge
{
    /// <summary>
    /// 每日挑战的埋点门面（D-20260925-01）。每个方法发什么，见 <see cref="DailyChallengeEvents"/> 各事件的口径。
    /// 不依赖关卡模块：每日挑战那一局的关卡事件由游戏把 <see cref="LevelContext"/> 拼进 <c>LevelTracker</c> 的公共参数。
    /// </summary>
    public sealed class DailyChallengeTracker
    {
        private readonly IAnalyticsBackend backend;

        /// <param name="backend">上报口；实现方不得抛。</param>
        public DailyChallengeTracker(IAnalyticsBackend backend)
        {
            this.backend = backend ?? throw new ArgumentNullException(nameof(backend));
        }

        /// <summary>
        /// 盖在每日挑战那一局的每条关卡事件上：哪一天的题、离今天几天。<paramref name="today"/> 是游戏的「今天」
        /// （本机本地日期），两个参数都只取日期部分。
        /// </summary>
        public static AnalyticsParameter[] LevelContext(DateTime date, DateTime today)
        {
            return new[]
            {
                AnalyticsParameter.Of(DailyChallengeEvents.Params.Date, date.Year * 10000L + date.Month * 100 + date.Day),
                AnalyticsParameter.Of(DailyChallengeEvents.Params.DaysAgo, (today.Date - date.Date).Days),
            };
        }

        public void Open(string source)
        {
            backend.LogEvent(DailyChallengeEvents.Open,
                AnalyticsParameter.Of(DailyChallengeEvents.Params.Source, source));
        }

        /// <param name="month">奖励属于的那个月，只取年月。</param>
        public void RewardUnlocked(DateTime month, string tier)
        {
            backend.LogEvent(DailyChallengeEvents.RewardUnlocked,
                AnalyticsParameter.Of(DailyChallengeEvents.Params.Month, month.Year * 100L + month.Month),
                AnalyticsParameter.Of(DailyChallengeEvents.Params.Tier, tier));
        }

        /// <param name="index">第几条提醒，1 起。</param>
        public void ReminderOpened(int index)
        {
            backend.LogEvent(DailyChallengeEvents.ReminderOpen,
                AnalyticsParameter.Of(DailyChallengeEvents.Params.ReminderIndex, index));
        }
    }
}
