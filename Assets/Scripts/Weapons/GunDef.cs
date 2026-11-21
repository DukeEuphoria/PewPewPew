using PewPewPew.Core;
using PewPewPew.Ships;
using UnityEngine;

namespace PewPewPew.Weapons
{
    /// A gun is active whenever it launches a burst: ActivePowerDrain is paid per burst. Angles are degrees from the emission point's direction.
    [CreateAssetMenu(menuName = "PewPewPew/Gun")]
    public class GunDef : ShipComponentDef
    {
        private const float MinFireRate = 0.01f;
        [SerializeField] private float m_FireRate = 5f;
        [SerializeField] private float m_LaunchSpeed = 40f;
        [SerializeField] private float m_MinLaunchAngle = -5f;
        [SerializeField] private float m_MaxLaunchAngle = 5f;
        [SerializeField] private int m_BulletsPerBurst = 1;
        [SerializeField, Tooltip("Radians per second the Sinewave pattern sweeps.")] private float m_SweepSpeed = 3f;
        [SerializeField] private FirePattern m_Pattern = FirePattern.Spread;
        [SerializeField] private Bullet m_BulletPrefab;
        [SerializeField] private GameObject m_LaunchEffect;
        [SerializeField] private AudioClip m_LaunchSound;

        public float FireRate => m_FireRate;
        public float LaunchSpeed => m_LaunchSpeed;
        public float MinLaunchAngle => m_MinLaunchAngle;
        public float MaxLaunchAngle => m_MaxLaunchAngle;
        public int BulletsPerBurst => m_BulletsPerBurst;
        public float SweepSpeed => m_SweepSpeed;
        public FirePattern Pattern => m_Pattern;
        public Bullet BulletPrefab => m_BulletPrefab;
        public GameObject LaunchEffect => m_LaunchEffect;
        public AudioClip LaunchSound => m_LaunchSound;
        public override ComponentType Type => ComponentType.Gun;

        private void OnValidate()
        {
            m_FireRate = Mathf.Max(MinFireRate, m_FireRate);
            m_BulletsPerBurst = Mathf.Max(1, m_BulletsPerBurst);
            m_MaxLaunchAngle = Mathf.Max(m_MinLaunchAngle, m_MaxLaunchAngle);
        }
    }
}
