using HarmonyLib;
using KSP.Sim.impl;
using LunaMultiplayer.KSP2.Base;

namespace LunaMultiplayer.KSP2.Patches
{
    /// <summary>
    /// 可选：直接挂到 KSP2 仿真主循环的 FixedUpdate，作为同步的固定步长驱动。
    /// 与 Ksp2Runner（Unity MonoBehaviour）二选一即可；若两者都启用会重复驱动，
    /// 故 Plugin 默认只启用 Ksp2Runner。此文件演示如何用 Harmony 接触 KSP2 内部循环，
    /// 是后续接入更高级同步（如物理帧精确插值）的入口。
    ///
    /// VERIFY: HarmonyPatch 目标方法签名需对照 KSP2 源码确认（此处按已测绘的
    /// SpaceSimulation.OnFixedUpdate(Single) 编写）。
    /// </summary>
    [HarmonyPatch(typeof(SpaceSimulation))]
    [HarmonyPatch("OnFixedUpdate")]
    public static class SpaceSimulationPatch
    {
        public static void Postfix()
        {
            SystemBase.FixedUpdateAll();
        }
    }
}
