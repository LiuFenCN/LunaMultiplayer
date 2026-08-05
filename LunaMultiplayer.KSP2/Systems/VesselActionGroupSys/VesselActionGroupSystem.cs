using LunaMultiplayer.KSP2.Base;
using LunaMultiplayer.KSP2.Core;
using LunaMultiplayer.KSP2.VesselUtilities;

namespace LunaMultiplayer.KSP2.Systems.VesselActionGroupSys
{
    /// <summary>
    /// 动作组同步系统：每帧轮询本机活动飞船的动作组状态，仅在其变化（或活动飞船切换）时广播。
    /// 接收端逐组 SetActionGroup 写回，使其他玩家看到的动作组状态（起落架/灯光/RCS/SAS/自定义组等）与操作端一致。
    /// </summary>
    public class VesselActionGroupSystem : MessageSystem<VesselActionGroupSystem, VesselActionGroupMessageSender, VesselActionGroupMessageHandler>
    {
        public override string SystemName => nameof(VesselActionGroupSystem);

        private string _lastVesselId;
        private uint _lastMask;

        protected override void OnEnabled()
        {
            base.OnEnabled();
            MessageRouter.Register(typeof(VesselActionGroupMsgData),
                m => MessageHandler.IncomingMessages.Enqueue(m));
        }

        protected override void OnLateUpdate()
        {
            var v = VesselCommon.ActiveVessel;
            if (v == null) { _lastVesselId = null; return; }

            string vid = v.Guid;
            uint mask = VesselActionGroupMessageSender.ComputeMask(v);

            if (vid != _lastVesselId)
            {
                // 切换到另一艘活动飞船：重置基线并立即广播一次，确保对端状态同步
                _lastVesselId = vid;
                _lastMask = mask;
                MessageSender.SendVesselActionGroups(v, mask);
                return;
            }

            if (mask != _lastMask)
            {
                _lastMask = mask;
                MessageSender.SendVesselActionGroups(v, mask);
            }
        }
    }
}
