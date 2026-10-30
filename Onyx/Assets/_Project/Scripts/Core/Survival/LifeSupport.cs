using System;

namespace Onyx.Core.Survival
{
    public enum VitalState
    {
        /// <summary>Handlungsfähig.</summary>
        Alive,

        /// <summary>Kampfunfähig, wartet auf Wiederbelebung durch Mitspieler.</summary>
        Downed,
    }

    /// <summary>
    /// Sauerstoff, Schild und Gesundheit eines Charakters. Läuft beim Host;
    /// das Spiel ruft <see cref="Tick"/> regelmäßig mit der aktuellen Umgebung auf.
    /// </summary>
    public sealed class LifeSupport
    {
        private readonly LifeSupportSettings settings;
        private float timeSinceShieldHit;

        public ResourcePool Oxygen { get; }
        public ResourcePool Shield { get; }
        public ResourcePool Health { get; }

        public VitalState State { get; private set; } = VitalState.Alive;

        /// <summary>Wird ausgelöst, wenn der Charakter kampfunfähig wird.</summary>
        public event Action Downed;

        /// <summary>Wird ausgelöst, wenn der Charakter wiederbelebt wurde.</summary>
        public event Action Revived;

        public LifeSupport(LifeSupportSettings settings = null)
        {
            this.settings = settings ?? new LifeSupportSettings();
            Oxygen = new ResourcePool(this.settings.OxygenCapacity);
            Shield = new ResourcePool(this.settings.ShieldCapacity);
            Health = new ResourcePool(this.settings.HealthCapacity);
            timeSinceShieldHit = this.settings.ShieldRechargeDelay;
        }

        /// <summary>Schreitet die Simulation um <paramref name="deltaSeconds"/> voran.</summary>
        public void Tick(float deltaSeconds, Surroundings surroundings)
        {
            if (deltaSeconds < 0f || float.IsNaN(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "Zeitschritt darf nicht negativ sein.");
            }

            if (deltaSeconds == 0f)
            {
                return;
            }

            // Sauerstoff: nachfüllen oder verbrauchen. Auch Kampfunfähige atmen weiter.
            // Reicht der Vorrat nicht für den ganzen Zeitschritt, zählt nur die Zeit ohne Luft als Erstickung.
            float secondsWithoutOxygen = 0f;
            if (surroundings.OxygenRefillPerSecond > 0f)
            {
                Oxygen.Refill(surroundings.OxygenRefillPerSecond * deltaSeconds);
            }
            else if (!surroundings.Breathable && settings.OxygenUsePerSecond > 0f)
            {
                float needed = settings.OxygenUsePerSecond * deltaSeconds;
                float taken = Oxygen.Drain(needed);
                secondsWithoutOxygen = (needed - taken) / settings.OxygenUsePerSecond;
            }

            if (State == VitalState.Downed)
            {
                return;
            }

            if (surroundings.HazardDamagePerSecond > 0f)
            {
                ApplyDamage(surroundings.HazardDamagePerSecond * deltaSeconds);
            }
            else
            {
                timeSinceShieldHit += deltaSeconds;
                if (timeSinceShieldHit >= settings.ShieldRechargeDelay)
                {
                    Shield.Refill(settings.ShieldRechargePerSecond * deltaSeconds);
                }
            }

            if (secondsWithoutOxygen > 0f)
            {
                // Erstickungsschaden umgeht den Schild.
                Health.Drain(settings.SuffocationDamagePerSecond * secondsWithoutOxygen);
            }

            CheckDowned();
        }

        /// <summary>
        /// Schaden von außen (Angriffe, Explosionen). Trifft zuerst den Schild, der Rest die Gesundheit.
        /// </summary>
        public void ApplyDamage(float amount)
        {
            if (amount < 0f || float.IsNaN(amount))
            {
                throw new ArgumentOutOfRangeException(nameof(amount), amount, "Schaden darf nicht negativ sein.");
            }

            if (State == VitalState.Downed || amount == 0f)
            {
                return;
            }

            timeSinceShieldHit = 0f;
            float absorbed = Shield.Drain(amount);
            Health.Drain(amount - absorbed);
            CheckDowned();
        }

        /// <summary>Wiederbelebung durch einen Mitspieler. Nur im Zustand <see cref="VitalState.Downed"/> wirksam.</summary>
        /// <returns>true, wenn der Charakter wiederbelebt wurde.</returns>
        public bool Revive()
        {
            if (State != VitalState.Downed)
            {
                return false;
            }

            Health.Refill(settings.ReviveHealth);
            State = VitalState.Alive;
            Revived?.Invoke();
            return true;
        }

        private void CheckDowned()
        {
            if (State == VitalState.Alive && Health.IsEmpty)
            {
                State = VitalState.Downed;
                Downed?.Invoke();
            }
        }
    }
}
