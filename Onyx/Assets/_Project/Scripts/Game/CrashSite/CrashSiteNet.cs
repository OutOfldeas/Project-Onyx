using Onyx.Core.CrashSite;
using Onyx.Core.Session;
using Onyx.Game.Player;
using Onyx.Game.Session;
using Unity.Netcode;
using UnityEngine;

namespace Onyx.Game.CrashSite
{
    /// <summary>
    /// Netzwerk-Anker der Absturzstelle (liegt in der Szene). Der Host rechnet Reparaturen und
    /// Tageszeit und verteilt sie; Mitspieler übernehmen die Werte in ihren lokalen Spielstand.
    /// Ist dieses Objekt aktiv, gelten Kollision, Sauerstoff und Kälte der Absturzstelle.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public sealed class CrashSiteNet : NetworkBehaviour
    {
        /// <summary>So lange müssen alle kampfunfähig sein, bevor die Rettung sie zur Kapsel zurückholt.</summary>
        public const float RescueDelaySeconds = 6f;

        public static CrashSiteNet Instance { get; private set; }

        private readonly NetworkVariable<int> seed = new NetworkVariable<int>();
        private readonly NetworkVariable<float> timeOfDay = new NetworkVariable<float>();
        private NetworkList<float> progress;

        private float timeSyncCooldown;
        private float allDownedFor;

        /// <summary>Spielstand der Absturzstelle, sobald die Sitzung läuft – sonst null.</summary>
        public static CrashSiteState ActiveSite =>
            Instance != null && Instance.IsSpawned && GameSession.Instance != null ? GameSession.Instance.World.CrashSite : null;

        /// <summary>Eine Station ist fertig (auf allen Rechnern, für Meldungen im HUD).</summary>
        public static event System.Action<RepairPoint> StationRepaired;

        private void Awake()
        {
            progress = new NetworkList<float>();
            gameObject.AddComponent<CrashSiteView>();
        }

        public override void OnNetworkSpawn()
        {
            Instance = this;
            var session = GameSession.Instance;

            if (IsServer)
            {
                seed.Value = session.World.Seed;
                FillProgress();
                session.WorldReplaced += OnServerWorldReplaced;
                NetworkManager.OnClientDisconnectCallback += OnClientDisconnect;
            }
            else
            {
                AdoptSeed(seed.Value);
                seed.OnValueChanged += (_, value) => AdoptSeed(value);
                progress.OnListChanged += OnProgressChanged;
                ApplyAllProgress();
                session.World.Clock.SetTimeOfDay(timeOfDay.Value);
            }

            Subscribe(session.World);
        }

        public override void OnNetworkDespawn()
        {
            var session = GameSession.Instance;
            if (session != null)
            {
                session.WorldReplaced -= OnServerWorldReplaced;
                Unsubscribe(session.World);
            }

            if (IsServer && NetworkManager != null)
            {
                NetworkManager.OnClientDisconnectCallback -= OnClientDisconnect;
            }

            progress.OnListChanged -= OnProgressChanged;
            if (Instance == this)
            {
                Instance = null;
            }
        }

        // ------------------------------------------------------------------ Host

        private void Update()
        {
            if (!IsSpawned || GameSession.Instance == null)
            {
                return;
            }

            var world = GameSession.Instance.World;
            if (IsServer)
            {
                world.CrashSite.Tick(Time.deltaTime);
                PushProgress(world.CrashSite);

                timeSyncCooldown -= Time.unscaledDeltaTime;
                if (timeSyncCooldown <= 0f)
                {
                    timeSyncCooldown = 0.5f;
                    timeOfDay.Value = world.Clock.TimeOfDay;
                }

                CheckRescue();
            }
            else if (Mathf.Abs(Mathf.DeltaAngle(world.Clock.TimeOfDay * 360f, timeOfDay.Value * 360f)) > 3f)
            {
                // Nur bei spürbarer Abweichung nachziehen; dazwischen läuft die Uhr lokal weiter.
                world.Clock.SetTimeOfDay(timeOfDay.Value);
            }
        }

        private void FillProgress()
        {
            var site = GameSession.Instance.World.CrashSite;
            progress.Clear();
            for (int i = 0; i < site.Layout.RepairPoints.Count; i++)
            {
                progress.Add(site.Job(i).Progress);
            }
        }

        private void PushProgress(CrashSiteState site)
        {
            for (int i = 0; i < progress.Count; i++)
            {
                var job = site.Job(i);
                float sent = progress[i];
                bool sentComplete = sent >= job.RequiredWork;
                if (Mathf.Abs(job.Progress - sent) > 0.25f || job.IsComplete != sentComplete || (job.Progress != sent && job.WorkerCount == 0))
                {
                    progress[i] = job.Progress;
                }
            }
        }

        /// <summary>Spieler beginnt zu reparieren (vom Spielerobjekt per RPC weitergereicht).</summary>
        public JoinResult ServerJoin(int index, ulong clientId)
        {
            var site = GameSession.Instance.World.CrashSite;
            if (index < 0 || index >= site.Layout.RepairPoints.Count)
            {
                return JoinResult.AlreadyRepaired;
            }

            return site.Join(index, clientId);
        }

        public void ServerLeave(ulong clientId)
        {
            GameSession.Instance.World.CrashSite.LeaveAll(clientId);
        }

        private void OnClientDisconnect(ulong clientId)
        {
            ServerLeave(clientId);
        }

        private void OnServerWorldReplaced()
        {
            seed.Value = GameSession.Instance.World.Seed;
            FillProgress();
            Subscribe(GameSession.Instance.World);
        }

        /// <summary>Sind alle Spieler kampfunfähig, holt die Rettungsautomatik der Kapsel sie zurück.</summary>
        private void CheckRescue()
        {
            var all = PlayerVitals.All;
            bool everyoneDown = all.Count > 0;
            foreach (var vitals in all)
            {
                if (!vitals.IsDowned)
                {
                    everyoneDown = false;
                    break;
                }
            }

            allDownedFor = everyoneDown ? allDownedFor + Time.deltaTime : 0f;
            if (allDownedFor < RescueDelaySeconds)
            {
                return;
            }

            allDownedFor = 0f;
            int slot = 0;
            foreach (var vitals in all)
            {
                var (x, y) = SpawnLayout.GetSpawnOffset(slot++);
                vitals.ServerRescue(new Vector3(x, y, 0f));
            }
        }

        // ------------------------------------------------------------------ Mitspieler

        private void AdoptSeed(int value)
        {
            var session = GameSession.Instance;
            if (session.World.Seed != value)
            {
                Unsubscribe(session.World);
                session.ReplaceWorld(WorldState.CreateNew(value));
                Subscribe(session.World);
            }
        }

        private void OnProgressChanged(NetworkListEvent<float> change)
        {
            ApplyAllProgress();
        }

        private void ApplyAllProgress()
        {
            var site = GameSession.Instance.World.CrashSite;
            for (int i = 0; i < progress.Count && i < site.Layout.RepairPoints.Count; i++)
            {
                site.ApplySyncedProgress(i, progress[i]);
            }
        }

        // ------------------------------------------------------------------ Meldungen

        private CrashSiteState subscribedSite;

        private void Subscribe(WorldState world)
        {
            if (subscribedSite == world.CrashSite) return;
            if (subscribedSite != null) subscribedSite.StationRepaired -= RaiseRepaired;
            subscribedSite = world.CrashSite;
            subscribedSite.StationRepaired += RaiseRepaired;
        }

        private void Unsubscribe(WorldState world)
        {
            if (subscribedSite != null) subscribedSite.StationRepaired -= RaiseRepaired;
            subscribedSite = null;
        }

        private static void RaiseRepaired(RepairPoint point) => StationRepaired?.Invoke(point);
    }
}
