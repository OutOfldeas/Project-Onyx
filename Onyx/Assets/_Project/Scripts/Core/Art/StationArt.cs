using Onyx.Core.CrashSite;

namespace Onyx.Core.Art
{
    /// <summary>
    /// Die drei Reparaturstellen der Fregatte als Pixelbild (16 × 24, Fuß auf Zeile <see cref="BaseRow"/>):
    /// Sauerstoffgenerator mit Tank, Reaktor mit Kern, Antrieb mit Düse. Kaputt: dunkel, rote Anzeige,
    /// Funken. Repariert: leuchtet in der Systemfarbe.
    /// </summary>
    public static class StationArt
    {
        public const int Width = 16;
        public const int Height = 24;
        public const int BaseRow = 2;

        private static readonly string[] Oxygen =
        {
            "................",
            "................",
            ".....kkkkkk.....",
            "....kGGGGGGk....",
            "...kGgGGGGGGk...",
            "...kGgGGGGGGk...",
            "...kGGGGGGGGk...",
            "...kGGGGGGGGk...",
            "...kkkkkkkkkk...",
            "...kSSSSSSSSk...",
            ".kkkShSSSSSSkkk.",
            ".kpkSSSSSSSSkpk.",
            ".kpkSkLLLLkSkpk.",
            ".kpkSkLlLLkSkpk.",
            ".kpkSkLLLLkSkpk.",
            ".kpkSSSSSSSSkpk.",
            ".kpkSSIISSSSkpk.",
            ".kkkSSSSSSSSkkk.",
            "...kddddddddk...",
            "..kddddddddddk..",
            "..kkkkkkkkkkkk..",
            "..xxxxxxxxxxxx..",
            "................",
            "................",
        };

        private static readonly string[] Reactor =
        {
            "................",
            "................",
            "................",
            "....kkkkkkkk....",
            "...kSSSSSSSSk...",
            "..kShSSSSSSSSk..",
            "..kSkkkkkkkkSk..",
            "..kSkGGGGGGkSk..",
            "..kSkGggggGkSk..",
            "..kSkGgWWgGkSk..",
            "..kSkGgWWgGkSk..",
            "..kSkGggggGkSk..",
            "..kSkGGGGGGkSk..",
            "..kSkkkkkkkkSk..",
            "..kSSSSSSSSSSk..",
            "..kddddddddddk..",
            "..kdLLddddIIdk..",
            "..kdlLddddIIdk..",
            "..kddddddddddk..",
            "..kkkkkkkkkkkk..",
            "..xxxxxxxxxxxx..",
            "................",
            "................",
            "................",
        };

        private static readonly string[] Engine =
        {
            "................",
            "................",
            "..kkkkkkkkkkkk..",
            ".kSSSSSSSSSSSSk.",
            ".kShhhhhhhhhhSk.",
            ".kSkkkkkkkkkkSk.",
            ".kSkGGGGGGGGkSk.",
            ".kSkGggggggGkSk.",
            ".kSkGgWWWWgGkSk.",
            ".kSkGggggggGkSk.",
            ".kSkGGGGGGGGkSk.",
            ".kSkkkkkkkkkkSk.",
            ".kSSSSSSSSSSSSk.",
            "..kdkdkdkdkdkk..",
            "..kdkdkdkdkdkk..",
            "..kSSSSSSSSSSk..",
            "..kSLLSSSSIISk..",
            "..kSLlSSSSIISk..",
            "..kddddddddddk..",
            "..kkkkkkkkkkkk..",
            "..xxxxxxxxxxxx..",
            "................",
            "................",
            "................",
        };

        public static PixelImage Render(StationKind kind, bool repaired)
        {
            var img = new PixelImage(Width, Height);
            uint glow = kind == StationKind.OxygenGenerator ? Rust.Cyan : kind == StationKind.Reactor ? Rust.Amber : Rust.Cyan;
            uint glowDark = kind == StationKind.Reactor ? Rust.R7 : Rust.CyanDark;

            uint? Palette(char c)
            {
                switch (c)
                {
                    case 'k': return Rust.S0;
                    case 'S': return Rust.S3;
                    case 'h': return Rust.S4;
                    case 'd': return Rust.S2;
                    case 'p': return Rust.S2;
                    case 'x': return 0x66000000; // weicher Schatten am Fuß
                    case 'G': return repaired ? glowDark : Rust.S1;
                    case 'g': return repaired ? glow : Rust.R2;
                    case 'W': return repaired ? Rust.Bone : Rust.R3;
                    case 'L': return repaired ? Rust.CyanDark : Rust.R4;
                    case 'l': return repaired ? Rust.Cyan : Rust.Red;
                    case 'I': return repaired ? Rust.Lime : Rust.R2;
                    default: return null;
                }
            }

            string[] art = kind == StationKind.OxygenGenerator ? Oxygen : kind == StationKind.Reactor ? Reactor : Engine;
            img.Stamp(art, 0, 0, Palette);

            if (!repaired)
            {
                // Funken und Brandflecken
                img.Set(4, 13, Rust.Amber);
                img.Set(5, 14, Rust.R8);
                img.Set(11, 17, Rust.Amber);
                img.Set(12, 18, Rust.R9);
            }

            return img;
        }
    }
}
