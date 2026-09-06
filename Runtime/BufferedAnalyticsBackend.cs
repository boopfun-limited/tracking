using System;
using System.Collections.Generic;

namespace LevelTracking
{
    /// <summary>
    /// 在真实后端就绪之前缓存事件的装配件。
    ///
    /// 🔴 存在的理由是一个**静默丢事件**的时序窗：宿主装配（`BeforeSceneLoad`）是同步的，
    /// 而 Firebase 的依赖检查是异步的（`CheckAndFixDependenciesAsync`），冷启动慢机上要一两秒。
    /// 玩家点进第一关往往就落在这个窗口里——也就是说，**丢掉的恰好是首次会话的第一关**，
    /// 而那是留存漏斗最关心的一格。丢了不会报错、DebugView 里也不会缺一行提示，
    /// 只是那个数字长期偏低，没有任何东西会告诉你。
    ///
    /// 把这段判定放在 `LevelTracking`（全平台）而不是 Android 侧，是因为后者
    /// `includePlatforms: ["Android"]`、EditMode 不编译，写进去就没有任何测试够得着。
    /// </summary>
    public sealed class BufferedAnalyticsBackend : IAnalyticsBackend
    {
        /// <summary>
        /// 缓冲上限。够装完就绪前的自然事件量（进程启动到 Firebase 就绪，通常个位数），
        /// 又不至于在后端**永远**不就绪时把内存吃穿——那种情况下该丢就丢，
        /// 埋点不值得为自己拖垮一局游戏。
        /// </summary>
        public const int DefaultCapacity = 32;

        private readonly object gate = new object();
        private readonly Queue<PendingEvent> pending = new Queue<PendingEvent>();
        private readonly int capacity;

        private IAnalyticsBackend target;
        private int droppedEventCount;

        public BufferedAnalyticsBackend(int capacity = DefaultCapacity)
        {
            if (capacity <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(capacity), capacity, "缓冲上限必须为正；不需要缓冲就直接用真实后端。");
            }

            this.capacity = capacity;
        }

        /// <summary>已缓存、尚未转发的事件数。</summary>
        public int PendingEventCount
        {
            get
            {
                lock (gate)
                {
                    return pending.Count;
                }
            }
        }

        /// <summary>
        /// 因缓冲满或事件名非法而丢弃的事件数。**刻意留成可读的计数而不是纯静默**：
        /// 上报链路出问题时，这个数字是唯一能自证「丢了多少」的东西。
        /// </summary>
        public int DroppedEventCount
        {
            get
            {
                lock (gate)
                {
                    return droppedEventCount;
                }
            }
        }

        public bool IsAttached
        {
            get
            {
                lock (gate)
                {
                    return target != null;
                }
            }
        }

        /// <summary>
        /// 接上真实后端，并把缓存的事件按**原始顺序**补报一遍。
        ///
        /// 🔴 转发在锁内完成，不是疏忽：放到锁外做，另一个线程新来的事件会插到补报的事件**前面**，
        /// 事件顺序就此乱掉，而 GA4 那头是按到达顺序排的、事后无从校正。整个进程里这段只跑一次、
        /// 至多 <see cref="DefaultCapacity"/> 条，代价可以忽略；能自陷的只有「后端在
        /// LogEvent 里回调本对象」，而后端的契约本来就是不得回调、不得抛。
        /// </summary>
        public void Attach(IAnalyticsBackend backend)
        {
            if (backend == null)
            {
                throw new ArgumentNullException(nameof(backend));
            }

            if (ReferenceEquals(backend, this))
            {
                throw new ArgumentException("埋点后端不能接到自己身上。", nameof(backend));
            }

            lock (gate)
            {
                if (target != null)
                {
                    throw new InvalidOperationException(
                        "埋点后端已经接上；第二次 Attach 只会让「谁在收事件」取决于初始化顺序。");
                }

                while (pending.Count > 0)
                {
                    var buffered = pending.Dequeue();
                    backend.LogEvent(buffered.EventName, buffered.Parameters);
                }

                target = backend;
            }
        }

        public void LogEvent(string eventName, params AnalyticsParameter[] parameters)
        {
            if (string.IsNullOrWhiteSpace(eventName))
            {
                // 空事件名到了 Firebase 一样是静默丢弃，在这里丢至少还进得了计数。
                lock (gate)
                {
                    droppedEventCount++;
                }

                return;
            }

            IAnalyticsBackend attached;
            lock (gate)
            {
                attached = target;
                if (attached == null)
                {
                    if (pending.Count >= capacity)
                    {
                        droppedEventCount++;
                        return;
                    }

                    pending.Enqueue(new PendingEvent(eventName, parameters ?? Array.Empty<AnalyticsParameter>()));
                    return;
                }
            }

            // 已接上之后不再走锁：稳态下每关几条事件，不该为它们互相排队。
            attached.LogEvent(eventName, parameters);
        }

        private readonly struct PendingEvent
        {
            public PendingEvent(string eventName, AnalyticsParameter[] parameters)
            {
                EventName = eventName;
                Parameters = parameters;
            }

            public string EventName { get; }

            public AnalyticsParameter[] Parameters { get; }
        }
    }
}
