using System;

namespace Tracking.Consent
{
    /// <summary>
    /// 非 Android 平台（Editor、其余平台）用的空实现：**当作服务端判定「不需要同意」**——
    /// 直接放行广告、不弹表单、不显示隐私入口。
    ///
    /// 这不是「假装用户同意了」：那些平台上游戏根本没接广告与 UMP（装的是空广告后端），
    /// 这里放行的是一个什么都不做的后端。Android 上一律走 <c>UmpConsentPlatform</c>。
    /// </summary>
    public sealed class NullConsentPlatform : IConsentPlatform
    {
        public static readonly NullConsentPlatform Instance = new NullConsentPlatform();

        public bool CanRequestAds => true;

        public bool IsConsentFormRequired => false;

        public bool IsPrivacyOptionsRequired => false;

        public string PurposeConsents => null;

        public string GppString => null;

        public void RequestConsentInfoUpdate(Action onSuccess, Action<string> onFailure)
        {
            onSuccess?.Invoke();
        }

        public void LoadConsentForm(Action onLoaded, Action<string> onFailure)
        {
            onFailure?.Invoke("NullConsentPlatform 不加载表单");
        }

        public void ShowConsentForm(Action<string> onDismissed)
        {
            onDismissed?.Invoke("NullConsentPlatform 不显示表单");
        }

        public void ShowPrivacyOptionsForm(Action<string> onDismissed)
        {
            onDismissed?.Invoke("NullConsentPlatform 不显示表单");
        }
    }
}
