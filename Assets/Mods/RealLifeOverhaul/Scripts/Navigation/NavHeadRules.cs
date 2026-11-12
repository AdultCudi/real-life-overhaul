using System;

namespace RealLifeOverhaul.Nav
{
    /// <summary>A box in the minimap's headline band, in the game HUD's units from the band's lower left corner.</summary>
    public struct NavHeadBox
    {
        public float X, Y, Width, Height;

        public NavHeadBox(float x, float y, float width, float height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        public float Right => X + Width;
        public float Top => Y + Height;
    }

    /// <summary>
    /// The minimap's headline in plain numbers, game-free so EconomyCheck holds it at every card size and text width: a dark band of the card's own plate,
    /// inside the card's drawn edge, with the place's name over its address at the left and the minutes over the distance right aligned at the right; and
    /// where the destination's marker stays when it is held at the view's edge, clear of the district strip and the round buttons. Colours are the card's
    /// own pairs (the strip's plate, ink, accent and line grey). Cost: a few sums, when a text or the card's size changes.
    /// </summary>
    public static class NavHeadRules
    {
        public const float Drawn = 14f;        // the card's box is drawn this far inside its rectangle (NavPlaceRules.ViewSide)
        public const float Inset = 16f;        // the band's inset from the card's rectangle at the sides and the top: 2 inside the drawn edge
        public const float BandGap = 4f;       // between the band's lower edge and the map's top
        public const float Pad = 14f;          // the texts' inset from the band's sides
        public const float Between = 12f;      // between the name and the minutes
        public const float RightLeast = 64f;   // the minutes' column at least
        public const float RightMost = 0.55f;  // and at most this part of the band's inner width; the name keeps the rest
        public const float RightSize = 28f;    // the minutes' lettering, and the least it shrinks to before they are shortened
        public const float RightLeastSize = 18f;
        public const float NameRow = 0.52f;    // the name's part of the band's height when an address shows under it

        // The card's own colour pairs, as 0xRRGGBBAA (the district strip's plate, ink, accent and line grey: NavHud.Strip).
        public const uint Band = 0x0F1922F0;       // the plate, nearly solid: the band reads the same over a bright street or a dark one
        public const uint Name = 0x4FB8EAFF;       // the accent blue, as the game's own panel headlines
        public const uint Address = 0x93A6B7FF;    // the line grey
        public const uint Minutes = 0xE9EFF4FF;    // the ink
        public const uint Distance = 0x93A6B7FF;   // the line grey
        public const string DistanceHex = "#93A6B7";

        /// <summary>The band's height in the card: the headline less the inset at the top and the gap above the map.</summary>
        public static float BandHeight(float head) => head - Inset - BandGap;

        /// <summary>The band's width for a card this wide.</summary>
        public static float BandWidth(float cardWidth) => Math.Max(0f, cardWidth - Inset * 2f);

        /// <summary>
        /// Lays the headline's texts out in a band <paramref name="width"/> by <paramref name="height"/>: the minutes' column as wide as its text asks
        /// (<paramref name="rightWanted"/>, between <see cref="RightLeast"/> and <see cref="RightMost"/> of the inner width), the name and the address in
        /// what is left, the name over the address when there is one, alone and centred when not.
        /// </summary>
        public static void Layout(float width, float height, float rightWanted, bool address, bool right, out NavHeadBox name, out NavHeadBox under, out NavHeadBox minutes)
        {
            float inner = Math.Max(0f, width - Pad * 2f);
            float column = right ? Math.Min(inner * RightMost, Math.Max(RightLeast, rightWanted)) : 0f;
            minutes = new NavHeadBox(width - Pad - column, 0f, column, height);
            float textRight = right ? minutes.X - Between : width - Pad;
            float textWidth = Math.Max(0f, textRight - Pad);
            if (address)
            {
                float split = (float)Math.Round(height * (1f - NameRow));
                name = new NavHeadBox(Pad, split, textWidth, height - split);
                under = new NavHeadBox(Pad, 0f, textWidth, split);
            }
            else
            {
                name = new NavHeadBox(Pad, 0f, textWidth, height);
                under = new NavHeadBox(Pad, 0f, textWidth, 0f);
            }
        }

        /// <summary>
        /// Holds the destination's marker, already held inside the view (<paramref name="x"/>, <paramref name="y"/> from the view's middle), clear of the
        /// district strip along the view's top (<paramref name="rowDepth"/> down from the top edge) and of the round buttons' column at the top right
        /// (<paramref name="columnWidth"/> wide, <paramref name="columnDepth"/> deep): it slides in towards the middle along its own line, so it still
        /// points the way. <paramref name="halfWidth"/> and <paramref name="halfHeight"/> are the view's half sizes; the depths and the width count the
        /// marker's own radius in. In a low view the buttons' column may reach below the middle.
        /// </summary>
        public static void KeepClear(ref float x, ref float y, float halfWidth, float halfHeight, float rowDepth, float columnWidth, float columnDepth)
        {
            float scale = 1f;
            float rowLimit = Math.Max(0f, halfHeight - rowDepth);
            if (y > rowLimit)
            {
                scale = rowLimit / y;
            }
            float columnX = Math.Max(0f, halfWidth - columnWidth);
            float columnY = halfHeight - columnDepth;
            if (x > 0f && x * scale > columnX && y * scale > columnY)
            {
                // out of the column by its left side, or by its lower edge where that is above the middle (a slide in never lowers a point under it)
                scale = Math.Max(columnX / x, y > 0f && columnY >= 0f ? columnY / y : 0f);
            }
            x *= scale;
            y *= scale;
        }

        /// <summary>The contrast of two colours (0xRRGGBBAA, the first drawn over the second, the second solid) as the web's accessibility rules count it, 1 to 21.</summary>
        public static double Contrast(uint text, uint ground)
        {
            double a = Luminance(text), b = Luminance(ground);
            return (Math.Max(a, b) + 0.05) / (Math.Min(a, b) + 0.05);
        }

        /// <summary>A colour with alpha (0xRRGGBBAA) laid over a solid one: the solid colour the eye sees.</summary>
        public static uint Over(uint top, uint under)
        {
            double alpha = (top & 0xFF) / 255.0;
            uint mixed = 0xFF;
            for (int shift = 24; shift >= 8; shift -= 8)
            {
                double t = (top >> shift) & 0xFF, u = (under >> shift) & 0xFF;
                mixed |= (uint)Math.Round(t * alpha + u * (1.0 - alpha)) << shift;
            }
            return mixed;
        }

        private static double Luminance(uint colour)
        {
            return 0.2126 * Channel((colour >> 24) & 0xFF) + 0.7152 * Channel((colour >> 16) & 0xFF) + 0.0722 * Channel((colour >> 8) & 0xFF);
        }

        private static double Channel(uint value)
        {
            double c = value / 255.0;
            return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }
    }
}
