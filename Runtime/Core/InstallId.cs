using System;
using UnityEngine;

namespace Tracking
{
    /// <summary>
    /// 安装标识：App 本地生成的 GUID，存 <see cref="PlayerPrefs"/>，随 App 数据一起备份 / 还原，
    /// 卸载即失、重装即换。包在 Firebase 就绪时把它设成 GA4 `user_id`
    /// （<c>FirebaseAnalyticsBackend.AttachWhenReady</c>），接入的游戏不用写任何一行。
    ///
    /// 为什么不用 SDK 的 ID：`user_pseudo_id` / AFID / App Set ID 在重装时全部重置，
    /// **备份还原也救不回来**（2026-09-09 真机实测）；而 PlayerPrefs 和游戏存档在同一份备份里——
    /// 存档回来它也回来，事件历史因此跟着「这份存档」。它不是能识别到个人的东西。
    /// </summary>
    public static class InstallId
    {
        public const string Key = "gthbj.tracking.install_id";

        public static string GetOrCreate()
        {
            var existing = PlayerPrefs.GetString(Key, "");
            if (!string.IsNullOrEmpty(existing))
            {
                return existing;
            }

            var created = Guid.NewGuid().ToString("N");
            PlayerPrefs.SetString(Key, created);
            PlayerPrefs.Save();
            return created;
        }
    }
}
