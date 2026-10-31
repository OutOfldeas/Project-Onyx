using Onyx.Core.CrashSite;
using Onyx.Game.CrashSite;
using Onyx.Game.DevTools;
using Onyx.Game.UI;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Onyx.Game.Player
{
    /// <summary>
    /// Bewegt die eigene Figur in acht Richtungen (WASD, Pfeiltasten oder linker Stick).
    /// Die Position wird vom Besitzer gesendet (NetworkTransform im Owner-Modus):
    /// Bewegung fühlt sich so ohne Verzögerung an. Spielzustände wie Sauerstoff oder
    /// Inventar bleiben dagegen in der Hand des Hosts.
    /// </summary>
    public sealed class PlayerMovement : NetworkBehaviour
    {
        [Tooltip("Laufgeschwindigkeit in Kacheln pro Sekunde.")]
        [SerializeField] private float speed = 5f;

        /// <summary>Laufgeschwindigkeit in Kacheln pro Sekunde (im Dev-Modus änderbar).</summary>
        public float Speed
        {
            get => speed;
            set => speed = Mathf.Max(0f, value);
        }

        private void Update()
        {
            if (!IsSpawned || !IsOwner || DevMode.BlocksGameplayInput || StarMapScreen.IsOpen)
            {
                return;
            }

            Vector2 input = ReadInput();
            if (input.sqrMagnitude > 1f)
            {
                input.Normalize();
            }

            Vector2 delta = input * (speed * Time.deltaTime);
            if (delta == Vector2.zero || (vitals != null && vitals.IsDowned))
            {
                return;
            }

            Vector3 position = transform.position;
            var site = CrashSiteNet.ActiveSite;
            if (site == null || NoClip)
            {
                transform.position = position + (Vector3)delta;
                return;
            }

            // Achsen einzeln prüfen, damit man an Wänden entlanggleitet statt hängen zu bleiben.
            var layout = site.Layout;
            if (!Blocked(layout, position.x + delta.x, position.y)) position.x += delta.x;
            if (!Blocked(layout, position.x, position.y + delta.y)) position.y += delta.y;
            transform.position = position;
        }

        /// <summary>Dev-Modus: durch Wände laufen.</summary>
        public static bool NoClip { get; set; }

        // Fußfläche der Figur (in Kacheln): halbe Breite und Tiefe ab dem Fußpunkt.
        private const float FootHalfWidth = 0.3f;
        private const float FootDepth = 0.25f;

        private static bool Blocked(CrashSiteLayout layout, float x, float y)
        {
            return layout.IsBlockedAt(x - FootHalfWidth, y) || layout.IsBlockedAt(x + FootHalfWidth, y)
                || layout.IsBlockedAt(x - FootHalfWidth, y + FootDepth) || layout.IsBlockedAt(x + FootHalfWidth, y + FootDepth);
        }

        private PlayerVitals vitals;

        private void Awake()
        {
            vitals = GetComponent<PlayerVitals>();
        }

        private static Vector2 ReadInput()
        {
            Vector2 value = Vector2.zero;

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) value.y += 1f;
                if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) value.y -= 1f;
                if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) value.x += 1f;
                if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) value.x -= 1f;
            }

            Gamepad gamepad = Gamepad.current;
            if (gamepad != null)
            {
                value += gamepad.leftStick.ReadValue();
            }

            return value;
        }
    }
}
