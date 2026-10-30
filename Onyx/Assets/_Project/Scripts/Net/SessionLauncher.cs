using Onyx.Core;
using Onyx.Core.Session;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace Onyx.Net
{
    /// <summary>
    /// Startet und beendet Spielsitzungen. Der Host ist zugleich Server und Spieler.
    /// Prüft beim Beitreten, ob die Welt voll ist, und legt den Startplatz fest.
    /// </summary>
    [RequireComponent(typeof(NetworkManager))]
    public sealed class SessionLauncher : MonoBehaviour
    {
        /// <summary>Mittelpunkt, um den die Spieler erscheinen (Weltkoordinaten).</summary>
        [SerializeField] private Vector2 spawnCenter = Vector2.zero;

        private NetworkManager networkManager;
        private UnityTransport transport;

        /// <summary>Text für die Anzeige, z. B. warum eine Verbindung getrennt wurde.</summary>
        public string LastMessage { get; private set; } = string.Empty;

        public bool IsRunning => networkManager != null && networkManager.IsListening;

        public bool IsHost => networkManager != null && networkManager.IsHost;

        public int PlayerCount => networkManager != null && networkManager.IsServer
            ? networkManager.ConnectedClientsIds.Count
            : 0;

        private void Awake()
        {
            networkManager = GetComponent<NetworkManager>();
            transport = GetComponent<UnityTransport>();
            if (transport == null)
            {
                transport = gameObject.AddComponent<UnityTransport>();
            }

            networkManager.NetworkConfig.NetworkTransport = transport;
            networkManager.NetworkConfig.ConnectionApproval = true;
        }

        private void OnEnable()
        {
            if (networkManager == null)
            {
                return;
            }

            networkManager.ConnectionApprovalCallback = ApproveConnection;
            networkManager.OnClientDisconnectCallback += HandleClientDisconnect;
        }

        private void OnDisable()
        {
            if (networkManager == null)
            {
                return;
            }

            networkManager.ConnectionApprovalCallback = null;
            networkManager.OnClientDisconnectCallback -= HandleClientDisconnect;
        }

        /// <summary>Eröffnet eine Sitzung auf diesem Rechner und spielt selbst mit.</summary>
        public bool Host(ushort port = SessionConfig.DefaultPort)
        {
            transport.SetConnectionData("127.0.0.1", port, "0.0.0.0");
            bool ok = networkManager.StartHost();
            LastMessage = ok ? $"Sitzung läuft auf Port {port}." : "Sitzung konnte nicht gestartet werden.";
            return ok;
        }

        /// <summary>Tritt einer Sitzung unter der angegebenen Adresse bei.</summary>
        public bool Join(string address, ushort port = SessionConfig.DefaultPort)
        {
            if (string.IsNullOrWhiteSpace(address))
            {
                LastMessage = "Bitte eine Adresse eingeben.";
                return false;
            }

            transport.SetConnectionData(address.Trim(), port);
            bool ok = networkManager.StartClient();
            LastMessage = ok ? $"Verbinde mit {address}:{port} …" : "Verbindung konnte nicht gestartet werden.";
            return ok;
        }

        /// <summary>Verlässt die Sitzung bzw. beendet sie (als Host).</summary>
        public void Leave()
        {
            if (networkManager.IsListening)
            {
                networkManager.Shutdown();
            }

            LastMessage = "Sitzung verlassen.";
        }

        private void ApproveConnection(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            int slot = networkManager.ConnectedClientsIds.Count;
            if (slot >= GameConstants.MaxPlayers)
            {
                response.Approved = false;
                response.Reason = $"Die Welt ist voll (max. {GameConstants.MaxPlayers} Spieler).";
                return;
            }

            var (x, y) = SpawnLayout.GetSpawnOffset(slot);
            response.Approved = true;
            response.CreatePlayerObject = true;
            response.Position = new Vector3(spawnCenter.x + x, spawnCenter.y + y, 0f);
            response.Rotation = Quaternion.identity;
        }

        private void HandleClientDisconnect(ulong clientId)
        {
            // Nur relevant, wenn wir selbst getrennt wurden.
            if (networkManager.IsServer && clientId != networkManager.LocalClientId)
            {
                return;
            }

            string reason = networkManager.DisconnectReason;
            LastMessage = string.IsNullOrEmpty(reason) ? "Verbindung getrennt." : reason;
        }
    }
}
