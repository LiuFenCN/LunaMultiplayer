using BepInEx;
using UnityEngine;
using LunaMultiplayer.KSP2.Base;
using LunaMultiplayer.KSP2.Core;
using LunaMultiplayer.KSP2.Network;
using LunaMultiplayer.KSP2.Systems.TimeSyncSys;
using LunaMultiplayer.KSP2.Systems.VesselPositionSys;
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

        private void Awake()
        {
            Instance = this;
            Ksp2Logger.Init(Logger);
            Ksp2Logger.Info($"{PluginName} v{PluginVersion} 加载");

            // 主动注册消息类型，确保收到网络包时反序列化能找到类型
            MessageRegistry.Register(typeof(VesselPositionMsgData));
            MessageRegistry.Register(typeof(TimeSyncMsgData));

            // 启动网络线程（Lidgren 客户端）
            NetworkMain.Start();

            // 主循环驱动器
            var runner = new GameObject("LMP2_Runner");
            DontDestroyOnLoad(runner);
            runner.AddComponent<Ksp2Runner>();

            // 实例化并启用同步系统
            _vesselSystem = new VesselPositionSystem();
            _timeSystem = new TimeSyncSystem();
            _vesselSystem.SetEnabled(true);
            _timeSystem.SetEnabled(true);

            Ksp2Logger.Info("系统已启用。连接示例：NetworkConnection.Connect(host, port)");
            // 例：NetworkConnection.Connect("127.0.0.1", 8800);
        }

        private void OnDestroy()
        {
            _vesselSystem?.SetEnabled(false);
            _timeSystem?.SetEnabled(false);
            NetworkMain.Stop();
            Ksp2Logger.Info("LunaMultiplayer KSP2 卸载");
        }
    }
}
