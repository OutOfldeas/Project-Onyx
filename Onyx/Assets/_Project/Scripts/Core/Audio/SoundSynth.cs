using System;

namespace Onyx.Core.Audio
{
    /// <summary>Die Geräusche des Spiels.</summary>
    public enum Sfx
    {
        StepSand,
        StepMetal,
        Ratchet,
        RepairDone,
        LevelUp,
        OxygenAlarm,
        Downed,
        Revived,
    }

    /// <summary>
    /// Erzeugt alle Geräusche aus Rauschen und Tönen (Retro-Stil, keine Audiodateien nötig, lizenzfrei).
    /// Ergebnis: Mono-Samples in [-1, 1] bei <see cref="SampleRate"/>. Deterministisch über die Variante.
    /// </summary>
    public static class SoundSynth
    {
        public const int SampleRate = 44100;

        /// <summary>Wie viele leicht unterschiedliche Fassungen es von Schritten gibt.</summary>
        public const int StepVariants = 3;

        private const float TwoPi = (float)(Math.PI * 2);

        public static float[] Create(Sfx sound, int variant = 0)
        {
            switch (sound)
            {
                case Sfx.StepSand: return StepSand(variant);
                case Sfx.StepMetal: return StepMetal(variant);
                case Sfx.Ratchet: return Ratchet();
                case Sfx.RepairDone: return Arpeggio(new[] { 523.25f, 659.25f, 783.99f, 1046.5f }, 0.09f, 0.28f, 0.35f);
                case Sfx.LevelUp: return Arpeggio(new[] { 783.99f, 987.77f, 1174.66f, 1567.98f, 1975.53f }, 0.065f, 0.2f, 0.6f);
                case Sfx.OxygenAlarm: return Alarm();
                case Sfx.Downed: return Sweep(420f, 110f, 0.7f);
                case Sfx.Revived: return Sweep(180f, 640f, 0.45f);
                default: throw new ArgumentOutOfRangeException(nameof(sound), sound, null);
            }
        }

        // ------------------------------------------------------------------ Schritte

        /// <summary>Knirschen im Sand: gefiltertes Rauschen mit kurzem Nachknirschen.</summary>
        private static float[] StepSand(int variant)
        {
            var random = new SeededRandom(1000 + variant);
            var s = Buffer(0.09f);
            float cutoff = 900f + variant * 220f;
            float low = 0f, a = LowpassFactor(cutoff);
            for (int i = 0; i < s.Length; i++)
            {
                float t = i / (float)SampleRate;
                float env = Attack(t, 0.003f) * (float)Math.Exp(-t / 0.018f) + 0.35f * Window(t, 0.028f, 0.004f) * (float)Math.Exp(-(t - 0.028f) / 0.015f);
                low += a * (random.NextFloat(-1f, 1f) - low);
                s[i] = low * env;
            }

            return Normalize(s, 0.55f);
        }

        /// <summary>Schritt auf Metall: Klick plus kurz nachklingendes Blech.</summary>
        private static float[] StepMetal(int variant)
        {
            var random = new SeededRandom(2000 + variant);
            var s = Buffer(0.16f);
            float f1 = 480f + variant * 37f, f2 = 1290f + variant * 61f;
            for (int i = 0; i < s.Length; i++)
            {
                float t = i / (float)SampleRate;
                float click = t < 0.004f ? random.NextFloat(-1f, 1f) * (1f - t / 0.004f) : 0f;
                float ring = (float)(Math.Sin(TwoPi * f1 * t) + 0.5 * Math.Sin(TwoPi * f2 * t)) * (float)Math.Exp(-t / 0.035f);
                s[i] = 0.7f * click + 0.5f * ring * Attack(t, 0.001f);
            }

            return Normalize(s, 0.45f);
        }

        // ------------------------------------------------------------------ Reparieren

        /// <summary>Ratsche: vier kurze, helle Metallklicks.</summary>
        private static float[] Ratchet()
        {
            var random = new SeededRandom(3000);
            var s = Buffer(0.26f);
            float low = 0f, a = LowpassFactor(2500f);
            for (int i = 0; i < s.Length; i++)
            {
                float t = i / (float)SampleRate;
                float local = t % 0.06f;
                float noise = random.NextFloat(-1f, 1f);
                low += a * (noise - low);
                float high = noise - low; // Hochpass: nur das Klirren
                float click = high * (float)Math.Exp(-local / 0.004f);
                float ping = (float)Math.Sin(TwoPi * 2100f * local) * (float)Math.Exp(-local / 0.009f);
                s[i] = 0.8f * click + 0.4f * ping;
            }

            return Normalize(s, 0.5f);
        }

        // ------------------------------------------------------------------ Töne

        /// <summary>Aufsteigende Tonfolge (Glockenklang), z. B. für „repariert“ und „Stufe erreicht“.</summary>
        private static float[] Arpeggio(float[] notes, float step, float decay, float brightness)
        {
            var s = Buffer(step * notes.Length + decay * 3f);
            for (int n = 0; n < notes.Length; n++)
            {
                int start = (int)(n * step * SampleRate);
                for (int i = start; i < s.Length; i++)
                {
                    float t = (i - start) / (float)SampleRate;
                    float env = Attack(t, 0.004f) * (float)Math.Exp(-t / decay);
                    float tone = (float)(Math.Sin(TwoPi * notes[n] * t) + brightness * Math.Sin(TwoPi * notes[n] * 2f * t) + 0.15 * Math.Sin(TwoPi * notes[n] * 3f * t));
                    s[i] += tone * env;
                }
            }

            return Normalize(s, 0.5f);
        }

        /// <summary>Warnton bei wenig Sauerstoff: zwei weiche Rechtecktöne.</summary>
        private static float[] Alarm()
        {
            var s = Buffer(0.3f);
            Beep(s, 0f, 0.1f, 880f);
            Beep(s, 0.15f, 0.1f, 660f);
            return Normalize(s, 0.4f);
        }

        private static void Beep(float[] s, float start, float length, float frequency)
        {
            int from = (int)(start * SampleRate);
            int to = Math.Min(s.Length, (int)((start + length) * SampleRate));
            for (int i = from; i < to; i++)
            {
                float t = (i - from) / (float)SampleRate;
                float env = Attack(t, 0.005f) * Math.Min(1f, (length - t) / 0.01f);
                s[i] += SoftSquare(frequency, t) * env;
            }
        }

        /// <summary>Gleitender Ton mit leichtem Zittern (fallend = kampfunfähig, steigend = wiederbelebt).</summary>
        private static float[] Sweep(float from, float to, float length)
        {
            var s = Buffer(length);
            float phase = 0f;
            for (int i = 0; i < s.Length; i++)
            {
                float t = i / (float)SampleRate;
                float k = t / length;
                float frequency = from * (float)Math.Pow(to / from, k);
                phase += TwoPi * frequency / SampleRate;
                float tremolo = 0.75f + 0.25f * (float)Math.Sin(TwoPi * 11f * t);
                float env = Attack(t, 0.01f) * Math.Min(1f, (length - t) / 0.08f);
                s[i] = ((float)Math.Sin(phase) + 0.3f * (float)Math.Sin(phase * 3f) / 3f) * tremolo * env;
            }

            return Normalize(s, 0.45f);
        }

        // ------------------------------------------------------------------ Umgebung

        /// <summary>
        /// Wind als nahtlose Schleife: tiefes, langsam auf- und abschwellendes Rauschen.
        /// Anfang und Ende sind überblendet, damit die Wiederholung nicht knackt.
        /// </summary>
        public static float[] WindLoop(float seconds = 6f)
        {
            int n = (int)(seconds * SampleRate);
            int fade = SampleRate / 2;
            var raw = new float[n + fade];
            var random = new SeededRandom(4000);
            float low1 = 0f, low2 = 0f;
            for (int i = 0; i < raw.Length; i++)
            {
                float t = i / (float)SampleRate;
                float cutoff = 260f + 180f * (float)Math.Sin(TwoPi * t / seconds) + 90f * (float)Math.Sin(TwoPi * 3f * t / seconds);
                float a = LowpassFactor(cutoff);
                low1 += a * (random.NextFloat(-1f, 1f) - low1);
                low2 += a * (low1 - low2);
                float gust = 0.6f + 0.4f * (float)Math.Sin(TwoPi * 2f * t / seconds + 1.3f);
                raw[i] = low2 * gust;
            }

            var loop = new float[n];
            for (int i = 0; i < n; i++)
            {
                if (i < fade)
                {
                    float k = i / (float)fade;
                    loop[i] = raw[i] * k + raw[n + i] * (1f - k);
                }
                else
                {
                    loop[i] = raw[i];
                }
            }

            return Normalize(loop, 0.6f);
        }

        // ------------------------------------------------------------------ Hilfen

        private static float[] Buffer(float seconds) => new float[Math.Max(1, (int)(seconds * SampleRate))];

        private static float LowpassFactor(float cutoff) => 1f - (float)Math.Exp(-TwoPi * cutoff / SampleRate);

        private static float Attack(float t, float duration) => t >= duration ? 1f : t / duration;

        private static float Window(float t, float start, float attack) => t < start ? 0f : Attack(t - start, attack);

        private static float SoftSquare(float f, float t) =>
            (float)(Math.Sin(TwoPi * f * t) + Math.Sin(TwoPi * 3f * f * t) / 3.0 + Math.Sin(TwoPi * 5f * f * t) / 5.0);

        private static float[] Normalize(float[] s, float peak)
        {
            float max = 0f;
            foreach (float v in s) max = Math.Max(max, Math.Abs(v));
            if (max <= 0f) return s;
            float k = peak / max;
            for (int i = 0; i < s.Length; i++) s[i] *= k;
            return s;
        }
    }
}
