using PewPewPew.Core;
using UnityEngine;

namespace PewPewPew.World
{
    internal static class AsteroidEffects
    {
        private const int MaxLiveFragments = 96;
        private static int s_LiveFragments;
        private static Mesh s_FragmentMesh;

        public static void Play(Vector2 point, GameObject explosion, AudioClip sound, bool shatter,
            uint seed, Vector2 inheritedVelocity, Material material, float cellSize)
        {
            if (explosion != null) Object.Instantiate(explosion, point, explosion.transform.rotation);
            if (sound != null) AudioSource.PlayClipAtPoint(sound, point, shatter ? 0.8f : 0.35f);
            if (material == null) return;
            if (s_FragmentMesh == null)
            {
                var field = new AsteroidGeometry(2, 2, new byte[] { 255, 255, 255, 0 });
                s_FragmentMesh = AsteroidMesh.Create(AsteroidMesh.Paths(field, Vector2.one, out _), 0.5f);
            }
            int count = Mathf.Min(shatter ? 12 : 5, MaxLiveFragments - s_LiveFragments);
            for (int i = 0; i < count; i++)
            {
                var rock = new GameObject("Temporary asteroid fragment", typeof(MeshFilter), typeof(MeshRenderer));
                rock.GetComponent<MeshFilter>().sharedMesh = s_FragmentMesh;
                rock.GetComponent<MeshRenderer>().sharedMaterial = material;
                float angle = (AsteroidGeometry.Next(ref seed) >> 8) / 16777216f * 360f;
                float scale = Mathf.Clamp(cellSize * (shatter ? 0.18f : 0.09f), 0.04f, 0.4f);
                rock.transform.SetPositionAndRotation(point, Quaternion.Euler(0f, 0f, angle));
                rock.transform.localScale = Vector3.one * scale;
                var motion = rock.AddComponent<AsteroidDebrisMotion>();
                motion.Velocity = inheritedVelocity + (Vector2)(Quaternion.Euler(0f, 0f, angle) * Vector2.right) * (shatter ? 4f : 2f);
                motion.Spin = angle - 180f;
                s_LiveFragments++;
            }
        }

        internal static void Released() => s_LiveFragments = Mathf.Max(0, s_LiveFragments - 1);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            s_LiveFragments = 0;
            if (s_FragmentMesh != null) Object.Destroy(s_FragmentMesh);
            s_FragmentMesh = null;
        }
    }

    internal sealed class AsteroidDebrisMotion : MonoBehaviour
    {
        public Vector2 Velocity;
        public float Spin;
        private float m_Age;
        private void Update()
        {
            m_Age += Time.deltaTime;
            transform.position += (Vector3)(Velocity * Time.deltaTime);
            transform.Rotate(0f, 0f, Spin * Time.deltaTime);
            if (m_Age > 1.5f) transform.localScale *= Mathf.Max(0f, 1f - 4f * Time.deltaTime);
            if (m_Age > 2f) Destroy(gameObject);
        }
        private void OnDestroy() => AsteroidEffects.Released();
    }
}
