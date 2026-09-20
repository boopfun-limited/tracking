using System;

namespace Tracking.Consent
{
    /// <summary>
    /// 首启条款弹窗的**判定**：这次冷启弹不弹、弹的时候发哪条事件、点下之后按什么顺序收尾。
    /// 规格是 <c>Docs~/PRD_20260911_1509_首启同意流程SDK照oakever.md</c> §5.2（原包文档「三」）：
    /// 全球新装都弹、一颗同意按钮、无拒绝、不点不放行。
    ///
    /// 🔴 **界面不在这里，也不打算进来**（D-20260920-02）。弹窗的美术、文案、语言、排版留在游戏侧：
    /// 四款游戏四套主题、四套自建 uGUI、四份措辞不同的协议正文（两款有广告有归因、两款纯单机），
    /// 共用一份界面是把四张皮塞进一个接口。进包的是下面这三件**每个游戏都要写一遍、写错了还都静默**的事：
    /// <list type="number">
    /// <item>🔴 **老玩家（已经同意过的）也必须给 <see cref="ConsentFlow"/> 发信号**。不发，UMP 那条流程
    ///       就永远等不到第二个前置条件：请求不发、表单不弹、广告不放行，而且一声不响——
    ///       水位线上看到的只是「欧洲没广告」。water_sort 当初是自己发现并补了这一行的
    ///       （`GameManager.cs` 里老玩家那条分支），arrows 靠弹窗过后无条件补发才没踩到。</item>
    /// <item>两条事件发在正确的位置：<see cref="ConsentEvents.TermsDialogShow"/> 在弹出时、
    ///       <see cref="ConsentEvents.TermsDialogAccept"/> 在点下时。🔴 这两个名字 2026-09-11
    ///       建模块时就定下了，**在本类之前没有任何发射点**——GA4 里在钉上本 SHA 的构建之前
    ///       一条都没有，不要把这段空白当成「弹窗没弹」。</item>
    /// <item>同意**只落盘一次、且立刻落盘**（连点两下只算一次），见 <see cref="ITermsStore"/>。</item>
    /// </list>
    ///
    /// 用法：冷启时 <c>gate.ShowIfNeeded(BuildMyDialog)</c>，游戏那颗唯一的按钮上接 <c>gate.Accept</c>。
    /// 挡什么由游戏决定（arrows 挡住整条冷启协程、boopdoku 只挡点击），包不碰它。
    ///
    /// 🔴 **不含 PRD §5.1-3「同意之前的普通打点丢弃」那条**：本包的 <c>BufferedAnalyticsBackend</c>
    /// 是排队补报、不是丢弃。要照原包丢，是游戏自己决定同意前把哪个 backend 交出去；
    /// 而弹窗这两条按原包是**直发**的，所以给本类的 <c>analytics</c> 要是那个直发的后端。
    /// </summary>
    public sealed class TermsGate
    {
        private readonly ITermsStore store;
        private readonly IAnalyticsBackend analytics;
        private readonly ConsentFlow flow;

        /// <param name="flow">
        /// 不接广告、没有 UMP 的游戏（arrows-3d、boopdoku 这类纯单机）传 null：
        /// 弹窗与落盘照走，只是没有下游要放行。
        /// </param>
        public TermsGate(ITermsStore store, IAnalyticsBackend analytics, ConsentFlow flow = null)
        {
            this.store = store ?? throw new ArgumentNullException(nameof(store));
            this.analytics = analytics ?? throw new ArgumentNullException(nameof(analytics));
            this.flow = flow;
        }

        /// <summary>这台机器上已经同意过。游戏拿它决定要不要挡住开屏。</summary>
        public bool IsAccepted => store.IsAccepted;

        /// <summary>
        /// 冷启调一次。已同意 ⟹ 不弹，**就地给流程发「条款已同意」**，返回 false；
        /// 没同意 ⟹ 发 <see cref="ConsentEvents.TermsDialogShow"/>、调
        /// <paramref name="showDialog"/> 把界面立起来，返回 true。
        ///
        /// 同一次冷启调两次会发两条弹出事件——**这是有意的**：真弹了两次就该有两条。
        /// 要的是「别弹两次」，不是「弹了两次只记一次」。
        /// </summary>
        /// <param name="showDialog">同步立起游戏自己那张全屏闸；按钮上接 <see cref="Accept"/>。</param>
        /// <returns>弹了没有。</returns>
        public bool ShowIfNeeded(Action showDialog)
        {
            if (showDialog == null)
            {
                throw new ArgumentNullException(nameof(showDialog));
            }

            if (store.IsAccepted)
            {
                flow?.MarkTermsAccepted();
                return false;
            }

            analytics.LogEvent(ConsentEvents.TermsDialogShow);
            showDialog();
            return true;
        }

        /// <summary>
        /// 玩家点下那颗唯一的按钮。顺序照 PRD §5.2：发事件 → 落盘 → 给流程发信号。
        ///
        /// 连点两下（或者游戏两处都接了）只发一条事件、只写一次盘；给流程的信号每次都发，
        /// 因为 <see cref="ConsentFlow"/> 自己就是幂等的，而漏发的代价远大于多发。
        ///
        /// 🔴 **Firebase 那次「再写一遍四项 GRANTED」不在这里**：那是
        /// <c>Tracking.Firebase.Android</c> 的事，域模块之间互不引用（`CLAUDE.md` 的模块边界）。
        /// 装配根里 <c>AttachWhenReady</c> 一进来就写过一次，那一次覆盖的正是本弹窗这一段
        /// （D-20260916-01），所以这里不补。
        /// </summary>
        public void Accept()
        {
            if (!store.IsAccepted)
            {
                analytics.LogEvent(ConsentEvents.TermsDialogAccept);
                store.MarkAccepted();
            }

            flow?.MarkTermsAccepted();
        }
    }
}
