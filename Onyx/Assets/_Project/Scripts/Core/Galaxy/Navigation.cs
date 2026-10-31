using System;
using System.Collections.Generic;
using Onyx.Core.Survival;

namespace Onyx.Core.Galaxy
{
    public sealed class Route
    {
        public IReadOnlyList<int> Systems { get; }
        public float Distance { get; }
        public int Jumps => Systems.Count - 1;

        public Route(IReadOnlyList<int> systems, float distance)
        {
            Systems = systems;
            Distance = distance;
        }
    }

    /// <summary>Kürzeste Route über Sprungverbindungen (Dijkstra).</summary>
    public static class RoutePlanner
    {
        /// <param name="maxJumpDistance">Längste Verbindung, die das Schiff schafft; längere werden gemieden.</param>
        /// <returns>null, wenn kein Weg existiert.</returns>
        public static Route FindRoute(GalaxyMap map, int from, int to, float maxJumpDistance = float.MaxValue)
        {
            int count = map.Systems.Count;
            var dist = new float[count];
            var previous = new int[count];
            var done = new bool[count];
            for (int i = 0; i < count; i++)
            {
                dist[i] = float.MaxValue;
                previous[i] = -1;
            }

            dist[from] = 0f;
            for (int step = 0; step < count; step++)
            {
                int current = -1;
                for (int i = 0; i < count; i++)
                {
                    if (!done[i] && dist[i] < float.MaxValue && (current < 0 || dist[i] < dist[current]))
                    {
                        current = i;
                    }
                }

                if (current < 0 || current == to)
                {
                    break;
                }

                done[current] = true;
                foreach (var lane in map.LanesOf(current))
                {
                    if (lane.Distance > maxJumpDistance)
                    {
                        continue;
                    }

                    int next = lane.Other(current);
                    float candidate = dist[current] + lane.Distance;
                    if (candidate < dist[next])
                    {
                        dist[next] = candidate;
                        previous[next] = current;
                    }
                }
            }

            if (dist[to] == float.MaxValue)
            {
                return null;
            }

            var path = new List<int>();
            for (int at = to; at != -1; at = previous[at])
            {
                path.Add(at);
            }

            path.Reverse();
            return new Route(path, dist[to]);
        }
    }

    public enum JumpResult
    {
        Ok,
        NoLane,
        OutOfRange,
        NotEnoughFuel,
    }

    /// <summary>
    /// Wo sich das Schiff befindet und wie es zwischen Systemen springt. Läuft beim Host.
    /// </summary>
    public sealed class ShipNavigation
    {
        private readonly GalaxyMap map;

        public int CurrentSystemId { get; private set; }

        /// <summary>Planet im aktuellen System, den das Schiff umkreist; null = im freien Raum.</summary>
        public string OrbitPlanetId { get; private set; }

        public ResourcePool Fuel { get; }

        /// <summary>Treibstoff pro Lichtjahr (Karteneinheit).</summary>
        public float FuelPerDistance { get; set; }

        /// <summary>Längste Verbindung, die das Schiff in einem Sprung schafft.</summary>
        public float JumpRange { get; set; }

        /// <summary>Nach jedem erfolgreichen Sprung: (von, nach).</summary>
        public event Action<int, int> Jumped;

        public ShipNavigation(GalaxyMap map, float fuelCapacity, float jumpRange, float fuelPerDistance = 1f, int? startSystemId = null)
        {
            this.map = map ?? throw new ArgumentNullException(nameof(map));
            CurrentSystemId = startSystemId ?? map.StartSystemId;
            Fuel = new ResourcePool(fuelCapacity);
            JumpRange = jumpRange;
            FuelPerDistance = fuelPerDistance;
            Discover(CurrentSystemId);
        }

        public StarSystem CurrentSystem => map.GetSystem(CurrentSystemId);

        public float FuelCost(Lane lane) => lane.Distance * FuelPerDistance;

        public JumpResult CanJump(int targetSystemId)
        {
            if (!map.TryGetLane(CurrentSystemId, targetSystemId, out var lane))
            {
                return JumpResult.NoLane;
            }

            if (lane.Distance > JumpRange)
            {
                return JumpResult.OutOfRange;
            }

            return Fuel.Current + 0.0001f < FuelCost(lane) ? JumpResult.NotEnoughFuel : JumpResult.Ok;
        }

        public JumpResult Jump(int targetSystemId)
        {
            JumpResult check = CanJump(targetSystemId);
            if (check != JumpResult.Ok)
            {
                return check;
            }

            map.TryGetLane(CurrentSystemId, targetSystemId, out var lane);
            Fuel.Drain(Math.Min(FuelCost(lane), Fuel.Current));
            int from = CurrentSystemId;
            CurrentSystemId = targetSystemId;
            OrbitPlanetId = null;
            Discover(targetSystemId);
            Jumped?.Invoke(from, targetSystemId);
            return JumpResult.Ok;
        }

        /// <summary>In eine Umlaufbahn um einen Planeten des aktuellen Systems wechseln (ohne Treibstoff).</summary>
        public bool EnterOrbit(string planetId)
        {
            foreach (var planet in CurrentSystem.Planets)
            {
                if (planet.Id == planetId)
                {
                    OrbitPlanetId = planetId;
                    return true;
                }
            }

            return false;
        }

        /// <summary>Stellt Position, Tankinhalt und Umlaufbahn aus einem Spielstand wieder her.</summary>
        public void Restore(int systemId, float fuel, string orbitPlanetId)
        {
            map.GetSystem(systemId);
            CurrentSystemId = systemId;
            Discover(systemId);
            Fuel.Drain(Fuel.Max);
            Fuel.Refill(Math.Max(0f, fuel));
            OrbitPlanetId = null;
            if (!string.IsNullOrEmpty(orbitPlanetId))
            {
                EnterOrbit(orbitPlanetId);
            }
        }

        /// <summary>Teleport für Tests und Dev-Modus; ignoriert Verbindungen und Treibstoff.</summary>
        public void DebugSetSystem(int systemId)
        {
            map.GetSystem(systemId);
            CurrentSystemId = systemId;
            OrbitPlanetId = null;
            Discover(systemId);
        }

        private void Discover(int systemId)
        {
            map.GetSystem(systemId).Discovered = true;
        }
    }
}
