using System;
using PewPewPew.Core;
using UnityEngine;

namespace PewPewPew.Ships
{
    public class SubSystem : IHudElement
    {
        private static readonly Color ActiveColor = new Color(0.3f, 1f, 0.4f);
        private static readonly Color CoolingColor = new Color(1f, 0.6f, 0.15f);
        private static readonly Color ReadyColor = new Color(0.7f, 1f, 0.8f);
        private static readonly Color DisabledColor = new Color(0.4f, 0.4f, 0.4f);

        public SubSystem(SubSystemDef def)
        {
            Def = def;
            System = new ShipSystem(def, Array.Empty<Transform>());
            Activation = new Activation(def.Mode, def.Duration, def.Cooldown, def.ActivePowerDrain);
        }

        public SubSystemDef Def { get; }
        public ShipSystem System { get; }
        public Activation Activation { get; }
        public HudGauge Gauge { get; set; }
        public float HudLevel { get; private set; }
        public HudState HudState { get; private set; }

        public bool IsRunning => !System.Health.IsDestroyed && Activation.IsActive;

        /// Server only: takes the display values from the real state.
        public void RefreshHud()
        {
            if (System.Health.IsDestroyed) SetHud(0f, HudState.Destroyed);
            else if (Activation.IsActive) SetHud(Activation.Level, HudState.Active);
            else if (Activation.IsCoolingDown) SetHud(Activation.Level, HudState.Cooling);
            else SetHud(Activation.Level, Activation.Level > 0f ? HudState.Ready : HudState.Off);
        }

        public void SetHud(float level, HudState state)
        {
            HudLevel = level;
            HudState = state;
        }

        public void Render()
        {
            if (Gauge == null) return;

            Gauge.SetFill(HudLevel);
            Gauge.SetColor(HudState switch
            {
                HudState.Active => ActiveColor,
                HudState.Cooling => CoolingColor,
                HudState.Ready => ReadyColor,
                _ => DisabledColor,
            });
        }
    }
}