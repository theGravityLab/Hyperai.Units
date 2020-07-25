using System;
using System.Collections.Generic;

namespace Hyperai.Units
{
    public interface IUnitService
    {
        void SearchForUnits();

        IEnumerable<ActionEntry> GetEntries();

        void Handle(MessageContext context);

        void WaitOne(Channel channel, ActionDelegate action, TimeSpan timeout);
    }
}