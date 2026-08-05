using LunaMultiplayer.KSP2.Base;
using LunaMultiplayer.KSP2.Core;

namespace LunaMultiplayer.KSP2.Systems.TimeSyncSys
{
    /// <summary>处理时间同步回包，估算客户端与服务端的时钟偏移。</summary>
    public class TimeSyncMessageHandler : MessageHandlerBase<TimeSyncSystem>
    {
        public override void HandleMessage(IMessageData msg)
        {
            if (!(msg is TimeSyncMsgData data)) return;

            // 服务端角色：把请求原样带回并填上 ServerTime
            if (data.ServerTime <= 0)
            {
                System.MessageSender.SendTimeReply(data);
                return;
            }

            // 客户端角色：用往返估算偏移 = ServerTime - 客户端中点
            var now = Ksp2Time.UniversalTime;
            var offset = data.ServerTime - (data.ClientSendTime + now) / 2.0;
            TimeSyncSystem.ServerOffset = offset;
            Ksp2Logger.Debug($"时间偏移估算: {offset:F3}s");
        }
    }
}
