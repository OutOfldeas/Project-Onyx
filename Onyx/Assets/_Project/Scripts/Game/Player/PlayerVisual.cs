using Unity.Netcode;
using UnityEngine;

namespace Onyx.Game.Player
{
    /// <summary>
    /// Zeigt die Spielfigur als Platzhalter-Sprite in der Farbe des Spielers.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class PlayerVisual : NetworkBehaviour
    {
        private SpriteRenderer spriteRenderer;

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            spriteRenderer.spriteSortPoint = SpriteSortPoint.Pivot;
        }

        public override void OnNetworkSpawn()
        {
            spriteRenderer.sprite = PlaceholderSprites.Character(RoleColors.ForSlot(OwnerClientId));
        }
    }
}
