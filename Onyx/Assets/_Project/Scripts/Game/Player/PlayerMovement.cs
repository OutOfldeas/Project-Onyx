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

        private void Update()
        {
            if (!IsSpawned || !IsOwner)
            {
                return;
            }

            Vector2 input = ReadInput();
            if (input.sqrMagnitude > 1f)
            {
                input.Normalize();
            }

            transform.position += (Vector3)(input * (speed * Time.deltaTime));
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
