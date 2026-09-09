namespace LevelTracking
{
    /// <summary>
    /// <see cref="LevelTracker"/> 的一个发射方法对应表里的一行：它发哪个事件、带不带
    /// <c>play_time_seconds</c>、以及除公共参数之外还带哪些**事件专属**参数。
    /// </summary>
    public readonly struct MethodSpec
    {
        public MethodSpec(string method, string eventName, bool playTime, params string[] parameters)
        {
            Method = method;
            Event = eventName;
            PlayTime = playTime;
            Parameters = parameters ?? System.Array.Empty<string>();
        }

        /// <summary><see cref="LevelTracker"/> 上的方法名。</summary>
        public string Method { get; }

        public string Event { get; }

        /// <summary>false 只有原点那一行：<c>level_start</c> 是计时原点本身，带上恒为 0。</summary>
        public bool PlayTime { get; }

        /// <summary>事件专属参数（不含 <see cref="LevelTrackingSchema.CommonParams"/> 与游戏自己盖的）。</summary>
        public string[] Parameters { get; }
    }

    /// <summary>
    /// 方法 → 事件 → 标准参数的**机器真源**，包内唯一一份。
    ///
    /// 🔴 两边同时读它：包内测试 <c>LevelTrackerEmitsExactlyItsSchema</c> 用记录型 backend
    /// 反射调每个公开方法，断言发出的参数集合 == 这里的那一行（表 == 行为）；游戏侧的
    /// <c>analytics_doc.py</c> 按钉住的 SHA 读这个文件、按调用点的方法名找行（文档 == 表）。
    /// 所以 <see cref="Methods"/> 的形状是固定的：**一行一个发射方法**，
    /// <c>new MethodSpec(nameof(LevelTracker.X), LevelTrackingEvents.Y, playTime: bool, Params…)</c>，
    /// 工具只认这一种写法，认不出就拒绝执行——别在这里写表达式、条件或循环。
    /// </summary>
    public static class LevelTrackingSchema
    {
        /// <summary>库给每条关卡事件都盖的公共参数；游戏自己的公共参数经 <c>gameCommons</c> 传入，不在这里。</summary>
        public static readonly string[] CommonParams =
        {
            LevelTrackingEvents.Params.LevelNumber,
            LevelTrackingEvents.Params.PlayTimeSeconds,
        };

        public static readonly MethodSpec[] Methods =
        {
            new MethodSpec(nameof(LevelTracker.LevelStart), LevelTrackingEvents.LevelStart, playTime: false, LevelTrackingEvents.Params.StartReason),
            new MethodSpec(nameof(LevelTracker.LevelStartWithPrevFails), LevelTrackingEvents.LevelStart, playTime: false, LevelTrackingEvents.Params.StartReason, LevelTrackingEvents.Params.PrevFailCount),
            new MethodSpec(nameof(LevelTracker.LevelEnd), LevelTrackingEvents.LevelEnd, playTime: true, LevelTrackingEvents.Params.Success),
            new MethodSpec(nameof(LevelTracker.LevelEndWithFailCount), LevelTrackingEvents.LevelEnd, playTime: true, LevelTrackingEvents.Params.Success, LevelTrackingEvents.Params.FailCount),
            new MethodSpec(nameof(LevelTracker.LevelRevive), LevelTrackingEvents.LevelRevive, playTime: true, LevelTrackingEvents.Params.ReviveIndex),
            new MethodSpec(nameof(LevelTracker.LevelExit), LevelTrackingEvents.LevelExit, playTime: true),
            new MethodSpec(nameof(LevelTracker.LevelResume), LevelTrackingEvents.LevelResume, playTime: true),
            new MethodSpec(nameof(LevelTracker.BoosterUsed), LevelTrackingEvents.BoosterUsed, playTime: true, LevelTrackingEvents.Params.Booster, LevelTrackingEvents.Params.BoosterAmount),
            new MethodSpec(nameof(LevelTracker.LevelStuck), LevelTrackingEvents.LevelStuck, playTime: true),
        };
    }
}
