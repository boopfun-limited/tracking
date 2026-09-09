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

        /// <summary>
        /// 回调是异步来的，而 Java 那侧只持有代理对象的弱引用形态并不保证跨 GC 存活，
        /// 所以这里钉一个静态引用，等回调跑完再放掉。丢了它的症状是**什么都不发生**
        /// ——不报错、不进日志，正是最难查的那一类。
        /// </summary>
        private static AndroidJavaProxy pendingListener;

        /// <summary>
        /// 异步取 App Set ID，拿到就写进用户属性。
        ///
        /// 🔴 **失败是静默降级，不是抛**：装配跑在 `BeforeSceneLoad`，这里抛出去等于
        /// 因为一个标识符取不到而整包黑屏。拿不到（没有 Play 服务 / 版本太老 / 用户
        /// 重置中）就什么都不设——那一台设备参与不了跨 App join，仅此而已。
        ///
        /// 传 <c>BufferedAnalyticsBackend</c> 而不是已 Attach 的后端：属性与事件共用
        /// 同一条有序队列，补报时它落在「当时那一刻」而不是被提到所有事件前面。
        /// </summary>
        public static void SetWhenReady(IAnalyticsBackend analytics)
        {
            if (analytics == null)
            {
                throw new ArgumentNullException(nameof(analytics));
            }

            try
            {
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var appSet = new AndroidJavaClass("com.google.android.gms.appset.AppSet"))
                using (var client = appSet.CallStatic<AndroidJavaObject>("getClient", activity))
                using (var task = client.Call<AndroidJavaObject>("getAppSetIdInfo"))
                {
                    var listener = new SuccessListener(analytics);
                    pendingListener = listener;

                    // addOnSuccessListener 返回 Task 本身（链式），这里不用它，随手放掉。
                    using (task.Call<AndroidJavaObject>("addOnSuccessListener", listener))
                    {
                    }
                }
            }
            catch (Exception error)
            {
                pendingListener = null;
                Debug.LogWarning($"[Tracking] 取 App Set ID 的入口抛了，本次会话不带这个属性：{error}");
            }
        }

        private sealed class SuccessListener : AndroidJavaProxy
        {
            private readonly IAnalyticsBackend analytics;

            public SuccessListener(IAnalyticsBackend analytics)
                : base("com.google.android.gms.tasks.OnSuccessListener")
            {
                this.analytics = analytics;
            }

            /// <summary>
            /// Java 签名是 `void onSuccess(Object)`；运行时传进来的是 `AppSetIdInfo`。
            /// 名字与参数个数要和 Java 那边对得上，Unity 按这两样派发。
            /// </summary>
            public void onSuccess(AndroidJavaObject appSetIdInfo)
            {
                try
                {
                    if (appSetIdInfo == null)
                    {
                        return;
                    }

                    int scope = appSetIdInfo.Call<int>("getScope");
                    string id = appSetIdInfo.Call<string>("getId");

                    // 两条一起送。作用域先送：join 那侧要先看它才知道 id 能不能用，
                    // 而属性是按设置顺序落在后续事件上的，先后无所谓但读起来顺。
                    analytics.SetUserProperty(ScopePropertyName, DescribeScope(scope));

                    if (!string.IsNullOrEmpty(id))
                    {
                        analytics.SetUserProperty(IdPropertyName, id);
                    }
                }
                catch (Exception error)
                {
                    Debug.LogWarning($"[Tracking] 读 App Set ID 回调失败，本次会话不带这个属性：{error}");
                }
                finally
                {
                    appSetIdInfo?.Dispose();
                    pendingListener = null;
                }
            }

            private static string DescribeScope(int scope)
            {
                switch (scope)
                {
                    case ScopeDeveloper:
                        return "developer";
                    case ScopeApp:
                        return "app";
                    default:
                        // 没见过的取值原样送出去，别悄悄归到已知的两档里——
                        // 那会让「Play 服务加了新档」看起来跟正常情况一模一样。
                        return "unknown_" + scope.ToString(
                            System.Globalization.CultureInfo.InvariantCulture);
                }
            }
        }
    }
}
