using Lidgren.Network;

namespace LunaMultiplayer.KSP2.Base
{
    /// <summary>
    /// 消息数据契约。每个具体消息（如 VesselPositionMsgData）实现它，
    /// 并标注 [MessageTypeId(...)] 以便 MessageRegistry 反序列化时按 id 重建实例。
    /// 设计沿用 LMP 的 IMessageData：纯值类型、避免引用类型分配（位置同步每帧发送）。
    /// </summary>
    public interface IMessageData
    {
        ushort SubType { get; }
        void Serialize(NetOutgoingMessage msg);
        void Deserialize(NetIncomingMessage msg);
        int GetMessageSize();
    }
}
