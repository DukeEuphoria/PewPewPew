using PewPewPew.Core;
using UnityEngine;

namespace PewPewPew.Ships
{
    public abstract class SubSystem : IHudElement
    {
        private static readonly Color ActiveColor = new Color(0.3f, 1f, 0.4f);
        private static readonly Color CoolingColor = new Color(1f, 0.6f, 0.15f);
        private static readonly Color ReadyColor = new Color(0.7f, 1f, 0.8f);
        private static readonly Color DisabledColor = new Color(0.4f, 0.4f, 0.4f);

        protected SubSystem(SubSystemDef def, Transform mount = null, Transform space = null)
        {
            Def = def;
            System = new ShipSystem(def, mount, space);
            Activation = new Activation(def.Mode, def.Duration, def.Cooldown, def.ActivePowerDrain);
            if (def.Mode == ActivationMode.Toggle) Activation.Press();
        }

        public SubSystemDef Def { get; }
        public ShipSystem System { get; }
        public Activation Activation { get; }
        public HudGauge Gauge { get; set; }
        public float HudLevel { get; private set; }
        public HudState HudState { get; private set; }

        public bool IsRunning => System.Enabled && !System.Health.IsDestroyed && Activation.IsActive;

        public void TogglePower()
        {
            if (System.Health.IsDestroyed) return;
            if (Def.Mode == ActivationMode.Triggered)
            {
                Activation.Press();
                return;
            }
            if (System.Enabled)
            {
                System.Enabled = false;
                Activation.Stop();
                return;
            }
            System.Enabled = true;
            if (Def.Mode == ActivationMode.Passive) return;
            Activation.Press();
        }

        public void Step(float deltaTime, PowerBank power)
        {
            if (System.Health.IsDestroyed)
            {
                Activation.Stop();
                return;
            }
            if (!System.Enabled)
            {
                Activation.AdvanceCooldown(deltaTime);
                return;
            }
            Activation.Step(deltaTime, power);
            if (Def.Mode == ActivationMode.Toggle && !Activation.IsActive)
                System.Enabled = false;
        }

        /// Server only: takes the display values from the real state.
        public void RefreshHud()
        {
            if (System.Health.IsDestroyed) SetHud(0f, HudState.Destroyed);
            else if (!System.Enabled) SetHud(Activation.Level, Activation.IsCoolingDown ? HudState.Cooling : HudState.Off);
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

    public sealed class StatBoosterSubSystem : SubSystem
    {
        public StatBoosterSubSystem(StatBoosterSubSystemDef def, Transform mount = null, Transform space = null)
            : base(def, mount, space) => Def = def;

        public new StatBoosterSubSystemDef Def { get; }
    }

    public sealed class RadarSubSystem : SubSystem
    {
        public RadarSubSystem(RadarSubSystemDef def, Transform mount = null, Transform space = null)
            : base(def, mount, space) => Def = def;

        public new RadarSubSystemDef Def { get; }
        public bool IsRadarActive => Def.Radar != null && HudState == HudState.Active && System.Point != null;
    }

    public sealed class TractorSubSystem : SubSystem
    {
        public TractorSubSystem(TractorSubSystemDef def, Transform mount = null, Transform space = null)
            : base(def, mount, space) => Def = def;

        public new TractorSubSystemDef Def { get; }
    }
}