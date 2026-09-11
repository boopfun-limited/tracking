using System;
using System.Collections.Generic;
using NUnit.Framework;
using Tracking;

namespace Tracking.Consent.Tests.EditMode
{
    /// <summary>
    /// <see cref="ConsentFlow"/> 的不变量。用例分两组：
    /// **照竞品的**（触发时点、REQUIRED 才弹表单、收尾无论成败都回调）与
    /// **唯一偏离的那条**（请求之后立刻用上次的结论放行广告、失败重试，PRD §6）。
    ///
    /// 🔴 假件 <see cref="FakePlatform"/> 复刻了 UMP 的一条关键语义：
    /// <c>CanRequestAds</c> 在本进程调过 <c>RequestConsentInfoUpdate</c> **之前恒为假**。
    /// 没有这一条，「检查排在请求之前」这个竞品实际踩的坑在测试里看不出来——
    /// <see cref="ReturningUserGetsAdsBeforeTheServerAnswers"/> 正是靠它才有判别力。
    /// </summary>
    public sealed class ConsentFlowTests
    {
        [Test]
        public void RequestWaitsForBothPreconditionsAndIsSentOnce()
        {
            var platform = new FakePlatform();
            var flow = NewFlow(platform, out _, out _);

            flow.MarkTermsAccepted();
            Assert.That(platform.RequestCount, Is.EqualTo(0), "只有条款同意、游戏还没放行，不该发请求");

            flow.MarkGameReady();
            Assert.That(platform.RequestCount, Is.EqualTo(1), "两个前置条件都到了才发");

            flow.MarkTermsAccepted();
            flow.MarkGameReady();
            Assert.That(platform.RequestCount, Is.EqualTo(1), "再标几次也只发一次");
        }

        [Test]
        public void PreconditionsInTheOtherOrderStillTakeTheLaterOne()
        {
            var platform = new FakePlatform();
            var flow = NewFlow(platform, out _, out _);

            flow.MarkGameReady();
            Assert.That(platform.RequestCount, Is.EqualTo(0));

            flow.MarkTermsAccepted();
            Assert.That(platform.RequestCount, Is.EqualTo(1));
        }

        /// <summary>🔴 唯一偏离竞品的那条：老用户不必等服务端回话就能起广告。</summary>
        [Test]
        public void ReturningUserGetsAdsBeforeTheServerAnswers()
        {
            var platform = new FakePlatform { StoredConsentAllowsAds = true };
            var flow = NewFlow(platform, out var adsAllowed, out var finished);

            flow.MarkTermsAccepted();
            flow.MarkGameReady();

            Assert.That(adsAllowed.Count, Is.EqualTo(1), "请求发出后立刻读到上次的结论，就该放行广告");
            Assert.That(finished.Count, Is.EqualTo(0), "但流程还没收尾——回话还没到");
            Assert.That(platform.RequestCount, Is.EqualTo(1));
        }

        /// <summary>新用户在两种做法下必须完全一样：回话之前不放广告，该弹的表单照弹。</summary>
        [Test]
        public void NewUserGetsNoAdsUntilTheFormIsDone()
        {
            var platform = new FakePlatform { StoredConsentAllowsAds = false, IsConsentFormRequired = true };
            var flow = NewFlow(platform, out var adsAllowed, out var finished);

            flow.MarkTermsAccepted();
            flow.MarkGameReady();
            Assert.That(adsAllowed.Count, Is.EqualTo(0), "本地没有上次的结论，请求之后仍然不放行");

            platform.CompleteRequest();
            Assert.That(platform.LoadCount, Is.EqualTo(1), "服务端判 REQUIRED 就得加载表单");
            platform.CompleteLoad();
            Assert.That(platform.ShowCount, Is.EqualTo(1));

            platform.StoredConsentAllowsAds = true;   // 用户在表单上做完选择（同意或拒绝都是 OBTAINED）
            platform.DismissForm();

            Assert.That(adsAllowed.Count, Is.EqualTo(1));
            Assert.That(finished, Is.EqualTo(new[] { true }));
        }

        [Test]
        public void NotRequiredSkipsTheFormAndFinishes()
        {
            var platform = new FakePlatform { StoredConsentAllowsAds = true, IsConsentFormRequired = false };
            var flow = NewFlow(platform, out _, out var finished);

            flow.MarkTermsAccepted();
            flow.MarkGameReady();
            platform.CompleteRequest();

            Assert.That(platform.LoadCount, Is.EqualTo(0), "不是 REQUIRED 就不该加载表单");
            Assert.That(finished, Is.EqualTo(new[] { true }));
        }

        /// <summary>照竞品：表单加载失败也直接收尾，不重试、不卡住游戏。</summary>
        [Test]
        public void FormLoadFailureStillFinishes()
        {
            var platform = new FakePlatform { IsConsentFormRequired = true };
            var flow = NewFlow(platform, out _, out var finished);

            flow.MarkTermsAccepted();
            flow.MarkGameReady();
            platform.CompleteRequest();
            platform.FailLoad();

            Assert.That(platform.ShowCount, Is.EqualTo(0));
            Assert.That(finished, Is.EqualTo(new[] { false }));
        }

        /// <summary>🔴 偏离的后一半：请求失败按退避重试，用尽才收尾。竞品是一次失败就整场放弃。</summary>
        [Test]
        public void FailedRequestRetriesOnTheBackoffThenFinishes()
        {
            var platform = new FakePlatform();
            var clock = 0f;
            var finished = new List<bool>();
            var flow = new ConsentFlow(platform, new RecordingAnalytics(), () => clock,
                new ConsentFlow.RetryPolicy(3, 2f, 4f));
            flow.Finished += ok => finished.Add(ok);

            flow.MarkTermsAccepted();
            flow.MarkGameReady();
            platform.FailRequest();
            Assert.That(finished.Count, Is.EqualTo(0), "还有重试次数，先别收尾");

            clock = 1.9f;
            flow.Tick();
            Assert.That(platform.RequestCount, Is.EqualTo(1), "没到点不重试");

            clock = 2f;
            flow.Tick();
            Assert.That(platform.RequestCount, Is.EqualTo(2), "第一个间隔 2 秒");
            platform.FailRequest();

            clock = 2f + 7.9f;
            flow.Tick();
            Assert.That(platform.RequestCount, Is.EqualTo(2), "第二个间隔是 2×4 = 8 秒");

            clock = 2f + 8f;
            flow.Tick();
            Assert.That(platform.RequestCount, Is.EqualTo(3));
            platform.FailRequest();

            Assert.That(finished, Is.EqualTo(new[] { false }), "次数用尽才收尾，且带最终结论");

            clock = 1000f;
            flow.Tick();
            Assert.That(platform.RequestCount, Is.EqualTo(3), "收尾之后不再重试");
        }

        [Test]
        public void RetryThatSucceedsStillShowsTheForm()
        {
            var platform = new FakePlatform { IsConsentFormRequired = true };
            var clock = 0f;
            var flow = new ConsentFlow(platform, new RecordingAnalytics(), () => clock,
                new ConsentFlow.RetryPolicy(3, 2f, 4f));

            flow.MarkTermsAccepted();
            flow.MarkGameReady();
            platform.FailRequest();

            clock = 2f;
            flow.Tick();
            platform.CompleteRequest();

            Assert.That(platform.LoadCount, Is.EqualTo(1), "重试成功后该弹的表单照弹");
        }

        [Test]
        public void RetryPolicyOfOneAttemptIsTheOriginalPackageBehaviour()
        {
            var platform = new FakePlatform();
            var clock = 0f;
            var finished = new List<bool>();
            var flow = new ConsentFlow(platform, new RecordingAnalytics(), () => clock,
                new ConsentFlow.RetryPolicy(1, 2f, 4f));
            flow.Finished += ok => finished.Add(ok);

            flow.MarkTermsAccepted();
            flow.MarkGameReady();
            platform.FailRequest();

            Assert.That(finished, Is.EqualTo(new[] { false }), "只允许一次尝试时，失败即收尾——就是竞品那套");
            clock = 100f;
            flow.Tick();
            Assert.That(platform.RequestCount, Is.EqualTo(1));
        }

        [Test]
        public void EventsFollowTheOriginalPackageSequence()
        {
            var platform = new FakePlatform { IsConsentFormRequired = true, PurposeConsents = "11111111111" };
            var analytics = new RecordingAnalytics();
            var flow = new ConsentFlow(platform, analytics, () => 0f);

            flow.MarkTermsAccepted();
            flow.MarkGameReady();
            platform.CompleteRequest();
            platform.CompleteLoad();
            platform.DismissForm();

            Assert.That(analytics.Events, Is.EqualTo(new[]
            {
                ConsentEvents.InfoRequestStart,
                ConsentEvents.FormLoadStart,
                ConsentEvents.FormTryShow,
                ConsentEvents.FormShowSuccess,
            }));
            Assert.That(analytics.LastParameterOf(ConsentEvents.FormShowSuccess, ConsentEvents.Params.Purpose),
                Is.EqualTo("11111111111"), "成功事件带 IABTCF_PurposeConsents");
        }

        [Test]
        public void EveryAttemptLogsItsOwnRequestStart()
        {
            var platform = new FakePlatform();
            var clock = 0f;
            var analytics = new RecordingAnalytics();
            var flow = new ConsentFlow(platform, analytics, () => clock, new ConsentFlow.RetryPolicy(2, 2f, 4f));

            flow.MarkTermsAccepted();
            flow.MarkGameReady();
            platform.FailRequest();
            clock = 2f;
            flow.Tick();

            Assert.That(analytics.Events, Is.EqualTo(new[]
            {
                ConsentEvents.InfoRequestStart,
                ConsentEvents.InfoRequestStart,
            }), "重试也算一次请求，不逐次发就看不出重试");
        }

        [Test]
        public void PrivacyOptionsEntryFollowsTheStatusAndLogsBothEvents()
        {
            var platform = new FakePlatform();
            var analytics = new RecordingAnalytics();
            var flow = new ConsentFlow(platform, analytics, () => 0f);

            Assert.That(flow.IsPrivacyOptionsEntryVisible, Is.False);
            platform.IsPrivacyOptionsRequired = true;
            Assert.That(flow.IsPrivacyOptionsEntryVisible, Is.True);

            string dismissed = "unset";
            flow.ShowPrivacyOptions(error => dismissed = error);
            platform.DismissPrivacyForm();

            Assert.That(analytics.Events, Is.EqualTo(new[]
            {
                ConsentEvents.PrivacyFormTryShow,
                ConsentEvents.PrivacyFormShowSuccess,
            }));
            Assert.That(dismissed, Is.Null);
        }

        [Test]
        public void PrivacyOptionsFailureLogsOnlyTheAttempt()
        {
            var platform = new FakePlatform();
            var analytics = new RecordingAnalytics();
            var flow = new ConsentFlow(platform, analytics, () => 0f);

            string dismissed = null;
            flow.ShowPrivacyOptions(error => dismissed = error);
            platform.DismissPrivacyForm("没有有效 Activity");

            Assert.That(analytics.Events, Is.EqualTo(new[] { ConsentEvents.PrivacyFormTryShow }));
            Assert.That(dismissed, Is.EqualTo("没有有效 Activity"));
        }

        private static ConsentFlow NewFlow(FakePlatform platform, out List<int> adsAllowed, out List<bool> finished)
        {
            var allowed = new List<int>();
            var done = new List<bool>();
            var flow = new ConsentFlow(platform, new RecordingAnalytics(), () => 0f);
            flow.AdsAllowed += () => allowed.Add(1);
            flow.Finished += ok => done.Add(ok);
            adsAllowed = allowed;
            finished = done;
            return flow;
        }

        private sealed class FakePlatform : IConsentPlatform
        {
            private bool requested;
            private Action onRequestSuccess;
            private Action<string> onRequestFailure;
            private Action onFormLoaded;
            private Action<string> onFormLoadFailure;
            private Action<string> onFormDismissed;
            private Action<string> onPrivacyDismissed;

            /// <summary>上次会话存下来的结论够不够起广告。</summary>
            public bool StoredConsentAllowsAds { get; set; }

            public bool IsConsentFormRequired { get; set; }

            public bool IsPrivacyOptionsRequired { get; set; }

            public string PurposeConsents { get; set; }

            public string GppString { get; set; }

            public int RequestCount { get; private set; }

            public int LoadCount { get; private set; }

            public int ShowCount { get; private set; }

            /// <summary>🔴 复刻 UMP：调过更新请求之前，这个值恒为假。</summary>
            public bool CanRequestAds => requested && StoredConsentAllowsAds;

            public void RequestConsentInfoUpdate(Action onSuccess, Action<string> onFailure)
            {
                RequestCount++;
                requested = true;
                onRequestSuccess = onSuccess;
                onRequestFailure = onFailure;
            }

            public void CompleteRequest() => onRequestSuccess();

            public void FailRequest(string error = "连不上") => onRequestFailure(error);

            public void LoadConsentForm(Action onLoaded, Action<string> onFailure)
            {
                LoadCount++;
                onFormLoaded = onLoaded;
                onFormLoadFailure = onFailure;
            }

            public void CompleteLoad() => onFormLoaded();

            public void FailLoad(string error = "加载失败") => onFormLoadFailure(error);

            public void ShowConsentForm(Action<string> onDismissed)
            {
                ShowCount++;
                onFormDismissed = onDismissed;
            }

            public void DismissForm(string error = null) => onFormDismissed(error);

            public void ShowPrivacyOptionsForm(Action<string> onDismissed) => onPrivacyDismissed = onDismissed;

            public void DismissPrivacyForm(string error = null) => onPrivacyDismissed(error);
        }

        private sealed class RecordingAnalytics : IAnalyticsBackend
        {
            private readonly List<(string Name, AnalyticsParameter[] Parameters)> log =
                new List<(string, AnalyticsParameter[])>();

            public IEnumerable<string> Events
            {
                get
                {
                    var names = new List<string>();
                    foreach (var entry in log)
                    {
                        names.Add(entry.Name);
                    }

                    return names;
                }
            }

            public void LogEvent(string eventName, params AnalyticsParameter[] parameters)
            {
                log.Add((eventName, parameters));
            }

            public void SetUserProperty(string name, string value)
            {
            }

            public string LastParameterOf(string eventName, string parameterName)
            {
                for (var i = log.Count - 1; i >= 0; i--)
                {
                    if (log[i].Name != eventName)
                    {
                        continue;
                    }

                    foreach (var parameter in log[i].Parameters)
                    {
                        if (parameter.Name == parameterName)
                        {
                            return parameter.StringValue;
                        }
                    }
                }

                return null;
            }
        }
    }

    /// <summary>
    /// <see cref="UsPrivacy"/> 的 fail-open 语义：只有恰好等于那一串才算未同意。
    /// 这不是疏漏，是照竞品——它读不到那个键时走的就是「已同意」分支。
    /// </summary>
    public sealed class UsPrivacyTests
    {
        [Test]
        public void OnlyTheExactStringCountsAsNotAgreed()
        {
            Assert.That(UsPrivacy.IsAgreed(UsPrivacy.NotAgreedGppString), Is.False);
        }

        [Test]
        public void AnythingElseCountsAsAgreedIncludingAbsent()
        {
            Assert.That(UsPrivacy.IsAgreed(null), Is.True, "读不到那个键 → 已同意");
            Assert.That(UsPrivacy.IsAgreed(string.Empty), Is.True);
            Assert.That(UsPrivacy.IsAgreed(UsPrivacy.NotAgreedGppString + "g"), Is.True, "整串比较，差一个字符就不是它");
            Assert.That(UsPrivacy.IsAgreed(UsPrivacy.NotAgreedGppString.ToLowerInvariant()), Is.True, "大小写也算不同");
        }
    }
}
