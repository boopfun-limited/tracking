using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace LevelTracking.Tests.EditMode
{
    /// <summary>
    /// 库发出的每个名字都得是 GA4 收得下的：≤40 字符、小写字母开头、只含小写字母数字下划线，
    /// 不以 <c>firebase_</c> / <c>google_</c> / <c>ga_</c> 开头。🔴 违规的事件 Firebase **静默丢弃**，
    /// 不报错也不进 DebugView，所以这条门在包里、不在游戏里。
    /// </summary>
    public sealed class LevelTrackingEventsTests
    {
        private static readonly Regex Ga4Name = new Regex("^[a-z][a-z0-9_]{0,39}$");

        private static IEnumerable<(string Owner, string Name, string Value)> AllNames()
        {
            foreach (var type in new[] { typeof(LevelTrackingEvents), typeof(LevelTrackingEvents.Params) })
            {
                foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Static)
                    .Where(f => f.IsLiteral && f.FieldType == typeof(string)))
                {
                    yield return (type.Name, field.Name, (string)field.GetRawConstantValue());
                }
            }
        }

        [Test]
        public void EveryNameIsGa4Legal()
        {
            var names = AllNames().ToList();
            Assert.That(names, Is.Not.Empty);
            foreach (var (owner, name, value) in names)
            {
                Assert.That(Ga4Name.IsMatch(value), Is.True, $"{owner}.{name} = {value}");
                foreach (var reserved in new[] { "firebase_", "google_", "ga_" })
                {
                    Assert.That(value.StartsWith(reserved), Is.False, $"{owner}.{name} = {value} 用了保留前缀");
                }
            }
        }

        [Test]
        public void EventNamesAndParameterNamesAreEachUnique()
        {
            var events = AllNames().Where(n => n.Owner == nameof(LevelTrackingEvents)).Select(n => n.Value).ToList();
            var parameters = AllNames().Where(n => n.Owner == "Params").Select(n => n.Value).ToList();
            Assert.That(events, Is.Unique);
            Assert.That(parameters, Is.Unique);
        }

        [Test]
        public void EverySchemaRowNamesOnlyDeclaredEventsAndParameters()
        {
            var events = AllNames().Where(n => n.Owner == nameof(LevelTrackingEvents)).Select(n => n.Value).ToHashSet();
            var parameters = AllNames().Where(n => n.Owner == "Params").Select(n => n.Value).ToHashSet();
            foreach (var row in LevelTrackingSchema.Methods)
            {
                Assert.That(events, Does.Contain(row.Event), row.Method);
                Assert.That(parameters, Is.SupersetOf(row.Parameters), row.Method);
            }
            Assert.That(parameters, Is.SupersetOf(LevelTrackingSchema.CommonParams));
            // 每个声明的事件至少有一行会发它；每个参数至少被一行（或公共参数）带到——声明了没人发就是死名字。
            Assert.That(LevelTrackingSchema.Methods.Select(r => r.Event).ToHashSet(), Is.EquivalentTo(events));
            var carried = LevelTrackingSchema.Methods.SelectMany(r => r.Parameters).Concat(LevelTrackingSchema.CommonParams).ToHashSet();
            Assert.That(carried, Is.EquivalentTo(parameters));
        }
    }
}
