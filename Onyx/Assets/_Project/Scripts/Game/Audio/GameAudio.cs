using System.Collections.Generic;
using Onyx.Core.Audio;
using Onyx.Core.CrashSite;
using Onyx.Core.Skills;
using Onyx.Game.CrashSite;
using Onyx.Game.Player;
using Onyx.Game.Session;
using UnityEngine;

namespace Onyx.Game.Audio
{
    /// <summary>
    /// Spielt die Geräusche aus <see cref="SoundSynth"/>: Schritte, Ratsche, Meldungen, Sauerstoff-Alarm
    /// und Wind. Hängt am dauerhaften Objekt [Onyx]. Geräusche anderer Spieler werden mit der
    /// Entfernung zur eigenen Figur leiser und nach links/rechts verteilt.
    /// </summary>
    public sealed class GameAudio : MonoBehaviour
    {
        private const string VolumeKey = "Onyx.Volume";
        private const int Voices = 10;

        /// <summary>Ab dieser Entfernung (Kacheln) hört man andere Spieler nicht mehr.</summary>
        public const float HearingRange = 14f;

        public static GameAudio Instance { get; private set; }

        private readonly Dictionary<(Sfx, int), AudioClip> clips = new Dictionary<(Sfx, int), AudioClip>();
        private readonly List<AudioSource> voices = new List<AudioSource>();
        private int nextVoice;
        private AudioSource wind;
        private AudioListener ownListener;
        private float listenerCheck;

        private float alarmCooldown;
        private bool wasDowned;
        private CharacterSkills subscribedCharacter;

        /// <summary>Gesamtlautstärke 0–1 (wird gespeichert).</summary>
        public static float Volume
        {
            get => PlayerPrefs.GetFloat(VolumeKey, 0.8f);
            set
            {
                PlayerPrefs.SetFloat(VolumeKey, Mathf.Clamp01(value));
                AudioListener.volume = Mathf.Clamp01(value);
            }
        }

        private void Awake()
        {
            Instance = this;
            AudioListener.volume = Volume;
            for (int i = 0; i < Voices; i++)
            {
                var source = gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 0f;
                voices.Add(source);
            }

            wind = gameObject.AddComponent<AudioSource>();
            wind.playOnAwake = false;
            wind.loop = true;
            wind.volume = 0f;
            wind.clip = MakeClip("Wind", SoundSynth.WindLoop());

            CrashSiteNet.StationRepaired += OnStationRepaired;
        }

        private void OnDestroy()
        {
            CrashSiteNet.StationRepaired -= OnStationRepaired;
            if (subscribedCharacter != null) subscribedCharacter.LevelChanged -= OnLevelChanged;
            if (Instance == this) Instance = null;
        }

        // ------------------------------------------------------------------ Abspielen

        /// <summary>Spielt ein Geräusch ohne Ortung (eigene Aktionen, Meldungen).</summary>
        public static void Play(Sfx sound, float volume = 1f, float pitch = 1f, int variant = 0)
        {
            if (Instance != null) Instance.PlayInternal(sound, volume, pitch, 0f, variant);
        }

        /// <summary>Spielt ein Geräusch an einem Ort: leiser mit Abstand zur eigenen Figur, seitlich verteilt.</summary>
        public static void PlayAt(Sfx sound, Vector3 position, float volume = 1f, float pitch = 1f, int variant = 0)
        {
            if (Instance == null) return;
            var listener = PlayerVitals.Local;
            if (listener == null)
            {
                Instance.PlayInternal(sound, volume, pitch, 0f, variant);
                return;
            }

            Vector2 offset = position - listener.transform.position;
            float distance = offset.magnitude;
            if (distance >= HearingRange) return;
            float falloff = 1f - distance / HearingRange;
            Instance.PlayInternal(sound, volume * falloff * falloff, pitch, Mathf.Clamp(offset.x / 8f, -0.8f, 0.8f), variant);
        }

        private void PlayInternal(Sfx sound, float volume, float pitch, float pan, int variant)
        {
            var source = voices[nextVoice];
            nextVoice = (nextVoice + 1) % voices.Count;
            source.clip = Clip(sound, variant);
            source.volume = Mathf.Clamp01(volume);
            source.pitch = pitch;
            source.panStereo = pan;
            source.Play();
        }

        private AudioClip Clip(Sfx sound, int variant)
        {
            if (!clips.TryGetValue((sound, variant), out var clip) || clip == null)
            {
                clip = MakeClip($"{sound} {variant}", SoundSynth.Create(sound, variant));
                clips[(sound, variant)] = clip;
            }

            return clip;
        }

        private static AudioClip MakeClip(string name, float[] samples)
        {
            var clip = AudioClip.Create(name, samples.Length, 1, SoundSynth.SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        // ------------------------------------------------------------------ Laufend

        private void Update()
        {
            EnsureListener();
            UpdateWind();
            UpdateLocalPlayer();
            UpdateCharacterSubscription();
        }

        /// <summary>Ohne AudioListener ist alles stumm: Hat die Szene keinen, bringt [Onyx] einen mit.</summary>
        private void EnsureListener()
        {
            listenerCheck -= Time.unscaledDeltaTime;
            if (listenerCheck > 0f) return;
            listenerCheck = 1f;

            var all = FindObjectsByType<AudioListener>();
            int others = 0;
            foreach (var l in all)
            {
                if (l != ownListener && l.enabled) others++;
            }

            if (others == 0 && ownListener == null)
            {
                ownListener = gameObject.AddComponent<AudioListener>();
            }
            else if (ownListener != null)
            {
                ownListener.enabled = others == 0;
            }
        }

        private void UpdateWind()
        {
            var site = CrashSiteNet.ActiveSite;
            var local = PlayerVitals.Local;
            if (site == null || local == null || GameSession.Instance == null)
            {
                wind.volume = Mathf.MoveTowards(wind.volume, 0f, Time.deltaTime);
                if (wind.volume <= 0f && wind.isPlaying) wind.Stop();
                return;
            }

            if (!wind.isPlaying) wind.Play();
            float night = 1f - GameSession.Instance.World.Clock.Daylight;
            Vector3 p = local.transform.position;
            bool sheltered = site.Layout.ZoneAt(p.x, p.y) != SiteZone.Outside;
            float target = (0.12f + 0.18f * night) * (sheltered ? 0.35f : 1f);
            wind.volume = Mathf.MoveTowards(wind.volume, target, Time.deltaTime * 0.3f);
        }

        private void UpdateLocalPlayer()
        {
            var local = PlayerVitals.Local;
            if (local == null)
            {
                wasDowned = false;
                return;
            }

            if (local.IsDowned != wasDowned)
            {
                Play(local.IsDowned ? Sfx.Downed : Sfx.Revived, 0.8f);
                wasDowned = local.IsDowned;
            }

            // Sauerstoff-Alarm: unter 25 % und kein Nachschub – unter 10 % doppelt so oft
            bool losingAir = local.Environment == VitalsEnvironment.NoAir || local.Environment == VitalsEnvironment.Cold;
            alarmCooldown -= Time.deltaTime;
            if (!local.IsDowned && losingAir && local.Oxygen < 0.25f && alarmCooldown <= 0f)
            {
                Play(Sfx.OxygenAlarm, 0.55f);
                alarmCooldown = local.Oxygen < 0.1f ? 0.8f : 1.6f;
            }
        }

        private void UpdateCharacterSubscription()
        {
            var character = GameSession.Instance != null ? GameSession.Instance.Character : null;
            if (character == subscribedCharacter) return;
            if (subscribedCharacter != null) subscribedCharacter.LevelChanged -= OnLevelChanged;
            subscribedCharacter = character;
            if (subscribedCharacter != null) subscribedCharacter.LevelChanged += OnLevelChanged;
        }

        private void OnLevelChanged(Discipline discipline, int level) => Play(Sfx.LevelUp, 0.7f);

        private void OnStationRepaired(RepairPoint point) => Play(Sfx.RepairDone, 0.8f);
    }
}
