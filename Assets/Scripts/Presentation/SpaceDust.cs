using System;
using PewPewPew.Core;
using UnityEngine;

namespace PewPewPew.Presentation
{
    /// Layers of dust motes that fill the camera view, drift with parallax, fade in and out, and streak at speed.
    /// The ParticleSystem on this object only supplies the material; each layer gets its own child system.
    [RequireComponent(typeof(ParticleSystem))]
    [DefaultExecutionOrder(100)] // After ShipCamera has moved and zoomed this frame.
    public class SpaceDust : MonoBehaviour
    {
        [Serializable]
        private class Layer
        {
            [Min(0)] public int Count = 40;
            [Tooltip("Fraction of world motion the layer shows: 1 is fixed in space, below 1 is farther away, above 1 is in front.")]
            public float Depth = 1f;
            [Tooltip("Mote size as a fraction of the view height.")]
            public float Size = 0.006f;
            public Color Tint = Color.white;
            [Tooltip("World Z; positive is behind the ships.")]
            public float Z = 10f;
            public int SortingOrder = -10;
        }

        [SerializeField] private Layer[] m_Layers =
        {
            new Layer { Count = 70, Depth = 0.25f, Size = 0.004f, Tint = new Color(0.7f, 0.8f, 1f, 0.45f), Z = 30f, SortingOrder = -30 },
            new Layer { Count = 50, Depth = 0.55f, Size = 0.0055f, Tint = new Color(0.8f, 0.9f, 1f, 0.65f), Z = 20f, SortingOrder = -20 },
            new Layer { Count = 35, Depth = 1f, Size = 0.007f, Tint = new Color(1f, 1f, 1f, 0.85f), Z = 10f, SortingOrder = -10 },
            new Layer { Count = 8, Depth = 1.4f, Size = 0.011f, Tint = new Color(1f, 1f, 1f, 0.5f), Z = -10f, SortingOrder = 10 },
        };
        [SerializeField, Min(0.1f)] private float m_MinLifetime = 4f;
        [SerializeField, Min(0.1f)] private float m_MaxLifetime = 8f;
        [SerializeField, Min(0f)] private float m_FadeTime = 0.6f;
        [SerializeField, Range(0f, 1f), Tooltip("Fraction of the camera's full-zoom speed at which motes start to streak.")]
        private float m_StreakStart = 0.5f;
        [SerializeField, Min(0f), Tooltip("Streak length in seconds of on-screen motion at full streak.")]
        private float m_StreakSeconds = 0.08f;
        [SerializeField, Min(0f), Tooltip("Longest streak as a fraction of the view height.")]
        private float m_MaxStreak = 0.15f;
        [SerializeField, Range(0f, 0.05f), Tooltip("Smallest mote size as a fraction of the screen, so distant motes stay visible.")]
        private float m_MinScreenSize = 0.003f;

        private struct Mote
        {
            public Vector2 View; // -1..1 across the view on each axis.
            public float Age;
            public float Lifetime;
        }

        // Large enough that the particle system never culls a mote itself.
        private const float ParticleLifetime = 1000f;

        private ParticleSystem[] m_Systems;
        private Mote[][] m_Motes;
        private ParticleSystem.Particle[][] m_Particles;
        private Camera m_Camera;
        private Vector2 m_LastCameraPosition;
        private bool m_HasLastPosition;

        private float Margin => 2f * m_MaxStreak + 0.05f;

        private void Awake()
        {
            ParticleSystem template = GetComponent<ParticleSystem>();
            template.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var templateRenderer = template.GetComponent<ParticleSystemRenderer>();
            Material material = templateRenderer.sharedMaterial;
            templateRenderer.enabled = false;

            m_Systems = new ParticleSystem[m_Layers.Length];
            m_Motes = new Mote[m_Layers.Length][];
            m_Particles = new ParticleSystem.Particle[m_Layers.Length][];
            for (int i = 0; i < m_Layers.Length; i++)
            {
                m_Systems[i] = CreateSystem(m_Layers[i], material, i);
                m_Motes[i] = new Mote[m_Layers[i].Count];
                m_Particles[i] = new ParticleSystem.Particle[m_Layers[i].Count];
                for (int j = 0; j < m_Motes[i].Length; j++) Respawn(ref m_Motes[i][j]);
            }
        }

        private ParticleSystem CreateSystem(Layer layer, Material material, int index)
        {
            var child = new GameObject($"Dust Layer {index}");
            child.transform.SetParent(transform, false);
            var system = child.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = system.main;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = layer.Count;
            main.startLifetime = ParticleLifetime;
            main.startSpeed = 0f;
            ParticleSystem.EmissionModule emission = system.emission;
            emission.enabled = false;
            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = false;

            var renderer = child.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 1f;
            renderer.lengthScale = 1f;
            renderer.cameraVelocityScale = 0f;
            renderer.minParticleSize = m_MinScreenSize;
            renderer.sortingOrder = layer.SortingOrder;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            system.Play();
            return system;
        }

        private void Respawn(ref Mote mote)
        {
            mote.View = new Vector2(UnityEngine.Random.Range(-1f, 1f), UnityEngine.Random.Range(-1f, 1f));
            mote.Age = 0f;
            mote.Lifetime = UnityEngine.Random.Range(m_MinLifetime, Mathf.Max(m_MinLifetime, m_MaxLifetime));
        }

        private void LateUpdate()
        {
            ShipCamera shipCamera = ShipCamera.Instance;
            if (shipCamera == null) return;
            if (m_Camera == null) m_Camera = shipCamera.GetComponent<Camera>();

            Vector2 cameraPosition = m_Camera.transform.position;
            Vector2 cameraDelta = m_HasLastPosition ? cameraPosition - m_LastCameraPosition : Vector2.zero;
            m_LastCameraPosition = cameraPosition;
            m_HasLastPosition = true;

            float halfHeight = m_Camera.orthographicSize;
            var half = new Vector2(halfHeight * m_Camera.aspect, halfHeight);
            float limit = 1f + Margin;
            float streak = DustMath.StreakAmount(shipCamera.Speed, shipCamera.SpeedForMaxSize, m_StreakStart);
            float maxStreak = m_MaxStreak * 2f * halfHeight;
            float deltaTime = Time.deltaTime;

            for (int i = 0; i < m_Layers.Length; i++)
            {
                Layer layer = m_Layers[i];
                Mote[] motes = m_Motes[i];
                ParticleSystem.Particle[] particles = m_Particles[i];
                Vector2 viewShift = new Vector2(cameraDelta.x / half.x, cameraDelta.y / half.y) * layer.Depth;
                Vector2 streakVector = Vector2.ClampMagnitude(-shipCamera.Velocity * layer.Depth * m_StreakSeconds * streak, maxStreak);
                float size = layer.Size * 2f * halfHeight;
                // Stretched billboards collapse with zero velocity, so keep a negligible one to give them a direction.
                float minStreak = size * 0.01f;
                if (streakVector.sqrMagnitude < minStreak * minStreak)
                {
                    Vector2 direction = shipCamera.Velocity.sqrMagnitude > 1e-6f ? -shipCamera.Velocity.normalized : Vector2.up;
                    streakVector = direction * minStreak;
                }

                for (int j = 0; j < motes.Length; j++)
                {
                    ref Mote mote = ref motes[j];
                    mote.Age += deltaTime;
                    if (mote.Age >= mote.Lifetime) Respawn(ref mote);
                    mote.View -= viewShift;
                    mote.View.x = DustMath.Wrap(mote.View.x, limit);
                    mote.View.y = DustMath.Wrap(mote.View.y, limit);

                    Color color = layer.Tint;
                    color.a *= DustMath.Fade(mote.Age, mote.Lifetime, m_FadeTime);
                    Vector2 world = cameraPosition + Vector2.Scale(mote.View, half);
                    particles[j] = new ParticleSystem.Particle
                    {
                        position = new Vector3(world.x, world.y, layer.Z),
                        velocity = streakVector,
                        startSize = size,
                        startColor = color,
                        startLifetime = ParticleLifetime,
                        remainingLifetime = ParticleLifetime,
                    };
                }
                m_Systems[i].SetParticles(particles, particles.Length);
            }
        }
    }
}
