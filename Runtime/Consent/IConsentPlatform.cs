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
    /// </summary>
    public interface IConsentPlatform
    {
        /// <summary>
        /// 现在可不可以初始化广告 SDK。UMP 的语义是
        /// <c>is_pub_misconfigured</c> ∨ 状态 ∈ {NOT_REQUIRED, OBTAINED}。
        ///
        /// 🔴 **这个值在 <see cref="RequestConsentInfoUpdate"/> 被调用之前恒为假**，
        /// 与用户上次选过什么无关：UMP 只在本进程调过一次更新请求之后才肯把持久化状态读出来。
        /// 竞品 oakever 正是把「沿用上次同意」的检查排在请求**之前**，于是那条路径永不触发
        /// （PRD §6）。**要读它，必须在调用之后。**
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
