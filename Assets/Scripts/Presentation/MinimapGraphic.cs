using PewPewPew.Core;
using UnityEngine;
using UnityEngine.UI;

namespace PewPewPew.Presentation
{
    public class MinimapGraphic : MaskableGraphic
    {
        internal enum Layer { Background, Border, Contacts }
        private MinimapRoot m_Root;
        private Layer m_Layer;

        internal void Initialize(MinimapRoot root, Layer layer)
        {
            m_Root = root;
            m_Layer = layer;
            raycastTarget = false;
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            if (m_Root == null) return;
            Rect rect = rectTransform.rect;
            Vector2 center = rect.center;
            float radius = Mathf.Min(rect.width, rect.height) * 0.5f;
            if (radius <= 0f) return;
            if (m_Layer == Layer.Background)
            {
                Circle(mesh, center, radius, m_Root.BackgroundColor, 64);
                return;
            }
            if (m_Layer == Layer.Border)
            {
                Ring(mesh, center, radius, Mathf.Min(radius, m_Root.BorderWidth), m_Root.BorderColor);
                return;
            }
            if (m_Root.LocalShip == null) return;
            float scale = radius / m_Root.DisplayRange;
            Vector2 origin = m_Root.LocalShip.transform.position;
            if (m_Root.ShowScan) DrawScans(mesh, center, origin, scale);
            foreach (MinimapRoot.Contact contact in m_Root.Contacts)
            {
                Vector2 offset = contact.Position - origin;
                if (offset.sqrMagnitude > m_Root.DisplayRange * m_Root.DisplayRange) continue;
                Vector2 position = center + offset * scale;
                Color tint = m_Root.ContactColor(contact);
                if (contact.Source is Ships.Ship) Triangle(mesh, position, contact.Forward, 5f, tint);
                else Circle(mesh, position, Mathf.Max(2f, contact.WorldRadius * scale), tint, 12);
            }
            Triangle(mesh, center, m_Root.LocalShip.transform.up, 6f, m_Root.PlayerColor);
        }

        private void DrawScans(VertexHelper mesh, Vector2 center, Vector2 origin, float scale)
        {
            Color tint = m_Root.PlayerColor;
            tint.a *= 0.3f;
            foreach (MinimapRoot.Emitter emitter in m_Root.Emitters)
            {
                double elapsed = Time.timeAsDouble - emitter.Started;
                Vector2 position = center + ((Vector2)emitter.Mount.position - origin) * scale;
                Vector2 forward = emitter.Forward;
                float range = emitter.Def.Range * scale;
                if (emitter.Def.Mode == RadarScanMode.Sweep)
                {
                    float angle = RadarMath.SweepAngle(elapsed, emitter.Def.HalfAngle, emitter.Def.SweepSpeed);
                    Line(mesh, position, position + Rotate(forward, angle) * range, 1f, tint);
                }
                else if (emitter.Def.Mode == RadarScanMode.CircularPulse)
                {
                    double interval = 1d / emitter.Def.RefreshRate;
                    double age = elapsed % interval;
                    for (int pulse = 0; pulse < 32 && age <= elapsed; pulse++, age += interval)
                    {
                        float pulseRadius = (float)age * emitter.Def.PulseSpeed * scale;
                        if (pulseRadius > range) break;
                        Arc(mesh, position, pulseRadius, forward, emitter.Def.HalfAngle, 1f, tint);
                    }
                }
                if (emitter.Def.HalfAngle < 180f)
                {
                    Line(mesh, position, position + Rotate(forward, -emitter.Def.HalfAngle) * range, 1f, tint);
                    Line(mesh, position, position + Rotate(forward, emitter.Def.HalfAngle) * range, 1f, tint);
                }
            }
        }

        private static Vector2 Rotate(Vector2 direction, float degrees)
        {
            float radians = degrees * Mathf.Deg2Rad;
            return new Vector2(direction.x * Mathf.Cos(radians) - direction.y * Mathf.Sin(radians),
                direction.x * Mathf.Sin(radians) + direction.y * Mathf.Cos(radians));
        }

        private static void Triangle(VertexHelper mesh, Vector2 center, Vector2 forward, float size, Color tint)
        {
            Vector2 right = new Vector2(forward.y, -forward.x);
            int index = mesh.currentVertCount;
            mesh.AddVert(center + forward * size, tint, Vector2.zero);
            mesh.AddVert(center - forward * size * 0.6f + right * size * 0.6f, tint, Vector2.zero);
            mesh.AddVert(center - forward * size * 0.6f - right * size * 0.6f, tint, Vector2.zero);
            mesh.AddTriangle(index, index + 1, index + 2);
        }

        private static void Circle(VertexHelper mesh, Vector2 center, float radius, Color tint, int segments)
        {
            int index = mesh.currentVertCount;
            mesh.AddVert(center, tint, Vector2.zero);
            for (int segment = 0; segment <= segments; segment++)
            {
                float angle = segment * Mathf.PI * 2f / segments;
                mesh.AddVert(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius, tint, Vector2.zero);
                if (segment > 0) mesh.AddTriangle(index, index + segment, index + segment + 1);
            }
        }

        private static void Ring(VertexHelper mesh, Vector2 center, float radius, float width, Color tint)
        {
            Arc(mesh, center, radius - width * 0.5f, Vector2.up, 180f, width, tint);
        }

        private static void Arc(VertexHelper mesh, Vector2 center, float radius, Vector2 forward, float halfAngle, float width, Color tint)
        {
            if (radius <= 0f || width <= 0f) return;
            const int segments = 64;
            int index = mesh.currentVertCount;
            for (int segment = 0; segment <= segments; segment++)
            {
                Vector2 direction = Rotate(forward, -halfAngle + 2f * halfAngle * segment / segments);
                mesh.AddVert(center + direction * Mathf.Max(0f, radius - width * 0.5f), tint, Vector2.zero);
                mesh.AddVert(center + direction * (radius + width * 0.5f), tint, Vector2.zero);
                if (segment == 0) continue;
                int current = index + segment * 2;
                mesh.AddTriangle(current - 2, current - 1, current);
                mesh.AddTriangle(current, current - 1, current + 1);
            }
        }

        private static void Line(VertexHelper mesh, Vector2 start, Vector2 end, float width, Color tint)
        {
            Vector2 delta = (end - start).normalized;
            Vector2 normal = new Vector2(-delta.y, delta.x) * width * 0.5f;
            int index = mesh.currentVertCount;
            mesh.AddVert(start - normal, tint, Vector2.zero);
            mesh.AddVert(start + normal, tint, Vector2.zero);
            mesh.AddVert(end - normal, tint, Vector2.zero);
            mesh.AddVert(end + normal, tint, Vector2.zero);
            mesh.AddTriangle(index, index + 1, index + 2);
            mesh.AddTriangle(index + 2, index + 1, index + 3);
        }
    }
}