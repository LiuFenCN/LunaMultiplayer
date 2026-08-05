using Lidgren.Network;
using System;
using System.Threading;

namespace LunaMultiplayer.KSP2.Network
{
    /// <summary>
    /// 接收端：从 Lidgren 读取 Data 消息，反序列化为 IMessageData，交给 MessageRouter 分发到对应 Handler 的入站队列。
    /// 对应 LMP 的 NetworkReceiver.ReceiveMain。
    /// </summary>
    public static class NetworkReceiver
    {
        public static void ReceiveMain()
        {
            while (NetworkMain.Client != null && NetworkMain.Client.Status != NetPeerStatus.NotRunning)
            {
                var msg = NetworkMain.Client.ReadMessage();
                if (msg == null)
                {
                    Thread.Sleep(10);
                    continue;
                }

                try
                {
                    switch (msg.MessageType)
                    {
                        case NetIncomingMessageType.Data:
                            var data = ClientMessage.Deserialize(msg);
                            MessageRouter.Route(data);
                            break;
                        case NetIncomingMessageType.StatusChanged:
                            var status = (NetConnectionStatus)msg.ReadByte();
                            Ksp2Logger.Info($"连接状态变化: {status}");
                            break;
                        case NetIncomingMessageType.ConnectionLatencyUpdated:
                            break;
                    }
                }
                catch (Exception e)
                {
                    Ksp2Logger.Error($"接收消息异常: {e}");
                }
                finally
                {
                    NetworkMain.Client.Recycle(msg);
                }
            }
        }
    }
}
