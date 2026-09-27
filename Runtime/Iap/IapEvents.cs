namespace Tracking.Iap
{
    /// <summary>Purchase diagnostics, deliberately distinct from automatic store revenue events.</summary>
    public static class IapEvents
    {
        public const string Flow = "iap_flow";
        public static class Params
        {
            public const string Action = "iap_action";
            public const string Result = "iap_result";
            public const string ProductId = "iap_product_id";
            public const string FlowId = "iap_flow_id";
            public const string AttemptId = "iap_attempt_id";
            public const string Source = "iap_source";
            public const string PriceLabel = "iap_price_label";
            public const string Owned = "iap_owned";
            public const string PreviousOwned = "iap_previous_owned";
            public const string StoreStatus = "iap_store_status";
        }
        public static class Actions
        {
            public const string EntryClick = "entry_click";
            public const string PanelView = "panel_view";
            public const string PanelClose = "panel_close";
            public const string PurchaseStart = "purchase_start";
            public const string PurchaseResult = "purchase_result";
            public const string RestoreStart = "restore_start";
            public const string RestoreResult = "restore_result";
            public const string EntitlementChange = "entitlement_change";
        }
    }
}
