namespace Onyx.Core
{
    /// <summary>
    /// Feste Eckdaten des Spiels, die überall gleich sein müssen.
    /// </summary>
    public static class GameConstants
    {
        /// <summary>Höchstzahl gleichzeitiger Spieler in einer Welt (Host eingeschlossen).</summary>
        public const int MaxPlayers = 4;

        /// <summary>Pixel pro Unity-Einheit. Eine Kachel entspricht genau einer Einheit.</summary>
        public const int PixelsPerUnit = 16;

        /// <summary>Kantenlänge einer Bodenkachel in Pixeln.</summary>
        public const int TileSizePx = 16;

        /// <summary>Breite einer Spielfigur in Pixeln.</summary>
        public const int CharacterWidthPx = 16;

        /// <summary>Höhe einer Spielfigur in Pixeln.</summary>
        public const int CharacterHeightPx = 32;
    }
}
