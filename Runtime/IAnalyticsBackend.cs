namespace LevelTracking
{
    /// <summary>
    /// 埋点上报抽象。存在的理由：把 Firebase 那套
    /// 关在 `LevelTracking.Android.FirebaseAnalyticsBackend` 一个文件里，好让「什么时候报、
    /// 报哪个事件、带哪些参数」这些**会写错的判定**留在纯 C# 里、在 EditMode 快车道可测。
    ///
    /// 🔴 `LevelTracking.Android` 是 `includePlatforms: ["Android"]`，
    /// EditMode **根本不编译它**——写进那个程序集的任何判定，测试全绿也证明不了它对。
    /// </summary>
    public interface IAnalyticsBackend
    {
        /// <summary>
        /// 报一次事件。**实现方不得抛异常**：埋点是旁路，不能让一次正常的通关崩在这里。
        ///
        /// 事件名与参数名需满足 GA4 的约束（≤40 字符、字母开头、只含字母数字下划线，
        /// 且不得用 `firebase_` / `google_` / `ga_` 前缀）。🔴 违规的事件 Firebase **静默丢弃**，
        /// 不报错也不进 DebugView——所以事件名请写成常量，不要在调用点拼字符串。
        /// </summary>
        void LogEvent(string eventName, params AnalyticsParameter[] parameters);
    }
}
