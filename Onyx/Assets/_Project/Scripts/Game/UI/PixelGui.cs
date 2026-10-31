using UnityEngine;

namespace Onyx.Game.UI
{
    /// <summary>
    /// Zeichenhilfe für Pixel-Oberflächen im Art-Direction-Stil: Man zeichnet auf einer virtuellen
    /// Fläche von 480 × 270 Pixeln, die ganzzahlig auf den Bildschirm skaliert wird – genau wie die
    /// Spielgrafik. Schrift: Silkscreen (8 px). Nur innerhalb von OnGUI benutzen.
    /// </summary>
    public sealed class PixelGui
    {
        public const int Width = 480;
        public const int Height = 270;

        public static readonly Color Void0 = new Color32(0x07, 0x08, 0x0F, 0xFF);
        public static readonly Color Void1 = new Color32(0x0F, 0x14, 0x24, 0xFF);
        public static readonly Color Steel1 = new Color32(0x4A, 0x55, 0x73, 0xFF);
        public static readonly Color Steel2 = new Color32(0x7A, 0x84, 0x9E, 0xFF);
        public static readonly Color Steel3 = new Color32(0xB4, 0xBC, 0xCC, 0xFF);
        public static readonly Color Bone = new Color32(0xE8, 0xE4, 0xD8, 0xFF);
        public static readonly Color Amber = new Color32(0xFF, 0xB5, 0x2E, 0xFF);
        public static readonly Color Red = new Color32(0xFF, 0x3D, 0x5A, 0xFF);
        public static readonly Color Cyan = new Color32(0x39, 0xD5, 0xE6, 0xFF);
        public static readonly Color Violet = new Color32(0xC7, 0x7D, 0xFF, 0xFF);
        public static readonly Color Lime = new Color32(0xA6, 0xD8, 0x4A, 0xFF);

        private readonly Font font;
        private Texture2D white;
        private GUIStyle style;
        private int styleScale;

        public int Scale { get; private set; } = 1;
        public float OriginX { get; private set; }
        public float OriginY { get; private set; }

        public PixelGui()
        {
            font = Resources.Load<Font>("Fonts/Silkscreen-Regular");
        }

        /// <summary>Zu Beginn jedes OnGUI aufrufen: berechnet Maßstab und Lage der virtuellen Fläche.</summary>
        public void Begin()
        {
            Scale = Mathf.Max(1, Mathf.FloorToInt(Mathf.Min(Screen.width / (float)Width, Screen.height / (float)Height)));
            OriginX = Mathf.Floor((Screen.width - Width * Scale) / 2f);
            OriginY = Mathf.Floor((Screen.height - Height * Scale) / 2f);

            if (white == null)
            {
                white = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
                white.SetPixel(0, 0, Color.white);
                white.Apply();
            }

            if (style == null || styleScale != Scale)
            {
                styleScale = Scale;
                style = new GUIStyle(GUI.skin.label)
                {
                    font = font,
                    fontSize = 8 * Scale,
                    padding = new RectOffset(0, 0, 0, 0),
                    margin = new RectOffset(0, 0, 0, 0),
                    wordWrap = false,
                    richText = false,
                    clipping = TextClipping.Overflow,
                    alignment = TextAnchor.UpperLeft,
                };
            }
        }

        public Rect ToScreen(float x, float y, float w, float h) => new Rect(OriginX + x * Scale, OriginY + y * Scale, w * Scale, h * Scale);

        /// <summary>Breite eines Texts in virtuellen Pixeln.</summary>
        public float TextWidth(string text) => style.CalcSize(new GUIContent(text)).x / Scale;

        public void Text(float x, float y, string text, Color color, TextAnchor anchor = TextAnchor.UpperLeft)
        {
            var content = new GUIContent(text);
            Vector2 size = style.CalcSize(content);
            float left = OriginX + x * Scale;
            if (anchor == TextAnchor.UpperRight) left -= size.x;
            else if (anchor == TextAnchor.UpperCenter) left -= Mathf.Round(size.x / 2f);
            float top = OriginY + y * Scale;

            style.normal.textColor = Void0; // harter Pixelschatten
            GUI.Label(new Rect(left + Scale, top + Scale, size.x, size.y), content, style);
            style.normal.textColor = color;
            GUI.Label(new Rect(left, top, size.x, size.y), content, style);
        }

        public void Fill(float x, float y, float w, float h, Color color, float alpha = 1f)
        {
            Color previous = GUI.color;
            color.a = alpha;
            GUI.color = color;
            GUI.DrawTexture(ToScreen(x, y, w, h), white);
            GUI.color = previous;
        }

        public void Frame(float x, float y, float w, float h, Color color)
        {
            Fill(x, y, w, 1, color);
            Fill(x, y + h - 1, w, 1, color);
            Fill(x, y, 1, h, color);
            Fill(x + w - 1, y, 1, h, color);
        }

        /// <summary>Balken mit Rahmen; <paramref name="ratio"/> 0–1. Bei niedrigem Wert blinkt er rot.</summary>
        public void Bar(float x, float y, float w, float h, float ratio, Color color, bool warn = false)
        {
            Fill(x, y, w, h, Void0, 0.85f);
            Frame(x, y, w, h, Steel1);
            float inner = Mathf.Round((w - 2) * Mathf.Clamp01(ratio));
            Color fill = warn && Mathf.PingPong(Time.unscaledTime * 3f, 1f) > 0.5f ? Red : color;
            if (inner > 0) Fill(x + 1, y + 1, inner, h - 2, fill);
        }

        /// <summary>Halbtransparentes Feld mit Rahmen (für Panels).</summary>
        public void Panel(float x, float y, float w, float h, Color border)
        {
            Fill(x, y, w, h, Void1, 0.88f);
            Frame(x, y, w, h, border);
        }
    }
}
