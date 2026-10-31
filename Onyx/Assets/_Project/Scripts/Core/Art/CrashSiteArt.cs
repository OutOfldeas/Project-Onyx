using System;
using Onyx.Core.CrashSite;

namespace Onyx.Core.Art
{
    /// <summary>
    /// Malt die Absturzstelle als ein großes Pixelbild (16 px je Kachel) im Stil der Art Direction:
    /// Rostwüste mit großen Farbflächen und Raster-Übergängen, Dünenrippeln, Kiesel, Brandspur
    /// der Fregatte, Metallböden, Felsen und Wände in 3/4-Ansicht (Oberseite + Vorderkante) mit
    /// Schlagschatten. Deterministisch: gleicher Seed = gleiches Bild auf allen Rechnern.
    /// Stationen, Figuren und Lichter zeichnet das Spiel darüber.
    /// </summary>
    public static class CrashSiteArt
    {
        public const int Tile = 16;
        private const int RockFace = 6;
        private const int WallFace = 5;
        private const int ShadowX = 3;
        private const int ShadowY = 4;

        public static PixelImage Render(CrashSiteLayout layout)
        {
            if (layout == null) throw new ArgumentNullException(nameof(layout));
            var img = new PixelImage(layout.Width * Tile, layout.Height * Tile);
            int seed = layout.Seed;

            for (int ty = 0; ty < layout.Height; ty++)
            {
                for (int tx = 0; tx < layout.Width; tx++)
                {
                    DrawGround(img, layout, tx, ty, seed);
                }
            }

            DrawScorch(img, layout, seed);

            for (int ty = 0; ty < layout.Height; ty++)
            {
                for (int tx = 0; tx < layout.Width; tx++)
                {
                    DrawGroundDetail(img, layout, tx, ty, seed);
                }
            }

            for (int ty = 0; ty < layout.Height; ty++)
            {
                for (int tx = 0; tx < layout.Width; tx++)
                {
                    SiteTile t = layout.Get(tx, ty);
                    if (t == SiteTile.Rock || t == SiteTile.PodWall || t == SiteTile.WreckWall) DrawShadow(img, layout, tx, ty);
                    else if (t == SiteTile.Boulder || t == SiteTile.WreckDebris) DrawRoundShadow(img, tx, ty);
                }
            }

            for (int ty = layout.Height - 1; ty >= 0; ty--)
            {
                for (int tx = 0; tx < layout.Width; tx++)
                {
                    switch (layout.Get(tx, ty))
                    {
                        case SiteTile.Rock: DrawBlock(img, layout, tx, ty, seed, BlockStyle.Rock); break;
                        case SiteTile.PodWall: DrawBlock(img, layout, tx, ty, seed, BlockStyle.Pod); break;
                        case SiteTile.WreckWall: DrawBlock(img, layout, tx, ty, seed, BlockStyle.Wreck); break;
                        case SiteTile.Boulder: DrawBoulder(img, tx, ty, seed); break;
                        case SiteTile.WreckDebris: DrawDebris(img, tx, ty, seed); break;
                    }
                }
            }

            return img;
        }

        private static bool IsTall(SiteTile t) =>
            t == SiteTile.Rock || t == SiteTile.PodWall || t == SiteTile.WreckWall || t == SiteTile.Boulder || t == SiteTile.WreckDebris;

        private static bool IsFloor(SiteTile t) => t == SiteTile.PodFloor || t == SiteTile.WreckFloor || t == SiteTile.Station || t == SiteTile.WreckDebris;

        private static float Hash(int x, int y, int seed) => Noise.Hash01(x, y, seed);

        // ------------------------------------------------------------------ Boden

        private static void DrawGround(PixelImage img, CrashSiteLayout layout, int tx, int ty, int seed)
        {
            SiteTile tile = layout.Get(tx, ty);
            int x0 = tx * Tile;
            int y0 = ty * Tile;

            if (tile == SiteTile.PodFloor)
            {
                for (int j = 0; j < Tile; j++)
                    for (int i = 0; i < Tile; i++)
                    {
                        bool seam = (x0 + i) % 8 == 0 || (y0 + j) % 8 == 0;
                        uint c = seam ? Rust.S2 : Rust.S3;
                        if (!seam && (i % 8 == 2 && j % 8 == 2)) c = Rust.S4;
                        img.Set(x0 + i, y0 + j, c);
                    }

                return;
            }

            if (IsFloor(tile))
            {
                bool lighter = Hash(tx, ty, seed + 5) < 0.18f;
                for (int j = 0; j < Tile; j++)
                    for (int i = 0; i < Tile; i++)
                    {
                        int px = x0 + i, py = y0 + j;
                        uint c = lighter ? Rust.S3 : Rust.S2;
                        if (i == 0 || j == 0) c = Rust.S1;
                        else if (i == 15 || j == 15) c = lighter ? Rust.S4 : Rust.S3;
                        float grime = Noise.Value(px / 9f, py / 9f, seed + 9) + (PixelImage.Bayer(px, py) - 0.5f) * 0.1f;
                        if (grime > 0.77f && i > 0 && j > 0) c = grime > 0.9f ? Rust.R3 : Rust.S1;
                        if (Hash(px, py, seed + 11) < 0.02f) c = Rust.S1;
                        img.Set(px, py, c);
                    }

                if (Hash(tx, ty, seed + 6) < 0.5f)
                {
                    img.Set(x0 + 3, y0 + 12, Rust.S4);
                    img.Set(x0 + 12, y0 + 12, Rust.S4);
                    img.Set(x0 + 3, y0 + 3, Rust.S4);
                    img.Set(x0 + 12, y0 + 3, Rust.S4);
                }

                return;
            }

            float bias = tile == SiteTile.SandDark ? -0.16f : tile == SiteTile.Gravel ? -0.08f : 0f;
            for (int j = 0; j < Tile; j++)
                for (int i = 0; i < Tile; i++)
                {
                    int px = x0 + i, py = y0 + j;
                    float n = 0.72f * Noise.Value(px / 56f, py / 56f, seed + 21) + 0.28f * Noise.Value(px / 9f, py / 9f, seed + 22) + bias;
                    float v = n + (PixelImage.Bayer(px, py) - 0.5f) * 0.12f;
                    uint c = v < 0.36f ? Rust.R4 : v < 0.62f ? Rust.R5 : Rust.R6;
                    img.Set(px, py, c);
                }
        }

        private static void DrawGroundDetail(PixelImage img, CrashSiteLayout layout, int tx, int ty, int seed)
        {
            SiteTile tile = layout.Get(tx, ty);
            if (tile != SiteTile.Sand && tile != SiteTile.SandDark && tile != SiteTile.Gravel && tile != SiteTile.Scrap && tile != SiteTile.Boulder)
            {
                return;
            }

            int x0 = tx * Tile;
            int y0 = ty * Tile;

            // Dünenrippel
            if (Hash(tx, ty, seed + 31) < 0.32f && tile != SiteTile.Gravel)
            {
                int length = 6 + (int)(Hash(tx, ty, seed + 32) * 6);
                int sx = x0 + (int)(Hash(tx, ty, seed + 33) * (Tile - length));
                int sy = y0 + 3 + (int)(Hash(tx, ty, seed + 34) * 10);
                for (int k = 0; k < length; k++)
                {
                    int yy = sy + (k % 6 < 3 ? 0 : 1);
                    img.Set(sx + k, yy, k % 3 == 0 ? Rust.R5 : Rust.R7);
                }
            }

            // Kiesel
            int pebbles = tile == SiteTile.Gravel ? 4 : Hash(tx, ty, seed + 35) < 0.3f ? 1 : 0;
            for (int p = 0; p < pebbles; p++)
            {
                int px = x0 + 1 + (int)(Hash(tx * 7 + p, ty, seed + 36) * 13);
                int py = y0 + 1 + (int)(Hash(tx, ty * 7 + p, seed + 37) * 13);
                img.Set(px, py, Rust.R3);
                img.Set(px + 1, py, Rust.R3);
                img.Set(px, py + 1, tile == SiteTile.Gravel ? Rust.S3 : Rust.R6);
            }

            // Wrackteile der Absturzspur
            if (tile == SiteTile.Scrap)
            {
                int ox = x0 + 3 + (int)(Hash(tx, ty, seed + 38) * 6);
                int oy = y0 + 4 + (int)(Hash(tx, ty, seed + 39) * 6);
                for (int k = 0; k < 6; k++)
                {
                    int yy = oy + (k < 3 ? k / 2 : 2 - (k - 3) / 2);
                    img.Set(ox + k, yy - 1, Rust.R2); // Schatten
                    img.Set(ox + k, yy, k % 2 == 0 ? Rust.S3 : Rust.S4);
                    img.Set(ox + k, yy + 1, k == 2 ? Rust.S5 : Rust.S4);
                }

                img.Set(ox + 2, oy + 2, Rust.S5);
            }
        }

        /// <summary>Dunkle, versengte Schleifspur von der Fregatte Richtung Kapsel.</summary>
        private static void DrawScorch(PixelImage img, CrashSiteLayout layout, int seed)
        {
            int startX = (layout.OriginX + 3) * Tile;
            int endX = layout.WreckInterior.MinX * Tile;
            float centerY = (layout.OriginY + 0.5f) * Tile;
            for (int py = (int)(centerY - 5 * Tile); py < centerY + 5 * Tile; py++)
            {
                for (int px = startX; px < endX; px++)
                {
                    var (tx, ty) = (px / Tile, py / Tile);
                    SiteTile tile = layout.Get(tx, ty);
                    if (tile != SiteTile.Sand && tile != SiteTile.SandDark && tile != SiteTile.Gravel && tile != SiteTile.Scrap) continue;

                    float along = (px - startX) / (float)(endX - startX);            // 0 Kapsel … 1 Wrack
                    float width = 1.2f + along * 2.6f;                                 // Spur wird zum Wrack hin breiter
                    float d = Math.Abs(py - centerY + (float)Math.Sin(px / 40f) * 6f) / (width * Tile);
                    float s = (1f - d) * (0.35f + 0.65f * along) * (0.6f + 0.6f * Noise.Value(px / 10f, py / 10f, seed + 41));
                    s += (PixelImage.Bayer(px, py) - 0.5f) * 0.2f;
                    if (s > 0.62f) img.Set(px, py, Rust.R2);
                    else if (s > 0.42f) img.Set(px, py, Rust.R3);
                    else if (s > 0.3f && img.Get(px, py) == Rust.R6) img.Set(px, py, Rust.R5);
                }
            }
        }

        private static void DrawShadow(PixelImage img, CrashSiteLayout layout, int tx, int ty)
        {
            int x0 = tx * Tile;
            int y0 = ty * Tile;
            for (int j = 0; j < Tile; j++)
                for (int i = 0; i < Tile; i++)
                {
                    int px = x0 + i + ShadowX;
                    int py = y0 + j - ShadowY;
                    if (px >= 0 && py >= 0 && !IsTall(layout.Get(px / Tile, py / Tile)))
                    {
                        img.Darken(px, py, 0.38f);
                    }
                }
        }

        private static void DrawRoundShadow(PixelImage img, int tx, int ty)
        {
            float cx = tx * Tile + 7.5f + ShadowX;
            float cy = ty * Tile + 5f - ShadowY / 2f;
            for (int py = (int)(cy - 5); py <= cy + 5; py++)
                for (int px = (int)(cx - 8); px <= cx + 8; px++)
                {
                    float dx = (px - cx) / 7f, dy = (py - cy) / 3.5f;
                    if (dx * dx + dy * dy <= 1f) img.Darken(px, py, 0.38f);
                }
        }

        // ------------------------------------------------------------------ Felsen und Wände (3/4)

        private enum BlockStyle
        {
            Rock,
            Pod,
            Wreck,
        }

        private static void DrawBlock(PixelImage img, CrashSiteLayout layout, int tx, int ty, int seed, BlockStyle style)
        {
            SiteTile kind = layout.Get(tx, ty);
            bool Same(int dx, int dy)
            {
                int x = tx + dx, y = ty + dy;
                if (!layout.InBounds(x, y)) return style == BlockStyle.Rock;
                return layout.Get(x, y) == kind;
            }

            bool southOpen = !Same(0, -1);
            bool northOpen = !Same(0, 1);
            bool westOpen = !Same(-1, 0);
            bool eastOpen = !Same(1, 0);
            int face = style == BlockStyle.Rock ? RockFace : WallFace;
            bool hazard = style == BlockStyle.Wreck && Hash(tx / 2, ty, seed + 51) < 0.35f;
            int x0 = tx * Tile;
            int y0 = ty * Tile;

            for (int j = 0; j < Tile; j++)
            {
                for (int i = 0; i < Tile; i++)
                {
                    int px = x0 + i, py = y0 + j;
                    uint c;
                    if (southOpen && j < face)
                    {
                        c = FaceColor(style, px, py, i, j, face, hazard, seed);
                        if ((westOpen && i == 0) || (eastOpen && i == 15)) c = style == BlockStyle.Rock ? Rust.R0 : Rust.S0;
                    }
                    else
                    {
                        c = TopColor(style, px, py, seed);
                        bool rimN = northOpen && j >= 14;
                        bool rimW = westOpen && i <= 0;
                        bool rimE = eastOpen && i >= 15;
                        if (rimN) c = style == BlockStyle.Rock ? (j == 15 ? Rust.R1 : Rust.R5) : style == BlockStyle.Pod ? (j == 15 ? Rust.S2 : Rust.S5) : (j == 15 ? Rust.S0 : Rust.S4);
                        else if (rimW || rimE) c = style == BlockStyle.Rock ? Rust.R1 : style == BlockStyle.Pod ? Rust.S2 : Rust.S0;
                        else if (northOpen && j == 13 && style != BlockStyle.Rock) c = style == BlockStyle.Pod ? Rust.S4 : Rust.S3;
                    }

                    img.Set(px, py, c);
                }
            }
        }

        private static uint TopColor(BlockStyle style, int px, int py, int seed)
        {
            switch (style)
            {
                case BlockStyle.Rock:
                    float n = 0.7f * Noise.Value(px / 12f, py / 12f, seed + 61) + 0.3f * Noise.Value(px / 4f, py / 4f, seed + 64)
                        + (PixelImage.Bayer(px, py) - 0.5f) * 0.08f;
                    if (Noise.Hash01(px, py, seed + 65) < 0.012f) return Rust.R5;
                    return n > 0.66f ? Rust.R4 : n > 0.34f ? Rust.R3 : Rust.R2;
                case BlockStyle.Pod:
                    return (px + py) % 6 < 3 && py % 16 == 8 ? Rust.Amber : Rust.S4;
                default:
                    if (px % 16 == 4 && py % 8 == 4) return Rust.S3; // Nieten
                    return Rust.S2;
            }
        }

        private static uint FaceColor(BlockStyle style, int px, int py, int i, int j, int face, bool hazard, int seed)
        {
            if (j == 0) return style == BlockStyle.Rock ? Rust.R0 : Rust.S0;
            switch (style)
            {
                case BlockStyle.Rock:
                    if (j == face - 1) return Rust.R1;
                    float streak = Hash(px, 0, seed + 63);
                    return streak > 0.75f ? Rust.R3 : streak < 0.12f ? Rust.R1 : Rust.R2;
                case BlockStyle.Pod:
                    if (j == face - 1) return Rust.S2;
                    if (j == 2) return px % 4 < 2 ? Rust.Amber : Rust.S2;
                    return px % 8 == 0 ? Rust.S2 : Rust.S3;
                default:
                    if (j == face - 1) return Rust.S0;
                    if (hazard && j >= 1 && j <= 3) return (px + j) % 6 < 3 ? Rust.Amber : Rust.R2;
                    return px % 8 == 0 ? Rust.S0 : Rust.S1;
            }
        }

        // ------------------------------------------------------------------ Einzelobjekte

        private static void DrawBoulder(PixelImage img, int tx, int ty, int seed)
        {
            int x0 = tx * Tile;
            int y0 = ty * Tile;
            float rx = 5.5f + Hash(tx, ty, seed + 71) * 1.5f;
            for (int j = 0; j < Tile; j++)
                for (int i = 0; i < Tile; i++)
                {
                    float dx = (i - 7.5f) / rx;
                    float dy = (j - 7f) / 5.5f;
                    float d = dx * dx + dy * dy;
                    if (d > 1f) continue;
                    uint c;
                    if (d > 0.78f) c = Rust.R1;
                    else if (dy < -0.25f) c = Rust.R2;                 // Vorderseite unten
                    else if (dx < -0.1f && dy > 0.2f) c = Rust.R5;      // Licht oben links
                    else c = Noise.Hash01(x0 + i, y0 + j, seed + 72) < 0.2f ? Rust.R3 : Rust.R4;
                    img.Set(x0 + i, y0 + j, c);
                }
        }

        private static void DrawDebris(PixelImage img, int tx, int ty, int seed)
        {
            int x0 = tx * Tile;
            int y0 = ty * Tile;
            for (int j = 1; j < 14; j++)
                for (int i = 1; i < 15; i++)
                {
                    float n = Noise.Value((x0 + i) / 3f, (y0 + j) / 3f, seed + 81);
                    float dx = (i - 7.5f) / 7f, dy = (j - 7f) / 6f;
                    if (n + 0.5f * (1f - (dx * dx + dy * dy)) < 0.85f) continue;
                    uint c = n > 0.72f ? Rust.S5 : n > 0.6f ? Rust.S4 : Rust.S3;
                    if (j <= 3) c = Rust.S1;
                    img.Set(x0 + i, y0 + j, c);
                }

            img.Set(x0 + 5, y0 + 9, Rust.R5);
            img.Set(x0 + 10, y0 + 6, Rust.R4);
        }
    }
}
