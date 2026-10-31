using System.Collections.Generic;
using Onyx.Core.CrashSite;
using Onyx.Core.Skills;
using Onyx.Game.CrashSite;
using Onyx.Game.Session;
using Unity.Netcode;
using UnityEngine;

namespace Onyx.Game.Player
{
    /// <summary>
    /// Verbindet den lokal gespeicherten Charakter eines Spielers mit der Sitzung: Der Besitzer
    /// meldet, wie schnell er repariert und wie viel Sauerstoff er braucht; der Host schickt ihm
    /// Erfahrung für das, was er getan hat (Lernen durch Tun). Ehrlichkeit unter Freunden
    /// vorausgesetzt – der Charakter liegt beim Spieler, nicht beim Host.
    /// </summary>
    public sealed class PlayerCharacter : NetworkBehaviour
    {
        private static readonly Dictionary<ulong, PlayerCharacter> byClient = new Dictionary<ulong, PlayerCharacter>();

        private readonly NetworkVariable<float> repairSpeed =
            new NetworkVariable<float>(1f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        private readonly NetworkVariable<float> oxygenUseFactor =
            new NetworkVariable<float>(1f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        private float publishCooldown;
        private Vector3 lastPosition;
        private float walkedOutside;

        public float RepairSpeed => repairSpeed.Value;
        public float OxygenUseFactor => oxygenUseFactor.Value;

        /// <summary>Arbeitstempo eines Spielers für den Host (1, wenn unbekannt).</summary>
        public static float RepairSpeedOf(ulong clientId) =>
            byClient.TryGetValue(clientId, out var c) && c != null ? Mathf.Max(0.1f, c.RepairSpeed) : 1f;

        public static PlayerCharacter ForClient(ulong clientId) =>
            byClient.TryGetValue(clientId, out var c) ? c : null;

        public override void OnNetworkSpawn()
        {
            byClient[OwnerClientId] = this;
            lastPosition = transform.position;
        }

        public override void OnNetworkDespawn()
        {
            if (byClient.TryGetValue(OwnerClientId, out var c) && c == this)
            {
                byClient.Remove(OwnerClientId);
            }
        }

        private void Update()
        {
            if (!IsSpawned || !IsOwner || GameSession.Instance == null)
            {
                return;
            }

            var character = GameSession.Instance.Character;

            publishCooldown -= Time.deltaTime;
            if (publishCooldown <= 0f)
            {
                publishCooldown = 0.5f;
                float speed = SkillEffects.RepairSpeed(character);
                float oxygen = SkillEffects.OxygenUseFactor(character);
                if (!Mathf.Approximately(repairSpeed.Value, speed)) repairSpeed.Value = speed;
                if (!Mathf.Approximately(oxygenUseFactor.Value, oxygen)) oxygenUseFactor.Value = oxygen;
            }

            TrackExploration(character);
        }

        /// <summary>Wer draußen unterwegs ist, lernt Erkundung (wird lokal berechnet, die Bewegung gehört dem Besitzer).</summary>
        private void TrackExploration(CharacterSkills character)
        {
            Vector3 position = transform.position;
            float step = Vector2.Distance(position, lastPosition);
            lastPosition = position;

            var site = CrashSiteNet.ActiveSite;
            if (site == null || step > 2f || site.Layout.ZoneAt(position.x, position.y) != SiteZone.Outside)
            {
                return; // Teleports und Wege in Kapsel/Wrack zählen nicht
            }

            walkedOutside += step;
            if (walkedOutside >= 4f)
            {
                character.AddExperience(Discipline.Exploration, walkedOutside * SkillEffects.ExplorationExperiencePerTile);
                walkedOutside = 0f;
            }
        }

        // ------------------------------------------------------------------ Host → Besitzer

        /// <summary>Host: Erfahrung an den Besitzer dieses Charakters schicken.</summary>
        public void ServerGrant(Discipline discipline, float amount)
        {
            if (IsServer && amount > 0f)
            {
                GrantExperienceRpc((int)discipline, amount);
            }
        }

        [Rpc(SendTo.Owner)]
        private void GrantExperienceRpc(int discipline, float amount)
        {
            if (GameSession.Instance != null)
            {
                GameSession.Instance.Character.AddExperience((Discipline)discipline, amount);
            }
        }
    }
}
