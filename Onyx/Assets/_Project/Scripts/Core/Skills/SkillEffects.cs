namespace Onyx.Core.Skills
{
    /// <summary>
    /// Was Disziplinstufen und Fähigkeiten im Spiel bewirken. An einer Stelle gesammelt,
    /// damit Werte leicht nachzustellen und zu testen sind.
    /// </summary>
    public static class SkillEffects
    {
        /// <summary>Technik-Erfahrung pro geleisteter Arbeitssekunde beim Reparieren.</summary>
        public const float RepairExperiencePerWork = 2f;

        /// <summary>Erkundungs-Erfahrung pro Kachel, die man draußen zurücklegt.</summary>
        public const float ExplorationExperiencePerTile = 0.25f;

        /// <summary>Erkundungs-Erfahrung für ein neu entdecktes Sternsystem.</summary>
        public const float ExplorationExperiencePerSystem = 60f;

        /// <summary>Arbeitstempo beim Reparieren (1 = ungeübt): +3 % je Technikstufe, +20 % mit „Grundreparatur“.</summary>
        public static float RepairSpeed(CharacterSkills skills)
        {
            float speed = 1f + 0.03f * skills.Level(Discipline.Engineering);
            if (skills.IsActive("eng.repair1")) speed += 0.2f;
            return speed;
        }

        /// <summary>Faktor auf den Sauerstoffverbrauch: „Leichter Schritt“ spart 20 %.</summary>
        public static float OxygenUseFactor(CharacterSkills skills)
        {
            return skills.IsActive("exp.lightfoot") ? 0.8f : 1f;
        }
    }
}
