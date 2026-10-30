using Onyx.Core;
using Onyx.Net;
using UnityEngine;

namespace Onyx.Game.DevTools
{
    /// <summary>
    /// Einfaches Entwickler-Menü zum Hosten und Beitreten per IP. Wird später durch das
    /// richtige Hauptmenü mit Steam ersetzt.
    /// </summary>
    public sealed class DevSessionPanel : MonoBehaviour
    {
        [SerializeField] private SessionLauncher launcher;

        private string address = "127.0.0.1";
        private string portText = SessionConfig.DefaultPort.ToString();

        private void OnGUI()
        {
            if (launcher == null)
            {
                return;
            }

            float scale = Mathf.Max(1f, Screen.height / 540f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));

            GUILayout.BeginArea(new Rect(10, 10, 260, 220), GUI.skin.box);
            GUILayout.Label("<b>Project Onyx · M0</b>", new GUIStyle(GUI.skin.label) { richText = true });

            if (!launcher.IsRunning)
            {
                GUILayout.Label("Adresse");
                address = GUILayout.TextField(address);
                GUILayout.Label("Port");
                portText = GUILayout.TextField(portText);
                ushort port = ushort.TryParse(portText, out var p) ? p : SessionConfig.DefaultPort;

                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Hosten"))
                {
                    launcher.Host(port);
                }

                if (GUILayout.Button("Beitreten"))
                {
                    launcher.Join(address, port);
                }

                GUILayout.EndHorizontal();
            }
            else
            {
                string role = launcher.IsHost ? "Host" : "Mitspieler";
                GUILayout.Label($"Rolle: {role}");
                if (launcher.IsHost)
                {
                    GUILayout.Label($"Spieler: {launcher.PlayerCount} / {GameConstants.MaxPlayers}");
                }

                GUILayout.Label("Bewegen: WASD / Pfeiltasten / Stick");
                if (GUILayout.Button(launcher.IsHost ? "Sitzung beenden" : "Verlassen"))
                {
                    launcher.Leave();
                }
            }

            if (!string.IsNullOrEmpty(launcher.LastMessage))
            {
                GUILayout.Label(launcher.LastMessage);
            }

            GUILayout.EndArea();
        }
    }
}
