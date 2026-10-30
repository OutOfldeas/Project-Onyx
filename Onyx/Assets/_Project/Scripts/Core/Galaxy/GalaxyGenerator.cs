using System;
using System.Collections.Generic;
using System.Linq;

namespace Onyx.Core.Galaxy
{
    public sealed class GalaxySettings
    {
        public int SystemCount = 24;
        public float Width = 100f;
        public float Height = 60f;

        /// <summary>Mindestabstand zwischen zwei Systemen.</summary>
        public float MinSystemDistance = 9f;

        /// <summary>Zusätzliche Verbindungen zu den nächsten Nachbarn (über das Minimalnetz hinaus).</summary>
        public int ExtraLanesPerSystem = 1;

        /// <summary>Längste Sprungverbindung, die zusätzlich angelegt wird.</summary>
        public float MaxExtraLaneLength = 22f;

        public int MinPlanets = 1;
        public int MaxPlanets = 5;
    }

    /// <summary>
    /// Erzeugt eine Sternenkarte deterministisch aus einem Seed.
    /// Gleicher Seed + gleiche Einstellungen = gleiche Galaxie bei allen Spielern.
    /// </summary>
    public static class GalaxyGenerator
    {
        private static readonly string[] Syllables =
        {
            "ka", "ri", "on", "vel", "tor", "ix", "ma", "dra", "sel", "ne", "ul", "zor",
            "ae", "th", "or", "bal", "cy", "mir", "os", "tek", "ra", "ves", "lun", "gar",
        };

        private static readonly string[] FactionNames = { "Konzern", "Synth-Kollektiv", "Schrottgilde" };

        private static readonly PlanetType[] PlanetTypes =
        {
            PlanetType.RustDesert, PlanetType.Savanna, PlanetType.Jungle, PlanetType.Ice,
            PlanetType.Toxic, PlanetType.Volcanic, PlanetType.Barren, PlanetType.GasGiant,
        };

        public static GalaxyMap Generate(int seed, GalaxySettings settings = null)
        {
            settings = settings ?? new GalaxySettings();
            var random = new SeededRandom(seed);

            var positions = PlacePositions(random, settings);
            int count = positions.Count;

            // Startsystem: das am weitesten links in der Mitte (ruhige Ecke der Karte).
            int startId = Enumerable.Range(0, count)
                .OrderBy(i => positions[i].x + Math.Abs(positions[i].y - settings.Height / 2f) * 0.5f)
                .First();

            var lanes = BuildLanes(positions, settings);
            var factions = FactionNames.Select((name, i) => new Faction(i, name)).ToList();
            var owners = AssignTerritories(random, positions, startId, factions.Count);

            var usedNames = new HashSet<string>();
            var systems = new List<StarSystem>(count);
            for (int i = 0; i < count; i++)
            {
                string name = UniqueName(random, usedNames);
                var star = (StarType)random.Next(0, Enum.GetValues(typeof(StarType)).Length);
                var planets = CreatePlanets(random, i, name, settings, isStart: i == startId);
                systems.Add(new StarSystem(i, name, positions[i].x, positions[i].y, star, planets, owners[i])
                {
                    Discovered = i == startId,
                });
            }

            return new GalaxyMap(seed, systems, lanes, factions, startId);
        }

        private static List<(float x, float y)> PlacePositions(SeededRandom random, GalaxySettings settings)
        {
            var positions = new List<(float x, float y)>();
            int attempts = 0;
            float minSq = settings.MinSystemDistance * settings.MinSystemDistance;
            while (positions.Count < settings.SystemCount && attempts < settings.SystemCount * 200)
            {
                attempts++;
                float x = random.NextFloat(0f, settings.Width);
                float y = random.NextFloat(0f, settings.Height);
                bool tooClose = positions.Any(p => (p.x - x) * (p.x - x) + (p.y - y) * (p.y - y) < minSq);
                if (!tooClose)
                {
                    positions.Add((x, y));
                }
            }

            return positions;
        }

        /// <summary>Minimaler Spannbaum (alle Systeme erreichbar) plus kurze Zusatzverbindungen.</summary>
        private static List<Lane> BuildLanes(List<(float x, float y)> positions, GalaxySettings settings)
        {
            int count = positions.Count;
            var edges = new List<Lane>();
            for (int a = 0; a < count; a++)
            {
                for (int b = a + 1; b < count; b++)
                {
                    edges.Add(new Lane(a, b, Distance(positions[a], positions[b])));
                }
            }

            edges.Sort((l, r) => l.Distance.CompareTo(r.Distance));

            // Kruskal
            var parent = Enumerable.Range(0, count).ToArray();
            int Find(int i) => parent[i] == i ? i : parent[i] = Find(parent[i]);

            var lanes = new List<Lane>();
            var degree = new int[count];
            foreach (var edge in edges)
            {
                int ra = Find(edge.A);
                int rb = Find(edge.B);
                if (ra != rb)
                {
                    parent[ra] = rb;
                    lanes.Add(edge);
                    degree[edge.A]++;
                    degree[edge.B]++;
                }
            }

            // Zusatzverbindungen: Für Rundwege und alternative Routen.
            var existing = new HashSet<(int, int)>(lanes.Select(l => (l.A, l.B)));
            int maxDegree = 2 + settings.ExtraLanesPerSystem;
            foreach (var edge in edges)
            {
                if (edge.Distance > settings.MaxExtraLaneLength)
                {
                    break;
                }

                if (existing.Contains((edge.A, edge.B)) || degree[edge.A] >= maxDegree || degree[edge.B] >= maxDegree)
                {
                    continue;
                }

                lanes.Add(edge);
                existing.Add((edge.A, edge.B));
                degree[edge.A]++;
                degree[edge.B]++;
            }

            return lanes;
        }

        private static int[] AssignTerritories(SeededRandom random, List<(float x, float y)> positions, int startId, int factionCount)
        {
            int count = positions.Count;
            var owners = Enumerable.Repeat(-1, count).ToArray();

            // Fraktionskerne: möglichst weit vom Start entfernte, unterschiedliche Systeme.
            var candidates = Enumerable.Range(0, count)
                .Where(i => i != startId)
                .OrderByDescending(i => Distance(positions[i], positions[startId]))
                .Take(Math.Max(factionCount * 3, factionCount))
                .ToList();

            var cores = new List<int>();
            while (cores.Count < factionCount && candidates.Count > 0)
            {
                int pick = candidates[random.Next(0, candidates.Count)];
                candidates.Remove(pick);
                if (cores.All(c => Distance(positions[c], positions[pick]) > 20f) || candidates.Count == 0)
                {
                    cores.Add(pick);
                }
            }

            const float TerritoryRadius = 26f;
            for (int i = 0; i < count; i++)
            {
                if (i == startId)
                {
                    continue;
                }

                float best = float.MaxValue;
                for (int f = 0; f < cores.Count; f++)
                {
                    float d = Distance(positions[i], positions[cores[f]]);
                    if (d < best && d < TerritoryRadius)
                    {
                        best = d;
                        owners[i] = f;
                    }
                }
            }

            return owners;
        }

        private static List<Planet> CreatePlanets(SeededRandom random, int systemId, string systemName, GalaxySettings settings, bool isStart)
        {
            int planetCount = random.Next(settings.MinPlanets, settings.MaxPlanets + 1);
            var planets = new List<Planet>(planetCount);
            for (int p = 0; p < planetCount; p++)
            {
                PlanetType type = isStart && p == 0 ? PlanetType.RustDesert : random.Pick(PlanetTypes);
                int size = type == PlanetType.GasGiant ? 3 : random.Next(1, 4);
                int hazard = isStart && p == 0 ? 0 : HazardFor(type, random);
                string name = $"{systemName} {Roman(p + 1)}";
                planets.Add(new Planet($"S{systemId}-P{p}", name, type, size, hazard));
            }

            return planets;
        }

        private static int HazardFor(PlanetType type, SeededRandom random)
        {
            switch (type)
            {
                case PlanetType.Volcanic:
                case PlanetType.Toxic:
                    return random.Next(2, 4);
                case PlanetType.Ice:
                case PlanetType.Jungle:
                    return random.Next(1, 3);
                case PlanetType.GasGiant:
                    return 3;
                default:
                    return random.Next(0, 2);
            }
        }

        private static string UniqueName(SeededRandom random, HashSet<string> used)
        {
            for (int attempt = 0; attempt < 50; attempt++)
            {
                int parts = random.Next(2, 4);
                string raw = string.Concat(Enumerable.Range(0, parts).Select(_ => random.Pick(Syllables)));
                string name = char.ToUpperInvariant(raw[0]) + raw.Substring(1);
                if (used.Add(name))
                {
                    return name;
                }
            }

            string fallback = $"System-{used.Count}";
            used.Add(fallback);
            return fallback;
        }

        private static string Roman(int n)
        {
            string[] numerals = { "I", "II", "III", "IV", "V", "VI", "VII", "VIII" };
            return n >= 1 && n <= numerals.Length ? numerals[n - 1] : n.ToString();
        }

        private static float Distance((float x, float y) a, (float x, float y) b)
        {
            float dx = a.x - b.x;
            float dy = a.y - b.y;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }
    }
}
