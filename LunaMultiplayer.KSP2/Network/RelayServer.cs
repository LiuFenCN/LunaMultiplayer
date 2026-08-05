using Lidgren.Network;
using System.Threading;
using System.Threading.Tasks;

namespace LunaMultiplayer.KSP2.Network
{
    /// <summary>
    /// 轻量中继服务端（listen server / host 模式）。
    /// 启动一个 Lidgren <see cref="NetServer"/>，把每个客户端发来的 Data 消息原样转发给
    /// 其它客户端，形成星型中继拓扑。host 自身也通过 loopback 作为客户端接入，
    /// 因此完全复用现有 NetClient 收发管线与 MessageRouter，无需为服务端单独写一套消息处理。
    ///
    /// 设计取舍：服务端只做字节级转发，不解析消息语义——这意味着它可以完整透传
    /// LMP/KSP2 的 <see cref="ClientMessage"/> 协议，也不需要在服务端持有任何仿真状态。
    /// 每个客户端各自跑自己的 KSP2 仿真，服务端只负责把状态广播出去（经典的 co-op 中继模型）。
    /// 未来若需要"服务端权威 + 场景/飞船所有权仲裁"，再在此之上叠加 LMP Server 工程逻辑。
    /// </summary>
    public static class RelayServer
    {
        public static NetServer Server { get; private set; }

        public static bool IsRunning => Server != null && Server.Status == NetPeerStatus.Running;

        private static Task _relayTask;

        /// <summary>在指定端口启动中继服务端。已在运行则直接返回。</summary>
        public static void Start(int port)
        {
            if (IsRunning) return;

            var config = new NetPeerConfiguration(NetworkMain.AppIdentifier)
            {
                UseMessageRecycling = true,
                MaximumTransmissionUnit = 1200,
                Port = port,
                // 局域网/本地联机，放宽超时以便调试
                ConnectionTimeout = 30
            };

            Server = new NetServer(config);
            Server.Start();
            _relayTask = Task.Factory.StartNew(RelayMain, TaskCreationOptions.LongRunning);
            Ksp2Logger.Info($"中继服务端已启动，监听端口 {port}");
        }

        /// <summary>停止中继服务端。</summary>
        public static void Stop()
        {
            try { Server?.Shutdown("LMP2 server stop"); }
            catch (System.Exception) { /* 忽略关闭异常 */ }
            Thread.Sleep(200);
            Server = null;
            _relayTask = null;
        }

        private static void RelayMain()
        {
            while (Server != null && Server.Status != NetPeerStatus.NotRunning)
            {
                var msg = Server.ReadMessage();
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
                            RelayData(msg);
                            break;
                        case NetIncomingMessageType.StatusChanged:
                            var status = (NetConnectionStatus)msg.ReadByte();
                            Ksp2Logger.Info($"[Relay] 客户端连接状态变化: {status}");
                            break;
                        case NetIncomingMessageType.ConnectionLatencyUpdated:
                            break;
                    }
                }
                catch (System.Exception e)
                {
                    Ksp2Logger.Error($"[Relay] 转发异常: {e}");
                }
                finally
                {
                    Server.Recycle(msg);
                }
            }
        }

        /// <summary>
        /// 把一条 Data 消息转发给除发送者以外的所有已连接客户端。
        /// 用 ReliableOrdered 重发以保证中继不丢状态；位置类消息的实时性由客户端侧插值兜底。
        /// </summary>
        private static void RelayData(NetIncomingMessage msg)
        {
            int len = msg.LengthBytes;
            if (len <= 0) return;

            byte[] payload = msg.ReadBytes(len);
            var fromConn = msg.SenderConnection;

            foreach (var conn in Server.Connections)
            {
                if (conn == fromConn) continue; // 不把消息回弹给发送者本人
                var om = Server.CreateMessage();
                om.Write(payload, 0, len);
                Server.SendMessage(om, conn, NetDeliveryMethod.ReliableOrdered);
            }
        }
    }
}
