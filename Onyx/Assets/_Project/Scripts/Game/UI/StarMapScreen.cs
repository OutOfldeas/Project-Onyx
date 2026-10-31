using System.Collections.Generic;
using System.Linq;
using Onyx.Core.Galaxy;
using Onyx.Core.Session;
using Onyx.Game.DevTools;
using Onyx.Game.Session;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Onyx.Game.UI
{
    /// <summary>
    /// Die Sternenkarte (Taste M): Pixelkarte im Stil der Art Direction, gezeichnet von
    /// <see cref="StarMapRasterizer"/> in 480 × 270 und ganzzahlig hochskaliert.
    /// Klick wählt ein System; von dort kann man springen oder eine Route planen.
    /// </summary>
    /// <remarks>
    /// Vorerst wird der lokale Spielstand verändert. Mit M2 (geteilte Welt) geht der Sprung
    /// als Anfrage an den Host, die Karte selbst bleibt gleich.
    /// </remarks>
    public sealed class StarMapScreen : MonoBehaviour
    {
        /// <summary>true, solange die Karte offen ist – die Figur bleibt dann stehen.</summary>
        public static bool IsOpen { get; private set; }

        private const int W = StarMapRasterizer.Width;
        private const int H = StarMapRasterizer.Height;
        private const int PanelX = 304;
        private const int PanelY = 26;
        private const int PanelW = 168;
        private const int Line = 10;

        private Texture2D mapTexture;
        private Texture2D white;
        private StarMapImage image;
        private int imageKey = int.MinValue;

        private WorldState world;
        private int selected = -1;
        private int routeTarget = -1;
        private List<int> route;
        private string message;
        private float messageUntil;

        private Font font;
        private GUIStyle textStyle;
        private int styleScale;

        // Bildschirmabbildung, im OnGUI pro Frame neu berechnet
        private int scale = 1;
        private float originX;
        private float originY;

        private void Awake()
        {
            font = Resources.Load<Font>("Fonts/Silkscreen-Regular");
            white = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
            white.SetPixel(0, 0, Color.white);
            white.Apply();
        }

        private void OnDisable()
        {
            IsOpen = false;
        }

        private void OnDestroy()
        {
            if (mapTexture != null) Destroy(mapTexture);
            if (white != null) Destroy(white);
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            // Nicht reagieren, während in der Dev-Konsole getippt wird.
            bool typing = DevMode.BlocksGameplayInput;
            if (!typing && keyboard.mKey.wasPressedThisFrame)
            {
                IsOpen = !IsOpen;
            }
            else if (IsOpen && keyboard.escapeKey.wasPressedThisFrame)
            {
                IsOpen = false;
            }

            SyncWorld();
            if (IsOpen)
            {
                FollowRoute();
            }
        }

        private void SyncWorld()
        {
            var current = GameSession.Instance != null ? GameSession.Instance.World : null;
            if (current != world)
            {
                world = current;
                selected = -1;
                ClearRoute();
                imageKey = int.MinValue;
            }
        }

        // ------------------------------------------------------------------ Route

        private ShipNavigation Nav => world.Navigation;

        private float PlanningRange => Nav.JumpRange > 0f ? Nav.JumpRange : float.MaxValue;

        private void PlanRoute(int target)
        {
            var found = RoutePlanner.FindRoute(world.Galaxy, Nav.CurrentSystemId, target, PlanningRange);
            if (found == null)
            {
                Flash("Kein Weg mit dieser Sprungreichweite");
                return;
            }

            routeTarget = target;
            route = found.Systems.ToList();
        }

        private void ClearRoute()
        {
            route = null;
            routeTarget = -1;
        }

        /// <summary>Kürzt die Route, wenn das Schiff weitergesprungen ist (oder plant neu, wenn es abgewichen ist).</summary>
        private void FollowRoute()
        {
            if (route == null)
            {
                return;
            }

            int here = Nav.CurrentSystemId;
            if (here == routeTarget)
            {
                ClearRoute();
                return;
            }

            int index = route.IndexOf(here);
            if (index > 0)
            {
                route.RemoveRange(0, index);
            }
            else if (index < 0)
            {
                PlanRoute(routeTarget);
            }
        }

        private void Jump(int target)
        {
            int from = Nav.CurrentSystemId;
            JumpResult result = Nav.Jump(target);
            if (result == JumpResult.Ok)
            {
                Flash($"Gesprungen: {world.Galaxy.GetSystem(from).Name} nach {world.Galaxy.GetSystem(target).Name}");
                FollowRoute();
            }
            else
            {
                Flash(GalaxyText.JumpProblem(result));
            }
        }

        /// <summary>Nächster Halt: das Ziel selbst, wenn direkt verbunden, sonst der nächste Routenpunkt.</summary>
        private int NextHopTowards(int target)
        {
            if (world.Galaxy.TryGetLane(Nav.CurrentSystemId, target, out _))
            {
                return target;
            }

            return route != null && routeTarget == target && route.Count > 1 ? route[1] : -1;
        }

        private void Flash(string text)
        {
            message = text;
            messageUntil = Time.unscaledTime + 3f;
        }

        // ------------------------------------------------------------------ Zeichnen

        private void OnGUI()
        {
            if (!IsOpen || world == null)
            {
                return;
            }

            scale = Mathf.Max(1, Mathf.FloorToInt(Mathf.Min(Screen.width / (float)W, Screen.height / (float)H)));
            originX = Mathf.Floor((Screen.width - W * scale) / 2f);
            originY = Mathf.Floor((Screen.height - H * scale) / 2f);
            EnsureStyle();
            RefreshImage();

            // Ränder außerhalb der Karte abdunkeln, dann die Karte selbst
            DrawScreenRect(new Rect(0, 0, Screen.width, Screen.height), Palette(MapColor.Void0));
            GUI.DrawTexture(new Rect(originX, originY, W * scale, H * scale), mapTexture);

            DrawFactionNames();
            DrawSystemNames();
            DrawHeader();
            DrawLegend();
            bool panelUsedClick = selected >= 0 && DrawPanel();
            DrawFooter();

            if (!panelUsedClick)
            {
                HandleMapClick();
            }
        }

        private void RefreshImage()
        {
            var view = new StarMapView
            {
                CurrentSystemId = Nav.CurrentSystemId,
                SelectedSystemId = selected,
                Route = route,
                JumpRange = Nav.JumpRange,
            };

            int key = ViewKey(view);
            if (key == imageKey && mapTexture != null)
            {
                return;
            }

            imageKey = key;
            image = StarMapRasterizer.Render(world.Galaxy, view);
            if (mapTexture == null)
            {
                mapTexture = new Texture2D(W, H, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp,
                    hideFlags = HideFlags.HideAndDontSave,
                };
            }

            var colors = new Color32[W * H];
            for (int y = 0; y < H; y++)
            {
                int target = (H - 1 - y) * W; // Texturen beginnen unten, das Bild oben
                for (int x = 0; x < W; x++)
                {
                    colors[target + x] = Palette(image.Get(x, y));
                }
            }

            mapTexture.SetPixels32(colors);
            mapTexture.Apply(false);
        }

        private int ViewKey(StarMapView view)
        {
            unchecked
            {
                int key = world.Seed;
                key = key * 31 + view.CurrentSystemId;
                key = key * 31 + view.SelectedSystemId;
                key = key * 31 + view.JumpRange.GetHashCode();
                foreach (var system in world.Galaxy.Systems)
                {
                    key = key * 3 + (system.Discovered ? 1 : 0);
                }

                if (route != null)
                {
                    foreach (int id in route) key = key * 31 + id;
                }

                return key;
            }
        }

        private void DrawHeader()
        {
            Text(8, 6, "Ebene 3 · Galaxie / Sternenkarte", MapColor.Amber);
            Text(8, 16, $"Standort: {Nav.CurrentSystem.Name}", MapColor.Steel3);

            string fuel = $"Treibstoff {Nav.Fuel.Current:0}/{Nav.Fuel.Max:0}";
            Text(W - 8, 6, fuel, MapColor.Bone, TextAnchor.UpperRight);
            if (Nav.JumpRange > 0f)
            {
                Text(W - 8, 16, $"Sprungreichweite {Nav.JumpRange:0.0}", MapColor.Steel3, TextAnchor.UpperRight);
            }
            else
            {
                Text(W - 8, 16, "Antrieb ausgefallen", MapColor.Red, TextAnchor.UpperRight);
            }
        }

        private void DrawFactionNames()
        {
            foreach (var pair in image.FactionLabels)
            {
                var faction = world.Galaxy.GetFaction(pair.Key);
                if (faction == null) continue;
                var (_, border) = MapPalette.Faction(pair.Key);
                Text(pair.Value.x, pair.Value.y + 10, faction.Name.ToUpperInvariant(), border, TextAnchor.UpperCenter);
            }
        }

        private void DrawSystemNames()
        {
            foreach (var pair in image.SystemPositions)
            {
                var (x, y) = pair.Value;
                if (image.DiscoveredVisible.Contains(pair.Key))
                {
                    MapColor color = pair.Key == Nav.CurrentSystemId ? MapColor.Amber : pair.Key == selected ? MapColor.Bone : MapColor.Steel3;
                    Text(x + 6, y - 4, world.Galaxy.GetSystem(pair.Key).Name, color);
                }
                else
                {
                    Text(x + 5, y - 4, "?", MapColor.Steel1);
                }
            }
        }

        private void DrawLegend()
        {
            int y = H - 8 - 7 * 8;
            foreach (var faction in world.Galaxy.Factions)
            {
                var (_, border) = MapPalette.Faction(faction.Id);
                FillImageRect(8, y + 1, 5, 5, border);
                Text(17, y, faction.Name, MapColor.Steel3);
                y += 8;
            }

            FillImageRect(8, y + 3, 6, 1, MapColor.Amber);
            Text(17, y, "Reiseroute", MapColor.Steel3);
            y += 8;
            for (int i = 0; i < 6; i += 4) FillImageRect(8 + i, y + 3, 2, 1, MapColor.Steel2);
            Text(17, y, "In Reichweite", MapColor.Steel3);
            y += 8;
            for (int i = 0; i < 6; i += 4) FillImageRect(8 + i, y + 3, 2, 1, MapColor.Red);
            Text(17, y, "Außer Reichweite", MapColor.Steel3);
            y += 8;
            FrameImageRect(8, y + 1, 5, 5, MapColor.Steel2);
            Text(17, y, "Unerforscht", MapColor.Steel3);
        }

        private void DrawFooter()
        {
            if (message != null && Time.unscaledTime < messageUntil)
            {
                Text(W / 2, H - 22, message, MapColor.Amber, TextAnchor.UpperCenter);
            }

            Text(W - 8, H - 12, "Klick: System wählen   M / Esc: schließen", MapColor.Steel2, TextAnchor.UpperRight);
        }

        /// <returns>true, wenn der Klick dieses Frames im Panel gelandet ist.</returns>
        private bool DrawPanel()
        {
            var system = world.Galaxy.GetSystem(selected);
            bool discovered = system.Discovered;
            int lines = 4 + (discovered ? system.Planets.Count + 1 : 1) + 4;
            int height = lines * Line + 8;

            FillImageRect(PanelX, PanelY, PanelW, height, MapColor.Void1, 0.94f);
            FrameImageRect(PanelX, PanelY, PanelW, height, MapColor.Amber);
            FillImageRect(PanelX, PanelY, PanelW, 1, MapColor.Amber);

            int x = PanelX + 6;
            int y = PanelY + 5;
            Text(x, y, discovered ? system.Name : "Unbekanntes System", MapColor.Bone);
            y += Line;

            if (system.FactionId >= 0)
            {
                Text(x, y, world.Galaxy.GetFaction(system.FactionId).Name, MapPalette.Faction(system.FactionId).border);
            }
            else
            {
                Text(x, y, "Unbeansprucht", MapColor.Sand);
            }

            y += Line;
            Text(x, y, discovered ? GalaxyText.Star(system.Star) : "Daten fehlen", MapColor.Steel2);
            y += Line + 4;

            if (discovered)
            {
                Text(x, y, $"Planeten ({system.Planets.Count})", MapColor.Amber);
                y += Line;
                foreach (var planet in system.Planets)
                {
                    Text(x + 4, y, planet.Name, MapColor.Steel3);
                    Text(PanelX + PanelW - 6, y, $"{GalaxyText.Planet(planet.Type)} {new string('!', planet.Hazard)}",
                        planet.Hazard >= 2 ? MapColor.Red : MapColor.Steel2, TextAnchor.UpperRight);
                    y += Line;
                }
            }
            else
            {
                Text(x, y, "Erst nach einem Sprung bekannt", MapColor.Steel2);
                y += Line;
            }

            y += 4;
            bool clicked = false;
            if (selected == Nav.CurrentSystemId)
            {
                Text(x, y, "Ihr seid hier.", MapColor.Amber);
                y += Line;
            }
            else
            {
                if (routeTarget == selected && route != null)
                {
                    Text(x, y, $"Route: {route.Count - 1} Sprünge", MapColor.Steel3);
                }
                else if (world.Galaxy.TryGetLane(Nav.CurrentSystemId, selected, out var lane))
                {
                    Text(x, y, $"Direkt: {Nav.FuelCost(lane):0} Treibstoff", MapColor.Steel3);
                }

                y += Line + 2;

                int hop = NextHopTowards(selected);
                JumpResult check = hop >= 0 ? Nav.CanJump(hop) : JumpResult.NoLane;
                string jumpLabel = hop == selected || hop < 0 ? "Springen" : "Nächster Sprung";
                clicked |= Button(x, y, 74, jumpLabel, check == JumpResult.Ok, () => Jump(hop));
                clicked |= Button(x + 80, y, 76, routeTarget == selected ? "Route löschen" : "Route planen", true,
                    () => { if (routeTarget == selected) ClearRoute(); else PlanRoute(selected); });
                y += 14;

                string problem = check == JumpResult.NoLane && hop < 0 ? "Erst Route planen" : GalaxyText.JumpProblem(check);
                if (problem != null)
                {
                    Text(x, y, problem, MapColor.Red);
                }
            }

            var e = Event.current;
            if (e.type == EventType.MouseDown && ImageRect(PanelX, PanelY, PanelW, height).Contains(e.mousePosition))
            {
                e.Use();
                clicked = true;
            }

            return clicked;
        }

        private bool Button(int x, int y, int width, string label, bool enabled, System.Action action)
        {
            const int Height = 12;
            Rect rect = ImageRect(x, y, width, Height);
            var e = Event.current;
            bool hover = enabled && rect.Contains(e.mousePosition);
            MapColor frame = enabled ? MapColor.Amber : MapColor.Steel1;

            FillImageRect(x, y, width, Height, hover ? MapColor.Amber : MapColor.Void0, hover ? 1f : 0.8f);
            FrameImageRect(x, y, width, Height, frame);
            Text(x + width / 2, y + 2, label, hover ? MapColor.Void0 : enabled ? MapColor.Amber : MapColor.Steel1, TextAnchor.UpperCenter);

            if (hover && e.type == EventType.MouseDown && e.button == 0)
            {
                e.Use();
                action();
                return true;
            }

            return false;
        }

        private void HandleMapClick()
        {
            var e = Event.current;
            if (e.type != EventType.MouseDown || e.button != 0)
            {
                return;
            }

            float px = (e.mousePosition.x - originX) / scale;
            float py = (e.mousePosition.y - originY) / scale;
            int best = -1;
            float bestDistance = 8f * 8f;
            foreach (var pair in image.SystemPositions)
            {
                float dx = pair.Value.x - px;
                float dy = pair.Value.y - py;
                float d = dx * dx + dy * dy;
                if (d < bestDistance)
                {
                    bestDistance = d;
                    best = pair.Key;
                }
            }

            selected = best;
            e.Use();
        }

        // ------------------------------------------------------------------ Hilfen

        private void EnsureStyle()
        {
            if (textStyle != null && styleScale == scale)
            {
                return;
            }

            styleScale = scale;
            textStyle = new GUIStyle(GUI.skin.label)
            {
                font = font,
                fontSize = 8 * scale,
                padding = new RectOffset(0, 0, 0, 0),
                margin = new RectOffset(0, 0, 0, 0),
                wordWrap = false,
                richText = false,
                clipping = TextClipping.Overflow,
                alignment = TextAnchor.UpperLeft,
            };
        }

        private void Text(int x, int y, string text, MapColor color, TextAnchor anchor = TextAnchor.UpperLeft)
        {
            var content = new GUIContent(text);
            Vector2 size = textStyle.CalcSize(content);
            float left = originX + x * scale;
            if (anchor == TextAnchor.UpperRight) left -= size.x;
            else if (anchor == TextAnchor.UpperCenter) left -= Mathf.Round(size.x / 2f);
            float top = originY + y * scale;

            // Harter Pixelschatten für Lesbarkeit auf dem Nebel
            textStyle.normal.textColor = Palette(MapColor.Void0);
            GUI.Label(new Rect(left + scale, top + scale, size.x, size.y), content, textStyle);
            textStyle.normal.textColor = Palette(color);
            GUI.Label(new Rect(left, top, size.x, size.y), content, textStyle);
        }

        private Rect ImageRect(int x, int y, int w, int h) => new Rect(originX + x * scale, originY + y * scale, w * scale, h * scale);

        private void FillImageRect(int x, int y, int w, int h, MapColor color, float alpha = 1f)
        {
            Color c = Palette(color);
            c.a = alpha;
            DrawScreenRect(ImageRect(x, y, w, h), c);
        }

        private void FrameImageRect(int x, int y, int w, int h, MapColor color)
        {
            FillImageRect(x, y, w, 1, color);
            FillImageRect(x, y + h - 1, w, 1, color);
            FillImageRect(x, y, 1, h, color);
            FillImageRect(x + w - 1, y, 1, h, color);
        }

        private void DrawScreenRect(Rect rect, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, white);
            GUI.color = previous;
        }

        private static Color32 Palette(MapColor color)
        {
            uint rgb = MapPalette.Rgb[(int)color];
            return new Color32((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 255);
        }
    }
}
