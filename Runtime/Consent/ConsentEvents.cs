namespace Tracking.Consent
{
    /// <summary>
    /// 同意模块发出的事件名与参数名的唯一来源。名字照竞品 oakever（规格见
    /// <c>Docs~/PRD_20260911_1509_首启同意流程SDK照oakever.md</c> §5.7），**语义一一对应**。
    ///
    /// 🔴 **两条条款弹窗事件的大小写与原包不同**：原包是 <c>dlg_show_LAW</c> / <c>btn_click_LAW</c>，
    /// 本包写成全小写。理由是本包自己的名字门只收 <c>^[a-z][a-z0-9_]{0,39}$</c>
    /// （<c>ConsentEventsTests</c> 钉着，与 <c>LevelTrackingEvents</c> 同一条），
    /// 而 GA4 的事件名**区分大小写**，两种写法在报表里是两个事件。这是**只差大小写、不差语义**的
    /// 偏离；要不要连大小写一起照抄，属 PRD §10-4 待 owner 拍板的那一项。
    ///
    /// 🔴 **改这里的任何名字都是破坏性升版本**，理由与 <c>LevelTrackingEvents</c> 同：GA4 事件名
    /// 一旦发出就进了 property 的字典，改名等于新开事件、历史不跟。
    /// </summary>
    public static class ConsentEvents
    {
        /// <summary>条款弹窗弹出。原包 <c>dlg_show_LAW</c>。</summary>
        public const string TermsDialogShow = "dlg_show_law";

        /// <summary>条款弹窗上那颗唯一的按钮被点下。原包 <c>btn_click_LAW</c>。</summary>
        public const string TermsDialogAccept = "btn_click_law";

        /// <summary>
        /// 向 UMP 发出一次同意信息更新请求。
        ///
        /// 🔴 **每次尝试都发一条**，不是每个流程一条——本包会在请求失败后重试
        /// （PRD §6，原包不重试），不逐次发就看不出重试。据此估算失败率时，
        /// 分母用本事件数、分子用「其后没有任何后续事件」的那些。
        /// </summary>
        public const string InfoRequestStart = "ump_consent_info_request_start";

        /// <summary>开始加载同意表单（仅当服务端判定 REQUIRED）。</summary>
        public const string FormLoadStart = "ump_consent_form_load_start";

        /// <summary>表单已加载，准备显示。</summary>
        public const string FormTryShow = "ump_consent_form_try_show";

        /// <summary>
        /// 表单显示并被用户关掉（无论他选的是同意还是拒绝——UMP 两种选择都算 OBTAINED）。
        /// 带 <see cref="Params.Purpose"/>。
        /// </summary>
        public const string FormShowSuccess = "ump_consent_form_show_success";

        /// <summary>从设置页的隐私入口重新打开隐私选项表单，准备显示。</summary>
        public const string PrivacyFormTryShow = "ump_privacy_form_try_show";

        /// <summary>隐私选项表单显示并被关掉。</summary>
        public const string PrivacyFormShowSuccess = "ump_privacy_form_show_success";

        /// <summary>本模块用到的参数名。</summary>
        public static class Params
        {
            /// <summary>
            /// 用户在表单里对各**用途**的选择，取自 <c>IABTCF_PurposeConsents</c>——
            /// 一串 "0"/"1"，第 N 位是第 N 个用途。原包就带这一个参数。
            /// </summary>
            public const string Purpose = "purpose";
        }
    }
}
