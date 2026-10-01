using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace PewPewPew.Networking
{
    public class MenuScreenFlow : MonoBehaviour
    {
        private enum FadeState
        {
            Idle,
            FadingOut,
            FadingIn
        }

        [FormerlySerializedAs("fadeOverlay"), SerializeField] private CanvasGroup m_FadeOverlay;
        [FormerlySerializedAs("fadeDuration"), SerializeField, Min(0f)] private float m_FadeDuration = 0.35f;
        [FormerlySerializedAs("raycastInput"), SerializeField] private MenuRaycastInput m_RaycastInput;

        private readonly Dictionary<MenuScreenId, MenuScreen> m_Menus = new Dictionary<MenuScreenId, MenuScreen>();
        private MenuScreen m_InitialMenu;
        private MenuScreen m_CurrentMenu;
        private MenuScreen m_DestinationMenu;
        private FadeState m_FadeState;
        private float m_FadeElapsed;
        private float m_FadeStartAlpha;
        private float m_FadeTargetAlpha;

        public bool IsTransitioning => m_FadeState != FadeState.Idle;
        public MenuScreenId? CurrentScreen => m_CurrentMenu != null ? m_CurrentMenu.ScreenId : null;
        public event Action<MenuScreenId> ScreenChanged;

        private void Start()
        {
            if (m_FadeOverlay == null)
            {
                Debug.LogError("MenuScreenFlow requires a CanvasGroup fade overlay.", this);
                enabled = false;
                return;
            }

            if (m_InitialMenu == null)
            {
                Debug.LogError("MenuScreenFlow requires exactly one menu with Initial Menu enabled.", this);
                enabled = false;
                return;
            }

            if (!SetMenuActive(m_InitialMenu))
            {
                enabled = false;
                return;
            }

            m_FadeOverlay.alpha = 0f;
            m_FadeOverlay.interactable = false;
            m_FadeOverlay.blocksRaycasts = false;
            m_FadeOverlay.gameObject.SetActive(false);
            m_RaycastInput?.SetDetectionEnabled(true);
            m_FadeState = FadeState.Idle;
        }

        private void Update()
        {
            if (m_FadeState != FadeState.Idle)
            {
                UpdateFade();
                return;
            }

            if (Keyboard.current == null) return;

            if (m_CurrentMenu == null) return;

            if (Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                if (m_CurrentMenu.NavigatesOnEscape)
                {
                    ShowScreen(m_CurrentMenu.EscapeDestination);
                }
            }
            else if (m_CurrentMenu.NavigatesOnAnyKey && Keyboard.current.anyKey.wasPressedThisFrame)
            {
                ShowScreen(m_CurrentMenu.AnyKeyDestination);
            }
        }

        private void OnDisable()
        {
            if (m_FadeState == FadeState.Idle) return;

            m_FadeState = FadeState.Idle;
            m_DestinationMenu = null;
            if (m_FadeOverlay != null)
            {
                m_FadeOverlay.alpha = 0f;
                m_FadeOverlay.interactable = false;
                m_FadeOverlay.blocksRaycasts = false;
                m_FadeOverlay.gameObject.SetActive(false);
            }
            m_RaycastInput?.SetDetectionEnabled(true);
        }

        public void ShowScreen(MenuScreenId screen)
        {
            if (IsTransitioning || m_CurrentMenu == null || screen == m_CurrentMenu.ScreenId) return;
            if (!m_Menus.TryGetValue(screen, out MenuScreen destination))
            {
                Debug.LogError($"No menu is registered for screen '{screen}'.", this);
                return;
            }

            m_DestinationMenu = destination;
            m_CurrentMenu?.ShutdownMenu();
            m_RaycastInput?.SetDetectionEnabled(false);
            m_FadeOverlay.gameObject.SetActive(true);
            m_FadeOverlay.interactable = false;
            m_FadeOverlay.blocksRaycasts = true;
            BeginFade(FadeState.FadingOut, 1f);
        }

        public void RegisterMenu(MenuScreen menu)
        {
            if (menu == null) return;

            if (m_Menus.TryGetValue(menu.ScreenId, out MenuScreen registeredMenu) && registeredMenu != menu)
            {
                Debug.LogError($"More than one menu is registered for screen '{menu.ScreenId}'.", menu);
                return;
            }

            Debug.Log($"Registering menu for screen '{menu.ScreenId}'.", menu);

            m_Menus[menu.ScreenId] = menu;
            if (!menu.IsInitialMenu) return;

            if (m_InitialMenu != null && m_InitialMenu != menu)
            {
                Debug.LogError("Only one menu can have Initial Menu enabled.", menu);
                return;
            }

            m_InitialMenu = menu;
            Debug.Log($"Set initial menu to screen '{menu.ScreenId}'.", menu);
        }

        public void UnregisterMenu(MenuScreen menu)
        {
            if (menu == null) return;
            if (m_Menus.TryGetValue(menu.ScreenId, out MenuScreen registeredMenu) && registeredMenu == menu)
            {
                m_Menus.Remove(menu.ScreenId);
            }
            if (m_InitialMenu == menu) m_InitialMenu = null;
            if (m_CurrentMenu == menu) m_CurrentMenu = null;
        }

        private void UpdateFade()
        {
            m_FadeElapsed += Time.unscaledDeltaTime;
            float progress = m_FadeDuration <= 0f ? 1f : Mathf.Clamp01(m_FadeElapsed / m_FadeDuration);
            m_FadeOverlay.alpha = Mathf.Lerp(m_FadeStartAlpha, m_FadeTargetAlpha, progress);
            if (progress < 1f) return;

            if (m_FadeState == FadeState.FadingOut)
            {
                MenuScreen target = m_DestinationMenu;
                m_DestinationMenu = null;
                if (SetMenuActive(target))
                {
                    BeginFade(FadeState.FadingIn, 0f);
                    return;
                }

                BeginFade(FadeState.FadingIn, 0f);
                return;
            }

            m_FadeState = FadeState.Idle;
            m_FadeOverlay.interactable = false;
            m_FadeOverlay.blocksRaycasts = false;
            m_FadeOverlay.gameObject.SetActive(false);
            m_RaycastInput?.SetDetectionEnabled(true);
        }

        private void BeginFade(FadeState state, float targetAlpha)
        {
            m_FadeState = state;
            m_FadeElapsed = 0f;
            m_FadeStartAlpha = m_FadeOverlay.alpha;
            m_FadeTargetAlpha = targetAlpha;
        }

        private bool SetMenuActive(MenuScreen target)
        {
            if (target == null || target.ScreenRoot == null)
            {
                Debug.LogError("Cannot activate an unregistered menu or a menu without a root GameObject.", this);
                return false;
            }


            MenuScreen[] allMenus = m_Menus.Values.ToArray<MenuScreen>();
            foreach (MenuScreen menu in allMenus)
            {
                if (menu.ScreenRoot != null)
                {
                    menu.ScreenRoot.SetActive(menu == target);
                }
            }

            m_CurrentMenu = target;
            target.InitializeMenu();
            ScreenChanged?.Invoke(target.ScreenId);
            return true;
        }
    }
}