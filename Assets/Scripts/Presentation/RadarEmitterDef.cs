using UnityEngine;

namespace PewPewPew.Presentation
{
    public enum RadarScanMode { JustRefresh, CircularPulse, Sweep }

    [CreateAssetMenu(menuName = "PewPewPew/Radar Emitter")]
    public class RadarEmitterDef : ScriptableObject
    {
        [SerializeField, Min(1f)] private float m_Range = 500f;
        [SerializeField] private RadarScanMode m_Mode = RadarScanMode.JustRefresh;
        [SerializeField, Min(0.1f), Tooltip("Full refreshes or launched pulses per second. Sweep repetition is determined by sweep speed.")] private float m_RefreshRate = 2f;
        [SerializeField, Min(0.1f), Tooltip("Detection samples per second for pulse and sweep modes.")] private float m_SampleRate = 20f;
        [SerializeField, Min(1f)] private float m_PulseSpeed = 250f;
        [SerializeField, Min(1f)] private float m_SweepSpeed = 90f;
        [SerializeField, Range(1f, 180f)] private float m_HalfAngle = 180f;

        public float Range => Mathf.Max(1f, m_Range);
        public RadarScanMode Mode => m_Mode;
        public float RefreshRate => Mathf.Max(0.1f, m_RefreshRate);
        public float SampleRate => Mathf.Max(0.1f, m_SampleRate);
        public float PulseSpeed => Mathf.Max(1f, m_PulseSpeed);
        public float SweepSpeed => Mathf.Max(1f, m_SweepSpeed);
        public float HalfAngle => Mathf.Clamp(m_HalfAngle, 1f, 180f);
    }
}