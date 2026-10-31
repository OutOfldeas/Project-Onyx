using System;
using System.Collections.Generic;

namespace Onyx.Core.CrashSite
{
    /// <summary>
    /// Erzeugt die Absturzstelle aus einem Seed: Rostwüste mit Felsen, links die Fluchtkapsel,
    /// rechts das Wrack der Fregatte mit drei Räumen und den kaputten Systemen.
    /// Kapsel und Wrack liegen immer gleich (planbar fürs Leveldesign), das Gelände variiert.
    /// </summary>
    public static class CrashSiteGenerator
    {
        public const int Width = 72;
        public const int Height = 44;

        // Kapsel: Innenraum 4 × 4 um den Weltursprung, Tür nach Osten.
        private const int OriginX = 14;
        private const int OriginY = 22;

        // Wrack (Außenwände)
        private const int WreckMinX = 40;
        private const int WreckMaxX = 67;
        private const int WreckMinY = 12;
        private const int WreckMaxY = 31;

        // Mittelgang der Fregatte und Durchbrüche
        private const int AisleMinY = 21;
        private const int AisleMaxY = 22;

        public static CrashSiteLayout Generate(int seed)
        {
            var tiles = new SiteTile[Width * Height];
            void Set(int x, int y, SiteTile t)
            {
                if (x >= 0 && y >= 0 && x < Width && y < Height) tiles[y * Width + x] = t;
            }

            SiteTile Get(int x, int y) => tiles[y * Width + x];

            var random = new SeededRandom(seed ^ 0x0C4A5);
            var pod = new TileRect(OriginX - 2, OriginY - 2, OriginX + 1, OriginY + 1);
            var wreckOuter = new TileRect(WreckMinX, WreckMinY, WreckMaxX, WreckMaxY);
            var wreckInterior = new TileRect(WreckMinX + 1, WreckMinY + 1, WreckMaxX - 1, WreckMaxY - 1);

            // --- Gelände
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    float ground = Noise.Value(x / 5f, y / 5f, seed + 3);
                    SiteTile t = ground > 0.66f ? SiteTile.SandDark : ground < 0.22f ? SiteTile.Gravel : SiteTile.Sand;

                    bool edge = x < 2 || y < 2 || x >= Width - 2 || y >= Height - 2;
                    bool reserved = NearRect(pod, x, y, 4) || NearRect(wreckOuter, x, y, 3) || InCorridor(x, y);
                    if (edge)
                    {
                        t = SiteTile.Rock;
                    }
                    else if (!reserved)
                    {
                        float rocks = Noise.Value(x / 6f, y / 6f, seed + 11);
                        float edgeFade = Math.Min(Math.Min(x, Width - 1 - x), Math.Min(y, Height - 1 - y));
                        float threshold = edgeFade < 5 ? 0.62f : 0.74f; // zum Rand hin mehr Fels
                        if (rocks > threshold)
                        {
                            t = SiteTile.Rock;
                        }
                        else if (random.Chance(0.015f))
                        {
                            t = SiteTile.Boulder;
                        }
                    }

                    Set(x, y, t);
                }
            }

            // --- Absturzspur: Trümmer entlang des Wegs von der Kapsel zum Wrack
            for (int x = OriginX + 4; x < WreckMinX; x++)
            {
                for (int y = OriginY - 4; y <= OriginY + 4; y++)
                {
                    if (random.Chance(0.07f)) Set(x, y, SiteTile.Scrap);
                }
            }

            // --- Fluchtkapsel
            for (int y = pod.MinY - 1; y <= pod.MaxY + 1; y++)
            {
                for (int x = pod.MinX - 1; x <= pod.MaxX + 1; x++)
                {
                    bool wall = !pod.Contains(x, y);
                    Set(x, y, wall ? SiteTile.PodWall : SiteTile.PodFloor);
                }
            }

            Set(pod.MaxX + 1, OriginY - 1, SiteTile.PodFloor); // Tür
            Set(pod.MaxX + 1, OriginY, SiteTile.PodFloor);

            // --- Fregattenwrack: Hülle, abgeschrägte Ecken, Innenwände
            for (int y = WreckMinY; y <= WreckMaxY; y++)
            {
                for (int x = WreckMinX; x <= WreckMaxX; x++)
                {
                    bool outerWall = x == WreckMinX || x == WreckMaxX || y == WreckMinY || y == WreckMaxY;
                    Set(x, y, outerWall ? SiteTile.WreckWall : SiteTile.WreckFloor);
                }
            }

            // Bug (Osten) spitz zulaufen lassen: Ecken abschneiden
            for (int i = 0; i < 4; i++)
            {
                for (int k = 0; k <= i; k++)
                {
                    int x = WreckMaxX - 3 + i;
                    Set(x, WreckMinY + k, SiteTile.Sand);
                    Set(x, WreckMaxY - k, SiteTile.Sand);
                }

                Set(WreckMaxX - 3 + i, WreckMinY + i + 1, SiteTile.WreckWall);
                Set(WreckMaxX - 3 + i, WreckMaxY - i - 1, SiteTile.WreckWall);
            }

            int wall1 = WreckMinX + 10; // Lebenserhaltung | Reaktor
            int wall2 = WreckMinX + 19; // Reaktor | Antrieb
            for (int y = WreckMinY + 1; y < WreckMaxY; y++)
            {
                bool door = y >= AisleMinY && y <= AisleMaxY;
                if (!door)
                {
                    Set(wall1, y, SiteTile.WreckWall);
                    Set(wall2, y, SiteTile.WreckWall);
                }
            }

            // Durchbrüche: Haupteingang West (Absturzseite) und ein Riss in der Südwand
            for (int y = AisleMinY - 1; y <= AisleMaxY + 1; y++) Set(WreckMinX, y, SiteTile.WreckFloor);
            for (int x = wall1 + 3; x <= wall1 + 4; x++) Set(x, WreckMinY, SiteTile.WreckFloor);

            // --- Reparaturstellen
            var points = new List<RepairPoint>
            {
                new RepairPoint(0, StationKind.OxygenGenerator, "Sauerstoffgenerator", WreckMinX + 4, WreckMaxY - 3, 40f),
                new RepairPoint(1, StationKind.Reactor, "Reaktor", wall1 + 5, WreckMaxY - 4, 60f),
                new RepairPoint(2, StationKind.Engine, "Antrieb", wall2 + 5, AisleMaxY + 3, 90f, StationKind.Reactor),
            };

            foreach (var p in points)
            {
                Set(p.TileX, p.TileY, SiteTile.Station);
            }

            // --- Trümmer im Wrack, ohne Türen, Gang und Stationen zu versperren
            for (int y = wreckInterior.MinY; y <= wreckInterior.MaxY; y++)
            {
                for (int x = wreckInterior.MinX; x <= wreckInterior.MaxX; x++)
                {
                    if (Get(x, y) != SiteTile.WreckFloor) continue;
                    if (y >= AisleMinY - 1 && y <= AisleMaxY + 1) continue;
                    if (Math.Abs(x - wall1) <= 1 || Math.Abs(x - wall2) <= 1) continue;
                    if (Math.Abs(x - (wall1 + 3.5f)) <= 2 && y <= WreckMinY + 3) continue;
                    if (NearAnyPoint(points, x, y, 2)) continue;
                    if (random.Chance(0.05f)) Set(x, y, SiteTile.WreckDebris);
                }
            }

            return new CrashSiteLayout(seed, Width, Height, OriginX, OriginY, tiles, pod, wreckInterior, points);
        }

        private static bool NearRect(TileRect r, int x, int y, int margin) =>
            x >= r.MinX - margin && x <= r.MaxX + margin && y >= r.MinY - margin && y <= r.MaxY + margin;

        private static bool InCorridor(int x, int y) =>
            x >= OriginX && x <= WreckMinX && y >= AisleMinY - 4 && y <= AisleMaxY + 4;

        private static bool NearAnyPoint(List<RepairPoint> points, int x, int y, int distance)
        {
            foreach (var p in points)
            {
                if (Math.Abs(p.TileX - x) <= distance && Math.Abs(p.TileY - y) <= distance) return true;
            }

            return false;
        }
    }
}
