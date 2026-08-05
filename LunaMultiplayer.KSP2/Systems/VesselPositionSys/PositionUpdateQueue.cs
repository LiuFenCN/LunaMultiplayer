using System.Collections.Concurrent;

namespace LunaMultiplayer.KSP2.Systems.VesselPositionSys
{
    /// <summary>
    /// 单艘飞船的入站位置更新队列（用于插值）。
    /// 对应 LMP 的 PositionUpdateQueue。保留最近 N 条，避免内存无限增长。
    /// </summary>
    public class PositionUpdateQueue
    {
        private readonly ConcurrentQueue<VesselPositionMsgData> _queue = new ConcurrentQueue<VesselPositionMsgData>();

        public int Count => _queue.Count;

        public void Enqueue(VesselPositionMsgData data)
        {
            if (_queue.Count > 30)
                _queue.TryDequeue(out _);
            _queue.Enqueue(data);
        }

        public bool TryPeek(out VesselPositionMsgData data) => _queue.TryPeek(out data);
        public bool TryDequeue(out VesselPositionMsgData data) => _queue.TryDequeue(out data);
    }
}
