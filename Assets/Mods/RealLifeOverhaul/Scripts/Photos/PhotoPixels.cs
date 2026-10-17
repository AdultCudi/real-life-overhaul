using System;

namespace RealLifeOverhaul.Photos
{
    /// <summary>
    /// A photo's pixels made smaller: the screen read by the camera (3 or 4 bytes a pixel, red or blue first, rows from the
    /// bottom up as the engine keeps them) to the photo of about 1080 lines that is kept, and the photo to its small copy
    /// (Core/PhotoThumbs' format: 256 pixels wide, RGB24, rows from the bottom up). Each pixel of the result is the plain
    /// average of the block of the source it covers (a box filter), so a 4K screen is averaged two by two and nothing of
    /// it is skipped; a source no larger than the result is copied as it is.
    ///
    /// Game-free: spans of bytes in and out, so the camera's worker hands it the engine's own copy of the screen (outside the
    /// managed heap) and the offline checks hand it arrays (EconomyCheck, PhotoChecks.cs). Runs on a worker thread only; a
    /// 4K screen to 1080 lines is some 8 million reads, about 30 ms there. Nothing is allocated.
    /// </summary>
    public static class PhotoPixels
    {
        public const int PhotoWidth = 1920;     // pixels: the widest photo kept...
        public const int PhotoHeight = 1080;    // ...and the tallest: about 1080p
        public const int SmallWidth = PhotoThumbs.Width;   // the small copy, as Flare and the Gallery read it
        public const int SmallTallest = 1024;   // pixels: a small copy's height at most (a very tall screen)

        /// <summary>The size a picture is made to fit a box, its shape kept, never made larger: at least one pixel each way.</summary>
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

        /// <summary>The size of a photo's small copy: <see cref="SmallWidth"/> wide (or the photo's own width if it is narrower), its height in proportion.</summary>
        public static void SmallSize(int width, int height, out int smallWidth, out int smallHeight)
        {
            Fit(width, height, SmallWidth, SmallTallest, out smallWidth, out smallHeight);
        }

        /// <summary>
        /// A picture made smaller by averaging: from <paramref name="source"/> (rows of <paramref name="width"/> pixels of
        /// <paramref name="bytes"/> bytes, 3 or 4; <paramref name="bgr"/> for blue first) to RGB24 rows into
        /// <paramref name="into"/>, <paramref name="toWidth"/> by <paramref name="toHeight"/>. The rows stay in the order they
        /// came. False, with nothing written, when a size or a length does not add up.
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
