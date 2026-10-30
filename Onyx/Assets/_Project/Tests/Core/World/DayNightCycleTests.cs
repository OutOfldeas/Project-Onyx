using NUnit.Framework;
using Onyx.Core.World;

namespace Onyx.Core.Tests.World
{
    public class DayNightCycleTests
    {
        [TestCase(0.0f, DayPhase.Night)]
        [TestCase(0.25f, DayPhase.Dawn)]
        [TestCase(0.5f, DayPhase.Day)]
        [TestCase(0.75f, DayPhase.Dusk)]
        [TestCase(0.9f, DayPhase.Night)]
        public void PhaseAt_MatchesTimeOfDay(float time, DayPhase expected)
        {
            Assert.That(DayNightCycle.PhaseAt(time), Is.EqualTo(expected));
        }

        [Test]
        public void Daylight_FullAtNoon_ZeroAtMidnight()
        {
            Assert.That(DayNightCycle.DaylightAt(0.5f), Is.EqualTo(1f));
            Assert.That(DayNightCycle.DaylightAt(0f), Is.EqualTo(0f));
        }

        [Test]
        public void Daylight_RisesDuringDawn()
        {
            float early = DayNightCycle.DaylightAt(0.22f);
            float late = DayNightCycle.DaylightAt(0.28f);

            Assert.That(early, Is.GreaterThan(0f));
            Assert.That(late, Is.GreaterThan(early));
            Assert.That(late, Is.LessThan(1f));
        }

        [Test]
        public void Tick_AdvancesAndWrapsIntoNextDay()
        {
            var cycle = new DayNightCycle(dayLengthSeconds: 100f, startTimeOfDay: 0.9f);

            cycle.Tick(20f);

            Assert.That(cycle.TimeOfDay, Is.EqualTo(0.1f).Within(0.0001f));
            Assert.That(cycle.DayCount, Is.EqualTo(1));
        }

        [Test]
        public void SetTimeOfDay_Wraps()
        {
            var cycle = new DayNightCycle();

            cycle.SetTimeOfDay(1.5f);

            Assert.That(cycle.TimeOfDay, Is.EqualTo(0.5f).Within(0.0001f));
        }
    }
}
