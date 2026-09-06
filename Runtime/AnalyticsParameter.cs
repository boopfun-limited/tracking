namespace LevelTracking
{
    /// <summary>
    /// 一个埋点参数。**刻意只有三种值类型**——字符串 / 整数 / 浮点，因为下游 GA4 就只认这三种，
    /// 多造一种（枚举、布尔、时间戳）都要在后端再拍一次「它到底存成什么」，而那个决定
    /// 一旦分散到各个调用点就再也收不回来了。布尔请显式写成 0/1 的 <see cref="long"/>，
    /// 时间戳写成毫秒 <see cref="long"/>。
    /// </summary>
    public readonly struct AnalyticsParameter
    {
        public enum ValueKind
        {
            String = 0,
            Long = 1,
            Double = 2,
        }

        private AnalyticsParameter(string name, ValueKind kind, string stringValue, long longValue, double doubleValue)
        {
            Name = name;
            Kind = kind;
            StringValue = stringValue;
            LongValue = longValue;
            DoubleValue = doubleValue;
        }

        public string Name { get; }

        public ValueKind Kind { get; }

        /// <summary>仅当 <see cref="Kind"/> 为 <see cref="ValueKind.String"/> 时有意义。</summary>
        public string StringValue { get; }

        /// <summary>仅当 <see cref="Kind"/> 为 <see cref="ValueKind.Long"/> 时有意义。</summary>
        public long LongValue { get; }

        /// <summary>仅当 <see cref="Kind"/> 为 <see cref="ValueKind.Double"/> 时有意义。</summary>
        public double DoubleValue { get; }

        public static AnalyticsParameter Of(string name, string value)
        {
            return new AnalyticsParameter(name, ValueKind.String, value, 0L, 0d);
        }

        public static AnalyticsParameter Of(string name, long value)
        {
            return new AnalyticsParameter(name, ValueKind.Long, null, value, 0d);
        }

        public static AnalyticsParameter Of(string name, double value)
        {
            return new AnalyticsParameter(name, ValueKind.Double, null, 0L, value);
        }
    }
}
