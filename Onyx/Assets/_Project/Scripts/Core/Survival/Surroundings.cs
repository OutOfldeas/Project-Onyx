namespace Onyx.Core.Survival
{
    /// <summary>
    /// Was die Umgebung an der Position eines Charakters gerade bewirkt.
    /// Wird pro Tick vom Spiel ermittelt (z. B. „steht in der Kapsel“, „im Giftnebel“).
    /// </summary>
    public readonly struct Surroundings
    {
        /// <summary>Atembare Luft: Kein Sauerstoffverbrauch (Kapsel, Schiff, Außenposten).</summary>
        public readonly bool Breathable;

        /// <summary>Sauerstoff-Nachfüllung pro Sekunde (z. B. an der Kapsel). 0 = keine.</summary>
        public readonly float OxygenRefillPerSecond;

        /// <summary>Umweltschaden pro Sekunde (Hitze, Strahlung, Gift). Trifft zuerst den Schild.</summary>
        public readonly float HazardDamagePerSecond;

        public Surroundings(bool breathable, float oxygenRefillPerSecond = 0f, float hazardDamagePerSecond = 0f)
        {
            Breathable = breathable;
            OxygenRefillPerSecond = oxygenRefillPerSecond;
            HazardDamagePerSecond = hazardDamagePerSecond;
        }

        /// <summary>Draußen auf einem Planeten ohne besondere Gefahren.</summary>
        public static Surroundings Outside => new Surroundings(false);
    }
}
