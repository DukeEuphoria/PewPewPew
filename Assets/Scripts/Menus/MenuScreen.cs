using UnityEngine;
using UnityEngine.Events;

namespace PewPewPew.Networking
{
    public enum MenuScreenId
    {
        INVALID =-1,
        Title =0,
        Main,
        Host,
        Join,
        Settings,
        Gameplay,
        GameplaySettings,
        Quit,
        GameplayQuit
    }

    public abstract class MenuScreen : MonoBehaviour
    {
        [SerializeField] private MenuScreenFlow m_MenuScreenFlow;
        [SerializeField] private GameObject m_ScreenRoot;
        [SerializeField] private bool m_InitialMenu;
        [SerializeField, Tooltip("While this screen is shown, gameplay input (ship controls, HUD buttons) is ignored.")] private bool m_Blocking;
        [SerializeField] private bool m_NavigateOnEscape;
        [SerializeField] private MenuScreenId m_EscapeDestination;
        [SerializeField] private bool m_NavigateOnAnyKey;
        [SerializeField] private MenuScreenId m_AnyKeyDestination;
        [SerializeField] private UnityEvent m_OnInitialize = new UnityEvent();
        [SerializeField] private UnityEvent m_OnShutdown = new UnityEvent();

        public abstract MenuScreenId ScreenId { get; }
        public bool IsInitialMenu => m_InitialMenu;
        public bool IsBlocking => m_Blocking;
        public bool NavigatesOnEscape => m_NavigateOnEscape;
        public MenuScreenId EscapeDestination => m_EscapeDestination;
        public bool NavigatesOnAnyKey => m_NavigateOnAnyKey;
        public MenuScreenId AnyKeyDestination => m_AnyKeyDestination;
        public GameObject ScreenRoot => m_ScreenRoot != null ? m_ScreenRoot : gameObject;

        protected virtual void Awake()
        {
            RegisterWithFlow();
        }

        protected virtual void OnDestroy()
        {
            if (m_MenuScreenFlow != null) m_MenuScreenFlow.UnregisterMenu(this);
        }

        internal void SetMenuScreenFlow(MenuScreenFlow flow)
        {
            m_MenuScreenFlow = flow;
        }

        public void InitializeMenu()
        {
            m_OnInitialize?.Invoke();
            OnInitializeMenu();
        }

        public void ShutdownMenu()
        {
            OnShutdownMenu();
            m_OnShutdown?.Invoke();
        }

        protected virtual void OnInitializeMenu()
        {
        }

        protected virtual void OnShutdownMenu()
        {
        }

        private void RegisterWithFlow()
        {
            Debug.Assert(m_MenuScreenFlow != null, $"{nameof(MenuScreen)} on '{name}' requires a MenuScreenFlow reference.", this);
            if (m_MenuScreenFlow == null) return;

            m_MenuScreenFlow.RegisterMenu(this);
        }
    }
}