using PewPewPew.Presentation;
using UnityEngine;

namespace PewPewPew.Ships
{
    [CreateAssetMenu(menuName = "PewPewPew/SubSystems/Radar")]
    public sealed class RadarSubSystemDef : SubSystemDef
    {

        [Header("Radar Settings")]
        [SerializeField] private RadarEmitterDef m_Radar;

        public RadarEmitterDef Radar => m_Radar;
        public override SubSystem Create(Transform mount, Transform space) => new RadarSubSystem(this, mount, space);
    }
}
