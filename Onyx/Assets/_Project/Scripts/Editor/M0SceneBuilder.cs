using System.Collections.Generic;
using System.Linq;
using Onyx.Core;
using Onyx.Game.CameraRig;
using Onyx.Game.DevTools;
using Onyx.Game.Player;
using Onyx.Game.World;
using Onyx.Net;
using Unity.Netcode;
using Unity.Netcode.Components;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

namespace Onyx.Editor
{
    /// <summary>
    /// Legt per Menü das Testgelände für M0 an: Spieler-Prefab, Netzwerk-Prefab-Liste und Szene.
    /// Über die Unity-API erzeugt, damit alle IDs (z. B. für Netcode) korrekt vergeben werden.
    /// Kann gefahrlos erneut ausgeführt werden; vorhandene Dateien werden überschrieben.
    /// </summary>
    public static class M0SceneBuilder
    {
        private const string Root = "Assets/_Project";
        internal const string PrefabFolder = Root + "/Prefabs";
        internal const string SceneFolder = Root + "/Scenes";
        internal const string NetworkDataFolder = Root + "/Data/Network";

        private const string PlayerPrefabPath = PrefabFolder + "/Player.prefab";
        private const string PrefabListPath = NetworkDataFolder + "/OnyxNetworkPrefabs.asset";
        private const string ScenePath = SceneFolder + "/M0_Testgelaende.unity";

        // Referenzauflösung 480 × 270 → ×4 ergibt 1920 × 1080.
        private const int RefWidth = 480;
        private const int RefHeight = 270;

        [MenuItem("Onyx/Setup/M0-Testgelände erstellen")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            EnsureFolder(PrefabFolder);
            EnsureFolder(SceneFolder);
            EnsureFolder(NetworkDataFolder);

            GameObject playerPrefab = CreatePlayerPrefab();
            NetworkPrefabsList prefabList = CreatePrefabList(playerPrefab);
            CreateScene(playerPrefab, prefabList);
            PutSceneFirstInBuild(ScenePath);
            AssetDatabase.SaveAssets();

            EditorUtility.DisplayDialog(
                "Onyx",
                "M0-Testgelände angelegt:\n\n" +
                $"• {ScenePath}\n• {PlayerPrefabPath}\n• {PrefabListPath}\n\n" +
                "Play drücken und im Menü oben links „Hosten“ wählen.",
                "OK");
        }

        internal static GameObject CreatePlayerPrefab()
        {
            var go = new GameObject("Player");
            go.AddComponent<NetworkObject>();

            var networkTransform = go.AddComponent<NetworkTransform>();
            networkTransform.AuthorityMode = NetworkTransform.AuthorityModes.Owner;
            networkTransform.SyncPositionZ = false;
            networkTransform.SyncRotAngleX = false;
            networkTransform.SyncRotAngleY = false;
            networkTransform.SyncRotAngleZ = false;
            networkTransform.SyncScaleX = false;
            networkTransform.SyncScaleY = false;
            networkTransform.SyncScaleZ = false;

            go.AddComponent<SpriteRenderer>();
            go.AddComponent<PlayerVitals>();
            go.AddComponent<PlayerCharacter>();
            go.AddComponent<PlayerVisual>();
            go.AddComponent<PlayerMovement>();
            go.AddComponent<PlayerInteraction>();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, PlayerPrefabPath);
            Object.DestroyImmediate(go);
            return prefab;
        }

        internal static NetworkPrefabsList CreatePrefabList(GameObject playerPrefab)
        {
            var list = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(PrefabListPath);
            if (list == null)
            {
                list = ScriptableObject.CreateInstance<NetworkPrefabsList>();
                AssetDatabase.CreateAsset(list, PrefabListPath);
            }

            if (!list.Contains(playerPrefab))
            {
                list.Add(new NetworkPrefab { Prefab = playerPrefab });
            }

            EditorUtility.SetDirty(list);
            return list;
        }

        private static void CreateScene(GameObject playerPrefab, NetworkPrefabsList prefabList)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateNetworkCameraAndLight(playerPrefab, prefabList);

            // Boden
            var grid = new GameObject("Grid");
            grid.AddComponent<Grid>();
            var ground = new GameObject("Ground");
            ground.transform.SetParent(grid.transform, false);
            ground.AddComponent<Tilemap>();
            var groundRenderer = ground.AddComponent<TilemapRenderer>();
            groundRenderer.sortingOrder = -100;
            ground.AddComponent<PlaceholderGround>();

            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        /// <summary>Netzwerk, Pixel-Perfect-Kamera und globales 2D-Licht – gemeinsam für alle Testszenen.</summary>
        internal static void CreateNetworkCameraAndLight(GameObject playerPrefab, NetworkPrefabsList prefabList)
        {
            // Netzwerk
            var network = new GameObject("NetworkManager");
            var manager = network.AddComponent<NetworkManager>();
            var transport = network.AddComponent<UnityTransport>();
            if (manager.NetworkConfig == null)
            {
                manager.NetworkConfig = new NetworkConfig();
            }

            manager.NetworkConfig.NetworkTransport = transport;
            manager.NetworkConfig.PlayerPrefab = playerPrefab;
            manager.NetworkConfig.ConnectionApproval = true;
            manager.NetworkConfig.Prefabs.NetworkPrefabsLists = new List<NetworkPrefabsList> { prefabList };
            var launcher = network.AddComponent<SessionLauncher>();

            var panel = network.AddComponent<DevSessionPanel>();
            var panelSo = new SerializedObject(panel);
            panelSo.FindProperty("launcher").objectReferenceValue = launcher;
            panelSo.ApplyModifiedPropertiesWithoutUndo();

            // Kamera
            var cameraGo = new GameObject("Main Camera") { tag = "MainCamera" };
            cameraGo.transform.position = new Vector3(0f, 0f, -10f);
            var camera = cameraGo.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = RefHeight / 2f / GameConstants.PixelsPerUnit;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color32(0x07, 0x08, 0x0F, 0xFF);
            cameraGo.AddComponent<UniversalAdditionalCameraData>();

            var pixelPerfect = cameraGo.AddComponent<PixelPerfectCamera>();
            pixelPerfect.assetsPPU = GameConstants.PixelsPerUnit;
            pixelPerfect.refResolutionX = RefWidth;
            pixelPerfect.refResolutionY = RefHeight;
            pixelPerfect.gridSnapping = PixelPerfectCamera.GridSnapping.UpscaleRenderTexture;
            pixelPerfect.cropFrame = PixelPerfectCamera.CropFrame.None;
            cameraGo.AddComponent<CameraFollow>();

            // Licht: Helligkeit folgt der Tageszeit (WorldLighting).
            var lightGo = new GameObject("Global Light 2D");
            var globalLight = lightGo.AddComponent<Light2D>();
            globalLight.lightType = Light2D.LightType.Global;
            globalLight.intensity = 1f;
            globalLight.targetSortingLayers = SortingLayer.layers.Select(layer => layer.id).ToArray();
        }

        internal static void PutSceneFirstInBuild(string path)
        {
            var scenes = EditorBuildSettings.scenes.Where(s => s.path != path).ToList();
            scenes.Insert(0, new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        internal static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            int slash = path.LastIndexOf('/');
            string parent = path.Substring(0, slash);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(slash + 1));
        }
    }
}
