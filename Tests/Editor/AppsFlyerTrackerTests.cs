using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NUnit.Framework;
using Tracking;
using Tracking.AppsFlyer;
using UnityEngine;
using UnityEngine.TestTools;

namespace LevelTracking.Tests.EditMode
{
    /// <summary>跑在编辑器自己的 PlayerPrefs 上，前后都清掉本测试碰的键（安装标识与一个商品的去重键），不碰别的。</summary>
    public sealed class AppsFlyerTrackerTests
    {
        private const string PurchaseKey = AppsFlyerTracker.ReportedPurchaseKeyPrefix + "remove_ads";

        private sealed class Sdk : IAppsFlyerSdk
        {
            public readonly List<string> Calls = new List<string>();
            public bool FailStart;
            public bool FailSend;

            public void Init(string devKey) => Calls.Add("init:" + devKey);

            public void SetCustomerUserId(string id) => Calls.Add("cuid:" + id);

            public void Start()
            {
                if (FailStart)
                {
                    throw new InvalidOperationException("boom");
                }

                Calls.Add("start");
            }

            public string GetAppsFlyerId() => "af-1";

            public void LogAdRevenue(string network, double revenue, string currency, Dictionary<string, string> extra) =>
                Calls.Add($"ad:{network}:{revenue.ToString(CultureInfo.InvariantCulture)}:{currency}:{Join(extra)}");

            public void SendEvent(string eventName, Dictionary<string, string> values)
            {
                if (FailSend)
                {
                    throw new InvalidOperationException("boom");
                }

                Calls.Add(eventName + ":" + Join(values));
            }

            private static string Join(Dictionary<string, string> map) =>
                string.Join(",", map.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => pair.Key + "=" + pair.Value));
        }

        private sealed class Properties : IAnalyticsBackend
        {
            public readonly Dictionary<string, string> Values = new Dictionary<string, string>();

            public void LogEvent(string eventName, params AnalyticsParameter[] parameters)
            {
            }

            public void SetUserProperty(string name, string value) => Values[name] = value;
        }

        private Sdk sdk;
        private AppsFlyerTracker tracker;

        [SetUp]
        public void Setup()
        {
            PlayerPrefs.DeleteKey(InstallId.Key);
            PlayerPrefs.DeleteKey(PurchaseKey);
            sdk = new Sdk();
            tracker = new AppsFlyerTracker(sdk);
        }

        [TearDown]
        public void Cleanup()
        {
            PlayerPrefs.DeleteKey(InstallId.Key);
            PlayerPrefs.DeleteKey(PurchaseKey);
        }

        [Test]
        public void StartSetsTheInstallIdAsCustomerUserIdBeforeStartingAndTagsTheAppsFlyerId()
        {
            var analytics = new Properties();

            tracker.Start("key", analytics);

            Assert.That(sdk.Calls, Is.EqualTo(new[] { "init:key", "cuid:" + InstallId.GetOrCreate(), "start" }));
            Assert.That(analytics.Values, Is.EqualTo(new Dictionary<string, string> { [AppsFlyerTracker.AppsFlyerIdUserProperty] = "af-1" }));
        }

        [Test]
        public void NothingIsSentBeforeStartOrAfterAFailedStart()
        {
            tracker.LogAdRevenue("unity", 0.01, "unit", "INTER", "US");
            tracker.LogPurchase("remove_ads", "tx-1", 9.99m, "USD");
            sdk.FailStart = true;
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(@"^\[AppsFlyer\] 起 SDK 失败"));
            tracker.Start("key");
            tracker.LogAdRevenue("unity", 0.01, "unit", "INTER", "US");
            tracker.LogPurchase("remove_ads", "tx-1", 9.99m, "USD");

            Assert.That(tracker.Started, Is.False);
            Assert.That(sdk.Calls.Where(call => call.StartsWith("ad:") || call.StartsWith("af_purchase")), Is.Empty);
        }

        [Test]
        public void AdRevenueDropsInvalidValuesKeepsZeroAndOmitsEmptyExtras()
        {
            tracker.Start("key");
            sdk.Calls.Clear();

            foreach (var revenue in new[] { -1d, double.NaN, double.PositiveInfinity })
            {
                tracker.LogAdRevenue("unity", revenue, "unit", "INTER", "US");
            }

            tracker.LogAdRevenue("unity", 0d, "unit", "", null);

            Assert.That(sdk.Calls, Is.EqualTo(new[] { "ad:unity:0:USD:ad_unit=unit" }));
        }

        [Test]
        public void EachPurchaseIsReportedOnceWithStorePriceAndCurrencyButNoTransactionId()
        {
            tracker.Start("key");
            sdk.Calls.Clear();

            tracker.LogPurchase("remove_ads", "tx-1", 9.99m, "USD");
            tracker.LogPurchase("remove_ads", "tx-1", 9.99m, "USD");
            tracker.LogPurchase("remove_ads", "tx-2", 150000m, "IDR");

            Assert.That(sdk.Calls, Is.EqualTo(new[]
            {
                "af_purchase:af_content_id=remove_ads,af_currency=USD,af_revenue=9.99",
                "af_purchase:af_content_id=remove_ads,af_currency=IDR,af_revenue=150000",
            }));
        }

        [TestCase("", 9.99, "USD")]
        [TestCase("tx-1", 0d, "USD")]
        [TestCase("tx-1", 9.99, "")]
        public void PurchaseWithoutTransactionPriceOrCurrencyIsNotReported(string transactionId, double price, string currency)
        {
            tracker.Start("key");
            sdk.Calls.Clear();

            tracker.LogPurchase("remove_ads", transactionId, (decimal)price, currency);

            Assert.That(sdk.Calls, Is.Empty);
            Assert.That(PlayerPrefs.HasKey(PurchaseKey), Is.False);
        }

        [Test]
        public void APurchaseThatFailedToSendIsReportedWhenRedelivered()
        {
            tracker.Start("key");
            sdk.Calls.Clear();
            sdk.FailSend = true;
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(@"^\[AppsFlyer\] 内购收入上报失败"));

            tracker.LogPurchase("remove_ads", "tx-1", 9.99m, "USD");
            sdk.FailSend = false;
            tracker.LogPurchase("remove_ads", "tx-1", 9.99m, "USD");

            Assert.That(sdk.Calls, Has.Count.EqualTo(1));
        }
    }
}
