using KSP.Sim.impl;
using LunaMultiplayer.KSP2.Base;

namespace LunaMultiplayer.KSP2.Systems.VesselStructureSys
{
    /// <summary>
    /// 广播本机飞船的零件计数变化。
    /// </summary>
    public class VesselStructureMessageSender : MessageSenderBase<VesselStructureSystem>
    {
        public void SendStructure(VesselComponent vessel, int partCount)
        {
            if (vessel == null) return;
            var msg = new VesselStructureMsgData
            {
                VesselId = vessel.Guid,
                PartCount = partCount
            };
            Ksp2Logger.Debug($"广播结构变化 {msg.VesselId}: 零件数={partCount}");
            Send(msg);
        }
    }
}
