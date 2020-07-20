using Hyperai.Events;
using System;

namespace Hyperai.Units.Attributes
{
    [AttributeUsage(AttributeTargets.Method)]
    public class ReceiveAttribute : Attribute
    {
        public MessageEventType Type { get; set; }
        public ReceiveAttribute(MessageEventType type)
        {
            Type = type;
        }

        public ReceiveAttribute() : this(MessageEventType.Friend) { }
    }
}
