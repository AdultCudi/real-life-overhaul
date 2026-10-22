using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using BAModAPI;
using City.CityMap;
using Streets;
using UI;
using UI.Guiders;
using UI.InGameUI;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace RealLifeOverhaul
{
    /// <summary>The map places' built-in glyphs, drawn in code.</summary>
    public static partial class MapPlaces
    {
        /// <summary>One of the built-in glyphs as a sprite, white on nothing, 64 pixels square, drawn on first asking.</summary>
        public static Sprite Glyph(MapIcon icon)
        {
            int index = Mathf.Clamp((int)icon, 0, Glyphs.Length - 1);
            if (Glyphs[index] == null)
            {
                Glyphs[index] = Draw((MapIcon)index);
            }
            return Glyphs[index];
        }

        private const int Size = 64;                // pixels
        private const int Fine = 4;                 // samples a pixel each way: soft edges without a filter
        private const int Grid = Size * Fine;

        private static bool[] _ink;

        private static Sprite Draw(MapIcon icon)
        {
            _ink = new bool[Grid * Grid];
            switch (icon)
            {
                case MapIcon.Shield:
                    Poly(true, 10, 57, 54, 57, 54, 34, 32, 6, 10, 34);
                    Star(false, 32f, 37f, 12f, 5f);
                    break;
                case MapIcon.Target:
                    Ring(32f, 32f, 27f, 5f);
                    Ring(32f, 32f, 15f, 5f);
                    Disc(true, 32f, 32f, 4.5f);
                    Line(true, 32f, 61f, 32f, 50f, 4f);
                    Line(true, 32f, 3f, 32f, 14f, 4f);
                    Line(true, 3f, 32f, 14f, 32f, 4f);
                    Line(true, 61f, 32f, 50f, 32f, 4f);
                    break;
                case MapIcon.Bus:
                    Rect(true, 8f, 18f, 56f, 54f);
                    Rect(false, 13f, 36f, 51f, 48f);
                    Rect(false, 30f, 36f, 34f, 48f);
                    Rect(true, 30.8f, 36f, 33.2f, 48f);
                    Disc(true, 20f, 17f, 7f);
                    Disc(true, 44f, 17f, 7f);
                    Disc(false, 20f, 17f, 3f);
                    Disc(false, 44f, 17f, 3f);
                    Rect(false, 12f, 25f, 19f, 29f);
                    Rect(false, 45f, 25f, 52f, 29f);
                    break;
                case MapIcon.Bed:
                    Rect(true, 6f, 18f, 58f, 31f);
                    Rect(true, 6f, 18f, 12f, 48f);
                    Rect(true, 15f, 31f, 30f, 39f);
                    Rect(true, 6f, 9f, 11f, 18f);
                    Rect(true, 53f, 9f, 58f, 18f);
                    Rect(true, 52f, 18f, 58f, 37f);
                    break;
                case MapIcon.Scales:
                    Line(true, 32f, 10f, 32f, 54f, 4f);
                    Line(true, 10f, 48f, 54f, 48f, 4f);
                    Rect(true, 19f, 6f, 45f, 12f);
                    Line(true, 10f, 48f, 4f, 30f, 2.4f);
                    Line(true, 10f, 48f, 16f, 30f, 2.4f);
                    Poly(true, 2, 30, 18, 30, 14, 23, 6, 23);
                    Line(true, 54f, 48f, 48f, 30f, 2.4f);
                    Line(true, 54f, 48f, 60f, 30f, 2.4f);
                    Poly(true, 46, 30, 62, 30, 58, 23, 50, 23);
                    Disc(true, 32f, 55f, 4f);
                    break;
                case MapIcon.Wheel:
                    Ring(32f, 32f, 27f, 6f);
                    Disc(true, 32f, 32f, 7.5f);
                    Line(true, 32f, 32f, 8f, 34f, 5f);
                    Line(true, 32f, 32f, 56f, 34f, 5f);
                    Line(true, 32f, 32f, 32f, 8f, 5f);
                    break;
                case MapIcon.Bicycle:
                    Ring(15f, 22f, 12f, 4f);
                    Ring(49f, 22f, 12f, 4f);
                    Line(true, 15f, 22f, 26f, 42f, 3.2f);
                    Line(true, 26f, 42f, 44f, 42f, 3.2f);
                    Line(true, 44f, 42f, 49f, 22f, 3.2f);
                    Line(true, 26f, 42f, 33f, 22f, 3.2f);
                    Line(true, 33f, 22f, 44f, 42f, 3.2f);
                    Line(true, 15f, 22f, 33f, 22f, 3.2f);
                    Line(true, 21f, 47f, 31f, 47f, 3.6f);
                    Line(true, 26f, 42f, 26f, 47f, 3f);
                    Line(true, 44f, 42f, 43f, 50f, 3f);
                    Line(true, 40f, 50f, 49f, 51f, 3.6f);
                    break;
                case MapIcon.Wrench:
                    Line(true, 17f, 17f, 45f, 45f, 9f);
                    Disc(true, 47f, 47f, 12.5f);
                    Poly(false, 46, 46, 62, 50, 62, 62, 50, 62);
                    Disc(true, 15f, 15f, 8f);
                    Disc(false, 15f, 15f, 3.2f);
                    break;
                case MapIcon.Briefcase:
                    Rect(true, 7f, 12f, 57f, 44f);
                    Rect(true, 23f, 44f, 41f, 53f);
                    Rect(false, 27f, 44f, 37f, 49f);
                    Rect(false, 7f, 28.5f, 57f, 31f);
                    Rect(true, 28f, 26f, 36f, 34f);
                    break;
                case MapIcon.Ballot:
                    Rect(true, 9f, 6f, 55f, 36f);
                    Rect(false, 20f, 30f, 44f, 33f);
                    Poly(true, 25, 33, 41, 33, 45, 58, 29, 60);
                    Rect(false, 20f, 33f, 44f, 36f);
                    Rect(true, 9f, 34f, 20f, 36f);
                    Rect(true, 44f, 34f, 55f, 36f);
                    break;
                case MapIcon.Cone:
                    Poly(true, 27, 58, 37, 58, 48, 14, 16, 14);
                    Rect(true, 8f, 7f, 56f, 14f);
                    Rect(false, 16f, 30f, 48f, 36f);
                    Rect(false, 16f, 44f, 48f, 48f);
                    break;
                case MapIcon.Star:
                    Star(true, 32f, 30f, 29f, 12f);
                    break;
                case MapIcon.Flag:
                    Line(true, 14f, 6f, 14f, 58f, 5f);
                    Poly(true, 16, 56, 54, 44, 16, 30);
                    Rect(true, 7f, 5f, 23f, 9f);
                    break;
                case MapIcon.Paper:
                    // A newspaper: the sheet, the masthead, two columns of print and a picture.
                    Rect(true, 9f, 8f, 55f, 56f);
                    Rect(false, 13.5f, 12.5f, 50.5f, 51.5f);
                    Rect(true, 17f, 41f, 47f, 47.5f);
                    Rect(true, 17f, 33.5f, 30f, 36.5f);
                    Rect(true, 17f, 27f, 30f, 30f);
                    Rect(true, 17f, 20.5f, 30f, 23.5f);
                    Rect(true, 17f, 15f, 27f, 18f);
                    Rect(true, 34f, 22f, 47f, 36.5f);
                    Rect(true, 34f, 15f, 47f, 18f);
                    break;
                case MapIcon.Tower:
                    // An office tower: three columns of windows, the door at its foot.
                    Rect(true, 14f, 4f, 50f, 60f);
                    for (int floor = 0; floor < 5; floor++)
                    {
                        for (int column = 0; column < 3; column++)
                        {
                            Rect(false, 19f + column * 10f, 18f + floor * 8.4f, 25f + column * 10f, 23.5f + floor * 8.4f);
                        }
                    }
                    Rect(false, 28f, 4f, 36f, 13f);
                    break;
                case MapIcon.Cloud:
                    // A cloud over two lit servers.
                    Disc(true, 22f, 40f, 11f);
                    Disc(true, 36f, 45f, 13f);
                    Disc(true, 47f, 37f, 9f);
                    Rect(true, 12f, 30f, 54f, 40f);
                    Rect(true, 18f, 6f, 46f, 15f);
                    Rect(true, 18f, 18f, 46f, 27f);
                    Rect(false, 21f, 9f, 25f, 12f);
                    Rect(false, 21f, 21f, 25f, 24f);
                    break;
                case MapIcon.Coin:
                    // A coin: its rim and a dollar sign in six strokes.
                    Ring(32f, 32f, 27f, 5f);
                    Line(true, 23f, 43f, 41f, 43f, 4.5f);
                    Line(true, 23f, 43f, 23f, 32f, 4.5f);
                    Line(true, 23f, 32f, 41f, 32f, 4.5f);
                    Line(true, 41f, 32f, 41f, 21f, 4.5f);
                    Line(true, 41f, 21f, 23f, 21f, 4.5f);
                    Line(true, 32f, 13f, 32f, 51f, 4.5f);
                    break;
                case MapIcon.House:
                    // A house: roof, walls, door and chimney.
                    Poly(true, 4, 32, 32, 58, 60, 32);
                    Rect(true, 12f, 6f, 52f, 34f);
                    Rect(false, 27f, 6f, 37f, 22f);
                    Rect(true, 42f, 44f, 48f, 56f);
                    break;
                case MapIcon.Heart:
                    Disc(true, 21f, 40f, 13f);
                    Disc(true, 43f, 40f, 13f);
                    Poly(true, 9, 36, 55, 36, 32, 8);
                    break;
                case MapIcon.Mortarboard:
                    // A graduate's cap: the cap, a gap, the square board and the tassel.
                    Poly(true, 17, 34, 47, 34, 46, 18, 32, 13, 18, 18);
                    Poly(false, 0, 40, 32, 56, 64, 40, 32, 24);
                    Poly(true, 4, 40, 32, 53, 60, 40, 32, 27);
                    Line(true, 51f, 38f, 51f, 21f, 2.6f);
                    Disc(true, 51f, 18f, 3.6f);
                    break;
                default:
                    Rect(true, 24f, 7f, 40f, 57f);
                    Rect(true, 7f, 24f, 57f, 40f);
                    break;
            }
            var pixels = new Color32[Size * Size];
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    int inked = 0;
                    for (int sy = 0; sy < Fine; sy++)
                    {
                        for (int sx = 0; sx < Fine; sx++)
                        {
                            inked += _ink[(y * Fine + sy) * Grid + x * Fine + sx] ? 1 : 0;
                        }
                    }
                    pixels[y * Size + x] = new Color32(255, 255, 255, (byte)(inked * 255 / (Fine * Fine)));
                }
            }
            _ink = null;
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false) { name = "RealLifeOverhaul Map " + icon };
            texture.SetPixels32(pixels);
            texture.Apply();
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f));
            sprite.name = texture.name;
            return sprite;
        }

        // The drawing board is Size units square, y up; each call inks or wipes what its shape covers.
        private static void Each(bool ink, float x0, float y0, float x1, float y1, Func<float, float, bool> covers)
        {
            int fromX = Mathf.Clamp(Mathf.FloorToInt(x0 * Fine), 0, Grid - 1);
            int toX = Mathf.Clamp(Mathf.CeilToInt(x1 * Fine), 0, Grid - 1);
            int fromY = Mathf.Clamp(Mathf.FloorToInt(y0 * Fine), 0, Grid - 1);
            int toY = Mathf.Clamp(Mathf.CeilToInt(y1 * Fine), 0, Grid - 1);
            for (int y = fromY; y <= toY; y++)
            {
                for (int x = fromX; x <= toX; x++)
                {
                    if (covers((x + 0.5f) / Fine, (y + 0.5f) / Fine))
                    {
                        _ink[y * Grid + x] = ink;
                    }
                }
            }
        }

        private static void Rect(bool ink, float x0, float y0, float x1, float y1)
        {
            Each(ink, x0, y0, x1, y1, (x, y) => x >= x0 && x <= x1 && y >= y0 && y <= y1);
        }

        private static void Disc(bool ink, float cx, float cy, float radius)
        {
            Each(ink, cx - radius, cy - radius, cx + radius, cy + radius, (x, y) => (x - cx) * (x - cx) + (y - cy) * (y - cy) <= radius * radius);
        }

        private static void Ring(float cx, float cy, float radius, float width)
        {
            float inner = (radius - width) * (radius - width);
            Each(true, cx - radius, cy - radius, cx + radius, cy + radius, (x, y) =>
            {
                float sqr = (x - cx) * (x - cx) + (y - cy) * (y - cy);
                return sqr <= radius * radius && sqr >= inner;
            });
        }

        private static void Line(bool ink, float x0, float y0, float x1, float y1, float width)
        {
            float half = width * 0.5f;
            float dx = x1 - x0, dy = y1 - y0;
            float length = Mathf.Max(0.0001f, dx * dx + dy * dy);
            Each(ink, Mathf.Min(x0, x1) - half, Mathf.Min(y0, y1) - half, Mathf.Max(x0, x1) + half, Mathf.Max(y0, y1) + half, (x, y) =>
            {
                float along = Mathf.Clamp01(((x - x0) * dx + (y - y0) * dy) / length);
                float px = x0 + dx * along - x, py = y0 + dy * along - y;
                return px * px + py * py <= half * half;
            });
        }

        private static void Poly(bool ink, params int[] corners)
        {
            int count = corners.Length / 2;
            Each(ink, 0f, 0f, Size, Size, (x, y) =>
            {
                bool odd = false;
                for (int i = 0, j = count - 1; i < count; j = i++)
                {
                    float xi = corners[i * 2], yi = corners[i * 2 + 1], xj = corners[j * 2], yj = corners[j * 2 + 1];
                    if ((yi > y) != (yj > y) && x < (xj - xi) * (y - yi) / (yj - yi) + xi)
                    {
                        odd = !odd;
                    }
                }
                return odd;
            });
        }

        private static void Star(bool ink, float cx, float cy, float outer, float inner)
        {
            var corners = new float[20];
            for (int i = 0; i < 10; i++)
            {
                float angle = Mathf.PI * 0.5f + i * Mathf.PI / 5f;
                float radius = (i & 1) == 0 ? outer : inner;
                corners[i * 2] = cx + Mathf.Cos(angle) * radius;
                corners[i * 2 + 1] = cy + Mathf.Sin(angle) * radius;
            }
            Each(ink, cx - outer, cy - outer, cx + outer, cy + outer, (x, y) =>
            {
                bool odd = false;
                for (int i = 0, j = 9; i < 10; j = i++)
                {
                    float xi = corners[i * 2], yi = corners[i * 2 + 1], xj = corners[j * 2], yj = corners[j * 2 + 1];
                    if ((yi > y) != (yj > y) && x < (xj - xi) * (y - yi) / (yj - yi) + xi)
                    {
                        odd = !odd;
                    }
                }
                return odd;
            });
        }
    }
}
