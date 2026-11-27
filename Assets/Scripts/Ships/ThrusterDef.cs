using UnityEngine;

namespace PewPewPew.Ships
{
    [CreateAssetMenu(menuName = "PewPewPew/Thruster")]
    public class ThrusterDef : ShipComponentDef
    {
        [Header("Thruster Settings")]
        [SerializeField] private float m_Force = 500f;
        [SerializeField] private float m_RampUpTime = 0.3f;
        [SerializeField] private float m_RampDownTime = 0.5f;
        [SerializeField, Tooltip("Effect prefab with exhaust emitted along its local +Y axis.")] private GameObject m_Effect;

        public float Force => m_Force;
        public float RampUpTime => m_RampUpTime;
        public float RampDownTime => m_RampDownTime;
        public GameObject Effect => m_Effect;
        public override ComponentType Type => ComponentType.Thruster;

    }
}
