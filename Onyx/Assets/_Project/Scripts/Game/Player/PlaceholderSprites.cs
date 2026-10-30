using System.Collections.Generic;
using Onyx.Core;
using UnityEngine;

namespace Onyx.Game.Player
{
    /// <summary>
    /// Erzeugt Platzhalter-Grafiken zur Laufzeit, bis echte Pixelart vorliegt.
    /// Formate wie im GDD: Figuren 16 × 32 px, Kacheln 16 × 16 px.
    /// </summary>
    public static class PlaceholderSprites
    {
        // 16 × 32, oberste Zeile zuerst. V/R = Rollenfarbe.
        private static readonly string[] CharacterTemplate =
        {
            "................",
            "................",
            "................",
            "................",
            ".....kkkkkk.....",
            "....kSSSSSSk....",
            "...kSSSSSShSk...",
            "...kSSSSSSSSk...",
            "...kSVVVVVVSk...",
            "...kSVvVVVVSk...",
            "...kSVVVVVVSk...",
            "...kbSSSSSSbk...",
            "....kbbbbbbk....",
            "...kkkddddkkk...",
            "..kBBBBBBBBBBk..",
            ".kBBBBRRRRBBBBk.",
            ".kbBBBRRRRBBBbk.",
            ".kbBBBBBBBBBBbk.",
            ".kgbBBBBBBBBbgk.",
            ".kgkBBBBBBBBkgk.",
            ".kk.kBddddBk.kk.",
            "....kBBBBBBk....",
            "....kbBBBBbk....",
            "....kbBkkBbk....",
            "....kbBkkBbk....",
            "....kbBkkBbk....",
            "....kbBkkBbk....",
            "...kgggkkgggk...",
            "...kkkkkkkkkk...",
            "................",
            "................",
            "................",
        };

        // Unterste Pixelzeile der Füße (von unten gezählt), damit der Pivot auf dem Boden steht.
        private const int FeetRowFromBottom = 3;

        private static readonly Dictionary<Color, Sprite> CharacterCache = new Dictionary<Color, Sprite>();

        public static Sprite Character(Color roleColor)
        {
            if (CharacterCache.TryGetValue(roleColor, out var cached) && cached != null)
            {
                return cached;
            }

            var palette = new Dictionary<char, Color32>
            {
                ['k'] = new Color32(0x07, 0x08, 0x0F, 0xFF),
                ['S'] = new Color32(0xB4, 0xBC, 0xCC, 0xFF),
                ['h'] = new Color32(0xE8, 0xE4, 0xD8, 0xFF),
                ['v'] = new Color32(0xE8, 0xE4, 0xD8, 0xFF),
                ['b'] = new Color32(0x4A, 0x55, 0x73, 0xFF),
                ['B'] = new Color32(0x7A, 0x84, 0x9E, 0xFF),
                ['d'] = new Color32(0x2C, 0x36, 0x54, 0xFF),
                ['g'] = new Color32(0x1B, 0x22, 0x38, 0xFF),
                ['V'] = roleColor,
                ['R'] = roleColor,
            };

            int w = GameConstants.CharacterWidthPx;
            int h = GameConstants.CharacterHeightPx;
            var texture = NewTexture(w, h, "Placeholder Character");
            var pixels = new Color32[w * h];
            for (int row = 0; row < h; row++)
            {
                string line = CharacterTemplate[row];
                for (int x = 0; x < w; x++)
                {
                    // Texturen beginnen unten, die Vorlage oben.
                    int index = (h - 1 - row) * w + x;
                    pixels[index] = palette.TryGetValue(line[x], out var c) ? c : new Color32(0, 0, 0, 0);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);

            var pivot = new Vector2(0.5f, FeetRowFromBottom / (float)h);
            var sprite = Sprite.Create(texture, new Rect(0, 0, w, h), pivot, GameConstants.PixelsPerUnit);
            sprite.name = texture.name;
            CharacterCache[roleColor] = sprite;
            return sprite;
        }

        /// <summary>Rostiger Sandboden mit leichter Körnung. Gleiche Variante = gleiches Bild.</summary>
        public static Sprite GroundTile(int variant)
        {
            Color32[] tones =
            {
                new Color32(0x5A, 0x24, 0x30, 0xFF),
                new Color32(0x82, 0x32, 0x2E, 0xFF),
                new Color32(0xB0, 0x48, 0x2C, 0xFF),
                new Color32(0xD8, 0x69, 0x2E, 0xFF),
            };

            int size = GameConstants.TileSizePx;
            var random = new System.Random(1000 + variant);
            int baseTone = 1 + variant % 2;
            var texture = NewTexture(size, size, $"Placeholder Ground {variant}");
            var pixels = new Color32[size * size];
            for (int i = 0; i < pixels.Length; i++)
            {
                double roll = random.NextDouble();
                int tone = roll < 0.08 ? baseTone - 1 : roll > 0.94 ? baseTone + 1 : baseTone;
                pixels[i] = tones[tone];
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            var sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), GameConstants.PixelsPerUnit);
            sprite.name = texture.name;
            return sprite;
        }

        private static Texture2D NewTexture(int width, int height, string name)
        {
            return new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = name,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave,
            };
        }
    }
}
