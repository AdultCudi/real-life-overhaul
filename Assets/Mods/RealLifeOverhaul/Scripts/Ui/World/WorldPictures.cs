using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The pictures the world's windows show: the game's own sprites (a car, a sofa, a box of goods) by a key made once a
    /// sprite, and squares of one colour for a row of colours (UiDraw.SwatchKey). Hooked into UiPictures.Find once a session
    /// before whoever answered before, which still answers every other key. Cost: a key a new sprite; a small texture a new
    /// colour.
    /// </summary>
    internal static class WorldPictures
    {
        private const string Key = "world:";

        private static readonly Dictionary<int, Sprite> Sprites = new Dictionary<int, Sprite>(64);
        private static readonly Dictionary<int, string> Keys = new Dictionary<int, string>(64);
        private static readonly Dictionary<int, Sprite> Squares = new Dictionary<int, Sprite>(32);
        private static Func<string, Sprite> _mine;

        /// <summary>A sprite of the game's as a picture key; null for none. The key stays the same for the same sprite.</summary>
        public static string Of(Sprite sprite)
        {
            if (sprite == null)
            {
                return null;
            }
            On();
            int id = sprite.GetInstanceID();
            Sprites[id] = sprite;
            if (!Keys.TryGetValue(id, out string key))
            {
                key = Key + id.ToString(CultureInfo.InvariantCulture);
                Keys[id] = key;
            }
            return key;
        }

        /// <summary>A colour as a swatch's picture key.</summary>
        public static string Of(Color colour)
        {
            On();
            Color32 c = colour;
            return UiDraw.SwatchKey((c.r << 16) | (c.g << 8) | c.b);
        }

        /// <summary>The sprites of a city that ended go with it, and the squares of colour made for it are let go.</summary>
        public static void Forget()
        {
            Sprites.Clear();
            Keys.Clear();
            foreach (Sprite sprite in Squares.Values)
            {
                if (sprite != null)
                {
                    UnityEngine.Object.Destroy(sprite.texture);
                    UnityEngine.Object.Destroy(sprite);
                }
            }
            Squares.Clear();
        }

        private static void On()
        {
            if (_mine != null && UiPictures.Find == _mine)
            {
                return;
            }
            Func<string, Sprite> before = UiPictures.Find;
            _mine = key => Find(key) ?? before?.Invoke(key);
            UiPictures.Find = _mine;
        }

        private static Sprite Find(string key)
        {
            if (key == null)
            {
                return null;
            }
            if (key.StartsWith(Key, StringComparison.Ordinal))
            {
                return int.TryParse(key.Substring(Key.Length), NumberStyles.Integer, CultureInfo.InvariantCulture, out int id) && Sprites.TryGetValue(id, out Sprite s) ? s : null;
            }
            int rgb = UiDraw.SwatchColour(key);
            return rgb >= 0 ? Square(rgb) : null;
        }

        // One square of a colour, made the first time it is asked for and kept for the session.
        private static Sprite Square(int rgb)
        {
            if (Squares.TryGetValue(rgb, out Sprite sprite) && sprite != null)
            {
                return sprite;
            }
            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false) { name = "RealLife swatch", hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Point };
            var c = new Color32((byte)((rgb >> 16) & 255), (byte)((rgb >> 8) & 255), (byte)(rgb & 255), 255);
            var px = new Color32[16];
            for (int i = 0; i < px.Length; i++)
            {
                px[i] = c;
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            sprite = Sprite.Create(tex, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f), 100f);
            sprite.hideFlags = HideFlags.HideAndDontSave;
            Squares[rgb] = sprite;
            return sprite;
        }
    }
}
