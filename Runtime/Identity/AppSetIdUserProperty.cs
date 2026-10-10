using System;
using UnityEngine;

namespace Tracking.Identity.Android
{
    /// <summary>
    /// 把 **App Set ID** 写成 GA4 用户属性。
    ///
    /// App Set ID 是 Google 对标 iOS `identifierForVendor`（IDFV）造的标识符：
    /// **同一个 Play Console 开发者账号下的所有 App，在同一台设备上拿到同一个值**，
    /// 把开发者的 App 全卸掉就重置。它是 join「这台设备玩过我另一款游戏吗」的键。
    /// 与 GAID 不同，它**不是广告标识符**，Google 明确给非广告用途用。
    ///
    /// 🔴 **不用 ANDROID_ID 顶替**：Play 政策不允许把 ANDROID_ID 与广告 ID 关联，
    /// 而 GA4 导出的同一行里已经有 `device.advertising_id`（GAID），把 ANDROID_ID
    /// 送进来等于在同一行里做这个关联。App Set ID 没有这条限制。
    ///
    /// 🔴 **只有接线，没有判定**：本程序集 `includePlatforms: ["Android"]`，
    /// EditMode 根本不编译它——写进这里的任何判断，快车道全绿也证明不了它对。
    /// 与 <c>Tracking.Firebase.Android</c> 同一条理由。
    ///
    /// 🔴 **这一面只有真机跑得出来，而且还得是 Play 装的包**：作用域是 Play 服务判的，
    /// 侧载 / adb install 的包拿到的是 `SCOPE_APP`（每个 App 一个值），不是
    /// `SCOPE_DEVELOPER`。所以本类**两个属性都送**——见 <see cref="ScopePropertyName"/>。
    /// </summary>
    public static class AppSetIdUserProperty
    {
        /// <summary>
        /// 属性名。GA4 上限 24 字符，这里 10 个。
        ///
        /// 🔴 **值的长度正好贴到上限**：App Set ID 是 UUID 字符串
        /// （`xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx`），**36 个字符**，而 GA4 用户属性
        /// 值的上限也**正好是 36**。一点余量都没有——将来谁想在这里加前缀
        /// （`android:` 之类）或换成别的拼法，会被 Firebase **静默截断 / 丢弃**，
        /// 症状是 join 的那一列大面积为空，而那时数据已经攒了几个月、补不回来。
        /// </summary>
        private const string IdPropertyName = "app_set_id";

        /// <summary>
        /// 作用域也送一条。**这不是冗余，是让这条链在上 Play 之前可验证**：
        /// 侧载包恒为 `app`，Play 装的包才是 `developer`。没有这条的话，
        /// 测试包上「属性没出现」和「代码根本没跑」分不出来。
        ///
        /// 🔴 **做跨 App join 时必须先按它过滤 `= "developer"`**：`app` 档的值是
        /// 每个 App 各一个，拿它 join 会把两个游戏的同一台设备判成两台，
        /// 而且不会报任何错。
        /// </summary>
        private const string ScopePropertyName = "app_set_id_scope";

        /// <summary>`AppSetIdInfo.SCOPE_APP` / `SCOPE_DEVELOPER` 的取值，见 Play 服务文档。</summary>
        private const int ScopeApp = 1;
        private const int ScopeDeveloper = 2;

        /// <summary>Google Task finishes entirely in Java; Unity reads the completed value.</summary>
        public static void SetWhenReady(IAnalyticsBackend analytics)
        {
            if (analytics == null) throw new ArgumentNullException(nameof(analytics));
            var host = new GameObject("Tracking App Set ID");
            UnityEngine.Object.DontDestroyOnLoad(host);
            host.AddComponent<AppSetIdPump>().Begin(analytics);
        }

        internal static void Apply(IAnalyticsBackend analytics, int scope, string id)
        {
            analytics.SetUserProperty(ScopePropertyName, scope == ScopeApp ? "app" : scope == ScopeDeveloper
                ? "developer" : "unknown_" + scope.ToString(System.Globalization.CultureInfo.InvariantCulture));
            if (!string.IsNullOrEmpty(id)) analytics.SetUserProperty(IdPropertyName, id);
        }
    }

    internal sealed class AppSetIdPump : MonoBehaviour
    {
        private AndroidJavaObject request;
        private IAnalyticsBackend analytics;
        private float nextPoll;

        internal void Begin(IAnalyticsBackend backend)
        {
            analytics = backend;
            try
            {
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var bridge = new AndroidJavaClass("com.gthbj.tracking.identity.AppSetIdBridge"))
                    request = bridge.CallStatic<AndroidJavaObject>("request", activity);
            }
            catch (Exception)
            {
                Debug.LogWarning("[Tracking] App Set ID request unavailable.");
                Destroy(gameObject);
            }
        }

        private void Update()
        {
            if (request == null || Time.realtimeSinceStartup < nextPoll) return;
            nextPoll = Time.realtimeSinceStartup + .25f;
            try
            {
                if (!request.Call<bool>("isDone")) return;
                if (request.Call<bool>("isSuccessful"))
                    AppSetIdUserProperty.Apply(analytics, request.Call<int>("getScope"), request.Call<string>("getId"));
                Debug.Log("[Tracking] App Set ID completion read on Unity thread.");
            }
            catch (Exception) { Debug.LogWarning("[Tracking] App Set ID result unavailable."); }
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            request?.Dispose();
            request = null;
            analytics = null;
        }
    }
}
