using System.Collections.Generic;
using UnityEngine;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// A game picture drawn as sharp as its pixels allow, for every place the kit shows one: a picture made smaller than it was
    /// drawn for is resampled once into a sprite of the size it shows at (the engine's plain bilinear skips pixels when a large
    /// picture shrinks a long way, and a small tile of a big icon shimmers), and one that would be stretched past its pixels
    /// keeps its own size instead of being blown up soft. Cost: a resample once per picture and size; a layout looks the sprite up.
    /// </summary>
    public sealed partial class UiView
    {
        private const int MostSharp = 192;         // resampled sprites kept; the oldest go first
        private const int DropSharp = 64;

        private readonly Dictionary<long, Sprite> _sharp = new Dictionary<long, Sprite>(64);
        private readonly Queue<long> _sharpOrder = new Queue<long>(64);
        private Canvas _scaleCanvas;

        // Device pixels in a px of the layout: the canvas units a px is, times the pixels a canvas unit is on this screen.
        private float DevicePerPx()
        {
            if (_scaleCanvas == null && _root != null)
            {
                // Walked by hand: the engine keeps rootCanvas up only while the canvas is on.
                for (Transform t = _root; t != null; t = t.parent)
                {
                    Canvas c = t.GetComponent<Canvas>();
                    if (c != null)
                    {
                        _scaleCanvas = c;
                    }
                }
            }
            float canvasToScreen = _scaleCanvas != null ? _scaleCanvas.scaleFactor : Screen.height / (UiTokens.CanvasPerPx * 1080f);
            return Scale * canvasToScreen;
        }

        // The sprite to show for a picture's box and the box it takes (its place in px of the layout, written back).
        private Sprite Crisp(Sprite sprite, ref float x, ref float y, ref float w, ref float h)
        {
            Rect pixels = sprite.textureRect;
            return UiSharpRules.Plan(pixels.width, pixels.height, DevicePerPx(), ref x, ref y, ref w, ref h, out int tw, out int th) == 2 ? Resampled(sprite, tw, th) ?? sprite : sprite;
        }

        private Sprite Resampled(Sprite source, int w, int h)
        {
            long key = ((long)source.GetInstanceID() << 24) ^ ((long)w << 12) ^ (long)h;
            if (_sharp.TryGetValue(key, out Sprite kept) && kept != null)
            {
                return kept;
            }
            Texture texture = source.texture;
            if (texture == null)
            {
                return null;
            }
            Rect part = source.textureRect;
            var scale = new Vector2(part.width / texture.width, part.height / texture.height);
            var shift = new Vector2(part.x / texture.width, part.y / texture.height);
            FilterMode filter = texture.filterMode;
            texture.filterMode = FilterMode.Bilinear;   // the halving below is a box filter only while it samples bilinearly
            RenderTexture previous = RenderTexture.active;
            int cw = Mathf.RoundToInt(part.width), ch = Mathf.RoundToInt(part.height);
            Texture from = texture;
            RenderTexture held = null;
            try
            {
                while (true)
                {
                    int nw = Mathf.Max(w, (cw + 1) / 2), nh = Mathf.Max(h, (ch + 1) / 2);
                    if (cw <= w * 2 && ch <= h * 2)
                    {
                        nw = w;
                        nh = h;
                    }
                    RenderTexture finished = held;
                    held = RenderTexture.GetTemporary(nw, nh, 0, RenderTextureFormat.ARGB32);
                    held.filterMode = FilterMode.Bilinear;
                    if (ReferenceEquals(from, texture))
                    {
                        Graphics.Blit(from, held, scale, shift);
                    }
                    else
                    {
                        Graphics.Blit(from, held);
                    }
                    if (finished != null)
                    {
                        RenderTexture.ReleaseTemporary(finished);
                    }
                    from = held;
                    cw = nw;
                    ch = nh;
                    if (nw == w && nh == h)
                    {
                        break;
                    }
                }
                RenderTexture.active = held;
                var made = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                made.ReadPixels(new Rect(0f, 0f, w, h), 0, 0);
                made.Apply(false, true);
                Sprite sprite = Sprite.Create(made, new Rect(0f, 0f, w, h), new Vector2(0.5f, 0.5f), source.pixelsPerUnit, 0, SpriteMeshType.FullRect);
                Remember(key, sprite);
                return sprite;
            }
            finally
            {
                texture.filterMode = filter;
                RenderTexture.active = previous;
                if (held != null)
                {
                    RenderTexture.ReleaseTemporary(held);
                }
            }
        }

        private void Remember(long key, Sprite sprite)
        {
            if (_sharp.TryGetValue(key, out Sprite old) && old != null)
            {
                Release(old);
            }
            else
            {
                _sharpOrder.Enqueue(key);
            }
            _sharp[key] = sprite;
            while (_sharp.Count > MostSharp && _sharpOrder.Count > 0)
            {
                for (int i = 0; i < DropSharp && _sharpOrder.Count > 0; i++)
                {
                    long gone = _sharpOrder.Dequeue();
                    if (_sharp.TryGetValue(gone, out Sprite s))
                    {
                        _sharp.Remove(gone);
                        Release(s);
                    }
                }
            }
        }

        private static void Release(Sprite sprite)
        {
            if (sprite == null)
            {
                return;
            }
            Texture2D texture = sprite.texture;
            Object.Destroy(sprite);
            Object.Destroy(texture);
        }
    }
}
