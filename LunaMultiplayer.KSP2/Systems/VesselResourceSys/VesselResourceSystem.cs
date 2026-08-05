using System;
using LunaMultiplayer.KSP2.Base;
using LunaMultiplayer.KSP2.Core;
using LunaMultiplayer.KSP2.VesselUtilities;

namespace LunaMultiplayer.KSP2.Systems.VesselResourceSys
{
    /// <summary>
    /// 零件级资源同步系统：编排"发送本机飞船资源 / 接收并写回远端飞船"。
    /// 架构对应 LMP 的 VesselResourceSys。节流 2Hz 广播本机飞船资源。
    /// </summary>
    public class VesselResourceSystem : MessageSystem<VesselResourceSystem, VesselResourceMessageSender, VesselResourceMessageHandler>
    {
        public override string SystemName => nameof(VesselResourceSystem);

        private static DateTime _lastSent = DateTime.UtcNow;
        private static readonly TimeSpan SendInterval = TimeSpan.FromMilliseconds(500); // 2Hz

        protected override void OnEnabled()
        {
            base.OnEnabled();
            MessageRouter.Register(typeof(VesselResourceMsgData),
                m => MessageHandler.IncomingMessages.Enqueue(m));
        }

        protected override void OnLateUpdate()
        {
            if (VesselCommon.ActiveVessel != null && (DateTime.UtcNow - _lastSent) > SendInterval)
            {
                MessageSender.SendVesselResources(VesselCommon.ActiveVessel);
                _lastSent = DateTime.UtcNow;
            }
        }
    }
}
