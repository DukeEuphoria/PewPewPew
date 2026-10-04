using System;
using UnityEngine;

namespace PewPewPew.Ships
{
    /// Position relative to the ship centre and an angle relative to the ship's forward direction.
    [Serializable]
    public struct EmissionPoint
    {
        [SerializeField] private Vector2 m_Position;
        [SerializeField] private float m_Angle;

        public Vector2 Position => m_Position;
        public float Angle => m_Angle;
    }
}
