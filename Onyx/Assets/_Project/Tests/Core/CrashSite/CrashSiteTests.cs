using System.Linq;
using NUnit.Framework;
using Onyx.Core.CrashSite;
using Onyx.Core.Session;

namespace Onyx.Core.Tests.CrashSite
{
    public class CrashSiteTests
    {
        [Test]
        public void Generate_IsDeterministic()
        {
            var a = CrashSiteGenerator.Generate(42);
            var b = CrashSiteGenerator.Generate(42);

            for (int y = 0; y < a.Height; y++)
            {
                for (int x = 0; x < a.Width; x++)
                {
                    Assert.That(a.Get(x, y), Is.EqualTo(b.Get(x, y)));
                }
            }
        }

        [TestCase(1)]
        [TestCase(20261030)]
        [TestCase(-77)]
        public void SpawnPlaces_AreFreeAndInsidePod(int seed)
        {
            var layout = CrashSiteGenerator.Generate(seed);

            for (int slot = 0; slot < GameConstants.MaxPlayers; slot++)
            {
                var (x, y) = SpawnLayout.GetSpawnOffset(slot);
                Assert.That(layout.IsBlockedAt(x, y), Is.False, $"Platz {slot}");
                Assert.That(layout.ZoneAt(x, y), Is.EqualTo(SiteZone.Pod), $"Platz {slot}");
            }
        }

        [TestCase(1)]
        [TestCase(20261030)]
        [TestCase(-77)]
        public void EveryStation_IsReachableFromPod(int seed)
        {
            var layout = CrashSiteGenerator.Generate(seed);
            var reachable = Flood(layout, layout.OriginX, layout.OriginY);

            foreach (var point in layout.RepairPoints)
            {
                bool neighborReachable =
                    reachable[point.TileX + 1, point.TileY] || reachable[point.TileX - 1, point.TileY] ||
                    reachable[point.TileX, point.TileY + 1] || reachable[point.TileX, point.TileY - 1];
                Assert.That(neighborReachable, Is.True, point.Name);
            }
        }

        [Test]
        public void Map_IsClosedByRocks()
        {
            var layout = CrashSiteGenerator.Generate(5);

            for (int x = 0; x < layout.Width; x++)
            {
                Assert.That(layout.IsBlocked(x, 0), Is.True);
                Assert.That(layout.IsBlocked(x, layout.Height - 1), Is.True);
            }
        }

        [Test]
        public void Pod_RefillsOxygen_OutsideDoesNot()
        {
            var site = new CrashSiteState(CrashSiteGenerator.Generate(3));

            var pod = site.SurroundingsAt(0.5f, 0.5f, 1f);
            var outside = site.SurroundingsAt(-8f, 0.5f, 1f);

            Assert.That(pod.Breathable, Is.True);
            Assert.That(pod.OxygenRefillPerSecond, Is.GreaterThan(0f));
            Assert.That(outside.Breathable, Is.False);
            Assert.That(outside.HazardDamagePerSecond, Is.EqualTo(0f));
        }

        [Test]
        public void Night_IsCold_OutsideOnly()
        {
            var site = new CrashSiteState(CrashSiteGenerator.Generate(3));

            Assert.That(site.SurroundingsAt(-8f, 0.5f, 0f).HazardDamagePerSecond, Is.GreaterThan(0f));
            Assert.That(site.SurroundingsAt(0.5f, 0.5f, 0f).HazardDamagePerSecond, Is.EqualTo(0f));
        }

        [Test]
        public void Wreck_GetsAir_OnlyAfterOxygenGenerator()
        {
            var site = new CrashSiteState(CrashSiteGenerator.Generate(3));
            var (wx, wy) = WreckFloor(site.Layout);

            Assert.That(site.SurroundingsAt(wx, wy, 0f).Breathable, Is.False);
            Assert.That(site.SurroundingsAt(wx, wy, 0f).HazardDamagePerSecond, Is.EqualTo(0f), "Wrack schützt vor Kälte");

            var o2 = site.Point(StationKind.OxygenGenerator);
            site.ApplySyncedProgress(o2.Index, o2.Work);

            Assert.That(site.SurroundingsAt(wx, wy, 0f).Breathable, Is.True);
        }

        [Test]
        public void Engine_NeedsReactorFirst()
        {
            var site = new CrashSiteState(CrashSiteGenerator.Generate(3));
            int engine = site.Point(StationKind.Engine).Index;

            Assert.That(site.Join(engine, 1), Is.EqualTo(JoinResult.MissingRequirement));

            var reactor = site.Point(StationKind.Reactor);
            site.ApplySyncedProgress(reactor.Index, reactor.Work);

            Assert.That(site.Join(engine, 1), Is.EqualTo(JoinResult.Ok));
        }

        [Test]
        public void TwoPlayers_RepairFaster_AndRaiseEvent()
        {
            var site = new CrashSiteState(CrashSiteGenerator.Generate(3));
            var o2 = site.Point(StationKind.OxygenGenerator);
            RepairPoint repaired = null;
            site.StationRepaired += p => repaired = p;

            site.Join(o2.Index, 1);
            site.Join(o2.Index, 2);
            site.Tick(o2.Work / 2.5f + 0.01f);

            Assert.That(site.IsRepaired(StationKind.OxygenGenerator), Is.True);
            Assert.That(repaired, Is.SameAs(o2));
        }

        [Test]
        public void Player_WorksOnOneStationAtATime()
        {
            var site = new CrashSiteState(CrashSiteGenerator.Generate(3));
            var o2 = site.Point(StationKind.OxygenGenerator);
            var reactor = site.Point(StationKind.Reactor);

            site.Join(o2.Index, 7);
            site.Join(reactor.Index, 7);

            Assert.That(site.WorkingOn(7), Is.EqualTo(reactor.Index));
            Assert.That(site.Job(o2.Index).WorkerCount, Is.EqualTo(0));
        }

        [Test]
        public void NearestOpen_FindsStationNextToPlayer()
        {
            var site = new CrashSiteState(CrashSiteGenerator.Generate(3));
            var reactor = site.Point(StationKind.Reactor);
            var (cx, cy) = site.Layout.TileCenter(reactor.TileX, reactor.TileY - 1);

            Assert.That(site.NearestOpen(cx, cy, 1.6f), Is.SameAs(reactor));
            Assert.That(site.NearestOpen(0f, 0f, 1.6f), Is.Null);
        }

        [Test]
        public void WorldState_StartsWithBrokenEngine()
        {
            var world = WorldState.CreateNew(9);

            Assert.That(world.CrashSite.AllRepaired, Is.False);
            Assert.That(world.Navigation.JumpRange, Is.EqualTo(0f));
        }

        private static (float x, float y) WreckFloor(CrashSiteLayout layout)
        {
            var r = layout.WreckInterior;
            for (int y = r.MinY; y <= r.MaxY; y++)
            {
                for (int x = r.MinX; x <= r.MaxX; x++)
                {
                    if (layout.Get(x, y) == SiteTile.WreckFloor) return layout.TileCenter(x, y);
                }
            }

            Assert.Fail("Kein Wrackboden gefunden");
            return default;
        }

        private static bool[,] Flood(CrashSiteLayout layout, int startX, int startY)
        {
            var seen = new bool[layout.Width, layout.Height];
            var queue = new System.Collections.Generic.Queue<(int, int)>();
            queue.Enqueue((startX, startY));
            seen[startX, startY] = true;
            while (queue.Count > 0)
            {
                var (x, y) = queue.Dequeue();
                foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                {
                    int nx = x + dx;
                    int ny = y + dy;
                    if (!layout.InBounds(nx, ny) || seen[nx, ny] || layout.IsBlocked(nx, ny)) continue;
                    seen[nx, ny] = true;
                    queue.Enqueue((nx, ny));
                }
            }

            return seen;
        }
    }
}
