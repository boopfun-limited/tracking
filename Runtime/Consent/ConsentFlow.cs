using System;

namespace Tracking.Consent
{
    /// <summary>
    /// 首启同意流程的状态机：把「什么时候向 UMP 发请求、失败怎么办、什么时候可以起广告」收在一处。
    /// 规格与逐条出处见 <c>Docs~/PRD_20260911_1509_首启同意流程SDK照oakever.md</c>；
    /// 行为真源是 <c>package-analysis</c> 仓那份竞品实测文档。
    ///
    /// 照竞品 oakever 的三件事：
    /// <list type="number">
    /// <item>触发时点 = 「条款已同意」与「游戏放行」**两个都到，取较晚者，且只跑一次**；</item>
    /// <item>服务端判 REQUIRED 才加载并显示表单；其余状态、请求失败、表单失败都直接进收尾；</item>
    /// <item>收尾时 <see cref="IConsentPlatform.CanRequestAds"/> 为真才放行广告，
    ///       **但无论成败都回调游戏**（<see cref="Finished"/>）。</item>
    /// </list>
    ///
    /// 🔴 **唯一偏离竞品的一条**（owner 2026-09-11 拍板，PRD §6）：请求发出**之后立刻**再查一次
    /// <see cref="IConsentPlatform.CanRequestAds"/>，为真就先放行广告；请求失败按退避重试。
    /// 竞品把这个检查排在请求**之前**，那一刻该值恒为假（见
    /// <see cref="IConsentPlatform.CanRequestAds"/> 的注释），于是它那条「沿用上次同意」的快速路径
    /// 永不触发，后果是每次冷启广告都等服务端回话、请求失败则整场无广告且不重试。
    ///
    /// **这条不动合规**：新用户本地没有上次的结论，那一刻仍为假，照样等回话、照样弹表单；
    /// 放行用的是用户**上次自己做过的**选择。
    /// </summary>
    public sealed class ConsentFlow
    {
        /// <summary>
        /// 请求失败后的重试节奏。🔴 **出厂值是旋钮不是契约**：三次（首发 + 两次重试）、2 秒起、每次 ×4，
        /// 即约 2 秒与 8 秒后各重试一次。没有实测支撑，只是「比不重试好、又不至于把表单拖到关卡中间」的初值。
        /// </summary>
        public readonly struct RetryPolicy
        {
            public RetryPolicy(int attempts, float firstDelaySeconds, float multiplier)
            {
                Attempts = attempts;
                FirstDelaySeconds = firstDelaySeconds;
                Multiplier = multiplier;
            }

            /// <summary>总尝试次数，**含第一次**。1 即等于竞品的「不重试」。</summary>
            public int Attempts { get; }

            public float FirstDelaySeconds { get; }

            public float Multiplier { get; }

            public static RetryPolicy Default => new RetryPolicy(3, 2f, 4f);
        }

        private readonly IConsentPlatform platform;
        private readonly IAnalyticsBackend analytics;
        private readonly Func<float> now;
        private readonly RetryPolicy retry;

        private bool termsAccepted;
        private bool gameReady;
        private bool started;
        private bool finished;
        private bool adsAllowed;
        private int attemptsUsed;

        /// <summary>下一次重试的时刻；<see cref="float.NaN"/> 表示没有待重试。</summary>
        private float retryAt = float.NaN;

        /// <param name="now">时钟。生产传 <c>() =&gt; Time.unscaledTime</c>，测试传假钟——与 <c>PlayClock</c> 同一套。</param>
        /// <param name="retry">不传即 <see cref="RetryPolicy.Default"/>。</param>
        public ConsentFlow(IConsentPlatform platform, IAnalyticsBackend analytics, Func<float> now, RetryPolicy? retry = null)
        {
            this.platform = platform ?? throw new ArgumentNullException(nameof(platform));
            this.analytics = analytics ?? throw new ArgumentNullException(nameof(analytics));
            this.now = now ?? throw new ArgumentNullException(nameof(now));
            this.retry = retry ?? RetryPolicy.Default;
        }

        /// <summary>
        /// 可以初始化广告 SDK 了。**只会触发一次**，可能早于 <see cref="Finished"/>
        /// （老用户走上面那条偏离），也可能与它同时（新用户、或上次没选过的）。
        /// 🔴 广告后端接这个信号即可，**本模块不认识任何广告 SDK**——AdMob 换 MAX 时这里一行不用改。
        /// </summary>
        public event Action AdsAllowed;

        /// <summary>
        /// 同意流程收尾，**无论成败都会触发一次**；参数是最终「能不能请求广告」。
        /// 照竞品：请求失败也回调，游戏不该无限等。
        /// </summary>
        public event Action<bool> Finished;

        /// <summary>设置页的「隐私偏好」入口显示与否。请求成功之前恒为假。</summary>
        public bool IsPrivacyOptionsEntryVisible => platform.IsPrivacyOptionsRequired;

        /// <summary>条款弹窗被点下（或老用户判定为早已同意）。</summary>
        public void MarkTermsAccepted()
        {
            termsAccepted = true;
            StartIfBothArrived();
        }

        /// <summary>游戏放行：冷启走到该跑 CMP 的那一步。</summary>
        public void MarkGameReady()
        {
            gameReady = true;
            StartIfBothArrived();
        }

        /// <summary>
        /// 每帧调一次（与 <c>PlayClock.Tick</c> 同一处）。做三件事：交付平台侧排队的跨线程回调、
        /// 在请求在途期间重读快速路径、到点了发重试。三件都没事做时是空转。
        ///
        /// 🔴 **不调它，整条流程就停住**——表单不弹、广告不放行。理由见
        /// <see cref="IConsentPlatform.Pump"/>。
        /// </summary>
        public void Tick()
        {
            platform.Pump();

            if (started && !finished)
            {
                // 🔴 快速路径必须在这里**重读**，不能只靠 SendRequest 里那一次同步读。
                // Android 实现把请求排到 Android UI 线程执行，那一跳完成之前
                // CanRequestAds 还是旧值（恒假）。只读一次的话，老用户那条
                // 「沿用上次同意、先起广告」永远不触发——正是 PRD §6 要修掉的竞品行为，
                // 而且症状一模一样地静默。窗口就是「请求已发出、服务端还没回话」这一段，
                // 所以收尾之后不再轮询。
                AllowAdsIfPossible();
            }

            if (float.IsNaN(retryAt) || now() < retryAt)
            {
                return;
            }

            retryAt = float.NaN;
            SendRequest();
        }

        /// <summary>
        /// 从设置页打开隐私选项表单。与主流程无关，可在流程收尾后任意时刻调。
        /// </summary>
        public void ShowPrivacyOptions(Action<string> onDismissed = null)
        {
            analytics.LogEvent(ConsentEvents.PrivacyFormTryShow);
            platform.ShowPrivacyOptionsForm(error =>
            {
                if (error == null)
                {
                    analytics.LogEvent(ConsentEvents.PrivacyFormShowSuccess);
                }

                onDismissed?.Invoke(error);
            });
        }

        private void StartIfBothArrived()
        {
            if (started || !termsAccepted || !gameReady)
            {
                return;
            }

            started = true;
            SendRequest();
        }

        private void SendRequest()
        {
            attemptsUsed++;
            analytics.LogEvent(ConsentEvents.InfoRequestStart);
            platform.RequestConsentInfoUpdate(OnUpdateSucceeded, OnUpdateFailed);

            // 🔴 偏离竞品的那一行：必须在请求**之后**读，之前恒为假。
            AllowAdsIfPossible();
        }

        private void OnUpdateSucceeded()
        {
            AllowAdsIfPossible();

            if (!platform.IsConsentFormRequired)
            {
                Finish();
                return;
            }

            analytics.LogEvent(ConsentEvents.FormLoadStart);
            platform.LoadConsentForm(OnFormLoaded, _ => Finish());
        }

        private void OnFormLoaded()
        {
            analytics.LogEvent(ConsentEvents.FormTryShow);
            platform.ShowConsentForm(error =>
            {
                if (error == null)
                {
                    analytics.LogEvent(
                        ConsentEvents.FormShowSuccess,
                        AnalyticsParameter.Of(ConsentEvents.Params.Purpose, platform.PurposeConsents ?? string.Empty));
                }

                Finish();
            });
        }

        private void OnUpdateFailed(string error)
        {
            if (attemptsUsed < retry.Attempts)
            {
                retryAt = now() + DelayAfter(attemptsUsed);
                return;
            }

            Finish();
        }

        /// <summary>第 n 次尝试失败之后等多久：n=1 → 首个间隔，之后每次乘 <see cref="RetryPolicy.Multiplier"/>。</summary>
        private float DelayAfter(int attempts)
        {
            var delay = retry.FirstDelaySeconds;
            for (var i = 1; i < attempts; i++)
            {
                delay *= retry.Multiplier;
            }

            return delay;
        }

        private void AllowAdsIfPossible()
        {
            if (adsAllowed || !platform.CanRequestAds)
            {
                return;
            }

            adsAllowed = true;
            AdsAllowed?.Invoke();
        }

        private void Finish()
        {
            if (finished)
            {
                return;
            }

            finished = true;
            retryAt = float.NaN;
            AllowAdsIfPossible();
            Finished?.Invoke(adsAllowed);
        }
    }
}
