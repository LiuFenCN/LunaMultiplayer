using KSP.Game;
using KSP.Sim;
using KSP.Sim.ResourceSystem;
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

        // ───────────────────────── 资源同步辅助 ─────────────────────────

        /// <summary>全局资源定义库（名称↔ID 互查）。VERIFY: 实际访问器可能挂在
        /// Game.Instance.ResourceDefinitionDatabase 或 SpaceSimulation 上。</summary>
        public static ResourceDefinitionDatabase ResourceDatabase
        {
            get
            {
                var game = Game.Instance;
                if (game == null) return null;
                // VERIFY: 以下属性名需对照 KSP2 源码确认其一
                var db = game.ResourceDefinitionDatabase; // 常见命名
                return db;
            }
        }

        /// <summary>遍历一艘飞船的所有零件（跨端零件顺序/IGGuid 稳定）。VERIFY: 行为获取与零件枚举 API。</summary>
        public static void ForEachPart(VesselComponent vessel, Action<PartComponent> action)
        {
            if (vessel == null || action == null) return;
            var view = Game.Instance?.UniverseView;
            if (view == null) return;

            // VERIFY: 取 VesselBehavior（含 PartOwner）。泛型 GetBehaviorIfLoaded 或带 out 参数的重载二选一
            var behavior = view.GetBehaviorIfLoaded<VesselBehavior>(vessel);
            var owner = behavior?.PartOwner; // PartOwnerComponent
            if (owner?.Parts == null) return;

            // PartOwnerComponent.Parts 是 PartInfoDictionary<IGGuid, PartInfo>
            foreach (var kv in owner.Parts)
            {
                if (owner.TryGetPartValue(kv.Key, out var part) && part != null)
                    action(part);
            }
        }

        /// <summary>把一条资源消息写回远端飞船：逐记录定位零件容器并 SetResourceStoredUnits。</summary>
        public static void ApplyVesselResources(VesselResourceMsgData d)
        {
            if (!Guid.TryParse(d.VesselId, out var g)) return;
            var sim = Game.Instance?.SpaceSimulation;
            if (sim == null) return;
            var igg = new IGGuid(g); // VERIFY: IGGuid 构造
            var remote = sim.GetSimulationObjectComponent<VesselComponent>(igg);
            if (remote == null || remote.IsLocallyOwned) return;

            var db = ResourceDatabase;
            if (db == null) return;

            int n = Math.Min(d.EntryCount, VesselResourceMsgData.MaxEntries);
            for (int i = 0; i < n; i++)
            {
                var partGuid = d.PartGuids[i];
                var resName = d.ResourceNames[i];
                if (string.IsNullOrEmpty(partGuid) || string.IsNullOrEmpty(resName)) continue;

                PartComponent target = null;
                ForEachPart(remote, p =>
                {
                    if (p.Guid == partGuid) target = p; // VERIFY: PartComponent.Guid
                });
                if (target == null) continue;

                var container = target.PartResourceContainer as IResourceContainer; // VERIFY: 容器类型转换
                if (container == null) continue;

                try
                {
                    var resId = db.GetResourceIDFromName(resName);
                    container.SetResourceStoredUnits(resId, d.Amounts[i]);
                }
                catch (Exception e)
                {
                    Ksp2Logger.Debug($"资源写入跳过 {resName}: {e.Message}");
                }
            }
        }
    }
}
