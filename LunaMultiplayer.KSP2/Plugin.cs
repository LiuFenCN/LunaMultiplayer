using BepInEx;
using SpaceWarp2.API.Mods;
using UnityEngine;
using System;
using LunaMultiplayer.KSP2.Base;
using LunaMultiplayer.KSP2.Core;
using LunaMultiplayer.KSP2.Network;
using LunaMultiplayer.KSP2.Systems.TimeSyncSys;
using LunaMultiplayer.KSP2.Systems.VesselPositionSys;
using LunaMultiplayer.KSP2.Systems.VesselResourceSys;
using LunaMultiplayer.KSP2.Systems.VesselActionGroupSys;
using LunaMultiplayer.KSP2.Systems.VesselStructureSys;

namespace LunaMultiplayer.KSP2
{
    /// <summary>
    /// KSP2 联机 mod 入口。
    /// 继承自 SpaceWarp2 的 GeneralMod（实现 ISpaceWarpMod），
    /// 这样 Redux 环境下的 SpaceWarp2.ModuleManager 才会发现并注册本 mod。
    /// （KSP2 Redux 没有启用 BepInEx 链式加载器 doorstop 注入，普通 BaseUnityPlugin 不会被加载。）
    /// 生命周期走 SpaceWarp2 的 OnInitialized，而非 MonoBehaviour.Awake。
    /// </summary>
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency("com.github.x606.spacewarp", BepInDependency.DependencyFlags.SoftDependency)]
    public class Plugin : GeneralMod
    {
        public const string PluginGuid = "com.liufencn.lunamultiplayer.ksp2";
        public const string PluginName = "LunaMultiplayer KSP2";
        public const string PluginVersion = "0.1.0";

        public static Plugin Instance { get; private set; }

        /// <summary>
        /// 静态构造：注册程序集解析回退，确保 Lidgren.Network 无论被 SpaceWarp
        /// 以哪种加载上下文（LoadFile / LoadFrom）预载，都能被本 mod 正确解析到。
        /// 仅在默认解析失败时触发，不会造成重复加载。
        /// </summary>
        static Plugin()
        {
            AppDomain.CurrentDomain.AssemblyResolve += (sender, args) =>
            {
                try
                {
                    var name = new System.Reflection.AssemblyName(args.Name).Name;
                    if (name == "Lidgren.Network")
                    {
                        var modDir = System.IO.Path.GetDirectoryName(typeof(Plugin).Assembly.Location) ?? ".";
                        var candidate = System.IO.Path.Combine(modDir, "lib", "Lidgren.Network.dll");
                        if (System.IO.File.Exists(candidate))
                            return System.Reflection.Assembly.LoadFrom(candidate);
                    }
                }
                catch
                {
                    // 解析失败就返回 null，交给运行时默认处理
                }
                return null;
            };
        }

        // 系统实例
        private VesselPositionSystem _vesselSystem;
        private TimeSyncSystem _timeSystem;
        private VesselResourceSystem _resourceSystem;
        private VesselActionGroupSystem _actionGroupSystem;
        private VesselStructureSystem _structureSystem;

        public override void OnInitialized()
        {
            // 用裸 UnityEngine.Debug 打入口标记，防止 Ksp2Logger 或 SpaceWarp 日志层异常导致看不到
            global::UnityEngine.Debug.Log("[LMP2] >>> OnInitialized ENTRY");

            try
            {
                base.OnInitialized();
                global::UnityEngine.Debug.Log("[LMP2] base.OnInitialized OK");
            }
            catch (Exception ex)
            {
                global::UnityEngine.Debug.LogError("[LMP2] base.OnInitialized FAILED: " + ex);
            }

            Instance = this;

            try
            {
                Ksp2Logger.Info($"{PluginName} v{PluginVersion} 加载（SpaceWarp2 mod）");

                // 进程退出时清理
                Application.quitting += OnQuit;

                // 主动注册消息类型，确保收到网络包时反序列化能找到类型
                MessageRegistry.Register(typeof(VesselPositionMsgData));
                MessageRegistry.Register(typeof(TimeSyncMsgData));
                MessageRegistry.Register(typeof(VesselResourceMsgData));
                MessageRegistry.Register(typeof(VesselActionGroupMsgData));
                MessageRegistry.Register(typeof(VesselStructureMsgData));
                Ksp2Logger.Info("[LMP2] MessageRegistry 注册完成");

                // 启动网络线程（Lidgren 客户端，待命，不自动连接）
                NetworkMain.Start();
                Ksp2Logger.Info("[LMP2] NetworkMain.Start() 完成");

                // 主循环驱动器
                var runner = new GameObject("LMP2_Runner");
                UnityEngine.Object.DontDestroyOnLoad(runner);
                runner.AddComponent<Ksp2Runner>();
                runner.AddComponent<Lmp2Ui>();   // 游戏内 UI（按 F7 开关）
                Ksp2Logger.Info("[LMP2] Ksp2Runner / Lmp2Ui 已挂载");

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
                global::UnityEngine.Debug.Log("[LMP2] <<< OnInitialized SUCCESS");
            }
            catch (Exception ex)
            {
                global::UnityEngine.Debug.LogError("[LMP2] OnInitialized body FAILED: " + ex);
            }
        }

        private void OnQuit()
        {
            Ksp2Logger.Info("LunaMultiplayer KSP2 卸载");
            _vesselSystem?.SetEnabled(false);
            _timeSystem?.SetEnabled(false);
            _resourceSystem?.SetEnabled(false);
            _actionGroupSystem?.SetEnabled(false);
            _structureSystem?.SetEnabled(false);
            NetworkMain.Stop();
        }
    }
}
