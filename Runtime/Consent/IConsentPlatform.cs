using System;

namespace Tracking.Consent
{
    /// <summary>
    /// 同意流程与 Google UMP 之间的缝。存在的理由与 <see cref="IAnalyticsBackend"/> 同：
    /// 把「什么时候请求、失败怎么办、什么时候可以起广告」这些**会写错的判定**留在
    /// <c>Tracking.Consent</c>（全平台、EditMode 可测），把 JNI 关在
    /// <c>Tracking.Consent.Android</c> 里。
    ///
    /// 🔴 实现方**不得抛异常**：同意流程是旁路，JNI 出问题该降级成「请求失败」，
    /// 不能让一次正常的冷启崩在这里。
    ///
    /// 🔴 **每个带回调的方法，恰好回调一次**——不重复、也不能都不回。
    /// <see cref="ConsentFlow"/> 依赖这一条而**没有另加防御状态**：真实 UMP 就是这个形状，
    /// 为「万一桥写错了」再叠一层令牌是给不存在的情景加状态。
    /// 代价写在明处：桥若把同一次请求的失败回调发两次，重试次数会超出策略；
    /// 若一次都不回，流程会停在那里不收尾（游戏侧的加载不等它，所以不会卡住玩家）。
    /// **写桥的人负责满足这条契约。**
    /// </summary>
    public interface IConsentPlatform
    {
        /// <summary>
        /// 把实现方排队的回调交付到**调用它的这个线程**上。
        /// <see cref="ConsentFlow.Tick"/> 每帧调一次，所以「调用它的线程」就是 Unity 主线程。
        ///
        /// 🔴 **这个方法存在的理由**：UMP 的监听器由 Android 的 <c>Handler.post</c> 投递，
        /// 落在 **Android 主线程（UI 线程）**上，那不是 Unity 主线程。上面那条
        /// 「回调必须在 Unity 主线程」的要求，实现方就靠排队 + 这里交付来兑现。
        ///
        /// 🔴 **代价说在明处**：游戏不每帧调 <c>Tick()</c>，回调就永远不交付，
        /// 整条同意流程停住（表单不弹、广告不放行）。这是有意选的失败形态——
        /// 比「藏一个 <c>DontDestroyOnLoad</c> 的 GameObject 在包里自己泵」好：
        /// 那东西没建起来时同样什么都不发生，却**在 EditMode 里一条也测不到**。
        ///
        /// 没有跨线程回调的实现（Editor、测试假件）这里是空转。
        /// </summary>
        void Pump();

        /// <summary>
        /// 现在可不可以初始化广告 SDK。UMP 的语义是
        /// <c>is_pub_misconfigured</c> ∨ 状态 ∈ {NOT_REQUIRED, OBTAINED}。
        ///
        /// 🔴 **这个值在 <see cref="RequestConsentInfoUpdate"/> 被调用之前恒为假**，
        /// 与用户上次选过什么无关：UMP 只在本进程调过一次更新请求之后才肯把持久化状态读出来
        /// （4.0.0 实测：<c>consent_sdk.zzj.canRequestAds()</c> 先查一个在
        /// <c>requestConsentInfoUpdate</c> 头上同步置位的布尔 <c>zzj.zzg</c>）。
        /// 竞品 oakever 正是把「沿用上次同意」的检查排在请求**之前**，于是那条路径永不触发
        /// （PRD §6）。**要读它，必须在调用之后。**
        ///
        /// 🔴 **它是「最终一致」的，不保证调用一返回就新**：Android 实现要把请求绕到
        /// Android UI 线程上执行（<c>runOnUiThread</c>），那一跳之后这个值才会翻。
        /// 所以 <see cref="ConsentFlow"/> 在请求在途期间**每帧重读一次**，
        /// 而不是只在 <c>SendRequest</c> 里读那一次。少了那次重读，快速路径就退化成
        /// 竞品那条永不触发的死路——而且**一样是静默的**。
        /// </summary>
        bool CanRequestAds { get; }

        /// <summary>服务端判定这次要弹同意表单（状态 == REQUIRED）。请求成功之后才有意义。</summary>
        bool IsConsentFormRequired { get; }

        /// <summary>
        /// 要不要在设置页显示「隐私偏好」入口
        /// （<c>getPrivacyOptionsRequirementStatus() == REQUIRED</c>）。
        /// </summary>
        bool IsPrivacyOptionsRequired { get; }

        /// <summary>
        /// <c>IABTCF_PurposeConsents</c> 的当前值，没有就返回 null。
        /// 只用来给 <see cref="ConsentEvents.FormShowSuccess"/> 带参数。
        /// </summary>
        string PurposeConsents { get; }

        /// <summary>
        /// <c>IABGPP_HDR_GppString</c> 的当前值，没有就返回 null。喂给 <see cref="UsPrivacy"/>。
        /// </summary>
        string GppString { get; }

        /// <summary>
        /// 发一次同意信息更新请求（异步）。默认参数：**无 debug geography、无未成年标记**，照原包。
        /// 成功走 <paramref name="onSuccess"/>，失败走 <paramref name="onFailure"/>（带一句人话错误）。
        /// **两个回调都必须在 Unity 主线程上调**。
        /// </summary>
        void RequestConsentInfoUpdate(Action onSuccess, Action<string> onFailure);

        /// <summary>加载同意表单。</summary>
        void LoadConsentForm(Action onLoaded, Action<string> onFailure);

        /// <summary>
        /// 显示已加载的同意表单；关掉时回调，<c>null</c> 表示没出错。
        /// 没有有效 Activity 时由实现方等到下一次 <c>onResume</c>，照原包。
        /// </summary>
        void ShowConsentForm(Action<string> onDismissed);

        /// <summary>显示隐私选项表单（设置页入口）；关掉时回调，<c>null</c> 表示没出错。</summary>
        void ShowPrivacyOptionsForm(Action<string> onDismissed);
    }
}
