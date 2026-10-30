using System;

namespace Onyx.Core.Survival
{
    /// <summary>
    /// Ein begrenzter Vorrat, der sich leeren und wieder auffüllen lässt,
    /// z. B. Sauerstoff oder Schildenergie. Der Wert bleibt immer zwischen 0 und <see cref="Max"/>.
    /// </summary>
    public sealed class ResourcePool
    {
        /// <summary>Fassungsvermögen des Vorrats.</summary>
        public float Max { get; private set; }

        /// <summary>Aktuell vorhandene Menge.</summary>
        public float Current { get; private set; }

        /// <summary>Füllstand von 0 (leer) bis 1 (voll).</summary>
        public float Ratio => Current / Max;

        public bool IsEmpty => Current <= 0f;

        public bool IsFull => Current >= Max;

        /// <summary>Erzeugt einen vollen Vorrat.</summary>
        public ResourcePool(float max) : this(max, max)
        {
        }

        /// <summary>Erzeugt einen Vorrat mit einem Startwert; dieser wird auf 0 bis <paramref name="max"/> begrenzt.</summary>
        public ResourcePool(float max, float current)
        {
            if (max <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(max), max, "Max muss größer als 0 sein.");
            }

            Max = max;
            Current = Clamp(current);
        }

        /// <summary>Entnimmt bis zu <paramref name="amount"/>.</summary>
        /// <returns>Die tatsächlich entnommene Menge (kann kleiner sein, wenn der Vorrat nicht reicht).</returns>
        public float Drain(float amount)
        {
            RequireNonNegative(amount, nameof(amount));
            float taken = Math.Min(amount, Current);
            Current -= taken;
            return taken;
        }

        /// <summary>Füllt bis zu <paramref name="amount"/> auf.</summary>
        /// <returns>Die tatsächlich hinzugefügte Menge (kann kleiner sein, wenn der Vorrat fast voll ist).</returns>
        public float Refill(float amount)
        {
            RequireNonNegative(amount, nameof(amount));
            float added = Math.Min(amount, Max - Current);
            Current += added;
            return added;
        }

        /// <summary>
        /// Ändert das Fassungsvermögen, z. B. durch einen größeren Tank.
        /// </summary>
        /// <param name="max">Neues Fassungsvermögen, größer als 0.</param>
        /// <param name="keepRatio">
        /// true: Der Füllstand in Prozent bleibt gleich.
        /// false: Die Menge bleibt gleich und wird nur bei Bedarf auf das neue Maximum begrenzt.
        /// </param>
        public void SetMax(float max, bool keepRatio = false)
        {
            if (max <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(max), max, "Max muss größer als 0 sein.");
            }

            float ratio = Ratio;
            Max = max;
            Current = keepRatio ? max * ratio : Clamp(Current);
        }

        private float Clamp(float value)
        {
            if (value < 0f)
            {
                return 0f;
            }

            return value > Max ? Max : value;
        }

        private static void RequireNonNegative(float amount, string paramName)
        {
            if (amount < 0f || float.IsNaN(amount))
            {
                throw new ArgumentOutOfRangeException(paramName, amount, "Menge darf nicht negativ sein.");
            }
        }
    }
}
