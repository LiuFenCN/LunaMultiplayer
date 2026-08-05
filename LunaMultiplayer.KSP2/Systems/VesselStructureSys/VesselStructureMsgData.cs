using Lidgren.Network;
using LunaMultiplayer.KSP2.Base;

namespace LunaMultiplayer.KSP2.Systems.VesselStructureSys
{
    /// <summary>
    /// 飞船结构变化通知线格式（对接/分离/级间分离都会导致零件数变化）。
    /// 用于告知对端"某飞船的零件拓扑变了"，以便其对端刷新（资源同步由 owner 的 2Hz 自动覆盖新增零件）。
    /// 说明：KSP2 的飞船几何合并在远端完整重建成本极高，本版本不复制对接后的物理合并几何，
    /// 仅同步"结构变化"事件 + 零件计数，供对端感知与后续扩展。
    /// </summary>
    [MessageTypeId(113)]
    public class VesselStructureMsgData : IMessageData
    {
        static VesselStructureMsgData()
        {
            MessageRegistry.Register(typeof(VesselStructureMsgData));
        }

        public ushort SubType => 0;

        public string VesselId;
        public int PartCount;

        public void Serialize(NetOutgoingMessage m)
        {
            m.Write(VesselId ?? "");
            m.Write(PartCount);
        }

        public void Deserialize(NetIncomingMessage m)
        {
            VesselId = m.ReadString();
            PartCount = m.ReadInt32();
        }

        public int GetMessageSize() => 64 + sizeof(int);
    }
}
