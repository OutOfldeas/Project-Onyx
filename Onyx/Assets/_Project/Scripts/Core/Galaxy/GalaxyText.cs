namespace Onyx.Core.Galaxy
{
    /// <summary>Deutsche Anzeigenamen für Galaxie-Begriffe (Sternenkarte, Konsole).</summary>
    public static class GalaxyText
    {
        public static string Star(StarType star)
        {
            switch (star)
            {
                case StarType.RedDwarf: return "Roter Zwerg";
                case StarType.BlueGiant: return "Blauer Riese";
                case StarType.WhiteDwarf: return "Weißer Zwerg";
                default: return "Gelber Zwerg";
            }
        }

        public static string Planet(PlanetType type)
        {
            switch (type)
            {
                case PlanetType.RustDesert: return "Rostwüste";
                case PlanetType.Savanna: return "Savanne";
                case PlanetType.Jungle: return "Dschungel";
                case PlanetType.Ice: return "Eiswelt";
                case PlanetType.Toxic: return "Giftwelt";
                case PlanetType.Volcanic: return "Vulkanwelt";
                case PlanetType.Barren: return "Ödland";
                case PlanetType.GasGiant: return "Gasriese";
                default: return type.ToString();
            }
        }

        /// <summary>Warum ein Sprung nicht geht – oder null, wenn er geht.</summary>
        public static string JumpProblem(JumpResult result)
        {
            switch (result)
            {
                case JumpResult.NoLane: return "Keine direkte Verbindung";
                case JumpResult.OutOfRange: return "Außer Sprungreichweite";
                case JumpResult.NotEnoughFuel: return "Zu wenig Treibstoff";
                default: return null;
            }
        }

        /// <summary>Gefahrenstufe 0–3 als Wort.</summary>
        public static string Hazard(int hazard)
        {
            switch (hazard)
            {
                case 0: return "sicher";
                case 1: return "rau";
                case 2: return "gefährlich";
                default: return "tödlich";
            }
        }
    }
}
