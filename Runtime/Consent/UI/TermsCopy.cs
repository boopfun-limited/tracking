using System;
using UnityEngine;
using UnityEngine.UI;

namespace Tracking.Consent
{
    /// <summary>
    /// 首启条款弹窗上的字：标题、正文、同意键、两个文件名，14 种语言各一组（D-20260924-01）。
    /// 真源是同目录的 <c>Resources/TrackingTermsCopy.json</c>，字段名就是 JSON key，与 arrows / sudoku 的 `UiCopy` 同名字段一致。
    ///
    /// 🔴 **只有字，没有皮**：卡片、压暗层、颜色、字体、字号、按钮样式留在游戏（D-20260920-02 这一半仍然有效）。
    /// 🔴 **游戏的界面字体要把这份文件算进去**：界面字体多是按自家文案裁的子集，这里的字不在子集里的话，
    /// 手机上是空白、不报错（编辑器里会被系统字体补上，截图看不出）。这份文件改了措辞，各游戏都要重出字体。
    /// </summary>
    [Serializable]
    public sealed class TermsCopy
    {
        public const string ResourcePath = "TrackingTermsCopy";

        /// <summary>语言名，与 arrows / sudoku 的 `UiLanguage` 枚举同名（English、ChineseSimplified……）。</summary>
        public string language;

        public string consentTitle;

        /// <summary>`{0}` = <see cref="consentTermsLabel"/>，`{1}` = <see cref="consentPrivacyLabel"/>；填好的正文取 <see cref="Body"/>。</summary>
        public string consentBody;

        public string consentAccept;
        public string consentTermsLabel;
        public string consentPrivacyLabel;

        private static TermsCopy[] all;

        public static TermsCopy[] All =>
            all ??= JsonUtility.FromJson<Table>(Resources.Load<TextAsset>(ResourcePath).text).packs;

        /// <summary>正文，两个文件名已经填进去。</summary>
        public string Body => string.Format(consentBody, consentTermsLabel, consentPrivacyLabel);

        /// <summary>
        /// 按游戏当前的界面语言取（传 `UiLanguage` 的名字）。没有这种语言就给英文，与游戏界面「没备文案的落英文」同口径；
        /// 别按系统语言另取一次——界面落了英文、弹窗却是本地语言，一屏两种语言。
        /// </summary>
        public static TermsCopy For(string language) =>
            Array.Find(All, pack => pack.language == language) ?? Array.Find(All, pack => pack.language == "English");

        /// <summary>
        /// 把正文填进 <paramref name="body"/>，两个文件名画下划线、各自可点（<see cref="TextLinks"/>）。
        /// 它会改写正文里的换行，所以要在量正文高度、摆版式之前调；<paramref name="wrapWidth"/> 是折行宽度。
        /// </summary>
        public void ShowBody(Text body, float wrapWidth, Action openTerms, Action openPrivacy)
        {
            body.text = Body;
            TextLinks.Attach(body, wrapWidth, (consentTermsLabel, openTerms), (consentPrivacyLabel, openPrivacy));
        }

        [Serializable]
        private sealed class Table
        {
            public TermsCopy[] packs;
        }
    }
}
