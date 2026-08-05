using KSP.Sim;
using KSP.Sim.ResourceSystem;
using KSP.Sim.impl;
using LunaMultiplayer.KSP2.Base;
using LunaMultiplayer.KSP2.Core;
using LunaMultiplayer.KSP2.VesselUtilities;
using System;
using System.Collections.Generic;

namespace LunaMultiplayer.KSP2.Systems.VesselResourceSys
{
    /// <summary>
    /// 枚举本机飞船每个零件的容器资源，填充 VesselResourceMsgData 并发送。
    /// 对应 LMP 的 VesselResourceSys / PartResourceSys。所有资源写入/读取均基于已测绘的
    /// IResourceContainer（GetResourceStoredUnits / SetResourceStoredUnits）与
    /// ResourceDefinitionDatabase（GetResourceNameFromID）。
    /// </summary>
    public class VesselResourceMessageSender : MessageSenderBase<VesselResourceSystem>
    {
        public void SendVesselResources(VesselComponent vessel)
        {
            if (vessel == null) return;

            var db = VesselCommon.ResourceDatabase; // VERIFY: 全局资源库访问器
            if (db == null) return;

            var msg = new VesselResourceMsgData { VesselId = vessel.Guid };
            int idx = 0;

            VesselCommon.ForEachPart(vessel, part =>
            {
                if (idx >= VesselResourceMsgData.MaxEntries) return;
                var container = part.PartResourceContainer; // ResourceContainer : IResourceContainer
                if (container == null) return;

                var data = container.GetAllResourcesContainedData(); // IEnumerable<ContainedResourceData>
                if (data == null) return;
                foreach (var rd in data)
                {
                    if (idx >= VesselResourceMsgData.MaxEntries) break;
                    var name = db.GetResourceNameFromID(rd.ResourceID); // VERIFY: 字段名 ResourceID
                    if (string.IsNullOrEmpty(name)) continue;
                    msg.PartGuids[idx] = part.Guid;                    // VERIFY: PartComponent.Guid
                    msg.ResourceNames[idx] = name;
                    msg.Amounts[idx] = rd.StoredUnits;                // VERIFY: 字段名 StoredUnits
                    idx++;
                }
            });

            msg.EntryCount = idx;
            if (idx > 0)
            {
                Ksp2Logger.Debug($"发送资源 {msg.VesselId}: {idx} 条");
                Send(msg);
            }
        }
    }
}
