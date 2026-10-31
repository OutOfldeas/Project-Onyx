using System;
using System.Linq;
using System.Text;
using Onyx.Core.CrashSite;
using Onyx.Core.Galaxy;
using Onyx.Core.Session;
using Onyx.Core.Skills;

namespace Onyx.Core.DevTools
{
    /// <summary>
    /// Entwicklerbefehle für alle Systeme der Kernlogik. Die Unity-Seite ergänzt eigene Befehle
    /// (z. B. Teleport der Figur); diese hier funktionieren auch ohne Unity.
    /// </summary>
    public static class CoreDevCommands
    {
        /// <param name="getWorld">Liefert die aktuelle Welt (kann sich durch „newworld“ ändern).</param>
        /// <param name="replaceWorld">Ersetzt die Welt, z. B. bei einem neuen Seed.</param>
        public static void Register(DevCommandRegistry registry, Func<WorldState> getWorld, Action<WorldState> replaceWorld)
        {
            // --- Welt ---
            registry.Register("newworld", "newworld <seed>", "Erzeugt eine neue Welt mit diesem Seed.", args =>
            {
                int seed = DevCommandRegistry.Int(args, 0, "seed");
                replaceWorld(WorldState.CreateNew(seed));
                return $"Neue Welt mit Seed {seed}: {getWorld().Galaxy.Systems.Count} Systeme.";
            });

            registry.Register("time", "time <0-1>", "Setzt die Tageszeit (0 Mitternacht, 0,5 Mittag).", args =>
            {
                var clock = getWorld().Clock;
                clock.SetTimeOfDay(DevCommandRegistry.Float(args, 0, "tageszeit"));
                return $"Tageszeit {clock.TimeOfDay:0.00} ({clock.Phase}), Helligkeit {clock.Daylight:0.00}.";
            });

            // --- Galaxie und Reisen ---
            registry.Register("where", "where", "Zeigt das aktuelle System, Planeten und Nachbarn.", _ => DescribeLocation(getWorld()));

            registry.Register("systems", "systems", "Listet alle Systeme der Galaxie.", _ =>
            {
                var w = getWorld();
                var b = new StringBuilder();
                foreach (var s in w.Galaxy.Systems)
                {
                    string owner = w.Galaxy.GetFaction(s.FactionId)?.Name ?? "frei";
                    string marker = s.Id == w.Navigation.CurrentSystemId ? " ◄ hier" : string.Empty;
                    b.AppendLine($"#{s.Id,-3} {s.Name,-14} {owner,-15} {(s.Discovered ? "entdeckt" : "unbekannt")}{marker}");
                }

                return b.ToString().TrimEnd();
            });

            registry.Register("jump", "jump <systemId>", "Springt zu einem benachbarten System.", args =>
            {
                var w = getWorld();
                int target = DevCommandRegistry.Int(args, 0, "systemId");
                var result = w.Navigation.Jump(target);
                return result == JumpResult.Ok ? DescribeLocation(w) : $"Sprung nicht möglich: {result}.";
            });

            registry.Register("route", "route <systemId>", "Plant die kürzeste Route mit der aktuellen Sprungreichweite.", args =>
            {
                var w = getWorld();
                int target = DevCommandRegistry.Int(args, 0, "systemId");
                var route = RoutePlanner.FindRoute(w.Galaxy, w.Navigation.CurrentSystemId, target, w.Navigation.JumpRange);
                if (route == null)
                {
                    return $"Keine Route mit Sprungreichweite {w.Navigation.JumpRange:0.0} gefunden.";
                }

                string path = string.Join(" → ", route.Systems.Select(id => w.Galaxy.GetSystem(id).Name));
                return $"{path}\n{route.Jumps} Sprünge, {route.Distance:0.0} Lj, Treibstoff ca. {route.Distance * w.Navigation.FuelPerDistance:0}.";
            });

            registry.Register("warp", "warp <systemId>", "Teleport in ein System (ohne Verbindung und Treibstoff).", args =>
            {
                var w = getWorld();
                w.Navigation.DebugSetSystem(DevCommandRegistry.Int(args, 0, "systemId"));
                return DescribeLocation(w);
            });

            registry.Register("fuel", "fuel [menge]", "Füllt den Tank auf (ohne Angabe: voll).", args =>
            {
                var fuel = getWorld().Navigation.Fuel;
                fuel.Refill(args.Length > 0 ? DevCommandRegistry.Float(args, 0, "menge") : fuel.Max);
                return $"Treibstoff {fuel.Current:0}/{fuel.Max:0}.";
            });

            registry.Register("range", "range <reichweite>", "Setzt die Sprungreichweite (bis zur nächsten Änderung am Schiff).", args =>
            {
                var nav = getWorld().Navigation;
                nav.JumpRange = System.Math.Max(0f, DevCommandRegistry.Float(args, 0, "reichweite"));
                return $"Sprungreichweite {nav.JumpRange:0.0}.";
            });

            registry.Register("reveal", "reveal", "Deckt alle Systeme auf.", _ =>
            {
                foreach (var s in getWorld().Galaxy.Systems) s.Discovered = true;
                return "Alle Systeme aufgedeckt.";
            });

            // --- Skills ---
            registry.Register("skills", "skills", "Zeigt Disziplinen, Stufen und freigeschaltete Fähigkeiten.", _ => DescribeSkills(getWorld().LocalSkills));

            registry.Register("xp", "xp <disziplin> <menge>", "Gibt Erfahrung in einer Disziplin (erkundung, technik, diplomatie, logistik).", args =>
            {
                var skills = getWorld().LocalSkills;
                var d = ParseDiscipline(DevCommandRegistry.Text(args, 0, "disziplin"));
                float gained = skills.AddExperience(d, DevCommandRegistry.Float(args, 1, "menge"));
                return $"+{gained:0} EP {DisciplineNames.German(d)} → Stufe {skills.Level(d)} (gesamt {skills.TotalLevel}/{CharacterSkills.TotalLevelCap}).";
            });

            registry.Register("level", "level <disziplin> <stufe>", "Setzt eine Disziplinstufe direkt.", args =>
            {
                var skills = getWorld().LocalSkills;
                var d = ParseDiscipline(DevCommandRegistry.Text(args, 0, "disziplin"));
                skills.DebugSetLevel(d, DevCommandRegistry.Int(args, 1, "stufe"));
                return $"{DisciplineNames.German(d)} auf Stufe {skills.Level(d)}.";
            });

            registry.Register("lock", "lock <disziplin> <raise|lower|locked>", "Umlern-Einstellung einer Disziplin.", args =>
            {
                var skills = getWorld().LocalSkills;
                var d = ParseDiscipline(DevCommandRegistry.Text(args, 0, "disziplin"));
                if (!Enum.TryParse(DevCommandRegistry.Text(args, 1, "modus"), true, out SkillLock mode))
                {
                    throw new DevCommandException("Modus muss raise, lower oder locked sein.");
                }

                skills.SetLock(d, mode);
                return $"{DisciplineNames.German(d)}: {mode}.";
            });

            registry.Register("unlock", "unlock <skillId>", "Schaltet eine Fähigkeit frei (z. B. eng.repair1).", args =>
            {
                var skills = getWorld().LocalSkills;
                string id = DevCommandRegistry.Text(args, 0, "skillId");
                if (!skills.Tree.Contains(id))
                {
                    return $"Unbekannte Fähigkeit „{id}“.";
                }

                var result = skills.Unlock(id);
                return result == UnlockCheck.Ok ? $"Freigeschaltet: {skills.Tree.Get(id).Name}." : $"Nicht möglich: {result}.";
            });

            // --- Schiff ---
            registry.Register("ship", "ship", "Zeigt die Werte und Mängel des Schiffs.", _ => DescribeShip(getWorld()));

            registry.Register("place", "place <modulId> <x> <y> [r]", "Platziert ein Modul im Bauplan (r = gedreht).", args =>
            {
                var w = getWorld();
                string id = DevCommandRegistry.Text(args, 0, "modulId");
                if (!w.Modules.TryGet(id, out var definition))
                {
                    return $"Unbekanntes Modul „{id}“. Verfügbar: {string.Join(", ", w.Modules.All.Select(m => m.Id))}";
                }

                bool rotated = args.Length > 3 && args[3] == "r";
                var result = w.Ship.Place(definition, DevCommandRegistry.Int(args, 1, "x"), DevCommandRegistry.Int(args, 2, "y"), rotated);
                return result == Ships.PlacementResult.Ok ? $"{definition.Name} platziert." : $"Nicht möglich: {result}.";
            });

            registry.Register("hull", "hull <x> <y> <breite> <höhe>", "Füllt ein Rechteck mit Rumpfkacheln.", args =>
            {
                var w = getWorld();
                w.Ship.FillHull(DevCommandRegistry.Int(args, 0, "x"), DevCommandRegistry.Int(args, 1, "y"), DevCommandRegistry.Int(args, 2, "breite"), DevCommandRegistry.Int(args, 3, "höhe"));
                return $"Rumpf: {w.Ship.HullCount} Kacheln.";
            });

            // --- Absturzstelle ---
            registry.Register("site", "site", "Zeigt den Reparaturstand der Fregatte.", _ => DescribeCrashSite(getWorld().CrashSite));

            registry.Register("repair", "repair <sauerstoff|reaktor|antrieb|all> [prozent]", "Setzt den Reparaturfortschritt einer Station (nur beim Host wirksam).", args =>
            {
                var site = getWorld().CrashSite;
                string which = DevCommandRegistry.Text(args, 0, "station").ToLowerInvariant();
                float percent = args.Length > 1 ? DevCommandRegistry.Float(args, 1, "prozent") : 100f;
                var targets = which == "all" ? site.Layout.RepairPoints.ToList() : new System.Collections.Generic.List<RepairPoint> { site.Point(ParseStation(which)) };
                foreach (var point in targets)
                {
                    site.ApplySyncedProgress(point.Index, point.Work * percent / 100f);
                }

                return DescribeCrashSite(site);
            });
        }

        public static StationKind ParseStation(string text)
        {
            switch (text.ToLowerInvariant())
            {
                case "sauerstoff":
                case "o2":
                case "oxygen":
                    return StationKind.OxygenGenerator;
                case "reaktor":
                case "reactor":
                    return StationKind.Reactor;
                case "antrieb":
                case "engine":
                    return StationKind.Engine;
                default:
                    throw new DevCommandException($"Unbekannte Station „{text}“ (sauerstoff, reaktor, antrieb, all).");
            }
        }

        public static string DescribeCrashSite(CrashSiteState site)
        {
            var b = new StringBuilder();
            foreach (var point in site.Layout.RepairPoints)
            {
                var job = site.Job(point.Index);
                string state = job.IsComplete ? "läuft" : $"{job.Ratio * 100f:0} %";
                b.AppendLine($"{point.Name,-20} {state,-6} Helfer {job.WorkerCount}");
            }

            b.Append(site.AllRepaired ? "Fregatte startklar." : $"{site.RepairedCount}/{site.Layout.RepairPoints.Count} Systeme laufen.");
            return b.ToString();
        }

        public static Discipline ParseDiscipline(string text)
        {
            switch (text.ToLowerInvariant())
            {
                case "erkundung":
                case "exploration":
                case "scout":
                    return Discipline.Exploration;
                case "technik":
                case "engineering":
                case "ingenieur":
                    return Discipline.Engineering;
                case "diplomatie":
                case "diplomacy":
                case "diplomat":
                    return Discipline.Diplomacy;
                case "logistik":
                case "logistics":
                case "logistiker":
                    return Discipline.Logistics;
                default:
                    throw new DevCommandException($"Unbekannte Disziplin „{text}“.");
            }
        }

        public static string DescribeLocation(WorldState w)
        {
            var nav = w.Navigation;
            var system = nav.CurrentSystem;
            var b = new StringBuilder();
            string owner = w.Galaxy.GetFaction(system.FactionId)?.Name ?? "frei";
            b.AppendLine($"System #{system.Id} {system.Name} ({system.Star}, {owner})");
            b.AppendLine($"Treibstoff {nav.Fuel.Current:0}/{nav.Fuel.Max:0} · Sprungreichweite {nav.JumpRange:0.0}");
            b.AppendLine("Planeten:");
            foreach (var planet in system.Planets)
            {
                b.AppendLine($"  {planet}");
            }

            b.AppendLine("Verbindungen:");
            foreach (var lane in w.Galaxy.LanesOf(system.Id))
            {
                var target = w.Galaxy.GetSystem(lane.Other(system.Id));
                string name = target.Discovered ? target.Name : "???";
                b.AppendLine($"  #{target.Id} {name} – {lane.Distance:0.0} Lj, {nav.FuelCost(lane):0} Treibstoff ({nav.CanJump(target.Id)})");
            }

            return b.ToString().TrimEnd();
        }

        public static string DescribeSkills(CharacterSkills skills)
        {
            var b = new StringBuilder();
            b.AppendLine($"Gesamtstufe {skills.TotalLevel}/{CharacterSkills.TotalLevelCap}");
            foreach (var d in DisciplineNames.All)
            {
                b.AppendLine($"{DisciplineNames.German(d),-11} Stufe {skills.Level(d),2}  EP {skills.Experience(d):0}/{CharacterSkills.ExperienceToNext(skills.Level(d)):0}  Punkte frei {skills.AvailablePoints(d)}  [{skills.GetLock(d)}]");
            }

            if (skills.Unlocked.Count > 0)
            {
                b.AppendLine("Freigeschaltet: " + string.Join(", ", skills.Unlocked));
            }

            return b.ToString().TrimEnd();
        }

        public static string DescribeShip(WorldState w)
        {
            var s = w.ShipStats;
            var b = new StringBuilder();
            b.AppendLine($"{w.Ship.Name}: {w.Ship.HullCount} Rumpfkacheln, {w.Ship.Modules.Count} Module, Masse {s.Mass:0}");
            b.AppendLine($"Energie {s.PowerProduced:0}/{s.PowerConsumed:0} (Bilanz {s.PowerBalance:+0;-0;0}) · O₂ {s.OxygenPerMinute:0}/min · Fracht {s.CargoSlots}");
            b.AppendLine($"Schub {s.Thrust:0} · Tank {s.FuelCapacity:0} · Reichweite {s.JumpRange:0.0} · Kojen {s.CrewBeds} · Schild {s.Shield:0} · Kosten {s.TotalCost}");
            b.Append(s.IsSpaceworthy ? "Flugbereit." : "Mängel:\n  " + string.Join("\n  ", s.Issues));
            return b.ToString();
        }
    }
}
