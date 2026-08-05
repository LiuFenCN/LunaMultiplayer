using System;
using KSP.Sim;
using KSP.Sim.impl;
using LunaMultiplayer.KSP2.Base;
using LunaMultiplayer.KSP2.VesselUtilities;

namespace LunaMultiplayer.KSP2.Systems.VesselActionGroupSys
{
    /// <summary>
    /// 处理入站动作组消息：把每位对应的动作组状态写回对应远端飞船。
    /// 仅对"非本机控制"的飞船生效（DoVesselChecks 已保证）。
    /// </summary>
    public class VesselActionGroupMessageHandler : MessageHandlerBase<VesselActionGroupSystem>
    {
        public override void HandleMessage(IMessageData msg)
        {
            if (!(msg is VesselActionGroupMsgData data)) return;
            if (!Guid.TryParse(data.VesselId, out var id)) return;
            if (!VesselCommon.DoVesselChecks(id)) return;

            var sim = GameManager.Instance?.Game?.SpaceSimulation;
            if (sim == null) return;
            var vessel = sim.GetSimulationObjectComponent<VesselComponent>(new IGGuid(id));
            if (vessel == null) return;

            for (int i = 0; i < VesselActionGroupMessageSender.Groups.Length; i++)
            {
                bool on = (data.GroupMask & (1u << i)) != 0;
                try
                {
                    vessel.SetActionGroup(VesselActionGroupMessageSender.Groups[i], on);
                }
                catch (Exception e)
                {
                    Ksp2Logger.Debug($"动作组写回失败 {VesselActionGroupMessageSender.Groups[i]}: {e.Message}");
                }
            }
        }
    }
}
