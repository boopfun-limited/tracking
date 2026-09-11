using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using UnityEngine;

namespace Tracking.Consent.Android
{
    /// <summary>
    /// <see cref="IConsentPlatform"/> 的 Android 实现：一层薄 JNI，接到包内的 Java 桥
    /// <c>com.gthbj.tracking.consent.ConsentBridge</c>（源码在同目录
    /// <c>TrackingConsent.androidlib/</c>）。
    ///
    /// 🔴 **只有接线，没有判定**：本程序集 <c>includePlatforms: ["Android"]</c>，
    /// EditMode 根本不编译它——写进这里的任何判断，快车道全绿也证明不了它对。
    /// 与 <c>Tracking.Identity.Android</c> / <c>Tracking.Firebase.Android</c> 同一条理由。
    /// 判定全在 <see cref="ConsentFlow"/>（全平台、EditMode 可测）。
    ///
    /// 🔴 **两次线程转换，一次都不能省**：
    /// <list type="number">
    /// <item>Unity 线程 → UI 线程：Java 桥里的 <c>runOnUiThread</c>；</item>
    /// <item>UI 线程 → Unity 线程：桥的回调落在 Android 主线程上，本类**排队**，
    ///       由 <see cref="Pump"/> 在 Unity 线程上交付。在 UI 线程上直接跑
    ///       <see cref="ConsentFlow"/> 的回调会碰 Unity API，是未定义行为。</item>
    /// </list>
    ///
    /// 消费方要做的两件事（都在**游戏仓**，包管不了）见包 README「同意模块的 Android 侧」：
    /// 声明 <c>com.google.android.ump:user-messaging-platform</c> 依赖、
    /// 以及每帧调一次 <c>ConsentFlow.Tick()</c>（不调则回调永不交付）。
    /// keep 规则不用游戏管——随 <c>.androidlib</c> 的 consumer 规则走。
    /// </summary>
    public sealed class UmpConsentPlatform : IConsentPlatform
    {
        private const string BridgeClassName = "com.gthbj.tracking.consent.ConsentBridge";
        private const string CallbackInterfaceName = "com.gthbj.tracking.consent.ConsentCallback";

        /// <summary>IAB TCF v2.2 的目的同意位串。只用来给表单成功事件带参数。</summary>
        private const string PurposeConsentsKey = "IABTCF_PurposeConsents";

        /// <summary>IAB GPP 头串，喂给 <see cref="UsPrivacy"/>。</summary>
        private const string GppStringKey = "IABGPP_HDR_GppString";

        private readonly ConcurrentQueue<Action> pending = new ConcurrentQueue<Action>();

        /// <summary>
        /// 还没回调的代理。
        ///
        /// 🔴 **不钉住就会被 GC 掉**：Java 那侧对 <see cref="AndroidJavaProxy"/> 的持有
        /// 不保证托管对象存活，丢了的症状是**什么都不发生**——不报错、不进日志
        /// （<c>AppSetIdUserProperty</c> 那条链踩过同一个坑，那里用的是一个静态字段）。
        /// 这里同时在两个线程上动（Unity 线程加、UI 线程删），所以要锁。
        /// </summary>
        private readonly HashSet<AndroidJavaProxy> alive = new HashSet<AndroidJavaProxy>();

        private readonly AndroidJavaClass bridge;
        private readonly AndroidJavaObject activity;

        public UmpConsentPlatform()
        {
            try
            {
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                {
                    activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                }

                bridge = new AndroidJavaClass(BridgeClassName);
            }
            catch (Exception error)
            {
                // 桥找不到 = R8 把它改名了，或者 .androidlib 根本没进构建。
                // 两种都不该崩掉一次正常冷启：降级成「所有请求都失败」，让流程照常收尾。
                bridge = null;
                Debug.LogError(
                    "[Tracking] 接不上同意桥，本次会话不会请求 UMP（广告按「不可请求」处理）：" + error);
            }
        }

        public bool CanRequestAds => CallBool("canRequestAds");

        public bool IsConsentFormRequired => CallBool("isConsentFormRequired");

        public bool IsPrivacyOptionsRequired => CallBool("isPrivacyOptionsRequired");

        public string PurposeConsents => ReadPreference(PurposeConsentsKey);

        public string GppString => ReadPreference(GppStringKey);

        public void RequestConsentInfoUpdate(Action onSuccess, Action<string> onFailure)
        {
            Invoke("requestConsentInfoUpdate", onSuccess, onFailure);
        }

        public void LoadConsentForm(Action onLoaded, Action<string> onFailure)
        {
            Invoke("loadConsentForm", onLoaded, onFailure);
        }

        public void ShowConsentForm(Action<string> onDismissed)
        {
            Invoke("showConsentForm", () => onDismissed?.Invoke(null), error => onDismissed?.Invoke(error));
        }

        public void ShowPrivacyOptionsForm(Action<string> onDismissed)
        {
            Invoke(
                "showPrivacyOptionsForm",
                () => onDismissed?.Invoke(null),
                error => onDismissed?.Invoke(error));
        }

        /// <summary>
        /// 把桥排队的回调在**当前线程**上交付。由 <see cref="ConsentFlow.Tick"/> 每帧调。
        /// </summary>
        public void Pump()
        {
            while (pending.TryDequeue(out var deliver))
            {
                try
                {
                    deliver();
                }
                catch (Exception error)
                {
                    // 一条回调里的异常不该把后面排队的那些一起吃掉。
                    Debug.LogError("[Tracking] 同意回调抛了：" + error);
                }
            }
        }

        private bool CallBool(string method)
        {
            if (bridge == null)
            {
                return false;
            }

            try
            {
                return bridge.CallStatic<bool>(method);
            }
            catch (Exception error)
            {
                Debug.LogWarning($"[Tracking] 同意桥 {method} 抛了，按假处理：{error}");
                return false;
            }
        }

        private string ReadPreference(string key)
        {
            if (bridge == null)
            {
                return null;
            }

            try
            {
                return bridge.CallStatic<string>("readPreference", activity, key);
            }
            catch (Exception error)
            {
                Debug.LogWarning($"[Tracking] 读 {key} 失败：{error}");
                return null;
            }
        }

        /// <summary>
        /// 调一个带回调的桥方法。**恰好回调一次**由两边共同保证：Java 那侧的 <c>Once</c>
        /// 包住 UMP 的监听器，这里则在桥根本调不起来时**自己补一次失败**。
        /// </summary>
        private void Invoke(string method, Action onSuccess, Action<string> onFailure)
        {
            if (bridge == null)
            {
                Enqueue(() => onFailure?.Invoke("同意桥不可用"));
                return;
            }

            var callback = new BridgeCallback(this, onSuccess, onFailure);

            lock (alive)
            {
                alive.Add(callback);
            }

            try
            {
                bridge.CallStatic(method, activity, callback);
            }
            catch (Exception error)
            {
                // 桥没接住这一次调用 → Java 的 Once 不会被触发，失败得由这里补，
                // 否则流程会停在那儿等一个永远不来的回调。
                if (callback.Claim())
                {
                    Enqueue(() => onFailure?.Invoke("调用同意桥失败：" + error));
                }
            }
        }

        private void Enqueue(Action deliver)
        {
            pending.Enqueue(deliver);
        }

        private void Retire(AndroidJavaProxy callback)
        {
            lock (alive)
            {
                alive.Remove(callback);
            }
        }

        /// <summary>
        /// <c>ConsentCallback</c> 的托管实现。🔴 <see cref="onResult"/> 跑在
        /// **Android UI 线程**上——除了入队什么都不许做。
        /// </summary>
        private sealed class BridgeCallback : AndroidJavaProxy
        {
            private readonly UmpConsentPlatform owner;
            private readonly Action onSuccess;
            private readonly Action<string> onFailure;

            /// <summary>0 = 还没人认领；1 = 已认领。<see cref="Claim"/> 说明它为什么存在。</summary>
            private int claimed;

            public BridgeCallback(UmpConsentPlatform owner, Action onSuccess, Action<string> onFailure)
                : base(CallbackInterfaceName)
            {
                this.owner = owner;
                this.onSuccess = onSuccess;
                this.onFailure = onFailure;
            }

            /// <summary>
            /// 认领这一次回调，成功返回 <c>true</c>。
            ///
            /// 🔴 这**不是**「桥可能回调两次」的防御——那条契约由 Java 侧的 <c>Once</c> 兑现。
            /// 它挡的是另一件事：<see cref="Invoke"/> 里 <c>CallStatic</c> 抛异常时，
            /// 我们分不清桥是「还没开始」还是「已经排了回调又抛的」。两边都走这个门，
            /// 谁先到谁算数，另一边直接丢掉。
            /// </summary>
            public bool Claim()
            {
                return System.Threading.Interlocked.Exchange(ref claimed, 1) == 0;
            }

            /// <summary>
            /// Java 签名是 <c>void onResult(String)</c>；Unity 按方法名与参数个数派发，
            /// 所以这里的名字必须是小写的 <c>onResult</c>。
            /// </summary>
            public void onResult(string error)
            {
                if (!Claim())
                {
                    return;
                }

                owner.Retire(this);
                owner.Enqueue(() =>
                {
                    if (error == null)
                    {
                        onSuccess?.Invoke();
                    }
                    else
                    {
                        onFailure?.Invoke(error);
                    }
                });
            }
        }
    }
}
