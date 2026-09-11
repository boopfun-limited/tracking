namespace Tracking.Consent
{
    /// <summary>
    /// 美国隐私串的判定，照竞品 oakever（PRD §5.6）：拿 <c>IABGPP_HDR_GppString</c> 与一个
    /// **写死的串整串比较**，不等就算「已同意」。结果供广告后端用（MAX 期的 <c>setDoNotSell</c>）。
    /// </summary>
    public static class UsPrivacy
    {
        /// <summary>
        /// 原包写死的那一串。它是「加州、未同意」那一种 GPP 串的具体取值——
        /// 原包**不解析** GPP，只认这一个字面量。
        /// </summary>
        public const string NotAgreedGppString = "DBABL~BVQVAAAAAg";

        /// <summary>
        /// 🔴 **这是 fail-open 的**：只有恰好等于 <see cref="NotAgreedGppString"/> 才算未同意，
        /// 其余一律算已同意——**包括 null、空串、以及任何别的州 / 别的版本的 GPP 串**。
        /// 照抄原包的语义，不是疏漏：原包读不到那个键时走的就是「已同意」分支。
        /// 换句话说，本函数回答的不是「用户同意了吗」，而是「是不是那一种已知的拒绝」。
        /// </summary>
        public static bool IsAgreed(string gppString)
        {
            return gppString != NotAgreedGppString;
        }
    }
}
