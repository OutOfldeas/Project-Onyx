using System.Linq;
using NUnit.Framework;
using Onyx.Core.Ships;

namespace Onyx.Core.Tests.Ships
{
    public class ShipBlueprintTests
    {
        private static readonly ModuleCatalog Catalog = ModuleCatalog.CreateDefault();

        private static ShipBlueprint SmallHull()
        {
            var ship = new ShipBlueprint(10, 10);
            ship.FillHull(0, 0, 6, 6);
            return ship;
        }

        [Test]
        public void Place_OnHull_Works()
        {
            var ship = SmallHull();

            Assert.That(ship.Place(Catalog.Get("reactor"), 0, 0), Is.EqualTo(PlacementResult.Ok));
            Assert.That(ship.ModuleAt(1, 1)?.Definition.Id, Is.EqualTo("reactor"));
        }

        [Test]
        public void Place_Overlapping_IsRejected()
        {
            var ship = SmallHull();
            ship.Place(Catalog.Get("reactor"), 0, 0);

            Assert.That(ship.Place(Catalog.Get("cargo"), 1, 1), Is.EqualTo(PlacementResult.Occupied));
        }

        [Test]
        public void Place_OffHull_OrOutOfBounds_IsRejected()
        {
            var ship = SmallHull();

            Assert.That(ship.Place(Catalog.Get("reactor"), 5, 5), Is.EqualTo(PlacementResult.NoHull));
            Assert.That(ship.Place(Catalog.Get("reactor"), 9, 9), Is.EqualTo(PlacementResult.OutOfBounds));
        }

        [Test]
        public void Rotation_SwapsFootprint()
        {
            var ship = SmallHull();

            Assert.That(ship.Place(Catalog.Get("engine"), 0, 0, rotated: true), Is.EqualTo(PlacementResult.Ok));
            var engine = ship.Modules.Single();
            Assert.That(engine.Width, Is.EqualTo(3));
            Assert.That(engine.Height, Is.EqualTo(2));
        }

        [Test]
        public void UniqueModule_OnlyOnce()
        {
            var ship = SmallHull();
            ship.Place(Catalog.Get("cockpit"), 0, 0);

            Assert.That(ship.Place(Catalog.Get("cockpit"), 3, 3), Is.EqualTo(PlacementResult.UniqueAlreadyPlaced));
        }

        [Test]
        public void HullUnderModule_CannotBeRemoved()
        {
            var ship = SmallHull();
            ship.Place(Catalog.Get("airlock"), 2, 2);

            Assert.That(ship.SetHull(2, 2, false), Is.False);
            ship.RemoveModuleAt(2, 2);
            Assert.That(ship.SetHull(2, 2, false), Is.True);
        }

        [Test]
        public void Clear_And_CopyFrom()
        {
            var template = ShipTemplates.CrashedFrigate(Catalog);
            var ship = new ShipBlueprint(ShipTemplates.GridWidth, ShipTemplates.GridHeight);
            int changes = 0;
            ship.Changed += () => changes++;

            ship.CopyFrom(template);
            Assert.That(ship.HullCount, Is.EqualTo(template.HullCount));
            Assert.That(ship.Modules.Count, Is.EqualTo(template.Modules.Count));

            ship.Clear();
            Assert.That(ship.HullCount, Is.EqualTo(0));
            Assert.That(ship.Modules, Is.Empty);
            Assert.That(changes, Is.EqualTo(2));
        }

        [Test]
        public void HullConnectivity_IsDetected()
        {
            var ship = new ShipBlueprint(10, 10);
            ship.FillHull(0, 0, 2, 2);
            Assert.That(ship.IsHullConnected(), Is.True);

            ship.SetHull(5, 5, true);
            Assert.That(ship.IsHullConnected(), Is.False);
        }
    }

    public class ShipStatsTests
    {
        private static readonly ModuleCatalog Catalog = ModuleCatalog.CreateDefault();

        [Test]
        public void CrashedFrigate_IsSpaceworthy_WithRange()
        {
            var frigate = ShipTemplates.CrashedFrigate(Catalog);

            var stats = ShipStats.Calculate(frigate);

            Assert.That(stats.Issues, Is.Empty);
            Assert.That(stats.PowerBalance, Is.GreaterThanOrEqualTo(0f));
            Assert.That(stats.JumpRange, Is.GreaterThan(10f));
            Assert.That(stats.FuelCapacity, Is.GreaterThan(0f));
        }

        [Test]
        public void EmptyHull_ListsMissingParts()
        {
            var ship = new ShipBlueprint(10, 10);
            ship.FillHull(0, 0, 4, 4);

            var stats = ShipStats.Calculate(ship);

            Assert.That(stats.IsSpaceworthy, Is.False);
            Assert.That(stats.Issues.Any(i => i.Contains("Cockpit")), Is.True);
            Assert.That(stats.JumpRange, Is.EqualTo(0f));
        }

        [Test]
        public void PowerDeficit_IsReported_AndBlocksJumping()
        {
            var ship = new ShipBlueprint(10, 10);
            ship.FillHull(0, 0, 10, 10);
            ship.Place(Catalog.Get("engine"), 0, 0);
            ship.Place(Catalog.Get("shield"), 4, 0);

            var stats = ShipStats.Calculate(ship);

            Assert.That(stats.PowerBalance, Is.LessThan(0f));
            Assert.That(stats.Issues.Any(i => i.StartsWith("Energiemangel")), Is.True);
            Assert.That(stats.JumpRange, Is.EqualTo(0f));
        }

        [Test]
        public void MoreEngines_MoreRange()
        {
            var ship = ShipTemplates.CrashedFrigate(Catalog);
            float before = ShipStats.Calculate(ship).JumpRange;

            ship.Place(Catalog.Get("reactor"), 20, 11);
            ship.Place(Catalog.Get("engine"), 6, 11, rotated: true);

            Assert.That(ShipStats.Calculate(ship).JumpRange, Is.GreaterThan(before));
        }
    }
}
