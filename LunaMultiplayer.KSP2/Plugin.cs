using BepInEx;
using BepInEx.Configuration;
using UnityEngine;
using LunaMultiplayer.KSP2.Base;
using LunaMultiplayer.KSP2.Core;
using LunaMultiplayer.KSP2.Network;
using LunaMultiplayer.KSP2.Systems.TimeSyncSys;
using LunaMultiplayer.KSP2.Systems.VesselPositionSys;
using LunaMultiplayer.KSP2.Systems.VesselResourceSys;
using LunaMultiplayer.KSP2.Systems.VesselActionGroupSys;
using LunaMultiplayer.KSP2.Systems.VesselStructureSys;
using System;

namespace LunaMultiplayer.KSP2
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency("com.icsharpcode.spacewarp", BepInDependency.DependencyFlags.SoftDependency)]
    public class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.liufencn.lunamultiplayer.ksp2";
        public const string PluginName = "LunaMultiplayer KSP2";
        public const string PluginVersion = "0.1.0";

        public static Plugin Instance { get; private set; }

        // 系统实例
        private VesselPositionSystem _vesselSystem;
        private TimeSyncSystem _timeSystem;
        private VesselResourceSystem _resourceSystem;
        private VesselActionGroupSystem _actionGroupSystem;
        private VesselStructureSystem _structureSystem;

        // 联机模式配置（在 BepInEx 配置文件中可调，无需改代码）
        private ConfigEntry<bool> _hostMode;
        private ConfigEntry<int> _hostPort;
        private ConfigEntry<string> _serverAddress;
        private ConfigEntry<int> _serverPort;

        private void Awake()
        {
            Instance = this;
            Ksp2Logger.Init(Logger);
            Ksp2Logger.Info($"{PluginName} v{PluginVersion} 加载");

            _hostMode = Config.Bind("Network", "HostMode", false,
                "true=以房主(host)身份启动并起中继服务端；false=普通客户端。");
            _hostPort = Config.Bind("Network", "HostPort", 8800,
                "host 模式下中继服务端监听的端口。");
            _serverAddress = Config.Bind("Network", "ServerAddress", "",
                "普通客户端模式下要连接的服务端地址；留空则启动后不自动连接（用 NetworkConnection.Connect 手动连）。");
            _serverPort = Config.Bind("Network", "ServerPort", 8800,
                "普通客户端模式下要连接的服务端端口。");

            // 主动注册消息类型，确保收到网络包时反序列化能找到类型
            MessageRegistry.Register(typeof(VesselPositionMsgData));
            MessageRegistry.Register(typeof(TimeSyncMsgData));
            MessageRegistry.Register(typeof(VesselResourceMsgData));
            MessageRegistry.Register(typeof(VesselActionGroupMsgData));
            MessageRegistry.Register(typeof(VesselStructureMsgData));

            // 启动网络线程（Lidgren 客户端）
            NetworkMain.Start();

            // 按配置选择联机模式
            if (_hostMode.Value)
            {
                NetworkConnection.Host(_hostPort.Value);
            }
            else if (!string.IsNullOrWhiteSpace(_serverAddress.Value))
            {
                NetworkConnection.Connect(_serverAddress.Value, _serverPort.Value);
            }

            // 主循环驱动器
            var runner = new GameObject("LMP2_Runner");
            DontDestroyOnLoad(runner);
            runner.AddComponent<Ksp2Runner>();

            // 实例化并启用同步系统
            _vesselSystem = new VesselPositionSystem();
            _timeSystem = new TimeSyncSystem();
            _resourceSystem = new VesselResourceSystem();
            _actionGroupSystem = new VesselActionGroupSystem();
            _structureSystem = new VesselStructureSystem();
            _vesselSystem.SetEnabled(true);
            _timeSystem.SetEnabled(true);
            _resourceSystem.SetEnabled(true);
            _actionGroupSystem.SetEnabled(true);
            _structureSystem.SetEnabled(true);

            Ksp2Logger.Info("系统已启用。联机 API：NetworkConnection.Host(port) / Connect(host, port)");
        }

        private void OnDestroy()
        {
            _vesselSystem?.SetEnabled(false);
            _timeSystem?.SetEnabled(false);
            _resourceSystem?.SetEnabled(false);
            _actionGroupSystem?.SetEnabled(false);
            _structureSystem?.SetEnabled(false);
            NetworkMain.Stop();
            Ksp2Logger.Info("LunaMultiplayer KSP2 卸载");
        }
    }
}
