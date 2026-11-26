using System;
using PewPewPew.Core;
using PewPewPew.Ships;
using PewPewPew.World;
using UnityEngine;

namespace PewPewPew.Presentation
{
    internal sealed class RadarEmitter
    {
        internal object Key;
        internal RadarEmitterDef Def;
        internal Transform Mount;
        internal double Started;
        internal bool Present;

        private double m_Previous = -0.000001d;
        private double m_NextSample;

        internal Vector2 Forward => Def.Mode == RadarScanMode.Sweep && Def.HalfAngle >= 180f
            ? Vector2.up : (Vector2)Mount.up;

        internal void Sample(double now, Ship localShip, Action<SpaceObject, double, bool> detectedContact)
        {
            if (now < m_NextSample) return;
            double elapsed = now - Started;
            Vector2 origin = Mount.position;
            Vector2 forward = Forward;
            foreach (SpaceObject source in SpaceObject.ActiveObjects)
            {
                if (source == localShip || !source.isClient ||
                    !(source is Ship || source is OrbitalBody || source is Asteroid)) continue;
                if (source is Ship ship && ship.HasExploded) continue;
                Vector2 offset = (Vector2)source.transform.position - origin;
                float distance = offset.magnitude;
                float angle = Vector2.SignedAngle(forward, offset);
                if (distance > Def.Range || Mathf.Abs(angle) > Def.HalfAngle) continue;
                bool detected = Def.Mode switch
                {
                    RadarScanMode.CircularPulse => RadarMath.PulseCrosses(m_Previous, elapsed, distance,
                        Def.PulseSpeed, Def.RefreshRate),
                    RadarScanMode.Sweep => RadarMath.SweepCrosses(m_Previous, elapsed, angle,
                        Def.HalfAngle, Def.SweepSpeed),
                    _ => true,
                };
                if (detected) detectedContact(source, now, Def.DetectTreasure);
            }
            m_Previous = elapsed;
            float rate = Def.Mode == RadarScanMode.JustRefresh ? Def.RefreshRate : Def.SampleRate;
            m_NextSample = now + 1d / rate;
        }
    }
}
