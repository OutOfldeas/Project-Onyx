using System;
using Onyx.Core.Session;
using Onyx.Core.Skills;
using UnityEngine;

namespace Onyx.Game.Session
{
    /// <summary>
    /// Hält den Spielstand der aktuellen Welt (<see cref="WorldState"/>) und lässt ihn laufen.
    /// Hängt am dauerhaften Objekt [Onyx]. Vorerst lokal; Synchronisierung mit Mitspielern
    /// und Speichern folgen mit M2.
    /// </summary>
    public sealed class GameSession : MonoBehaviour
    {
        /// <summary>Seed der Standardwelt, solange es noch kein Menü für neue Welten gibt.</summary>
        public const int DefaultSeed = 20261030;

        public static GameSession Instance { get; private set; }

        public WorldState World { get; private set; }

        /// <summary>
        /// Der Charakter des Spielers an diesem Rechner. Gehört dem Spieler, nicht der Welt:
        /// Er wird in jede geladene oder beigetretene Welt mitgenommen und separat gespeichert.
        /// </summary>
        public CharacterSkills Character { get; private set; }

        /// <summary>Zeitraffer für die Simulation (Dev-Modus). 1 = Echtzeit.</summary>
        public float TimeScale { get; set; } = 1f;

        /// <summary>Wird ausgelöst, wenn eine neue Welt geladen oder erzeugt wurde.</summary>
        public event Action WorldReplaced;

        private void Awake()
        {
            Instance = this;
            World = WorldState.CreateNew(DefaultSeed);
            Character = World.LocalSkills;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void Update()
        {
            World.Tick(Time.deltaTime * Mathf.Max(0f, TimeScale));
        }

        public void ReplaceWorld(WorldState world)
        {
            World = world ?? throw new ArgumentNullException(nameof(world));
            World.AttachLocalCharacter(Character);
            WorldReplaced?.Invoke();
        }
    }
}
