namespace Tracking.Consent
{
    /// <summary>
    /// 「这台机器上的人点过条款弹窗没有」存在哪。缝存在的理由只有一条：**键归游戏**。
    ///
    /// 🔴 **包不能自带一个统一的新键。** 本模块出现之前四款游戏各写过一遍，落点各不相同
    /// （arrows 是存档里的字段 <c>legalConsentAccepted</c>、water_sort 是 PlayerPrefs 键
    /// <c>WaterSort.LegalConsent.v1</c>、arrows-3d 是 <c>arrows3d.legal.accepted.v1</c>、
    /// boopdoku 是 <c>boopdoku.Legal.ConsentAccepted</c>）。包换一个自己的键，等于把**已经同意过的
    /// 老玩家读成没同意**——而条款弹窗是挡住整个开屏的全屏闸，后果是所有老玩家冷启多吃一道闸，
    /// 且没有任何门会红。要换键只能是刻意新开 <c>.v2</c>（协议正文发生需要重新征得同意的实质变化时）。
    ///
    /// 只有两个成员是**有意的**：读一个 bool、写一次「同意了」。没有撤回、没有 <c>Reset</c>——
    /// 四款游戏都没做，boopdoku 还专门写了一句「不要加反向开关」。要重现首启弹窗，删键。
    /// </summary>
    public interface ITermsStore
    {
        /// <summary>
        /// 🔴 **默认必须是「没同意」**：没有这个键的老玩家应当看到一次弹窗。把老档默默算成已同意，
        /// 等于替玩家点了那颗按钮。闸是**默认值**，不是迁移代码。
        /// </summary>
        bool IsAccepted { get; }

        /// <summary>
        /// 记下同意。🔴 **实现方必须立刻落盘**：玩家点完就进游戏，进程随时可能被系统杀掉，
        /// 攒着不刷等于下次冷启再弹一遍。
        /// </summary>
        void MarkAccepted();
    }
}
