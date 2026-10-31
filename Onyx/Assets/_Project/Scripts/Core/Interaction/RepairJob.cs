using System;
using System.Collections.Generic;

namespace Onyx.Core.Interaction
{
    /// <summary>
    /// Eine Reparatur, an der mehrere Spieler gleichzeitig arbeiten können,
    /// z. B. ein beschädigtes Modul der Fregatte. Läuft beim Host.
    /// </summary>
    public sealed class RepairJob
    {
        /// <summary>Zusätzliches Tempo pro weiterem Helfer (Teamarbeit lohnt sich).</summary>
        public const float TeamBonusPerExtraWorker = 0.25f;

        private readonly HashSet<ulong> workers = new HashSet<ulong>();

        /// <summary>Benötigte Arbeit in „Arbeitssekunden“ eines einzelnen ungeübten Spielers.</summary>
        public float RequiredWork { get; }

        public float Progress { get; private set; }

        public float Ratio => Progress / RequiredWork;

        public bool IsComplete => Progress >= RequiredWork;

        public int WorkerCount => workers.Count;

        /// <summary>Wird einmal ausgelöst, wenn die Reparatur abgeschlossen ist.</summary>
        public event Action Completed;

        public RepairJob(float requiredWork)
        {
            if (requiredWork <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(requiredWork), requiredWork, "Arbeit muss größer als 0 sein.");
            }

            RequiredWork = requiredWork;
        }

        /// <summary>Ein Spieler beginnt mitzuarbeiten. Doppelte Anmeldung wird ignoriert.</summary>
        public void Join(ulong playerId)
        {
            if (!IsComplete)
            {
                workers.Add(playerId);
            }
        }

        /// <summary>Ein Spieler hört auf mitzuarbeiten.</summary>
        public void Leave(ulong playerId)
        {
            workers.Remove(playerId);
        }

        public bool IsWorking(ulong playerId)
        {
            return workers.Contains(playerId);
        }

        /// <summary>
        /// Übernimmt einen Fortschritt von außen: bei Mitspielern den Stand des Hosts,
        /// im Dev-Modus beliebige Werte. Löst <see cref="Completed"/> aus, wenn die Reparatur dadurch fertig wird.
        /// </summary>
        public void SetProgress(float progress)
        {
            bool wasComplete = IsComplete;
            Progress = Math.Max(0f, Math.Min(RequiredWork, progress));
            if (IsComplete && !wasComplete)
            {
                workers.Clear();
                Completed?.Invoke();
            }
        }

        /// <summary>
        /// Schreitet die Reparatur voran.
        /// </summary>
        /// <param name="deltaSeconds">Vergangene Zeit.</param>
        /// <param name="speedOf">
        /// Liefert das Arbeitstempo eines Spielers (1 = ungeübt). Später hängt es von der
        /// Disziplin Technik ab. null = alle arbeiten mit Tempo 1.
        /// </param>
        /// <param name="workDone">
        /// Wird für jeden Helfer mit seinem Anteil an der Arbeit aufgerufen (Grundlage für Erfahrungspunkte).
        /// </param>
        /// <returns>Die in diesem Schritt geleistete Arbeit.</returns>
        public float Tick(float deltaSeconds, Func<ulong, float> speedOf = null, Action<ulong, float> workDone = null)
        {
            if (deltaSeconds < 0f || float.IsNaN(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "Zeitschritt darf nicht negativ sein.");
            }

            if (IsComplete || workers.Count == 0 || deltaSeconds == 0f)
            {
                return 0f;
            }

            float combinedSpeed = 0f;
            foreach (ulong worker in workers)
            {
                combinedSpeed += Math.Max(0f, speedOf?.Invoke(worker) ?? 1f);
            }

            float teamBonus = 1f + TeamBonusPerExtraWorker * (workers.Count - 1);
            float work = Math.Min(combinedSpeed * teamBonus * deltaSeconds, RequiredWork - Progress);
            Progress += work;

            if (workDone != null && combinedSpeed > 0f)
            {
                foreach (ulong worker in workers)
                {
                    float share = Math.Max(0f, speedOf?.Invoke(worker) ?? 1f) / combinedSpeed;
                    workDone(worker, work * share);
                }
            }

            if (IsComplete)
            {
                workers.Clear();
                Completed?.Invoke();
            }

            return work;
        }
    }
}
