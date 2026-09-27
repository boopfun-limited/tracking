using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Tracking.AppsFlyer
{
    /// <summary>
    /// 游戏把 AppsFlyer Unity 插件一比一转接成这几个调用，只转发、不判断。
    ///
    /// 🔴 包不直接引插件：插件以源码装在各游戏的 `Assets/AppsFlyer` 里，而有的消费方根本没装
    /// （arrows-3d / boopdoku / ball-sort），包里一引，它们的 Android 包就编不过。
    /// 聚合平台（`MediationNetwork`）与 dev key 是游戏的事，所以留在转接层。
    /// </summary>
    public interface IAppsFlyerSdk
    {
        /// <summary><c>AppsFlyer.initSDK</c>。</summary>
        void Init(string devKey);

        /// <summary><c>AppsFlyer.setCustomerUserId</c>。</summary>
        void SetCustomerUserId(string id);

        /// <summary><c>AppsFlyer.startSDK</c>。</summary>
        void Start();

        /// <summary><c>AppsFlyer.getAppsFlyerId</c>。</summary>
        string GetAppsFlyerId();

        /// <summary><c>AppsFlyer.logAdRevenue(new AFAdRevenueData(network, 聚合平台, currency, revenue), extra)</c>。</summary>
        void LogAdRevenue(string network, double revenue, string currency, Dictionary<string, string> extra);

        /// <summary><c>AppsFlyer.sendEvent</c>。</summary>
        void SendEvent(string eventName, Dictionary<string, string> values);
    }

    /// <summary>
    /// AppsFlyer（MMP）归因的机制：起 SDK 的顺序、安装标识作 CUID、AFID 用户属性、广告收入与内购收入的参数和去重。
    /// 游戏只给 dev key 和一个 <see cref="IAppsFlyerSdk"/> 转接层。
    ///
    /// 🔴 所有调用失败都静默降级、不抛：起 SDK 跑在 `BeforeSceneLoad` 的装配里，抛出去等于因为归因起不来而黑屏；
    /// 收入回传是旁路，丢一条也不该影响玩游戏。没起来（管理员包不调 <see cref="Start"/>、或起 SDK 抛了）的会话一条都不发。
    /// </summary>
    public sealed class AppsFlyerTracker
    {
        /// <summary>AFID 在 GA4 里的用户属性名（≤24 字符，超了 GA4 静默丢）。</summary>
        public const string AppsFlyerIdUserProperty = "appsflyer_id";

        /// <summary>按商品记「报过的最后一笔交易号」的 PlayerPrefs 键前缀。</summary>
        public const string ReportedPurchaseKeyPrefix = "gthbj.tracking.af_purchase.";

        private readonly IAppsFlyerSdk sdk;

        public AppsFlyerTracker(IAppsFlyerSdk sdk)
        {
            this.sdk = sdk ?? throw new ArgumentNullException(nameof(sdk));
        }

        public bool Started { get; private set; }

        /// <summary>
        /// 起 SDK。🔴 CUID（= <see cref="InstallId"/> = GA4 `user_id`）必须在 `Start` 之前设：AF 只给设了之后的记录带它，
        /// 装机那条最要紧；AF 原始数据的 `customer_user_id` 因此能和 GA4 直接 join。
        /// 传了 <paramref name="analytics"/> 就把 AFID 挂成用户属性（反向键）；不传不挂。
        /// </summary>
        public void Start(string devKey, IAnalyticsBackend analytics = null)
        {
            try
            {
                sdk.Init(devKey);
                sdk.SetCustomerUserId(InstallId.GetOrCreate());
                sdk.Start();
                Started = true;

                // AFID 是 SDK 本地生成的，Start 之后立刻可读。空值不设：设空串等于把属性清掉。
                // `first_open` 与首个 `session_start` 早于这一刻发出，那两条事件因此不带这个属性——时序事实不是 bug；
                // 要把装机归到人头上，在 BigQuery 里按 `user_pseudo_id` 用同设备的后续事件回填。
                var appsFlyerId = analytics == null ? null : sdk.GetAppsFlyerId();
                if (!string.IsNullOrEmpty(appsFlyerId))
                {
                    analytics.SetUserProperty(AppsFlyerIdUserProperty, appsFlyerId);
                }
            }
            catch (Exception error)
            {
                Debug.LogWarning($"[AppsFlyer] 起 SDK 失败，本次会话不归因：{error.Message}");
            }
        }

        /// <summary>
        /// 一次广告展示的收入（MAX 回报的收入恒为美元）。负数 / NaN / 无穷不报、零照报：MAX 拿不到收入字段时填 -1。
        /// 附加参数只带单元、格式、国家，空值不放（插件把字典逐项塞进 Java 的 HashMap，空串只会占一列废值）。
        /// 🔴 不带广告位与关号：隐私政策写的是「网络、格式、单元、国家」，加字段要先改政策。
        /// </summary>
        public void LogAdRevenue(string network, double revenue, string adUnitId, string adFormat, string countryCode)
        {
            if (!Started || !(revenue >= 0d) || double.IsInfinity(revenue))
            {
                return;
            }

            // 键名是插件 `AdRevenueScheme` 的 AD_UNIT / AD_TYPE / COUNTRY。
            var extra = new Dictionary<string, string>();
            PutIfPresent(extra, "ad_unit", adUnitId);
            PutIfPresent(extra, "ad_type", adFormat);
            PutIfPresent(extra, "country", countryCode);
            try
            {
                sdk.LogAdRevenue(network, revenue, "USD", extra);
            }
            catch (Exception error)
            {
                Debug.LogWarning($"[AppsFlyer] 广告收入上报失败：{error.Message}");
            }
        }

        /// <summary>
        /// 一笔新付款的内购：标准事件 `af_purchase`，带商品、商店标价与本币（AppsFlyer 自己折美元）。
        /// 只接购买包 `PurchaseService.Paid`——恢复购买与待付款不触发它。
        ///
        /// 🔴 一笔交易只报一次：确认没落地的订单商店会重投，`Paid` 就会对同一交易号再响，这里按商品记住报过的交易号。
        /// 🔴 缺交易号 / 价格 / 币种不报：AF 缺币种按美元计，本币标价会被当成美元。
        /// 🔴 交易号只用来去重、不发出去（`af_order_id` 不填）：隐私政策写着不上传交易号、购买令牌与收据。
        /// 报的是标价：没扣商店分成与税；许可测试 / 优惠码那种实付 0 的单子也按标价报。
        /// </summary>
        public void LogPurchase(string productId, string transactionId, decimal price, string currencyCode)
        {
            if (!Started || string.IsNullOrEmpty(transactionId) || price <= 0m || string.IsNullOrEmpty(currencyCode))
            {
                return;
            }

            var key = ReportedPurchaseKeyPrefix + productId;
            if (PlayerPrefs.GetString(key, string.Empty) == transactionId)
            {
                return;
            }

            try
            {
                sdk.SendEvent("af_purchase", new Dictionary<string, string>
                {
                    ["af_content_id"] = productId,
                    ["af_revenue"] = price.ToString(CultureInfo.InvariantCulture),
                    ["af_currency"] = currencyCode,
                });
            }
            catch (Exception error)
            {
                // 没交出去就不记，这笔交易下次重投时还能再报。
                Debug.LogWarning($"[AppsFlyer] 内购收入上报失败：{error.Message}");
                return;
            }

            PlayerPrefs.SetString(key, transactionId);
            PlayerPrefs.Save();
        }

        private static void PutIfPresent(Dictionary<string, string> map, string key, string value)
        {
            if (!string.IsNullOrEmpty(value))
            {
                map[key] = value;
            }
        }
    }
}
