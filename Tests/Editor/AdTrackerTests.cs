using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Tracking;
using Tracking.Ads;

namespace LevelTracking.Tests.EditMode
{
    public class AdTrackerTests
    {
        private sealed class Capture : IAnalyticsBackend
        {
            public readonly List<(string name, AnalyticsParameter[] parameters)> Events = new List<(string, AnalyticsParameter[])>();
            public void LogEvent(string name, params AnalyticsParameter[] parameters) => Events.Add((name, parameters));
            public void SetUserProperty(string name, string value) { }
            public AnalyticsParameter Param(int index, string name) => Events[index].parameters.Single(p => p.Name == name);
        }
        [Test]
        public void LoadPairsByRequestIdAndDeduplicatesTheResult()
        {
            var sink = new Capture(); double time = 10;
            var tracker = new AdTracker(sink, () => time);
            var load = tracker.Request(AdEvents.Formats.Rewarded, "reward-unit");
            time = 10.25;
            load.Fill(false, AnalyticsParameter.Of(AdEvents.Params.ErrorCode, 204L));
            load.Fill(true);
            Assert.That(sink.Events.Select(e => e.name), Is.EqualTo(new[] {"ad_request", "ad_fill"}));
            Assert.That(sink.Param(0, "request_id").StringValue, Is.EqualTo(sink.Param(1, "request_id").StringValue));
            Assert.That(sink.Param(1, "duration_ms").LongValue, Is.EqualTo(250));
            Assert.That(sink.Param(1, "result").StringValue, Is.EqualTo("failure"));
        }
        [Test]
        public void FlowSnapshotsContextAndSeparatesQualificationFromDelivery()
        {
            var sink = new Capture(); double time = 1;
            var tracker = new AdTracker(sink, () => time);
            var context = new[] {AnalyticsParameter.Of("level_number", 15L), AnalyticsParameter.Of("booster_type", "eraser")};
            var flow = tracker.Opportunity(AdEvents.Formats.Rewarded, "prop", context);
            context[0] = AnalyticsParameter.Of("level_number", 99L);
            flow.Decision("show", "ad_ready");
            flow.Decision("skip", "duplicate");
            flow.ShowRequest("load-1");
            time = 2; flow.ShowResult(true); flow.ShowResult(false);
            flow.RewardEarned(); flow.RewardEarned();
            time = 12; flow.Close(); flow.Close();
            flow.RewardResult(false, "state_unavailable"); flow.RewardResult(true, "duplicate");
            Assert.That(sink.Events.Select(e => e.name), Is.EqualTo(new[] {
                "ad_opportunity", "ad_decision", "ad_show_request", "ad_show_result",
                "ad_reward_earned", "ad_closed", "ad_reward_result"}));
            Assert.That(sink.Param(5, "duration_ms").LongValue, Is.EqualTo(10000));
            Assert.That(sink.Param(5, "reward_earned").LongValue, Is.EqualTo(1));
            Assert.That(sink.Param(6, "result").StringValue, Is.EqualTo("not_granted"));
            for (var i = 0; i < sink.Events.Count; i++) {
                Assert.That(sink.Param(i, "level_number").LongValue, Is.EqualTo(15));
                Assert.That(sink.Param(i, "ad_flow_id").StringValue, Is.EqualTo(flow.FlowId));
            }
        }
        [Test]
        public void ClosingWithoutDisplayDoesNotInventSuccessOrWatchDuration()
        {
            var sink = new Capture();
            var flow = new AdTracker(sink).Opportunity("REWARDED", "revive");
            flow.ShowRequest(); flow.Close(); flow.RewardResult(true, "granted");
            Assert.That(sink.Events.Select(e => e.name), Is.EqualTo(new[] {"ad_opportunity", "ad_show_request", "ad_closed"}));
            Assert.That(sink.Events[2].parameters.Any(p => p.Name == "duration_ms"), Is.False);
        }
        [Test]
        public void BannerAutomaticRefreshDoesNotInventRequestOrLatency()
        {
            var sink = new Capture();
            new AdTracker(sink).AutomaticFill("BANNER", "unit", true);
            Assert.That(sink.Events.Single().name, Is.EqualTo("ad_fill"));
            Assert.That(sink.Param(0, "load_origin").StringValue, Is.EqualTo("sdk_auto"));
            Assert.That(sink.Events[0].parameters.Any(p => p.Name == "request_id" || p.Name == "duration_ms"), Is.False);
        }
        [Test]
        public void RevenuePreservesZeroAndRejectsInvalidValues()
        {
            var sink = new Capture(); var tracker = new AdTracker(sink);
            foreach (var revenue in new[] {0d, -1d, double.NaN, double.PositiveInfinity})
                tracker.Impression("appLovin", "unit", "INTER", "level_end", "network", revenue, "USD");
            Assert.That(sink.Events.Count, Is.EqualTo(1));
            Assert.That(sink.Param(0, "value").DoubleValue, Is.EqualTo(0));
        }
        [Test]
        public void ClickNameDoesNotCollideWithFirebaseReservedAdClick()
        {
            var sink = new Capture();
            new AdTracker(sink).Click("BANNER", "banner");
            Assert.That(sink.Events.Single().name, Is.EqualTo("ad_clicked"));
        }
    }
}
