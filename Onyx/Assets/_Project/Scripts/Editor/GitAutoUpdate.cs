using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Onyx.Editor
{
    /// <summary>
    /// Holt neue Versionen aus dem GitHub-Repo automatisch, solange der Editor offen ist:
    /// beim Start und dann alle paar Minuten (nicht während Play). Aktualisiert nur, wenn es
    /// gefahrlos geht (<c>git merge --ff-only</c>) – eigene Änderungen werden nie stillschweigend
    /// überschrieben. Stehen lokale Änderungen im Weg, fragt ein Dialog nach.
    /// Menü: <b>Onyx → Git</b>.
    /// </summary>
    [InitializeOnLoad]
    public static class GitAutoUpdate
    {
        private const string EnabledKey = "Onyx.GitAutoUpdate.Enabled";
        private const string LastCheckKey = "Onyx.GitAutoUpdate.LastCheck";
        private const double IntervalSeconds = 180;
        private const int GitTimeoutMs = 60000;

        private static readonly ConcurrentQueue<Action> MainThread = new ConcurrentQueue<Action>();
        private static bool running;

        public static bool Enabled
        {
            get => EditorPrefs.GetBool(EnabledKey, true);
            set => EditorPrefs.SetBool(EnabledKey, value);
        }

        /// <summary>Wurzel des Git-Repos (eine Ebene über dem Unity-Projekt).</summary>
        private static string RepoRoot => Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));

        static GitAutoUpdate()
        {
            EditorApplication.update += Update;
        }

        [MenuItem("Onyx/Git/Jetzt aktualisieren", priority = 100)]
        private static void UpdateNow() => Check(interactive: true);

        [MenuItem("Onyx/Git/Automatisch aktualisieren", priority = 101)]
        private static void ToggleEnabled()
        {
            Enabled = !Enabled;
            Debug.Log($"[Onyx] Automatisches Aktualisieren aus dem Repo ist jetzt {(Enabled ? "an" : "aus")}.");
        }

        [MenuItem("Onyx/Git/Automatisch aktualisieren", true)]
        private static bool ToggleEnabledValidate()
        {
            Menu.SetChecked("Onyx/Git/Automatisch aktualisieren", Enabled);
            return true;
        }

        private static void Update()
        {
            while (MainThread.TryDequeue(out var action))
            {
                action();
            }

            if (!Enabled || running || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                return;
            }

            double now = EditorApplication.timeSinceStartup;
            double last = SessionState.GetFloat(LastCheckKey, -1000f);
            if (now - last >= IntervalSeconds || now < last)
            {
                Check(interactive: false);
            }
        }

        private static void Check(bool interactive)
        {
            if (running)
            {
                return;
            }

            running = true;
            SessionState.SetFloat(LastCheckKey, (float)EditorApplication.timeSinceStartup);
            string root = RepoRoot;

            Task.Run(() =>
            {
                try
                {
                    var result = FetchAndMerge(root);
                    MainThread.Enqueue(() => Finish(result, interactive, root));
                }
                catch (Exception e)
                {
                    MainThread.Enqueue(() =>
                    {
                        running = false;
                        if (interactive) EditorUtility.DisplayDialog("Onyx", $"Aktualisieren fehlgeschlagen:\n{e.Message}", "OK");
                        else Debug.LogWarning($"[Onyx] Automatisches Aktualisieren fehlgeschlagen: {e.Message}");
                    });
                }
            });
        }

        private enum Outcome
        {
            UpToDate,
            Updated,
            LocalCommitsAhead,
            BlockedByLocalChanges,
            Failed,
        }

        private sealed class Result
        {
            public Outcome Outcome;
            public string Details = string.Empty;
            public string Commits = string.Empty;
            public string ChangedFiles = string.Empty;
            public string Upstream = "origin/main";
        }

        // Läuft im Hintergrund – hier keine Unity-API benutzen.
        private static Result FetchAndMerge(string root)
        {
            var fetch = Git(root, "fetch --quiet origin");
            if (fetch.code != 0)
            {
                return new Result { Outcome = Outcome.Failed, Details = fetch.output };
            }

            string upstream = Git(root, "rev-parse --abbrev-ref --symbolic-full-name @{u}").output.Trim();
            if (string.IsNullOrEmpty(upstream) || upstream.Contains("fatal"))
            {
                upstream = "origin/main";
            }

            string head = Git(root, "rev-parse HEAD").output.Trim();
            string remote = Git(root, $"rev-parse {upstream}").output.Trim();
            if (head == remote)
            {
                return new Result { Outcome = Outcome.UpToDate };
            }

            // Eigene, noch nicht gepushte Commits? Dann nicht automatisch zusammenführen.
            if (Git(root, $"merge-base --is-ancestor HEAD {upstream}").code != 0)
            {
                return new Result { Outcome = Outcome.LocalCommitsAhead, Details = upstream };
            }

            string commits = Git(root, $"log --format=\"- %s\" HEAD..{upstream}").output.Trim();
            string files = Git(root, $"diff --name-only HEAD {upstream}").output.Trim();
            var merge = Git(root, $"merge --ff-only {upstream}");
            if (merge.code == 0)
            {
                return new Result { Outcome = Outcome.Updated, Commits = commits, ChangedFiles = files };
            }

            return new Result { Outcome = Outcome.BlockedByLocalChanges, Details = merge.output, Commits = commits, ChangedFiles = files, Upstream = upstream };
        }

        private static void Finish(Result result, bool interactive, string root)
        {
            running = false;
            switch (result.Outcome)
            {
                case Outcome.UpToDate:
                    if (interactive) EditorUtility.DisplayDialog("Onyx", "Das Projekt ist auf dem neuesten Stand.", "OK");
                    break;

                case Outcome.Updated:
                    Debug.Log($"[Onyx] Projekt aus dem Repo aktualisiert:\n{result.Commits}");
                    AssetDatabase.Refresh();
                    if (NeedsRebuild(result.ChangedFiles))
                    {
                        EditorUtility.DisplayDialog("Onyx – Update geladen",
                            $"Neue Version geladen:\n\n{Shorten(result.Commits)}\n\n" +
                            "Spielfigur oder Szenenaufbau haben sich geändert. Bitte einmal\n" +
                            "Onyx → Setup → M1-Absturzstelle erstellen ausführen.", "OK");
                    }
                    else if (interactive)
                    {
                        EditorUtility.DisplayDialog("Onyx – Update geladen", $"Neue Version geladen:\n\n{Shorten(result.Commits)}", "OK");
                    }

                    break;

                case Outcome.LocalCommitsAhead:
                    string message = "Du hast eigene Commits, die noch nicht auf GitHub sind. Bitte selbst mit\n" +
                                     "git pull und git push zusammenführen.";
                    if (interactive) EditorUtility.DisplayDialog("Onyx", message, "OK");
                    else Debug.LogWarning($"[Onyx] Automatisches Aktualisieren übersprungen: {message}");
                    break;

                case Outcome.BlockedByLocalChanges:
                    AskToDiscard(result, root);
                    break;

                default:
                    if (interactive) EditorUtility.DisplayDialog("Onyx", $"Aktualisieren fehlgeschlagen:\n{result.Details}", "OK");
                    else Debug.LogWarning($"[Onyx] Automatisches Aktualisieren fehlgeschlagen:\n{result.Details}");
                    break;
            }
        }

        private static void AskToDiscard(Result result, string root)
        {
            string upstream = result.Upstream;
            bool discard = EditorUtility.DisplayDialog(
                "Onyx – Update verfügbar",
                "Auf GitHub gibt es eine neue Version:\n\n" + Shorten(result.Commits) +
                "\n\nLokale Änderungen an denselben Dateien stehen im Weg:\n\n" + Shorten(result.Details) +
                "\n\nSollen deine lokalen Änderungen verworfen und das Update geladen werden?\n" +
                "(Eigene Arbeit vorher committen, wenn du sie behalten willst!)",
                "Verwerfen und aktualisieren",
                "Später");
            if (!discard)
            {
                Debug.LogWarning("[Onyx] Update aufgeschoben – lokale Änderungen stehen im Weg. Menü Onyx → Git → Jetzt aktualisieren.");
                return;
            }

            var reset = Git(root, $"reset --hard {upstream}");
            if (reset.code == 0)
            {
                Debug.Log($"[Onyx] Lokale Änderungen verworfen, Projekt auf {upstream} gesetzt.");
                AssetDatabase.Refresh();
                if (NeedsRebuild(result.ChangedFiles))
                {
                    EditorUtility.DisplayDialog("Onyx – Update geladen",
                        "Spielfigur oder Szenenaufbau haben sich geändert. Bitte einmal\n" +
                        "Onyx → Setup → M1-Absturzstelle erstellen ausführen.", "OK");
                }
            }
            else
            {
                EditorUtility.DisplayDialog("Onyx", $"Zurücksetzen fehlgeschlagen:\n{reset.output}", "OK");
            }
        }

        private static bool NeedsRebuild(string changedFiles)
        {
            foreach (string line in changedFiles.Split('\n'))
            {
                string f = line.Trim();
                if (f.Contains("Scripts/Editor/M0SceneBuilder") || f.Contains("Scripts/Editor/M1SceneBuilder"))
                {
                    return true;
                }
            }

            return false;
        }

        private static string Shorten(string text, int maxLines = 12)
        {
            var lines = text.Split('\n');
            if (lines.Length <= maxLines) return text;
            var b = new StringBuilder();
            for (int i = 0; i < maxLines; i++) b.AppendLine(lines[i]);
            b.Append($"… und {lines.Length - maxLines} weitere");
            return b.ToString();
        }

        private static (int code, string output) Git(string root, string arguments)
        {
            var info = new ProcessStartInfo("git", arguments)
            {
                WorkingDirectory = root,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
            };

            // Nie auf eine Passwortabfrage im unsichtbaren Fenster warten.
            info.EnvironmentVariables["GIT_TERMINAL_PROMPT"] = "0";

            using (var process = new Process { StartInfo = info })
            {
                try
                {
                    process.Start();
                }
                catch (Exception e)
                {
                    return (-1, $"Git konnte nicht gestartet werden ({e.Message}). Ist Git installiert und im PATH?");
                }

                var stdout = process.StandardOutput.ReadToEndAsync();
                var stderr = process.StandardError.ReadToEndAsync();
                if (!process.WaitForExit(GitTimeoutMs))
                {
                    try { process.Kill(); } catch { /* schon beendet */ }
                    return (-1, $"git {arguments}: Zeitüberschreitung.");
                }

                return (process.ExitCode, (stdout.Result + stderr.Result).Trim());
            }
        }
    }
}
