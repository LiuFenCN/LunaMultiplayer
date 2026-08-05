using Lidgren.Network;
using LunaMultiplayer.KSP2.Base;

namespace LunaMultiplayer.KSP2.Systems.TimeSyncSys
{
    /// <summary>
    /// 时间同步消息（NTP 风格往返）。客户端发 ClientSendTime，服务端回 ServerTime，
    /// 客户端据此估算与服务器的时钟偏移，供 subspace / 插值使用。
    /// </summary>
    [MessageTypeId(102)]
    public class TimeSyncMsgData : IMessageData
    {
        static TimeSyncMsgData()
        {
            MessageRegistry.Register(typeof(TimeSyncMsgData));
        }

        public ushort SubType => 0;

        public double ClientSendTime;   // 客户端发送时刻（统一游戏时间）
        public double ServerTime;       // 服务端统一时间（回包时填写）
        public double ClientReceiveTime;// 客户端收到时刻（统一游戏时间）

        public void Serialize(NetOutgoingMessage m)
        {
            m.Write(ClientSendTime);
            m.Write(ServerTime);
            m.Write(ClientReceiveTime);
        }

        public void Deserialize(NetIncomingMessage m)
        {
            ClientSendTime = m.ReadDouble();
            ServerTime = m.ReadDouble();
            ClientReceiveTime = m.ReadDouble();
        }

        public int GetMessageSize() => 3 * sizeof(double);
    }
}
