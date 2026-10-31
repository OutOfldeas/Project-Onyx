using System;
using System.Collections.Generic;
using System.Linq;
using Onyx.Core.Session;
using Onyx.Core.Ships;
using Onyx.Core.Skills;

namespace Onyx.Core.Persistence
{
    public sealed class SaveFormatException : Exception
    {
        public SaveFormatException(string message) : base(message)
        {
        }
    }

    /// <summary>
    /// Wandelt einen <see cref="WorldState"/> in Speicherdaten um und zurück. Die Welt selbst
    /// (Galaxie, Gelände) entsteht beim Laden wieder aus dem Seed; gespeichert wird nur,
    /// was Spieler verändert haben.
    /// </summary>
    public static class WorldPersistence
    {
        public const int CurrentVersion = 1;

        public static WorldSaveData Capture(WorldState world, float playTimeSeconds = 0f, DateTime? now = null)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));

            var site = world.CrashSite;
            var nav = world.Navigation;
            return new WorldSaveData
            {
                Version = CurrentVersion,
                SavedAtUtc = (now ?? DateTime.UtcNow).ToString("o"),
                PlayTimeSeconds = playTimeSeconds,
                Seed = world.Seed,
                TimeOfDay = world.Clock.TimeOfDay,
                DayCount = world.Clock.DayCount,
                RepairProgress = Enumerable.Range(0, site.Layout.RepairPoints.Count).Select(i => site.Job(i).Progress).ToArray(),
                CurrentSystemId = nav.CurrentSystemId,
                Fuel = nav.Fuel.Current,
                OrbitPlanetId = nav.OrbitPlanetId ?? string.Empty,
                DiscoveredSystems = world.Galaxy.Systems.Where(s => s.Discovered).Select(s => s.Id).ToArray(),
                Ship = CaptureShip(world.Ship),
                LocalCharacter = CaptureCharacter(world.LocalSkills),
            };
        }

        public static WorldState Restore(WorldSaveData data)
        {
            if (data == null) throw new SaveFormatException("Leerer Spielstand.");
            if (data.Version <= 0 || data.Version > CurrentVersion)
            {
                throw new SaveFormatException($"Spielstand-Version {data.Version} wird nicht unterstützt (erwartet bis {CurrentVersion}).");
            }

            var world = WorldState.CreateNew(data.Seed);
            world.Clock.Restore(data.TimeOfDay, data.DayCount);

            // Reihenfolge: Schiff zuerst (setzt Tankgröße), dann Reparaturen (Antrieb → Reichweite), zuletzt Treibstoff.
            if (data.Ship != null)
            {
                RestoreShip(world, data.Ship);
            }

            if (data.RepairProgress != null)
            {
                int count = Math.Min(data.RepairProgress.Length, world.CrashSite.Layout.RepairPoints.Count);
                for (int i = 0; i < count; i++)
                {
                    world.CrashSite.ApplySyncedProgress(i, data.RepairProgress[i]);
                }
            }

            if (data.DiscoveredSystems != null)
            {
                foreach (int id in data.DiscoveredSystems)
                {
                    if (id >= 0 && id < world.Galaxy.Systems.Count)
                    {
                        world.Galaxy.GetSystem(id).Discovered = true;
                    }
                }
            }

            if (data.CurrentSystemId >= 0 && data.CurrentSystemId < world.Galaxy.Systems.Count)
            {
                world.Navigation.Restore(data.CurrentSystemId, data.Fuel, data.OrbitPlanetId);
            }

            if (data.LocalCharacter != null)
            {
                RestoreCharacter(world.LocalSkills, data.LocalCharacter);
            }

            return world;
        }

        // ------------------------------------------------------------------ Schiff

        private static ShipSaveData CaptureShip(ShipBlueprint ship)
        {
            var hull = new List<int>();
            for (int y = 0; y < ship.Height; y++)
            {
                for (int x = 0; x < ship.Width; x++)
                {
                    if (ship.HasHull(x, y)) hull.Add(y * ship.Width + x);
                }
            }

            return new ShipSaveData
            {
                Name = ship.Name,
                Width = ship.Width,
                Height = ship.Height,
                Hull = hull.ToArray(),
                Modules = ship.Modules.Select(m => new ModuleSaveData { Id = m.Definition.Id, X = m.X, Y = m.Y, Rotated = m.Rotated }).ToArray(),
            };
        }

        private static void RestoreShip(WorldState world, ShipSaveData data)
        {
            if (data.Width != world.Ship.Width || data.Height != world.Ship.Height)
            {
                throw new SaveFormatException($"Schiffsraster {data.Width}×{data.Height} passt nicht zu {world.Ship.Width}×{world.Ship.Height}.");
            }

            // In einem Zwischenbauplan aufbauen und dann auf einmal übernehmen (nur eine Neuberechnung).
            var temp = new ShipBlueprint(data.Width, data.Height, data.Name ?? world.Ship.Name);
            foreach (int index in data.Hull ?? Array.Empty<int>())
            {
                temp.SetHull(index % data.Width, index / data.Width, true);
            }

            foreach (var module in data.Modules ?? Array.Empty<ModuleSaveData>())
            {
                if (world.Modules.TryGet(module.Id, out var definition))
                {
                    temp.Place(definition, module.X, module.Y, module.Rotated);
                }
            }

            world.Ship.CopyFrom(temp);
        }

        // ------------------------------------------------------------------ Charakter

        private static CharacterSaveData CaptureCharacter(CharacterSkills skills)
        {
            var all = DisciplineNames.All;
            return new CharacterSaveData
            {
                Levels = all.Select(skills.Level).ToArray(),
                Experience = all.Select(skills.Experience).ToArray(),
                Locks = all.Select(d => (int)skills.GetLock(d)).ToArray(),
                Unlocked = skills.Unlocked.OrderBy(id => id, StringComparer.Ordinal).ToArray(),
            };
        }

        private static void RestoreCharacter(CharacterSkills skills, CharacterSaveData data)
        {
            var all = DisciplineNames.All;
            for (int i = 0; i < all.Length; i++)
            {
                int level = data.Levels != null && i < data.Levels.Length ? data.Levels[i] : 0;
                float xp = data.Experience != null && i < data.Experience.Length ? data.Experience[i] : 0f;
                var mode = data.Locks != null && i < data.Locks.Length && Enum.IsDefined(typeof(SkillLock), data.Locks[i])
                    ? (SkillLock)data.Locks[i]
                    : SkillLock.Raise;
                skills.RestoreDiscipline(all[i], level, xp, mode);
            }

            skills.RestoreUnlocked(data.Unlocked ?? Array.Empty<string>());
        }
    }
}
