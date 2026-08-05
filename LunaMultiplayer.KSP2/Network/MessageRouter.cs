using System;
using System.Collections.Generic;
using LunaMultiplayer.KSP2.Base;

namespace LunaMultiplayer.KSP2.Network
{
    /// <summary>
    /// 消息路由：把反序列化后的 IMessageData 按类型派发到对应系统的 Handler 入站队列。
    /// 每个 MessageSystem 在 OnEnabled 时调用 Register 把自己登记进来。
    /// </summary>
    public static class MessageRouter
    {
        private static readonly Dictionary<Type, Action<IMessageData>> Routes =
            new Dictionary<Type, Action<IMessageData>>();

        public static void Register(Type messageType, Action<IMessageData> handler)
        {
            Routes[messageType] = handler;
        }

        public static void Route(IMessageData data)
        {
            if (Routes.TryGetValue(data.GetType(), out var handler))
                handler(data);
            else
                Ksp2Logger.Warn($"无人处理的消息类型: {data.GetType().Name}");
        }
    }
}
