using LunaMultiplayer.KSP2.Base;
using LunaMultiplayer.KSP2.Core;
using LunaMultiplayer.KSP2.VesselUtilities;

namespace LunaMultiplayer.KSP2.Systems.VesselStructureSys
{
    /// <summary>
    /// 飞船结构变化检测系统：轮询本机活动飞船的零件数（PartOwnerComponent.PartCount）。
    /// 对接/分离/级间分离都会改变零件数，变化时广播 VesselStructureMsgData 通知对端。
    /// 切换活动飞船时仅记录基线、不广播，避免误报。
    /// </summary>
    public class VesselStructureSystem : MessageSystem<VesselStructureSystem, VesselStructureMessageSender, VesselStructureMessageHandler>
    {
        public override string SystemName => nameof(VesselStructureSystem);

        private string _lastVesselId;
        private int _lastPartCount = -1;

        protected override void OnEnabled()
        {
            base.OnEnabled();
            MessageRouter.Register(typeof(VesselStructureMsgData),
                m => MessageHandler.IncomingMessages.Enqueue(m));
        }

        protected override void OnLateUpdate()
        {
            var v = VesselCommon.ActiveVessel;
            if (v == null) { _lastVesselId = null; _lastPartCount = -1; return; }

            int pc = VesselCommon.GetPartCount(v);
            if (v.Guid != _lastVesselId)
            {
                _lastVesselId = v.Guid;
                _lastPartCount = pc;
                return;
            }

            if (pc != _lastPartCount)
            {
                _lastPartCount = pc;
                MessageSender.SendStructure(v, pc);
            }
        }
    }
}
