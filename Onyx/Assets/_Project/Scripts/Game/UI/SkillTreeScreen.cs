using System.Collections.Generic;
using System.Linq;
using Onyx.Core.Skills;
using Onyx.Game.DevTools;
using Onyx.Game.Session;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Onyx.Game.UI
{
    /// <summary>
    /// Der Skilltree (Taste K) im Pixel-Stil: vier Disziplinen nebeneinander mit Stufe, Erfahrung
    /// und freien Punkten, darunter ihre Fähigkeiten mit Verbindungslinien zu den Voraussetzungen –
    /// auch quer zwischen den Bäumen (Hybride). Klick schaltet frei, Klick auf die Umlern-Einstellung
    /// wechselt zwischen Steigern, Senken und Sperren.
    /// </summary>
    public sealed class SkillTreeScreen : MonoBehaviour
    {
        public static bool IsOpen { get; private set; }

        public static void Close() => IsOpen = false;

        private const float ColumnX = 8;
        private const float ColumnPitch = 117;
        private const float BoxWidth = 114;
        private const float BoxHeight = 24;
        private const float NodesTop = 58;
        private const float RowPitch = 29;
        private const float InfoTop = 236;

        private PixelGui gui;
        private string message;
        private float messageUntil;

        private CharacterSkills layoutFor;
        private readonly Dictionary<string, Rect> boxes = new Dictionary<string, Rect>();

        public static Color ColorOf(Discipline d)
        {
            switch (d)
            {
                case Discipline.Exploration: return PixelGui.Amber;
                case Discipline.Engineering: return PixelGui.Cyan;
                case Discipline.Diplomacy: return PixelGui.Violet;
                default: return PixelGui.Lime;
            }
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            if (!DevMode.BlocksGameplayInput && keyboard.kKey.wasPressedThisFrame)
            {
                IsOpen = !IsOpen;
                if (IsOpen) StarMapScreen.Close();
            }
            else if (IsOpen && keyboard.escapeKey.wasPressedThisFrame)
            {
                IsOpen = false;
            }
        }

        private void OnDisable()
        {
            IsOpen = false;
        }

        private void OnGUI()
        {
            if (!IsOpen || GameSession.Instance == null)
            {
                return;
            }

            var skills = GameSession.Instance.Character;
            if (layoutFor != skills)
            {
                BuildLayout(skills);
            }

            gui ??= new PixelGui();
            gui.Begin();
            gui.Fill(-1000, -1000, 3000, 3000, PixelGui.Void0, 0.93f);

            gui.Text(8, 6, "Skills · Lernen durch Tun", PixelGui.Amber);
            gui.Text(PixelGui.Width - 8, 6, $"Gesamtstufe {skills.TotalLevel}/{CharacterSkills.TotalLevelCap}", PixelGui.Bone, TextAnchor.UpperRight);

            Vector2 mouse = gui.Mouse;
            bool click = Event.current.type == EventType.MouseDown && Event.current.button == 0;

            for (int i = 0; i < DisciplineNames.All.Length; i++)
            {
                DrawDisciplineHeader(skills, DisciplineNames.All[i], ColumnX + i * ColumnPitch, mouse, click);
            }

            DrawPrerequisiteLines(skills);

            SkillNode hovered = null;
            foreach (var node in skills.Tree.Nodes)
            {
                Rect r = boxes[node.Id];
                bool hover = PixelGui.Contains(r.x, r.y, r.width, r.height, mouse);
                if (hover) hovered = node;
                DrawNode(skills, node, r, hover);

                if (hover && click)
                {
                    TryUnlock(skills, node);
                    Event.current.Use();
                }
            }

            DrawInfo(skills, hovered);
        }

        private void BuildLayout(CharacterSkills skills)
        {
            layoutFor = skills;
            boxes.Clear();
            for (int i = 0; i < DisciplineNames.All.Length; i++)
            {
                var d = DisciplineNames.All[i];
                var column = skills.Tree.Nodes
                    .Where(n => n.Discipline == d)
                    .OrderBy(n => n.RequiredLevel)
                    .ThenBy(n => n.IsHybrid)
                    .ThenBy(n => n.Name)
                    .ToList();
                for (int row = 0; row < column.Count; row++)
                {
                    boxes[column[row].Id] = new Rect(ColumnX + i * ColumnPitch, NodesTop + row * RowPitch, BoxWidth, BoxHeight);
                }
            }
        }

        private void DrawDisciplineHeader(CharacterSkills skills, Discipline d, float x, Vector2 mouse, bool click)
        {
            Color color = ColorOf(d);
            int level = skills.Level(d);
            gui.Text(x, 20, DisciplineNames.German(d), color);

            float ratio = level >= CharacterSkills.MaxLevel ? 1f : skills.Experience(d) / CharacterSkills.ExperienceToNext(level);
            gui.Bar(x, 30, BoxWidth, 4, ratio, color);

            int points = skills.AvailablePoints(d);
            gui.Text(x, 37, $"Stufe {level}", PixelGui.Bone);
            gui.Text(x + BoxWidth, 37, points > 0 ? $"{points} Punkt{(points == 1 ? "" : "e")} frei" : "keine Punkte", points > 0 ? PixelGui.Amber : PixelGui.Steel2, TextAnchor.UpperRight);

            string mode = LockLabel(skills.GetLock(d));
            float width = gui.TextWidth(mode) + 6;
            bool hover = PixelGui.Contains(x, 46, width, 9, mouse);
            gui.Fill(x, 46, width, 9, hover ? PixelGui.Steel1 : PixelGui.Void1, 0.9f);
            gui.Text(x + 3, 47, mode, skills.GetLock(d) == SkillLock.Raise ? PixelGui.Steel3 : PixelGui.Amber);
            if (hover && click)
            {
                skills.SetLock(d, NextLock(skills.GetLock(d)));
                SaveSystem.Instance?.RequestCharacterSave();
                Event.current.Use();
            }
        }

        private static string LockLabel(SkillLock mode)
        {
            switch (mode)
            {
                case SkillLock.Lower: return "Senken";
                case SkillLock.Locked: return "Gesperrt";
                default: return "Steigern";
            }
        }

        private static SkillLock NextLock(SkillLock mode)
        {
            switch (mode)
            {
                case SkillLock.Raise: return SkillLock.Lower;
                case SkillLock.Lower: return SkillLock.Locked;
                default: return SkillLock.Raise;
            }
        }

        private void DrawPrerequisiteLines(CharacterSkills skills)
        {
            foreach (var node in skills.Tree.Nodes)
            {
                Rect to = boxes[node.Id];
                foreach (string pre in node.Prerequisites)
                {
                    if (!boxes.TryGetValue(pre, out Rect from)) continue;
                    bool met = skills.IsUnlocked(pre);
                    var preNode = skills.Tree.Get(pre);
                    bool crossTree = preNode.Discipline != node.Discipline;
                    Color color = met ? ColorOf(preNode.Discipline) : PixelGui.Steel1;

                    if (crossTree)
                    {
                        // Querverbindung: von der Seite des einen Kastens zur Seite des anderen
                        bool leftToRight = from.x < to.x;
                        float x0 = leftToRight ? from.xMax : from.x - 1;
                        float x1 = leftToRight ? to.x - 1 : to.xMax;
                        gui.Line(x0, from.y + from.height / 2, x1, to.y + to.height / 2, color, !met);
                    }
                    else
                    {
                        float x = from.x + 6;
                        gui.Line(x, from.yMax, x, to.y - 1, color, !met);
                    }
                }
            }
        }

        private void DrawNode(CharacterSkills skills, SkillNode node, Rect r, bool hover)
        {
            Color color = ColorOf(node.Discipline);
            bool unlocked = skills.IsUnlocked(node.Id);
            bool active = skills.IsActive(node.Id);
            UnlockCheck check = skills.CanUnlock(node.Id);

            Color border = unlocked ? color : check == UnlockCheck.Ok ? PixelGui.Amber : PixelGui.Steel1;
            gui.Fill(r.x, r.y, r.width, r.height, hover ? PixelGui.Steel1 : PixelGui.Void1, unlocked ? 1f : 0.85f);
            gui.Frame(r.x, r.y, r.width, r.height, border);
            if (unlocked) gui.Fill(r.x + 1, r.y + 1, 2, r.height - 2, color);

            gui.Text(r.x + 5, r.y + 3, node.Name, unlocked ? PixelGui.Bone : check == UnlockCheck.Ok ? PixelGui.Bone : PixelGui.Steel2);

            string detail = unlocked ? (active ? "aktiv" : "inaktiv") : $"Stufe {node.RequiredLevel} · {node.Cost} P";
            gui.Text(r.x + 5, r.y + 13, detail, unlocked && active ? color : PixelGui.Steel2);
            if (node.IsHybrid)
            {
                // Hybrid: Voraussetzungen aus zwei Bäumen
                gui.Text(r.xMax - 4, r.y + 13, "Hybrid", PixelGui.Violet, TextAnchor.UpperRight);
            }
        }

        private void TryUnlock(CharacterSkills skills, SkillNode node)
        {
            var result = skills.Unlock(node.Id);
            if (result == UnlockCheck.Ok)
            {
                SaveSystem.Instance?.RequestCharacterSave();
                Flash($"Freigeschaltet: {node.Name}");
            }
            else
            {
                Flash(Reason(skills, node, result));
            }
        }

        private static string Reason(CharacterSkills skills, SkillNode node, UnlockCheck result)
        {
            switch (result)
            {
                case UnlockCheck.AlreadyUnlocked: return $"{node.Name} ist schon freigeschaltet.";
                case UnlockCheck.LevelTooLow: return $"Braucht {DisciplineNames.German(node.Discipline)} Stufe {node.RequiredLevel}.";
                case UnlockCheck.MissingPrerequisite:
                    var missing = node.Prerequisites.Where(p => !skills.IsUnlocked(p)).Select(p => skills.Tree.Get(p).Name);
                    return $"Braucht zuerst: {string.Join(", ", missing)}.";
                case UnlockCheck.NotEnoughPoints: return $"Zu wenig Punkte in {DisciplineNames.German(node.Discipline)}.";
                default: return result.ToString();
            }
        }

        private void DrawInfo(CharacterSkills skills, SkillNode hovered)
        {
            gui.Panel(8, InfoTop, PixelGui.Width - 16, 30, PixelGui.Steel1);
            float right = PixelGui.Width - 14;

            if (hovered != null)
            {
                gui.Text(14, InfoTop + 5, hovered.Name, ColorOf(hovered.Discipline));
                if (!skills.IsUnlocked(hovered.Id))
                {
                    var check = skills.CanUnlock(hovered.Id);
                    gui.Text(right, InfoTop + 5, check == UnlockCheck.Ok ? "Klick: freischalten" : Reason(skills, hovered, check),
                        check == UnlockCheck.Ok ? PixelGui.Amber : PixelGui.Steel2, TextAnchor.UpperRight);
                }

                gui.Text(14, InfoTop + 17, hovered.Description, PixelGui.Bone);
            }
            else
            {
                gui.Text(14, InfoTop + 5, "Reparieren bringt Technik, Wege draußen bringen Erkundung.", PixelGui.Steel3);
                gui.Text(right, InfoTop + 5, "K / Esc: schließen", PixelGui.Steel2, TextAnchor.UpperRight);
                gui.Text(14, InfoTop + 17, "Umlernen: Klick auf Steigern / Senken / Gesperrt unter einer Disziplin.", PixelGui.Steel2);
            }

            if (message != null && Time.unscaledTime < messageUntil)
            {
                gui.Fill(8, InfoTop - 12, PixelGui.Width - 16, 11, PixelGui.Void0, 0.9f);
                gui.Text(PixelGui.Width / 2f, InfoTop - 11, message, PixelGui.Amber, TextAnchor.UpperCenter);
            }
        }

        private void Flash(string text)
        {
            message = text;
            messageUntil = Time.unscaledTime + 3f;
        }
    }
}
