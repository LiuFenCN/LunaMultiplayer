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

        public static void Disconnect(string reason = "disconnect")
        {
            NetworkMain.Client?.Shutdown(reason);
        }
    }
}
