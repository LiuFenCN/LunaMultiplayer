using System;
using LunaMultiplayer.KSP2.Base;

namespace LunaMultiplayer.KSP2.Systems.VesselPositionSys
{
    /// <summary>
    /// 处理入站飞船位置消息：登记新飞船、把更新样本加入插值缓冲。
    /// 对应 LMP 的 VesselPositionMessageHandler。
    /// </summary>
    public class VesselPositionMessageHandler : MessageHandlerBase<VesselPositionSystem>
    {
        public override void HandleMessage(IMessageData msg)
        {
            if (!(msg is VesselPositionMsgData data)) return;
            if (!Guid.TryParse(data.VesselId, out var id)) return;
            if (!VesselCommon.DoVesselChecks(id)) return;

            if (!VesselPositionSystem.CurrentVesselUpdate.TryGetValue(id, out var update))
            {
                update = new VesselPositionUpdate(data);
                VesselPositionSystem.CurrentVesselUpdate[id] = update;
            }
            else
            {
                update.AddSample(data);
            }
        }
    }
}
