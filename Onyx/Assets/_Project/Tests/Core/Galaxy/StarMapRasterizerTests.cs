using System.Linq;
using NUnit.Framework;
using Onyx.Core.Galaxy;

namespace Onyx.Core.Tests.Galaxy
{
    public class StarMapRasterizerTests
    {
        private static StarMapView ViewFor(GalaxyMap map) => new StarMapView { CurrentSystemId = map.StartSystemId, JumpRange = 20f };

        [Test]
        public void Image_HasGameResolution()
        {
            var map = GalaxyGenerator.Generate(5);

            var image = StarMapRasterizer.Render(map, ViewFor(map));

            Assert.That(image.Width, Is.EqualTo(480));
            Assert.That(image.Height, Is.EqualTo(270));
            Assert.That(image.Pixels.Length, Is.EqualTo(480 * 270));
        }

        [Test]
        public void SameInput_SameImage()
        {
            var map = GalaxyGenerator.Generate(8);

            var a = StarMapRasterizer.Render(map, ViewFor(map));
            var b = StarMapRasterizer.Render(map, ViewFor(map));

            Assert.That(a.Pixels, Is.EqualTo(b.Pixels));
        }

        [Test]
        public void AllSystems_InsideImage()
        {
            var map = GalaxyGenerator.Generate(13);

            foreach (var system in map.Systems)
            {
                var (x, y) = StarMapRasterizer.ToPixel(map, system);
                Assert.That(x, Is.InRange(0, 479));
                Assert.That(y, Is.InRange(0, 269));
            }
        }

        [Test]
        public void StartView_ShowsOnlyStartAndNeighbors()
        {
            var map = GalaxyGenerator.Generate(21);

            var image = StarMapRasterizer.Render(map, ViewFor(map));

            int neighbors = map.LanesOf(map.StartSystemId).Count;
            Assert.That(image.SystemPositions.Count, Is.EqualTo(neighbors + 1));
            Assert.That(image.DiscoveredVisible, Is.EquivalentTo(new[] { map.StartSystemId }));
        }

        [Test]
        public void RevealAll_ShowsEverything()
        {
            var map = GalaxyGenerator.Generate(21);
            var view = ViewFor(map);
            view.RevealAll = true;

            var image = StarMapRasterizer.Render(map, view);

            Assert.That(image.SystemPositions.Count, Is.EqualTo(map.Systems.Count));
        }

        [Test]
        public void CurrentSystem_HasShipRing()
        {
            var map = GalaxyGenerator.Generate(3);

            var image = StarMapRasterizer.Render(map, ViewFor(map));

            var (x, y) = image.SystemPositions[map.StartSystemId];
            Assert.That(image.Get(x + 9, y), Is.EqualTo(MapColor.Amber));
            Assert.That(image.Get(x, y), Is.EqualTo(MapColor.Bone));
        }

        [Test]
        public void Route_IsDrawnInAmber()
        {
            var map = GalaxyGenerator.Generate(3);
            var view = ViewFor(map);
            var target = map.LanesOf(map.StartSystemId).First().Other(map.StartSystemId);
            view.Route = new[] { map.StartSystemId, target };

            var withRoute = StarMapRasterizer.Render(map, view);
            var without = StarMapRasterizer.Render(map, ViewFor(map));

            int amberWith = withRoute.Pixels.Count(p => p == MapColor.Amber);
            int amberWithout = without.Pixels.Count(p => p == MapColor.Amber);
            Assert.That(amberWith, Is.GreaterThan(amberWithout));
        }

        [Test]
        public void Factions_HaveLabelsAndBorders()
        {
            var map = GalaxyGenerator.Generate(3);

            var image = StarMapRasterizer.Render(map, ViewFor(map));

            Assert.That(image.FactionLabels.Count, Is.GreaterThan(0));
            Assert.That(image.Pixels.Any(p => p == MapColor.Red || p == MapColor.Cyan || p == MapColor.Orange), Is.True);
        }

        [Test]
        public void ValueNoise_StaysInRange()
        {
            for (int i = 0; i < 200; i++)
            {
                float v = StarMapRasterizer.ValueNoise(i * 0.37f, i * 0.91f, 42);
                Assert.That(v, Is.InRange(0f, 1f));
            }
        }
    }
}
