using System;
using TMPro;
using UnityEngine;

namespace RealLifeOverhaul.Outlook
{
    /// <summary>
    /// The Economy app's charts, drawn on a page's pictures: the small line in a tile, the large line with its axes, last point and
    /// value, the bands of the cycle's history, and bars. Scaling and smoothing are <see cref="OutlookScale"/>'s (checked offline
    /// for any series); this puts them on the screen in the palette's colours. Run when a page is written, never per frame.
    /// </summary>
    internal static class OutlookDraw
    {
        private const int Held = 2048;
        private static readonly float[] Xs = new float[Held], Ys = new float[Held], Sx = new float[Held], Sy = new float[Held], Px = new float[Held], Py = new float[Held];

        private static Color32 K(int rgb, float alpha = 1f)
        {
            return OutlookKit.C32(rgb, alpha);
        }

        // The smooth path through a window's readings in a box (x by day), relative to the picture, and how many points it has.
        private static int Path(int[] window, int count, double unit, OutlookAxis axis, float x0, float y0, float w, float h, bool stepped, out int readings)
        {
            readings = OutlookScale.Points(window, count, unit, axis, w, h, Xs, Ys);
            for (int k = 0; k < readings; k++)
            {
                Xs[k] += x0;
                Ys[k] += y0;
            }
            if (readings < 2)
            {
                if (readings == 1)
                {
                    Sx[0] = Xs[0];
                    Sy[0] = Ys[0];
                }
                return readings;
            }
            if (stepped)
            {
                int m = 0;
                for (int k = 0; k < readings && m + 2 <= Held; k++)
                {
                    if (k > 0)
                    {
                        Sx[m] = Xs[k];
                        Sy[m] = Ys[k - 1];
                        m++;
                    }
                    Sx[m] = Xs[k];
                    Sy[m] = Ys[k];
                    m++;
                }
                return m;
            }
            int sub = readings < 40 ? 6 : readings < 90 ? 3 : 2;
            return OutlookScale.Smooth(Xs, Ys, readings, sub, Sx, Sy);
        }

        /// <summary>
        /// A small line with a fade under it and a dot at its last reading, filling a box. With no reading it is a faint level line; with
        /// one it is a dot. Returns how many readings it drew.
        /// </summary>
        public static int Spark(OutlookPage page, float x, float y, float w, float h, int[] window, int count, double unit, int colour, bool zero = false)
        {
            OutlookPalette p = page.Palette;
            OutlookMesh mesh = page.Picture(x, y, w, h);
            OutlookScale.Range(window, count, unit, out double lo, out double hi, out int found);
            if (found == 0)
            {
                mesh.Rect(0f, h * 0.5f - 1.5f, w, h * 0.5f + 1.5f, K(p.Border));
                mesh.End();
                return 0;
            }
            OutlookAxis axis = OutlookScale.Nice(lo, hi, 3, zero);
            int path = Path(window, count, unit, axis, 6f, 10f, w - 24f, h - 20f, false, out int readings);
            mesh.Area(Sx, Sy, path, h - 2f, K(colour, 0.32f), K(colour, 0f));
            mesh.Line(Sx, Sy, path, 4.5f, K(colour));
            mesh.Disc(Xs[readings - 1], Ys[readings - 1], 8f, K(colour, 0.28f));
            mesh.Disc(Xs[readings - 1], Ys[readings - 1], 5.2f, K(colour));
            mesh.End();
            return readings;
        }

        /// <summary>
        /// A large line chart in a box: faint gridlines with the figures at their left, a fade under the line, the last reading marked
        /// with its value, and the days at the foot. <paramref name="label"/> writes a figure on the axis. Returns the readings drawn.
        /// </summary>
        public static int Chart(OutlookPage page, float x, float y, float w, float h, int[] window, int count, double unit, int colour, Func<double, string> label,
            int endDay, bool stepped = false, bool zero = false)
        {
            OutlookPalette p = page.Palette;
            const float left = 120f, top = 18f, bottom = 52f, right = 40f;
            float plotW = Mathf.Max(40f, w - left - right), plotH = Mathf.Max(40f, h - top - bottom);
            OutlookMesh mesh = page.Picture(x, y, w, h);
            OutlookScale.Range(window, count, unit, out double lo, out double hi, out int found);
            OutlookAxis axis = OutlookScale.Nice(lo, hi, 5, zero);
            for (int i = 0; i < axis.Ticks; i++)
            {
                double v = axis.Tick(i);
                float gy = top + plotH * (1f - (float)axis.Fraction(v));
                mesh.Rect(left, gy - 0.9f, left + plotW, gy + 0.9f, K(p.Grid));
                page.Text(label(v), x, y + gy - 16f, left - 20f, 32f, 24f, p.Muted, TextAlignmentOptions.MidlineRight);
            }
            float first = count > 1 ? Mathf.Max(0, endDay - count + 1) : endDay;
            string leftDay = OutlookText.Day((int)Mathf.Max(1f, first)), midDay = OutlookText.Day((int)Mathf.Max(1f, first + (endDay - first) * 0.5f)), today = OutlookText.T("reallife_econapp_today");
            float foot = y + top + plotH + 14f;
            page.Text(leftDay, x + left - 6f, foot, 300f, 30f, 24f, p.Muted, TextAlignmentOptions.TopLeft);
            page.Text(midDay, x + left + plotW * 0.5f - 150f, foot, 300f, 30f, 24f, p.Muted, TextAlignmentOptions.Top);
            page.Text(today, x + left + plotW - 294f, foot, 300f, 30f, 24f, p.Muted, TextAlignmentOptions.TopRight);
            if (found == 0)
            {
                mesh.End();
                return 0;
            }
            int path = Path(window, count, unit, axis, left, top, plotW, plotH, stepped, out int readings);
            if (found >= 2)
            {
                mesh.Area(Sx, Sy, path, top + plotH, K(colour, 0.34f), K(colour, 0f));
            }
            mesh.Line(Sx, Sy, path, 5f, K(colour));
            float lx = Xs[readings - 1], ly = Ys[readings - 1];
            mesh.Disc(lx, ly, 17f, K(colour, 0.24f));
            mesh.Disc(lx, ly, 9.5f, K(colour));
            mesh.Disc(lx, ly, 4.2f, K(p.Card));
            // The last value, in a small pill beside the point.
            double last = 0.0;
            for (int i = count - 1; i >= 0; i--)
            {
                if (window[i] != OutlookCatalogue.NoValue)
                {
                    last = window[i] / unit;
                    break;
                }
            }
            string text = label(last);
            float tw = page.Measure(text, 26f, true) + 34f;
            float px = Mathf.Clamp(lx - tw - 18f, left, left + plotW - tw);
            float py = ly > top + 62f ? ly - 56f : ly + 22f;
            int pill = OutlookTheme.Legible(colour, out int ink);
            mesh.Round(px, py, px + tw, py + 40f, 20f, 20f, K(pill));
            mesh.End();
            page.Text(text, x + px, y + py, tw, 40f, 26f, ink, TextAlignmentOptions.Midline, true);
            return readings;
        }

        /// <summary>The cycle's history as bands, a column a day in the colour of the day's state (a day with none is faint), rounded.</summary>
        public static void Bands(OutlookPage page, float x, float y, float w, float h, int[] window, int count, int[] colours)
        {
            OutlookPalette p = page.Palette;
            OutlookMesh mesh = page.Picture(x, y, w, h);
            float step = count > 0 ? w / count : w;
            float gap = Mathf.Min(2.5f, step * 0.18f);
            for (int i = 0; i < count; i++)
            {
                int state = window[i];
                bool known = state != OutlookCatalogue.NoValue;
                int c = known ? colours[Mathf.Clamp(state, 0, colours.Length - 1)] : p.Border;
                mesh.Round(i * step + gap * 0.5f, 0f, (i + 1) * step - gap * 0.5f, h, 4f, 4f, K(c, known ? 1f : 0.5f));
            }
            mesh.End();
        }

        /// <summary>A track with a fill to a share of its length, rounded: a meter.</summary>
        public static void Meter(OutlookMesh mesh, float x, float y, float w, float h, float share, int track, int fill)
        {
            mesh.Round(x, y, x + w, y + h, h * 0.5f, h * 0.5f, K(track));
            float length = Mathf.Clamp01(share) * w;
            if (length > 0.5f)
            {
                mesh.Round(x, y, x + Mathf.Max(h, length), y + h, h * 0.5f, h * 0.5f, K(fill));
            }
        }

        /// <summary>A small arrow, up or down, in a box.</summary>
        public static void Arrow(OutlookMesh mesh, float x, float y, float size, bool up, int colour)
        {
            float half = size * 0.5f;
            if (up)
            {
                mesh.Triangle(x + half, y, x, y + size * 0.85f, x + size, y + size * 0.85f, K(colour));
            }
            else
            {
                mesh.Triangle(x, y + size * 0.15f, x + size, y + size * 0.15f, x + half, y + size, K(colour));
            }
        }
    }
}
