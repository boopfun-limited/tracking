using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Tracking;
using Tracking.Iap;

namespace LevelTracking.Tests.EditMode
{
    public sealed class IapTrackerTests
    {
        private sealed class Sink : IAnalyticsBackend
        {
            internal readonly List<string> Names = new List<string>();
            internal readonly List<Dictionary<string, AnalyticsParameter>> Events = new List<Dictionary<string, AnalyticsParameter>>();
            public void LogEvent(string name, params AnalyticsParameter[] values)
            {
                Names.Add(name);
                Events.Add(values.ToDictionary(p => p.Name));
            }
            public void SetUserProperty(string name, string value) { }
            internal Dictionary<string, AnalyticsParameter>[] Stage(string action) => Events.Where(e => e["iap_action"].StringValue == action).ToArray();
        }
        private static IapState State(bool owned, string status = "ready") => new IapState(owned, status, "9.99 USD");

        [Test]
        public void PendingOutlivesPanelAndKeepsImmutableContextWithoutDuplicateResults()
        {
            var sink = new Sink();
            using (var tracker = new IapTracker(sink, "product-a", State(false)))
            {
                var context = new[] { AnalyticsParameter.Of("level_number", 17L) };
                tracker.EntryClicked("hud", context); context[0] = AnalyticsParameter.Of("level_number", 99L);
                tracker.PanelShown(); tracker.PanelShown();
                var attempt = tracker.BeginPurchase();
                attempt.Complete(IapResult.Pending); attempt.Complete(IapResult.Pending);
                tracker.PanelClosed(); tracker.PanelClosed();
                tracker.EntryClicked("hud", AnalyticsParameter.Of("level_number", 88L));
                tracker.Observe(State(true)); tracker.Observe(State(true)); attempt.Complete(IapResult.Succeeded);
                Assert.That(sink.Stage("panel_view").Length, Is.EqualTo(1));
                Assert.That(sink.Stage("panel_close").Length, Is.EqualTo(1));
                var results = sink.Stage("purchase_result");
                Assert.That(results.Length, Is.EqualTo(2));
                Assert.That(results.Select(e => e["iap_result"].StringValue), Is.EqualTo(new[] { "pending", "succeeded" }));
                Assert.That(results.All(e => e["level_number"].LongValue == 17 && e["iap_attempt_id"].StringValue == attempt.AttemptId), Is.True);
                Assert.That(sink.Stage("entitlement_change").Length, Is.EqualTo(1));
            }
        }

        [Test]
        public void CachedOwnershipAndRestoreDoNotBecomePurchasesAndRevocationDeduplicates()
        {
            var sink = new Sink();
            using (var tracker = new IapTracker(sink, "product-a", State(true)))
            {
                tracker.Observe(State(true)); Assert.That(sink.Events, Is.Empty);
                var restore = tracker.BeginRestore(); restore.Complete(IapResult.Succeeded); restore.Complete(IapResult.Succeeded);
                tracker.Observe(State(false)); tracker.Observe(State(false));
                Assert.That(sink.Stage("purchase_result"), Is.Empty);
                Assert.That(sink.Stage("restore_result").Length, Is.EqualTo(1));
                var changed = sink.Stage("entitlement_change").Single();
                Assert.That(changed["iap_result"].StringValue, Is.EqualTo("revoked"));
                Assert.That(changed["iap_previous_owned"].LongValue, Is.EqualTo(1));
                Assert.That(changed["iap_owned"].LongValue, Is.Zero);
                Assert.That(changed["iap_source"].StringValue, Is.EqualTo("store_sync"));
            }
        }

        [Test]
        public void QueryFailureKeepsPendingAndSuccessfulAbsenceHasNoInventedReason()
        {
            var sink = new Sink();
            using (var tracker = new IapTracker(sink, "product-a", State(false)))
            {
                tracker.BeginPurchase().Complete(IapResult.Pending);
                tracker.Observe(State(false, "unavailable"));
                Assert.That(sink.Stage("purchase_result").Length, Is.EqualTo(1));
                tracker.Observe(State(false)); tracker.Observe(State(false));
                Assert.That(sink.Stage("purchase_result").Last()["iap_result"].StringValue, Is.EqualTo("not_owned"));
                Assert.That(sink.Stage("purchase_result").Length, Is.EqualTo(2));
            }
        }

        [Test]
        public void ProductsStaySeparateAndAlreadyOwnedIsNotANewPurchase()
        {
            var sink = new Sink();
            using (var a = new IapTracker(sink, "a", State(true)))
            using (var b = new IapTracker(sink, "b", State(false)))
            {
                a.BeginPurchase().Complete(IapResult.Succeeded);
                b.BeginPurchase().Complete(IapResult.Cancelled);
                var results = sink.Stage("purchase_result");
                Assert.That(results[0]["iap_product_id"].StringValue, Is.EqualTo("a"));
                Assert.That(results[0]["iap_result"].StringValue, Is.EqualTo("already_owned"));
                Assert.That(results[1]["iap_product_id"].StringValue, Is.EqualTo("b"));
                Assert.That(results[1]["iap_owned"].LongValue, Is.Zero);
            }
        }

        [Test]
        public void StandardPayloadContainsOnlyDeclaredDiagnosticFields()
        {
            var sink = new Sink();
            using (var tracker = new IapTracker(sink, "a", State(false))) tracker.BeginPurchase().Complete(IapResult.Failed);
            var names = typeof(IapEvents.Params).GetFields().Select(f => (string)f.GetValue(null)).ToArray();
            Assert.That(sink.Events.Count, Is.EqualTo(2));
            Assert.That(sink.Names, Is.All.EqualTo("iap_flow"));
            foreach (var value in sink.Events) Assert.That(value.Keys, Is.EquivalentTo(names));
        }

        [Test]
        public void DisposeStopsDelayedResultsAndBrokenTelemetryDoesNotThrow()
        {
            var sink = new Sink(); var tracker = new IapTracker(sink, "a", State(false));
            var attempt = tracker.BeginPurchase(); tracker.Dispose(); var count = sink.Events.Count;
            attempt.Complete(IapResult.Succeeded); tracker.Observe(State(true));
            Assert.That(sink.Events.Count, Is.EqualTo(count));
            using (tracker = new IapTracker(new BrokenSink(), "a", State(false)))
                Assert.DoesNotThrow(() => tracker.BeginPurchase().Complete(IapResult.Succeeded));
        }
        private sealed class BrokenSink : IAnalyticsBackend
        {
            public void LogEvent(string name, params AnalyticsParameter[] values) => throw new Exception();
            public void SetUserProperty(string name, string value) { }
        }
    }
}
