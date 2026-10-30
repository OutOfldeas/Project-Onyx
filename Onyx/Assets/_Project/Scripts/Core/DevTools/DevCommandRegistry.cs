using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Onyx.Core.DevTools
{
    /// <summary>
    /// Befehle für die Entwicklerkonsole. Jeder Befehl bekommt die Argumente als Text
    /// und liefert eine Antwort zurück. Ohne Unity testbar.
    /// </summary>
    public sealed class DevCommandRegistry
    {
        private sealed class Command
        {
            public string Name;
            public string Usage;
            public string Description;
            public Func<string[], string> Handler;
        }

        private readonly Dictionary<string, Command> commands = new Dictionary<string, Command>(StringComparer.OrdinalIgnoreCase);

        public DevCommandRegistry()
        {
            Register("help", "help [befehl]", "Zeigt alle Befehle oder die Hilfe zu einem Befehl.", Help);
        }

        public IEnumerable<string> CommandNames => commands.Keys.OrderBy(k => k);

        public void Register(string name, string usage, string description, Func<string[], string> handler)
        {
            if (string.IsNullOrWhiteSpace(name) || name.Any(char.IsWhiteSpace))
            {
                throw new ArgumentException("Befehlsname darf nicht leer sein und keine Leerzeichen enthalten.", nameof(name));
            }

            commands[name] = new Command
            {
                Name = name,
                Usage = usage,
                Description = description,
                Handler = handler ?? throw new ArgumentNullException(nameof(handler)),
            };
        }

        /// <summary>Führt eine Eingabezeile aus, z. B. „xp technik 500“.</summary>
        public string Execute(string line)
        {
            var parts = Tokenize(line);
            if (parts.Count == 0)
            {
                return string.Empty;
            }

            if (!commands.TryGetValue(parts[0], out var command))
            {
                return $"Unbekannter Befehl „{parts[0]}“. „help“ zeigt alle Befehle.";
            }

            try
            {
                return command.Handler(parts.Skip(1).ToArray());
            }
            catch (DevCommandException e)
            {
                return $"{e.Message}\nAufruf: {command.Usage}";
            }
            catch (Exception e)
            {
                return $"Fehler in „{command.Name}“: {e.Message}";
            }
        }

        /// <summary>Befehle, die mit dem Text beginnen (für Autovervollständigung).</summary>
        public IEnumerable<string> Complete(string prefix)
        {
            return CommandNames.Where(n => n.StartsWith(prefix ?? string.Empty, StringComparison.OrdinalIgnoreCase));
        }

        private string Help(string[] args)
        {
            if (args.Length > 0 && commands.TryGetValue(args[0], out var single))
            {
                return $"{single.Usage}\n{single.Description}";
            }

            var builder = new StringBuilder();
            foreach (var name in CommandNames)
            {
                var c = commands[name];
                builder.AppendLine($"{c.Usage,-34} {c.Description}");
            }

            return builder.ToString().TrimEnd();
        }

        /// <summary>Teilt an Leerzeichen; Text in Anführungszeichen bleibt zusammen.</summary>
        public static List<string> Tokenize(string line)
        {
            var result = new List<string>();
            if (string.IsNullOrWhiteSpace(line))
            {
                return result;
            }

            var current = new StringBuilder();
            bool quoted = false;
            foreach (char c in line.Trim())
            {
                if (c == '"')
                {
                    quoted = !quoted;
                }
                else if (char.IsWhiteSpace(c) && !quoted)
                {
                    if (current.Length > 0)
                    {
                        result.Add(current.ToString());
                        current.Clear();
                    }
                }
                else
                {
                    current.Append(c);
                }
            }

            if (current.Length > 0)
            {
                result.Add(current.ToString());
            }

            return result;
        }

        // --- Hilfen zum Lesen von Argumenten ---

        public static int Int(string[] args, int index, string name)
        {
            if (index >= args.Length || !int.TryParse(args[index], out int value))
            {
                throw new DevCommandException($"Zahl für „{name}“ erwartet.");
            }

            return value;
        }

        public static float Float(string[] args, int index, string name)
        {
            if (index >= args.Length || !float.TryParse(args[index].Replace(',', '.'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float value))
            {
                throw new DevCommandException($"Zahl für „{name}“ erwartet.");
            }

            return value;
        }

        public static string Text(string[] args, int index, string name)
        {
            if (index >= args.Length)
            {
                throw new DevCommandException($"Angabe „{name}“ fehlt.");
            }

            return args[index];
        }
    }

    /// <summary>Falsche Eingabe in einem Befehl; die Konsole zeigt dann den richtigen Aufruf an.</summary>
    public sealed class DevCommandException : Exception
    {
        public DevCommandException(string message) : base(message)
        {
        }
    }
}
