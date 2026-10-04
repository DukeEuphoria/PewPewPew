using System;

namespace PewPewPew.Ships
{
    /// Something on the ship that draws its own state onto the HUD.
    public interface IHudElement
    {
        void Render();
    }

    /// Fills a bar gauge from a 0..1 value.
    public class BarElement : IHudElement
    {
        private readonly HudGauge m_Gauge;
        private readonly Func<float> m_Fraction;

        public BarElement(HudGauge gauge, Func<float> fraction)
        {
            m_Gauge = gauge;
            m_Fraction = fraction;
        }

        public void Render() => m_Gauge.SetFill(m_Fraction());
    }
}
