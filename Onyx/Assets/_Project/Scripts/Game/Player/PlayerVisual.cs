using Onyx.Core.Art;
using Onyx.Game.CrashSite;
using Onyx.Game.Session;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Onyx.Game.Player
{
    /// <summary>
    /// Zeigt die Spielfigur in der Farbe des Spielers mit Laufanimation in vier Richtungen,
    /// grau, wenn sie kampfunfähig ist, und mit einer Helmlampe, die nachts leuchtet.
    /// Richtung und Laufen werden aus der Bewegung abgeleitet – so sieht es bei allen
    /// Mitspielern gleich aus, ohne zusätzliche Netzwerkdaten.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class PlayerVisual : NetworkBehaviour
    {
        private static readonly Color DownedTint = new Color(0.45f, 0.45f, 0.55f, 0.85f);
        private static readonly Color LampColor = new Color32(0xFF, 0xE8, 0xC0, 0xFF);

        private SpriteRenderer spriteRenderer;
        private PlayerVitals vitals;
        private Light2D lamp;
        private Color roleColor;
        private Vector3 lastPosition;
        private Facing facing = Facing.Down;
        private float animationTime;
        private float stillFor;

        /// <summary>Laufbilder pro Sekunde.</summary>
        private const float FramesPerSecond = 8f;

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            spriteRenderer.spriteSortPoint = SpriteSortPoint.Pivot;
            vitals = GetComponent<PlayerVitals>();
        }

        public override void OnNetworkSpawn()
        {
            roleColor = RoleColors.ForSlot(OwnerClientId);
            spriteRenderer.sprite = CharacterSprites.Get(roleColor, facing, 0);
            lastPosition = transform.position;

            // Schatten am Boden, unter allen Figuren und Objekten
            var shadowGo = new GameObject("Schatten");
            shadowGo.transform.SetParent(transform, false);
            shadowGo.transform.localPosition = new Vector3(0f, 0.05f, 0f);
            var shadowRenderer = shadowGo.AddComponent<SpriteRenderer>();
            shadowRenderer.sprite = SiteTileArt.Shadow();
            shadowRenderer.sortingOrder = -50;

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
            Animate(isDowned);

            if (lamp != null)
            {
                // Lampe sitzt am Helm, etwas in Blickrichtung
                float dx = facing == Facing.Right ? 0.25f : facing == Facing.Left ? -0.25f : 0f;
                lamp.transform.localPosition = new Vector3(dx, 1.2f, 0f);
            }

            if (lamp != null && GameSession.Instance != null)
            {
                float night = 1f - GameSession.Instance.World.Clock.Daylight;
                lamp.intensity = isDowned ? 0f : Mathf.Clamp01(night * 1.2f) * 0.85f;
            }
        }

        private void Animate(bool isDowned)
        {
            Vector3 position = transform.position;
            Vector2 delta = position - lastPosition;
            lastPosition = position;

            bool moving = !isDowned && delta.sqrMagnitude > 0.000004f && delta.sqrMagnitude < 4f;
            if (moving)
            {
                stillFor = 0f;
                facing = Mathf.Abs(delta.x) > Mathf.Abs(delta.y)
                    ? (delta.x > 0f ? Facing.Right : Facing.Left)
                    : (delta.y > 0f ? Facing.Up : Facing.Down);
                animationTime += Time.deltaTime;
            }
            else
            {
                // Kurze Aussetzer (Netzwerk-Interpolation) nicht als Stehenbleiben werten
                stillFor += Time.deltaTime;
                if (stillFor > 0.12f) animationTime = 0f;
            }

            int frame = animationTime > 0f ? 1 + (int)(animationTime * FramesPerSecond) % 4 : 0;
            if (frame == 4) frame = 0;
            spriteRenderer.sprite = CharacterSprites.Get(roleColor, facing, frame % CharacterArt.Frames);
        }
    }
}
