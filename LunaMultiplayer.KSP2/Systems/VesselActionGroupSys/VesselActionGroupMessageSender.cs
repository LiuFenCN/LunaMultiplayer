using System.Linq;
using KSP.Sim;
using KSP.Sim.impl;
using LunaMultiplayer.KSP2.Base;
using LunaMultiplayer.KSP2.VesselUtilities;

namespace LunaMultiplayer.KSP2.Systems.VesselActionGroupSys
{
    /// <summary>
    /// 读取本机飞船当前动作组状态，编码为位掩码并发送。
    /// Groups 为 KSPActionGroup 声明顺序（不含 None），作为"位索引 ↔ 枚举值"的稳定映射，
    /// 发送端与接收端共用，保证位置索引一致。
    /// </summary>
    public class VesselActionGroupMessageSender : MessageSenderBase<VesselActionGroupSystem>
    {
        /// <summary>动作组枚举值按声明顺序（排除 None），位 i 对应 Groups[i]。</summary>
        public static readonly KSPActionGroup[] Groups =
            global::System.Enum.GetValues(typeof(KSPActionGroup))
                .Cast<KSPActionGroup>()
                .Where(g => g != KSPActionGroup.None)
                .ToArray();

        /// <summary>把飞船当前动作组状态编码为位掩码（仅 True 记为开）。</summary>
        public static uint ComputeMask(VesselComponent vessel)
        {
            uint mask = 0;
            if (vessel == null) return mask;
            for (int i = 0; i < Groups.Length; i++)
            {
                if (vessel.GetActionGroupState(Groups[i]) == KSPActionGroupState.True)
                    mask |= (1u << i);
            }
            return mask;
        }

        public void SendVesselActionGroups(VesselComponent vessel, uint mask)
        {
            if (vessel == null) return;
            var msg = new VesselActionGroupMsgData
            {
                VesselId = vessel.Guid,
                GroupMask = mask
            };
            Ksp2Logger.Debug($"发送动作组 {msg.VesselId}: mask=0x{mask:X}");
            Send(msg);
        }
    }
}
