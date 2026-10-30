using Onyx.Game.Player;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Onyx.Game.World
{
    /// <summary>
    /// Füllt eine Tilemap mit Platzhalter-Boden. Das Muster ist fest vorgegeben (Seed),
    /// deshalb sieht der Boden bei allen Spielern gleich aus, ohne übers Netz zu gehen.
    /// </summary>
    [RequireComponent(typeof(Tilemap))]
    public sealed class PlaceholderGround : MonoBehaviour
    {
        [SerializeField] private Vector2Int size = new Vector2Int(48, 30);
        [SerializeField] private int seed = 7;

        private const int Variants = 4;

        private void Start()
        {
            var tiles = new Tile[Variants];
            for (int i = 0; i < Variants; i++)
            {
                tiles[i] = ScriptableObject.CreateInstance<Tile>();
                tiles[i].sprite = PlaceholderSprites.GroundTile(i);
                tiles[i].hideFlags = HideFlags.DontSave;
            }

            var tilemap = GetComponent<Tilemap>();
            tilemap.ClearAllTiles();
            var random = new System.Random(seed);
            int halfW = size.x / 2;
            int halfH = size.y / 2;
            for (int y = -halfH; y < size.y - halfH; y++)
            {
                for (int x = -halfW; x < size.x - halfW; x++)
                {
                    tilemap.SetTile(new Vector3Int(x, y, 0), tiles[random.Next(Variants)]);
                }
            }
        }
    }
}
