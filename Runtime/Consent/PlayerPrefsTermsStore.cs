using System;
using UnityEngine;

namespace Tracking.Consent
{
    /// <summary>
    /// <see cref="ITermsStore"/> 的 PlayerPrefs 实现，给**还没有落点的新游戏**用：
    /// 键由游戏传（理由见 <see cref="ITermsStore"/>），默认「没同意」，写一次就刷盘。
    ///
    /// 已经有落点的游戏不要改用它——那等于换键。存档字段（arrows）、自己的 prefs 门面
    /// （water_sort 的 <c>PlayerPrefsStore</c>、boopdoku 的 <c>IIntPrefs</c>）各自实现那两个成员即可，
    /// 本类一共就这么长。
    /// </summary>
    public sealed class PlayerPrefsTermsStore : ITermsStore
    {
        private readonly string key;

        /// <param name="key">游戏自己的键。带不带 <c>.v1</c> 后缀是游戏的口径，包不替它定。</param>
        public PlayerPrefsTermsStore(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentException("键不能为空——空键会让所有游戏共用同一条记录", nameof(key));
            }

            this.key = key;
        }

        public bool IsAccepted => PlayerPrefs.GetInt(key, 0) != 0;

        public void MarkAccepted()
        {
            PlayerPrefs.SetInt(key, 1);
            PlayerPrefs.Save();
        }
    }
}
