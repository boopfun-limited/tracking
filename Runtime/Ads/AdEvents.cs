namespace Tracking.Ads
{
    /// <summary>Shared ad vocabulary. Placements, rewards and business reasons belong to the host.</summary>
    public static class AdEvents
    {
        public const string Opportunity = "ad_opportunity";
        public const string Decision = "ad_decision";
        public const string Request = "ad_request";
        public const string Fill = "ad_fill";
        public const string ShowRequest = "ad_show_request";
        public const string ShowResult = "ad_show_result";
        public const string Clicked = "ad_clicked";
        public const string Closed = "ad_closed";
        public const string RewardEarned = "ad_reward_earned";
        public const string RewardResult = "ad_reward_result";
        public const string Impression = "ad_impression";
        public static class Params
        {
            public const string FlowId = "ad_flow_id";
            public const string RequestId = "request_id";
            public const string Format = "ad_format";
            public const string Placement = "ad_placement";
            public const string Source = "ad_source";
            public const string Unit = "ad_unit_name";
            public const string Platform = "ad_platform";
            public const string Result = "result";
            public const string Reason = "reason";
            public const string ErrorCode = "error_code";
            public const string DurationMs = "duration_ms";
            public const string RewardEarned = "reward_earned";
            public const string Value = "value";
            public const string Currency = "currency";
            public const string LoadOrigin = "load_origin";
        }
        public static class Results
        {
            public const string Success = "success";
            public const string Failure = "failure";
            public const string Show = "show";
            public const string Free = "free";
            public const string Skip = "skip";
            public const string Granted = "granted";
            public const string NotGranted = "not_granted";
        }
        public static class Formats
        {
            public const string Interstitial = "INTER";
            public const string Rewarded = "REWARDED";
            public const string Banner = "BANNER";
        }
    }
}
