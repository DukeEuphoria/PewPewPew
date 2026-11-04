using UnityEngine;
using UnityEngine.UI;

namespace PewPewPew.Match
{
    public enum LobbySymbolKind { Play, Stop, Skull, Crosshair }

    /// Resolution-independent lobby pictograms, saved as ordinary canvas graphics.
    [RequireComponent(typeof(CanvasRenderer))]
    public class LobbySymbol : MaskableGraphic
    {
        [SerializeField] private LobbySymbolKind m_Kind;

        public LobbySymbolKind Kind
        {
            get => m_Kind;
            set { if (m_Kind == value) return; m_Kind = value; SetVerticesDirty(); }
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect rect = GetPixelAdjustedRect();
            Vector2 centre = rect.center;
            float size = Mathf.Min(rect.width, rect.height);
            if (m_Kind == LobbySymbolKind.Play)
                Triangle(mesh, centre + new Vector2(-0.43f, -0.48f) * size,
                    centre + new Vector2(-0.43f, 0.48f) * size, centre + new Vector2(0.48f, 0) * size, color);
            else if (m_Kind == LobbySymbolKind.Stop)
                Box(mesh, centre, new Vector2(size * 0.86f, size * 0.86f), color);
            else if (m_Kind == LobbySymbolKind.Crosshair)
            {
                Ring(mesh, centre, size * 0.30f, size * 0.23f, color);
                Box(mesh, centre, Vector2.one * size * 0.09f, color);
                for (int i = 0; i < 4; i++)
                {
                    float angle = i * Mathf.PI * 0.5f;
                    Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                    Box(mesh, centre + direction * size * 0.36f,
                        i % 2 == 0 ? new Vector2(size * 0.27f, size * 0.07f) : new Vector2(size * 0.07f, size * 0.27f), color);
                }
            }
            else
            {
                Ring(mesh, centre + Vector2.up * size * 0.1f, size * 0.38f, 0, color);
                Box(mesh, centre + Vector2.down * size * 0.25f, new Vector2(size * 0.45f, size * 0.3f), color);
                Color ink = new Color(0.025f, 0.04f, 0.06f, color.a);
                Box(mesh, centre + new Vector2(-0.15f, 0.1f) * size, Vector2.one * size * 0.14f, ink);
                Box(mesh, centre + new Vector2(0.15f, 0.1f) * size, Vector2.one * size * 0.14f, ink);
                for (int i = -1; i <= 1; i++)
                    Box(mesh, centre + new Vector2(i * 0.12f, -0.35f) * size, new Vector2(size * 0.035f, size * 0.15f), ink);
            }
        }

        private static void Ring(VertexHelper mesh, Vector2 centre, float outer, float inner, Color tint)
        {
            for (int i = 0; i < 32; i++)
            {
                float a = i * Mathf.PI * 2 / 32;
                float b = (i + 1) * Mathf.PI * 2 / 32;
                Vector2 start = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                Vector2 end = new Vector2(Mathf.Cos(b), Mathf.Sin(b));
                Triangle(mesh, centre + start * outer, centre + end * outer, centre + start * inner, tint);
                if (inner > 0) Triangle(mesh, centre + end * outer, centre + end * inner, centre + start * inner, tint);
            }
        }

        private static void Box(VertexHelper mesh, Vector2 centre, Vector2 size, Color tint)
        {
            Vector2 half = size / 2;
            Vector2 a = centre - half;
            Vector2 b = centre + new Vector2(-half.x, half.y);
            Vector2 c = centre + half;
            Vector2 d = centre + new Vector2(half.x, -half.y);
            Triangle(mesh, a, b, c, tint);
            Triangle(mesh, a, c, d, tint);
        }

        private static void Triangle(VertexHelper mesh, Vector2 a, Vector2 b, Vector2 c, Color tint)
        {
            int index = mesh.currentVertCount;
            mesh.AddVert(a, tint, Vector2.zero);
            mesh.AddVert(b, tint, Vector2.zero);
            mesh.AddVert(c, tint, Vector2.zero);
            mesh.AddTriangle(index, index + 1, index + 2);
        }
    }
}
