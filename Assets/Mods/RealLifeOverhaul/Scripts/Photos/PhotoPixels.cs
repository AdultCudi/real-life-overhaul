using System;

namespace RealLifeOverhaul.Photos
{
    /// <summary>
    /// A photo's pixels made smaller by a box filter: the screen to the photo kept, and the photo to its small copy (RGB24,
    /// rows bottom up). Game-free spans in and out, so the worker hands it the engine's own buffer. Worker thread only:
    /// a 4K screen is about 30 ms; nothing is allocated.
    /// </summary>
    public static class PhotoPixels
    {
        public const int PhotoWidth = 1920;     // pixels
        public const int PhotoHeight = 1080;    // pixels
        public const int SmallWidth = PhotoThumbs.Width;
        public const int SmallTallest = 1024;   // pixels, for a very tall screen

        /// <summary>The size that fits a box with the shape kept, never larger, and at least one pixel each way.</summary>
        public static void Fit(int width, int height, int mostWidth, int mostHeight, out int fitWidth, out int fitHeight)
        {
            if (width <= 0 || height <= 0)
            {
                fitWidth = fitHeight = 0;
                return;
            }
            double scale = Math.Min(1.0, Math.Min((double)mostWidth / width, (double)mostHeight / height));
            fitWidth = Math.Max(1, (int)Math.Round(width * scale));
            fitHeight = Math.Max(1, (int)Math.Round(height * scale));
            fitWidth = Math.Min(fitWidth, mostWidth);
            fitHeight = Math.Min(fitHeight, mostHeight);
        }

        /// <summary>A small copy's size: <see cref="SmallWidth"/> wide at most, the height in proportion.</summary>
        public static void SmallSize(int width, int height, out int smallWidth, out int smallHeight)
        {
            Fit(width, height, SmallWidth, SmallTallest, out smallWidth, out smallHeight);
        }

        /// <summary>
        /// Averages <paramref name="source"/> (3 or 4 bytes a pixel, blue first if <paramref name="bgr"/>) into RGB24 rows,
        /// keeping the row order. False, with nothing written, when a size or a length does not add up.
        /// </summary>
        public static bool Shrink(ReadOnlySpan<byte> source, int width, int height, int bytes, bool bgr, Span<byte> into, int toWidth, int toHeight)
        {
            if (width <= 0 || height <= 0 || toWidth <= 0 || toHeight <= 0 || toWidth > width || toHeight > height || (bytes != 3 && bytes != 4)
                || source.Length < (long)width * height * bytes || into.Length < (long)toWidth * toHeight * 3)
            {
                return false;
            }
            int r0 = bgr ? 2 : 0, b0 = bgr ? 0 : 2;
            int stride = width * bytes;
            for (int y = 0; y < toHeight; y++)
            {
                int ya = (int)((long)y * height / toHeight), yb = Math.Max(ya + 1, (int)((long)(y + 1) * height / toHeight));
                int to = y * toWidth * 3;
                for (int x = 0; x < toWidth; x++)
                {
                    int xa = (int)((long)x * width / toWidth), xb = Math.Max(xa + 1, (int)((long)(x + 1) * width / toWidth));
                    int r = 0, g = 0, b = 0, n = 0;
                    for (int sy = ya; sy < yb; sy++)
                    {
                        int at = sy * stride + xa * bytes;
                        for (int sx = xa; sx < xb; sx++, at += bytes)
                        {
                            r += source[at + r0];
                            g += source[at + 1];
                            b += source[at + b0];
                            n++;
                        }
                    }
                    int half = n >> 1;
                    into[to] = (byte)((r + half) / n);
                    into[to + 1] = (byte)((g + half) / n);
                    into[to + 2] = (byte)((b + half) / n);
                    to += 3;
                }
            }
            return true;
        }
    }
}
