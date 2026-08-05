using Lidgren.Network;
using System.Collections.Concurrent;
using System.Threading;
using LunaMultiplayer.KSP2.Base;

namespace LunaMultiplayer.KSP2.Network
{
    /// <summary>
    /// 发送端：所有系统的 Sender 把 IMessageData 入队，发送线程按序写出。
    /// 对应 LMP 的 NetworkSender.OutgoingMessages。
    /// MVP 统一用 ReliableOrdered；后续可按消息类型选择 Unreliable（位置更新）以降低带宽。
    /// </summary>
    public static class NetworkSender
    {
        public static ConcurrentQueue<IMessageData> OutgoingMessages { get; set; } =
            new ConcurrentQueue<IMessageData>();

        public static void QueueOutgoing(IMessageData data) => OutgoingMessages.Enqueue(data);

        public static void SendMain()
        {
            while (NetworkMain.Client != null && NetworkMain.Client.Status != NetPeerStatus.NotRunning)
            {
                while (OutgoingMessages.TryDequeue(out var data))
                {
                    var om = NetworkMain.Client.CreateMessage();
                    new ClientMessage { Data = data }.Serialize(om);
                    NetworkMain.Client.SendMessage(om, NetDeliveryMethod.ReliableOrdered);
                }
                Thread.Sleep(10);
            }
        }
    }
}
