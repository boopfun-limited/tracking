using Tracking;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace LevelTracking.Tests.EditMode
{
    /// <summary>
    /// 🔴 表 == 行为，包内唯一的一道门：<see cref="LevelTrackingSchema.Methods"/> 说每个方法发什么，
    /// 这里反射调**每一个**公开方法（除 <see cref="LevelTracker.Flat"/>）核对。表改了行为没改、
    /// 或反过来，这一条就红；游戏侧的文档工具读的正是那张表，所以它红了文档也不可信。
    /// </summary>
    public sealed class LevelTrackerSchemaTests
    {
        private sealed class Recorder : IAnalyticsBackend
        {
            public readonly List<(string Name, AnalyticsParameter[] Parameters)> Events =
                new List<(string, AnalyticsParameter[])>();

            public void LogEvent(string eventName, params AnalyticsParameter[] parameters)
            {
                Events.Add((eventName, parameters));
            }

            public void SetUserProperty(string name, string value)
            {
                // 这一组用例只核 LevelTracker 发了哪些事件，用户属性不参与判定。
            }
        }

        private static IEnumerable<MethodInfo> EmittingMethods()
        {
            return typeof(LevelTracker)
                .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(m => m.Name != nameof(LevelTracker.Flat));
        }

        private static object[] ArgumentsFor(MethodInfo method)
        {
            return method.GetParameters().Select(p =>
            {
                if (p.ParameterType == typeof(int)) return (object)1;
                if (p.ParameterType == typeof(string)) return "x";
                if (p.ParameterType == typeof(bool)) return true;
                if (p.ParameterType == typeof(AnalyticsParameter[])) return Array.Empty<AnalyticsParameter>();
                throw new InvalidOperationException($"{method.Name} 的形参 {p.Name} 类型 {p.ParameterType} 本测试不会构造——加一档。");
            }).ToArray();
        }

        [Test]
        public void LevelTrackerEmitsExactlyItsSchema()
        {
            var rows = LevelTrackingSchema.Methods.ToDictionary(r => r.Method);
            var clock = new PlayClock(() => 0f);

            foreach (var method in EmittingMethods())
            {
                Assert.That(rows.ContainsKey(method.Name), Is.True,
                    $"{method.Name} 是公开的发射方法，但 LevelTrackingSchema.Methods 里没有它这一行。");
                var row = rows[method.Name];

                var recorder = new Recorder();
                var tracker = new LevelTracker(recorder, clock, null);
                method.Invoke(tracker, ArgumentsFor(method));

                Assert.That(recorder.Events.Count, Is.EqualTo(1), $"{method.Name} 应恰好发一条事件");
                Assert.That(recorder.Events[0].Name, Is.EqualTo(row.Event), $"{method.Name} 发的事件名");

                var expected = new HashSet<string>(LevelTrackingSchema.CommonParams);
                if (!row.PlayTime)
                {
                    expected.Remove(LevelTrackingEvents.Params.PlayTimeSeconds);
                }
                expected.UnionWith(row.Parameters);
                var actual = recorder.Events[0].Parameters.Select(p => p.Name).ToList();
                Assert.That(actual, Is.Unique, $"{method.Name} 发了重复的参数名");
                Assert.That(new HashSet<string>(actual), Is.EquivalentTo(expected),
                    $"{method.Name} 发出的参数集合与表里那一行不一致");
            }

            // 双向：表里每一行也都得对应一个公开方法（表里多写一行，游戏文档会描述一个不存在的入口）。
            Assert.That(
                rows.Keys,
                Is.EquivalentTo(EmittingMethods().Select(m => m.Name)),
                "LevelTrackingSchema.Methods 的方法名集合 != LevelTracker 的公开发射方法集合");
        }

        [Test]
        public void GameCommonsAreStampedOnLevelEventsButNotOnFlatOnes()
        {
            var recorder = new Recorder();
            var tracker = new LevelTracker(recorder, new PlayClock(() => 0f),
                () => new[] { AnalyticsParameter.Of("game_common", "g") });

            tracker.LevelExit(3);
            tracker.Flat("flat_event", AnalyticsParameter.Of("only", 1L));

            Assert.That(recorder.Events[0].Parameters.Select(p => p.Name), Does.Contain("game_common"));
            Assert.That(recorder.Events[1].Parameters.Select(p => p.Name), Is.EquivalentTo(new[] { "only" }));
        }

        [Test]
        public void PlayTimeIsAbsentOnTheOriginAndCountsRealElapsedAfterIt()
        {
            var now = 1000f;
            var clock = new PlayClock(() => now);
            var recorder = new Recorder();
            var tracker = new LevelTracker(recorder, clock, null);

            clock.Restart();
            tracker.LevelStart(1, "fresh");
            now = 1012.5f;
            tracker.LevelExit(1);

            Assert.That(recorder.Events[0].Parameters.Select(p => p.Name),
                Does.Not.Contain(LevelTrackingEvents.Params.PlayTimeSeconds));
            var playTime = recorder.Events[1].Parameters.Single(p => p.Name == LevelTrackingEvents.Params.PlayTimeSeconds);
            Assert.That(playTime.Kind, Is.EqualTo(AnalyticsParameter.ValueKind.Double));
            Assert.That(playTime.DoubleValue, Is.EqualTo(12.5).Within(0.0005));
        }
    }
}
