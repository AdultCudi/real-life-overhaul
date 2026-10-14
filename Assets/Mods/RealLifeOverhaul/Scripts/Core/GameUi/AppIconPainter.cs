using System;
using System.Collections.Generic;
using BAModAPI;
using UnityEngine;

namespace RealLifeOverhaul.GameUi
{
    /// <summary>
    /// The picture on an app's button, made the way the game's own are: the rounded tile and soft shadow of one of the
    /// game's app icons (read from the game's picture while it runs, never shipped), filled with the app's own two
    /// colours from corner to corner, and one of the game's white glyphs in the middle. 160 by 160 like theirs.
    /// If the game's pictures cannot be read back, the glyph alone is the icon.
    ///
    /// KEPT FOR THE SESSION. Reading a picture of the game's back from the graphics card makes the processor wait for it (a
    /// stall of a millisecond or more each), and every app once read two in the first seconds after each load. Now the
    /// tile is read once a session, each glyph once at each size, and each finished icon is kept by its name and made
    /// again only if what it is made of is not what it was (another tile, glyph, colour or size): the same pixels, the
    /// same texture and sprite settings, so the same picture. The kept textures are marked never to be unloaded with a
    /// scene's unused assets, as the Ledger's runtime faces are (Ledger/LedgerType).
    ///
    /// Cost: nothing per frame. The first load of a session: one read of the tile and one of each glyph; every load after
    /// it: one dictionary look-up per app.
    /// </summary>
    public static class AppIconPainter
    {
        public static IModLogger Logger;

        private sealed class Painted
        {
            public Sprite Result, Like, Glyph;
            public Color32 Top, Bottom;
            public float GlyphSize;
        }

        private static readonly Dictionary<string, Painted> Made = new Dictionary<string, Painted>(32, StringComparer.Ordinal);
        private static readonly Dictionary<long, Color32[]> Reads = new Dictionary<long, Color32[]>(32);   // a picture read back, by sprite and size
        private static Color32[] _work = new Color32[0];

        /// <summary>The tile and shadow of 'like', coloured from 'top' (upper left) to 'bottom' (lower right), with 'glyph' on it.</summary>
        public static Sprite Paint(Sprite like, Sprite glyph, Color32 top, Color32 bottom, float glyphSize, string name)
        {
            try
            {
                if (like == null)
                {
                    return glyph;
                }
                string key = name ?? string.Empty;
                if (Made.TryGetValue(key, out Painted kept) && kept.Result != null && kept.Result.texture != null && kept.Like == like && kept.Glyph == glyph
                    && Same(kept.Top, top) && Same(kept.Bottom, bottom) && kept.GlyphSize == glyphSize)
                {
                    return kept.Result;
                }
                Sprite made = Draw(like, glyph, top, bottom, glyphSize, name);
                if (made != null && made != glyph)
                {
                    Made[key] = new Painted { Result = made, Like = like, Glyph = glyph, Top = top, Bottom = bottom, GlyphSize = glyphSize };
                }
                return made;
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                UiClone.Fallback("the app icon could not be painted, the glyph alone is used");
                return glyph;
            }
        }

        private static bool Same(Color32 a, Color32 b)
        {
            return a.r == b.r && a.g == b.g && a.b == b.b && a.a == b.a;
        }

        private static Sprite Draw(Sprite like, Sprite glyph, Color32 top, Color32 bottom, float glyphSize, string name)
        {
            int size = Mathf.RoundToInt(like.rect.width);
            Color32[] read = Read(like, size, size);
            if (read == null || size < 32)
            {
                return glyph;
            }
            if (_work.Length != read.Length)
            {
                _work = new Color32[read.Length];
            }
            Color32[] tile = _work;   // painted over in place: the tile as read stays as it was, for the next app
            Array.Copy(read, tile, read.Length);
            // The tile is every pixel that is fully there and not black; the shadow under it is black and faint.
            // Each of the game's icons has its glyph in white inside the tile, so the tile's own bounds are taken
            // and everything inside them that is opaque is painted over.
            int painted = 0;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Color32 c = tile[y * size + x];
                    if (c.a >= 250 && c.r + c.g + c.b > 60)
                    {
                        painted++;
                        float t = Mathf.Clamp01((x + (size - 1 - y)) / (2f * (size - 1)));
                        Color32 fill = Color32.Lerp(top, bottom, t);
                        fill.a = c.a;
                        tile[y * size + x] = fill;
                    }
                }
            }
            if (painted < size * size / 8)
            {
                UiClone.Fallback("the game's app icon could not be read back, the glyph alone is used");
                return glyph;
            }
            if (glyph != null)
            {
                int g = Mathf.RoundToInt(size * glyphSize);
                Color32[] mark = Read(glyph, g, g);
                if (mark != null)
                {
                    int at = (size - g) / 2;
                    // the tile sits a little above the middle of the picture: its shadow takes the room below it
                    int up = Mathf.RoundToInt(size * 0.025f);
                    for (int y = 0; y < g; y++)
                    {
                        for (int x = 0; x < g; x++)
                        {
                            Color32 m = mark[y * g + x];
                            if (m.a == 0)
                            {
                                continue;
                            }
                            int i = (y + at + up) * size + x + at;
                            Color32 under = tile[i];
                            float a = m.a / 255f;
                            tile[i] = new Color32((byte)Mathf.Lerp(under.r, 255, a), (byte)Mathf.Lerp(under.g, 255, a), (byte)Mathf.Lerp(under.b, 255, a), under.a > m.a ? under.a : m.a);
                        }
                    }
                }
            }
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false, false) { name = name, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            texture.SetPixels32(tile);
            texture.Apply(false, true);
            texture.hideFlags = HideFlags.DontUnloadUnusedAsset;   // kept for the session (Made)
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), like.pixelsPerUnit);
            sprite.name = name;
            sprite.hideFlags = HideFlags.DontUnloadUnusedAsset;
            return sprite;
        }

        // A sprite's picture at the given size, read back through a render texture (the game's textures cannot be read
        // directly). The sprite's own part of its sheet is drawn where it belongs in the sprite's full rectangle. Read once a
        // session for each sprite and size, and kept: whoever asks only reads what it is given.
        private static Color32[] Read(Sprite sprite, int width, int height)
        {
            if (sprite == null || sprite.texture == null || (sprite.packed && sprite.packingRotation != SpritePackingRotation.None))
            {
                return null;
            }
            long key = ((long)sprite.GetInstanceID() << 24) ^ ((long)width << 12) ^ height;
            if (Reads.TryGetValue(key, out Color32[] known))
            {
                return known;
            }
            Texture2D sheet = sprite.texture;
            Rect part = sprite.textureRect;
            Vector2 offset = sprite.textureRectOffset;
            float sx = width / sprite.rect.width, sy = height / sprite.rect.height;
            RenderTexture target = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            RenderTexture before = RenderTexture.active;
            Texture2D copy = null;
            try
            {
                RenderTexture.active = target;
                GL.Clear(true, true, new Color(0f, 0f, 0f, 0f));
                GL.PushMatrix();
                GL.LoadPixelMatrix(0, width, height, 0);
                var where = new Rect(offset.x * sx, height - (offset.y + part.height) * sy, part.width * sx, part.height * sy);
                var from = new Rect(part.x / sheet.width, part.y / sheet.height, part.width / sheet.width, part.height / sheet.height);
                Graphics.DrawTexture(where, sheet, from, 0, 0, 0, 0);
                GL.PopMatrix();
                copy = new Texture2D(width, height, TextureFormat.RGBA32, false, false);
                copy.ReadPixels(new Rect(0, 0, width, height), 0, 0, false);
                Color32[] pixels = copy.GetPixels32();
                Reads[key] = pixels;
                return pixels;
            }
            finally
            {
                RenderTexture.active = before;
                RenderTexture.ReleaseTemporary(target);
                if (copy != null)
                {
                    UnityEngine.Object.Destroy(copy);
                }
            }
        }
    }
}
