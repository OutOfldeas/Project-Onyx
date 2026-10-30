using System;
using System.Collections.Generic;
using System.Linq;

namespace Onyx.Core.Skills
{
    /// <summary>Einstellung pro Disziplin für das Umlernen an der Obergrenze (angelehnt an Ultima Online).</summary>
    public enum SkillLock
    {
        /// <summary>Darf steigen.</summary>
        Raise,

        /// <summary>Darf zugunsten anderer Disziplinen sinken.</summary>
        Lower,

        /// <summary>Bleibt, wie sie ist.</summary>
        Locked,
    }

    /// <summary>
    /// Fortschritt eines Charakters: Disziplinstufen durch Lernen durch Tun, Obergrenze
    /// für die Summe aller Stufen, Skillpunkte und freigeschaltete Fähigkeiten.
    /// Gehört dem Spieler (nicht der Welt). Läuft beim Host.
    /// </summary>
    public sealed class CharacterSkills
    {
        public const int MaxLevel = 20;

        /// <summary>Höchstsumme aller Disziplinstufen (zwei Meister und eine halbe dritte Disziplin).</summary>
        public const int TotalLevelCap = 50;

        /// <summary>Wie viele zurückliegende Aktionen für abnehmende Erfahrung berücksichtigt werden.</summary>
        public const int RepetitionWindow = 20;

        private readonly SkillTree tree;
        private readonly Dictionary<Discipline, int> levels = new Dictionary<Discipline, int>();
        private readonly Dictionary<Discipline, float> experience = new Dictionary<Discipline, float>();
        private readonly Dictionary<Discipline, SkillLock> locks = new Dictionary<Discipline, SkillLock>();
        private readonly HashSet<string> unlocked = new HashSet<string>();
        private readonly Queue<string> recentActions = new Queue<string>();

        public event Action<Discipline, int> LevelChanged;

        public CharacterSkills(SkillTree tree)
        {
            this.tree = tree ?? throw new ArgumentNullException(nameof(tree));
            foreach (var d in DisciplineNames.All)
            {
                levels[d] = 0;
                experience[d] = 0f;
                locks[d] = SkillLock.Raise;
            }
        }

        public SkillTree Tree => tree;

        public int Level(Discipline d) => levels[d];

        public float Experience(Discipline d) => experience[d];

        public int TotalLevel => levels.Values.Sum();

        public SkillLock GetLock(Discipline d) => locks[d];

        public void SetLock(Discipline d, SkillLock mode) => locks[d] = mode;

        public IReadOnlyCollection<string> Unlocked => unlocked;

        /// <summary>Erfahrung, die für den Aufstieg von <paramref name="level"/> auf die nächste Stufe nötig ist.</summary>
        public static float ExperienceToNext(int level)
        {
            return 100f * (float)Math.Pow(level + 1, 1.5);
        }

        /// <summary>
        /// Erfahrung aus einer Tätigkeit.
        /// </summary>
        /// <param name="actionKey">
        /// Art der Aktion (z. B. „repair:o2-generator“). Wiederholt sich dieselbe Aktion, bringt sie
        /// weniger Erfahrung. null = keine Abschwächung.
        /// </param>
        /// <param name="mentorLevel">Stufe eines Mitspielers, der dieselbe Tätigkeit ausübt (Mentoring).</param>
        /// <returns>Die tatsächlich gutgeschriebene Erfahrung.</returns>
        public float AddExperience(Discipline d, float amount, string actionKey = null, int mentorLevel = 0)
        {
            if (amount < 0f || float.IsNaN(amount))
            {
                throw new ArgumentOutOfRangeException(nameof(amount), amount, "Erfahrung darf nicht negativ sein.");
            }

            if (locks[d] != SkillLock.Raise || levels[d] >= MaxLevel)
            {
                return 0f;
            }

            float multiplier = 1f;
            if (actionKey != null)
            {
                int repeats = recentActions.Count(a => a == actionKey);
                multiplier /= 1f + 0.25f * repeats;
                recentActions.Enqueue(actionKey);
                while (recentActions.Count > RepetitionWindow)
                {
                    recentActions.Dequeue();
                }
            }

            if (mentorLevel >= levels[d] + 5)
            {
                multiplier *= 1.5f;
            }

            float gained = amount * multiplier;
            experience[d] += gained;

            while (levels[d] < MaxLevel && experience[d] >= ExperienceToNext(levels[d]))
            {
                if (TotalLevel >= TotalLevelCap && !TryLowerAnother(d))
                {
                    // An der Obergrenze und nichts darf sinken: Erfahrung bleibt am Schwellwert stehen.
                    experience[d] = ExperienceToNext(levels[d]);
                    break;
                }

                experience[d] -= ExperienceToNext(levels[d]);
                levels[d]++;
                LevelChanged?.Invoke(d, levels[d]);
            }

            return gained;
        }

        /// <summary>Freie Skillpunkte im Baum einer Disziplin (1 Punkt pro Stufe).</summary>
        public int AvailablePoints(Discipline d)
        {
            int spent = tree.InTree(d).Where(n => unlocked.Contains(n.Id)).Sum(n => n.Cost);
            return levels[d] - spent;
        }

        public bool IsUnlocked(string nodeId) => unlocked.Contains(nodeId);

        /// <summary>Freigeschaltet und die eigene Disziplin hat (noch) die nötige Stufe.</summary>
        public bool IsActive(string nodeId)
        {
            var node = tree.Get(nodeId);
            return unlocked.Contains(nodeId) && levels[node.Discipline] >= node.RequiredLevel;
        }

        public UnlockCheck CanUnlock(string nodeId)
        {
            var node = tree.Get(nodeId);
            if (unlocked.Contains(nodeId)) return UnlockCheck.AlreadyUnlocked;
            if (levels[node.Discipline] < node.RequiredLevel) return UnlockCheck.LevelTooLow;
            if (node.Prerequisites.Any(p => !unlocked.Contains(p))) return UnlockCheck.MissingPrerequisite;
            if (AvailablePoints(node.Discipline) < node.Cost) return UnlockCheck.NotEnoughPoints;
            return UnlockCheck.Ok;
        }

        public UnlockCheck Unlock(string nodeId)
        {
            var result = CanUnlock(nodeId);
            if (result == UnlockCheck.Ok)
            {
                unlocked.Add(nodeId);
            }

            return result;
        }

        /// <summary>Setzt eine Stufe direkt (Dev-Modus, Tests). Hält die Obergrenze nicht ein.</summary>
        public void DebugSetLevel(Discipline d, int level)
        {
            levels[d] = Math.Max(0, Math.Min(MaxLevel, level));
            experience[d] = 0f;
            LevelChanged?.Invoke(d, levels[d]);
        }

        private bool TryLowerAnother(Discipline raising)
        {
            foreach (var other in DisciplineNames.All)
            {
                if (other != raising && locks[other] == SkillLock.Lower && levels[other] > 0)
                {
                    levels[other]--;
                    experience[other] = 0f;
                    LevelChanged?.Invoke(other, levels[other]);
                    return true;
                }
            }

            return false;
        }
    }

    public enum UnlockCheck
    {
        Ok,
        AlreadyUnlocked,
        LevelTooLow,
        MissingPrerequisite,
        NotEnoughPoints,
    }
}
