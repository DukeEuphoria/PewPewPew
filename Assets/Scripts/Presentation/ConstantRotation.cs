using UnityEngine;

namespace PewPewPew.Presentation
{
    /// Spins a purely visual object at a constant rate, in local space.
    public class ConstantRotation : MonoBehaviour
    {
        [SerializeField, Tooltip("Degrees per second around local X, Y and Z.")] private Vector3 m_DegreesPerSecond = new Vector3(0f, 1f, 0f);

        private void Update() => transform.Rotate(m_DegreesPerSecond * Time.deltaTime, Space.Self);
    }
}
