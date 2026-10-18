using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

namespace RealLifeOverhaul.Flare
{
    /// <summary>
    /// The small copy of a picture sent to the other players of a Going Public session: at most 192 pixels wide and 16 KB,
    /// a few seconds of a quiet connection. The picture is shrunk by averaging, turned into brightness and two colours at
    /// half size (as a photograph's own format keeps them), each rounded to fewer steps, written as differences from the
    /// pixel before and deflated; rounded harder, then narrower, until it fits. Read back to RGB24, bottom row first.
    ///
    ///     "FS1" | rounding (byte) | width (ushort) | height (ushort) | the rest deflated: brightness, then the two colours
    ///
    /// It travels as Base64 in pieces of <see cref="ChunkChars"/> characters, a piece a second (<see cref="FlareSnapParts"/>).
    /// Game-free: it runs on a worker thread.
    /// </summary>
    public static class FlareSnap
    {
        public const int Width = 192;              // pixels: the widest small copy
        public const int TallMost = 192;           // pixels: the tallest
        public const int MostBytes = 16 * 1024;    // bytes: the largest small copy sent
        public const int ChunkChars = 3000;        // characters of Base64 in one piece (a line of a request)
        public const int PartsMost = 8;            // pieces of one picture at most: 16 KB is 22,000 characters
        private static readonly int[] YShift = { 2, 3, 3 };
        private static readonly int[] CShift = { 3, 4, 5 };
        private static readonly int[] Widths = { 192, 160, 128, 96 };

        /// <summary>
        /// A picture shrunk by averaging: from rows of <paramref name="bytes"/> bytes a pixel (3 or 4; <paramref name="bgr"/>
        /// for blue first) to RGB24 rows <paramref name="width"/> wide, no taller than <paramref name="tallMost"/>.
        /// </summary>
        public static byte[] Shrink(byte[] src, int w, int h, int bytes, bool bgr, int width, int tallMost, out int outW, out int outH)
        {
            outW = outH = 0;
            if (src == null || w <= 0 || h <= 0 || (bytes != 3 && bytes != 4) || src.Length < (long)w * h * bytes)
            {
                return null;
            }
            outW = Math.Max(1, Math.Min(width, w));
            outH = Math.Max(1, (int)Math.Round((double)h * outW / w));
            if (outH > tallMost)
            {
                outH = tallMost;
                outW = Math.Max(1, (int)Math.Round((double)w * outH / h));
            }
            var dst = new byte[outW * outH * 3];
            int r0 = bgr ? 2 : 0, b0 = bgr ? 0 : 2;
            for (int y = 0; y < outH; y++)
            {
                int ya = (int)((long)y * h / outH), yb = Math.Max(ya + 1, (int)((long)(y + 1) * h / outH));
                for (int x = 0; x < outW; x++)
                {
                    int xa = (int)((long)x * w / outW), xb = Math.Max(xa + 1, (int)((long)(x + 1) * w / outW));
                    long r = 0, g = 0, b = 0;
                    int n = 0;
                    for (int sy = ya; sy < yb && sy < h; sy++)
                    {
                        int row = sy * w * bytes;
                        for (int sx = xa; sx < xb && sx < w; sx++)
                        {
                            int at = row + sx * bytes;
                            r += src[at + r0];
                            g += src[at + 1];
                            b += src[at + b0];
                            n++;
                        }
                    }
                    int o = (y * outW + x) * 3;
                    n = Math.Max(1, n);
                    dst[o] = (byte)(r / n);
                    dst[o + 1] = (byte)(g / n);
                    dst[o + 2] = (byte)(b / n);
                }
            }
            return dst;
        }

        /// <summary>The small copy of RGB24 rows, packed, no more than <see cref="MostBytes"/>; null if it cannot be made.</summary>
        public static byte[] Encode(byte[] rgb, int w, int h)
        {
            if (rgb == null || w <= 0 || h <= 0 || rgb.Length < w * h * 3)
            {
                return null;
            }
            for (int s = 0; s < Widths.Length; s++)
            {
                int tw = w, th = h;
                byte[] small = rgb;
                if (w > Widths[s] || h > TallMost)
                {
                    small = Shrink(rgb, w, h, 3, false, Widths[s], TallMost, out tw, out th);
                }
                if (small == null)
                {
                    return null;
                }
                for (int level = 0; level < YShift.Length; level++)
                {
                    byte[] packed = Pack(small, tw, th, level);
                    if (packed != null && packed.Length <= MostBytes)
                    {
                        return packed;
                    }
                }
            }
            return null;
        }

        private static byte[] Pack(byte[] rgb, int w, int h, int level)
        {
            int cw = (w + 1) / 2, ch = (h + 1) / 2;
            var planes = new byte[w * h + cw * ch * 2];
            int ys = YShift[level], cs = CShift[level];
            // brightness, a sample a pixel
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int o = (y * w + x) * 3;
                    planes[y * w + x] = (byte)(Luma(rgb[o], rgb[o + 1], rgb[o + 2]) >> ys);
                }
            }
            // the two colours, a sample each for four pixels
            int cb0 = w * h, cr0 = cb0 + cw * ch;
            for (int y = 0; y < ch; y++)
            {
                for (int x = 0; x < cw; x++)
                {
                    int sb = 0, sr = 0, n = 0;
                    for (int dy = 0; dy < 2; dy++)
                    {
                        for (int dx = 0; dx < 2; dx++)
                        {
                            int px = x * 2 + dx, py = y * 2 + dy;
                            if (px < w && py < h)
                            {
                                int o = (py * w + px) * 3;
                                int l = Luma(rgb[o], rgb[o + 1], rgb[o + 2]);
                                sb += Clamp(128 + (rgb[o + 2] - l) * 564 / 1000);
                                sr += Clamp(128 + (rgb[o] - l) * 713 / 1000);
                                n++;
                            }
                        }
                    }
                    planes[cb0 + y * cw + x] = (byte)(sb / n >> cs);
                    planes[cr0 + y * cw + x] = (byte)(sr / n >> cs);
                }
            }
            Delta(planes, 0, w, h);
            Delta(planes, cb0, cw, ch);
            Delta(planes, cr0, cw, ch);
            using (var raw = new MemoryStream(planes.Length / 2 + 16))
            {
                raw.WriteByte((byte)'F');
                raw.WriteByte((byte)'S');
                raw.WriteByte((byte)'1');
                raw.WriteByte((byte)level);
                raw.WriteByte((byte)(w & 255));
                raw.WriteByte((byte)(w >> 8));
                raw.WriteByte((byte)(h & 255));
                raw.WriteByte((byte)(h >> 8));
                using (var deflate = new DeflateStream(raw, CompressionLevel.Optimal, true))
                {
                    deflate.Write(planes, 0, planes.Length);
                }
                return raw.ToArray();
            }
        }

        // Each sample as the difference from the one to its left (the first of a row from the one above it).
        private static void Delta(byte[] p, int at, int w, int h)
        {
            for (int y = h - 1; y >= 0; y--)
            {
                for (int x = w - 1; x >= 0; x--)
                {
                    int i = at + y * w + x;
                    int before = x > 0 ? p[i - 1] : y > 0 ? p[i - w] : 0;
                    p[i] = (byte)(p[i] - before);
                }
            }
        }

        private static void Undelta(byte[] p, int at, int w, int h)
        {
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int i = at + y * w + x;
                    int before = x > 0 ? p[i - 1] : y > 0 ? p[i - w] : 0;
                    p[i] = (byte)(p[i] + before);
                }
            }
        }

        /// <summary>A small copy read back to RGB24 rows; null for one that is damaged or not a small copy.</summary>
        public static byte[] Decode(byte[] packed, out int w, out int h)
        {
            w = h = 0;
            if (packed == null || packed.Length < 9 || packed.Length > MostBytes || packed[0] != 'F' || packed[1] != 'S' || packed[2] != '1')
            {
                return null;
            }
            int level = packed[3];
            w = packed[4] | packed[5] << 8;
            h = packed[6] | packed[7] << 8;
            if (level >= YShift.Length || w <= 0 || h <= 0 || w > Width || h > TallMost)
            {
                w = h = 0;
                return null;
            }
            int cw = (w + 1) / 2, ch = (h + 1) / 2;
            var planes = new byte[w * h + cw * ch * 2];
            try
            {
                using (var deflate = new DeflateStream(new MemoryStream(packed, 8, packed.Length - 8, false), CompressionMode.Decompress))
                {
                    int at = 0;
                    while (at < planes.Length)
                    {
                        int n = deflate.Read(planes, at, planes.Length - at);
                        if (n <= 0)
                        {
                            w = h = 0;
                            return null;
                        }
                        at += n;
                    }
                }
            }
            catch (Exception)
            {
                w = h = 0;
                return null;
            }
            int cb0 = w * h, cr0 = cb0 + cw * ch;
            Undelta(planes, 0, w, h);
            Undelta(planes, cb0, cw, ch);
            Undelta(planes, cr0, cw, ch);
            int ys = YShift[level], cs = CShift[level];
            var rgb = new byte[w * h * 3];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int l = (planes[y * w + x] << ys) + (1 << ys >> 1);
                    int ci = (y / 2) * cw + x / 2;
                    int cb = (planes[cb0 + ci] << cs) + (1 << cs >> 1) - 128;
                    int cr = (planes[cr0 + ci] << cs) + (1 << cs >> 1) - 128;
                    int o = (y * w + x) * 3;
                    rgb[o] = (byte)Clamp(l + cr * 1402 / 1000);
                    rgb[o + 1] = (byte)Clamp(l - cb * 344 / 1000 - cr * 714 / 1000);
                    rgb[o + 2] = (byte)Clamp(l + cb * 1772 / 1000);
                }
            }
            return rgb;
        }

        private static int Luma(int r, int g, int b)
        {
            return (299 * r + 587 * g + 114 * b) / 1000;
        }

        private static int Clamp(int v)
        {
            return v < 0 ? 0 : v > 255 ? 255 : v;
        }

        /// <summary>A small copy as pieces of text for the requests, into the list given (filled again).</summary>
        public static void Chunks(byte[] packed, List<string> into)
        {
            into.Clear();
            string all = Convert.ToBase64String(packed);
            for (int i = 0; i < all.Length; i += ChunkChars)
            {
                into.Add(all.Substring(i, Math.Min(ChunkChars, all.Length - i)));
            }
        }
    }

    /// <summary>The pieces of one small copy as they come in, put together once they are all there. Game-free.</summary>
    public sealed class FlareSnapParts
    {
        public readonly string Id;
        public readonly int Count;
        private readonly string[] _parts;
        private int _have;

        public FlareSnapParts(string id, int count)
        {
            Id = id;
            Count = Math.Max(1, Math.Min(FlareSnap.PartsMost, count));
            _parts = new string[Count];
        }

        public bool Whole => _have == Count;

        /// <summary>A piece by its number; a piece out of range, too long or already there is left out. True once all are there.</summary>
        public bool Add(int index, string text)
        {
            if (index < 0 || index >= Count || string.IsNullOrEmpty(text) || text.Length > FlareSnap.ChunkChars || _parts[index] != null)
            {
                return Whole;
            }
            _parts[index] = text;
            _have++;
            return Whole;
        }

        /// <summary>The small copy, once whole and readable; null otherwise.</summary>
        public byte[] Bytes()
        {
            if (!Whole)
            {
                return null;
            }
            try
            {
                byte[] bytes = Convert.FromBase64String(string.Concat(_parts));
                return bytes.Length <= FlareSnap.MostBytes ? bytes : null;
            }
            catch (FormatException)
            {
                return null;
            }
        }

        /// <summary>The pieces as text again: the host passes the picture on as it came.</summary>
        public string Text()
        {
            return Whole ? string.Concat(_parts) : null;
        }
    }
}
