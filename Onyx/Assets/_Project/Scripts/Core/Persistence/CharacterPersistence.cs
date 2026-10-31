using System;
using System.Linq;
using Onyx.Core.Skills;

namespace Onyx.Core.Persistence
{
    /// <summary>
    /// Speichert den Charakter eines Spielers (Disziplinen, Erfahrung, Fähigkeiten). Liegt bei jedem
    /// Spieler selbst – wie bei Terraria nimmt man seinen Charakter in jede Welt mit.
    /// </summary>
    public static class CharacterPersistence
    {
        public const int CurrentVersion = 1;

        public static CharacterSaveData Capture(CharacterSkills skills)
        {
            if (skills == null) throw new ArgumentNullException(nameof(skills));

            var all = DisciplineNames.All;
            return new CharacterSaveData
            {
                Version = CurrentVersion,
                Levels = all.Select(skills.Level).ToArray(),
                Experience = all.Select(skills.Experience).ToArray(),
                Locks = all.Select(d => (int)skills.GetLock(d)).ToArray(),
                Unlocked = skills.Unlocked.OrderBy(id => id, StringComparer.Ordinal).ToArray(),
            };
        }

        /// <summary>Überträgt gespeicherte Werte auf einen vorhandenen Charakter.</summary>
        public static void Restore(CharacterSkills skills, CharacterSaveData data)
        {
            if (skills == null) throw new ArgumentNullException(nameof(skills));
            if (data == null) throw new SaveFormatException("Leerer Charakter.");
            if (data.Version > CurrentVersion)
            {
                throw new SaveFormatException($"Charakter-Version {data.Version} wird nicht unterstützt (erwartet bis {CurrentVersion}).");
            }

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
