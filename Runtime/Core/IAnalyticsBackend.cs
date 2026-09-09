namespace Tracking
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

        /// <summary>
        /// 设一次用户属性；此后报的每一条事件都会带上它，直到被改写或清掉（<paramref name="value"/>
        /// 传 null 即清掉）。在 BigQuery 导出里落在 `user_properties` 数组，**不需要**去 GA4 界面
        /// 注册自定义维度——注册只影响界面报表，导出是原样过来的。
        ///
        /// 🔴 GA4 对属性的上限比事件参数紧得多：**属性名 ≤24 字符、值 ≤36 字符**，
        /// 每个媒体资源至多 25 个自定义属性。超限 Firebase **静默丢弃**，不报错也不进 DebugView——
        /// 症状是「join 的时候发现这一列大面积为空」，而那时数据已经攒了几个月、补不回来。
        /// 所以属性名写成常量，值的长度在接线处就要有数。
        ///
        /// **实现方不得抛异常**，理由同 <see cref="LogEvent"/>。
        /// </summary>
        void SetUserProperty(string name, string value);

        /// <summary>
        /// 设 GA4 的 `user_id`；此后报的每一条事件都带它，直到被改写或清掉（<paramref name="userId"/>
        /// 传 null 即清掉）。在 BigQuery 导出里落在顶层列 `user_id`（与 `user_pseudo_id` 并列），
        /// `users_*` 日表按它一人一行。**它放的是游戏自己生成、跟着存档走的安装标识**——
        /// 备份还原把存档带回来时它也回来，而 `user_pseudo_id` 不会，所以事件历史跟着「这份存档」。
        ///
        /// 🔴 Google 的 Analytics 政策禁止把能识别到个人的值放进来（邮箱、Google 账号 ID 都不行）；
        /// 值 ≤256 字符。和 <see cref="SetUserProperty"/> 一样只影响**此后**的事件——首会话里早于它的
        /// `first_open` / `session_start` 不带，要在 BigQuery 里按 `user_pseudo_id` 回填。
        ///
        /// **实现方不得抛异常**，理由同 <see cref="LogEvent"/>。
        /// </summary>
        void SetUserId(string userId);
    }
}
