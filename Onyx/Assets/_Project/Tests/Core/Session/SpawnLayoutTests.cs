using System;
using System.Collections.Generic;
using NUnit.Framework;
using Onyx.Core.Session;

namespace Onyx.Core.Tests.Session
{
    public class SpawnLayoutTests
    {
        [Test]
        public void FirstFourSlots_AreDistinct()
        {
            var seen = new HashSet<(float, float)>();
            for (int slot = 0; slot < GameConstants.MaxPlayers; slot++)
            {
                Assert.That(seen.Add(SpawnLayout.GetSpawnOffset(slot)), Is.True, $"Platz {slot} doppelt belegt");
            }
        }

        [Test]
        public void FirstRing_UsesRadius()
        {
            var (x, y) = SpawnLayout.GetSpawnOffset(0);

            Assert.That(Math.Abs(x), Is.EqualTo(SpawnLayout.Radius));
            Assert.That(Math.Abs(y), Is.EqualTo(SpawnLayout.Radius));
        }

        [Test]
        public void ExtraSlots_MoveFurtherOut()
        {
            var (x, _) = SpawnLayout.GetSpawnOffset(GameConstants.MaxPlayers);

            Assert.That(Math.Abs(x), Is.EqualTo(SpawnLayout.Radius * 2));
        }

        [Test]
        public void NegativeSlot_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => SpawnLayout.GetSpawnOffset(-1));
        }
    }
}
