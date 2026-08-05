using LunaMultiplayer.KSP2.VesselUtilities;

namespace LunaMultiplayer.KSP2.Systems.VesselPositionSys
{
    /// <summary>
    /// 一艘远端飞船的"当前已应用状态"。持有最近一次收到的位置消息，
    /// 并在每帧把插值后的状态写回该飞船的 VesselComponent。
    /// 对应 LMP 的 VesselPositionUpdate。
    /// MVP 直接应用最新消息；插值（在 Latest 与 TargetVesselUpdateQueue.Peek 之间按时间混合）
    /// 留作后续优化点，结构已预留。
    /// </summary>
    public class VesselPositionUpdate
    {
        public VesselPositionMsgData Latest { get; private set; }

        public VesselPositionUpdate(VesselPositionMsgData initial)
        {
            Latest = initial;
            VesselCommon.ApplyVesselUpdate(initial);
        }

        public void UpdateFrom(VesselPositionMsgData next)
        {
            Latest = next;
        }

        /// <summary>
        /// 每帧调用：把当前状态写回远端飞船。
        /// </summary>
        public void ApplyInterpolated()
        {
            VesselCommon.ApplyVesselUpdate(Latest);
        }
    }
}
