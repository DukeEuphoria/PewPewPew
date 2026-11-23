using Mirror;
using PewPewPew.Networking;
using PewPewPew.Presentation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace PewPewPew.Ships
{
    /// Reads the local player's input and sends it to the server: W/S thrust, A/D strafe, mouse aim,
    /// left button main gun, right button secondary gun, Space shields, 1-0 and Shift+1-0 components.
    [RequireComponent(typeof(Ship))]
    public class ShipInput : NetworkBehaviour
    {
        private const float AimSendInterval = 0.05f;

        private Ship m_Ship;
        private ShipControls m_Sent;
        private float m_NextAimSend;
        private bool m_ShieldToggleChord;

        private void Awake() => m_Ship = GetComponent<Ship>();

        // On the host, ownership can be set a frame before this callback runs, so m_Ship must not depend on it.
        public override void OnStartAuthority()
        {
            var prediction = GetComponent<ShipVisualPrediction>();
            Transform cameraTarget = prediction != null ? prediction.CameraTarget : transform;
            if (ShipCamera.Instance != null) ShipCamera.Instance.Follow(cameraTarget);
        }

        private void Update()
        {
            Camera camera = Camera.main;
            if (!isOwned || camera == null || Keyboard.current == null || Mouse.current == null) return;

            Vector3 screenPoint = Mouse.current.position.ReadValue();
            screenPoint.z = -camera.transform.position.z;
            bool blocked = MenuScreenFlow.AnyBlocking;
            bool overUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            bool shift = Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed;
            if (!Keyboard.current.spaceKey.isPressed) m_ShieldToggleChord = false;
            if (!blocked && shift && Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                m_ShieldToggleChord = true;
                m_Ship.CmdToggleShields();
            }
            var controls = new ShipControls
            {
                Thrust = Keyboard.current.wKey.isPressed ? 1f : 0f,
                ReverseThrust = Keyboard.current.sKey.isPressed ? 1f : 0f,
                SideThrust = (Keyboard.current.dKey.isPressed ? 1f : 0f) - (Keyboard.current.aKey.isPressed ? 1f : 0f),
                AimPoint = camera.ScreenToWorldPoint(screenPoint),
                FireMain = !overUi && Mouse.current.leftButton.isPressed,
                FireSecondary = !overUi && Mouse.current.rightButton.isPressed,
                HoldShields = Keyboard.current.spaceKey.isPressed && !m_ShieldToggleChord,
            };
            // A menu is up: release everything and keep the last aim.
            if (blocked) controls = new ShipControls { AimPoint = m_Sent.AimPoint };
            SendControls(controls);
            if (blocked) return;

            var componentKeys = new[] { Keyboard.current.digit1Key, Keyboard.current.digit2Key, Keyboard.current.digit3Key,
                Keyboard.current.digit4Key, Keyboard.current.digit5Key, Keyboard.current.digit6Key, Keyboard.current.digit7Key,
                Keyboard.current.digit8Key, Keyboard.current.digit9Key, Keyboard.current.digit0Key };
            for (int i = 0; i < componentKeys.Length; i++)
            {
                if (componentKeys[i].wasPressedThisFrame) m_Ship.PressComponentShortcut(i + (shift ? 10 : 0));
            }
        }

        // Button changes go out immediately; aim changes are throttled.
        private void SendControls(ShipControls controls)
        {
            bool buttonsChanged = controls.Thrust != m_Sent.Thrust || controls.ReverseThrust != m_Sent.ReverseThrust ||
                controls.SideThrust != m_Sent.SideThrust || controls.FireMain != m_Sent.FireMain ||
                controls.FireSecondary != m_Sent.FireSecondary || controls.HoldShields != m_Sent.HoldShields;
            bool aimChanged = controls.AimPoint != m_Sent.AimPoint && Time.time >= m_NextAimSend;
            if (!buttonsChanged && !aimChanged) return;

            m_Sent = controls;
            m_NextAimSend = Time.time + AimSendInterval;
            m_Ship.CmdSetControls(controls);
        }
    }
}
