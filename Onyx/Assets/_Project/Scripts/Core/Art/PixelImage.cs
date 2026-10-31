using System;

namespace Onyx.Core.Art
{
    /// <summary>
    /// Ein Pixelbild als Farbwerte 0xAARRGGBB. Zeile 0 ist <b>unten</b> – wie bei Unity-Texturen,
    /// damit das Spiel die Pixel ohne Umdrehen übernehmen kann.
    /// </summary>
    public sealed class PixelImage
    {
        public int Width { get; }
        public int Height { get; }
        public uint[] Pixels { get; }

        public PixelImage(int width, int height)
        {
            if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(width));
            Width = width;
            Height = height;
            Pixels = new uint[width * height];
        }

        public bool Contains(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;

        public uint Get(int x, int y) => Contains(x, y) ? Pixels[y * Width + x] : 0u;

        public void Set(int x, int y, uint argb)
        {
            if (Contains(x, y)) Pixels[y * Width + x] = argb;
        }

        public void Fill(int x, int y, int w, int h, uint argb)
        {
            for (int yy = y; yy < y + h; yy++)
                for (int xx = x; xx < x + w; xx++)
                    Set(xx, yy, argb);
        }

        /// <summary>Dunkelt einen Pixel ab (0 = unverändert, 1 = schwarz).</summary>
        public void Darken(int x, int y, float amount)
        {
            if (!Contains(x, y)) return;
            uint c = Pixels[y * Width + x];
            float f = 1f - amount;
            uint r = (uint)(((c >> 16) & 0xFF) * f);
            uint g = (uint)(((c >> 8) & 0xFF) * f);
            uint b = (uint)((c & 0xFF) * f);
            Pixels[y * Width + x] = (c & 0xFF000000) | (r << 16) | (g << 8) | b;
        }

        /// <summary>
        /// Zeichnet eine Vorlage aus Zeichen (oberste Zeile zuerst); Zeichen ohne Eintrag in der
        /// Palette bleiben durchsichtig. <paramref name="x"/>/<paramref name="y"/> = linke untere Ecke.
        /// </summary>
        public void Stamp(string[] rows, int x, int y, Func<char, uint?> palette, bool flipX = false)
        {
            int h = rows.Length;
            for (int r = 0; r < h; r++)
            {
                string row = rows[r];
                for (int c = 0; c < row.Length; c++)
                {
                    uint? color = palette(row[c]);
                    if (color.HasValue)
                    {
                        int px = flipX ? x + row.Length - 1 - c : x + c;
                        Set(px, y + h - 1 - r, color.Value);
                    }
                }
            }
        }

        /// <summary>Bayer-4×4-Schwellwert in [0, 1) für gerasterte Übergänge.</summary>
        public static float Bayer(int x, int y) => BayerMatrix[(y & 3) * 4 + (x & 3)];

        private static readonly float[] BayerMatrix =
        {
            0f / 16, 8f / 16, 2f / 16, 10f / 16,
            12f / 16, 4f / 16, 14f / 16, 6f / 16,
            3f / 16, 11f / 16, 1f / 16, 9f / 16,
            15f / 16, 7f / 16, 13f / 16, 5f / 16,
        };
    }

    /// <summary>Palette „Rost &amp; Signal“ aus der Art Direction (0xAARRGGBB, voll deckend).</summary>
    public static class Rust
    {
        // Rostwüste, dunkel nach hell
        public const uint R0 = 0xFF0B0710, R1 = 0xFF170D18, R2 = 0xFF24121F, R3 = 0xFF3A1A26, R4 = 0xFF5A2430,
            R5 = 0xFF82322E, R6 = 0xFFB0482C, R7 = 0xFFD8692E, R8 = 0xFFF09A45, R9 = 0xFFFFD08A;

        // Stahl, dunkel nach hell
        public const uint S0 = 0xFF07080F, S1 = 0xFF1B2238, S2 = 0xFF2C3654, S3 = 0xFF4A5573, S4 = 0xFF7A849E, S5 = 0xFFB4BCCC, Bone = 0xFFE8E4D8;

        // Signalfarben
        public const uint Amber = 0xFFFFB52E, Cyan = 0xFF39D5E6, CyanDark = 0xFF1A6F86, Lime = 0xFFA6D84A, Red = 0xFFFF3D5A;

        public const uint Clear = 0x00000000;
    }
}
