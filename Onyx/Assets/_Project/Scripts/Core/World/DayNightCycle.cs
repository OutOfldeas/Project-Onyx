using System;

namespace Onyx.Core.World
{
    public enum DayPhase
    {
        Dawn,
        Day,
        Dusk,
        Night,
    }

    /// <summary>
    /// Tageszeit eines Planeten. <see cref="TimeOfDay"/> läuft von 0 bis 1:
    /// 0 = Mitternacht, 0,25 = Sonnenaufgang, 0,5 = Mittag, 0,75 = Sonnenuntergang.
    /// Läuft beim Host; Mitspieler bekommen die Zeit übermittelt.
    /// </summary>
    public sealed class DayNightCycle
    {
        // Phasengrenzen als Anteil des Tages.
        public const float DawnStart = 0.20f;
        public const float DayStart = 0.30f;
        public const float DuskStart = 0.70f;
        public const float NightStart = 0.80f;

        /// <summary>Dauer eines vollen Tages in Echtzeit-Sekunden.</summary>
        public float DayLengthSeconds { get; }

        public float TimeOfDay { get; private set; }

        /// <summary>Wie viele volle Tage seit Start vergangen sind.</summary>
        public int DayCount { get; private set; }

        public DayPhase Phase => PhaseAt(TimeOfDay);

        /// <summary>Helligkeit der Umgebung von 0 (tiefste Nacht) bis 1 (Mittag).</summary>
        public float Daylight => DaylightAt(TimeOfDay);

        public DayNightCycle(float dayLengthSeconds = 1200f, float startTimeOfDay = 0.35f)
        {
            if (dayLengthSeconds <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(dayLengthSeconds), dayLengthSeconds, "Tageslänge muss größer als 0 sein.");
            }

            DayLengthSeconds = dayLengthSeconds;
            TimeOfDay = Wrap(startTimeOfDay);
        }

        public void Tick(float deltaSeconds)
        {
            if (deltaSeconds < 0f || float.IsNaN(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "Zeitschritt darf nicht negativ sein.");
            }

            float t = TimeOfDay + deltaSeconds / DayLengthSeconds;
            int fullDays = (int)Math.Floor(t);
            DayCount += fullDays;
            TimeOfDay = t - fullDays;
        }

        /// <summary>Stellt Tageszeit und Tageszähler aus einem Spielstand wieder her.</summary>
        public void Restore(float timeOfDay, int dayCount)
        {
            TimeOfDay = Wrap(timeOfDay);
            DayCount = Math.Max(0, dayCount);
        }

        /// <summary>Setzt die Tageszeit direkt (z. B. beim Laden oder zum Testen).</summary>
        public void SetTimeOfDay(float timeOfDay)
        {
            TimeOfDay = Wrap(timeOfDay);
        }

        public static DayPhase PhaseAt(float timeOfDay)
        {
            float t = Wrap(timeOfDay);
            if (t >= NightStart || t < DawnStart) return DayPhase.Night;
            if (t < DayStart) return DayPhase.Dawn;
            if (t < DuskStart) return DayPhase.Day;
            return DayPhase.Dusk;
        }

        /// <summary>
        /// Helligkeit: nachts konstant dunkel, in Morgen- und Abenddämmerung weicher Übergang,
        /// tagsüber voll hell.
        /// </summary>
        public static float DaylightAt(float timeOfDay)
        {
            float t = Wrap(timeOfDay);
            switch (PhaseAt(t))
            {
                case DayPhase.Day:
                    return 1f;
                case DayPhase.Dawn:
                    return SmoothStep((t - DawnStart) / (DayStart - DawnStart));
                case DayPhase.Dusk:
                    return 1f - SmoothStep((t - DuskStart) / (NightStart - DuskStart));
                default:
                    return 0f;
            }
        }

        private static float SmoothStep(float x)
        {
            x = Math.Max(0f, Math.Min(1f, x));
            return x * x * (3f - 2f * x);
        }

        private static float Wrap(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "Tageszeit muss eine endliche Zahl sein.");
            }

            float wrapped = value - (float)Math.Floor(value);
            return wrapped >= 1f ? 0f : wrapped;
        }
    }
}
