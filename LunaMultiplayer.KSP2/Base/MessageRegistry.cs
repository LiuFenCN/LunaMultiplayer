using System;
using System.Collections.Generic;

namespace LunaMultiplayer.KSP2.Base
{
    /// <summary>
    /// 消息类型注册表。每个消息数据类在其静态构造里调用 Register，
    /// 反序列化时按 typeId 找到工厂重建实例。对应 LMP 的 MessageStore/MessageFactory。
    /// </summary>
    public static class MessageRegistry
    {
        private static readonly Dictionary<ushort, Func<IMessageData>> Factories =
            new Dictionary<ushort, Func<IMessageData>>();

        public static void Register(Type t)
        {
            var attr = (MessageTypeIdAttribute)Attribute.GetCustomAttribute(t, typeof(MessageTypeIdAttribute));
            if (attr == null)
            {
                Ksp2Logger.Warn($"消息类 {t.Name} 缺少 [MessageTypeId]，不会被注册");
                return;
            }
            Factories[attr.Id] = () => (IMessageData)Activator.CreateInstance(t);
        }

        public static IMessageData Create(ushort typeId)
        {
            if (Factories.TryGetValue(typeId, out var factory))
                return factory();
            throw new Exception($"[LMP2] 未知消息类型 id {typeId}");
        }

        public static ushort GetTypeId(IMessageData data)
        {
            var attr = (MessageTypeIdAttribute)Attribute.GetCustomAttribute(data.GetType(), typeof(MessageTypeIdAttribute));
            return attr?.Id ?? 0;
        }
    }
}
