using UnityEngine;

namespace PewPewPew.Ships
{
    [CreateAssetMenu(menuName = "PewPewPew/Thruster")]
    public class ThrusterDef : ShipComponentDef
    {
        [SerializeField] private float m_Force = 500f;
        [SerializeField] private int m_EmissionPointsUsed = 1;
        [SerializeField] private float m_RampUpTime = 0.3f;
        [SerializeField] private float m_RampDownTime = 0.5f;
        [SerializeField] private GameObject m_Effect;

        public float Force => m_Force;
        public int EmissionPointsUsed => m_EmissionPointsUsed;
        public float RampUpTime => m_RampUpTime;
        public float RampDownTime => m_RampDownTime;
        public GameObject Effect => m_Effect;

        private void OnValidate() => m_EmissionPointsUsed = Mathf.Clamp(m_EmissionPointsUsed, 1, 4);
    }
}
