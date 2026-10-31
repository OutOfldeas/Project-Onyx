using System.Collections.Generic;
using Onyx.Core;
using Onyx.Core.Art;
using UnityEngine;

namespace Onyx.Game.Player
{
    /// <summary>
    /// Macht aus <see cref="CharacterArt"/> Unity-Sprites: je Rollenfarbe ein Bogen mit 4 Richtungen
    /// × 4 Laufbildern, einmal erzeugt und dann wiederverwendet.
    /// </summary>
    public static class CharacterSprites
    {
        private static readonly Dictionary<uint, Sprite[]> Cache = new Dictionary<uint, Sprite[]>();

        public static Sprite Get(Color roleColor, Facing facing, int frame)
        {
            Color32 c = roleColor;
            uint key = 0xFF000000u | ((uint)c.r << 16) | ((uint)c.g << 8) | c.b;
            if (!Cache.TryGetValue(key, out var sprites) || sprites[0] == null)
            {
                sprites = Build(key);
                Cache[key] = sprites;
            }

            return sprites[(int)facing * CharacterArt.Frames + ((frame % CharacterArt.Frames) + CharacterArt.Frames) % CharacterArt.Frames];
        }

        private static Sprite[] Build(uint roleColor)
        {
            const int count = 4 * CharacterArt.Frames;
            int w = CharacterArt.Width, h = CharacterArt.Height;
            var texture = new Texture2D(w * count, h, TextureFormat.RGBA32, false)
            {
                name = $"Figur {roleColor:X6}",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave,
            };

            var pixels = new Color32[w * count * h];
            for (int facing = 0; facing < 4; facing++)
            {
                for (int frame = 0; frame < CharacterArt.Frames; frame++)
                {
                    PixelImage image = CharacterArt.Render((Facing)facing, frame, roleColor);
                    int ox = (facing * CharacterArt.Frames + frame) * w;
                    for (int y = 0; y < h; y++)
                    {
                        for (int x = 0; x < w; x++)
                        {
                            uint p = image.Get(x, y);
                            pixels[y * w * count + ox + x] = new Color32((byte)(p >> 16), (byte)(p >> 8), (byte)p, (byte)(p >> 24));
                        }
                    }
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);

            var sprites = new Sprite[count];
            var pivot = new Vector2(0.5f, CharacterArt.FeetRow / (float)h);
            for (int i = 0; i < count; i++)
            {
                sprites[i] = Sprite.Create(texture, new Rect(i * w, 0, w, h), pivot, GameConstants.PixelsPerUnit);
                sprites[i].name = $"{texture.name} {(Facing)(i / CharacterArt.Frames)} {i % CharacterArt.Frames}";
            }

            return sprites;
        }
    }
}
