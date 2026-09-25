using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Tracking;
using Tracking.DailyChallenge;

namespace LevelTracking.Tests.EditMode
{
    public sealed class DailyChallengeTrackerTests
    {
        private sealed class Capture : IAnalyticsBackend
        {
            public readonly List<(string Name, AnalyticsParameter[] Parameters)> Events = new List<(string, AnalyticsParameter[])>();
            public void LogEvent(string name, params AnalyticsParameter[] parameters) => Events.Add((name, parameters));
            public void SetUserProperty(string name, string value) { }
        }

        private static Dictionary<string, object> Values(AnalyticsParameter[] parameters) =>
            parameters.ToDictionary(p => p.Name, p => p.Kind == AnalyticsParameter.ValueKind.String ? (object)p.StringValue : p.LongValue);

        private static IEnumerable<string> Declared(Type type) =>
            type.GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(f => f.IsLiteral && f.FieldType == typeof(string))
                .Select(f => (string)f.GetRawConstantValue());

        [Test]
        public void EachEventCarriesExactlyItsParameters()
        {
            var capture = new Capture();
            var tracker = new DailyChallengeTracker(capture);

            tracker.Open("tab");
            tracker.RewardUnlocked(new DateTime(2026, 9, 17), "gold");
            tracker.ReminderOpened(2);
            tracker.ShareOpened(new DateTime(2026, 8, 31, 23, 0, 0), "silver");
            tracker.Shared(new DateTime(2026, 8, 1), "silver", "facebook_fallback");
            tracker.Saved(new DateTime(2025, 12, 5), "bronze", "denied");

            Assert.That(capture.Events.Select(e => e.Name), Is.EqualTo(new[]
                { "dc_open", "dc_reward_unlocked", "dc_reminder_open", "dc_share_open", "dc_share", "dc_save" }));
            Assert.That(Values(capture.Events[0].Parameters), Is.EquivalentTo(new Dictionary<string, object> { ["dc_source"] = "tab" }));
            Assert.That(Values(capture.Events[1].Parameters), Is.EquivalentTo(new Dictionary<string, object> { ["dc_month"] = 202609L, ["dc_tier"] = "gold" }));
            Assert.That(Values(capture.Events[2].Parameters), Is.EquivalentTo(new Dictionary<string, object> { ["dc_reminder_index"] = 2L }));
            Assert.That(Values(capture.Events[3].Parameters), Is.EquivalentTo(new Dictionary<string, object> { ["dc_month"] = 202608L, ["dc_tier"] = "silver" }));
            Assert.That(Values(capture.Events[4].Parameters), Is.EquivalentTo(new Dictionary<string, object>
                { ["dc_month"] = 202608L, ["dc_tier"] = "silver", ["dc_share_method"] = "facebook_fallback" }));
            Assert.That(Values(capture.Events[5].Parameters), Is.EquivalentTo(new Dictionary<string, object>
                { ["dc_month"] = 202512L, ["dc_tier"] = "bronze", ["dc_save_result"] = "denied" }));
        }

        [Test]
        public void LevelContextCountsWholeLocalDays()
        {
            // 钟点不参与：补做 5 天前那题，哪怕今天凌晨 1 点、那天上午 10 点，也是 5。
            Assert.That(Values(DailyChallengeTracker.LevelContext(new DateTime(2026, 9, 20, 10, 0, 0), new DateTime(2026, 9, 25, 1, 0, 0))),
                Is.EquivalentTo(new Dictionary<string, object> { ["dc_date"] = 20260920L, ["dc_days_ago"] = 5L }));
            Assert.That(Values(DailyChallengeTracker.LevelContext(new DateTime(2025, 12, 31), new DateTime(2026, 1, 1, 23, 59, 0))),
                Is.EquivalentTo(new Dictionary<string, object> { ["dc_date"] = 20251231L, ["dc_days_ago"] = 1L }));
            Assert.That(Values(DailyChallengeTracker.LevelContext(new DateTime(2026, 9, 25, 23, 0, 0), new DateTime(2026, 9, 25, 8, 0, 0)))["dc_days_ago"],
                Is.EqualTo(0L));
        }

        /// <summary>
        /// 名字 GA4 收得下（违规的事件 Firebase 静默丢弃）、各自唯一，且声明了的名字都有人发——
        /// 声明了没人发的是死名字，游戏照着它查数据只会查到空。
        /// </summary>
        [Test]
        public void EveryDeclaredNameIsGa4LegalUniqueAndEmitted()
        {
            var events = Declared(typeof(DailyChallengeEvents)).ToList();
            var parameters = Declared(typeof(DailyChallengeEvents.Params)).ToList();
            var ga4 = new Regex("^[a-z][a-z0-9_]{0,39}$");
            foreach (var name in events.Concat(parameters))
            {
                Assert.That(ga4.IsMatch(name), Is.True, name);
                Assert.That(new[] { "firebase_", "google_", "ga_" }.Any(name.StartsWith), Is.False, name);
            }

            Assert.That(events, Is.Unique);
            Assert.That(parameters, Is.Unique);

            var capture = new Capture();
            var tracker = new DailyChallengeTracker(capture);
            tracker.Open("tab");
            tracker.RewardUnlocked(new DateTime(2026, 9, 1), "bronze");
            tracker.ReminderOpened(1);
            tracker.ShareOpened(new DateTime(2026, 9, 1), "gold");
            tracker.Shared(new DateTime(2026, 9, 1), "gold", "more");
            tracker.Saved(new DateTime(2026, 9, 1), "gold", "ok");
            Assert.That(capture.Events.Select(e => e.Name), Is.EquivalentTo(events));
            Assert.That(capture.Events.SelectMany(e => e.Parameters)
                    .Concat(DailyChallengeTracker.LevelContext(DateTime.Today, DateTime.Today))
                    .Select(p => p.Name)
                    .Distinct(),
                Is.EquivalentTo(parameters));
        }
    }
}
