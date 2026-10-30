using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Onyx.Core.Galaxy;

namespace Onyx.Core.Tests.Galaxy
{
    public class GalaxyTests
    {
        [Test]
        public void SameSeed_SameGalaxy()
        {
            var a = GalaxyGenerator.Generate(42);
            var b = GalaxyGenerator.Generate(42);

            Assert.That(a.Systems.Select(s => s.Name), Is.EqualTo(b.Systems.Select(s => s.Name)));
            Assert.That(a.Systems.Select(s => s.X), Is.EqualTo(b.Systems.Select(s => s.X)));
            Assert.That(a.Lanes.Count, Is.EqualTo(b.Lanes.Count));
        }

        [Test]
        public void DifferentSeed_DifferentGalaxy()
        {
            var a = GalaxyGenerator.Generate(1);
            var b = GalaxyGenerator.Generate(2);

            Assert.That(a.Systems.Select(s => s.Name), Is.Not.EqualTo(b.Systems.Select(s => s.Name)));
        }

        [TestCase(1)]
        [TestCase(7)]
        [TestCase(12345)]
        public void AllSystemsReachable(int seed)
        {
            var map = GalaxyGenerator.Generate(seed);
            var seen = new HashSet<int> { 0 };
            var queue = new Queue<int>(seen);
            while (queue.Count > 0)
            {
                int current = queue.Dequeue();
                foreach (var lane in map.LanesOf(current))
                {
                    if (seen.Add(lane.Other(current))) queue.Enqueue(lane.Other(current));
                }
            }

            Assert.That(seen.Count, Is.EqualTo(map.Systems.Count));
            Assert.That(map.Systems.Count, Is.GreaterThanOrEqualTo(15));
        }

        [Test]
        public void StartSystem_HasSafeRustPlanet_AndIsDiscovered()
        {
            var map = GalaxyGenerator.Generate(99);
            var start = map.GetSystem(map.StartSystemId);

            Assert.That(start.Planets[0].Type, Is.EqualTo(PlanetType.RustDesert));
            Assert.That(start.Planets[0].Hazard, Is.EqualTo(0));
            Assert.That(start.FactionId, Is.EqualTo(-1));
            Assert.That(start.Discovered, Is.True);
            Assert.That(map.Systems.Count(s => s.Discovered), Is.EqualTo(1));
        }

        [Test]
        public void NamesAreUnique()
        {
            var map = GalaxyGenerator.Generate(5);

            Assert.That(map.Systems.Select(s => s.Name).Distinct().Count(), Is.EqualTo(map.Systems.Count));
        }

        [Test]
        public void SomeSystemsBelongToFactions()
        {
            var map = GalaxyGenerator.Generate(3);

            Assert.That(map.Systems.Any(s => s.FactionId >= 0), Is.True);
        }
    }

    public class NavigationTests
    {
        private static (GalaxyMap map, int neighbor, Lane lane) StartAndNeighbor(int seed = 11)
        {
            var map = GalaxyGenerator.Generate(seed);
            var lane = map.LanesOf(map.StartSystemId).OrderBy(l => l.Distance).First();
            return (map, lane.Other(map.StartSystemId), lane);
        }

        [Test]
        public void Jump_ToNeighbor_ConsumesFuel_AndDiscovers()
        {
            var (map, neighbor, lane) = StartAndNeighbor();
            var nav = new ShipNavigation(map, fuelCapacity: 100f, jumpRange: 100f);

            var result = nav.Jump(neighbor);

            Assert.That(result, Is.EqualTo(JumpResult.Ok));
            Assert.That(nav.CurrentSystemId, Is.EqualTo(neighbor));
            Assert.That(nav.Fuel.Current, Is.EqualTo(100f - lane.Distance).Within(0.01f));
            Assert.That(map.GetSystem(neighbor).Discovered, Is.True);
        }

        [Test]
        public void Jump_WithoutLane_Fails()
        {
            var map = GalaxyGenerator.Generate(11);
            var nav = new ShipNavigation(map, 1000f, 1000f);
            int notNeighbor = map.Systems.Select(s => s.Id)
                .First(id => id != map.StartSystemId && !map.TryGetLane(map.StartSystemId, id, out _));

            Assert.That(nav.Jump(notNeighbor), Is.EqualTo(JumpResult.NoLane));
        }

        [Test]
        public void Jump_TooFar_OrNoFuel_Fails()
        {
            var (map, neighbor, lane) = StartAndNeighbor();

            var shortRange = new ShipNavigation(map, 100f, lane.Distance - 0.1f);
            Assert.That(shortRange.Jump(neighbor), Is.EqualTo(JumpResult.OutOfRange));

            var smallTank = new ShipNavigation(map, lane.Distance - 0.5f, 100f);
            Assert.That(smallTank.Jump(neighbor), Is.EqualTo(JumpResult.NotEnoughFuel));
        }

        [Test]
        public void Route_ReachesEverySystem_WithUnlimitedRange()
        {
            var map = GalaxyGenerator.Generate(21);
            foreach (var system in map.Systems)
            {
                var route = RoutePlanner.FindRoute(map, map.StartSystemId, system.Id);
                Assert.That(route, Is.Not.Null, $"Keine Route zu #{system.Id}");
                Assert.That(route.Systems.First(), Is.EqualTo(map.StartSystemId));
                Assert.That(route.Systems.Last(), Is.EqualTo(system.Id));
            }
        }

        [Test]
        public void Route_IsShortest_ComparedToDirectLane()
        {
            var (map, neighbor, lane) = StartAndNeighbor();

            var route = RoutePlanner.FindRoute(map, map.StartSystemId, neighbor);

            Assert.That(route.Distance, Is.LessThanOrEqualTo(lane.Distance + 0.001f));
        }

        [Test]
        public void EnterOrbit_OnlyForLocalPlanets()
        {
            var map = GalaxyGenerator.Generate(11);
            var nav = new ShipNavigation(map, 100f, 100f);
            string local = nav.CurrentSystem.Planets[0].Id;

            Assert.That(nav.EnterOrbit(local), Is.True);
            Assert.That(nav.OrbitPlanetId, Is.EqualTo(local));
            Assert.That(nav.EnterOrbit("S999-P0"), Is.False);
        }
    }
}
