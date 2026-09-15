using System;
using System.Collections.Generic;
using Tracking;
using Firebase;
using Firebase.Analytics;
using Firebase.Extensions;
using UnityEngine;

namespace Tracking.Firebase.Android
{
    /// <summary>
    /// 把 Firebase 关在这一个文件里的那道墙。**这里只有接线，没有判定**——
    /// 「什么时候报、报哪个事件、就绪前怎么办」全在 `LevelTracking`（全平台）那侧，
    /// 因为本程序集 `includePlatforms: ["Android"]`、EditMode 根本不编译它：
    /// 写进这里的任何判断，快车道全绿也证明不了它对。
    /// </summary>
    public static class FirebaseAnalyticsBackend
    {
        /// <summary>
        /// 异步等 Firebase 依赖就绪，就绪后把真实上报口接到缓冲件上。
        ///
        /// 🔴 **失败是静默降级，不是抛**：装配跑在 `BeforeSceneLoad`，这里抛出去等于
        /// 因为埋点起不来而整包黑屏。拿不到 Firebase（Play 服务缺失 / 过期 / 被禁用）时
        /// 缓冲件永远不 Attach，事件攒满上限后按序丢弃并计数——玩家一切照常。
        /// </summary>
        public static void AttachWhenReady(BufferedAnalyticsBackend buffer)
        {
            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            try
            {
                FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task =>
                {
                    if (task.IsFaulted || task.IsCanceled)
                    {
                        Debug.LogWarning($"[Analytics] Firebase 依赖检查未完成，本次会话不上报：{task.Exception}");
                        return;
                    }

                    if (task.Result != DependencyStatus.Available)
                    {
                        // 最常见的是这台机器没有 / 没更新 Google Play 服务。海外机型上少见，
                        // 但不是不可能，而它绝不该影响玩游戏。
                        Debug.LogWarning($"[Analytics] Firebase 依赖不可用（{task.Result}），本次会话不上报。");
                        return;
                    }

                    // 照 oakever 写死同意（D-20260911-01、PRD §5.1）：先于接上，回放的缓冲事件也落在这份同意值里。
                    GrantConsent();

                    var sink = new FirebaseSink();
                    // 🔴 先设身份再接上：接上那一刻缓冲件会按序回放此前攒下的事件，它们都得带 user_id。
                    // 安装标识由包自己生成（InstallId），游戏侧不用、也不要再设 user_id。
                    sink.SetUserId(InstallId.GetOrCreate());
                    buffer.Attach(sink);
                });
            }
            catch (Exception error)
            {
                Debug.LogWarning($"[Analytics] Firebase 初始化入口抛了，本次会话不上报：{error}");
            }
        }

        /// <summary>
        /// 开采集 + 四项同意写死 GRANTED——照 oakever（D-20260911-01「写死 GRANTED」、PRD §5.1；原包在
        /// `Application.onCreate` 里设）。
        ///
        /// 🔴 **用户在 UMP 里的选择不靠这里传**：UMP 每次请求回话后自己经反射调 Firebase 的 `setConsent`，
        /// 拒绝即写回四项 DENIED（原包 root 实测）。写死的 GRANTED 因此只管「本次冷启到 UMP 回话」这一段，
        /// 原包实测约 5.5 秒——这是照抄的行为，不是缺陷（PRD §9-7），别顺手「修好」。
        ///
        /// 🔴 **顺序前提**：这一步必须落在本进程 UMP 回话之前，否则会把刚推进来的拒绝盖回 GRANTED、盖一整场。
        /// 原包是 onCreate 同步设，天然在前；这里在依赖检查回调里（慢机一两秒），靠的是游戏发同意信号比它晚
        /// （arrows 在 Boot 收尾）。游戏若在 Firebase 就绪之前就发出同意请求，这个前提不成立。
        ///
        /// PRD §5.2「点下条款后再设一次」不做：同意流程要等条款信号才发请求，同一进程里那之前不会有 UMP 推送，
        /// 再设一次改不了任何值。
        /// </summary>
        private static void GrantConsent()
        {
            try
            {
                FirebaseAnalytics.SetAnalyticsCollectionEnabled(true);
                FirebaseAnalytics.SetConsent(new Dictionary<ConsentType, ConsentStatus>
                {
                    { ConsentType.AdStorage, ConsentStatus.Granted },
                    { ConsentType.AnalyticsStorage, ConsentStatus.Granted },
                    { ConsentType.AdUserData, ConsentStatus.Granted },
                    { ConsentType.AdPersonalization, ConsentStatus.Granted },
                });
            }
            catch (Exception error)
            {
                // 接口契约：不得抛。设不上顶多这一场按 Firebase 已存的同意值跑，不该崩掉装配。
                Debug.LogWarning($"[Analytics] 设 Firebase 同意值失败：{error}");
            }
        }

        private sealed class FirebaseSink : IAnalyticsBackend
        {
            public void LogEvent(string eventName, params AnalyticsParameter[] parameters)
            {
                try
                {
                    if (parameters == null || parameters.Length == 0)
                    {
                        FirebaseAnalytics.LogEvent(eventName);
                        return;
                    }

                    var converted = new Parameter[parameters.Length];
                    for (var i = 0; i < parameters.Length; i++)
                    {
                        converted[i] = Convert(parameters[i]);
                    }

                    FirebaseAnalytics.LogEvent(eventName, converted);
                }
                catch (Exception error)
                {
                    // 接口契约：上报不得抛。埋点是旁路，不能让一次正常的通关崩在这儿。
                    Debug.LogWarning($"[Analytics] 上报 {eventName} 失败：{error}");
                }
            }

            public void SetUserProperty(string name, string value)
            {
                try
                {
                    // value 传 null 即清掉该属性，这是 Firebase 自己的语义，原样透传。
                    FirebaseAnalytics.SetUserProperty(name, value);
                }
                catch (Exception error)
                {
                    // 接口契约：不得抛。属性设不上顶多少一列维度，不该崩掉一局游戏。
                    Debug.LogWarning($"[Analytics] 设置用户属性 {name} 失败：{error}");
                }
            }

            /// <summary>只给 AttachWhenReady 用；不在 IAnalyticsBackend 上——user_id 是包的决定，不是游戏的。</summary>
            public void SetUserId(string userId)
            {
                try
                {
                    // null 即清掉，Firebase 自己的语义，原样透传。
                    FirebaseAnalytics.SetUserId(userId);
                }
                catch (Exception error)
                {
                    // 接口契约：不得抛。user_id 设不上顶多这次会话的事件缺一列，不该崩掉一局游戏。
                    Debug.LogWarning($"[Analytics] 设置 user_id 失败：{error}");
                }
            }

            private static Parameter Convert(AnalyticsParameter parameter)
            {
                switch (parameter.Kind)
                {
                    case AnalyticsParameter.ValueKind.Long:
                        return new Parameter(parameter.Name, parameter.LongValue);
                    case AnalyticsParameter.ValueKind.Double:
                        return new Parameter(parameter.Name, parameter.DoubleValue);
                    default:
                        return new Parameter(parameter.Name, parameter.StringValue ?? string.Empty);
                }
            }
        }
    }
}
