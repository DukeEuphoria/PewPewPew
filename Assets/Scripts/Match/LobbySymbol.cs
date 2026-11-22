using UnityEngine;
using UnityEngine.UI;
using PewPewPew.Ships;

namespace PewPewPew.Match
{
    public enum LobbySymbolKind { Play, Stop, Skull, Crosshair, Gun, Shield, Thruster, SubSystem, Lock, Delete }

    /// Lobby and loadout pictograms drawn from replaceable textures in LobbySymbolLibrary.
    [RequireComponent(typeof(CanvasRenderer))]
    public class LobbySymbol : MaskableGraphic
    {
        [SerializeField] private LobbySymbolKind m_Kind;

        public LobbySymbolKind Kind
        {
            get => m_Kind;
            set { if (m_Kind == value) return; m_Kind = value; SetAllDirty(); }
        }

        public static LobbySymbolKind ForComponent(ComponentType type)
        {
            switch (type)
            {
                case ComponentType.Gun: return LobbySymbolKind.Gun;
                case ComponentType.Shield: return LobbySymbolKind.Shield;
                case ComponentType.Thruster: return LobbySymbolKind.Thruster;
                case ComponentType.SubSystem: return LobbySymbolKind.SubSystem;
                default: throw new System.ArgumentOutOfRangeException(nameof(type));
            }
        }

        public Sprite Sprite => LobbySymbolLibrary.Get(m_Kind);
        public override Texture mainTexture => Sprite != null ? Sprite.texture : s_WhiteTexture;

        // Square textured quad; the artwork itself lives in Art/Loadout Icons/Symbols.
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Sprite sprite = Sprite;
            if (sprite == null) return;
            Rect rect = GetPixelAdjustedRect();
            float size = Mathf.Min(rect.width, rect.height);
            Vector2 min = rect.center - Vector2.one * size / 2;
            Vector2 max = rect.center + Vector2.one * size / 2;
            Vector4 uv = UnityEngine.Sprites.DataUtility.GetOuterUV(sprite);
            Color32 tint = color;
            mesh.AddVert(new Vector3(min.x, min.y), tint, new Vector2(uv.x, uv.y));
            mesh.AddVert(new Vector3(min.x, max.y), tint, new Vector2(uv.x, uv.w));
            mesh.AddVert(new Vector3(max.x, max.y), tint, new Vector2(uv.z, uv.w));
            mesh.AddVert(new Vector3(max.x, min.y), tint, new Vector2(uv.z, uv.y));
            mesh.AddTriangle(0, 1, 2);
            mesh.AddTriangle(2, 3, 0);
        }
    }
}