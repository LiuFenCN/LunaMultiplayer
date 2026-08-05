using System;
using LunaMultiplayer.KSP2.Base;
using LunaMultiplayer.KSP2.VesselUtilities;

namespace LunaMultiplayer.KSP2.Systems.VesselStructureSys
{
    /// <summary>
    /// 处理入站结构变化通知：记录并提示对端飞船拓扑变化。
    /// 资源同步由 owner 端 2Hz 自动覆盖新增/移除零件，故此处主要做感知与日志，
    /// 为后续"远端几何合并"扩展保留钩子。
    /// </summary>
    public class VesselStructureMessageHandler : MessageHandlerBase<VesselStructureSystem>
    {
        public override void HandleMessage(IMessageData msg)
        {
            if (!(msg is VesselStructureMsgData data)) return;
            if (!Guid.TryParse(data.VesselId, out var id)) return;
            if (!VesselCommon.DoVesselChecks(id)) return;

            Ksp2Logger.Info($"远端飞船 {data.VesselId} 结构变化，当前零件数 {data.PartCount}（对接/分离/级间分离）。");
        }
    }
}
