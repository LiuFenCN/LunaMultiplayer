using Lidgren.Network;
using LunaMultiplayer.KSP2.Base;

namespace LunaMultiplayer.KSP2.Systems.VesselActionGroupSys
{
    /// <summary>
    /// 动作组同步线格式。用 32 位掩码表达各动作组的开/关状态。
    /// 位索引 i 对应 VesselActionGroupMessageSender.Groups[i]（KSP2 KSPActionGroup 枚举的声明顺序，
    /// 不含 None）。采用"枚举声明顺序 → 位索引"编码，不依赖 KSPActionGroup 在反射上下文下不可靠的
    /// 数值，跨端只要枚举声明顺序一致即可正确对应。
    /// </summary>
    [MessageTypeId(112)]
    public class VesselActionGroupMsgData : IMessageData
    {
        static VesselActionGroupMsgData()
        {
            MessageRegistry.Register(typeof(VesselActionGroupMsgData));
        }

        public ushort SubType => 0;

        public string VesselId;
        public uint GroupMask;

        public void Serialize(NetOutgoingMessage m)
        {
            m.Write(VesselId ?? "");
            m.Write(GroupMask);
        }

        public void Deserialize(NetIncomingMessage m)
        {
            VesselId = m.ReadString();
            GroupMask = m.ReadUInt32();
        }

        public int GetMessageSize() => 64 + sizeof(uint);
    }
}
