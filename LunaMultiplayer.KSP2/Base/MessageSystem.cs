using System.Collections.Concurrent;
using System.Collections.Generic;

namespace LunaMultiplayer.KSP2.Base
{
    /// <summary>
    /// 子系统基类（Sender / Handler 的公共父类），持有对父 System 的反向引用。
    /// 对应 LMP 的 SubSystem&lt;T&gt;。
    /// </summary>
    public abstract class SubSystem<T> where T : SystemBase, new()
    {
        public T System { get; internal set; }
    }

    /// <summary>
    /// 消息处理接口。每个 MessageSystem 的 Handler 实现它，并持有一条线程安全的入站队列。
    /// 对应 LMP 的 IMessageHandler。
    /// </summary>
    public interface IMessageHandler
    {
        ConcurrentQueue<IMessageData> IncomingMessages { get; }
        void HandleMessage(IMessageData msg);
    }

    /// <summary>
    /// 处理端基类：把收到的 IMessageData 入队，由主线程在 OnUpdate 中逐个处理（避免在非 Unity 线程触碰游戏对象）。
    /// </summary>
    public abstract class MessageHandlerBase<T> : SubSystem<T>, IMessageHandler
        where T : SystemBase, new()
    {
        public ConcurrentQueue<IMessageData> IncomingMessages { get; set; } = new ConcurrentQueue<IMessageData>();

        public abstract void HandleMessage(IMessageData msg);
    }

    /// <summary>
    /// 发送端基类：把 IMessageData 投递到网络发送队列。
    /// </summary>
    public abstract class MessageSenderBase<T> : SubSystem<T>
        where T : SystemBase, new()
    {
        protected void Send(IMessageData data)
        {
            NetworkSender.QueueOutgoing(data);
        }
    }

    /// <summary>
    /// 消息驱动的子系统：把 System + Sender + Handler 三者绑定在一起。
    /// 对应 LMP 的 MessageSystem&lt;T, TS, TH&gt;。主线程 OnUpdate 里把入站队列排空并交给 Handler。
    /// </summary>
    public abstract class MessageSystem<T, TS, TH> : SystemBase
        where T : MessageSystem<T, TS, TH>, new()
        where TS : SubSystem<T>, new()
        where TH : SubSystem<T>, IMessageHandler, new()
    {
        public TS MessageSender { get; } = new TS();
        public TH MessageHandler { get; } = new TH();

        protected override void OnEnabled()
        {
            MessageSender.System = (T)this;
            MessageHandler.System = (T)this;
        }

        protected override void OnUpdate()
        {
            var queue = MessageHandler.IncomingMessages;
            while (queue.TryDequeue(out var msg))
            {
                try
                {
                    MessageHandler.HandleMessage(msg);
                }
                catch (System.Exception e)
                {
                    Ksp2Logger.Error($"处理消息 {msg?.GetType().Name} 异常: {e}");
                }
            }
        }

        protected override void OnDisabled()
        {
            // IncomingMessages 通过 IMessageHandler 接口只暴露只读属性，无法重新赋值。
            // 改为排空队列以丢弃待处理的入站消息（接口引用下也能调用 TryDequeue）。
            var queue = MessageHandler.IncomingMessages;
            while (queue.TryDequeue(out _)) { }
        }
    }
}
