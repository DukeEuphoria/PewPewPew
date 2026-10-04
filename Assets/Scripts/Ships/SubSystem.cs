using System;
using PewPewPew.Core;

namespace PewPewPew.Ships
{
    public class SubSystem
    {
        public SubSystem(SubSystemDef def)
        {
            Def = def;
            System = new ShipSystem(def, Array.Empty<EmissionPoint>());
            Activation = new Activation(def.Mode, def.Duration, def.Cooldown, def.ActivePowerDrain);
        }

        public SubSystemDef Def { get; }
        public ShipSystem System { get; }
        public Activation Activation { get; }

        public bool IsRunning => !System.Health.IsDestroyed && Activation.IsActive;
    }
}
