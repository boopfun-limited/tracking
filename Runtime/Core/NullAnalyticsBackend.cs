namespace Tracking
{
    /// <summary>
    /// 什么都不做的埋点后端。编辑器与任何没有真实上报通道的宿主用它，
    /// 这样调用方不必到处判平台或判 null。
    /// </summary>
    public sealed class NullAnalyticsBackend : IAnalyticsBackend
    {
        public static readonly NullAnalyticsBackend Instance = new NullAnalyticsBackend();

        public void LogEvent(string eventName, params AnalyticsParameter[] parameters)
        {
        }

        public void SetUserProperty(string name, string value)
        {
        }
    }
}
