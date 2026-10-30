using System;
using System.Collections.Generic;
using System.Linq;

namespace Onyx.Core.Skills
{
    public enum Discipline
    {
        /// <summary>Scout</summary>
        Exploration,

        /// <summary>Ingenieur</summary>
        Engineering,

        /// <summary>Diplomat</summary>
        Diplomacy,

        /// <summary>Logistiker</summary>
        Logistics,
    }

    public static class DisciplineNames
    {
        public static string German(Discipline discipline)
        {
            switch (discipline)
            {
                case Discipline.Exploration: return "Erkundung";
                case Discipline.Engineering: return "Technik";
                case Discipline.Diplomacy: return "Diplomatie";
                default: return "Logistik";
            }
        }

        public static readonly Discipline[] All =
        {
            Discipline.Exploration, Discipline.Engineering, Discipline.Diplomacy, Discipline.Logistics,
        };
    }

    /// <summary>
    /// Eine Fähigkeit im Skilltree. Gehört zu genau einem Baum (<see cref="Discipline"/>) und kostet
    /// dessen Punkte; Voraussetzungen dürfen aus anderen Bäumen stammen (Hybrid-Fähigkeiten).
    /// </summary>
    public sealed class SkillNode
    {
        public string Id { get; }
        public string Name { get; }
        public string Description { get; }
        public Discipline Discipline { get; }
        public int Cost { get; }

        /// <summary>Mindeststufe der eigenen Disziplin, damit die Fähigkeit wirkt.</summary>
        public int RequiredLevel { get; }

        public IReadOnlyList<string> Prerequisites { get; }

        /// <summary>Hat Voraussetzungen aus mindestens einem anderen Baum.</summary>
        public bool IsHybrid { get; internal set; }

        public SkillNode(string id, string name, Discipline discipline, int cost, int requiredLevel, string description, params string[] prerequisites)
        {
            Id = id;
            Name = name;
            Discipline = discipline;
            Cost = cost;
            RequiredLevel = requiredLevel;
            Description = description;
            Prerequisites = prerequisites ?? Array.Empty<string>();
        }
    }

    public sealed class SkillTree
    {
        private readonly Dictionary<string, SkillNode> nodes;

        public IEnumerable<SkillNode> Nodes => nodes.Values;

        public SkillTree(IEnumerable<SkillNode> nodeList)
        {
            nodes = new Dictionary<string, SkillNode>();
            foreach (var node in nodeList)
            {
                if (nodes.ContainsKey(node.Id))
                {
                    throw new ArgumentException($"Skill-ID doppelt: {node.Id}");
                }

                nodes[node.Id] = node;
            }

            foreach (var node in nodes.Values)
            {
                foreach (string prerequisite in node.Prerequisites)
                {
                    if (!nodes.TryGetValue(prerequisite, out var pre))
                    {
                        throw new ArgumentException($"{node.Id}: unbekannte Voraussetzung {prerequisite}");
                    }

                    if (pre.Discipline != node.Discipline)
                    {
                        node.IsHybrid = true;
                    }
                }
            }

            ThrowOnCycle();
        }

        public SkillNode Get(string id)
        {
            return nodes.TryGetValue(id, out var node) ? node : throw new KeyNotFoundException($"Unbekannte Fähigkeit: {id}");
        }

        public bool Contains(string id) => nodes.ContainsKey(id);

        public IEnumerable<SkillNode> InTree(Discipline discipline) => nodes.Values.Where(n => n.Discipline == discipline);

        private void ThrowOnCycle()
        {
            var state = new Dictionary<string, int>(); // 0 = offen, 1 = in Bearbeitung, 2 = fertig
            void Visit(SkillNode node)
            {
                state.TryGetValue(node.Id, out int s);
                if (s == 2) return;
                if (s == 1) throw new ArgumentException($"Zyklus im Skilltree bei {node.Id}");
                state[node.Id] = 1;
                foreach (string pre in node.Prerequisites) Visit(nodes[pre]);
                state[node.Id] = 2;
            }

            foreach (var node in nodes.Values) Visit(node);
        }
    }

    /// <summary>Erster Entwurf des Skilltrees, abgeleitet aus dem GDD (Abschnitt 5).</summary>
    public static class SkillCatalog
    {
        public static SkillTree CreateDefault()
        {
            return new SkillTree(new[]
            {
                // Erkundung
                new SkillNode("exp.scanner1", "Scanner I", Discipline.Exploration, 1, 1, "Ressourcen im Umkreis werden sichtbar."),
                new SkillNode("exp.deepscan", "Tiefenscan", Discipline.Exploration, 2, 4, "Größere Scanreichweite, zeigt Gefahren.", "exp.scanner1"),
                new SkillNode("exp.oreveins", "Erzadern erkennen", Discipline.Exploration, 2, 8, "Seltene Erzadern werden sichtbar.", "exp.deepscan"),
                new SkillNode("exp.lightfoot", "Leichter Schritt", Discipline.Exploration, 1, 2, "Weniger Sauerstoffverbrauch beim Laufen."),

                // Technik
                new SkillNode("eng.repair1", "Grundreparatur", Discipline.Engineering, 1, 1, "Reparaturen 20 % schneller."),
                new SkillNode("eng.fieldrepair", "Feldreparatur", Discipline.Engineering, 2, 4, "Reparieren ohne Werkbank.", "eng.repair1"),
                new SkillNode("eng.precision", "Präzisionsreparatur", Discipline.Engineering, 3, 10, "Reparaturen stellen mehr Haltbarkeit her.", "eng.fieldrepair", "hyb.weakspot"),
                new SkillNode("eng.reactor", "Reaktortechnik", Discipline.Engineering, 2, 6, "Reaktoren liefern mehr Energie.", "eng.repair1"),

                // Diplomatie
                new SkillNode("dip.trade1", "Feilschen", Discipline.Diplomacy, 1, 1, "Bessere Preise beim Handel."),
                new SkillNode("dip.languages", "Fraktionssprachen", Discipline.Diplomacy, 2, 5, "Funksprüche der Fraktionen verstehen.", "dip.trade1"),
                new SkillNode("dip.calm", "Beschwichtigen", Discipline.Diplomacy, 2, 8, "Feindselige Begegnungen entschärfen.", "dip.languages"),

                // Logistik
                new SkillNode("log.storage", "Lagerverwaltung", Discipline.Logistics, 1, 1, "Mehr Platz in Lagern."),
                new SkillNode("log.conveyors", "Förderbänder", Discipline.Logistics, 2, 4, "Förderbänder bauen.", "log.storage"),
                new SkillNode("log.recycling", "Recycling", Discipline.Logistics, 2, 6, "Mehr Material aus Schrott.", "log.storage"),

                // Hybrid-Fähigkeiten (Voraussetzungen aus zwei Bäumen)
                new SkillNode("hyb.weakspot", "Schwachstellenscan", Discipline.Exploration, 2, 6, "Scan zeigt Schwachstellen von Maschinen, Wracks und Gegnern.", "exp.deepscan", "eng.fieldrepair"),
                new SkillNode("hyb.pathfinder", "Pfadfinder", Discipline.Logistics, 2, 6, "Erkundete Wege beschleunigen Träger und Drohnen.", "exp.deepscan", "log.conveyors"),
                new SkillNode("hyb.recon", "Aufklärung", Discipline.Diplomacy, 2, 6, "Gesammelte Informationen geben Verhandlungsvorteile.", "exp.scanner1", "dip.languages"),
                new SkillNode("hyb.automation", "Automatisierung", Discipline.Engineering, 3, 8, "Förderbänder und Drohnen vernetzen.", "eng.fieldrepair", "log.conveyors"),
                new SkillNode("hyb.alientech", "Fremdtechnik", Discipline.Engineering, 3, 10, "Technik anderer Fraktionen nachbauen.", "eng.reactor", "dip.languages"),
                new SkillNode("hyb.tradenet", "Handelsnetz", Discipline.Logistics, 3, 8, "Handelsrouten und bessere Preise.", "log.recycling", "dip.trade1"),
            });
        }
    }
}
