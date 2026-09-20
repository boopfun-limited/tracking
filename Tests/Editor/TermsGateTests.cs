using System;
using System.Collections.Generic;
using NUnit.Framework;
using Tracking;

namespace Tracking.Consent.Tests.EditMode
{
    /// <summary>
    /// <see cref="TermsGate"/> 的不变量——就是它类注释里那三件「每个游戏都要写一遍、写错了还都静默」的事。
    ///
    /// 🔴 「流程有没有被放行」一律拿**平台假件上的请求次数**判，不看 gate 自己的字段：
    /// 真会坏的后果是「UMP 请求根本没发出去」，断言要钉在那个后果上。假件里
    /// <see cref="FakePlatform.RequestConsentInfoUpdate"/> 同步回成功，满足
    /// <see cref="IConsentPlatform"/>「每次恰好回调一次」的契约。
    /// </summary>
    public sealed class TermsGateTests
    {
        /// <summary>🔴 最贵的那条：老玩家不弹窗，但流程照样要被放行，否则整场没有 UMP、没有广告，且一声不响。</summary>
        [Test]
        public void ReturningPlayerSeesNoDialogAndStillReleasesTheFlow()
        {
            var gate = NewGate(accepted: true, out var store, out var analytics, out var platform);
            var shown = 0;

            var popped = gate.ShowIfNeeded(() => shown++);

            Assert.That(popped, Is.False);
            Assert.That(shown, Is.EqualTo(0), "同意过的人不该再吃一道全屏闸");
            Assert.That(analytics.Count(ConsentEvents.TermsDialogShow), Is.EqualTo(0), "没弹就不该有弹出事件");
            Assert.That(store.Writes, Is.EqualTo(0), "没点过按钮，不该写盘");
            Assert.That(platform.RequestCount, Is.EqualTo(1), "🔴 老玩家也必须放行流程");
        }

        [Test]
        public void NewPlayerGetsTheDialogAndTheFlowStaysPut()
        {
            var gate = NewGate(accepted: false, out var store, out var analytics, out var platform);
            var shown = 0;

            var popped = gate.ShowIfNeeded(() => shown++);

            Assert.That(popped, Is.True);
            Assert.That(shown, Is.EqualTo(1));
            Assert.That(analytics.Count(ConsentEvents.TermsDialogShow), Is.EqualTo(1));
            Assert.That(store.Writes, Is.EqualTo(0), "弹出不等于同意");
            Assert.That(gate.IsAccepted, Is.False);
            Assert.That(platform.RequestCount, Is.EqualTo(0), "没点之前不许向 UMP 发请求");
        }

        /// <summary>点下之后的整条顺序：弹出 → 点击 → UMP 请求。三条名字与先后都钉住。</summary>
        [Test]
        public void AcceptLogsPersistsAndReleasesTheFlow()
        {
            var gate = NewGate(accepted: false, out var store, out var analytics, out var platform);

            gate.ShowIfNeeded(() => { });
            gate.Accept();

            Assert.That(gate.IsAccepted, Is.True);
            Assert.That(store.Writes, Is.EqualTo(1));
            Assert.That(platform.RequestCount, Is.EqualTo(1));
            Assert.That(
                analytics.Names,
                Is.EqualTo(new[]
                {
                    ConsentEvents.TermsDialogShow,
                    ConsentEvents.TermsDialogAccept,
                    ConsentEvents.InfoRequestStart,
                }),
                "顺序照 PRD §5.2：发事件 → 落盘 → 放行流程");
        }

        /// <summary>连点两下（或者游戏两处都接了 Accept）：事件与写盘各只有一次，流程仍然只发一次请求。</summary>
        [Test]
        public void DoubleTapCountsOnce()
        {
            var gate = NewGate(accepted: false, out var store, out var analytics, out var platform);

            gate.ShowIfNeeded(() => { });
            gate.Accept();
            gate.Accept();

            Assert.That(analytics.Count(ConsentEvents.TermsDialogAccept), Is.EqualTo(1));
            Assert.That(store.Writes, Is.EqualTo(1));
            Assert.That(platform.RequestCount, Is.EqualTo(1));
        }

        /// <summary>不接广告的游戏（arrows-3d / boopdoku 这类纯单机）不传 flow，弹窗与落盘照走。</summary>
        [Test]
        public void GateWithoutAFlowStillPopsAndPersists()
        {
            var store = new FakeStore();
            var analytics = new RecordingAnalytics();
            var gate = new TermsGate(store, analytics);

            Assert.That(gate.ShowIfNeeded(() => { }), Is.True);
            gate.Accept();

            Assert.That(store.Writes, Is.EqualTo(1));
            Assert.That(analytics.Count(ConsentEvents.TermsDialogAccept), Is.EqualTo(1));
        }

        /// <summary>「游戏放行」先到，剩下的就只等条款——这样请求次数直接反映条款那一路。</summary>
        private static TermsGate NewGate(
            bool accepted, out FakeStore store, out RecordingAnalytics analytics, out FakePlatform platform)
        {
            store = new FakeStore { IsAccepted = accepted };
            analytics = new RecordingAnalytics();
            platform = new FakePlatform();
            var flow = new ConsentFlow(platform, analytics, () => 0f);
            flow.MarkGameReady();
            return new TermsGate(store, analytics, flow);
        }

        private sealed class FakeStore : ITermsStore
        {
            public bool IsAccepted { get; set; }

            public int Writes { get; private set; }

            public void MarkAccepted()
            {
                Writes++;
                IsAccepted = true;
            }
        }

        private sealed class RecordingAnalytics : IAnalyticsBackend
        {
            private readonly List<string> names = new List<string>();

            public IEnumerable<string> Names => names;

            public void LogEvent(string eventName, params AnalyticsParameter[] parameters) => names.Add(eventName);

            public void SetUserProperty(string name, string value)
            {
            }

            public int Count(string eventName) => names.FindAll(n => n == eventName).Count;
        }

        private sealed class FakePlatform : IConsentPlatform
        {
            public int RequestCount { get; private set; }

            public bool CanRequestAds => false;

            public bool IsConsentFormRequired => false;

            public bool IsPrivacyOptionsRequired => false;

            public string PurposeConsents => null;

            public string GppString => null;

            public void Pump()
            {
            }

            public void RequestConsentInfoUpdate(Action onSuccess, Action<string> onFailure)
            {
                RequestCount++;
                onSuccess();
            }

            public void LoadConsentForm(Action onLoaded, Action<string> onFailure)
            {
            }

            public void ShowConsentForm(Action<string> onDismissed)
            {
            }

            public void ShowPrivacyOptionsForm(Action<string> onDismissed)
            {
            }
        }
    }
}
