using System.Collections.Generic;
using Onyx.Core;
using Onyx.Core.Art;
using Onyx.Core.CrashSite;
using UnityEngine;

namespace Onyx.Game.CrashSite
{
    /// <summary>
    /// Macht aus den Pixelbildern der Kernlogik (<see cref="StationArt"/>) Unity-Sprites und hält
    /// sie vor. Den Boden malt <see cref="CrashSiteArt"/>, die Figuren <see cref="CharacterArt"/>.
    /// </summary>
    public static class SiteTileArt
    {
        private static readonly Dictionary<(StationKind, bool), Sprite> StationCache = new Dictionary<(StationKind, bool), Sprite>();
        private static Sprite shadow;

        /// <summary>Konsole einer Reparaturstelle (16 × 24, Pivot am Fuß).</summary>
        public static Sprite Station(StationKind kind, bool repaired)
        {
            if (StationCache.TryGetValue((kind, repaired), out var cached) && cached != null)
            {
                return cached;
            }

            var sprite = ToSprite(StationArt.Render(kind, repaired), new Vector2(0.5f, StationArt.BaseRow / (float)StationArt.Height), $"Station {kind} {(repaired ? "ok" : "kaputt")}");
            StationCache[(kind, repaired)] = sprite;
            return sprite;
        }

        /// <summary>Weicher ovaler Schatten unter Figuren (12 × 5 px, Pivot in der Mitte).</summary>
        public static Sprite Shadow()
        {
            if (shadow != null)
            {
                return shadow;
            }

            var img = new PixelImage(12, 5);
            for (int y = 0; y < 5; y++)
            {
                for (int x = 0; x < 12; x++)
                {
                    float dx = (x - 5.5f) / 6f, dy = (y - 2f) / 2.5f;
                    float d = dx * dx + dy * dy;
                    if (d <= 1f)
                    {
                        img.Set(x, y, d < 0.5f ? 0x73000000u : 0x40000000u);
                    }
                }
            }

            shadow = ToSprite(img, new Vector2(0.5f, 0.5f), "Schatten");
            return shadow;
        }

        public static Sprite ToSprite(PixelImage image, Vector2 pivot, string name)
        {
            var texture = new Texture2D(image.Width, image.Height, TextureFormat.RGBA32, false)
            {
                name = name,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave,
            };

            var colors = new Color32[image.Pixels.Length];
            for (int i = 0; i < colors.Length; i++)
            {
                uint c = image.Pixels[i];
                colors[i] = new Color32((byte)(c >> 16), (byte)(c >> 8), (byte)c, (byte)(c >> 24));
            }

            texture.SetPixels32(colors);
            texture.Apply(false, true);
            var sprite = Sprite.Create(texture, new Rect(0, 0, image.Width, image.Height), pivot, GameConstants.PixelsPerUnit);
            sprite.name = name;
            return sprite;
        }
    }
}
