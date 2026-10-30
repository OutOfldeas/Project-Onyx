using System;
using System.Collections.Generic;
using System.Linq;

namespace Onyx.Core.Ships
{
    /// <summary>
    /// Aus dem Bauplan berechnete Werte des Schiffs und Hinweise, was noch fehlt.
    /// </summary>
    public sealed class ShipStats
    {
        public const float HullMassPerTile = 0.5f;
        public const int HullCostPerTile = 2;

        public float PowerProduced { get; private set; }
        public float PowerConsumed { get; private set; }
        public float PowerBalance => PowerProduced - PowerConsumed;
        public float OxygenPerMinute { get; private set; }
        public int CargoSlots { get; private set; }
        public float FuelCapacity { get; private set; }
        public float Thrust { get; private set; }
        public int CrewBeds { get; private set; }
        public float Shield { get; private set; }
        public float Mass { get; private set; }
        public int TotalCost { get; private set; }

        /// <summary>
        /// Längste Sprungverbindung in Karteneinheiten: Schub im Verhältnis zur Masse.
        /// 0, wenn das Schiff nicht flugfähig ist (kein Triebwerk oder Energiemangel).
        /// </summary>
        public float JumpRange { get; private set; }

        public IReadOnlyList<string> Issues { get; private set; }

        public bool IsSpaceworthy => Issues.Count == 0;

        public static ShipStats Calculate(ShipBlueprint blueprint)
        {
            var stats = new ShipStats();
            var issues = new List<string>();

            foreach (var module in blueprint.Modules)
            {
                var d = module.Definition;
                if (d.Power >= 0) stats.PowerProduced += d.Power;
                else stats.PowerConsumed -= d.Power;
                stats.OxygenPerMinute += d.OxygenPerMinute;
                stats.CargoSlots += d.CargoSlots;
                stats.FuelCapacity += d.FuelCapacity;
                stats.Thrust += d.Thrust;
                stats.CrewBeds += d.CrewBeds;
                stats.Shield += d.Shield;
                stats.Mass += d.Mass;
                stats.TotalCost += d.Cost;
            }

            stats.Mass += blueprint.HullCount * HullMassPerTile;
            stats.TotalCost += blueprint.HullCount * HullCostPerTile;

            bool Has(string id) => blueprint.Modules.Any(m => m.Definition.Id == id);
            if (blueprint.HullCount == 0) issues.Add("Kein Rumpf vorhanden.");
            else if (!blueprint.IsHullConnected()) issues.Add("Der Rumpf ist nicht zusammenhängend.");
            if (!Has("cockpit")) issues.Add("Ein Cockpit fehlt.");
            if (!Has("airlock")) issues.Add("Mindestens eine Luftschleuse fehlt.");
            if (stats.Thrust <= 0) issues.Add("Kein Triebwerk eingebaut.");
            if (stats.FuelCapacity <= 0) issues.Add("Kein Treibstofftank eingebaut.");
            if (stats.PowerBalance < 0) issues.Add($"Energiemangel: {stats.PowerConsumed:0} verbraucht, {stats.PowerProduced:0} erzeugt.");
            if (stats.OxygenPerMinute <= 0) issues.Add("Keine Sauerstoffversorgung.");
            if (stats.CrewBeds < 1) issues.Add("Keine Koje für die Crew.");

            bool canFly = stats.Thrust > 0 && stats.PowerBalance >= 0 && stats.Mass > 0;
            stats.JumpRange = canFly ? Math.Min(40f, 10f + 40f * stats.Thrust / stats.Mass) : 0f;
            stats.Issues = issues;
            return stats;
        }
    }

    /// <summary>Vorgefertigte Baupläne.</summary>
    public static class ShipTemplates
    {
        public const int GridWidth = 32;
        public const int GridHeight = 20;

        /// <summary>
        /// Die abgestürzte Fregatte aus Akt 1: Grundausstattung mit kleinem Reaktor,
        /// einem Triebwerk und wenig Platz. Die Crew baut sie nach und nach aus.
        /// </summary>
        public static ShipBlueprint CrashedFrigate(ModuleCatalog catalog)
        {
            var ship = new ShipBlueprint(GridWidth, GridHeight, "Fregatte");
            ship.FillHull(6, 6, 18, 8);   // Hauptrumpf
            ship.FillHull(24, 7, 3, 6);   // Bug
            ship.FillHull(3, 7, 3, 6);    // Heck

            ship.Place(catalog.Get("cockpit"), 24, 9);
            ship.Place(catalog.Get("reactor"), 8, 9);
            ship.Place(catalog.Get("engine"), 3, 8);
            ship.Place(catalog.Get("fueltank"), 10, 7);
            ship.Place(catalog.Get("o2gen"), 12, 7);
            ship.Place(catalog.Get("bunk"), 14, 11);
            ship.Place(catalog.Get("bunk"), 15, 11);
            ship.Place(catalog.Get("cargo"), 18, 7);
            ship.Place(catalog.Get("airlock"), 16, 6);
            return ship;
        }
    }
}
