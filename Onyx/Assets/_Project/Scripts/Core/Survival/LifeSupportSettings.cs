namespace Onyx.Core.Survival
{
    /// <summary>
    /// Zahlenwerte für die Lebenserhaltung eines Charakters. Startwerte für Playtests;
    /// später kommen sie aus Ausrüstung und Skills.
    /// </summary>
    public sealed class LifeSupportSettings
    {
        /// <summary>Sauerstoff-Tankgröße.</summary>
        public float OxygenCapacity = 100f;

        /// <summary>Verbrauch pro Sekunde außerhalb atembarer Luft (100 / 0,5 = 200 s ≈ 3:20 min).</summary>
        public float OxygenUsePerSecond = 0.5f;

        /// <summary>Gesundheit; sinkt bei Sauerstoffmangel und Schaden, der den Schild durchschlägt.</summary>
        public float HealthCapacity = 100f;

        /// <summary>Gesundheitsverlust pro Sekunde, solange der Sauerstoff leer ist.</summary>
        public float SuffocationDamagePerSecond = 10f;

        /// <summary>Schildenergie; fängt Schaden ab, bevor er die Gesundheit trifft.</summary>
        public float ShieldCapacity = 50f;

        /// <summary>Schild-Aufladung pro Sekunde, sobald er eine Weile keinen Schaden genommen hat.</summary>
        public float ShieldRechargePerSecond = 5f;

        /// <summary>Wartezeit nach dem letzten Treffer, bevor der Schild wieder lädt.</summary>
        public float ShieldRechargeDelay = 3f;

        /// <summary>Gesundheit nach einer Wiederbelebung durch Mitspieler.</summary>
        public float ReviveHealth = 25f;
    }
}
