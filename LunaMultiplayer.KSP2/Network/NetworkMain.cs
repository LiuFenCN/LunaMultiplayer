using Lidgren.Network;
using System.Threading;
using System.Threading.Tasks;

namespace LunaMultiplayer.KSP2.Network
{
    /// <summary>
    /// 网络主入口。沿用 LMP 的 Lidgren.Net 客户端模型：独立的接收线程 + 发送线程。
    /// AppIdentifier 与 LMP 不同（"LMP2"），避免误连 KSP1 服务端。
    /// </summary>
    public static class NetworkMain
    {
        public const string AppIdentifier = "LMP2";

        public static NetPeerConfiguration Config { get; private set; }
        public static NetClient Client { get; private set; }

        private static Task _receiveTask;
        private static Task _sendTask;

        public static void Start()
        {
            Config = new NetPeerConfiguration(AppIdentifier)
            {
                UseMessageRecycling = true,
                MaximumTransmissionUnit = 1200,
                PingInterval = 2f,
                ConnectionTimeout = 12
            };

            Client = new NetClient(Config);
            Client.Start();

            _receiveTask = Task.Factory.StartNew(NetworkReceiver.ReceiveMain, TaskCreationOptions.LongRunning);
            _sendTask = Task.Factory.StartNew(NetworkSender.SendMain, TaskCreationOptions.LongRunning);

            Ksp2Logger.Info("网络线程已启动");
        }

        public static void Stop()
        {
            try { Client?.Shutdown("LMP2 disconnect"); }
            catch { }
            Thread.Sleep(200);
        }
    }
}
