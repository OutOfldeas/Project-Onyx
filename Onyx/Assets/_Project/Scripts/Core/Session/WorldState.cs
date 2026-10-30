using System;
using Onyx.Core.Galaxy;
using Onyx.Core.Ships;
using Onyx.Core.Skills;
using Onyx.Core.World;

namespace Onyx.Core.Session
{
    /// <summary>
    /// Der gesamte Spielstand einer Welt an einem Ort: Galaxie, Schiff, Navigation, Tageszeit
    /// und (vorerst) die Skills des lokalen Spielers. Läuft beim Host.
    /// Später kommen Speichern/Laden und die Synchronisierung mit Mitspielern dazu.
    /// </summary>
    public sealed class WorldState
    {
        public int Seed { get; }
        public GalaxyMap Galaxy { get; }
        public ModuleCatalog Modules { get; }
        public ShipBlueprint Ship { get; }
        public ShipStats ShipStats { get; private set; }
        public ShipNavigation Navigation { get; }
        public SkillTree SkillTree { get; }
        public CharacterSkills LocalSkills { get; }
        public DayNightCycle Clock { get; }

        /// <summary>Nach jeder Änderung am Schiff (neu berechnete Werte).</summary>
        public event Action ShipChanged;

        private WorldState(int seed)
        {
            Seed = seed;
            Galaxy = GalaxyGenerator.Generate(seed);
            Modules = ModuleCatalog.CreateDefault();
            Ship = ShipTemplates.CrashedFrigate(Modules);
            ShipStats = ShipStats.Calculate(Ship);
            Navigation = new ShipNavigation(Galaxy, Math.Max(1f, ShipStats.FuelCapacity), ShipStats.JumpRange);
            SkillTree = SkillCatalog.CreateDefault();
            LocalSkills = new CharacterSkills(SkillTree);
            Clock = new DayNightCycle();

            Ship.Changed += RecalculateShip;
        }

        public static WorldState CreateNew(int seed) => new WorldState(seed);

        /// <summary>Berechnet die Schiffswerte neu und überträgt Reichweite und Tankgröße auf die Navigation.</summary>
        public void RecalculateShip()
        {
            ShipStats = ShipStats.Calculate(Ship);
            Navigation.JumpRange = ShipStats.JumpRange;
            Navigation.Fuel.SetMax(Math.Max(1f, ShipStats.FuelCapacity));
            ShipChanged?.Invoke();
        }

        /// <summary>Simulation voranschreiten lassen (Tageszeit; später Außenposten, Ereignisse …).</summary>
        public void Tick(float deltaSeconds)
        {
            Clock.Tick(deltaSeconds);
        }
    }
}
