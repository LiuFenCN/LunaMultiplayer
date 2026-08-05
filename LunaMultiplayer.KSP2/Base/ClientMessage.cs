using Lidgren.Network;

namespace LunaMultiplayer.KSP2.Base
{
    /// <summary>
    /// 客户端消息信封：包裹一个 IMessageData，并在其前后写入协议头 (typeId, subType)。
    /// 序列/反序列化的线格式刻意保持与 LMP 的 MessageBase 一致（先写 ushort typeId，
    /// 再写 ushort subType，pad bits，再写 data），以便未来可与 LMP 服务端互通。
    /// </summary>
    public class ClientMessage
    {
        public IMessageData Data;

        public void Serialize(NetOutgoingMessage msg)
        {
            msg.Write(MessageRegistry.GetTypeId(Data));
            msg.Write(Data.SubType);
            msg.WritePadBits();
            Data.Serialize(msg);
        }

        public static IMessageData Deserialize(NetIncomingMessage msg)
        {
            var typeId = msg.ReadUInt16();
            var subType = msg.ReadUInt16();
            msg.ReadPadBits();
            var data = MessageRegistry.Create(typeId);
            data.Deserialize(msg);
            return data;
        }
    }
}
