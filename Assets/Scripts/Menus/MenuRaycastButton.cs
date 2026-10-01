using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;

namespace PewPewPew.Networking
{
    [RequireComponent(typeof(BoxCollider))]
    public class MenuRaycastButton : MonoBehaviour
    {
        [FormerlySerializedAs("screenFlow"), SerializeField] private MenuScreenFlow m_ScreenFlow;
        [FormerlySerializedAs("destination"), SerializeField] private MenuScreenId m_Destination;
        [SerializeField] private UnityEvent m_OnActivate = new UnityEvent();

        private BoxCollider m_BoxCollider;

        private void Start()
        {
            FitColliderToRenderer();
        }


        private void FitColliderToRenderer()
        {
            if (m_BoxCollider == null) m_BoxCollider = GetComponent<BoxCollider>();

            Renderer[] targetRenderers = GetComponentsInChildren<Renderer>();
            if (m_BoxCollider == null || targetRenderers.Length == 0) return;

            bool hasBounds = false;
            Bounds localBounds = default;
            foreach (Renderer targetRenderer in targetRenderers)
            {
                Bounds worldBounds = targetRenderer.bounds;
                Vector3 worldExtents = worldBounds.extents;
                for (int xIndex = -1; xIndex <= 1; xIndex += 2)
                {
                    for (int yIndex = -1; yIndex <= 1; yIndex += 2)
                    {
                        for (int zIndex = -1; zIndex <= 1; zIndex += 2)
                        {
                            Vector3 worldCorner = worldBounds.center + Vector3.Scale(worldExtents,new Vector3(xIndex, yIndex, zIndex));
                            Vector3 localCorner = transform.InverseTransformPoint(worldCorner);
                            if (hasBounds)
                            {
                                localBounds.Encapsulate(localCorner);
                            }
                            else
                            {
                                localBounds = new Bounds(localCorner, Vector3.zero);
                                hasBounds = true;
                            }
                        }
                    }
                }
            }

            if (!hasBounds || localBounds.size.sqrMagnitude <= Mathf.Epsilon) return;
            m_BoxCollider.center = localBounds.center;
            m_BoxCollider.size = localBounds.size;
        }

        public void Activate()
        {
            m_OnActivate?.Invoke();

            if (m_ScreenFlow == null)
            {
                Debug.LogError($"{nameof(MenuRaycastButton)} on '{name}' requires a MenuScreenFlow reference.", this);
                return;
            }

            if (m_ScreenFlow.CurrentScreen == m_Destination) return;

            m_ScreenFlow.ShowScreen(m_Destination);
        }
    }
}