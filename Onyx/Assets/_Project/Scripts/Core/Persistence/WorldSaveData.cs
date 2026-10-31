using System;

namespace Onyx.Core.Persistence
{
    // Reine Datenklassen für Spielstände. Nur öffentliche Felder und einfache Typen, damit sie
    // sich in Unity mit JsonUtility (und später mit jedem anderen Format) speichern lassen.
    // Neue Felder immer hinten anfügen und mit sinnvollem Standardwert versehen; bei
    // inkompatiblen Änderungen WorldPersistence.CurrentVersion erhöhen.

    [Serializable]
    public sealed class WorldSaveData
    {
        public int Version;
        public string SavedAtUtc;
        public float PlayTimeSeconds;

        public int Seed;
        public float TimeOfDay;
        public int DayCount;

        public float[] RepairProgress;

        public int CurrentSystemId;
        public float Fuel;
        public string OrbitPlanetId;
        public int[] DiscoveredSystems;

        public ShipSaveData Ship;
    }

    [Serializable]
    public sealed class ShipSaveData
    {
        public string Name;
        public int Width;
        public int Height;

        /// <summary>Rumpfkacheln als Index y * Width + x.</summary>
        public int[] Hull;

        public ModuleSaveData[] Modules;
    }

    [Serializable]
    public sealed class ModuleSaveData
    {
        public string Id;
        public int X;
        public int Y;
        public bool Rotated;
    }

    [Serializable]
    public sealed class CharacterSaveData
    {
        public int Version;

        /// <summary>Je Disziplin in der Reihenfolge von DisciplineNames.All.</summary>
        public int[] Levels;
        public float[] Experience;
        public int[] Locks;
        public string[] Unlocked;
    }
}
