using System.Reflection;
using Onyx.Game.CrashSite;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Onyx.Editor
{
    /// <summary>
    /// Legt per Menü die Szene für M1 an: die Absturzstelle mit Fluchtkapsel und Fregattenwrack.
    /// Gelände, Wrack und Lichter entstehen zur Laufzeit aus dem Seed (CrashSiteView);
    /// in der Szene liegt nur der Netzwerk-Anker <see cref="CrashSiteNet"/>.
    /// Erneutes Ausführen überschreibt Szene und Spieler-Prefab.
    /// </summary>
    public static class M1SceneBuilder
    {
        private const string ScenePath = M0SceneBuilder.SceneFolder + "/M1_Absturzstelle.unity";

        [MenuItem("Onyx/Setup/M1-Absturzstelle erstellen")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            M0SceneBuilder.EnsureFolder(M0SceneBuilder.PrefabFolder);
            M0SceneBuilder.EnsureFolder(M0SceneBuilder.SceneFolder);
            M0SceneBuilder.EnsureFolder(M0SceneBuilder.NetworkDataFolder);

            GameObject playerPrefab = M0SceneBuilder.CreatePlayerPrefab();
            NetworkPrefabsList prefabList = M0SceneBuilder.CreatePrefabList(playerPrefab);

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            M0SceneBuilder.CreateNetworkCameraAndLight(playerPrefab, prefabList);

            var site = new GameObject("CrashSite");
            var networkObject = site.AddComponent<NetworkObject>();
            site.AddComponent<CrashSiteNet>();

            // Szene zuerst speichern: Erst dann kann Netcode dem Objekt eine feste ID geben.
            EditorSceneManager.SaveScene(scene, ScenePath);
            typeof(NetworkObject)
                .GetMethod("OnValidate", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                ?.Invoke(networkObject, null);
            EditorUtility.SetDirty(networkObject);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);

            M0SceneBuilder.PutSceneFirstInBuild(ScenePath);
            AssetDatabase.SaveAssets();

            EditorUtility.DisplayDialog(
                "Onyx",
                "M1-Absturzstelle angelegt:\n\n" +
                $"• {ScenePath}\n\n" +
                "Play drücken und oben links „Hosten“ wählen.\n" +
                "Ziel: Sauerstoffgenerator, Reaktor und Antrieb der Fregatte reparieren (E halten).",
                "OK");
        }
    }
}
