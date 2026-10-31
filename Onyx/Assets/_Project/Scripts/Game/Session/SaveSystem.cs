using System;
using System.IO;
using Onyx.Core.CrashSite;
using Onyx.Core.Persistence;
using Onyx.Game.CrashSite;
using Unity.Netcode;
using UnityEngine;

namespace Onyx.Game.Session
{
    /// <summary>
    /// Speichert und lädt die Welt als JSON-Datei. Beim Start wird ein vorhandener Spielstand
    /// geladen; danach speichert der Host (oder wer allein spielt) regelmäßig, nach wichtigen
    /// Ereignissen und beim Beenden. Mitspieler speichern nicht – ihre Welt kommt vom Host.
    /// </summary>
    public sealed class SaveSystem : MonoBehaviour
    {
        public const float AutosaveIntervalSeconds = 60f;
        private const string FileName = "welt.json";

        public static SaveSystem Instance { get; private set; }

        /// <summary>Ordner der Spielstände (unter Windows in AppData/LocalLow/…/Saves).</summary>
        public static string Folder => Path.Combine(Application.persistentDataPath, "Saves");

        public static string FilePath => Path.Combine(Folder, FileName);

        /// <summary>Zeitpunkt (unscaledTime) der letzten erfolgreichen Speicherung, für die Anzeige.</summary>
        public float LastSavedAt { get; private set; } = -100f;

        public string LastMessage { get; private set; } = string.Empty;

        private float playTimeSeconds;
        private float nextAutosave;
        private bool saveRequested;

        private void Awake()
        {
            Instance = this;
        }

        private void Start()
        {
            if (File.Exists(FilePath))
            {
                Load();
            }

            nextAutosave = Time.unscaledTime + AutosaveIntervalSeconds;
            CrashSiteNet.StationRepaired += OnStationRepaired;
        }

        private void OnDestroy()
        {
            CrashSiteNet.StationRepaired -= OnStationRepaired;
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void OnStationRepaired(RepairPoint point) => saveRequested = true;

        /// <summary>Darf dieser Rechner speichern? Nicht als reiner Mitspieler.</summary>
        public static bool MaySave
        {
            get
            {
                var manager = NetworkManager.Singleton;
                return manager == null || !manager.IsListening || manager.IsServer;
            }
        }

        private void Update()
        {
            playTimeSeconds += Time.unscaledDeltaTime;
            if (!MaySave)
            {
                return;
            }

            if (saveRequested || Time.unscaledTime >= nextAutosave)
            {
                saveRequested = false;
                nextAutosave = Time.unscaledTime + AutosaveIntervalSeconds;
                Save();
            }
        }

        private void OnApplicationQuit()
        {
            if (MaySave)
            {
                Save();
            }
        }

        public bool Save()
        {
            var session = GameSession.Instance;
            if (session == null)
            {
                return false;
            }

            try
            {
                var data = WorldPersistence.Capture(session.World, playTimeSeconds);
                string json = JsonUtility.ToJson(data, true);
                Directory.CreateDirectory(Folder);

                // Erst in eine Zwischendatei schreiben, dann austauschen: Ein Absturz beim
                // Speichern zerstört so nie den letzten guten Spielstand.
                string temp = FilePath + ".tmp";
                File.WriteAllText(temp, json);
                if (File.Exists(FilePath))
                {
                    File.Copy(FilePath, FilePath + ".bak", true);
                    File.Delete(FilePath);
                }

                File.Move(temp, FilePath);
                LastSavedAt = Time.unscaledTime;
                LastMessage = $"Gespeichert ({DateTime.Now:HH:mm:ss}).";
                return true;
            }
            catch (Exception e)
            {
                LastMessage = $"Speichern fehlgeschlagen: {e.Message}";
                Debug.LogError($"[Onyx] {LastMessage}\n{e}");
                return false;
            }
        }

        public bool Load()
        {
            var session = GameSession.Instance;
            if (session == null)
            {
                return false;
            }

            try
            {
                string json = File.ReadAllText(FilePath);
                var data = JsonUtility.FromJson<WorldSaveData>(json);
                var world = WorldPersistence.Restore(data);
                playTimeSeconds = data.PlayTimeSeconds;
                session.ReplaceWorld(world);
                LastMessage = $"Spielstand geladen (Seed {data.Seed}, gespeichert {data.SavedAtUtc}).";
                Debug.Log($"[Onyx] {LastMessage}");
                return true;
            }
            catch (Exception e)
            {
                LastMessage = $"Laden fehlgeschlagen: {e.Message}";
                Debug.LogError($"[Onyx] {LastMessage}\n{e}");
                return false;
            }
        }

        /// <summary>Löscht den Spielstand (die laufende Welt bleibt, bis eine neue erzeugt wird).</summary>
        public void Delete()
        {
            foreach (string path in new[] { FilePath, FilePath + ".bak", FilePath + ".tmp" })
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }

            LastMessage = "Spielstand gelöscht.";
        }
    }
}
