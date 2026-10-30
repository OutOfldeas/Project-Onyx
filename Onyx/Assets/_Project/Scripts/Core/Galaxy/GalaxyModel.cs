using System;
using System.Collections.Generic;

namespace Onyx.Core.Galaxy
{
    public enum StarType
    {
        YellowDwarf,
        RedDwarf,
        BlueGiant,
        WhiteDwarf,
    }

    public enum PlanetType
    {
        /// <summary>Rostwüste – wie die Heimatwelt der Absturzstelle.</summary>
        RustDesert,
        Savanna,
        Jungle,
        Ice,
        Toxic,
        Volcanic,
        Barren,
        /// <summary>Gasriese – nicht begehbar, aber Treibstoffquelle.</summary>
        GasGiant,
    }

    public sealed class Planet
    {
        public string Id { get; }
        public string Name { get; }
        public PlanetType Type { get; }

        /// <summary>Größe 1 (klein) bis 3 (groß); bestimmt später die Kartengröße.</summary>
        public int Size { get; }

        /// <summary>Gefahrenstufe 0 (harmlos) bis 3 (nur mit Spezialausrüstung).</summary>
        public int Hazard { get; }

        public bool Landable => Type != PlanetType.GasGiant;

        public Planet(string id, string name, PlanetType type, int size, int hazard)
        {
            Id = id;
            Name = name;
            Type = type;
            Size = size;
            Hazard = hazard;
        }

        public override string ToString() => $"{Name} ({Type}, Größe {Size}, Gefahr {Hazard})";
    }

    public sealed class StarSystem
    {
        public int Id { get; }
        public string Name { get; }
        public float X { get; }
        public float Y { get; }
        public StarType Star { get; }
        public IReadOnlyList<Planet> Planets { get; }

        /// <summary>Fraktion, die das System kontrolliert; -1 = niemand.</summary>
        public int FactionId { get; }

        /// <summary>Wurde das System schon besucht oder gescannt?</summary>
        public bool Discovered { get; set; }

        public StarSystem(int id, string name, float x, float y, StarType star, IReadOnlyList<Planet> planets, int factionId)
        {
            Id = id;
            Name = name;
            X = x;
            Y = y;
            Star = star;
            Planets = planets;
            FactionId = factionId;
        }

        public float DistanceTo(StarSystem other)
        {
            float dx = other.X - X;
            float dy = other.Y - Y;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }

        public override string ToString() => $"#{Id} {Name}";
    }

    /// <summary>Sprungverbindung zwischen zwei Systemen.</summary>
    public readonly struct Lane
    {
        public readonly int A;
        public readonly int B;
        public readonly float Distance;

        public Lane(int a, int b, float distance)
        {
            A = Math.Min(a, b);
            B = Math.Max(a, b);
            Distance = distance;
        }

        public int Other(int systemId) => systemId == A ? B : A;
    }

    public sealed class Faction
    {
        public int Id { get; }
        public string Name { get; }

        public Faction(int id, string name)
        {
            Id = id;
            Name = name;
        }
    }

    /// <summary>
    /// Die Sternenkarte einer Welt: Systeme, Sprungverbindungen und Fraktionen.
    /// Wird aus einem Seed erzeugt (<see cref="GalaxyGenerator"/>); nur der Fortschritt
    /// (z. B. entdeckte Systeme) muss gespeichert werden.
    /// </summary>
    public sealed class GalaxyMap
    {
        private readonly Dictionary<int, List<Lane>> lanesBySystem = new Dictionary<int, List<Lane>>();

        public int Seed { get; }
        public IReadOnlyList<StarSystem> Systems { get; }
        public IReadOnlyList<Lane> Lanes { get; }
        public IReadOnlyList<Faction> Factions { get; }

        /// <summary>Startsystem der Kampagne (Absturzort).</summary>
        public int StartSystemId { get; }

        public GalaxyMap(int seed, IReadOnlyList<StarSystem> systems, IReadOnlyList<Lane> lanes, IReadOnlyList<Faction> factions, int startSystemId)
        {
            Seed = seed;
            Systems = systems;
            Lanes = lanes;
            Factions = factions;
            StartSystemId = startSystemId;

            foreach (var system in systems)
            {
                lanesBySystem[system.Id] = new List<Lane>();
            }

            foreach (var lane in lanes)
            {
                lanesBySystem[lane.A].Add(lane);
                lanesBySystem[lane.B].Add(lane);
            }
        }

        public StarSystem GetSystem(int id)
        {
            if (id < 0 || id >= Systems.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(id), id, "Unbekanntes System.");
            }

            return Systems[id];
        }

        public IReadOnlyList<Lane> LanesOf(int systemId) => lanesBySystem[systemId];

        public bool TryGetLane(int a, int b, out Lane lane)
        {
            foreach (var candidate in lanesBySystem[a])
            {
                if (candidate.Other(a) == b)
                {
                    lane = candidate;
                    return true;
                }
            }

            lane = default;
            return false;
        }

        public Faction GetFaction(int id) => id >= 0 && id < Factions.Count ? Factions[id] : null;
    }
}
