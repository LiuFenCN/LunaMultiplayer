using KSP.Game;
using KSP.Sim;
using KSP.Sim.impl;
using System;
using System.Collections.Generic;

namespace LunaMultiplayer.KSP2.VesselUtilities
{
    /// <summary>
    /// KSP2 飞船工具集，替换 LMP 的 VesselCommon。
    /// 全部基于已测绘的 KSP.Sim API（Game.Instance.SpaceSimulation / VesselComponent）。
    /// </summary>
    public static class VesselCommon
    {
        /// <summary>本机拥有控制权的活动飞船（即需要向外广播的飞船）。</summary>
        public static VesselComponent ActiveVessel
        {
            get
            {
                var sim = Game.Instance?.SpaceSimulation;
                if (sim == null) return null;
                foreach (var guid in sim.GetVesselGuids())           // VERIFY: 集合元素类型应为 IGGuid
                {
                    var v = sim.GetSimulationObjectComponent<VesselComponent>(guid);
                    if (v != null && v.IsLocallyOwned)
                        return v;
                }
                return null;
            }
        }

        public static IEnumerable<VesselComponent> AllVessels
        {
            get
            {
                var sim = Game.Instance?.SpaceSimulation;
                if (sim == null) yield break;
                foreach (var guid in sim.GetVesselGuids())
                {
                    var v = sim.GetSimulationObjectComponent<VesselComponent>(guid);
                    if (v != null) yield return v;
                }
            }
        }

        /// <summary>是否应处理该飞船的入站更新：存在且不是本机控制的。</summary>
        public static bool DoVesselChecks(Guid id)
        {
            var sim = Game.Instance?.SpaceSimulation;
            if (sim == null) return false;
            var v = sim.GetSimulationObjectComponent<VesselComponent>((IGGuid)id); // VERIFY: IGGuid 转换
            return v != null && !v.IsLocallyOwned;
        }

        /// <summary>
        /// 把一条位置消息写回远端飞船。这是同步的"落地"动作。
        /// 通过 SpaceSimulation.TeleportSimObjectToOrbit 重建轨道（地表飞船也走轨道，
        /// KSP2 中着陆飞船是贴地圆轨道），朝向单独写入 transform.Rotation。
        /// VERIFY 标注处为需对照 KSP2 源码确认的集成点。
        /// </summary>
        public static void ApplyVesselUpdate(VesselPositionMsgData d)
        {
            var sim = Game.Instance?.SpaceSimulation;
            if (sim == null) return;
            if (!Guid.TryParse(d.VesselId, out var g)) return;

            var igg = new IGGuid(g); // VERIFY: IGGuid 构造（可能需 IGGuid.Parse 或隐式转换）
            var remote = sim.GetSimulationObjectComponent<VesselComponent>(igg);
            if (remote == null || remote.IsLocallyOwned) return;

            try
            {
                var state = BuildKeplerOrbitState(d, remote.mainBody); // VERIFY: KeplerOrbitState 构造
                sim.TeleportSimObjectToOrbit(igg, state, false);
            }
            catch (Exception e)
            {
                Ksp2Logger.Error($"应用远端飞船轨道失败 {d.VesselId}: {e}");
            }

            try
            {
                // 朝向：ITransformModel.Rotation 的 setter 形态需确认（属性或 UpdateRotation 方法）
                remote.transform.Rotation = new Rotation(
                    d.SrfRelRotation[0], d.SrfRelRotation[1], d.SrfRelRotation[2], d.SrfRelRotation[3]);
            }
            catch (Exception e)
            {
                Ksp2Logger.Debug($"朝向写入跳过（VERIFY ITransformModel）: {e.Message}");
            }
        }

        private static KeplerOrbitState BuildKeplerOrbitState(VesselPositionMsgData d, CelestialBodyComponent body)
        {
            // VERIFY: KeplerOrbitState 标准构造参数为
            // (inclination, eccentricity, semiMajorAxis, LAN, argumentOfPeriapsis, meanAnomalyAtEpoch, epoch, referenceBody)
            return new KeplerOrbitState(
                d.Orbit[0], d.Orbit[1], d.Orbit[2], d.Orbit[3], d.Orbit[4], d.Orbit[5], d.Orbit[6], body);
        }
    }
}
