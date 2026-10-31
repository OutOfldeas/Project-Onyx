using Onyx.Game.Session;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Onyx.Game.Player
{
    /// <summary>
    /// Zeigt die Spielfigur als Platzhalter-Sprite in der Farbe des Spielers, grau, wenn sie
    /// kampfunfähig ist, und mit einer Helmlampe, die nachts leuchtet.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class PlayerVisual : NetworkBehaviour
    {
        private static readonly Color DownedTint = new Color(0.45f, 0.45f, 0.55f, 0.85f);
        private static readonly Color LampColor = new Color32(0xFF, 0xE8, 0xC0, 0xFF);

        private SpriteRenderer spriteRenderer;
        private PlayerVitals vitals;
        private Light2D lamp;

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            spriteRenderer.spriteSortPoint = SpriteSortPoint.Pivot;
            vitals = GetComponent<PlayerVitals>();
        }

        public override void OnNetworkSpawn()
        {
            spriteRenderer.sprite = PlaceholderSprites.Character(RoleColors.ForSlot(OwnerClientId));

            var lampGo = new GameObject("Helmlampe");
            lampGo.transform.SetParent(transform, false);
            lampGo.transform.localPosition = new Vector3(0f, 1.2f, 0f);
            lamp = lampGo.AddComponent<Light2D>();
            lamp.lightType = Light2D.LightType.Point;
            lamp.color = LampColor;
            lamp.pointLightInnerRadius = 0.6f;
            lamp.pointLightOuterRadius = 3.5f;
            lamp.falloffIntensity = 0.7f;
            lamp.intensity = 0f;
        }

        private void Update()
        {
            if (!IsSpawned)
            {
                return;
            }

            bool isDowned = vitals != null && vitals.IsDowned;
            spriteRenderer.color = isDowned ? DownedTint : Color.white;

            if (lamp != null && GameSession.Instance != null)
            {
                float night = 1f - GameSession.Instance.World.Clock.Daylight;
                lamp.intensity = isDowned ? 0f : Mathf.Clamp01(night * 1.2f) * 0.85f;
            }
        }
    }
}
