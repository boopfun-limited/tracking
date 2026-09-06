using System;

namespace LevelTracking
{
    /// <summary>
    /// 关卡事件的门面：每条关卡事件统一盖 <c>level_number</c> + 游戏自己的公共参数 +
    /// <c>play_time_seconds</c>（原点 <c>level_start</c> 不带），事件专属参数按方法签名给。
    ///
    /// 🔴 带可选参数的变体是**不同的方法名**（<see cref="LevelStartWithPrevFails"/> /
    /// <see cref="LevelEndWithFailCount"/>），不是重载、不是 <c>int?</c>：游戏侧的文档门按
    /// 调用点的**方法名**判「这个游戏发不发它」——带 <c>params</c> 的重载靠数实参个数分不开
    /// （<c>LevelEnd(l, ok, failCount)</c> 与 <c>LevelEnd(l, ok, Of(moves, n))</c> 同为 3 元）。
    /// 每个方法发什么，真源是 <see cref="LevelTrackingSchema.Methods"/>，包内测试钉着表 == 行为。
    /// </summary>
    public sealed class LevelTracker
    {
        private static readonly AnalyticsParameter[] NoParameters = Array.Empty<AnalyticsParameter>();

        private readonly IAnalyticsBackend backend;
        private readonly PlayClock clock;
        private readonly Func<AnalyticsParameter[]> gameCommons;

        /// <param name="backend">上报口；实现方不得抛。</param>
        /// <param name="clock">这一次尝试的计时（<see cref="PlayClock.ElapsedSeconds"/> 进 <c>play_time_seconds</c>）。</param>
        /// <param name="gameCommons">
        /// 游戏自己盖在**每条关卡事件**上的参数（arrows 的 <c>source_level</c> / <c>level_corpus</c>），
        /// 每次事件现算；没有就传 null。平事件（<see cref="Flat"/>）不盖。
        /// </param>
        public LevelTracker(IAnalyticsBackend backend, PlayClock clock, Func<AnalyticsParameter[]> gameCommons)
        {
            this.backend = backend ?? throw new ArgumentNullException(nameof(backend));
            this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
            this.gameCommons = gameCommons;
        }

        public void LevelStart(int levelNumber, string startReason, params AnalyticsParameter[] extra)
        {
            Emit(LevelTrackingEvents.LevelStart, false, levelNumber, extra,
                AnalyticsParameter.Of(LevelTrackingEvents.Params.StartReason, startReason));
        }

        public void LevelStartWithPrevFails(int levelNumber, string startReason, int prevFailCount, params AnalyticsParameter[] extra)
        {
            Emit(LevelTrackingEvents.LevelStart, false, levelNumber, extra,
                AnalyticsParameter.Of(LevelTrackingEvents.Params.StartReason, startReason),
                AnalyticsParameter.Of(LevelTrackingEvents.Params.PrevFailCount, prevFailCount));
        }

        public void LevelEnd(int levelNumber, bool success, params AnalyticsParameter[] extra)
        {
            Emit(LevelTrackingEvents.LevelEnd, true, levelNumber, extra,
                AnalyticsParameter.Of(LevelTrackingEvents.Params.Success, success ? 1L : 0L));
        }

        public void LevelEndWithFailCount(int levelNumber, bool success, int failCount, params AnalyticsParameter[] extra)
        {
            Emit(LevelTrackingEvents.LevelEnd, true, levelNumber, extra,
                AnalyticsParameter.Of(LevelTrackingEvents.Params.Success, success ? 1L : 0L),
                AnalyticsParameter.Of(LevelTrackingEvents.Params.FailCount, failCount));
        }

        public void LevelRevive(int levelNumber, int reviveIndex, params AnalyticsParameter[] extra)
        {
            Emit(LevelTrackingEvents.LevelRevive, true, levelNumber, extra,
                AnalyticsParameter.Of(LevelTrackingEvents.Params.ReviveIndex, reviveIndex));
        }

        public void LevelExit(int levelNumber, params AnalyticsParameter[] extra)
        {
            Emit(LevelTrackingEvents.LevelExit, true, levelNumber, extra);
        }

        public void LevelResume(int levelNumber, params AnalyticsParameter[] extra)
        {
            Emit(LevelTrackingEvents.LevelResume, true, levelNumber, extra);
        }

        public void BoosterUsed(int levelNumber, string booster, int amount, params AnalyticsParameter[] extra)
        {
            Emit(LevelTrackingEvents.BoosterUsed, true, levelNumber, extra,
                AnalyticsParameter.Of(LevelTrackingEvents.Params.Booster, booster),
                AnalyticsParameter.Of(LevelTrackingEvents.Params.BoosterAmount, amount));
        }

        /// <summary>平事件直通：不盖任何公共参数。事件名请传游戏的常量，游戏侧的门只认常量。</summary>
        public void Flat(string eventName, params AnalyticsParameter[] parameters)
        {
            backend.LogEvent(eventName, parameters ?? NoParameters);
        }

        private void Emit(string eventName, bool carriesPlayTime, int levelNumber,
            AnalyticsParameter[] extra, params AnalyticsParameter[] standard)
        {
            var commons = gameCommons?.Invoke() ?? NoParameters;
            extra = extra ?? NoParameters;
            var count = 1 + commons.Length + (carriesPlayTime ? 1 : 0) + standard.Length + extra.Length;
            var parameters = new AnalyticsParameter[count];
            var index = 0;
            parameters[index++] = AnalyticsParameter.Of(LevelTrackingEvents.Params.LevelNumber, levelNumber);
            commons.CopyTo(parameters, index);
            index += commons.Length;
            if (carriesPlayTime)
            {
                // 三位小数＝毫秒精度，够用且不带浮点噪声（arrows 2026-09-01 起的口径）。
                parameters[index++] = AnalyticsParameter.Of(
                    LevelTrackingEvents.Params.PlayTimeSeconds,
                    Math.Round((double)clock.ElapsedSeconds, 3));
            }
            standard.CopyTo(parameters, index);
            index += standard.Length;
            extra.CopyTo(parameters, index);
            backend.LogEvent(eventName, parameters);
        }
    }
}
