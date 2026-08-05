using Lidgren.Network;

namespace LunaMultiplayer.KSP2.Network
{
    /// <summary>
    /// 连接管理：连接/断开服务端。Hail 消息可携带版本号、玩家名，供服务端做兼容校验。
    /// </summary>
    public static class NetworkConnection
    {
        public static bool IsConnected => NetworkMain.Client?.Status == NetPeerStatus.Running;

        public static void Connect(string host, int port, string playerName = "Player")
        {
            var hail = NetworkMain.Client.CreateMessage();
            hail.Write("LMP2");
            hail.Write(playerName ?? "Player");
            NetworkMain.Client.Connect(host, port, hail);
            Ksp2Logger.Info($"正在连接 {host}:{port} ...");
        }

        /// <summary>
        /// 以 host（房主）身份启动：先在本机起中继服务端，再让自身作为客户端通过 loopback 接入，
        /// 从而复用同一套收发/路由管线。其它玩家用 <see cref="Connect"/> 连到同一端口即可加入。
        /// </summary>
        public static void Host(int port, string playerName = "Host")
        {
            RelayServer.Start(port);
            // 给服务端一点绑定时间，避免 loopback 连接过早失败
            Thread.Sleep(150);
            Connect("127.0.0.1", port, playerName);
            Ksp2Logger.Info($"已进入 host 模式（端口 {port}），等待其它玩家加入…");
        }

        public static void Disconnect(string reason = "disconnect")
        {
            NetworkMain.Client?.Shutdown(reason);
        }
    }
}
