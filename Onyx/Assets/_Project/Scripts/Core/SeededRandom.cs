using System;

namespace Onyx.Core
{
    /// <summary>
    /// Deterministischer Zufallsgenerator (SplitMix64). Gleicher Seed ergibt auf jedem Rechner
    /// und jeder .NET-Version dieselbe Folge – wichtig, damit alle Spieler dieselbe Welt sehen.
    /// </summary>
    public sealed class SeededRandom
    {
        private ulong state;

        public SeededRandom(int seed)
        {
            state = unchecked((ulong)seed * 0x9E3779B97F4A7C15UL + 0x632BE59BD9B4E019UL);
        }

        private ulong NextULong()
        {
            unchecked
            {
                ulong z = state += 0x9E3779B97F4A7C15UL;
                z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
                return z ^ (z >> 31);
            }
        }

        /// <summary>Zahl in [0, 1).</summary>
        public double NextDouble()
        {
            return (NextULong() >> 11) * (1.0 / (1UL << 53));
        }

        public float NextFloat(float min, float max)
        {
            return min + (float)NextDouble() * (max - min);
        }

        /// <summary>Ganzzahl in [min, max).</summary>
        public int Next(int min, int max)
        {
            if (max <= min)
            {
                throw new ArgumentOutOfRangeException(nameof(max), "max muss größer als min sein.");
            }

            return min + (int)(NextULong() % (ulong)(max - min));
        }

        public bool Chance(double probability)
        {
            return NextDouble() < probability;
        }

        public T Pick<T>(T[] items)
        {
            return items[Next(0, items.Length)];
        }
    }
}
