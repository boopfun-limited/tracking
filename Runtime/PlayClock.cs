using System;

namespace LevelTracking
{
    /// <summary>
    /// 埋点用的「这一次尝试玩了多久」时钟。从 arrows 的 <c>GameController</c> 摘出
    /// （PRD_20260906_1854 阶段 1 → 阶段 2 进包），语义与 arrows DECISION-20260901-11 定下、
    /// 真机 A/B 验过的那套
    /// **一字不改**，只是换了住处：
    ///
    /// * 停表理由是**集合**不是计数器——两种停表会嵌套（失败页开着时切后台），计数器少减
    ///   一次就永远停着，而集合的置位 / 清位都是幂等的。
    /// * 停表期间 <see cref="ElapsedSeconds"/> 读的是**停表那一刻**——「暂停中报出去的事件
    ///   该是什么数」因此不取决于调用顺序。
    /// * 后台段在**真实帧**上结算（<see cref="Tick"/>），并由「见过恢复」把门：Unity 在
    ///   <c>OnApplicationPause(true)</c> 之后仍可能再跑一帧，无条件结算会让挂起段一秒都扣不掉。
    /// * 🔴 不要改成「用 <c>realtimeSinceStartup</c> 量那一段再减掉」：那种写法的对错取决于
    ///   <c>unscaledTime</c> 有没有把挂起时长吸收进来，两种假设给出的修正方向相反。本写法
    ///   两种假设下都对，因为它**从不跨越那个边界测量**。
    ///
    /// 时钟注入：生产传 <c>() => Time.unscaledTime</c>，测试传假钟——<c>Time.unscaledTime</c>
    /// 在 EditMode 里不按测试的节奏走。
    /// </summary>
    public sealed class PlayClock
    {
        /// <summary>
        /// 时钟自己唯一认识的理由位（<see cref="OnApplicationPause"/> 用）。
        /// 调用方的理由位从 <c>Backgrounded &lt;&lt; 1</c> 起自定义。
        /// </summary>
        public const int Backgrounded = 1 << 0;

        private readonly Func<float> now;

        /// <summary>
        /// 计时原点。🔴 **不是只写一次的常量**：每次停表结束，<see cref="Resume"/> 会把它
        /// 整体往后推「停了多久」，于是「现在 − 原点」自然就不含那一段。
        /// </summary>
        private float origin;

        private int stops;

        /// <summary>停表那一刻的读数。只在 <see cref="stops"/> 非空时有意义。</summary>
        private float stoppedAt;

        /// <summary>见过一次 <c>OnApplicationPause(false)</c>，但还没在真实帧上结算。</summary>
        private bool backgroundResumeSeen;

        public PlayClock(Func<float> now)
        {
            this.now = now ?? throw new ArgumentNullException(nameof(now));
        }

        /// <summary>
        /// 装载：原点 = 现在 − <paramref name="elapsedOffset"/>，并清空全部停表状态。
        /// 恢复局中进度时传已玩秒数——这个参数问的是「这**一次尝试**玩了多久」，回一趟主页
        /// 不开启新的尝试。停表状态必须跟着原点一起清：重开是从失败页走的，那个理由不清掉，
        /// 新一关一开始就停着表，而且「停表时刻」还是上一关的。
        /// </summary>
        public void Restart(float elapsedOffset = 0f)
        {
            origin = now() - elapsedOffset;
            stops = 0;
            backgroundResumeSeen = false;
        }

        /// <summary>这一次尝试已经玩了多久（秒）。停表期间读的是停表那一刻。</summary>
        public float ElapsedSeconds => (stops == 0 ? now() : stoppedAt) - origin;

        /// <summary>停表。已经停着时**不**重记时刻——那正是嵌套的那一档要的语义。</summary>
        public void Stop(int reason)
        {
            if (stops == 0)
            {
                stoppedAt = now();
            }

            stops |= reason;
        }

        /// <summary>
        /// 撤掉一个停表理由；理由全撤光了才真的把那一段从计时里扣掉。
        /// 没停着时是 no-op（撤一个没置过的理由不会凭空扣时间）。
        /// </summary>
        public void Resume(int reason)
        {
            if (stops == 0)
            {
                return;
            }

            stops &= ~reason;
            if (stops != 0)
            {
                return;
            }

            origin += now() - stoppedAt;
        }

        /// <summary>转发宿主的生命周期回调；<c>paused=false</c> 只置「见过恢复」，不结算。</summary>
        public void OnApplicationPause(bool paused)
        {
            if (paused)
            {
                Stop(Backgrounded);
                backgroundResumeSeen = false;
                return;
            }

            backgroundResumeSeen = true;
        }

        /// <summary>
        /// 每帧调用。🔴 结算点必须是**真实帧**，不能是 <c>OnApplicationPause(false)</c> 本身：
        /// <c>unscaledTime</c> 到底在恢复回调之前还是之后才把挂起的那一大段吸收进来，是引擎
        /// 实现细节、本仓没有在真机上验过。跑到这里就一定是在一个真实帧上，读数必然已经是
        /// 恢复后的值——判据因此不依赖那个未验的顺序。
        /// </summary>
        public void Tick()
        {
            if (!backgroundResumeSeen)
            {
                return;
            }

            backgroundResumeSeen = false;
            Resume(Backgrounded);
        }
    }
}
