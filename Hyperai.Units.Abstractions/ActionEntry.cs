using Hyperai.Events;
using System;
using System.Reflection;

namespace Hyperai.Units
{
    public struct ActionEntry
    {
        public MessageEventType Type { get; private set; }
        public MethodInfo Action { get; private set; }
        public Type Unit { get; private set; }
        public object State { get; set; }

        public ActionEntry(MessageEventType type, MethodInfo action, Type unit, object state)
        {
            Type = type;
            Action = action;
            Unit = unit;
            State = state;
        }

        public override string ToString()
        {
            return $"{Unit.Name}.{Action.Name}@{Type}";
        }
    }
}
