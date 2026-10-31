using System;
using System.Linq;
using Onyx.Core.Interaction;
using Onyx.Core.Survival;

namespace Onyx.Core.CrashSite
{
    public enum JoinResult
    {
        Ok,
        AlreadyRepaired,
        MissingRequirement,
    }

    /// <summary>
    /// Zustand der Absturzstelle: welche Systeme der Fregatte schon laufen und was die Umgebung
    /// an einer Stelle bewirkt. Läuft beim Host; Mitspieler übernehmen den Fortschritt über
    /// <see cref="ApplySyncedProgress"/>.
    /// </summary>
    public sealed class CrashSiteState
    {
        /// <summary>Sauerstoff-Nachfüllung pro Sekunde in der Kapsel.</summary>
        public const float PodRefillPerSecond = 10f;

        /// <summary>Nachfüllung im Wrack, sobald der Sauerstoffgenerator läuft.</summary>
        public const float WreckRefillPerSecond = 4f;

        /// <summary>Kälteschaden pro Sekunde, nachts im Freien. Trifft zuerst den Schild.</summary>
        public const float ColdDamagePerSecond = 1.5f;

        /// <summary>Unterhalb dieses Tageslichts (0–1) gilt es als Nacht.</summary>
        public const float NightBelowDaylight = 0.35f;

        private readonly RepairJob[] jobs;

        public CrashSiteLayout Layout { get; }

        /// <summary>Eine Station wurde fertig repariert.</summary>
        public event Action<RepairPoint> StationRepaired;

        public CrashSiteState(CrashSiteLayout layout)
        {
            Layout = layout ?? throw new ArgumentNullException(nameof(layout));
            jobs = new RepairJob[layout.RepairPoints.Count];
            for (int i = 0; i < jobs.Length; i++)
            {
                var point = layout.RepairPoints[i];
                jobs[i] = new RepairJob(point.Work);
                jobs[i].Completed += () => StationRepaired?.Invoke(point);
            }
        }

        public RepairJob Job(int index) => jobs[index];

        public RepairPoint Point(StationKind kind) => Layout.RepairPoints.First(p => p.Kind == kind);

        public bool IsRepaired(StationKind kind) => jobs[Point(kind).Index].IsComplete;

        public bool AllRepaired => jobs.All(j => j.IsComplete);

        public int RepairedCount => jobs.Count(j => j.IsComplete);

        // ------------------------------------------------------------------ Umgebung

        public Surroundings SurroundingsAt(float worldX, float worldY, float daylight)
        {
            switch (Layout.ZoneAt(worldX, worldY))
            {
                case SiteZone.Pod:
                    return new Surroundings(true, PodRefillPerSecond);
                case SiteZone.Wreck:
                    // Das Wrack schützt vor Kälte; Luft gibt es erst mit dem Generator.
                    return IsRepaired(StationKind.OxygenGenerator) ? new Surroundings(true, WreckRefillPerSecond) : Surroundings.Outside;
                default:
                    return new Surroundings(false, 0f, IsNight(daylight) ? ColdDamagePerSecond : 0f);
            }
        }

        public static bool IsNight(float daylight) => daylight < NightBelowDaylight;

        // ------------------------------------------------------------------ Reparieren

        public JoinResult CanWorkOn(int index)
        {
            var point = Layout.RepairPoints[index];
            if (jobs[index].IsComplete) return JoinResult.AlreadyRepaired;
            if (point.Requires.HasValue && !IsRepaired(point.Requires.Value)) return JoinResult.MissingRequirement;
            return JoinResult.Ok;
        }

        /// <summary>Spieler arbeitet ab jetzt an dieser Station (und an keiner anderen mehr).</summary>
        public JoinResult Join(int index, ulong playerId)
        {
            JoinResult check = CanWorkOn(index);
            if (check != JoinResult.Ok)
            {
                return check;
            }

            LeaveAll(playerId);
            jobs[index].Join(playerId);
            return JoinResult.Ok;
        }

        public void LeaveAll(ulong playerId)
        {
            foreach (var job in jobs)
            {
                job.Leave(playerId);
            }
        }

        /// <summary>An welcher Station arbeitet der Spieler gerade? -1 = keiner.</summary>
        public int WorkingOn(ulong playerId)
        {
            for (int i = 0; i < jobs.Length; i++)
            {
                if (jobs[i].IsWorking(playerId)) return i;
            }

            return -1;
        }

        /// <returns>Die insgesamt geleistete Arbeit in diesem Schritt.</returns>
        public float Tick(float deltaSeconds, Func<ulong, float> speedOf = null, Action<ulong, float> workDone = null)
        {
            float work = 0f;
            foreach (var job in jobs)
            {
                work += job.Tick(deltaSeconds, speedOf, workDone);
            }

            return work;
        }

        public void ApplySyncedProgress(int index, float progress) => jobs[index].SetProgress(progress);

        /// <summary>Nächste noch kaputte Station in Reichweite, oder null.</summary>
        public RepairPoint NearestOpen(float worldX, float worldY, float maxDistance)
        {
            RepairPoint best = null;
            float bestDistance = maxDistance;
            foreach (var point in Layout.RepairPoints)
            {
                if (jobs[point.Index].IsComplete) continue;
                float d = Layout.DistanceTo(point, worldX, worldY);
                if (d <= bestDistance)
                {
                    best = point;
                    bestDistance = d;
                }
            }

            return best;
        }

        public static string Describe(JoinResult result, RepairPoint point, CrashSiteState state)
        {
            switch (result)
            {
                case JoinResult.AlreadyRepaired: return $"{point.Name} läuft bereits.";
                case JoinResult.MissingRequirement: return $"{point.Name} braucht zuerst: {state.Point(point.Requires.Value).Name}.";
                default: return null;
            }
        }
    }
}
