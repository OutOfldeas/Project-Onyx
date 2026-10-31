using System.Collections.Generic;
using Onyx.Core;
using Onyx.Core.CrashSite;
using UnityEngine;

namespace Onyx.Game.CrashSite
{
    /// <summary>
    /// Platzhalter-Pixelgrafik für die Absturzstelle, zur Laufzeit erzeugt (16 × 16 px je Kachel).
    /// Palette aus der Art Direction „Rost &amp; Signal“. Wird ersetzt, sobald ein echtes Tileset da ist.
    /// </summary>
    public static class SiteTileArt
    {
        public const int Variants = 4;

        private static readonly Color32 Clear = new Color32(0, 0, 0, 0);
        private static readonly Color32 Void0 = Hex(0x07080F);
        private static readonly Color32 Ink = Hex(0x1B2238);
        private static readonly Color32 Void3 = Hex(0x2C3654);
        private static readonly Color32 Smog = Hex(0x3A2E3A);
        private static readonly Color32 Rust0 = Hex(0x5A2430);
        private static readonly Color32 Rust1 = Hex(0x82322E);
        private static readonly Color32 Rust2 = Hex(0xB0482C);
        private static readonly Color32 Rust3 = Hex(0xD8692E);
        private static readonly Color32 Steel1 = Hex(0x4A5573);
        private static readonly Color32 Steel2 = Hex(0x7A849E);
        private static readonly Color32 Steel3 = Hex(0xB4BCCC);
        private static readonly Color32 Amber = Hex(0xFFB52E);
        private static readonly Color32 Red = Hex(0xFF3D5A);
        private static readonly Color32 Cyan = Hex(0x39D5E6);

        private static readonly Dictionary<(SiteTile, int), Sprite> TileCache = new Dictionary<(SiteTile, int), Sprite>();
        private static readonly Dictionary<(StationKind, bool), Sprite> StationCache = new Dictionary<(StationKind, bool), Sprite>();

        public static Sprite Tile(SiteTile tile, int variant)
        {
            variant = Mathf.Abs(variant) % Variants;
            if (TileCache.TryGetValue((tile, variant), out var cached) && cached != null)
            {
                return cached;
            }

            var px = new Color32[16 * 16];
            int seed = (int)tile * 97 + variant * 13 + 5;
            switch (tile)
            {
                case SiteTile.Sand: Sand(px, seed, Rust2, Rust3, Rust1); break;
                case SiteTile.SandDark: Sand(px, seed, Rust1, Rust2, Rust0); break;
                case SiteTile.Gravel: Gravel(px, seed); break;
                case SiteTile.Scrap: Sand(px, seed, Rust2, Rust3, Rust1); Scrap(px, seed); break;
                case SiteTile.Rock: Rock(px, seed); break;
                case SiteTile.Boulder: Sand(px, seed, Rust2, Rust3, Rust1); Boulder(px, seed); break;
                case SiteTile.PodFloor: Plate(px, seed, Steel2, Steel1, Steel3, 8); break;
                case SiteTile.PodWall: Wall(px, seed, Steel3, Steel2, Amber); break;
                case SiteTile.WreckFloor:
                case SiteTile.Station: Plate(px, seed, Steel1, Void3, Steel2, 16); break;
                case SiteTile.WreckWall: Wall(px, seed, Void3, Ink, variant == 0 ? Amber : Steel2); break;
                case SiteTile.WreckDebris: Plate(px, seed, Steel1, Void3, Steel2, 16); Debris(px, seed); break;
            }

            var sprite = MakeSprite(px, 16, 16, new Vector2(0.5f, 0.5f), $"Site {tile} {variant}");
            TileCache[(tile, variant)] = sprite;
            return sprite;
        }

        /// <summary>Konsole einer Reparaturstelle (16 × 24, Pivot unten). Bildschirm rot = kaputt, cyan = läuft.</summary>
        public static Sprite Station(StationKind kind, bool repaired)
        {
            if (StationCache.TryGetValue((kind, repaired), out var cached) && cached != null)
            {
                return cached;
            }

            string[] art =
            {
                "................",
                "...kkkkkkkkkk...",
                "..kSSSSSSSSSSk..",
                "..kSkkkkkkkkSk..",
                "..kSkLLLLLLkSk..",
                "..kSkLlLLLLkSk..",
                "..kSkLLLLLLkSk..",
                "..kSkkkkkkkkSk..",
                "..kSSSSSSSSSSk..",
                ".kBBBBBBBBBBBBk.",
                ".kBaBBBBBBBBaBk.",
                ".kbbbbbbbbbbbbk.",
                ".kbIIbbbbbbIIbk.",
                ".kbIIbbbbbbIIbk.",
                ".kbbbbXXXXbbbbk.",
                ".kbbbbXxxXbbbbk.",
                ".kbbbbXXXXbbbbk.",
                ".kbbbbbbbbbbbbk.",
                ".kBBBBBBBBBBBBk.",
                ".kddddddddddddk.",
                ".kkkkkkkkkkkkkk.",
                "..g..........g..",
                "................",
                "................",
            };

            Color32 screen = repaired ? Cyan : Red;
            Color32 symbol = kind == StationKind.OxygenGenerator ? Cyan : kind == StationKind.Reactor ? Amber : Steel3;
            var palette = new Dictionary<char, Color32>
            {
                ['k'] = Void0, ['S'] = Steel2, ['B'] = Steel3, ['b'] = Steel1, ['d'] = Void3, ['g'] = Ink,
                ['L'] = Shade(screen, 0.55f), ['l'] = screen, ['a'] = repaired ? Cyan : Amber,
                ['I'] = Void3, ['X'] = Ink, ['x'] = symbol,
            };

            var px = FromArt(art, palette);
            var sprite = MakeSprite(px, 16, 24, new Vector2(0.5f, 2f / 24f), $"Station {kind} {(repaired ? "ok" : "kaputt")}");
            StationCache[(kind, repaired)] = sprite;
            return sprite;
        }

        // ------------------------------------------------------------------ Muster

        private static void Sand(Color32[] px, int seed, Color32 baseColor, Color32 light, Color32 dark)
        {
            for (int y = 0; y < 16; y++)
            {
                for (int x = 0; x < 16; x++)
                {
                    float r = Noise.Hash01(x, y, seed);
                    px[y * 16 + x] = r < 0.07f ? dark : r > 0.95f ? light : baseColor;
                }
            }

            // eine Düne als helle Welle
            int dy = (int)(Noise.Hash01(0, 99, seed) * 12) + 2;
            for (int x = 2; x < 13; x++)
            {
                int y = dy + (x % 6 < 3 ? 0 : 1);
                if (Noise.Hash01(x, 7, seed) < 0.75f) px[y * 16 + x] = light;
            }
        }

        private static void Gravel(Color32[] px, int seed)
        {
            Sand(px, seed, Rust1, Rust2, Rust0);
            for (int i = 0; i < 7; i++)
            {
                int x = (int)(Noise.Hash01(i, 1, seed) * 14) + 1;
                int y = (int)(Noise.Hash01(i, 2, seed) * 14) + 1;
                px[y * 16 + x] = Steel1;
                px[y * 16 + x + 1] = Smog;
                if (y + 1 < 16) px[(y + 1) * 16 + x] = Steel2;
            }
        }

        private static void Rock(Color32[] px, int seed)
        {
            for (int y = 0; y < 16; y++)
            {
                for (int x = 0; x < 16; x++)
                {
                    float r = Noise.Hash01(x, y, seed);
                    Color32 c = r < 0.15f ? Void0 : r > 0.85f ? Rust0 : Smog;
                    if (y >= 13) c = r < 0.5f ? Rust0 : Smog;      // obere Kante heller (Licht von oben)
                    if (y <= 1) c = Void0;                          // Schattenkante unten
                    px[y * 16 + x] = c;
                }
            }

            // Riss
            int cx = (int)(Noise.Hash01(3, 3, seed) * 10) + 3;
            for (int y = 3; y < 12; y++)
            {
                cx += Noise.Hash01(y, 4, seed) < 0.5f ? 0 : (Noise.Hash01(y, 5, seed) < 0.5f ? -1 : 1);
                cx = Mathf.Clamp(cx, 1, 14);
                px[y * 16 + cx] = Void0;
            }
        }

        private static void Boulder(Color32[] px, int seed)
        {
            for (int y = 0; y < 16; y++)
            {
                for (int x = 0; x < 16; x++)
                {
                    float dx = (x - 7.5f) / 6.5f;
                    float dy = (y - 7f) / 5.5f;
                    float d = dx * dx + dy * dy;
                    if (d > 1f)
                    {
                        if (y == 1 && Mathf.Abs(x - 7.5f) < 6f) px[y * 16 + x] = Rust0; // Schatten
                        continue;
                    }

                    float r = Noise.Hash01(x, y, seed + 1);
                    Color32 c = dy > 0.3f ? Rust0 : Smog;
                    if (r < 0.12f) c = Void0;
                    if (d > 0.8f) c = Void0;
                    px[y * 16 + x] = c;
                }
            }
        }

        private static void Scrap(Color32[] px, int seed)
        {
            int ox = (int)(Noise.Hash01(1, 1, seed) * 8) + 2;
            int oy = (int)(Noise.Hash01(2, 2, seed) * 8) + 2;
            for (int i = 0; i < 5; i++)
            {
                int x = ox + i;
                int y = oy + (i % 2);
                if (x < 16 && y < 15)
                {
                    px[y * 16 + x] = Steel2;
                    px[(y + 1) * 16 + x] = Steel3;
                    if (y > 0) px[(y - 1) * 16 + x] = Rust0;
                }
            }

            px[(oy + 2) * 16 + ox + 1] = Steel1;
        }

        private static void Plate(Color32[] px, int seed, Color32 baseColor, Color32 seam, Color32 rivet, int cell)
        {
            for (int y = 0; y < 16; y++)
            {
                for (int x = 0; x < 16; x++)
                {
                    bool line = x % cell == 0 || y % cell == 0;
                    float r = Noise.Hash01(x, y, seed);
                    Color32 c = line ? seam : baseColor;
                    if (!line && r < 0.04f) c = seam; // Kratzer
                    px[y * 16 + x] = c;
                }
            }

            px[2 * 16 + 2] = rivet;
            px[2 * 16 + 13] = rivet;
            px[13 * 16 + 2] = rivet;
            px[13 * 16 + 13] = rivet;
        }

        private static void Wall(Color32[] px, int seed, Color32 face, Color32 dark, Color32 stripe)
        {
            for (int y = 0; y < 16; y++)
            {
                for (int x = 0; x < 16; x++)
                {
                    Color32 c = face;
                    if (y >= 13) c = Steel3;                                  // Oberkante
                    else if (y <= 2) c = dark;                                // Sockel
                    else if (y >= 6 && y <= 8 && (x + y) % 6 < 3) c = stripe; // Warnstreifen
                    if (Noise.Hash01(x, y, seed) < 0.05f) c = dark;
                    px[y * 16 + x] = c;
                }
            }
        }

        private static void Debris(Color32[] px, int seed)
        {
            for (int y = 2; y < 13; y++)
            {
                for (int x = 2; x < 14; x++)
                {
                    float r = Noise.Value(x / 3f, y / 3f, seed);
                    if (r > 0.55f) px[y * 16 + x] = r > 0.75f ? Steel3 : Ink;
                }
            }

            px[3 * 16 + 4] = Rust2; // Rost
            px[9 * 16 + 11] = Rust1;
        }

        // ------------------------------------------------------------------ Hilfen

        private static Color32[] FromArt(string[] art, Dictionary<char, Color32> palette)
        {
            int h = art.Length;
            int w = art[0].Length;
            var px = new Color32[w * h];
            for (int row = 0; row < h; row++)
            {
                for (int x = 0; x < w; x++)
                {
                    px[(h - 1 - row) * w + x] = palette.TryGetValue(art[row][x], out var c) ? c : Clear;
                }
            }

            return px;
        }

        private static Sprite MakeSprite(Color32[] px, int w, int h, Vector2 pivot, string name)
        {
            var texture = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                name = name,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave,
            };
            texture.SetPixels32(px);
            texture.Apply(false, true);
            var sprite = Sprite.Create(texture, new Rect(0, 0, w, h), pivot, GameConstants.PixelsPerUnit);
            sprite.name = name;
            return sprite;
        }

        private static Color32 Shade(Color32 c, float f) =>
            new Color32((byte)(c.r * f), (byte)(c.g * f), (byte)(c.b * f), 255);

        private static Color32 Hex(uint rgb) => new Color32((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 255);
    }
}
