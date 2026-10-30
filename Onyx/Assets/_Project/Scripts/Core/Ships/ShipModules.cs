using System;
using System.Collections.Generic;
using System.Linq;

namespace Onyx.Core.Ships
{
    public enum ModuleCategory
    {
        Command,
        Power,
        Propulsion,
        LifeSupport,
        Storage,
        Crew,
        Production,
        Defense,
    }

    /// <summary>
    /// Bauteil, das in der Schiffswerft auf Rumpfkacheln platziert wird.
    /// Größen in Kacheln (16 px). Werte sind erste Annahmen für Playtests.
    /// </summary>
    public sealed class ModuleDefinition
    {
        public string Id { get; }
        public string Name { get; }
        public ModuleCategory Category { get; }
        public int Width { get; }
        public int Height { get; }

        /// <summary>Positiv = erzeugt Energie, negativ = verbraucht.</summary>
        public float Power { get; set; }

        public float OxygenPerMinute { get; set; }
        public int CargoSlots { get; set; }
        public float FuelCapacity { get; set; }
        public float Thrust { get; set; }
        public int CrewBeds { get; set; }
        public float Shield { get; set; }
        public float Mass { get; set; }

        /// <summary>Baukosten in Schrott.</summary>
        public int Cost { get; set; }

        /// <summary>Darf nur einmal pro Schiff verbaut werden.</summary>
        public bool Unique { get; set; }

        public ModuleDefinition(string id, string name, ModuleCategory category, int width, int height)
        {
            if (width <= 0 || height <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(width), "Module brauchen mindestens 1 × 1 Kachel.");
            }

            Id = id;
            Name = name;
            Category = category;
            Width = width;
            Height = height;
        }
    }

    public sealed class ModuleCatalog
    {
        private readonly Dictionary<string, ModuleDefinition> modules;

        public ModuleCatalog(IEnumerable<ModuleDefinition> definitions)
        {
            modules = definitions.ToDictionary(m => m.Id);
        }

        public IEnumerable<ModuleDefinition> All => modules.Values;

        public ModuleDefinition Get(string id)
        {
            return modules.TryGetValue(id, out var m) ? m : throw new KeyNotFoundException($"Unbekanntes Modul: {id}");
        }

        public bool TryGet(string id, out ModuleDefinition definition) => modules.TryGetValue(id, out definition);

        public static ModuleCatalog CreateDefault()
        {
            return new ModuleCatalog(new[]
            {
                new ModuleDefinition("cockpit", "Cockpit", ModuleCategory.Command, 2, 2) { Power = -1, Mass = 4, Cost = 40, Unique = true },
                new ModuleDefinition("reactor", "Reaktor", ModuleCategory.Power, 2, 2) { Power = 10, Mass = 8, Cost = 60 },
                new ModuleDefinition("solar", "Solarpaneel", ModuleCategory.Power, 1, 2) { Power = 2, Mass = 1, Cost = 15 },
                new ModuleDefinition("engine", "Triebwerk", ModuleCategory.Propulsion, 2, 3) { Power = -3, Thrust = 30, Mass = 6, Cost = 50 },
                new ModuleDefinition("fueltank", "Treibstofftank", ModuleCategory.Propulsion, 1, 2) { FuelCapacity = 40, Mass = 2, Cost = 20 },
                new ModuleDefinition("o2gen", "Sauerstoffgenerator", ModuleCategory.LifeSupport, 1, 2) { Power = -2, OxygenPerMinute = 30, Mass = 2, Cost = 30 },
                new ModuleDefinition("hydro", "Hydroponik", ModuleCategory.LifeSupport, 2, 2) { Power = -1, OxygenPerMinute = 10, Mass = 3, Cost = 35 },
                new ModuleDefinition("cargo", "Frachtraum", ModuleCategory.Storage, 2, 2) { CargoSlots = 16, Mass = 2, Cost = 20 },
                new ModuleDefinition("bunk", "Koje", ModuleCategory.Crew, 1, 2) { CrewBeds = 1, Mass = 1, Cost = 10 },
                new ModuleDefinition("medbay", "Krankenstation", ModuleCategory.Crew, 2, 2) { Power = -2, Mass = 3, Cost = 45 },
                new ModuleDefinition("fabricator", "Werkbank", ModuleCategory.Production, 2, 1) { Power = -1, Mass = 2, Cost = 25 },
                new ModuleDefinition("recycler", "Recycler", ModuleCategory.Production, 2, 2) { Power = -2, Mass = 4, Cost = 40 },
                new ModuleDefinition("shield", "Schildgenerator", ModuleCategory.Defense, 2, 2) { Power = -4, Shield = 50, Mass = 4, Cost = 70 },
                new ModuleDefinition("airlock", "Luftschleuse", ModuleCategory.LifeSupport, 1, 1) { Mass = 1, Cost = 15 },
                new ModuleDefinition("scanner", "Langstreckenscanner", ModuleCategory.Command, 1, 1) { Power = -1, Mass = 1, Cost = 30, Unique = true },
            });
        }
    }
}
