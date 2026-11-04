using System;

namespace RealLifeOverhaul.Photos
{
    /// <summary>
    /// A photo's pixels made smaller by a box filter: the screen to the photo kept (cut to the shape the camera was set to), and
    /// the photo to its small copy (RGB24, rows bottom up). Then, for the camera and for an edit after the shot: a frame burned
    /// in, a colour grade as the camera's filters are, a turn by quarters. Game-free spans in and out, so the worker hands it the
    /// engine's own buffer. Worker thread only: a 4K screen is about 30 ms, a grade or a frame some 10 ms; nothing is allocated.
    /// </summary>
    public static class PhotoPixels
    {
        public const int PhotoWidth = 1920;     // pixels
        public const int PhotoHeight = 1080;    // pixels
        public const int SmallWidth = PhotoThumbs.Width;
        public const int SmallTallest = 1024;   // pixels, for a very tall screen
        public const int PhotoLong = 1920;      // pixels: the longest side of a photo of any shape, so every shape is the 1080p size class

        // Paper, as the viewfinder draws a frame's white (0.97, 0.96, 0.93).
        private const byte PaperR = 247, PaperG = 245, PaperB = 237;

        /// <summary>The box a shape of photo fits: 1920 by 1080, or 1080 by 1920 for a tall shape (the same pixels).</summary>
        public static void Box(CameraAspect aspect, out int mostWidth, out int mostHeight)
        {
            bool tall = CameraRules.AspectRatio(aspect) < 1f;
            mostWidth = tall ? PhotoHeight : PhotoWidth;
            mostHeight = tall ? PhotoWidth : PhotoHeight;
        }

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

        /// <summary>Averages 3 or 4 bytes a pixel into RGB24 rows, keeping the row order; false, nothing written, if a size does not add up.</summary>
        public static bool Shrink(ReadOnlySpan<byte> source, int width, int height, int bytes, bool bgr, Span<byte> into, int toWidth, int toHeight)
        {
            return Shrink(source, width, height, bytes, bgr, into, toWidth, toHeight, 0, 0, width, height);
        }

        /// <summary>
        /// The same cut to a rectangle of the source: only the pixels of (<paramref name="cropX"/>, <paramref name="cropY"/>, <paramref name="cropWidth"/>,
        /// <paramref name="cropHeight"/>) are averaged into the output, which is <paramref name="toWidth"/> by <paramref name="toHeight"/>. False,
        /// nothing written, if the rectangle is not in the source or the sizes do not add up.
        /// </summary>
        public static bool Shrink(ReadOnlySpan<byte> source, int width, int height, int bytes, bool bgr, Span<byte> into, int toWidth, int toHeight,
            int cropX, int cropY, int cropWidth, int cropHeight)
        {
            if (width <= 0 || height <= 0 || toWidth <= 0 || toHeight <= 0 || (bytes != 3 && bytes != 4)
                || cropX < 0 || cropY < 0 || cropWidth <= 0 || cropHeight <= 0 || cropX + cropWidth > width || cropY + cropHeight > height
                || toWidth > cropWidth || toHeight > cropHeight
                || source.Length < (long)width * height * bytes || into.Length < (long)toWidth * toHeight * 3)
            {
                return false;
            }
            int r0 = bgr ? 2 : 0, b0 = bgr ? 0 : 2;
            int stride = width * bytes;
            for (int y = 0; y < toHeight; y++)
            {
                int ya = cropY + (int)((long)y * cropHeight / toHeight), yb = Math.Max(ya + 1, cropY + (int)((long)(y + 1) * cropHeight / toHeight));
                int to = y * toWidth * 3;
                for (int x = 0; x < toWidth; x++)
                {
                    int xa = cropX + (int)((long)x * cropWidth / toWidth), xb = Math.Max(xa + 1, cropX + (int)((long)(x + 1) * cropWidth / toWidth));
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

        // ---------------------------------------------------------------------------- frames

        /// <summary>
        /// Burns a frame into an RGB24 picture (rows bottom up, as the engine gives them): a Polaroid's border deeper at the foot, a
        /// film's bars above and below, or a thin border all round. False, nothing changed, for no frame or a picture too small.
        /// </summary>
        public static bool Frame(Span<byte> rgb, int width, int height, CameraFrame frame)
        {
            if (frame == CameraFrame.None || width < 16 || height < 16 || rgb.Length < (long)width * height * 3)
            {
                return false;
            }
            int top, foot, side;
            bool paper = true;
            switch (frame)
            {
                case CameraFrame.Polaroid:
                    top = side = (int)Math.Round(height * CameraRules.BorderSide);
                    foot = (int)Math.Round(height * CameraRules.BorderFoot);
                    break;
                case CameraFrame.Border:
                    top = foot = side = Math.Max(1, (int)Math.Round(Math.Min(width, height) * CameraRules.EdgeSide));
                    break;
                case CameraFrame.Film:
                    paper = false;
                    top = foot = (int)Math.Round(height * CameraRules.FilmBar((float)width / height));
                    side = 0;
                    break;
                default:
                    return false;
            }
            if (top <= 0 && foot <= 0 && side <= 0)
            {
                return false;
            }
            byte r = paper ? PaperR : (byte)0, g = paper ? PaperG : (byte)0, b = paper ? PaperB : (byte)0;
            for (int row = 0; row < height; row++)
            {
                int fromTop = height - 1 - row;   // rows are bottom up
                bool bar = fromTop < top || row < foot;
                int at = row * width * 3;
                if (bar)
                {
                    for (int x = 0; x < width; x++, at += 3)
                    {
                        rgb[at] = r;
                        rgb[at + 1] = g;
                        rgb[at + 2] = b;
                    }
                }
                else if (side > 0)
                {
                    for (int x = 0; x < side; x++)
                    {
                        int l = at + x * 3, rr = at + (width - 1 - x) * 3;
                        rgb[l] = r; rgb[l + 1] = g; rgb[l + 2] = b;
                        rgb[rr] = r; rgb[rr + 1] = g; rgb[rr + 2] = b;
                    }
                }
            }
            return true;
        }

        // ---------------------------------------------------------------------------- grade

        /// <summary>
        /// Grades an RGB24 picture as the camera's filter does on screen, as near as the CPU's arithmetic gets to the engine's colour
        /// adjustments: the tint, the exposure in stops (the filter's own and <paramref name="extraStops"/>), the saturation, then the
        /// contrast about the middle grey. False, nothing changed, when the filter is the natural one with no exposure.
        /// </summary>
        public static bool Grade(Span<byte> rgb, int width, int height, CameraFilter f, float extraStops)
        {
            float stops = f.Exposure + extraStops;
            bool natural = f.Saturation == 0f && f.Contrast == 0f && stops == 0f && f.R >= 1f && f.G >= 1f && f.B >= 1f;
            if (natural || width <= 0 || height <= 0 || rgb.Length < (long)width * height * 3)
            {
                return false;
            }
            float gain = (float)Math.Pow(2.0, stops) / 255f;
            float gr = gain * f.R, gg = gain * f.G, gb = gain * f.B;
            float sat = 1f + f.Saturation / 100f, con = 1f + f.Contrast / 100f;
            int n = width * height * 3;
            for (int i = 0; i < n; i += 3)
            {
                float r = rgb[i] * gr, g = rgb[i + 1] * gg, b = rgb[i + 2] * gb;
                float luma = 0.2126f * r + 0.7152f * g + 0.0722f * b;
                r = luma + (r - luma) * sat;
                g = luma + (g - luma) * sat;
                b = luma + (b - luma) * sat;
                r = (r - 0.5f) * con + 0.5f;
                g = (g - 0.5f) * con + 0.5f;
                b = (b - 0.5f) * con + 0.5f;
                rgb[i] = Byte(r);
                rgb[i + 1] = Byte(g);
                rgb[i + 2] = Byte(b);
            }
            return true;
        }

        private static byte Byte(float v)
        {
            return v <= 0f ? (byte)0 : v >= 1f ? (byte)255 : (byte)(v * 255f + 0.5f);
        }

        // ---------------------------------------------------------------------------- turn

        /// <summary>The size of a picture turned by quarters (to the right): the sides swap for one and three.</summary>
        public static void Turned(int width, int height, int quarters, out int turnedWidth, out int turnedHeight)
        {
            bool swap = (((quarters % 4) + 4) % 4) % 2 == 1;
            turnedWidth = swap ? height : width;
            turnedHeight = swap ? width : height;
        }

        /// <summary>
        /// Turns an RGB24 picture (rows bottom up) by quarters to the right (clockwise as it is seen), into a buffer of the turned
        /// size. False, nothing written, if a size does not add up. Nothing is allocated.
        /// </summary>
        public static bool Turn(ReadOnlySpan<byte> source, int width, int height, int quarters, Span<byte> into)
        {
            int q = ((quarters % 4) + 4) % 4;
            long size = (long)width * height * 3;
            if (width <= 0 || height <= 0 || source.Length < size || into.Length < size)
            {
                return false;
            }
            Turned(width, height, q, out int tw, out int th);
            for (int yd = 0; yd < th; yd++)             // yd, ys: rows seen from the top
            {
                int rowD = th - 1 - yd;
                for (int xd = 0; xd < tw; xd++)
                {
                    int xs, ys;
                    switch (q)
                    {
                        case 1: xs = yd; ys = height - 1 - xd; break;
                        case 2: xs = width - 1 - xd; ys = height - 1 - yd; break;
                        case 3: xs = width - 1 - yd; ys = xd; break;
                        default: xs = xd; ys = yd; break;
                    }
                    int from = ((height - 1 - ys) * width + xs) * 3, to = (rowD * tw + xd) * 3;
                    into[to] = source[from];
                    into[to + 1] = source[from + 1];
                    into[to + 2] = source[from + 2];
                }
            }
            return true;
        }
    }
}
