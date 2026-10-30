using NUnit.Framework;
using Onyx.Core.Survival;

namespace Onyx.Core.Tests.Survival
{
    public class LifeSupportTests
    {
        private static LifeSupportSettings Settings() => new LifeSupportSettings
        {
            OxygenCapacity = 100f,
            OxygenUsePerSecond = 1f,
            HealthCapacity = 100f,
            SuffocationDamagePerSecond = 10f,
            ShieldCapacity = 50f,
            ShieldRechargePerSecond = 5f,
            ShieldRechargeDelay = 3f,
            ReviveHealth = 25f,
        };

        [Test]
        public void Outside_UsesOxygen()
        {
            var life = new LifeSupport(Settings());

            life.Tick(10f, Surroundings.Outside);

            Assert.That(life.Oxygen.Current, Is.EqualTo(90f).Within(0.001f));
        }

        [Test]
        public void Breathable_DoesNotUseOxygen()
        {
            var life = new LifeSupport(Settings());

            life.Tick(10f, new Surroundings(breathable: true));

            Assert.That(life.Oxygen.IsFull, Is.True);
        }

        [Test]
        public void RefillStation_RefillsOxygen()
        {
            var life = new LifeSupport(Settings());
            life.Tick(50f, Surroundings.Outside);

            life.Tick(2f, new Surroundings(breathable: true, oxygenRefillPerSecond: 20f));

            Assert.That(life.Oxygen.Current, Is.EqualTo(90f).Within(0.001f));
        }

        [Test]
        public void EmptyOxygen_DamagesHealth_ThenDowns()
        {
            var life = new LifeSupport(Settings());
            bool downedFired = false;
            life.Downed += () => downedFired = true;

            life.Tick(100f, Surroundings.Outside); // Sauerstoff leer
            life.Tick(5f, Surroundings.Outside);
            Assert.That(life.Health.Current, Is.EqualTo(50f).Within(0.001f));
            Assert.That(life.Shield.IsFull, Is.True, "Erstickung umgeht den Schild");

            life.Tick(5f, Surroundings.Outside);
            Assert.That(life.State, Is.EqualTo(VitalState.Downed));
            Assert.That(downedFired, Is.True);
        }

        [Test]
        public void OxygenRunningOutMidTick_OnlyCountsTimeWithoutAir()
        {
            var life = new LifeSupport(Settings());
            life.Tick(98f, Surroundings.Outside); // noch 2 Sekunden Luft

            life.Tick(5f, Surroundings.Outside);  // davon 3 Sekunden ohne Luft

            Assert.That(life.Health.Current, Is.EqualTo(70f).Within(0.001f));
        }

        [Test]
        public void Damage_HitsShieldFirst()
        {
            var life = new LifeSupport(Settings());

            life.ApplyDamage(70f);

            Assert.That(life.Shield.IsEmpty, Is.True);
            Assert.That(life.Health.Current, Is.EqualTo(80f).Within(0.001f));
        }

        [Test]
        public void Shield_RechargesOnlyAfterDelay()
        {
            var life = new LifeSupport(Settings());
            life.ApplyDamage(20f);

            life.Tick(2f, new Surroundings(breathable: true));
            Assert.That(life.Shield.Current, Is.EqualTo(30f).Within(0.001f), "noch in der Wartezeit");

            life.Tick(2f, new Surroundings(breathable: true));
            Assert.That(life.Shield.Current, Is.GreaterThan(30f));
        }

        [Test]
        public void Hazard_DrainsShieldOverTime()
        {
            var life = new LifeSupport(Settings());

            life.Tick(2f, new Surroundings(breathable: true, hazardDamagePerSecond: 10f));

            Assert.That(life.Shield.Current, Is.EqualTo(30f).Within(0.001f));
            Assert.That(life.Health.IsFull, Is.True);
        }

        [Test]
        public void Revive_RestoresSomeHealth()
        {
            var life = new LifeSupport(Settings());
            bool revivedFired = false;
            life.Revived += () => revivedFired = true;
            life.ApplyDamage(500f);
            Assert.That(life.State, Is.EqualTo(VitalState.Downed));

            bool revived = life.Revive();

            Assert.That(revived, Is.True);
            Assert.That(revivedFired, Is.True);
            Assert.That(life.State, Is.EqualTo(VitalState.Alive));
            Assert.That(life.Health.Current, Is.EqualTo(25f).Within(0.001f));
        }

        [Test]
        public void Revive_WhenAlive_DoesNothing()
        {
            var life = new LifeSupport(Settings());

            Assert.That(life.Revive(), Is.False);
            Assert.That(life.Health.IsFull, Is.True);
        }

        [Test]
        public void Downed_TakesNoFurtherDamage()
        {
            var life = new LifeSupport(Settings());
            life.ApplyDamage(500f);

            life.ApplyDamage(10f);
            life.Tick(10f, new Surroundings(breathable: false, hazardDamagePerSecond: 10f));

            Assert.That(life.Health.IsEmpty, Is.True);
            Assert.That(life.State, Is.EqualTo(VitalState.Downed));
        }
    }
}
