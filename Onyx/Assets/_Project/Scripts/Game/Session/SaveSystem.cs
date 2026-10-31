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
    /// Speichert und lädt Welt und Charakter als JSON-Dateien. Beim Start wird beides geladen.
    /// Die Welt speichert der Host (oder wer allein spielt) regelmäßig, nach wichtigen Ereignissen
    /// und beim Beenden; Mitspieler speichern keine Welt – sie kommt vom Host. Den eigenen
    /// Charakter speichert jeder Spieler selbst.
    /// </summary>
    public sealed class SaveSystem : MonoBehaviour
    {
        public const float AutosaveIntervalSeconds = 60f;
        private const string FileName = "welt.json";
        private const string CharacterFileName = "charakter.json";
        public const float CharacterSaveIntervalSeconds = 30f;

        public static SaveSystem Instance { get; private set; }

        /// <summary>Ordner der Spielstände (unter Windows in AppData/LocalLow/…/Saves).</summary>
        public static string Folder => Path.Combine(Application.persistentDataPath, "Saves");

        public static string FilePath => Path.Combine(Folder, FileName);

        /// <summary>Der eigene Charakter (Skills) – speichert jeder Spieler selbst, auch als Mitspieler.</summary>
        public static string CharacterFilePath => Path.Combine(Folder, CharacterFileName);

        /// <summary>Zeitpunkt (unscaledTime) der letzten erfolgreichen Speicherung, für die Anzeige.</summary>
        public float LastSavedAt { get; private set; } = -100f;

        public string LastMessage { get; private set; } = string.Empty;

        private float playTimeSeconds;
        private float nextAutosave;
        private bool saveRequested;
        private bool characterSaveRequested;
        private float nextCharacterSave;

        private void Awake()
        {
            Instance = this;
        }

        private void Start()
        {
            if (File.Exists(CharacterFilePath))
            {
                LoadCharacter();
            }

            if (File.Exists(FilePath))
            {
                Load();
            }

            nextCharacterSave = Time.unscaledTime + CharacterSaveIntervalSeconds;
            if (GameSession.Instance != null)
            {
                GameSession.Instance.Character.LevelChanged += (_, __) => characterSaveRequested = true;
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

        /// <summary>Charakter bald speichern (z. B. nach dem Freischalten einer Fähigkeit).</summary>
        public void RequestCharacterSave() => characterSaveRequested = true;

        private void Update()
        {
            playTimeSeconds += Time.unscaledDeltaTime;

            if (characterSaveRequested || Time.unscaledTime >= nextCharacterSave)
            {
                characterSaveRequested = false;
                nextCharacterSave = Time.unscaledTime + CharacterSaveIntervalSeconds;
                SaveCharacter();
            }

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
            SaveCharacter();
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
                WriteSafely(FilePath, JsonUtility.ToJson(data, true));
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

        public bool SaveCharacter()
        {
            var session = GameSession.Instance;
            if (session == null)
            {
                return false;
            }

            try
            {
                WriteSafely(CharacterFilePath, JsonUtility.ToJson(CharacterPersistence.Capture(session.Character), true));
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[Onyx] Charakter speichern fehlgeschlagen: {e.Message}\n{e}");
                return false;
            }
        }

        public bool LoadCharacter()
        {
            var session = GameSession.Instance;
            if (session == null)
            {
                return false;
            }

            try
            {
                var data = JsonUtility.FromJson<CharacterSaveData>(File.ReadAllText(CharacterFilePath));
                CharacterPersistence.Restore(session.Character, data);
                Debug.Log("[Onyx] Charakter geladen.");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[Onyx] Charakter laden fehlgeschlagen: {e.Message}\n{e}");
                return false;
            }
        }

        /// <summary>
        /// Erst in eine Zwischendatei schreiben, dann austauschen: Ein Absturz beim Speichern
        /// zerstört so nie den letzten guten Stand. Die vorherige Fassung bleibt als .bak.
        /// </summary>
        private static void WriteSafely(string path, string content)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temp = path + ".tmp";
            File.WriteAllText(temp, content);
            if (File.Exists(path))
            {
                File.Copy(path, path + ".bak", true);
                File.Delete(path);
            }

            File.Move(temp, path);
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
