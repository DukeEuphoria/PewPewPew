using UnityEngine;

namespace PewPewPew.Presentation
{
    /// Far background layer: 0 stays fixed in the world, 1 stays fixed on screen.
    public class ParallaxLayer : MonoBehaviour
    {
        [SerializeField, Range(0f, 1f)] private float m_Factor = 0.95f;

        private void LateUpdate()
        {
            if (ShipCamera.Instance == null) return;

            Vector3 camera = ShipCamera.Instance.transform.position;
            transform.position = new Vector3(camera.x * m_Factor, camera.y * m_Factor, transform.position.z);
        }
    }
}
