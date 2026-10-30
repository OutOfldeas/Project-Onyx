using System;

namespace Onyx.Core.Session
{
    /// <summary>
    /// Verteilt Spieler beim Betreten der Welt auf feste Plätze rund um einen Mittelpunkt,
    /// damit niemand auf einem anderen erscheint. Ergebnis in Kachel-Einheiten.
    /// </summary>
    public static class SpawnLayout
    {
        /// <summary>Abstand der Startplätze vom Mittelpunkt in Kacheln.</summary>
        public const float Radius = 1.5f;

        /// <summary>
        /// Liefert den Startplatz für den Spieler mit dem Platzindex <paramref name="slot"/>
        /// (0 = erster Spieler, normalerweise der Host).
        /// </summary>
        public static (float X, float Y) GetSpawnOffset(int slot)
        {
            if (slot < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(slot), slot, "Platzindex darf nicht negativ sein.");
            }

            // Vier Plätze im Quadrat (oben links, oben rechts, unten links, unten rechts).
            // Weitere Spieler beginnen eine neue, weiter außen liegende Runde.
            int ring = slot / GameConstants.MaxPlayers;
            int corner = slot % GameConstants.MaxPlayers;
            float distance = Radius * (ring + 1);

            float x = corner % 2 == 0 ? -distance : distance;
            float y = corner < 2 ? distance : -distance;
            return (x, y);
        }
    }
}
