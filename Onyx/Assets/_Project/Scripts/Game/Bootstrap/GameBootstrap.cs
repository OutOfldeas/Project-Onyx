using UnityEngine;

namespace Onyx.Game.Bootstrap
{
    /// <summary>
    /// Startpunkt des Spiels. Läuft automatisch, bevor die erste Szene geladen wird – egal,
    /// welche Szene im Editor geöffnet ist. Legt ein dauerhaftes Wurzelobjekt an, an das
    /// später Netzwerk, Speicherung und andere Dienste gehängt werden.
    /// </summary>
    public static class GameBootstrap
    {
        private const string RootName = "[Onyx]";

        /// <summary>Dauerhaftes Objekt, das Szenenwechsel überlebt.</summary>
        public static GameObject Root { get; private set; }

        // Setzt den Zustand zurück, falls „Enter Play Mode Options“ das Neuladen der Domain abschalten.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Root = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            if (Root != null)
            {
                return;
            }

            Root = new GameObject(RootName);
            Object.DontDestroyOnLoad(Root);

            Debug.Log($"{RootName} Bootstrap: {Application.productName} {Application.version}, Unity {Application.unityVersion}");
        }
    }
}
