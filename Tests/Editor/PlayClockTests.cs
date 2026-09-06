using NUnit.Framework;

namespace LevelTracking.Tests.EditMode
{
    /// <summary>
    /// <see cref="PlayClock"/> 的三条不变量（arrows DECISION-20260901-11，真机 A/B 验过），
    /// 用例名沿用 arrows 控制器级那三条，驱动改为直接构造注入。三条变异各恰好打红一条：
    /// `Stop` 每次重记时刻 → 第一条；停表期间读当前 → 第二条；`Tick` 无条件结算 → 第三条。
    /// </summary>
    public sealed class PlayClockTests
    {
        private const int FailPage = PlayClock.Backgrounded << 1;

        [Test]
        public void BackgroundingWhileTheFailPageIsOpenSettlesOnlyOnce()
        {
            var now = 0f;
            var clock = new PlayClock(() => now);
            clock.Restart();

            now = 10f;
            clock.Stop(FailPage);                // 失败页打开：停表时刻 = 10
            now = 20f;
            clock.OnApplicationPause(true);      // 失败页上切后台：已停着，不重记时刻
            now = 30f;
            clock.OnApplicationPause(false);
            clock.Tick();                        // 后台理由撤了，失败页还在 → 仍停着
            Assert.That(clock.ElapsedSeconds, Is.EqualTo(10f).Within(0.001f), "两个理由都撤光之前读数冻在 10");
            now = 40f;
            clock.Resume(FailPage);              // 全撤光：扣掉 10→40 整段（30 秒）
            now = 50f;

            Assert.That(clock.ElapsedSeconds, Is.EqualTo(20f).Within(0.001f),
                "停表段只结算一次、从最早的停表时刻起算：50 − 30 = 20；若 Stop 重记了时刻会得到 30");
        }

        [Test]
        public void AnEventReportedWhileTheClockIsStoppedReadsTheFrozenValue()
        {
            var now = 0f;
            var clock = new PlayClock(() => now);
            clock.Restart();

            now = 10f;
            clock.Stop(FailPage);
            now = 15f;

            Assert.That(clock.ElapsedSeconds, Is.EqualTo(10f).Within(0.001f), "停表期间读的是停表那一刻");
        }

        [Test]
        public void AnExtraFrameAfterThePauseSignalDoesNotCancelTheStop()
        {
            var now = 0f;
            var clock = new PlayClock(() => now);
            clock.Restart();

            now = 10f;
            clock.OnApplicationPause(true);
            now = 11f;
            clock.Tick();                        // Unity 在 pause(true) 之后仍可能再跑一帧
            now = 20f;
            Assert.That(clock.ElapsedSeconds, Is.EqualTo(10f).Within(0.001f), "多出来的那一帧不能把停表取消掉");

            now = 30f;
            clock.OnApplicationPause(false);
            Assert.That(clock.ElapsedSeconds, Is.EqualTo(10f).Within(0.001f), "恢复回调本身不结算，要等真实帧");
            now = 31f;
            clock.Tick();
            now = 41f;

            Assert.That(clock.ElapsedSeconds, Is.EqualTo(20f).Within(0.001f), "后台段 10→31 整段扣掉：41 − 21 = 20");
        }

        [Test]
        public void RestartClearsStandingStopsAndTakesAnOffset()
        {
            var now = 100f;
            var clock = new PlayClock(() => now);
            clock.Restart();
            clock.Stop(FailPage);

            now = 500f;
            clock.Restart(elapsedOffset: 30f);   // 恢复局中进度：已经玩了 30 秒
            now = 510f;

            Assert.That(clock.ElapsedSeconds, Is.EqualTo(40f).Within(0.001f), "原点 = 500 − 30，停表状态清空");
        }

        [Test]
        public void ResumingAReasonThatWasNeverSetIsANoOp()
        {
            var now = 0f;
            var clock = new PlayClock(() => now);
            clock.Restart();
            now = 5f;
            clock.Resume(FailPage);
            now = 8f;

            Assert.That(clock.ElapsedSeconds, Is.EqualTo(8f).Within(0.001f));
        }
    }
}
