using System;
using LunaMultiplayer.KSP2.Base;
using LunaMultiplayer.KSP2.VesselUtilities;

namespace LunaMultiplayer.KSP2.Systems.VesselResourceSys
{
    /// <summary>
    /// 处理入站资源消息：把每条记录写回对应远端飞船的零件容器。
    /// </summary>
    public class VesselResourceMessageHandler : MessageHandlerBase<VesselResourceSystem>
    {
        public override void HandleMessage(IMessageData msg)
        {
            if (!(msg is VesselResourceMsgData data)) return;
            if (!Guid.TryParse(data.VesselId, out var id)) return;
            if (!VesselCommon.DoVesselChecks(id)) return;

            VesselCommon.ApplyVesselResources(data);
        }
    }
}
