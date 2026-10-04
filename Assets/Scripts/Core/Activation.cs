namespace PewPewPew.Core
{
    public enum ActivationMode
    {
        /// Always active; only its passive drain applies.
        Passive,
        /// Press to switch on or off.
        Toggle,
        /// Press to run for a fixed duration, then recharge for a cooldown.
        Triggered,
    }

    /// While active a system draws its active power every step (negative drain generates power).
    /// If the power is not there at that instant, it switches off.
    public class Activation
    {
        private readonly ActivationMode m_Mode;
        private readonly float m_Duration;
        private readonly float m_Cooldown;
        private readonly float m_ActiveDrain;
        private bool m_Wanted;
        private float m_Elapsed;
        private float m_CooldownLeft;

        public Activation(ActivationMode mode, float duration, float cooldown, float activeDrainPerSecond)
        {
            m_Mode = mode;
            m_Duration = duration;
            m_Cooldown = cooldown;
            m_ActiveDrain = activeDrainPerSecond;
            IsActive = mode == ActivationMode.Passive;
        }

        public bool IsActive { get; private set; }

        public float CooldownRemaining => m_CooldownLeft;

        public bool IsCoolingDown => !IsActive && m_CooldownLeft > 0f;

        /// 0..1 for display: drains while a Triggered system runs, refills through its cooldown (1 when ready); Toggle is 1 when on, 0 when off.
        public float Level
        {
            get
            {
                if (IsActive) return m_Mode == ActivationMode.Triggered && m_Duration > 0f ? Clamp01(1f - m_Elapsed / m_Duration) : 1f;
                if (m_Mode != ActivationMode.Triggered) return 0f;
                return m_Cooldown > 0f ? Clamp01(1f - m_CooldownLeft / m_Cooldown) : 1f;
            }
        }

        private static float Clamp01(float value) => value < 0f ? 0f : value > 1f ? 1f : value;

        public void Press()
        {
            switch (m_Mode)
            {
                case ActivationMode.Toggle:
                    m_Wanted = !m_Wanted;
                    break;
                case ActivationMode.Triggered:
                    if (!m_Wanted && m_CooldownLeft <= 0f) m_Wanted = true;
                    break;
            }
        }

        public void Step(float deltaTime, PowerBank power)
        {
            if (m_Mode == ActivationMode.Passive) return;

            if (m_CooldownLeft > 0f) m_CooldownLeft -= deltaTime;
            if (!m_Wanted)
            {
                Stop();
                return;
            }

            if (m_ActiveDrain <= 0f) power.Charge(-m_ActiveDrain * deltaTime);
            else if (!power.TryDraw(m_ActiveDrain * deltaTime))
            {
                Stop();
                return;
            }

            IsActive = true;
            m_Elapsed += deltaTime;
            if (m_Mode == ActivationMode.Triggered && m_Elapsed >= m_Duration) Stop();
        }

        /// Switches off immediately, e.g. when the system is destroyed. Triggered systems then start their cooldown.
        public void Stop()
        {
            if (m_Mode == ActivationMode.Passive) return;

            if (IsActive && m_Mode == ActivationMode.Triggered) m_CooldownLeft = m_Cooldown;
            IsActive = false;
            m_Wanted = false;
            m_Elapsed = 0f;
        }
    }
}
