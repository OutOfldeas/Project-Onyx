using System;
using System.Collections.Generic;

namespace Onyx.Core.CrashSite
{
    /// <summary>Kachelarten der Absturzstelle (je 16 × 16 px).</summary>
    public enum SiteTile : byte
    {
        Sand,
        SandDark,
        Gravel,
        Scrap,
        Rock,
        Boulder,
        PodFloor,
        PodWall,
        WreckFloor,
        WreckWall,
        WreckDebris,
        Station,
    }

    /// <summary>Die kaputten Systeme der Fregatte, die die Gruppe reparieren muss.</summary>
    public enum StationKind
    {
        OxygenGenerator,
        Reactor,
        Engine,
    }

    public enum SiteZone
    {
        Outside,
        Pod,
        Wreck,
    }

    /// <summary>Eine Reparaturstelle an der Fregatte.</summary>
    public sealed class RepairPoint
    {
        public int Index { get; }
        public StationKind Kind { get; }
        public string Name { get; }

        /// <summary>Kachelposition der Station (die Kachel selbst ist blockiert, man arbeitet daneben).</summary>
        public int TileX { get; }
        public int TileY { get; }

        /// <summary>Benötigte Arbeitssekunden eines einzelnen ungeübten Spielers.</summary>
        public float Work { get; }

        /// <summary>Station, die vorher laufen muss (z. B. der Antrieb braucht Strom vom Reaktor). null = keine.</summary>
        public StationKind? Requires { get; }

        public RepairPoint(int index, StationKind kind, string name, int tileX, int tileY, float work, StationKind? requires = null)
        {
            Index = index;
            Kind = kind;
            Name = name;
            TileX = tileX;
            TileY = tileY;
            Work = work;
            Requires = requires;
        }
    }

    /// <summary>Rechteck in Kachelkoordinaten (einschließlich beider Ränder).</summary>
    public readonly struct TileRect
    {
        public readonly int MinX;
        public readonly int MinY;
        public readonly int MaxX;
        public readonly int MaxY;

        public TileRect(int minX, int minY, int maxX, int maxY)
        {
            MinX = minX;
            MinY = minY;
            MaxX = maxX;
            MaxY = maxY;
        }

        public bool Contains(int x, int y) => x >= MinX && x <= MaxX && y >= MinY && y <= MaxY;

        public float CenterX => (MinX + MaxX + 1) / 2f;
        public float CenterY => (MinY + MaxY + 1) / 2f;
    }

    /// <summary>
    /// Die Karte der Absturzstelle: Kacheln, Fluchtkapsel, Fregattenwrack und Reparaturstellen.
    /// Weltkoordinaten (Einheit = 1 Kachel) haben ihren Ursprung in der Mitte der Kapsel,
    /// dort erscheinen die Spieler.
    /// </summary>
    public sealed class CrashSiteLayout
    {
        private readonly SiteTile[] tiles;

        public int Seed { get; }
        public int Width { get; }
        public int Height { get; }

        /// <summary>Kachel, die dem Weltursprung (0, 0) entspricht.</summary>
        public int OriginX { get; }
        public int OriginY { get; }

        /// <summary>Innenraum der Kapsel (füllt Sauerstoff auf).</summary>
        public TileRect PodInterior { get; }

        /// <summary>Innenraum der Fregatte (Schutz vor Kälte, Luft erst mit Sauerstoffgenerator).</summary>
        public TileRect WreckInterior { get; }

        public IReadOnlyList<RepairPoint> RepairPoints { get; }

        public CrashSiteLayout(int seed, int width, int height, int originX, int originY, SiteTile[] tiles,
            TileRect podInterior, TileRect wreckInterior, IReadOnlyList<RepairPoint> repairPoints)
        {
            if (tiles.Length != width * height)
            {
                throw new ArgumentException("Kachelzahl passt nicht zur Größe.", nameof(tiles));
            }

            Seed = seed;
            Width = width;
            Height = height;
            OriginX = originX;
            OriginY = originY;
            this.tiles = tiles;
            PodInterior = podInterior;
            WreckInterior = wreckInterior;
            RepairPoints = repairPoints;
        }

        public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;

        /// <summary>Kachel an der Stelle; außerhalb der Karte zählt alles als Fels.</summary>
        public SiteTile Get(int x, int y) => InBounds(x, y) ? tiles[y * Width + x] : SiteTile.Rock;

        public static bool IsBlocking(SiteTile tile)
        {
            switch (tile)
            {
                case SiteTile.Rock:
                case SiteTile.Boulder:
                case SiteTile.PodWall:
                case SiteTile.WreckWall:
                case SiteTile.WreckDebris:
                case SiteTile.Station:
                    return true;
                default:
                    return false;
            }
        }

        public bool IsBlocked(int x, int y) => IsBlocking(Get(x, y));

        // ------------------------------------------------------------------ Welt <-> Kachel

        public (int x, int y) WorldToTile(float worldX, float worldY) =>
            ((int)Math.Floor(worldX) + OriginX, (int)Math.Floor(worldY) + OriginY);

        /// <summary>Mittelpunkt einer Kachel in Weltkoordinaten.</summary>
        public (float x, float y) TileCenter(int x, int y) => (x - OriginX + 0.5f, y - OriginY + 0.5f);

        public bool IsBlockedAt(float worldX, float worldY)
        {
            var (x, y) = WorldToTile(worldX, worldY);
            return IsBlocked(x, y);
        }

        public SiteZone ZoneAt(float worldX, float worldY)
        {
            var (x, y) = WorldToTile(worldX, worldY);
            if (PodInterior.Contains(x, y)) return SiteZone.Pod;
            if (WreckInterior.Contains(x, y)) return SiteZone.Wreck;
            return SiteZone.Outside;
        }

        /// <summary>Abstand eines Weltpunkts zur Mitte einer Reparaturstelle (in Kacheln).</summary>
        public float DistanceTo(RepairPoint point, float worldX, float worldY)
        {
            var (cx, cy) = TileCenter(point.TileX, point.TileY);
            float dx = cx - worldX;
            float dy = cy - worldY;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }
    }
}
