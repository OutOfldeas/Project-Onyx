using Onyx.Core;

namespace Onyx.Net
{
    /// <summary>
    /// Grundeinstellungen für Spielsitzungen. Der Host ist zugleich Server und Spieler.
    /// </summary>
    public static class SessionConfig
    {
        /// <summary>Standard-Port für direkte Verbindungen per IP (Entwicklung und lokale Tests).</summary>
        public const ushort DefaultPort = 7777;

        /// <summary>Wie viele Spieler zusätzlich zum Host beitreten dürfen.</summary>
        public const int MaxClients = GameConstants.MaxPlayers - 1;
    }
}
