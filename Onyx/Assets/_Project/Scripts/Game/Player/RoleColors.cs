using UnityEngine;

namespace Onyx.Game.Player
{
    /// <summary>
    /// Signalfarben aus der Art Direction. Bis Disziplinen eingebaut sind, bekommt
    /// jeder Spieler nach Beitrittsreihenfolge eine eigene Farbe.
    /// </summary>
    public static class RoleColors
    {
        public static readonly Color Amber = new Color32(0xFF, 0xB5, 0x2E, 0xFF);   // Erkundung / Scout
        public static readonly Color Cyan = new Color32(0x39, 0xD5, 0xE6, 0xFF);    // Technik / Ingenieur
        public static readonly Color Violet = new Color32(0xC7, 0x7D, 0xFF, 0xFF);  // Diplomatie / Diplomat
        public static readonly Color Lime = new Color32(0xA6, 0xD8, 0x4A, 0xFF);    // Logistik / Logistiker

        private static readonly Color[] BySlot = { Amber, Cyan, Violet, Lime };

        public static Color ForSlot(ulong slot)
        {
            return BySlot[(int)(slot % (ulong)BySlot.Length)];
        }
    }
}
