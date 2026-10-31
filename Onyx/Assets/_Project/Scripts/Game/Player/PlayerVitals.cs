using System.Collections.Generic;
using Onyx.Core.CrashSite;
using Onyx.Core.Survival;
using Onyx.Game.CrashSite;
using Onyx.Game.Session;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

namespace Onyx.Game.Player
{
    /// <summary>Was die Umgebung gerade mit dem Spieler macht (für die Anzeige).</summary>
    public enum VitalsEnvironment : byte
    {
        Safe,
        PodRefill,
        WreckAir,
        NoAir,
        Cold,
    }

    /// <summary>
    /// Sauerstoff, Schild und Gesundheit eines Spielers. Der Host rechnet (<see cref="LifeSupport"/>),
    /// alle anderen sehen die Werte über NetworkVariables. Kampfunfähige können von Mitspielern
    /// wiederbelebt werden.
    /// </summary>
    public sealed class PlayerVitals : NetworkBehaviour
    {
        /// <summary>Sekunden, die ein einzelner Helfer zum Wiederbeleben braucht.</summary>
        public const float ReviveSeconds = 4f;

        private static readonly List<PlayerVitals> all = new List<PlayerVitals>();

        /// <summary>Alle Spieler in der Sitzung.</summary>
        public static IReadOnlyList<PlayerVitals> All => all;

        /// <summary>Die eigene Figur auf diesem Rechner.</summary>
        public static PlayerVitals Local { get; private set; }

        private readonly NetworkVariable<float> oxygen = new NetworkVariable<float>(1f);
        private readonly NetworkVariable<float> shield = new NetworkVariable<float>(1f);
        private readonly NetworkVariable<float> health = new NetworkVariable<float>(1f);
        private readonly NetworkVariable<bool> downed = new NetworkVariable<bool>();
        private readonly NetworkVariable<byte> environment = new NetworkVariable<byte>();
        private readonly NetworkVariable<float> reviveProgress = new NetworkVariable<float>();

        private LifeSupport life;
        private LifeSupportSettings settings;
        private PlayerCharacter character;
        private float baseOxygenUse;
        private readonly HashSet<ulong> revivers = new HashSet<ulong>();

        /// <summary>Dev-Modus: nichts verbraucht sich (nur beim Host wirksam).</summary>
        public bool GodMode { get; set; }

        public float Oxygen => oxygen.Value;
        public float Shield => shield.Value;
        public float Health => health.Value;
        public bool IsDowned => downed.Value;
        public VitalsEnvironment Environment => (VitalsEnvironment)environment.Value;
        public float ReviveProgress => reviveProgress.Value;

        public override void OnNetworkSpawn()
        {
            all.Add(this);
            if (IsOwner)
            {
                Local = this;
            }

            if (IsServer)
            {
                settings = new LifeSupportSettings();
                baseOxygenUse = settings.OxygenUsePerSecond;
                life = new LifeSupport(settings);
                character = GetComponent<PlayerCharacter>();
            }
        }

        public override void OnNetworkDespawn()
        {
            all.Remove(this);
            if (Local == this)
            {
                Local = null;
            }
        }

        private void Update()
        {
            if (!IsSpawned || !IsServer || life == null)
            {
                return;
            }

            var site = CrashSiteNet.ActiveSite;
            Surroundings surroundings;
            VitalsEnvironment env;
            if (site == null || GodMode)
            {
                surroundings = new Surroundings(true);
                env = VitalsEnvironment.Safe;
            }
            else
            {
                float daylight = GameSession.Instance.World.Clock.Daylight;
                Vector3 p = transform.position;
                surroundings = site.SurroundingsAt(p.x, p.y, daylight);
                env = Classify(site.Layout.ZoneAt(p.x, p.y), surroundings);
            }

            // Fähigkeiten des Spielers (z. B. „Leichter Schritt“) wirken auf den Verbrauch.
            settings.OxygenUsePerSecond = baseOxygenUse * (character != null ? character.OxygenUseFactor : 1f);
            life.Tick(Time.deltaTime, surroundings);
            if (GodMode)
            {
                life.Oxygen.Refill(life.Oxygen.Max);
                life.Shield.Refill(life.Shield.Max);
                life.Health.Refill(life.Health.Max);
            }

            TickRevive();
            Publish(env);
        }

        private static VitalsEnvironment Classify(SiteZone zone, Surroundings s)
        {
            if (zone == SiteZone.Pod) return VitalsEnvironment.PodRefill;
            if (s.Breathable) return VitalsEnvironment.WreckAir;
            if (s.HazardDamagePerSecond > 0f) return VitalsEnvironment.Cold;
            return VitalsEnvironment.NoAir;
        }

        private void TickRevive()
        {
            if (life.State != VitalState.Downed || revivers.Count == 0)
            {
                if (reviveProgress.Value != 0f && revivers.Count == 0) reviveProgress.Value = 0f;
                return;
            }

            float speed = 1f + 0.25f * (revivers.Count - 1);
            float value = reviveProgress.Value + Time.deltaTime * speed / ReviveSeconds;
            if (value >= 1f)
            {
                life.Revive();
                revivers.Clear();
                value = 0f;
            }

            reviveProgress.Value = value;
        }

        private void Publish(VitalsEnvironment env)
        {
            SetIfChanged(oxygen, life.Oxygen.Ratio);
            SetIfChanged(shield, life.Shield.Ratio);
            SetIfChanged(health, life.Health.Ratio);
            if (downed.Value != (life.State == VitalState.Downed)) downed.Value = life.State == VitalState.Downed;
            if (environment.Value != (byte)env) environment.Value = (byte)env;
        }

        private static void SetIfChanged(NetworkVariable<float> variable, float value)
        {
            // Kleine Schwankungen nicht senden; volle und leere Werte immer exakt.
            if (Mathf.Abs(variable.Value - value) > 0.004f || ((value == 0f || value == 1f) && variable.Value != value))
            {
                variable.Value = value;
            }
        }

        // ------------------------------------------------------------------ Host-Befehle

        public void ServerAddReviver(ulong clientId)
        {
            if (IsServer && life != null && life.State == VitalState.Downed && clientId != OwnerClientId)
            {
                revivers.Add(clientId);
            }
        }

        public void ServerRemoveReviver(ulong clientId) => revivers.Remove(clientId);

        /// <summary>Volle Werte (Dev-Modus) – belebt auch wieder.</summary>
        public void ServerHeal()
        {
            if (life == null) return;
            life.Revive();
            life.Oxygen.Refill(life.Oxygen.Max);
            life.Shield.Refill(life.Shield.Max);
            life.Health.Refill(life.Health.Max);
        }

        /// <summary>Sofort kampfunfähig (Dev-Modus, zum Testen der Wiederbelebung).</summary>
        public void ServerKnockDown()
        {
            life?.ApplyDamage(life.Shield.Max + life.Health.Max);
        }

        public void ServerSetOxygen(float ratio)
        {
            if (life == null) return;
            life.Oxygen.Drain(life.Oxygen.Max);
            life.Oxygen.Refill(Mathf.Clamp01(ratio) * life.Oxygen.Max);
        }

        /// <summary>Rettung zur Kapsel: wiederbeleben und dorthin versetzen.</summary>
        public void ServerRescue(Vector3 position)
        {
            ServerHeal();
            if (life != null)
            {
                life.Health.Drain(life.Health.Max * 0.5f);
            }

            TeleportRpc(position);
        }

        [Rpc(SendTo.Owner)]
        private void TeleportRpc(Vector3 position)
        {
            // Die Position gehört dem Besitzer (NetworkTransform im Owner-Modus), also setzt er sie selbst.
            var networkTransform = GetComponent<NetworkTransform>();
            if (networkTransform != null)
            {
                networkTransform.Teleport(position, transform.rotation, transform.localScale);
            }
            else
            {
                transform.position = position;
            }
        }
    }
}
