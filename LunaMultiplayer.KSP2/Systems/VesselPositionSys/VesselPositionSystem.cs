using System;
using System.Collections.Concurrent;
using LunaMultiplayer.KSP2.Base;
using LunaMultiplayer.KSP2.VesselUtilities;

namespace LunaMultiplayer.KSP2.Systems.VesselPositionSys
{
    /// <summary>
    /// 飞船位置同步系统：编排"发送本机飞船 / 接收并应用远端飞船"。
    /// 架构完全对应 LMP 的 VesselPositionSystem（MessageSystem + 收发 + 插值队列）。
    /// </summary>
    public class VesselPositionSystem : MessageSystem<VesselPositionSystem, VesselPositionMessageSender, VesselPositionMessageHandler>
    {
        public static ConcurrentDictionary<Guid, VesselPositionUpdate> CurrentVesselUpdate { get; } =
            new ConcurrentDictionary<Guid, VesselPositionUpdate>();

        public override string SystemName => nameof(VesselPositionSystem);

        // 10Hz 广播本机飞船位置
        private static DateTime _lastSent = DateTime.UtcNow;
        private static readonly TimeSpan SendInterval = TimeSpan.FromMilliseconds(100);

        protected override void OnEnabled()
        {
            base.OnEnabled();
            MessageRouter.Register(typeof(VesselPositionMsgData),
                m => MessageHandler.IncomingMessages.Enqueue(m));
        }

        protected override void OnDisabled()
        {
            CurrentVesselUpdate.Clear();
            base.OnDisabled();
        }

        /// <summary>主线程每帧排空入站队列（由 MessageSystem.OnUpdate 调用 HandleMessage）。</summary>

        /// <summary>LateUpdate：广播本机控制的飞船位置。</summary>
        protected override void OnLateUpdate()
        {
            if (VesselCommon.ActiveVessel != null && (DateTime.UtcNow - _lastSent) > SendInterval)
            {
                MessageSender.SendVesselPositionUpdate(VesselCommon.ActiveVessel);
                _lastSent = DateTime.UtcNow;
            }
        }

        /// <summary>FixedUpdate：把插值后的远端飞船状态写回仿真。</summary>
        protected override void OnFixedUpdate()
        {
            foreach (var kv in CurrentVesselUpdate)
                kv.Value.ApplyInterpolated();
        }

        public void RemoveVessel(Guid id)
        {
            CurrentVesselUpdate.TryRemove(id, out _);
        }
    }
}
