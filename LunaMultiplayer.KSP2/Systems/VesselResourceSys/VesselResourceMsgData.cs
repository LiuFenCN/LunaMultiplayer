using Lidgren.Network;
using LunaMultiplayer.KSP2.Base;

namespace LunaMultiplayer.KSP2.Systems.VesselResourceSys
{
    /// <summary>
    /// 零件级资源同步线格式。
    /// 每条记录描述：某飞船的某零件(partGuid) 的某种资源(resourceName) 的当前存量(amount)。
    /// 资源按"名称"而非 ResourceDefinitionID 传输——名称跨端稳定，
    /// 接收端用 ResourceDefinitionDatabase.GetResourceIDFromName 还原为本地 ID。
    /// 发送端按 2Hz 节流（见 VesselResourceMessageSender），避免大飞船每帧刷爆带宽。
    /// </summary>
    [MessageTypeId(111)]
    public class VesselResourceMsgData : IMessageData
    {
        static VesselResourceMsgData()
        {
            MessageRegistry.Register(typeof(VesselResourceMsgData));
        }

        public ushort SubType => 0;

        public const int MaxEntries = 512; // 单船资源记录硬上限（防爆内存/带宽）

        public string VesselId;
        public int EntryCount;
        public string[] PartGuids = new string[MaxEntries];
        public string[] ResourceNames = new string[MaxEntries];
        public double[] Amounts = new double[MaxEntries];

        public void Serialize(NetOutgoingMessage m)
        {
            m.Write(VesselId ?? "");
            int n = Math.Min(EntryCount, MaxEntries);
            m.Write(n);
            for (int i = 0; i < n; i++)
            {
                m.Write(PartGuids[i] ?? "");
                m.Write(ResourceNames[i] ?? "");
                m.Write(Amounts[i]);
            }
        }

        public void Deserialize(NetIncomingMessage m)
        {
            VesselId = m.ReadString();
            EntryCount = m.ReadInt32();
            int n = Math.Min(EntryCount, MaxEntries);
            for (int i = 0; i < n; i++)
            {
                PartGuids[i] = m.ReadString();
                ResourceNames[i] = m.ReadString();
                Amounts[i] = m.ReadDouble();
            }
        }

        public int GetMessageSize()
        {
            int n = Math.Min(EntryCount, MaxEntries);
            return 64 + sizeof(int) + n * (2 * 64 + sizeof(double));
        }
    }
}
