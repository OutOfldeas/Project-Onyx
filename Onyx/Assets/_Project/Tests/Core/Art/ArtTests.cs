using System.Linq;
using NUnit.Framework;
using Onyx.Core.Art;
using Onyx.Core.CrashSite;

namespace Onyx.Core.Tests.Art
{
    public class CrashSiteArtTests
    {
        [Test]
        public void Image_Is16PixelsPerTile()
        {
            var layout = CrashSiteGenerator.Generate(4);

            var img = CrashSiteArt.Render(layout);

            Assert.That(img.Width, Is.EqualTo(layout.Width * 16));
            Assert.That(img.Height, Is.EqualTo(layout.Height * 16));
            Assert.That(img.Pixels.All(p => (p >> 24) == 0xFF), Is.True, "Boden ist überall deckend");
        }

        [Test]
        public void SameSeed_SameImage()
        {
            var a = CrashSiteArt.Render(CrashSiteGenerator.Generate(77));
            var b = CrashSiteArt.Render(CrashSiteGenerator.Generate(77));

            Assert.That(a.Pixels, Is.EqualTo(b.Pixels));
        }

        [Test]
        public void PodFloor_IsSteel()
        {
            var layout = CrashSiteGenerator.Generate(4);
            var img = CrashSiteArt.Render(layout);
            var (tx, ty) = layout.WorldToTile(0.5f, 0.5f);

            uint center = img.Get(tx * 16 + 5, ty * 16 + 5);

            Assert.That(new[] { Rust.S2, Rust.S3, Rust.S4 }, Does.Contain(center));
        }

        [Test]
        public void Rock_WithOpenGroundBelow_HasDarkFrontEdge()
        {
            var layout = CrashSiteGenerator.Generate(4);
            var img = CrashSiteArt.Render(layout);

            int found = 0;
            for (int ty = 1; ty < layout.Height - 1 && found < 5; ty++)
                for (int tx = 1; tx < layout.Width - 1 && found < 5; tx++)
                {
                    if (layout.Get(tx, ty) == SiteTile.Rock && layout.Get(tx, ty - 1) == SiteTile.Sand)
                    {
                        Assert.That(img.Get(tx * 16 + 8, ty * 16), Is.EqualTo(Rust.R0), $"Fels {tx},{ty}");
                        found++;
                    }
                }

            Assert.That(found, Is.GreaterThan(0));
        }
    }

    public class CharacterArtTests
    {
        private const uint Role = Rust.Cyan;

        [Test]
        public void Sprite_HasExpectedSize()
        {
            var img = CharacterArt.Render(Facing.Down, 0, Role);

            Assert.That(img.Width, Is.EqualTo(16));
            Assert.That(img.Height, Is.EqualTo(32));
        }

        [Test]
        public void FeetStandOnFeetRow()
        {
            var img = CharacterArt.Render(Facing.Down, 0, Role);

            bool feetRowFilled = Enumerable.Range(0, 16).Any(x => (img.Get(x, CharacterArt.FeetRow) >> 24) != 0);
            bool belowEmpty = Enumerable.Range(0, 16).All(x => (img.Get(x, CharacterArt.FeetRow - 1) >> 24) == 0);
            Assert.That(feetRowFilled && belowEmpty, Is.True);
        }

        [Test]
        public void Front_ShowsVisorInRoleColor()
        {
            var front = CharacterArt.Render(Facing.Down, 0, Role);

            Assert.That(front.Pixels.Count(p => p == Role), Is.GreaterThan(10));
        }

        [Test]
        public void Left_IsMirroredRight()
        {
            var right = CharacterArt.Render(Facing.Right, 1, Role);
            var left = CharacterArt.Render(Facing.Left, 1, Role);

            for (int y = 0; y < 32; y++)
                for (int x = 0; x < 16; x++)
                    Assert.That(left.Get(15 - x, y), Is.EqualTo(right.Get(x, y)), $"{x},{y}");
        }

        [Test]
        public void WalkFrames_DifferFromStanding()
        {
            var stand = CharacterArt.Render(Facing.Down, 0, Role);
            var step = CharacterArt.Render(Facing.Down, 1, Role);

            Assert.That(step.Pixels, Is.Not.EqualTo(stand.Pixels));
            Assert.That(CharacterArt.Render(Facing.Down, 4, Role).Pixels, Is.EqualTo(stand.Pixels), "Bilder wiederholen sich");
        }
    }

    public class StationArtTests
    {
        [TestCase(StationKind.OxygenGenerator)]
        [TestCase(StationKind.Reactor)]
        [TestCase(StationKind.Engine)]
        public void RepairedLooksDifferent_AndShowsGlow(StationKind kind)
        {
            var broken = StationArt.Render(kind, false);
            var repaired = StationArt.Render(kind, true);

            Assert.That(broken.Width, Is.EqualTo(16));
            Assert.That(broken.Height, Is.EqualTo(24));
            Assert.That(repaired.Pixels, Is.Not.EqualTo(broken.Pixels));
            Assert.That(broken.Pixels, Does.Contain(Rust.Red), "kaputt: rote Anzeige");
            Assert.That(repaired.Pixels, Does.Contain(Rust.Lime), "läuft: grüne Anzeige");
        }
    }
}
