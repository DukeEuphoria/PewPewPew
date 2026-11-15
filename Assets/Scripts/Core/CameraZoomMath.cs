using UnityEngine;

namespace PewPewPew.Core
{
    public static class CameraZoomMath
    {
        /// Blend an object's minimum half-height in as its circular footprint approaches the baseline view.
        public static float TargetSize(float baselineSize, float aspect, Vector2 offset, float radius, float minimumSize, float margin)
        {
            Vector2 outside = new Vector2(
                Mathf.Max(0f, Mathf.Abs(offset.x) - baselineSize * aspect),
                Mathf.Max(0f, Mathf.Abs(offset.y) - baselineSize));
            float gap = Mathf.Max(0f, outside.magnitude - radius);
            float influence = gap <= 0f ? 1f : margin > 0f ? Mathf.Clamp01(1f - gap / margin) : 0f;
            return Mathf.Lerp(baselineSize, Mathf.Max(baselineSize, minimumSize), influence);
        }
    }
}
