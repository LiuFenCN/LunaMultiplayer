using KSP.Game;
using KSP.Sim;
using KSP.Sim.impl;

namespace LunaMultiplayer.KSP2.Core
{
    /// <summary>
    /// 统一游戏时间访问器。同步时间戳、插值、subspace 都依赖它。
    /// KSP2 的全局时间在 SpaceSimulation.UniverseModel 上（实现 IUniverseTime.UniverseTime）。
    /// 若运行期拿不到 Simulation，则回退到系统 UTC（仅用于离线/调试）。
    /// </summary>
    public static class Ksp2Time
    {
        public static double UniversalTime
        {
            get
            {
                var sim = Game.Instance?.SpaceSimulation;
                if (sim?.UniverseModel is IUniverseTime ut)
                    return ut.UniverseTime;
                return 0d;
            }
        }

        public static bool IsTimePaused
        {
            get
            {
                var sim = Game.Instance?.SpaceSimulation;
                if (sim?.UniverseModel is IUniverseTime ut)
                    return ut.IsTimePaused;
                return false;
            }
        }
    }
}
