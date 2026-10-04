using Mirror;
using PewPewPew.Core;
using System.Collections.Generic;
using UnityEngine;

namespace PewPewPew.Ships
{
    /// On a remote client, draws the local ship's visuals ahead of its network position along straight-line flight,
    /// blended between the two. The root transform stays on the network position.
    [DefaultExecutionOrder(-50)] // Before ShipCamera follows the visuals.
    [RequireComponent(typeof(Ship))]
    public class ShipVisualPrediction : NetworkBehaviour
    {
        [SerializeField, Range(0f, 1f), Tooltip("0 draws the network position, 1 the full prediction.")] private float m_Blend = 0.5f;
        [SerializeField] private float m_MaxLookAhead = 0.25f;
        [SerializeField] private float m_VelocitySmoothing = 10f;

        private Ship m_Ship;
        private Vector3[] m_BasePositions;
        private Vector2 m_LastPosition;
        private Vector2 m_Velocity;

        public Transform CameraTarget => m_Ship.Visuals.Count > 0 ? m_Ship.Visuals[0] : transform;

        private void Awake()
        {
            m_Ship = GetComponent<Ship>();
            m_LastPosition = transform.position;
        }

        private void LateUpdate()
        {
            IReadOnlyList<Transform> visuals = m_Ship.Visuals;
            if (m_BasePositions == null)
            {
                if (visuals.Count == 0) return;

                m_BasePositions = new Vector3[visuals.Count];
                for (int i = 0; i < visuals.Count; i++) m_BasePositions[i] = visuals[i].localPosition;
            }

            Vector2 position = transform.position;
            if (Time.deltaTime > 0f)
            {
                Vector2 rawVelocity = (position - m_LastPosition) / Time.deltaTime;
                m_Velocity = Vector2.Lerp(m_Velocity, rawVelocity, 1f - Mathf.Exp(-m_VelocitySmoothing * Time.deltaTime));
            }
            m_LastPosition = position;

            // The host is the simulation, so there is nothing to predict.
            bool predicting = isOwned && !isServer;
            Vector2 offset = predicting ? PredictionMath.Offset(m_Velocity, (float)NetworkTime.rtt * 0.5f, m_Blend, m_MaxLookAhead) : Vector2.zero;
            Vector3 localOffset = transform.InverseTransformVector(offset);
            for (int i = 0; i < visuals.Count; i++) visuals[i].localPosition = m_BasePositions[i] + localOffset;
        }
    }
}
