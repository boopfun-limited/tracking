using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace Tracking.Consent.Tests.EditMode
{
    /// <summary>
    /// 同意模块发出的每个名字都得是 GA4 收得下的，判据与 <c>LevelTrackingEventsTests</c> 同一条：
    /// ≤40 字符、小写字母开头、只含小写字母数字下划线，不以 <c>firebase_</c> / <c>google_</c> /
    /// <c>ga_</c> 开头。🔴 违规的事件 Firebase **静默丢弃**，不报错也不进 DebugView，所以这道门在包里。
    ///
    /// 🔴 这道门也是「条款弹窗两条事件写成全小写」的原因——竞品原名是 <c>dlg_show_LAW</c>，
    /// 照抄会在这里红（见 <see cref="ConsentEvents"/> 的注释）。
    /// </summary>
    public sealed class ConsentEventsTests
    {
        private static readonly Regex Ga4Name = new Regex("^[a-z][a-z0-9_]{0,39}$");

        private static IEnumerable<(string Owner, string Name, string Value)> AllNames()
        {
            foreach (var type in new[] { typeof(ConsentEvents), typeof(ConsentEvents.Params) })
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
        public void NamesAreUnique()
        {
            var values = AllNames().Select(n => n.Value).ToList();
            Assert.That(values.Distinct().Count(), Is.EqualTo(values.Count), "名字撞了会静默混数据");
        }

        /// <summary>
        /// 六条 UMP 事件都带 <c>ump_</c> 前缀，两条条款弹窗事件不带——这是竞品的分法，
        /// 按前缀就能在 GA4 里把「同意表单」与「条款弹窗」分开看。
        /// </summary>
        [Test]
        public void UmpEventsAreNamespacedByPrefix()
        {
            var umpEvents = new[]
            {
                ConsentEvents.InfoRequestStart,
                ConsentEvents.FormLoadStart,
                ConsentEvents.FormTryShow,
                ConsentEvents.FormShowSuccess,
                ConsentEvents.PrivacyFormTryShow,
                ConsentEvents.PrivacyFormShowSuccess,
            };

            Assert.That(umpEvents.All(e => e.StartsWith("ump_")), Is.True);
            Assert.That(ConsentEvents.TermsDialogShow.StartsWith("ump_"), Is.False);
            Assert.That(ConsentEvents.TermsDialogAccept.StartsWith("ump_"), Is.False);
        }
    }
}
