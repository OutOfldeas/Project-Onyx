using System.Collections.Generic;
using Onyx.Core;
using Onyx.Core.Art;
using Onyx.Core.CrashSite;
using Onyx.Game.Session;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Onyx.Game.CrashSite
{
    /// <summary>
    /// Baut die Absturzstelle aus dem Spielstand sichtbar auf: Bodenbild, Konsolen der
    /// Reparaturstellen und Lichter (Kapsel, Warnlampen, Notbeleuchtung des Wracks).
    /// Rein lokal – jeder Rechner erzeugt dasselbe Bild aus demselben Seed.
    /// </summary>
    public sealed class CrashSiteView : MonoBehaviour
    {
        private static readonly Color PodLightColor = new Color32(0xFF, 0xB5, 0x2E, 0xFF);
        private static readonly Color AlarmColor = new Color32(0xFF, 0x3D, 0x5A, 0xFF);
        private static readonly Color RunningColor = new Color32(0x39, 0xD5, 0xE6, 0xFF);

        private Transform root;
        private CrashSiteState site;
        private readonly List<StationVisual> stations = new List<StationVisual>();
        private readonly List<Light2D> wreckLights = new List<Light2D>();

        private sealed class StationVisual
        {
            public RepairPoint Point;
            public SpriteRenderer Renderer;
            public Light2D Light;
            public bool ShownRepaired;
        }

        private void OnEnable()
        {
            if (GameSession.Instance != null)
            {
                GameSession.Instance.WorldReplaced += Rebuild;
            }
        }

        private void OnDisable()
        {
            if (GameSession.Instance != null)
            {
                GameSession.Instance.WorldReplaced -= Rebuild;
            }
        }

        private void Start()
        {
            Rebuild();
        }

        public void Rebuild()
        {
            if (root != null)
            {
                Destroy(root.gameObject);
            }

            stations.Clear();
            wreckLights.Clear();
            if (GameSession.Instance == null)
            {
                return;
            }

            site = GameSession.Instance.World.CrashSite;
            var layout = site.Layout;

            root = new GameObject("Absturzstelle (erzeugt)").transform;
            root.SetParent(transform, false);

            BuildGround(layout);
            BuildPod(layout);
            foreach (var point in layout.RepairPoints)
            {
                BuildStation(layout, point);
            }

            BuildWreckLights(layout);
        }

        private Texture2D groundTexture;

        /// <summary>Boden, Felsen und Wände als ein Pixelbild (siehe <see cref="CrashSiteArt"/>).</summary>
        private void BuildGround(CrashSiteLayout layout)
        {
            PixelImage image = CrashSiteArt.Render(layout);
            if (groundTexture != null)
            {
                Destroy(groundTexture);
            }

            groundTexture = new Texture2D(image.Width, image.Height, TextureFormat.RGBA32, false)
            {
                name = "Absturzstelle",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave,
            };

            var colors = new Color32[image.Pixels.Length];
            for (int i = 0; i < colors.Length; i++)
            {
                uint c = image.Pixels[i];
                colors[i] = new Color32((byte)(c >> 16), (byte)(c >> 8), (byte)c, (byte)(c >> 24));
            }

            groundTexture.SetPixels32(colors);
            groundTexture.Apply(false, true);

            var go = new GameObject("Boden");
            go.transform.SetParent(root, false);
            go.transform.position = new Vector3(-layout.OriginX, -layout.OriginY, 0f);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = Sprite.Create(groundTexture, new Rect(0, 0, image.Width, image.Height), Vector2.zero, GameConstants.PixelsPerUnit);
            renderer.sortingOrder = -100;
        }

        private void OnDestroy()
        {
            if (groundTexture != null)
            {
                Destroy(groundTexture);
            }
        }

        private void BuildPod(CrashSiteLayout layout)
        {
            var pod = layout.PodInterior;
            var (cx, cy) = (pod.CenterX - layout.OriginX, pod.CenterY - layout.OriginY);
            CreateLight("Kapsellicht", new Vector3(cx, cy, 0f), PodLightColor, 4.5f, 0.9f);
        }

        private void BuildStation(CrashSiteLayout layout, RepairPoint point)
        {
            var (x, y) = layout.TileCenter(point.TileX, point.TileY);
            var go = new GameObject(point.Name);
            go.transform.SetParent(root, false);
            go.transform.position = new Vector3(x, y - 0.5f, 0f);

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.spriteSortPoint = SpriteSortPoint.Pivot;
            bool repaired = site.Job(point.Index).IsComplete;
            renderer.sprite = SiteTileArt.Station(point.Kind, repaired);

            var light = CreateLight("Warnlampe", new Vector3(x, y + 0.3f, 0f), repaired ? RunningColor : AlarmColor, 2.5f, 1f);
            light.transform.SetParent(go.transform, true);
            stations.Add(new StationVisual { Point = point, Renderer = renderer, Light = light, ShownRepaired = repaired });
        }

        private void BuildWreckLights(CrashSiteLayout layout)
        {
            var r = layout.WreckInterior;
            int rooms = 3;
            float width = (r.MaxX - r.MinX + 1f) / rooms;
            for (int i = 0; i < rooms; i++)
            {
                float x = r.MinX + width * (i + 0.5f) - layout.OriginX;
                float y = r.CenterY - layout.OriginY;
                var light = CreateLight($"Notbeleuchtung {i + 1}", new Vector3(x, y, 0f), RunningColor, 7f, 0.7f);
                light.enabled = false;
                wreckLights.Add(light);
            }
        }

        private Light2D CreateLight(string name, Vector3 position, Color color, float radius, float intensity)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            go.transform.position = position;
            var light = go.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.pointLightInnerRadius = radius * 0.25f;
            light.pointLightOuterRadius = radius;
            light.falloffIntensity = 0.6f;
            return light;
        }

        private void Update()
        {
            if (site == null || GameSession.Instance == null)
            {
                return;
            }

            if (site != GameSession.Instance.World.CrashSite)
            {
                Rebuild();
                return;
            }

            float blink = Mathf.PingPong(Time.time * 2f, 1f) > 0.5f ? 1f : 0.25f;
            foreach (var station in stations)
            {
                bool repaired = site.Job(station.Point.Index).IsComplete;
                if (repaired != station.ShownRepaired)
                {
                    station.ShownRepaired = repaired;
                    station.Renderer.sprite = SiteTileArt.Station(station.Point.Kind, repaired);
                    station.Light.color = repaired ? RunningColor : AlarmColor;
                }

                station.Light.intensity = repaired ? 0.9f : blink;
            }

            bool power = site.IsRepaired(StationKind.Reactor);
            foreach (var light in wreckLights)
            {
                light.enabled = power;
            }
        }
    }
}
