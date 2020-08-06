using Microsoft.Extensions.DependencyInjection;
using System;

namespace Hyperai.Units
{
    public class UnitFactory
    {
        public static UnitFactory Instance { get; set; }

        public UnitFactory()
        {
            Instance = this;
        }

        public UnitBase CreateUnit(Type type, MessageContext context, IServiceProvider provider)
        {
            UnitBase unit = (UnitBase)ActivatorUtilities.CreateInstance(provider, type);
            unit.Context = context;
            return unit;
        }
    }
}