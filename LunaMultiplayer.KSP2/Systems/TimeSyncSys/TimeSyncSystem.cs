using System;
using LunaMultiplayer.KSP2.Base;

namespace LunaMultiplayer.KSP2.Systems.TimeSyncSys
{
    /// <summary>
    /// 时间同步系统：周期性发请求、维护 ServerOffset。
    /// subspace（每台客户端独立时间扭曲隔离）在拿到可靠的 ServerOffset 后即可实现，
    /// 此处先落地时钟同步这一基础。
    /// </summary>
    public class TimeSyncSystem : MessageSystem<TimeSyncSystem, TimeSyncMessageSender, TimeSyncMessageHandler>
    {
        /// <summary>服务端统一时间 - 客户端统一时间。</summary>
        public static double ServerOffset { get; set; }

        public override string SystemName => nameof(TimeSyncSystem);

        private static DateTime _last = DateTime.UtcNow;
        private static readonly TimeSpan Interval = TimeSpan.FromSeconds(5);

        protected override void OnEnabled()
        {
            base.OnEnabled();
            MessageRouter.Register(typeof(TimeSyncMsgData),
                m => MessageHandler.IncomingMessages.Enqueue(m));
        }

        protected override void OnLateUpdate()
        {
            if ((DateTime.UtcNow - _last) > Interval)
            {
                MessageSender.SendTimeRequest();
                _last = DateTime.UtcNow;
            }
        }
    }
}
