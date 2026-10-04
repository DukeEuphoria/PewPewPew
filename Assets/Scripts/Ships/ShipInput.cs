using Mirror;
using PewPewPew.Networking;
using PewPewPew.Presentation;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PewPewPew.Ships
{
    /// Reads the local player's input and sends it to the server: WASD-style thrust on W, mouse aim,
    /// left button main gun, right button or Space secondary gun, 1-4 sub systems.
    [RequireComponent(typeof(Ship))]
    public class ShipInput : NetworkBehaviour
    {
        private const float AimSendInterval = 0.05f;

        private Ship m_Ship;
        private ShipControls m_Sent;
        private float m_NextAimSend;

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
            var controls = new ShipControls
            {
                Thrust = Keyboard.current.wKey.isPressed ? 1f : 0f,
                AimPoint = camera.ScreenToWorldPoint(screenPoint),
                FireMain = Mouse.current.leftButton.isPressed,
                FireSecondary = Mouse.current.rightButton.isPressed || Keyboard.current.spaceKey.isPressed,
            };
            // A menu is up: release everything and keep the last aim.
            bool blocked = MenuScreenFlow.AnyBlocking;
            if (blocked) controls = new ShipControls { AimPoint = m_Sent.AimPoint };
            SendControls(controls);
            if (blocked) return;

            var subSystemKeys = new[] { Keyboard.current.digit1Key, Keyboard.current.digit2Key, Keyboard.current.digit3Key, Keyboard.current.digit4Key };
            for (int i = 0; i < subSystemKeys.Length; i++)
            {
                if (subSystemKeys[i].wasPressedThisFrame) m_Ship.CmdPressSubSystem(i);
            }
        }

        // Button changes go out immediately; aim changes are throttled.
        private void SendControls(ShipControls controls)
        {
            bool buttonsChanged = controls.Thrust != m_Sent.Thrust || controls.FireMain != m_Sent.FireMain || controls.FireSecondary != m_Sent.FireSecondary;
            bool aimChanged = controls.AimPoint != m_Sent.AimPoint && Time.time >= m_NextAimSend;
            if (!buttonsChanged && !aimChanged) return;

            m_Sent = controls;
            m_NextAimSend = Time.time + AimSendInterval;
            m_Ship.CmdSetControls(controls);
        }
    }
}
