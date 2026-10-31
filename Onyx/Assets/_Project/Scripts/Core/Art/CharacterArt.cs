namespace Onyx.Core.Art
{
    public enum Facing
    {
        Down,
        Up,
        Right,
        Left,
    }

    /// <summary>
    /// Spielfigur 16 × 32 px im Stil der Rollen-Entwürfe: Raumanzug mit großem Helm, Visier in der
    /// Rollenfarbe. Vier Blickrichtungen (links = rechts gespiegelt) und vier Laufbilder
    /// (0 Stand, 1 linker Schritt, 2 Stand, 3 rechter Schritt). Die Füße stehen auf Pixelzeile
    /// <see cref="FeetRow"/> (von unten), dort liegt der Drehpunkt.
    /// </summary>
    public static class CharacterArt
    {
        public const int Width = 16;
        public const int Height = 32;
        public const int FeetRow = 3;
        public const int Frames = 4;

        // Kopf: 12 breit, 10 hoch, bei x = 2, oberste Zeile 5 von oben
        private static readonly string[] HeadDown =
        {
            "...kkkkkk...",
            "..kHHHHHHk..",
            ".kHHwHHHHhk.",
            ".kHHHHHHHhk.",
            "kHkkkkkkkkhk",
            "kHkVVVVVVkhk",
            "kHkVvVVVVkhk",
            "khkVVVVVVkhk",
            "khkkkkkkkkhk",
            ".khhhhhhhhk.",
        };

        private static readonly string[] HeadUp =
        {
            "...kkkkkk...",
            "..kHHHHHHk..",
            ".kHHwHHHHhk.",
            ".kHHHHHHHhk.",
            "kHHHHHHHHhhk",
            "kHHHHHHHHhhk",
            "kHHHHHHHhhhk",
            "khHHHHHHhhhk",
            "khhhhhhhhhhk",
            ".khhhhhhhhk.",
        };

        private static readonly string[] HeadRight =
        {
            "...kkkkkk...",
            "..kHHHHHHk..",
            ".kHHHHHwHHk.",
            ".kHHHHHHHHk.",
            "khHHHHkkkkkk",
            "khHHHkVVVVVk",
            "khHHHkVvVVVk",
            "khhHHkVVVVVk",
            "khhhhkkkkkkk",
            ".khhhhhhhhk.",
        };

        // Rumpf: 10 breit, 8 hoch, bei x = 3, Zeilen 15–22
        private static readonly string[] TorsoDown =
        {
            "..kddddk..",
            ".kBBBBBBk.",
            "kBBBRRBBBk",
            "kBBBRRBBBk",
            "kbBBBBBBbk",
            "kbbbbbbbbk",
            "kddddddddk",
            ".kkkkkkkk.",
        };

        private static readonly string[] TorsoUp =
        {
            "..kddddk..",
            ".kBBBBBBk.",
            "kBkPPPPkBk",
            "kBkPAPPkBk",
            "kbkPPPPkbk",
            "kbkppppkbk",
            "kddddddddk",
            ".kkkkkkkk.",
        };

        private static readonly string[] TorsoRight =
        {
            "...kdddk..",
            "..kBBBBBk.",
            ".kPkBBBBBk",
            ".kAkBBBBRk",
            ".kPkBBBBBk",
            ".kpkbBBBbk",
            ".kkkdddddk",
            "...kkkkkk.",
        };

        // Arm von der Seite: 2 breit, liegt vor dem Rumpf
        private static readonly string[] ArmSide =
        {
            "Bk",
            "Bk",
            "bk",
            "bk",
            "Gk",
            "kk",
        };

        // Arm: 3 breit, 6 hoch
        private static readonly string[] Arm =
        {
            "kBk",
            "kBk",
            "kbk",
            "kbk",
            "kGk",
            "kkk",
        };

        // Beine: 8 breit, 6 hoch, bei x = 4, Zeilen 23–28
        private static readonly string[][] LegsFront =
        {
            new[] { "kLLkkLLk", "kLlkkLlk", "kLlkkLlk", "kFFkkFFk", "kFFkkFFk", "kkkkkkkk" },
            new[] { "kLLkkLLk", "kLlkkLlk", "kFFkkLlk", "kFFkkFFk", "kkkkkFFk", "....kkkk" },
            new[] { "kLLkkLLk", "kLlkkLlk", "kLlkkLlk", "kFFkkFFk", "kFFkkFFk", "kkkkkkkk" },
            new[] { "kLLkkLLk", "kLlkkLlk", "kLlkkFFk", "kFFkkFFk", "kFFkkkkk", "kkkk...." },
        };

        private static readonly string[][] LegsSide =
        {
            new[] { "...kLLk.", "...kLlk.", "...kLlk.", "...kLlk.", "...kFFFk", "...kkkkk" },
            new[] { ".kLLLLk.", ".kLlkLlk", "kLlk.kLk", "kLk..kFk", "kFFk.kFF", "kkkk.kkk" },
            new[] { "...kLLk.", "...kLlk.", "...kLlk.", "...kLlk.", "...kFFFk", "...kkkkk" },
            new[] { ".kLLLLk.", "kLlkLlk.", "kLk.kLlk", "kFk..kLk", "kFF.kFFk", "kkk.kkkk" },
        };

        public static PixelImage Render(Facing facing, int frame, uint roleColor)
        {
            frame = ((frame % Frames) + Frames) % Frames;
            var img = new PixelImage(Width, Height);
            uint roleLight = Lighten(roleColor);
            uint? Palette(char c)
            {
                switch (c)
                {
                    case 'k': return Rust.S0;
                    case 'H': return Rust.S5;
                    case 'h': return Rust.S4;
                    case 'w': return Rust.Bone;
                    case 'V': return roleColor;
                    case 'v': return roleLight;
                    case 'R': return roleColor;
                    case 'B': return Rust.S4;
                    case 'b': return Rust.S3;
                    case 'd': return Rust.S2;
                    case 'P': return Rust.S3;
                    case 'p': return Rust.S2;
                    case 'A': return roleColor;
                    case 'G': return Rust.S5;
                    case 'L': return Rust.S3;
                    case 'l': return Rust.S2;
                    case 'F': return Rust.S2;
                    default: return null;
                }
            }

            bool side = facing == Facing.Right || facing == Facing.Left;
            bool flip = facing == Facing.Left;
            int swing = frame == 1 ? 1 : frame == 3 ? -1 : 0;

            // Von hinten nach vorn zeichnen. y-Werte = untere Kante der Teile (Zeile 0 = unten).
            int legsY = Height - 29;   // Zeilen 23–28 von oben
            int torsoY = Height - 23;  // Zeilen 15–22
            int headY = Height - 15;   // Zeilen 5–14

            if (side)
            {
                // Hinterer Arm (abgedunkelt durch Rumpf verdeckt), Beine, Rumpf, Kopf, vorderer Arm
                StampFlip(img, LegsSide[frame], 4, legsY, Palette, flip);
                StampFlip(img, TorsoRight, 3, torsoY, Palette, flip);
                StampFlip(img, HeadRight, 2, headY, Palette, flip);
                StampFlip(img, ArmSide, 9 - swing, torsoY + 1, Palette, flip);
            }
            else
            {
                var torso = facing == Facing.Up ? TorsoUp : TorsoDown;
                var head = facing == Facing.Up ? HeadUp : HeadDown;
                img.Stamp(LegsFront[frame], 4, legsY, Palette);
                img.Stamp(Arm, 1, torsoY + 1 + swing, Palette);
                img.Stamp(Arm, 12, torsoY + 1 - swing, Palette);
                img.Stamp(torso, 3, torsoY, Palette);
                img.Stamp(head, 2, headY, Palette);
            }

            return img;
        }

        /// <summary>Spiegelt um die Bildmitte (16 px), damit links und rechts deckungsgleich sind.</summary>
        private static void StampFlip(PixelImage img, string[] rows, int x, int y, System.Func<char, uint?> palette, bool flip)
        {
            int width = rows[0].Length;
            int px = flip ? Width - x - width : x;
            img.Stamp(rows, px, y, palette, flip);
        }

        private static uint Lighten(uint c)
        {
            uint r = (c >> 16) & 0xFF, g = (c >> 8) & 0xFF, b = c & 0xFF;
            r += (255 - r) / 2;
            g += (255 - g) / 2;
            b += (255 - b) / 2;
            return 0xFF000000 | (r << 16) | (g << 8) | b;
        }
    }
}
