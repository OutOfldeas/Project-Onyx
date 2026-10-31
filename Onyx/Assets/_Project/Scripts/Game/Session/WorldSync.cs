using System;
using System.Text;
using Onyx.Core.Galaxy;
using Onyx.Core.Persistence;
using Onyx.Core.Session;
using Onyx.Core.Skills;
using Unity.Netcode;
using UnityEngine;

namespace Onyx.Game.Session
{
    /// <summary>
    /// Hält den Spielstand aller Mitspieler gleich: Der Host schickt beim Beitreten und nach jeder
    /// Änderung (Schiff, Sprung, neue Welt) einen kompletten Stand – dieselben Daten wie beim
    /// Speichern. Mitspieler übernehmen ihn, ohne dass Gelände und Grafik neu entstehen.
    /// Sprünge fragen Mitspieler beim Host an. Liegt neben <c>CrashSiteNet</c> in der Szene.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public sealed class WorldSync : NetworkBehaviour
    {
        /// <summary>Länge eines Stücks beim Senden (Netzwerkpakete haben eine Höchstgröße).</summary>
        private const int ChunkLength = 3000;

        /// <summary>Höchstens so oft wird ein geänderter Stand verteilt.</summary>
        private const float BroadcastInterval = 0.5f;

        public static WorldSync Instance { get; private set; }

        /// <summary>Läuft eine Sitzung mit Synchronisierung? Sonst rechnet jeder lokal (z. B. Testgelände).</summary>
        public static bool Active => Instance != null && Instance.IsSpawned;

        /// <summary>Antwort auf einen Sprung (auch für angefragte Sprünge von Mitspielern).</summary>
        public static event Action<JumpResult, int, int> JumpAnswered;

        private WorldState subscribedWorld;
        private bool dirty;
        private float broadcastCooldown;
        private int snapshotCounter;

        // Empfang (Mitspieler)
        private int receivingId = -1;
        private string[] receivingChunks;
        private int receivedCount;

        public override void OnNetworkSpawn()
        {
            Instance = this;
            if (IsServer)
            {
                NetworkManager.OnClientConnectedCallback += OnClientConnected;
                GameSession.Instance.WorldReplaced += OnWorldReplaced;
                Subscribe(GameSession.Instance.World);
            }
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer)
            {
                if (NetworkManager != null) NetworkManager.OnClientConnectedCallback -= OnClientConnected;
                if (GameSession.Instance != null) GameSession.Instance.WorldReplaced -= OnWorldReplaced;
                Subscribe(null);
            }

            if (Instance == this)
            {
                Instance = null;
            }
        }

        // ------------------------------------------------------------------ Host: verteilen

        private void Subscribe(WorldState world)
        {
            if (subscribedWorld != null)
            {
                subscribedWorld.Ship.Changed -= MarkDirty;
                subscribedWorld.Navigation.Jumped -= OnJumped;
            }

            subscribedWorld = world;
            if (subscribedWorld != null)
            {
                subscribedWorld.Ship.Changed += MarkDirty;
                subscribedWorld.Navigation.Jumped += OnJumped;
            }
        }

        private void OnWorldReplaced()
        {
            Subscribe(GameSession.Instance.World);
            MarkDirty();
        }

        private void OnJumped(int from, int to) => MarkDirty();

        private void MarkDirty() => dirty = true;

        private void OnClientConnected(ulong clientId)
        {
            if (clientId != NetworkManager.ServerClientId)
            {
                SendSnapshot(RpcTarget.Single(clientId, RpcTargetUse.Temp));
            }
        }

        private void Update()
        {
            if (!IsSpawned || !IsServer)
            {
                return;
            }

            broadcastCooldown -= Time.unscaledDeltaTime;
            if (dirty && broadcastCooldown <= 0f && NetworkManager.ConnectedClientsIds.Count > 1)
            {
                dirty = false;
                broadcastCooldown = BroadcastInterval;
                SendSnapshot(RpcTarget.NotServer);
            }
        }

        private void SendSnapshot(BaseRpcTarget target)
        {
            var data = WorldPersistence.Capture(GameSession.Instance.World);
            string json = JsonUtility.ToJson(data);
            int count = Mathf.Max(1, Mathf.CeilToInt(json.Length / (float)ChunkLength));
            int id = ++snapshotCounter;
            for (int i = 0; i < count; i++)
            {
                int start = i * ChunkLength;
                string chunk = json.Substring(start, Mathf.Min(ChunkLength, json.Length - start));
                SnapshotChunkRpc(id, i, count, chunk, target);
            }
        }

        // ------------------------------------------------------------------ Mitspieler: übernehmen

        [Rpc(SendTo.SpecifiedInParams)]
        private void SnapshotChunkRpc(int snapshotId, int index, int count, string chunk, RpcParams rpcParams)
        {
            if (IsServer)
            {
                return;
            }

            if (snapshotId != receivingId)
            {
                receivingId = snapshotId;
                receivingChunks = new string[count];
                receivedCount = 0;
            }

            if (index < 0 || index >= receivingChunks.Length || receivingChunks[index] != null)
            {
                return;
            }

            receivingChunks[index] = chunk;
            receivedCount++;
            if (receivedCount == receivingChunks.Length)
            {
                var builder = new StringBuilder();
                foreach (string part in receivingChunks) builder.Append(part);
                receivingChunks = null;
                receivingId = -1;
                ApplySnapshot(builder.ToString());
            }
        }

        private static void ApplySnapshot(string json)
        {
            var session = GameSession.Instance;
            try
            {
                var data = JsonUtility.FromJson<WorldSaveData>(json);
                if (data.Seed != session.World.Seed)
                {
                    session.ReplaceWorld(WorldPersistence.Restore(data));
                }
                else
                {
                    WorldPersistence.Apply(session.World, data);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[Onyx] Stand vom Host konnte nicht übernommen werden: {e.Message}\n{e}");
            }
        }

        // ------------------------------------------------------------------ Sprünge

        /// <summary>Springen: Der Host springt sofort, Mitspieler fragen beim Host an.</summary>
        public void RequestJump(int targetSystemId)
        {
            if (IsServer)
            {
                ServerJump(targetSystemId, NetworkManager.LocalClientId);
            }
            else
            {
                RequestJumpRpc(targetSystemId);
            }
        }

        [Rpc(SendTo.Server)]
        private void RequestJumpRpc(int targetSystemId, RpcParams rpcParams = default)
        {
            ServerJump(targetSystemId, rpcParams.Receive.SenderClientId);
        }

        private void ServerJump(int target, ulong requester)
        {
            var world = GameSession.Instance.World;
            int from = world.Navigation.CurrentSystemId;
            if (target < 0 || target >= world.Galaxy.Systems.Count)
            {
                return;
            }

            bool newSystem = !world.Galaxy.GetSystem(target).Discovered;
            JumpResult result = world.Navigation.Jump(target);

            if (requester == NetworkManager.LocalClientId)
            {
                JumpAnswered?.Invoke(result, from, target);
            }
            else
            {
                JumpAnsweredRpc((int)result, from, target, RpcTarget.Single(requester, RpcTargetUse.Temp));
            }

            if (result == JumpResult.Ok && newSystem)
            {
                // Entdecken lernt die ganze Crew – jeder bekommt die Erfahrung auf seinen eigenen Charakter.
                DiscoveredRpc(target);
            }
        }

        [Rpc(SendTo.SpecifiedInParams)]
        private void JumpAnsweredRpc(int result, int from, int to, RpcParams rpcParams)
        {
            JumpAnswered?.Invoke((JumpResult)result, from, to);
        }

        [Rpc(SendTo.Everyone)]
        private void DiscoveredRpc(int systemId)
        {
            if (GameSession.Instance != null)
            {
                GameSession.Instance.Character.AddExperience(Discipline.Exploration, SkillEffects.ExplorationExperiencePerSystem);
            }
        }
    }
}
