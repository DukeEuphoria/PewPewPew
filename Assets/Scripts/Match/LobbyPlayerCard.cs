using System.Collections;
using PewPewPew.Core;
using Steamworks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PewPewPew.Match
{
    public class LobbyPlayerCard : MonoBehaviour
    {
        [SerializeField] private RawImage m_Avatar;
        [SerializeField] private RawImage m_Hull;
        [SerializeField] private Image m_ReadyOutline;
        [SerializeField] private TMP_Text m_NameLabel;
        [SerializeField] private TMP_Text m_Kills;
        [SerializeField] private TMP_Text m_Deaths;

        private uint m_PlayerId;
        private ulong m_SteamId;
        private ShipLoadout m_RenderedLoadout;
        private bool m_HasHull;
        private RenderTexture m_HullTexture;
        private Texture2D m_AvatarTexture;
        private Coroutine m_AvatarRequest;

        public void Bind(PlayerState player, LobbyHullRenderer renderer)
        {
            gameObject.SetActive(player != null);
            if (player == null) { Release(); return; }
            if (m_PlayerId != player.netId || m_SteamId != player.SteamId)
            {
                Release();
                m_PlayerId = player.netId;
                m_SteamId = player.SteamId;
                if (m_SteamId != 0 && SteamClient.IsValid) m_AvatarRequest = StartCoroutine(LoadAvatar(m_SteamId));
            }
            m_NameLabel.text = player.Name;
            m_Kills.text = player.Kills.ToString();
            m_Deaths.text = player.Deaths.ToString();
            m_ReadyOutline.color = player.Ready ? new Color(0.15f, 1f, 0.35f) : new Color(0.16f, 0.25f, 0.32f);
            ShipLoadout loadout = player.Loadout;
            if (!m_HasHull || loadout.Hull != m_RenderedLoadout.Hull ||
                !loadout.Colour0.Equals(m_RenderedLoadout.Colour0) || !loadout.Colour1.Equals(m_RenderedLoadout.Colour1))
            {
                ReleaseHull();
                m_HullTexture = renderer.Render(loadout);
                m_Hull.texture = m_HullTexture;
                m_Hull.enabled = true;
                m_RenderedLoadout = loadout;
                m_HasHull = true;
            }
        }

        private IEnumerator LoadAvatar(ulong steamId)
        {
            var request = SteamFriends.GetMediumAvatarAsync(steamId);
            while (!request.IsCompleted) yield return null;
            if (request.IsFaulted)
            {
                Debug.LogError($"Steam avatar lookup failed for {steamId}: {request.Exception}", this);
                yield break;
            }
            if (request.IsCanceled)
            {
                Debug.LogWarning($"Steam avatar lookup was cancelled for {steamId}.", this);
                yield break;
            }
            var result = request.Result;
            if (!result.HasValue)
            {
                Debug.LogWarning($"No Steam avatar available for {steamId}; using the card background.", this);
                yield break;
            }
            var image = result.Value;
            m_AvatarTexture = new Texture2D((int)image.Width, (int)image.Height, TextureFormat.RGBA32, false);
            m_AvatarTexture.LoadRawTextureData(image.Data);
            m_AvatarTexture.Apply();
            m_Avatar.texture = m_AvatarTexture;
            m_Avatar.uvRect = new Rect(0, 1, 1, -1);
            m_Avatar.enabled = true;
        }

        private void ReleaseHull()
        {
            m_Hull.texture = null;
            m_Hull.enabled = false;
            if (m_HullTexture != null)
            {
                m_HullTexture.Release();
                Dispose(m_HullTexture);
                m_HullTexture = null;
            }
            m_HasHull = false;
        }

        private void Release()
        {
            if (m_AvatarRequest != null) StopCoroutine(m_AvatarRequest);
            m_AvatarRequest = null;
            m_Avatar.texture = null;
            m_Avatar.enabled = false;
            if (m_AvatarTexture != null) Dispose(m_AvatarTexture);
            m_AvatarTexture = null;
            ReleaseHull();
            m_PlayerId = 0;
            m_SteamId = 0;
        }

        private void OnDestroy() => Release();

        private static void Dispose(Object value)
        {
            if (Application.isPlaying) Destroy(value);
            else DestroyImmediate(value);
        }

        private void OnDisable()
        {
            if (m_AvatarRequest != null) StopCoroutine(m_AvatarRequest);
            m_AvatarRequest = null;
        }

        private void OnEnable()
        {
            if (m_AvatarTexture == null && m_SteamId != 0 && SteamClient.IsValid)
                m_AvatarRequest = StartCoroutine(LoadAvatar(m_SteamId));
        }
    }
}
