using System.Collections.Generic;
using Onyx.Core;
using Onyx.Core.CrashSite;
using UnityEngine;

namespace Onyx.Game.CrashSite
{
    /// <summary>
    /// Pixelgrafik der Reparatur-Konsolen, zur Laufzeit erzeugt. Palette aus der Art Direction
    /// „Rost &amp; Signal“. Den Boden malt <see cref="Onyx.Core.Art.CrashSiteArt"/>.
    /// </summary>
    public static class SiteTileArt
    {
        private static readonly Color32 Clear = new Color32(0, 0, 0, 0);
        private static readonly Color32 Void0 = Hex(0x07080F);
        private static readonly Color32 Ink = Hex(0x1B2238);
        private static readonly Color32 Void3 = Hex(0x2C3654);
        private static readonly Color32 Steel1 = Hex(0x4A5573);
        private static readonly Color32 Steel2 = Hex(0x7A849E);
        private static readonly Color32 Steel3 = Hex(0xB4BCCC);
        private static readonly Color32 Amber = Hex(0xFFB52E);
        private static readonly Color32 Red = Hex(0xFF3D5A);
        private static readonly Color32 Cyan = Hex(0x39D5E6);

        private static readonly Dictionary<(StationKind, bool), Sprite> StationCache = new Dictionary<(StationKind, bool), Sprite>();

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
