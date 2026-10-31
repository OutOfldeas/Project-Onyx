using Onyx.Core.CrashSite;
using Onyx.Game.CrashSite;
using Onyx.Game.Player;
using Onyx.Game.Session;
using UnityEngine;

namespace Onyx.Game.UI
{
    /// <summary>
    /// Anzeige während des Spiels an der Absturzstelle: Sauerstoff, Schild, Gesundheit, Umgebung,
    /// Reparaturstand der Fregatte, Tageszeit, Interaktions-Hinweis und Meldungen.
    /// </summary>
    public sealed class GameHud : MonoBehaviour
    {
        private const float MessageSeconds = 4.5f;
        private const float HintSeconds = 40f;

        private PixelGui gui;
        private string message;
        private Color messageColor;
        private float messageUntil;
        private float shownSince = -1f;

        private void OnEnable()
        {
            CrashSiteNet.StationRepaired += OnStationRepaired;
        }

        private void OnDisable()
        {
            CrashSiteNet.StationRepaired -= OnStationRepaired;
        }

        private void OnStationRepaired(RepairPoint point)
        {
            var site = CrashSiteNet.ActiveSite;
            if (site != null && site.AllRepaired)
            {
                Show("Die Fregatte ist startklar!  (Sternenkarte: M)", PixelGui.Lime);
                return;
            }

            switch (point.Kind)
            {
                case StationKind.OxygenGenerator:
                    Show("Sauerstoffgenerator läuft: Im Wrack gibt es wieder Luft.", PixelGui.Cyan);
                    break;
                case StationKind.Reactor:
                    Show("Reaktor läuft: Strom und Licht im Wrack. Jetzt der Antrieb!", PixelGui.Amber);
                    break;
                default:
                    Show($"{point.Name} repariert.", PixelGui.Amber);
                    break;
            }
        }

        private void Show(string text, Color color)
        {
            message = text;
            messageColor = color;
            messageUntil = Time.unscaledTime + MessageSeconds;
        }

        private void OnGUI()
        {
            var vitals = PlayerVitals.Local;
            var site = CrashSiteNet.ActiveSite;
            if (vitals == null || site == null || StarMapScreen.IsOpen || GameSession.Instance == null)
            {
                shownSince = -1f;
                return;
            }

            if (shownSince < 0f)
            {
                shownSince = Time.unscaledTime;
            }

            gui ??= new PixelGui();
            gui.Begin();

            DrawVitals(vitals);
            DrawClock();
            DrawFrigate(site);
            DrawPrompt();
            if (vitals.IsDowned)
            {
                DrawDowned(vitals);
            }

            if (message != null && Time.unscaledTime < messageUntil)
            {
                gui.Text(PixelGui.Width / 2f, 62, message, messageColor, TextAnchor.UpperCenter);
            }

            var saves = SaveSystem.Instance;
            if (saves != null && Time.unscaledTime - saves.LastSavedAt < 2.5f)
            {
                gui.Text(PixelGui.Width - 8, PixelGui.Height - 12, "Gespeichert", PixelGui.Steel2, TextAnchor.UpperRight);
            }

            if (Time.unscaledTime - shownSince < HintSeconds)
            {
                gui.Text(8, PixelGui.Height - 12, "WASD laufen · E halten: reparieren · M Karte · F1 Dev", PixelGui.Steel2);
            }
        }

        private void DrawVitals(PlayerVitals vitals)
        {
            const float X = 6;
            const float Y = 6;
            gui.Panel(X, Y, 140, 50, PixelGui.Steel1);

            gui.Text(X + 5, Y + 4, "O2", PixelGui.Cyan);
            gui.Bar(X + 46, Y + 5, 88, 6, vitals.Oxygen, PixelGui.Cyan, vitals.Oxygen < 0.25f);
            gui.Text(X + 5, Y + 14, "Schild", PixelGui.Violet);
            gui.Bar(X + 46, Y + 15, 88, 6, vitals.Shield, PixelGui.Violet);
            gui.Text(X + 5, Y + 24, "Leben", PixelGui.Lime);
            gui.Bar(X + 46, Y + 25, 88, 6, vitals.Health, PixelGui.Lime, vitals.Health < 0.3f);

            var (text, color) = DescribeEnvironment(vitals.Environment);
            gui.Text(X + 5, Y + 37, text, color);
        }

        private static (string, Color) DescribeEnvironment(VitalsEnvironment environment)
        {
            switch (environment)
            {
                case VitalsEnvironment.PodRefill: return ("Kapsel: O2 tankt", PixelGui.Cyan);
                case VitalsEnvironment.WreckAir: return ("Wrack: Luft ok", PixelGui.Cyan);
                case VitalsEnvironment.NoAir: return ("Keine Luft: O2 sinkt", PixelGui.Amber);
                case VitalsEnvironment.Cold: return ("Kälte: Schild sinkt", PixelGui.Red);
                default: return ("Sicher", PixelGui.Steel3);
            }
        }

        private void DrawClock()
        {
            var clock = GameSession.Instance.World.Clock;
            float hours = clock.TimeOfDay * 24f;
            int h = Mathf.FloorToInt(hours) % 24;
            int m = Mathf.FloorToInt((hours - Mathf.Floor(hours)) * 60f);
            bool night = CrashSiteState.IsNight(clock.Daylight);
            gui.Text(PixelGui.Width / 2f, 6, $"Tag {clock.DayCount + 1} · {h:00}:{m:00}", night ? PixelGui.Violet : PixelGui.Steel3, TextAnchor.UpperCenter);
        }

        private void DrawFrigate(CrashSiteState site)
        {
            const float W = 136;
            float x = PixelGui.Width - 6 - W;
            const float Y = 6;
            int count = site.Layout.RepairPoints.Count;
            gui.Panel(x, Y, W, 18 + count * 10 + 10, site.AllRepaired ? PixelGui.Lime : PixelGui.Amber);
            gui.Text(x + 5, Y + 4, "Fregatte", PixelGui.Amber);

            float y = Y + 15;
            foreach (var point in site.Layout.RepairPoints)
            {
                var job = site.Job(point.Index);
                string state;
                Color color;
                if (job.IsComplete)
                {
                    state = "läuft";
                    color = PixelGui.Cyan;
                }
                else if (site.CanWorkOn(point.Index) == JoinResult.MissingRequirement)
                {
                    state = "ohne Strom";
                    color = PixelGui.Steel2;
                }
                else
                {
                    state = job.Progress > 0f ? $"{job.Ratio * 100f:0}%" : "kaputt";
                    color = PixelGui.Red;
                }

                gui.Text(x + 5, y, ShortName(point.Kind), PixelGui.Steel3);
                gui.Text(x + W - 5, y, state, color, TextAnchor.UpperRight);
                y += 10;
            }

            gui.Text(x + 5, y + 1, site.AllRepaired ? "Startklar!" : $"{site.RepairedCount}/{count} Systeme laufen", site.AllRepaired ? PixelGui.Lime : PixelGui.Steel2);
        }

        private static string ShortName(StationKind kind)
        {
            switch (kind)
            {
                case StationKind.OxygenGenerator: return "O2-Generator";
                case StationKind.Reactor: return "Reaktor";
                default: return "Antrieb";
            }
        }

        private void DrawPrompt()
        {
            var interaction = PlayerInteraction.Local;
            if (interaction == null || interaction.Prompt == null)
            {
                return;
            }

            const float Y = 214;
            gui.Text(PixelGui.Width / 2f, Y, interaction.Prompt, interaction.PromptBlocked ? PixelGui.Red : PixelGui.Bone, TextAnchor.UpperCenter);
            if (interaction.PromptProgress >= 0f)
            {
                gui.Bar(PixelGui.Width / 2f - 60, Y + 11, 120, 6, interaction.PromptProgress, PixelGui.Amber);
            }
        }

        private void DrawDowned(PlayerVitals vitals)
        {
            gui.Fill(0, 100, PixelGui.Width, 44, PixelGui.Void0, 0.75f);
            gui.Text(PixelGui.Width / 2f, 106, "Kampfunfähig", PixelGui.Red, TextAnchor.UpperCenter);
            bool alone = PlayerVitals.All.Count <= 1;
            string help = alone ? "Die Rettungsautomatik der Kapsel holt dich gleich zurück …" : "Ein Mitspieler kann dich mit E wiederbeleben.";
            gui.Text(PixelGui.Width / 2f, 118, help, PixelGui.Steel3, TextAnchor.UpperCenter);
            if (vitals.ReviveProgress > 0f)
            {
                gui.Bar(PixelGui.Width / 2f - 60, 130, 120, 6, vitals.ReviveProgress, PixelGui.Lime);
            }
        }
    }
}
