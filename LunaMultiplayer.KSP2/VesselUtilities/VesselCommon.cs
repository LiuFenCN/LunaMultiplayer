using KSP.Game;
using KSP.Sim;
using KSP.Sim.ResourceSystem;
using KSP.Sim.impl;
using KSP.Sim.State;
using LunaMultiplayer.KSP2.Systems.VesselPositionSys;
using LunaMultiplayer.KSP2.Systems.VesselResourceSys;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace LunaMultiplayer.KSP2.VesselUtilities
{
    /// <summary>
    /// KSP2 飞船工具集，替换 LMP 的 VesselCommon。
    /// 全部基于已测绘的 KSP.Sim API（GameManager.Instance.Game.SpaceSimulation / VesselComponent）。
    /// </summary>
    public static class VesselCommon
    {
        /// <summary>本机拥有控制权的活动飞船（即需要向外广播的飞船）。</summary>
        public static VesselComponent ActiveVessel
        {
            get
            {
                var sim = GameManager.Instance?.Game?.SpaceSimulation;
                if (sim == null) return null;
                foreach (var guidStr in sim.GetVesselGuids())          // 元素为 string，需转 IGGuid
                {
                    if (!IGGuid.TryParse(guidStr, out var guid)) continue;
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
                var sim = GameManager.Instance?.Game?.SpaceSimulation;
                if (sim == null) yield break;
                foreach (var guidStr in sim.GetVesselGuids())
                {
                    if (!IGGuid.TryParse(guidStr, out var guid)) continue;
                    var v = sim.GetSimulationObjectComponent<VesselComponent>(guid);
                    if (v != null) yield return v;
                }
            }
        }

        /// <summary>是否应处理该飞船的入站更新：存在且不是本机控制的。</summary>
        public static bool DoVesselChecks(Guid id)
        {
            var sim = GameManager.Instance?.Game?.SpaceSimulation;
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
            var sim = GameManager.Instance?.Game?.SpaceSimulation;
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
                // 朝向：ITransformModel.Rotation 是 KSP.Sim.Rotation（coordinateSystem + localRotation:QuaternionD）
                var tm = remote.transform;
                var q = new QuaternionD(d.SrfRelRotation[0], d.SrfRelRotation[1], d.SrfRelRotation[2], d.SrfRelRotation[3]);
                tm.Rotation = new Rotation(tm.coordinateSystem, q);
            }
            catch (Exception e)
            {
                Ksp2Logger.Debug($"朝向写入跳过（VERIFY ITransformModel）: {e.Message}");
            }
        }

        private static KeplerOrbitState BuildKeplerOrbitState(VesselPositionMsgData d, CelestialBodyComponent body)
        {
            // KeplerOrbitState 是值类型，公开字段直接赋值；referenceBodyGuid 为 string（用 body.Guid）
            var kos = new KeplerOrbitState
            {
                inclination = d.Orbit[0],
                eccentricity = d.Orbit[1],
                semiMajorAxis = d.Orbit[2],
                longitudeOfAscendingNode = d.Orbit[3],
                argumentOfPeriapsis = d.Orbit[4],
                meanAnomalyAtEpoch = d.Orbit[5],
                epoch = d.Orbit[6],
                referenceBodyGuid = body?.Guid ?? string.Empty
            };
            return kos;
        }

        // ───────────────────────── 资源同步辅助 ─────────────────────────

        /// <summary>全局资源定义库（名称↔ID 互查）。访问器为
        /// GameManager.Instance.Game.ResourceDefinitionDatabase。</summary>
        public static ResourceDefinitionDatabase ResourceDatabase
        {
            get
            {
                var game = GameManager.Instance?.Game;
                if (game == null) return null;
                // VERIFY: 以下属性名需对照 KSP2 源码确认其一
                var db = game.ResourceDefinitionDatabase; // 常见命名
                return db;
            }
        }

        // ───────────────────────── 轨道传播辅助 ─────────────────────────

        private static readonly Dictionary<string, double> _bodyMuCache = new Dictionary<string, double>();

        /// <summary>
        /// 参考天体的引力参数 mu (=GM, 单位 m^3/s^2)。用于把平近点角按 2 体规律从 epoch 传播到任意时刻。
        /// 按 BodyGuid 缓存，避免每帧重复查表。取不到时返回 0（调用方退化为线性插值）。
        /// </summary>
        public static double GetBodyGravParameter(string bodyGuid)
        {
            if (string.IsNullOrEmpty(bodyGuid)) return 0d;
            lock (_bodyMuCache)
            {
                if (_bodyMuCache.TryGetValue(bodyGuid, out var cached)) return cached;
            }
            double mu = 0d;
            try
            {
                var sim = GameManager.Instance?.Game?.SpaceSimulation;
                if (sim != null && IGGuid.TryParse(bodyGuid, out var igg))
                {
                    var body = sim.GetSimulationObjectComponent<CelestialBodyComponent>(igg);
                    if (body != null) mu = body.gravParameter;
                }
            }
            catch (Exception e)
            {
                Ksp2Logger.Debug($"取天体引力参数失败 {bodyGuid}: {e.Message}");
            }
            lock (_bodyMuCache) { _bodyMuCache[bodyGuid] = mu; }
            return mu;
        }

        /// <summary>取飞船当前零件数（PartOwnerComponent.PartCount），用于结构变化检测。</summary>
        public static int GetPartCount(VesselComponent vessel)
        {
            if (vessel == null) return 0;
            var sim = GameManager.Instance?.Game?.SpaceSimulation;
            if (sim == null) return 0;
            var owner = sim.GetSimulationObjectComponent<PartOwnerComponent>(vessel.GlobalId);
            return owner?.PartCount ?? 0;
        }

        /// <summary>遍历一艘飞船的所有零件（跨端零件 IGGuid 稳定）。用 SpaceSimulation 取同一 SimulationObject 的 PartOwnerComponent。</summary>
        public static void ForEachPart(VesselComponent vessel, Action<PartComponent> action)
        {
            if (vessel == null || action == null) return;
            var sim = GameManager.Instance?.Game?.SpaceSimulation;
            if (sim == null) return;

            // PartOwnerComponent 与 VesselComponent 同属一个 SimulationObject，按 GlobalId 取即可
            var owner = sim.GetSimulationObjectComponent<PartOwnerComponent>(vessel.GlobalId);
            if (owner?.Parts == null) return;

            // PartOwnerComponent.Parts 是 IEnumerable<PartComponent>
            foreach (var part in owner.Parts)
            {
                if (part != null) action(part);
            }
        }

        /// <summary>把一条资源消息写回远端飞船：逐记录定位零件容器并 SetResourceStoredUnits。</summary>
        public static void ApplyVesselResources(VesselResourceMsgData d)
        {
            if (!Guid.TryParse(d.VesselId, out var g)) return;
            var sim = GameManager.Instance?.Game?.SpaceSimulation;
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

                var container = target.PartResourceContainer; // ResourceContainer : IResourceContainer
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
