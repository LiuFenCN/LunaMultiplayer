using System;

namespace LunaMultiplayer.KSP2.Base
{
    /// <summary>
    /// 标注消息数据类的协议 id。typeId 区分消息大类（如 101=飞船位置），
    /// subType 用于同一大类下的子类型（如聊天消息的多子类型）。
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class MessageTypeIdAttribute : Attribute
    {
        public ushort Id;
        public ushort SubType;

        public MessageTypeIdAttribute(ushort id, ushort subType = 0)
        {
            Id = id;
            SubType = subType;
        }
    }
}
