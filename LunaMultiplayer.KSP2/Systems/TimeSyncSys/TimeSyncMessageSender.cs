using LunaMultiplayer.KSP2.Base;
using LunaMultiplayer.KSP2.Core;

namespace LunaMultiplayer.KSP2.Systems.TimeSyncSys
{
    /// <summary>发送时间同步请求（客户端 → 服务端）。</summary>
    public class TimeSyncMessageSender : MessageSenderBase<TimeSyncSystem>
    {
        public void SendTimeRequest()
        {
            var msg = new TimeSyncMsgData
            {
                ClientSendTime = Ksp2Time.UniversalTime
            };
            Send(msg);
        }

        public void SendTimeReply(TimeSyncMsgData request)
        {
            var msg = new TimeSyncMsgData
            {
                ClientSendTime = request.ClientSendTime,
                ServerTime = Ksp2Time.UniversalTime
            };
            Send(msg);
        }
    }
}
