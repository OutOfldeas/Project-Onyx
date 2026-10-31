using System;

namespace Onyx.Core
{
    /// <summary>Deterministisches Rauschen für erzeugte Inhalte (Karten, Gelände).</summary>
    public static class Noise
    {
        /// <summary>Glatter Wertrauschen-Wert in [0, 1]. Gleiche Eingabe = gleiches Ergebnis auf allen Rechnern.</summary>
        public static float Value(float x, float y, int seed)
        {
            int x0 = (int)Math.Floor(x);
            int y0 = (int)Math.Floor(y);
            float tx = x - x0;
            float ty = y - y0;
            tx = tx * tx * (3f - 2f * tx);
            ty = ty * ty * (3f - 2f * ty);

            float a = Hash01(x0, y0, seed);
            float b = Hash01(x0 + 1, y0, seed);
            float c = Hash01(x0, y0 + 1, seed);
            float d = Hash01(x0 + 1, y0 + 1, seed);
            float top = a + (b - a) * tx;
            float bottom = c + (d - c) * tx;
            return top + (bottom - top) * ty;
        }

        /// <summary>Zufallswert in [0, 1) für eine Gitterzelle.</summary>
        public static float Hash01(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + seed * 1442695041);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / (float)0x1000000;
            }
        }
    }
}
