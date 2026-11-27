using System;
using PewPewPew.World;
using UnityEngine;

namespace PewPewPew.Ships
{
    [RequireComponent(typeof(LineRenderer))]
    public sealed class TractorVisual : MonoBehaviour
    {
        private static readonly int BaseMapTransform = Shader.PropertyToID("_BaseMap_ST");
        private const string BaseMapName = "_BaseMap";

        [SerializeField, Min(0f)] private float TextureRepeat = 1f;

        [SerializeField, Min(0f)] private float m_MinStartWidth = 0.08f;
        [SerializeField, Range(0f, 1f)] private float m_StartWidthFraction = 0.5f;
        [SerializeField] private float m_DepthOffset = -0.5f;
        [SerializeField, Min(0f), Tooltip("Texture repeats per second towards the mount.")] private float m_ScrollSpeed = 1.0f;

        private LineRenderer m_Line;
        private MaterialPropertyBlock m_Properties;
        private Vector4 m_TextureTransform;
        private float m_ScrollOffset;

        private void Awake()
        {
            m_Line = gameObject.GetComponent<LineRenderer>();
            Material material = m_Line.sharedMaterial;
            Debug.Assert(material != null && material.HasProperty(BaseMapName), $"{name} requires a material with a {BaseMapName} texture.");

            m_Line.renderingLayerMask = Treasure.RenderingLayers;
            m_Line.positionCount = 2;
            m_Line.useWorldSpace = true;
            m_Line.enabled = false;

            Vector2 scale = material.GetTextureScale(BaseMapName);
            Vector2 offset = material.GetTextureOffset(BaseMapName);
            m_TextureTransform = new Vector4(scale.x, scale.y, offset.x, offset.y);
            m_Properties = new MaterialPropertyBlock();
        }

        private void Update()
        {
            if (!m_Line.enabled) return;
            m_ScrollOffset = Mathf.Repeat(m_ScrollOffset + m_ScrollSpeed * Time.deltaTime, TextureRepeat);
            m_Line.GetPropertyBlock(m_Properties);
            Vector4 transform = m_TextureTransform;
            transform.z += m_ScrollOffset;
            m_Properties.SetVector(BaseMapTransform, transform);
            m_Line.SetPropertyBlock(m_Properties);
        }

        public void Show(Transform mount, Treasure pickup)
        {
            if (pickup == null)
            {
                m_Line.enabled = false;
                return;
            }

            float diameter = 2f * pickup.Radius;
            m_Line.startWidth = Mathf.Min(m_MinStartWidth, diameter * m_StartWidthFraction);
            m_Line.endWidth = diameter;
            Vector3 start = mount.position;
            Vector3 end = pickup.transform.position;
            start.z += m_DepthOffset;
            end.z += m_DepthOffset;
            m_Line.SetPosition(0, start);
            m_Line.SetPosition(1, end);
            m_Line.enabled = true;
        }
    }
}
