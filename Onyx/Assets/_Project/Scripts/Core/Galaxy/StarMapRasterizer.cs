using System;
using System.Collections.Generic;
using System.Linq;

namespace Onyx.Core.Galaxy
{
    /// <summary>Feste Palette der Sternenkarte (Art Direction „Rost &amp; Signal“).</summary>
    public enum MapColor : byte
    {
        Void0, Void1, Void3, Smog1, VioletDark,
        Steel1, Steel2, Steel3, Bone,
        Amber, Red, RedDark, Cyan, CyanDark, Orange, OrangeDark, Sand,
    }

    public static class MapPalette
    {
        /// <summary>RGB je <see cref="MapColor"/> als 0xRRGGBB.</summary>
        public static readonly uint[] Rgb =
        {
            0x07080F, 0x0F1424, 0x2C3654, 0x3A2E3A, 0x5B2A86,
            0x4A5573, 0x7A849E, 0xB4BCCC, 0xE8E4D8,
            0xFFB52E, 0xFF3D5A, 0x8A1A34, 0x39D5E6, 0x1A6F86, 0xDE8A48, 0x7D3A24, 0xE9B872,
        };

        public static (MapColor fill, MapColor border) Faction(int factionId)
        {
            switch (factionId)
            {
                case 0: return (MapColor.RedDark, MapColor.Red);
                case 1: return (MapColor.CyanDark, MapColor.Cyan);
                case 2: return (MapColor.OrangeDark, MapColor.Orange);
                default: return (MapColor.Steel1, MapColor.Steel2);
            }
        }
    }

    /// <summary>Was die Karte gerade zeigen soll (Position, Auswahl, geplante Route).</summary>
    public sealed class StarMapView
    {
        public int CurrentSystemId = -1;
        public int SelectedSystemId = -1;
        public IReadOnlyList<int> Route;
        public float JumpRange = float.MaxValue;

        /// <summary>Alles anzeigen (Dev-Modus), sonst nur Entdecktes und direkte Nachbarn.</summary>
        public bool RevealAll;
    }

    /// <summary>Ergebnis: Pixel (Palettenindizes, Zeile 0 = oben) plus Positionen für Beschriftungen.</summary>
    public sealed class StarMapImage
    {
        public int Width { get; }
        public int Height { get; }
        public MapColor[] Pixels { get; }

        /// <summary>Pixelposition jedes sichtbaren Systems.</summary>
        public Dictionary<int, (int x, int y)> SystemPositions { get; } = new Dictionary<int, (int x, int y)>();

        /// <summary>Systeme, die als entdeckt gezeichnet wurden (bekommen einen Namen).</summary>
        public HashSet<int> DiscoveredVisible { get; } = new HashSet<int>();

        /// <summary>Mittelpunkt jedes Fraktionsgebiets für das Namensschild.</summary>
        public Dictionary<int, (int x, int y)> FactionLabels { get; } = new Dictionary<int, (int x, int y)>();

        public StarMapImage(int width, int height)
        {
            Width = width;
            Height = height;
            Pixels = new MapColor[width * height];
        }

        public MapColor Get(int x, int y) => Pixels[y * Width + x];

        internal void Set(int x, int y, MapColor color)
        {
            if (x >= 0 && y >= 0 && x < Width && y < Height)
            {
                Pixels[y * Width + x] = color;
            }
        }
    }

    /// <summary>
    /// Zeichnet die Sternenkarte als Pixelbild (480 × 270, wie die Spielgrafik): Nebel, Sterne,
    /// gerasterte Fraktionsgebiete, Sprungverbindungen, Systeme und die eigene Position.
    /// Deterministisch: gleiche Galaxie + gleiche Ansicht = gleiches Bild.
    /// </summary>
    public static class StarMapRasterizer
    {
        public const int Width = 480;
        public const int Height = 270;
        private const int MarginX = 28;
        private const int MarginY = 22;

        /// <summary>Radius der Fraktionsgebiete um ihre Systeme, in Karteneinheiten.</summary>
        public const float TerritoryRadius = 11f;

        private static readonly float[,] Bayer =
        {
            { 0f / 16, 8f / 16, 2f / 16, 10f / 16 },
            { 12f / 16, 4f / 16, 14f / 16, 6f / 16 },
            { 3f / 16, 11f / 16, 1f / 16, 9f / 16 },
            { 15f / 16, 7f / 16, 13f / 16, 5f / 16 },
        };

        public static StarMapImage Render(GalaxyMap map, StarMapView view)
        {
            var image = new StarMapImage(Width, Height);
            var transform = MapTransform.For(map);

            DrawNebula(image, map.Seed);
            DrawBackgroundStars(image, map.Seed);

            var visible = VisibleSystems(map, view);
            DrawTerritories(image, map, transform);
            DrawLanes(image, map, view, visible, transform);
            DrawSystems(image, map, view, visible, transform);
            DrawShipMarker(image, map, view, transform);
            return image;
        }

        /// <summary>Pixelposition eines Systems (auch für Klicks auf der Karte).</summary>
        public static (int x, int y) ToPixel(GalaxyMap map, StarSystem system) => MapTransform.For(map).Apply(system);

        // ------------------------------------------------------------------ Sichtbarkeit

        public static HashSet<int> VisibleSystems(GalaxyMap map, StarMapView view)
        {
            var visible = new HashSet<int>();
            foreach (var system in map.Systems)
            {
                if (view.RevealAll || system.Discovered)
                {
                    visible.Add(system.Id);
                    foreach (var lane in map.LanesOf(system.Id))
                    {
                        visible.Add(lane.Other(system.Id));
                    }
                }
            }

            return visible;
        }

        private static bool IsDiscovered(StarSystem system, StarMapView view) => view.RevealAll || system.Discovered;

        // ------------------------------------------------------------------ Hintergrund

        private static void DrawNebula(StarMapImage image, int seed)
        {
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    float n = 0.7f * ValueNoise(x / 70f, y / 70f, seed) + 0.3f * ValueNoise(x / 18f, y / 18f, seed + 17);
                    float v = n + (Bayer[y & 3, x & 3] - 0.5f) * 0.18f;
                    MapColor c = MapColor.Void0;
                    if (v > 0.55f) c = MapColor.Void1;
                    if (v > 0.70f) c = MapColor.Smog1;
                    if (v > 0.82f) c = MapColor.VioletDark;
                    image.Set(x, y, c);
                }
            }
        }

        private static void DrawBackgroundStars(StarMapImage image, int seed)
        {
            var random = new SeededRandom(seed ^ 0x5A17);
            MapColor[] dim = { MapColor.Void3, MapColor.Steel1, MapColor.Steel2 };
            for (int i = 0; i < 420; i++)
            {
                image.Set(random.Next(0, Width), random.Next(0, Height), random.Pick(dim));
            }

            for (int i = 0; i < 24; i++)
            {
                image.Set(random.Next(0, Width), random.Next(0, Height), MapColor.Bone);
            }
        }

        // ------------------------------------------------------------------ Fraktionen

        private static void DrawTerritories(StarMapImage image, GalaxyMap map, MapTransform transform)
        {
            // Auch neutrale Systeme zählen: Wo sie am nächsten liegen, bleibt eine Lücke im Gebiet.
            var members = map.Systems
                .Select(s => (faction: s.FactionId, pos: transform.Apply(s)))
                .ToList();
            if (members.All(m => m.faction < 0))
            {
                return;
            }

            float radiusPx = TerritoryRadius * transform.Scale;
            var owner = new int[Width * Height];
            var depth = new float[Width * Height];
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    float warp = (ValueNoise(x / 26f, y / 26f, map.Seed + 31) - 0.5f) * 0.55f;
                    int best = -1;
                    float bestD = 1f;
                    // best bleibt -1, wenn das nächste System neutral ist oder keines nah genug liegt.
                    foreach (var (faction, pos) in members)
                    {
                        float dx = x - pos.x;
                        float dy = y - pos.y;
                        float d = (float)Math.Sqrt(dx * dx + dy * dy) / radiusPx + warp;
                        if (d < bestD)
                        {
                            bestD = d;
                            best = faction;
                        }
                    }

                    owner[y * Width + x] = best;
                    depth[y * Width + x] = bestD;
                }
            }

            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    int o = owner[y * Width + x];
                    if (o < 0)
                    {
                        continue;
                    }

                    var (fill, border) = MapPalette.Faction(o);
                    bool edge = OwnerAt(owner, x - 1, y, o) != o || OwnerAt(owner, x + 1, y, o) != o
                        || OwnerAt(owner, x, y - 1, o) != o || OwnerAt(owner, x, y + 1, o) != o;
                    if (edge)
                    {
                        if ((x + y) % 3 != 0) image.Set(x, y, border);
                    }
                    else if (Bayer[y & 3, x & 3] < 0.04f + 0.16f * Math.Max(0f, depth[y * Width + x]))
                    {
                        image.Set(x, y, fill);
                    }
                }
            }

            foreach (var group in members.Where(m => m.faction >= 0).GroupBy(m => m.faction))
            {
                int cx = (int)group.Average(m => m.pos.x);
                int cy = (int)group.Average(m => m.pos.y);
                image.FactionLabels[group.Key] = (cx, cy);
            }
        }

        /// <summary>Besitzer eines Pixels; außerhalb des Bildes zählt der Nachbar als gleich (kein Rand am Bildrand).</summary>
        private static int OwnerAt(int[] owner, int x, int y, int outside)
        {
            if (x < 0 || y < 0 || x >= Width || y >= Height) return outside;
            return owner[y * Width + x];
        }

        // ------------------------------------------------------------------ Verbindungen

        private static void DrawLanes(StarMapImage image, GalaxyMap map, StarMapView view, HashSet<int> visible, MapTransform transform)
        {
            var routeLanes = new HashSet<(int, int)>();
            if (view.Route != null)
            {
                for (int i = 0; i + 1 < view.Route.Count; i++)
                {
                    routeLanes.Add((Math.Min(view.Route[i], view.Route[i + 1]), Math.Max(view.Route[i], view.Route[i + 1])));
                }
            }

            foreach (var lane in map.Lanes)
            {
                var a = map.GetSystem(lane.A);
                var b = map.GetSystem(lane.B);
                bool known = visible.Contains(lane.A) && visible.Contains(lane.B) && (IsDiscovered(a, view) || IsDiscovered(b, view));
                bool onRoute = routeLanes.Contains((lane.A, lane.B));
                if (!known && !onRoute)
                {
                    continue;
                }

                MapColor color = onRoute ? MapColor.Amber : lane.Distance <= view.JumpRange ? MapColor.Steel1 : MapColor.RedDark;
                DrawLine(image, transform.Apply(a), transform.Apply(b), color, dashed: !onRoute);
            }
        }

        private static void DrawLine(StarMapImage image, (int x, int y) a, (int x, int y) b, MapColor color, bool dashed)
        {
            int steps = Math.Max(Math.Abs(b.x - a.x), Math.Abs(b.y - a.y));
            for (int i = 0; i <= steps; i++)
            {
                if (dashed && (i / 2) % 2 == 1)
                {
                    continue;
                }

                float t = steps == 0 ? 0f : i / (float)steps;
                image.Set((int)Math.Round(a.x + (b.x - a.x) * t), (int)Math.Round(a.y + (b.y - a.y) * t), color);
            }
        }

        // ------------------------------------------------------------------ Systeme

        private static void DrawSystems(StarMapImage image, GalaxyMap map, StarMapView view, HashSet<int> visible, MapTransform transform)
        {
            foreach (var system in map.Systems)
            {
                if (!visible.Contains(system.Id))
                {
                    continue;
                }

                var p = transform.Apply(system);
                image.SystemPositions[system.Id] = p;

                if (IsDiscovered(system, view))
                {
                    image.DiscoveredVisible.Add(system.Id);
                    MapColor color = system.FactionId >= 0 ? MapPalette.Faction(system.FactionId).border : MapColor.Sand;
                    // Kreuz 5 × 5 mit hellem Kern
                    for (int i = -2; i <= 2; i++)
                    {
                        image.Set(p.x + i, p.y, color);
                        image.Set(p.x, p.y + i, color);
                    }

                    image.Set(p.x - 1, p.y - 1, color);
                    image.Set(p.x + 1, p.y - 1, color);
                    image.Set(p.x - 1, p.y + 1, color);
                    image.Set(p.x + 1, p.y + 1, color);
                    image.Set(p.x, p.y, MapColor.Bone);
                }
                else
                {
                    // Unerforscht: grauer Rahmen
                    for (int i = -2; i <= 2; i++)
                    {
                        image.Set(p.x + i, p.y - 2, MapColor.Steel1);
                        image.Set(p.x + i, p.y + 2, MapColor.Steel1);
                        image.Set(p.x - 2, p.y + i, MapColor.Steel1);
                        image.Set(p.x + 2, p.y + i, MapColor.Steel1);
                    }

                    image.Set(p.x, p.y, MapColor.Steel2);
                }

                if (system.Id == view.SelectedSystemId)
                {
                    DrawBrackets(image, p, 6, MapColor.Bone);
                }
            }
        }


        private static void DrawBrackets(StarMapImage image, (int x, int y) p, int r, MapColor color)
        {
            for (int i = 0; i < 3; i++)
            {
                image.Set(p.x - r + i, p.y - r, color); image.Set(p.x - r, p.y - r + i, color);
                image.Set(p.x + r - i, p.y - r, color); image.Set(p.x + r, p.y - r + i, color);
                image.Set(p.x - r + i, p.y + r, color); image.Set(p.x - r, p.y + r - i, color);
                image.Set(p.x + r - i, p.y + r, color); image.Set(p.x + r, p.y + r - i, color);
            }
        }

        private static void DrawShipMarker(StarMapImage image, GalaxyMap map, StarMapView view, MapTransform transform)
        {
            if (view.CurrentSystemId < 0)
            {
                return;
            }

            var p = transform.Apply(map.GetSystem(view.CurrentSystemId));

            // Ring um das aktuelle System
            const int Radius = 9;
            for (int a = 0; a < 64; a++)
            {
                double angle = a / 64.0 * Math.PI * 2;
                image.Set(p.x + (int)Math.Round(Math.Cos(angle) * Radius), p.y + (int)Math.Round(Math.Sin(angle) * Radius), MapColor.Amber);
            }

            // Schiff (Pfeil) oben rechts am Ring
            string[] ship =
            {
                "...a...",
                "..aba..",
                ".abbba.",
                "abbabba",
                "aa...aa",
            };
            int sx = p.x + 8;
            int sy = p.y - 14;
            for (int row = 0; row < ship.Length; row++)
            {
                for (int col = 0; col < ship[row].Length; col++)
                {
                    char c = ship[row][col];
                    if (c == 'a') image.Set(sx + col, sy + row, MapColor.Amber);
                    else if (c == 'b') image.Set(sx + col, sy + row, MapColor.Bone);
                }
            }
        }

        // ------------------------------------------------------------------ Hilfen

        private readonly struct MapTransform
        {
            public readonly float Scale;
            private readonly float offsetX;
            private readonly float offsetY;
            private readonly float minX;
            private readonly float minY;

            private MapTransform(float scale, float offsetX, float offsetY, float minX, float minY)
            {
                Scale = scale;
                this.offsetX = offsetX;
                this.offsetY = offsetY;
                this.minX = minX;
                this.minY = minY;
            }

            public static MapTransform For(GalaxyMap map)
            {
                float minX = map.Systems.Min(s => s.X);
                float maxX = map.Systems.Max(s => s.X);
                float minY = map.Systems.Min(s => s.Y);
                float maxY = map.Systems.Max(s => s.Y);
                float spanX = Math.Max(1f, maxX - minX);
                float spanY = Math.Max(1f, maxY - minY);
                float scale = Math.Min((Width - 2 * MarginX) / spanX, (Height - 2 * MarginY) / spanY);
                float offsetX = (Width - spanX * scale) / 2f;
                float offsetY = (Height - spanY * scale) / 2f;
                return new MapTransform(scale, offsetX, offsetY, minX, minY);
            }

            public (int x, int y) Apply(StarSystem s)
            {
                int x = (int)Math.Round(offsetX + (s.X - minX) * Scale);
                int y = (int)Math.Round(Height - offsetY - (s.Y - minY) * Scale); // y nach oben in der Galaxie, nach unten im Bild
                return (x, y);
            }
        }

        /// <summary>Glatter Wertrauschen-Wert in [0, 1], deterministisch über den Seed.</summary>
        public static float ValueNoise(float x, float y, int seed) => Noise.Value(x, y, seed);
    }
}
