using System;
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

                    buffer.Attach(new FirebaseSink());
                });
            }
            catch (Exception error)
            {
                Debug.LogWarning($"[Analytics] Firebase 初始化入口抛了，本次会话不上报：{error}");
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
