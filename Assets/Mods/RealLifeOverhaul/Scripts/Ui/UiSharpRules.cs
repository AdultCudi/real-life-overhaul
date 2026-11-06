using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>What drawing a game picture sharp means: its box in px of the layout, the pixels of the picture and the screen's pixels in a px of the layout. Game-free.</summary>
    public static class UiSharpRules
    {
        /// <summary>A picture drawn smaller than this share of its pixels is resampled to the size it shows at.</summary>
        public const float ShrinkLimit = 0.67f;

        /// <summary>
        /// How a picture of <paramref name="pw"/> x <paramref name="ph"/> pixels is shown in a box: 0 as it is, 1 in a box shrunk to its own pixels
        /// (<paramref name="w"/> and <paramref name="h"/> written back; it is never stretched past them), 2 resampled to <paramref name="tw"/> x
        /// <paramref name="th"/> pixels, the size it shows at.
        /// </summary>
        public static int Plan(float pw, float ph, float device, ref float x, ref float y, ref float w, ref float h, out int tw, out int th)
        {
            tw = th = 0;
            if (device <= 0f || pw < 1f || ph < 1f || w <= 0f || h <= 0f)
            {
                return 0;
            }
            float drawn = Math.Min(w * device / pw, h * device / ph);   // 1: a pixel of the picture for a pixel of the screen
            if (drawn > 1f)
            {
                float cw = pw / device, ch = ph / device;
                x += (w - cw) / 2f;
                y += (h - ch) / 2f;
                w = cw;
                h = ch;
                return 1;
            }
            if (drawn >= ShrinkLimit)
            {
                return 0;
            }
            tw = Math.Max(1, (int)Math.Round(pw * drawn));
            th = Math.Max(1, (int)Math.Round(ph * drawn));
            return 2;
        }
    }
}
