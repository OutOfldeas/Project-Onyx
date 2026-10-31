using System.Collections.Generic;
using System.Linq;
using Onyx.Core.DevTools;
using Onyx.Core.Galaxy;
using Onyx.Core.Session;
using Onyx.Core.Ships;
using Onyx.Core.Skills;
using Onyx.Game.Player;
using Onyx.Game.Session;
using Onyx.Game.World;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Onyx.Game.DevTools
{
    /// <summary>
    /// Entwickler-Modus (F1): Konsole, Sternenkarte, Skills, Schiffswerft und Welt-Einstellungen,
    /// um alle Systeme zu testen, bevor es die richtige Oberfläche gibt. Nur im Editor und in
    /// Entwickler-Builds aktiv (siehe GameBootstrap).
    /// </summary>
    public sealed class DevMode : MonoBehaviour
    {
        /// <summary>true, solange in der Konsole getippt wird – die Figur soll dann stehen bleiben.</summary>
        public static bool BlocksGameplayInput { get; private set; }

        private const int WindowId = 0x0A1;
        private const int Cell = 16;
        private const int MaxLogLines = 300;

        private static readonly string[] TabNames = { "Konsole", "Galaxie", "Skills", "Werft", "Welt" };
        private static readonly string[] ToolNames = { "Rumpf +", "Rumpf −", "Modul setzen", "Modul entfernen" };
        private static readonly string[] LockNames = { "Steigern", "Senken", "Sperren" };

        private enum Tool
        {
            HullAdd,
            HullRemove,
            PlaceModule,
            RemoveModule,
        }

        private readonly DevCommandRegistry registry = new DevCommandRegistry();
        private readonly List<string> log = new List<string>();

        private bool visible;
        private int tab;
        private Rect windowRect = new Rect(300, 40, 1000, 640);
        private string input = string.Empty;
        private Vector2 logScroll;
        private Vector2 skillScroll;
        private Vector2 infoScroll;
        private bool scrollLogToEnd;

        private int selectedSystem = -1;
        private Route plannedRoute;
        private string galaxyStatus = string.Empty;

        private Tool tool = Tool.HullAdd;
        private int moduleIndex;
        private bool rotateModule;
        private string shipyardStatus = string.Empty;

        private GUIStyle smallLabel;
        private GUIStyle logStyle;

        private static GameSession CurrentSession => GameSession.Instance;

        private void Awake()
        {
            CoreDevCommands.Register(registry, () => CurrentSession.World, w => CurrentSession.ReplaceWorld(w));
            RegisterGameCommands();
            Log("Onyx Dev-Modus. „help“ zeigt alle Befehle.");
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.f1Key.wasPressedThisFrame)
            {
                visible = !visible;
            }

            BlocksGameplayInput = visible && tab == 0;
        }

        private void OnDisable()
        {
            BlocksGameplayInput = false;
        }

        private void OnGUI()
        {
            if (!visible || CurrentSession == null)
            {
                return;
            }

            EnsureStyles();
            float scale = Mathf.Max(1f, Screen.height / 1080f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            windowRect = GUI.Window(WindowId, windowRect, DrawWindow, "Onyx Dev-Modus   (F1 schließt)");
        }

        private void DrawWindow(int id)
        {
            tab = GUILayout.Toolbar(tab, TabNames);
            GUILayout.Space(6);

            switch (tab)
            {
                case 0: DrawConsole(); break;
                case 1: DrawGalaxy(); break;
                case 2: DrawSkills(); break;
                case 3: DrawShipyard(); break;
                default: DrawWorld(); break;
            }

            GUI.DragWindow(new Rect(0, 0, 10000, 20));
        }

        // ------------------------------------------------------------------ Konsole

        private void DrawConsole()
        {
            logScroll = GUILayout.BeginScrollView(logScroll, GUILayout.ExpandHeight(true));
            GUILayout.Label(string.Join("\n", log), logStyle);
            GUILayout.EndScrollView();
            if (scrollLogToEnd && Event.current.type == EventType.Repaint)
            {
                logScroll.y = float.MaxValue;
                scrollLogToEnd = false;
            }

            Event e = Event.current;
            bool enterPressed = e.type == EventType.KeyDown
                && (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
                && GUI.GetNameOfFocusedControl() == "devinput";

            GUILayout.BeginHorizontal();
            GUI.SetNextControlName("devinput");
            input = GUILayout.TextField(input, GUILayout.ExpandWidth(true));
            bool clicked = GUILayout.Button("Ausführen", GUILayout.Width(100));
            GUILayout.EndHorizontal();

            if (enterPressed || clicked)
            {
                RunCommand(input);
                input = string.Empty;
                GUI.FocusControl("devinput");
                if (enterPressed)
                {
                    e.Use();
                }
            }
        }

        private void RunCommand(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                return;
            }

            Log("> " + line);
            Log(registry.Execute(line));
        }

        private void Log(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            log.AddRange(text.Split('\n'));
            if (log.Count > MaxLogLines)
            {
                log.RemoveRange(0, log.Count - MaxLogLines);
            }

            scrollLogToEnd = true;
        }

        private void RegisterGameCommands()
        {
            registry.Register("tp", "tp <x> <y>", "Teleportiert die eigene Figur (Kachel-Koordinaten).", args =>
            {
                Transform player = LocalPlayer();
                if (player == null)
                {
                    return "Keine eigene Figur – erst hosten oder beitreten.";
                }

                player.position = new Vector3(DevCommandRegistry.Float(args, 0, "x"), DevCommandRegistry.Float(args, 1, "y"), 0f);
                return $"Figur auf {player.position.x:0.0}, {player.position.y:0.0}.";
            });

            registry.Register("speed", "speed <kacheln/s>", "Laufgeschwindigkeit der eigenen Figur.", args =>
            {
                Transform player = LocalPlayer();
                var movement = player != null ? player.GetComponent<PlayerMovement>() : null;
                if (movement == null)
                {
                    return "Keine eigene Figur – erst hosten oder beitreten.";
                }

                movement.Speed = DevCommandRegistry.Float(args, 0, "geschwindigkeit");
                return $"Geschwindigkeit {movement.Speed:0.0}.";
            });

            registry.Register("timescale", "timescale <faktor>", "Zeitraffer für die Simulation (1 = Echtzeit, 0 = Pause).", args =>
            {
                CurrentSession.TimeScale = Mathf.Max(0f, DevCommandRegistry.Float(args, 0, "faktor"));
                return $"Zeitraffer ×{CurrentSession.TimeScale:0.##}.";
            });

            registry.Register("heal", "heal", "Füllt Sauerstoff, Schild und Leben der eigenen Figur auf (Host).", _ =>
                WithHostVitals(v => { v.ServerHeal(); return "Geheilt."; }));

            registry.Register("down", "down", "Macht die eigene Figur kampfunfähig, um Wiederbeleben zu testen (Host).", _ =>
                WithHostVitals(v => { v.ServerKnockDown(); return "Kampfunfähig."; }));

            registry.Register("o2", "o2 <prozent>", "Setzt den eigenen Sauerstoff (Host).", args =>
            {
                float percent = DevCommandRegistry.Float(args, 0, "prozent");
                return WithHostVitals(v => { v.ServerSetOxygen(percent / 100f); return $"Sauerstoff {percent:0} %."; });
            });

            registry.Register("god", "god", "Schaltet Unverwundbarkeit der eigenen Figur um (Host).", _ =>
                WithHostVitals(v => { v.GodMode = !v.GodMode; return v.GodMode ? "Unverwundbar." : "Wieder verwundbar."; }));

            registry.Register("noclip", "noclip", "Durch Wände laufen ein/aus.", _ =>
            {
                PlayerMovement.NoClip = !PlayerMovement.NoClip;
                return PlayerMovement.NoClip ? "Noclip an." : "Noclip aus.";
            });

            registry.Register("save", "save", "Speichert die Welt sofort.", _ =>
            {
                var saves = SaveSystem.Instance;
                if (saves == null) return "Kein Speichersystem.";
                if (!SaveSystem.MaySave) return "Nur der Host speichert.";
                saves.Save();
                return $"{saves.LastMessage}\n{SaveSystem.FilePath}";
            });

            registry.Register("load", "load", "Lädt den gespeicherten Spielstand.", _ =>
            {
                var saves = SaveSystem.Instance;
                if (saves == null) return "Kein Speichersystem.";
                if (!SaveSystem.MaySave) return "Nur der Host kann laden.";
                saves.Load();
                return saves.LastMessage;
            });

            registry.Register("wipe", "wipe [seed]", "Löscht den Spielstand und beginnt eine neue Welt.", args =>
            {
                var saves = SaveSystem.Instance;
                if (saves == null) return "Kein Speichersystem.";
                if (!SaveSystem.MaySave) return "Nur der Host kann das.";
                saves.Delete();
                int seed = args.Length > 0 ? DevCommandRegistry.Int(args, 0, "seed") : GameSession.DefaultSeed;
                CurrentSession.ReplaceWorld(WorldState.CreateNew(seed));
                return $"Spielstand gelöscht, neue Welt mit Seed {seed}.";
            });

            registry.Register("volume", "volume <0-100>", "Gesamtlautstärke (wird gespeichert).", args =>
            {
                Onyx.Game.Audio.GameAudio.Volume = DevCommandRegistry.Float(args, 0, "prozent") / 100f;
                return $"Lautstärke {Onyx.Game.Audio.GameAudio.Volume * 100f:0} %.";
            });

            registry.Register("sound", "sound <name>", "Spielt ein Geräusch ab (StepSand, StepMetal, Ratchet, RepairDone, LevelUp, OxygenAlarm, Downed, Revived).", args =>
            {
                string name = DevCommandRegistry.Text(args, 0, "name");
                if (!System.Enum.TryParse(name, true, out Onyx.Core.Audio.Sfx sound)) return $"Unbekanntes Geräusch „{name}“.";
                Onyx.Game.Audio.GameAudio.Play(sound);
                return $"Spiele {sound}.";
            });

            registry.Register("clear", "clear", "Leert die Konsole.", _ =>
            {
                log.Clear();
                return string.Empty;
            });
        }

        private static string WithHostVitals(System.Func<PlayerVitals, string> action)
        {
            var vitals = PlayerVitals.Local;
            if (vitals == null)
            {
                return "Keine eigene Figur – erst hosten oder beitreten.";
            }

            return vitals.IsServer ? action(vitals) : "Nur beim Host möglich.";
        }

        private static Transform LocalPlayer()
        {
            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null || !manager.IsClient || manager.LocalClient == null || manager.LocalClient.PlayerObject == null)
            {
                return null;
            }

            return manager.LocalClient.PlayerObject.transform;
        }

        // ------------------------------------------------------------------ Galaxie

        private void DrawGalaxy()
        {
            WorldState world = CurrentSession.World;
            GalaxyMap map = world.Galaxy;
            ShipNavigation nav = world.Navigation;

            GUILayout.BeginHorizontal();

            Rect area = GUILayoutUtility.GetRect(640, 540, GUILayout.Width(640), GUILayout.Height(540));
            GUI.Box(area, GUIContent.none);

            float maxX = Mathf.Max(1f, map.Systems.Max(s => s.X));
            float maxY = Mathf.Max(1f, map.Systems.Max(s => s.Y));
            float scale = Mathf.Min((area.width - 60f) / maxX, (area.height - 40f) / maxY);
            Vector2 ToScreen(StarSystem s) => new Vector2(area.x + 20f + s.X * scale, area.yMax - 20f - s.Y * scale);

            var routeLanes = new HashSet<(int, int)>();
            if (plannedRoute != null)
            {
                for (int i = 0; i + 1 < plannedRoute.Systems.Count; i++)
                {
                    int a = plannedRoute.Systems[i];
                    int b = plannedRoute.Systems[i + 1];
                    routeLanes.Add((Mathf.Min(a, b), Mathf.Max(a, b)));
                }
            }

            if (Event.current.type == EventType.Repaint)
            {
                foreach (Lane lane in map.Lanes)
                {
                    bool onRoute = routeLanes.Contains((lane.A, lane.B));
                    bool reachable = lane.Distance <= nav.JumpRange;
                    Color color = onRoute ? new Color(1f, 0.71f, 0.18f) : reachable ? new Color(0.55f, 0.6f, 0.75f, 0.8f) : new Color(0.4f, 0.25f, 0.3f, 0.6f);
                    DrawDots(ToScreen(map.GetSystem(lane.A)), ToScreen(map.GetSystem(lane.B)), color, onRoute ? 3f : 2f);
                }
            }

            foreach (StarSystem system in map.Systems)
            {
                Vector2 p = ToScreen(system);
                bool current = system.Id == nav.CurrentSystemId;
                bool selected = system.Id == selectedSystem;

                if (current || selected)
                {
                    DrawRect(new Rect(p.x - 9, p.y - 9, 18, 18), current ? new Color(1f, 0.71f, 0.18f) : Color.white);
                }

                DrawRect(new Rect(p.x - 6, p.y - 6, 12, 12), SystemColor(system));
                if (GUI.Button(new Rect(p.x - 7, p.y - 7, 14, 14), GUIContent.none, GUIStyle.none))
                {
                    selectedSystem = system.Id;
                    plannedRoute = null;
                    galaxyStatus = string.Empty;
                }

                string label = system.Discovered ? system.Name : "?";
                GUI.Label(new Rect(p.x + 8, p.y - 9, 120, 18), label, smallLabel);
            }

            GUILayout.BeginVertical();
            infoScroll = GUILayout.BeginScrollView(infoScroll);
            DrawGalaxyInfo(world);
            GUILayout.EndScrollView();
            GUILayout.EndVertical();

            GUILayout.EndHorizontal();
        }

        private void DrawGalaxyInfo(WorldState world)
        {
            GalaxyMap map = world.Galaxy;
            ShipNavigation nav = world.Navigation;
            StarSystem here = nav.CurrentSystem;

            GUILayout.Label($"<b>Schiff</b> in #{here.Id} {here.Name}");
            GUILayout.Label($"Treibstoff {nav.Fuel.Current:0}/{nav.Fuel.Max:0} · Reichweite {nav.JumpRange:0.0}");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Tank füllen")) nav.Fuel.Refill(nav.Fuel.Max);
            if (GUILayout.Button("Alles aufdecken")) foreach (var s in map.Systems) s.Discovered = true;
            GUILayout.EndHorizontal();

            GUILayout.Space(8);
            GUILayout.Label("<b>Planeten hier</b>");
            foreach (Planet planet in here.Planets)
            {
                GUILayout.BeginHorizontal();
                string marker = planet.Id == nav.OrbitPlanetId ? "◄ " : string.Empty;
                GUILayout.Label($"{marker}{planet.Name} · {planet.Type} · Gefahr {planet.Hazard}", GUILayout.Width(240));
                if (GUILayout.Button("Umlaufbahn", GUILayout.Width(90))) nav.EnterOrbit(planet.Id);
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(8);
            if (selectedSystem < 0)
            {
                GUILayout.Label("System auf der Karte anklicken.");
                return;
            }

            StarSystem target = map.GetSystem(selectedSystem);
            string owner = map.GetFaction(target.FactionId)?.Name ?? "frei";
            GUILayout.Label($"<b>Auswahl:</b> #{target.Id} {(target.Discovered ? target.Name : "Unerforscht")} · {owner} · {target.Star}");
            if (target.Discovered)
            {
                foreach (Planet planet in target.Planets)
                {
                    GUILayout.Label($"  {planet}", smallLabel);
                }
            }

            JumpResult jump = nav.CanJump(target.Id);
            GUI.enabled = jump == JumpResult.Ok;
            if (GUILayout.Button(jump == JumpResult.Ok ? "Springen" : $"Springen ({jump})"))
            {
                nav.Jump(target.Id);
                galaxyStatus = $"Angekommen in {target.Name}.";
                plannedRoute = null;
            }

            GUI.enabled = true;
            if (GUILayout.Button("Route planen"))
            {
                plannedRoute = RoutePlanner.FindRoute(map, nav.CurrentSystemId, target.Id, nav.JumpRange);
                galaxyStatus = plannedRoute == null
                    ? $"Keine Route mit Reichweite {nav.JumpRange:0.0}. Schiff ausbauen!"
                    : $"{plannedRoute.Jumps} Sprünge, {plannedRoute.Distance:0.0} Lj, Treibstoff ca. {plannedRoute.Distance * nav.FuelPerDistance:0}.";
            }

            if (GUILayout.Button("Warp (Dev, ohne Verbindung)"))
            {
                nav.DebugSetSystem(target.Id);
                plannedRoute = null;
            }

            if (!string.IsNullOrEmpty(galaxyStatus))
            {
                GUILayout.Label(galaxyStatus);
            }
        }

        private static Color SystemColor(StarSystem system)
        {
            Color color;
            switch (system.FactionId)
            {
                case 0: color = new Color(1f, 0.24f, 0.35f); break;   // Konzern
                case 1: color = new Color(0.22f, 0.84f, 0.9f); break; // Synth-Kollektiv
                case 2: color = new Color(0.87f, 0.54f, 0.28f); break; // Schrottgilde
                default: color = new Color(0.85f, 0.85f, 0.8f); break;
            }

            return system.Discovered ? color : color * 0.45f + new Color(0f, 0f, 0f, 0.55f);
        }

        // ------------------------------------------------------------------ Skills

        private void DrawSkills()
        {
            CharacterSkills skills = CurrentSession.World.LocalSkills;
            GUILayout.Label($"Gesamtstufe {skills.TotalLevel} / {CharacterSkills.TotalLevelCap}   (max. {CharacterSkills.MaxLevel} pro Disziplin)");

            foreach (Discipline d in DisciplineNames.All)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label($"<b>{DisciplineNames.German(d)}</b>", GUILayout.Width(100));
                GUILayout.Label($"Stufe {skills.Level(d),2}", GUILayout.Width(70));
                GUILayout.Label($"EP {skills.Experience(d):0}/{CharacterSkills.ExperienceToNext(skills.Level(d)):0}", GUILayout.Width(110));
                GUILayout.Label($"Punkte {skills.AvailablePoints(d)}", GUILayout.Width(70));
                int lockIndex = GUILayout.Toolbar((int)skills.GetLock(d), LockNames, GUILayout.Width(220));
                skills.SetLock(d, (SkillLock)lockIndex);
                if (GUILayout.Button("+100 EP", GUILayout.Width(70))) skills.AddExperience(d, 100f);
                if (GUILayout.Button("+1000 EP", GUILayout.Width(75))) skills.AddExperience(d, 1000f);
                if (GUILayout.Button("Stufe +1", GUILayout.Width(70))) skills.DebugSetLevel(d, skills.Level(d) + 1);
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(8);
            skillScroll = GUILayout.BeginScrollView(skillScroll);
            foreach (Discipline d in DisciplineNames.All)
            {
                GUILayout.Label($"<b>— {DisciplineNames.German(d)} —</b>");
                foreach (SkillNode node in skills.Tree.InTree(d).OrderBy(n => n.RequiredLevel))
                {
                    GUILayout.BeginHorizontal();
                    string status = skills.IsActive(node.Id) ? "aktiv"
                        : skills.IsUnlocked(node.Id) ? "inaktiv (Stufe zu niedrig)"
                        : skills.CanUnlock(node.Id).ToString();
                    string hybrid = node.IsHybrid ? " [Hybrid]" : string.Empty;
                    string prerequisites = node.Prerequisites.Count > 0
                        ? " · braucht " + string.Join(", ", node.Prerequisites.Select(p => skills.Tree.Get(p).Name))
                        : string.Empty;
                    GUILayout.Label($"{node.Name}{hybrid} (Stufe {node.RequiredLevel}, {node.Cost} P){prerequisites}", GUILayout.Width(560));
                    GUILayout.Label(status, GUILayout.Width(200));
                    GUI.enabled = skills.CanUnlock(node.Id) == UnlockCheck.Ok;
                    if (GUILayout.Button("Freischalten", GUILayout.Width(100))) skills.Unlock(node.Id);
                    GUI.enabled = true;
                    GUILayout.EndHorizontal();
                    GUILayout.Label("    " + node.Description, smallLabel);
                }
            }

            GUILayout.EndScrollView();
        }

        // ------------------------------------------------------------------ Schiffswerft

        private void DrawShipyard()
        {
            WorldState world = CurrentSession.World;
            ShipBlueprint ship = world.Ship;
            ModuleDefinition[] modules = world.Modules.All.ToArray();
            moduleIndex = Mathf.Clamp(moduleIndex, 0, modules.Length - 1);

            GUILayout.BeginHorizontal();

            GUILayout.BeginVertical(GUILayout.Width(ship.Width * Cell + 4));
            tool = (Tool)GUILayout.Toolbar((int)tool, ToolNames);
            Rect grid = GUILayoutUtility.GetRect(ship.Width * Cell, ship.Height * Cell, GUILayout.Width(ship.Width * Cell), GUILayout.Height(ship.Height * Cell));
            HandleShipyardInput(grid, ship, modules[moduleIndex]);
            if (Event.current.type == EventType.Repaint)
            {
                DrawShipGrid(grid, ship, modules[moduleIndex]);
            }

            GUILayout.Label("Klicken bzw. ziehen. Module setzen ihre linke untere Ecke auf die angeklickte Kachel.", smallLabel);
            GUILayout.Label(shipyardStatus);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Vorlage: Fregatte"))
            {
                ship.CopyFrom(ShipTemplates.CrashedFrigate(world.Modules));
                shipyardStatus = "Fregatte geladen.";
            }

            if (GUILayout.Button("Alles leeren"))
            {
                ship.Clear();
                shipyardStatus = "Bauplan geleert.";
            }

            GUILayout.EndHorizontal();
            GUILayout.EndVertical();

            GUILayout.BeginVertical();
            GUILayout.Label("<b>Module</b>");
            moduleIndex = GUILayout.SelectionGrid(moduleIndex, modules.Select(m => $"{m.Name} {m.Width}×{m.Height}").ToArray(), 2);
            rotateModule = GUILayout.Toggle(rotateModule, " Drehen (90°)");
            ModuleDefinition m0 = modules[moduleIndex];
            GUILayout.Label($"Energie {m0.Power:+0;-0;0} · O₂ {m0.OxygenPerMinute:0} · Fracht {m0.CargoSlots} · Tank {m0.FuelCapacity:0} · Schub {m0.Thrust:0} · Kojen {m0.CrewBeds} · Masse {m0.Mass:0} · Kosten {m0.Cost}", smallLabel);
            GUILayout.Space(8);
            GUILayout.Label("<b>Schiffswerte</b>");
            GUILayout.Label(CoreDevCommands.DescribeShip(world));
            GUILayout.EndVertical();

            GUILayout.EndHorizontal();
        }

        private void HandleShipyardInput(Rect grid, ShipBlueprint ship, ModuleDefinition module)
        {
            Event e = Event.current;
            bool paintTool = tool == Tool.HullAdd || tool == Tool.HullRemove;
            bool relevant = e.button == 0 && (e.type == EventType.MouseDown || (e.type == EventType.MouseDrag && paintTool));
            if (!relevant || !grid.Contains(e.mousePosition))
            {
                return;
            }

            Vector2Int cell = CellAt(grid, ship, e.mousePosition);
            switch (tool)
            {
                case Tool.HullAdd:
                    ship.SetHull(cell.x, cell.y, true);
                    break;
                case Tool.HullRemove:
                    if (!ship.SetHull(cell.x, cell.y, false) && ship.ModuleAt(cell.x, cell.y) != null)
                    {
                        shipyardStatus = "Unter einem Modul kann der Rumpf nicht entfernt werden.";
                    }

                    break;
                case Tool.PlaceModule:
                    PlacementResult result = ship.Place(module, cell.x, cell.y, rotateModule);
                    shipyardStatus = result == PlacementResult.Ok ? $"{module.Name} gesetzt." : $"Nicht möglich: {result}.";
                    break;
                case Tool.RemoveModule:
                    PlacedModule removed = ship.RemoveModuleAt(cell.x, cell.y);
                    shipyardStatus = removed != null ? $"{removed.Definition.Name} entfernt." : "Hier ist kein Modul.";
                    break;
            }

            e.Use();
        }

        private static Vector2Int CellAt(Rect grid, ShipBlueprint ship, Vector2 mouse)
        {
            int x = Mathf.Clamp((int)((mouse.x - grid.x) / Cell), 0, ship.Width - 1);
            int rowFromTop = Mathf.Clamp((int)((mouse.y - grid.y) / Cell), 0, ship.Height - 1);
            return new Vector2Int(x, ship.Height - 1 - rowFromTop);
        }

        private static Rect CellRect(Rect grid, ShipBlueprint ship, int x, int y, int width = 1, int height = 1)
        {
            return new Rect(grid.x + x * Cell, grid.y + (ship.Height - y - height) * Cell, width * Cell, height * Cell);
        }

        private void DrawShipGrid(Rect grid, ShipBlueprint ship, ModuleDefinition module)
        {
            DrawRect(grid, new Color(0.03f, 0.03f, 0.06f));
            for (int x = 0; x < ship.Width; x++)
            {
                for (int y = 0; y < ship.Height; y++)
                {
                    Rect r = CellRect(grid, ship, x, y);
                    Color color = ship.HasHull(x, y) ? new Color(0.36f, 0.4f, 0.5f) : new Color(0.1f, 0.11f, 0.15f);
                    DrawRect(new Rect(r.x, r.y, r.width - 1, r.height - 1), color);
                }
            }

            foreach (PlacedModule placed in ship.Modules)
            {
                Rect r = CellRect(grid, ship, placed.X, placed.Y, placed.Width, placed.Height);
                DrawRect(new Rect(r.x + 1, r.y + 1, r.width - 3, r.height - 3), CategoryColor(placed.Definition.Category));
                GUI.Label(new Rect(r.x + 2, r.y, r.width, 16), Abbreviation(placed.Definition), smallLabel);
            }

            Vector2 mouse = Event.current.mousePosition;
            if (tool == Tool.PlaceModule && grid.Contains(mouse))
            {
                Vector2Int cell = CellAt(grid, ship, mouse);
                bool ok = ship.CanPlace(module, cell.x, cell.y, rotateModule) == PlacementResult.Ok;
                int w = rotateModule ? module.Height : module.Width;
                int h = rotateModule ? module.Width : module.Height;
                DrawRect(CellRect(grid, ship, cell.x, cell.y, w, h), ok ? new Color(0.4f, 1f, 0.4f, 0.35f) : new Color(1f, 0.3f, 0.3f, 0.35f));
            }
        }

        private static Color CategoryColor(ModuleCategory category)
        {
            switch (category)
            {
                case ModuleCategory.Command: return new Color(0.22f, 0.84f, 0.9f);
                case ModuleCategory.Power: return new Color(1f, 0.71f, 0.18f);
                case ModuleCategory.Propulsion: return new Color(0.87f, 0.54f, 0.28f);
                case ModuleCategory.LifeSupport: return new Color(0.65f, 0.85f, 0.29f);
                case ModuleCategory.Storage: return new Color(0.7f, 0.35f, 0.19f);
                case ModuleCategory.Crew: return new Color(0.78f, 0.49f, 1f);
                case ModuleCategory.Production: return new Color(0.71f, 0.74f, 0.8f);
                default: return new Color(1f, 0.24f, 0.35f);
            }
        }

        private static string Abbreviation(ModuleDefinition definition)
        {
            string name = definition.Name;
            return name.Length <= 3 ? name : name.Substring(0, 3);
        }

        // ------------------------------------------------------------------ Welt

        private void DrawWorld()
        {
            GameSession session = CurrentSession;
            var clock = session.World.Clock;

            GUILayout.Label($"<b>Welt</b> · Seed {session.World.Seed}");
            GUILayout.BeginHorizontal();
            GUILayout.Label($"Tageszeit {clock.TimeOfDay:0.00} · {clock.Phase} · Helligkeit {clock.Daylight:0.00} · Tag {clock.DayCount + 1}", GUILayout.Width(420));
            float time = GUILayout.HorizontalSlider(clock.TimeOfDay, 0f, 0.999f, GUILayout.Width(300));
            if (!Mathf.Approximately(time, clock.TimeOfDay))
            {
                clock.SetTimeOfDay(time);
            }

            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label($"Zeitraffer ×{session.TimeScale:0.##}", GUILayout.Width(140));
            foreach (float factor in new[] { 0f, 1f, 10f, 60f, 300f })
            {
                if (GUILayout.Button(factor == 0f ? "Pause" : $"×{factor:0}", GUILayout.Width(70)))
                {
                    session.TimeScale = factor;
                }
            }

            GUILayout.EndHorizontal();

            var lighting = GetComponent<WorldLighting>();
            if (lighting != null)
            {
                lighting.FollowTimeOfDay = GUILayout.Toggle(lighting.FollowTimeOfDay, " Licht folgt der Tageszeit");
            }

            GUILayout.Space(10);
            GUILayout.Label("<b>Neue Welt</b> (ersetzt Galaxie, Schiff und Skills)");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Zufälliger Seed", GUILayout.Width(160)))
            {
                RunCommand($"newworld {Random.Range(1, 1000000)}");
            }

            if (GUILayout.Button("Standardwelt", GUILayout.Width(160)))
            {
                RunCommand($"newworld {GameSession.DefaultSeed}");
            }

            GUILayout.EndHorizontal();

            GUILayout.Space(10);
            NetworkManager manager = NetworkManager.Singleton;
            string net = manager == null || !manager.IsListening ? "keine Sitzung"
                : manager.IsHost ? $"Host, {manager.ConnectedClientsIds.Count} Spieler"
                : "Mitspieler";
            GUILayout.Label($"<b>Netzwerk</b> · {net}");
            Transform player = LocalPlayer();
            if (player != null)
            {
                GUILayout.Label($"Eigene Figur bei {player.position.x:0.0}, {player.position.y:0.0}");
            }
        }

        // ------------------------------------------------------------------ Hilfen

        private void EnsureStyles()
        {
            if (smallLabel != null)
            {
                return;
            }

            GUI.skin.label.richText = true;
            smallLabel = new GUIStyle(GUI.skin.label) { fontSize = 11, richText = true };
            logStyle = new GUIStyle(GUI.skin.label) { fontSize = 12, wordWrap = false, richText = false };
        }

        private static void DrawRect(Rect rect, Color color)
        {
            Color old = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = old;
        }

        private static void DrawDots(Vector2 a, Vector2 b, Color color, float size)
        {
            float length = Vector2.Distance(a, b);
            int steps = Mathf.Max(1, (int)(length / 5f));
            for (int i = 0; i <= steps; i++)
            {
                Vector2 p = Vector2.Lerp(a, b, i / (float)steps);
                DrawRect(new Rect(p.x - size / 2f, p.y - size / 2f, size, size), color);
            }
        }
    }
}
