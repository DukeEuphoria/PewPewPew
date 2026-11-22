using UnityEngine;

namespace PewPewPew.Match
{
    /// Replaceable textures for lobby and loadout pictograms, loaded from Resources/LobbySymbols.
    [CreateAssetMenu(menuName = "PewPewPew/Lobby Symbol Library")]
    public class LobbySymbolLibrary : ScriptableObject
    {
        [System.Serializable]
        private struct Entry
        {
            public LobbySymbolKind Kind;
            public Sprite Sprite;
        }

        public const string ResourcePath = "LobbySymbols";
        [SerializeField] private Entry[] m_Symbols;
        private static LobbySymbolLibrary s_Instance;

        public static LobbySymbolLibrary Instance =>
            s_Instance != null ? s_Instance : s_Instance = Resources.Load<LobbySymbolLibrary>(ResourcePath);

        public static Sprite Get(LobbySymbolKind kind)
        {
            if (Instance == null || Instance.m_Symbols == null) return null;
            foreach (Entry entry in Instance.m_Symbols)
                if (entry.Kind == kind) return entry.Sprite;
            return null;
        }
    }
}
