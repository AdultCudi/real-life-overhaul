using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The rule of a picture that covers its box (UiFlags.Cover): the largest piece of the picture with the box's shape, centred, so it
    /// fills the box edge to edge and is never stretched. Game-free; the host cuts a sprite to this rectangle once per picture and shape.
    /// </summary>
    public static class UiCover
    {
        /// <summary>The box's shape as a whole number (thousandths of width over height), what a cut is kept for.</summary>
        public static int ShapeOf(float w, float h) => h > 0f ? (int)Math.Round(w / h * 1000f) : 0;

        /// <summary>The piece to show, in the picture's pixels from its bottom left: x, y, width, height.</summary>
        public static void Cut(float picW, float picH, float boxW, float boxH, out float x, out float y, out float w, out float h)
        {
            x = 0f;
            y = 0f;
            w = picW;
            h = picH;
            if (picW <= 0f || picH <= 0f || boxW <= 0f || boxH <= 0f)
            {
                return;
            }
            float want = boxW / boxH, have = picW / picH;
            if (have > want)
            {
                w = picH * want;   // a wider picture loses its sides
                x = (picW - w) / 2f;
            }
            else if (have < want)
            {
                h = picW / want;   // a taller picture loses its top and bottom
                y = (picH - h) / 2f;
            }
        }
    }
}
