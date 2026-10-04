using Mirror;
using PewPewPew.Core;
using UnityEngine;

namespace PewPewPew.Ships
{
    /// On a remote client, draws the local ship's visuals ahead of its network position along straight-line flight,
    /// blended between the two. The root transform stays on the network position.
    [DefaultExecutionOrder(-50)] // Before ShipCamera follows the visuals.
    public class ShipVisualPrediction : NetworkBehaviour
    {
        [SerializeField, Tooltip("Direct children of the ship root (mesh, shield) that are drawn at the predicted position.")] private Transform[] m_Visuals;
        [SerializeField, Range(0f, 1f), Tooltip("0 draws the network position, 1 the full prediction.")] private float m_Blend = 0.5f;
        [SerializeField] private float m_MaxLookAhead = 0.25f;
        [SerializeField] private float m_VelocitySmoothing = 10f;

        private Vector3[] m_BasePositions;
        private Vector2 m_LastPosition;
        private Vector2 m_Velocity;

        public Transform CameraTarget => m_Visuals[0];

        private void Awake()
        {
            m_BasePositions = new Vector3[m_Visuals.Length];
            for (int i = 0; i < m_Visuals.Length; i++) m_BasePositions[i] = m_Visuals[i].localPosition;
            m_LastPosition = transform.position;
        }

        private void LateUpdate()
        {
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
            for (int i = 0; i < m_Visuals.Length; i++) m_Visuals[i].localPosition = m_BasePositions[i] + localOffset;
        }
    }
}
