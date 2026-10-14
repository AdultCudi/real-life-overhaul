using System;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The small copy of a photo the player took (Leisure/Photo/PhotoMode), 256 pixels wide, that Flare shows in a post
    /// (Social/Flare/FlareApp.Pictures.cs). A photo is a full-screen PNG of 3 to 8 MB; the small copy was once made from it
    /// on the main thread every time Flare was opened: the file read whole, decoded by the engine (40 to 150 ms) and drawn
    /// small. Now it is made once, off the main thread, when the photo is taken (for a photo taken before this, the first
    /// time Flare wants it), and kept beside the photo in "Thumbs" as its raw pixels; showing it is one read of some 100 KB
    /// off the main thread and one upload of those pixels on it.
    ///
    /// THE SAME PICTURE. The small copy is worked out as the engine drew it before: the PNG read row by row to its 8-bit
    /// pixels (<see cref="PngRows"/>: every filter of the format; grey, grey and alpha, colour, colour and alpha at 8 bits a
    /// channel, not interlaced; anything else is left to the engine, as before), and each pixel of the copy sampled at its
    /// centre between the four nearest pixels of the photo (what Graphics.Blit of a texture without mipmaps to a smaller
    /// target does), in linear light (the texture and the target are sRGB in a linear project: each pixel made linear
    /// before the four are weighed, the result sRGB again), rounded to 8 bits, rows from the bottom up as the engine keeps
    /// them, without alpha (the copy is RGB24). A graphics card weighs the four with fewer bits than this does: a channel
    /// may differ by one step in 255. A photo no wider than the copy (256 pixels or less) is left to the engine.
    ///
    /// MEMORY. The photo is never held whole: the file is read through a small buffer, inflated a row at a time, and only
    /// two rows of it are kept, so a 4K photo costs some 40 KB of managed memory to shrink, not the 60 MB a whole decode
    /// would (an allocation that size on the Boehm heap would bring on a collection, a pause of the main thread).
    ///
    /// The file: "RLOT", the width and the height (little-endian 32-bit), then the rows. Written under a name of its own and
    /// then moved into place, so a reader never finds half of one; one that is not whole is made again.
    ///
    /// Game-free: no Unity or game type; the offline checks run it as it is (EconomyCheck, AppsChecks.cs). Nothing here
    /// runs on the main thread but the call that starts the work.
    /// </summary>
    public static class PhotoThumbs
    {
        public const int Width = 256;                 // pixels: the width Flare shows a picture at (FlareRules.ThumbWidth)
        private const int WaitMs = 30000;             // milliseconds a new photo is waited for while the engine writes it
        private const int PollMs = 100;               // milliseconds between two looks at a photo being written
        private const int ReadBuffer = 65536;         // bytes: the file is read through a buffer this size
        private const string Magic = "RLOT";

        private static readonly float[] Linear = MakeLinear();

        /// <summary>The small copy's file for a photo: beside it, in "Thumbs".</summary>
        public static string FileOf(string photo)
        {
            return Path.Combine(Path.GetDirectoryName(photo) ?? string.Empty, "Thumbs", Path.GetFileName(photo) + ".thumb");
        }

        /// <summary>
        /// A photo just taken: its small copy is made on a worker thread once the engine has written the photo (at the end
        /// of the frame it was taken in). Nothing is done on the calling thread but starting it; a failure leaves the copy
        /// to be made when Flare first wants it.
        /// </summary>
        public static void MakeSoon(string photo)
        {
            if (string.IsNullOrEmpty(photo))
            {
                return;
            }
            Task.Run(() =>
            {
                try
                {
                    MakeWhenWritten(photo);
                }
                catch (Exception)
                {
                    // (left to be made when Flare first wants it)
                }
            });
        }

        private static void MakeWhenWritten(string photo)
        {
            for (int waited = 0; waited < WaitMs; waited += PollMs)
            {
                Thread.Sleep(PollMs);
                if (Read(FileOf(photo), out _, out _) != null)
                {
                    return;   // made meanwhile (Flare asked for it)
                }
                byte[] small = FromPhoto(photo, out int width, out int height, out bool undecoded);
                if (small != null)
                {
                    Write(FileOf(photo), small, width, height);
                    return;
                }
                if (undecoded && File.Exists(photo) && Whole(photo))
                {
                    return;   // a PNG this does not read: the engine makes the copy when Flare wants it, as before
                }
            }
        }

        // Whether a file ends as a PNG does (its last chunk, IEND, written): one not yet whole is still being written.
        private static bool Whole(string file)
        {
            try
            {
                using (var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    if (stream.Length < 12)
                    {
                        return false;
                    }
                    stream.Seek(-8, SeekOrigin.End);
                    return stream.ReadByte() == 'I' && stream.ReadByte() == 'E' && stream.ReadByte() == 'N' && stream.ReadByte() == 'D';
                }
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }

        /// <summary>
        /// A photo's small copy: read from its file, or made from the photo and its file written. Null if the photo is not
        /// there or not whole yet, or ('undecoded') is a PNG this does not read: the caller then lets the engine make it, as
        /// before. Run off the main thread.
        /// </summary>
        public static byte[] Get(string photo, out int width, out int height, out bool undecoded)
        {
            undecoded = false;
            string file = FileOf(photo);
            byte[] kept = Read(file, out width, out height);
            if (kept != null)
            {
                return kept;
            }
            byte[] small = FromPhoto(photo, out width, out height, out undecoded);
            if (small != null)
            {
                Write(file, small, width, height);
            }
            return small;
        }

        private static byte[] FromPhoto(string photo, out int width, out int height, out bool undecoded)
        {
            width = height = 0;
            undecoded = false;
            try
            {
                if (!File.Exists(photo))
                {
                    return null;
                }
                using (var stream = new FileStream(photo, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, ReadBuffer))
                {
                    byte[] small = Small(stream, out width, out height, out undecoded);
                    return small;
                }
            }
            catch (IOException)
            {
                return null;   // (being written)
            }
            catch (UnauthorizedAccessException)
            {
                return null;
            }
        }

        // ---------------------------------------------------------------- the small copy's file

        /// <summary>A small copy's rows from its file, or null if there is none or it is not whole.</summary>
        public static byte[] Read(string file, out int width, out int height)
        {
            width = height = 0;
            try
            {
                if (!File.Exists(file))
                {
                    return null;
                }
                byte[] all = File.ReadAllBytes(file);
                if (all.Length < 12 || all[0] != Magic[0] || all[1] != Magic[1] || all[2] != Magic[2] || all[3] != Magic[3])
                {
                    return null;
                }
                int w = BitConverter.ToInt32(all, 4), h = BitConverter.ToInt32(all, 8);
                if (w <= 0 || h <= 0 || w > 4096 || h > 4096 || all.Length != 12 + w * h * 3)
                {
                    return null;
                }
                width = w;
                height = h;
                var rows = new byte[w * h * 3];
                Buffer.BlockCopy(all, 12, rows, 0, rows.Length);
                return rows;
            }
            catch (IOException)
            {
                return null;
            }
            catch (UnauthorizedAccessException)
            {
                return null;
            }
        }

        /// <summary>Writes a small copy's file whole: under a name of its own first, then moved into place.</summary>
        public static void Write(string file, byte[] rows, int width, int height)
        {
            string temporary = file + "." + Guid.NewGuid().ToString("N") + ".part";
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(file) ?? ".");
                var all = new byte[12 + rows.Length];
                all[0] = (byte)Magic[0];
                all[1] = (byte)Magic[1];
                all[2] = (byte)Magic[2];
                all[3] = (byte)Magic[3];
                Buffer.BlockCopy(BitConverter.GetBytes(width), 0, all, 4, 4);
                Buffer.BlockCopy(BitConverter.GetBytes(height), 0, all, 8, 4);
                Buffer.BlockCopy(rows, 0, all, 12, rows.Length);
                File.WriteAllBytes(temporary, all);
                if (File.Exists(file))
                {
                    File.Delete(file);   // (one that was not whole)
                }
                File.Move(temporary, file);
            }
            catch (IOException)
            {
                // (another thread put one there meanwhile, or the disk refused: it is made again next time)
            }
            catch (UnauthorizedAccessException)
            {
            }
            finally
            {
                try
                {
                    if (File.Exists(temporary))
                    {
                        File.Delete(temporary);
                    }
                }
                catch (Exception)
                {
                    // (a stray part file is harmless)
                }
            }
        }

        // ---------------------------------------------------------------- the small copy

        /// <summary>
        /// The small copy of a PNG read from a stream: <see cref="Width"/> pixels wide and as tall as its shape gives (the
        /// photo's height times that width over its width, in whole pixels, at least one), RGB, rows from the bottom up, each
        /// pixel the photo sampled bilinearly at its centre in linear light (see the class's summary). Null if the stream is
        /// not whole, or ('undecoded') is a PNG this does not read or a photo narrower than the copy.
        /// </summary>
        public static byte[] Small(Stream png, out int width, out int height, out bool undecoded)
        {
            width = height = 0;
            undecoded = false;
            var reader = new PngRows(png);
            PngRows.Opened opened = reader.Open();
            if (opened != PngRows.Opened.Yes)
            {
                undecoded = opened == PngRows.Opened.NotRead;
                return null;
            }
            int w = reader.Width, h = reader.Height;
            if (w <= Width)
            {
                undecoded = true;   // a copy as large as the photo or larger samples at its edges: left to the engine
                return null;
            }
            int tw = Width, th = Math.Max(1, h * Width / w);
            // Each row of the copy (from the bottom) weighs two neighbouring rows of the photo: the engine's texture row y0,
            // which is the file's row h - 1 - y0, and the one above it in the picture, the file's row before it.
            var lower = new int[th];
            var weight = new float[th];
            for (int y = 0; y < th; y++)
            {
                float ty = (y + 0.5f) / th * h - 0.5f;
                int y0 = (int)Math.Floor(ty);
                if (y0 < 0 || y0 + 1 >= h)
                {
                    undecoded = true;   // (only a copy taller than its photo samples past the edge)
                    return null;
                }
                lower[y] = h - 1 - y0;
                weight[y] = ty - y0;
            }
            var left = new int[tw];
            var across = new float[tw];
            for (int x = 0; x < tw; x++)
            {
                float tx = (x + 0.5f) / tw * w - 0.5f;
                int x0 = (int)Math.Floor(tx);
                if (x0 < 0 || x0 + 1 >= w)
                {
                    undecoded = true;   // (only a copy as wide as its photo samples at the edge)
                    return null;
                }
                left[x] = x0;
                across[x] = tx - x0;
            }
            var small = new byte[tw * th * 3];
            var above = new byte[w * 3];
            var row = new byte[w * 3];
            int next = th - 1;   // the copy's rows are met from the top of the picture: its last row first
            for (int r = 0; r < h && next >= 0; r++)
            {
                if (!reader.Next(row))
                {
                    return null;   // cut short (still being written, or damaged)
                }
                while (next >= 0 && lower[next] == r)
                {
                    // 'row' is the engine's texture row y0, 'above' the row over it in the picture (texture row y0 + 1)
                    float fy = weight[next];
                    int to = next * tw * 3;
                    for (int x = 0; x < tw; x++)
                    {
                        int c0 = left[x] * 3, c1 = c0 + 3;
                        float fx = across[x];
                        for (int k = 0; k < 3; k++)
                        {
                            float bottom = Linear[row[c0 + k]] * (1f - fx) + Linear[row[c1 + k]] * fx;
                            float top = Linear[above[c0 + k]] * (1f - fx) + Linear[above[c1 + k]] * fx;
                            small[to + x * 3 + k] = Encode(bottom * (1f - fy) + top * fy);
                        }
                    }
                    next--;
                }
                byte[] swap = above;
                above = row;
                row = swap;
            }
            if (next >= 0)
            {
                return null;
            }
            width = tw;
            height = th;
            return small;
        }

        private static float[] MakeLinear()
        {
            var table = new float[256];
            for (int i = 0; i < 256; i++)
            {
                double c = i / 255.0;
                table[i] = (float)(c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4));
            }
            return table;
        }

        // Linear light back to an sRGB byte, rounded to the nearest.
        private static byte Encode(float linear)
        {
            double l = linear <= 0f ? 0.0 : linear >= 1f ? 1.0 : linear;
            double c = l <= 0.0031308 ? l * 12.92 : 1.055 * Math.Pow(l, 1.0 / 2.4) - 0.055;
            int v = (int)(c * 255.0 + 0.5);
            return (byte)(v < 0 ? 0 : v > 255 ? 255 : v);
        }

        // ---------------------------------------------------------------- the PNG, a row at a time

        /// <summary>
        /// A PNG read a row at a time from a stream: its header first (<see cref="Open"/>), then each row's pixels as RGB, from
        /// the top (<see cref="Next"/>). Only the image data's chunks are inflated, through a stream of their own; nothing is
        /// held but the row being read and the one above it.
        /// </summary>
        public sealed class PngRows
        {
            public enum Opened { Yes, NotRead, NotWhole }

            private readonly Stream _file;
            private Stream _inflate;
            private ChunkData _data;
            private byte[] _raw, _up, _row;
            private int _channels, _stride, _rows;

            public int Width { get; private set; }
            public int Height { get; private set; }

            public PngRows(Stream file)
            {
                _file = file;
            }

            /// <summary>Reads the signature and the header, up to the first image data.</summary>
            public Opened Open()
            {
                var head = new byte[8];
                if (!Fill(_file, head, 8))
                {
                    return Opened.NotWhole;
                }
                if (head[0] != 0x89 || head[1] != (byte)'P' || head[2] != (byte)'N' || head[3] != (byte)'G')
                {
                    return Opened.NotRead;
                }
                var chunk = new byte[13];
                int depth = 0, colour = -1;
                while (true)
                {
                    if (!Fill(_file, head, 8))
                    {
                        return Opened.NotWhole;
                    }
                    int length = (head[0] << 24) | (head[1] << 16) | (head[2] << 8) | head[3];
                    if (length < 0)
                    {
                        return Opened.NotRead;
                    }
                    if (Is(head, "IHDR"))
                    {
                        if (length != 13 || !Fill(_file, chunk, 13) || !Skip(_file, 4))
                        {
                            return Opened.NotWhole;
                        }
                        Width = (chunk[0] << 24) | (chunk[1] << 16) | (chunk[2] << 8) | chunk[3];
                        Height = (chunk[4] << 24) | (chunk[5] << 16) | (chunk[6] << 8) | chunk[7];
                        depth = chunk[8];
                        colour = chunk[9];
                        if (chunk[10] != 0 || chunk[11] != 0 || chunk[12] != 0)
                        {
                            return Opened.NotRead;   // another compression or filter method, or interlaced
                        }
                        continue;
                    }
                    if (Is(head, "IDAT"))
                    {
                        _channels = colour == 0 ? 1 : colour == 2 ? 3 : colour == 4 ? 2 : colour == 6 ? 4 : 0;
                        if (depth != 8 || _channels == 0 || Width <= 0 || Height <= 0 || Width > 16384 || Height > 16384)
                        {
                            return Opened.NotRead;
                        }
                        _data = new ChunkData(_file, length);
                        int cmf = _data.ReadByte(), flg = _data.ReadByte();
                        if (cmf < 0 || flg < 0)
                        {
                            return Opened.NotWhole;
                        }
                        if ((cmf & 0x0F) != 8 || (flg & 0x20) != 0)
                        {
                            return Opened.NotRead;   // not deflate, or a preset dictionary
                        }
                        _inflate = new DeflateStream(_data, CompressionMode.Decompress);
                        _stride = Width * _channels;
                        _raw = new byte[_stride + 1];
                        _up = new byte[_stride];
                        _row = new byte[_stride];
                        return Opened.Yes;
                    }
                    if (Is(head, "IEND"))
                    {
                        return Opened.NotRead;   // no image data at all
                    }
                    if (!Skip(_file, length + 4))
                    {
                        return Opened.NotWhole;
                    }
                }
            }

            /// <summary>The next row's pixels as RGB (3 bytes a pixel) into 'rgb'; false if the data ends short or is damaged.</summary>
            public bool Next(byte[] rgb)
            {
                if (_inflate == null || _rows >= Height)
                {
                    return false;
                }
                try
                {
                    if (!Fill(_inflate, _raw, _raw.Length))
                    {
                        return false;
                    }
                }
                catch (InvalidDataException)
                {
                    return false;
                }
                catch (IOException)
                {
                    return false;
                }
                if (!Unfilter(_raw[0]))
                {
                    return false;
                }
                int channels = _channels;
                for (int x = 0; x < Width; x++)
                {
                    int from = x * channels, to = x * 3;
                    if (channels >= 3)
                    {
                        rgb[to] = _row[from];
                        rgb[to + 1] = _row[from + 1];
                        rgb[to + 2] = _row[from + 2];
                    }
                    else
                    {
                        rgb[to] = rgb[to + 1] = rgb[to + 2] = _row[from];
                    }
                }
                byte[] swap = _up;
                _up = _row;
                _row = swap;
                _rows++;
                return true;
            }

            // The row out of its filter (PNG's five: none, sub, up, average, Paeth), against the row above it.
            private bool Unfilter(int filter)
            {
                byte[] raw = _raw, row = _row, up = _up;
                int stride = _stride, bpp = _channels;
                switch (filter)
                {
                    case 0:
                        Buffer.BlockCopy(raw, 1, row, 0, stride);
                        return true;
                    case 1:
                        for (int i = 0; i < stride; i++)
                        {
                            row[i] = (byte)(raw[i + 1] + (i >= bpp ? row[i - bpp] : 0));
                        }
                        return true;
                    case 2:
                        for (int i = 0; i < stride; i++)
                        {
                            row[i] = (byte)(raw[i + 1] + up[i]);
                        }
                        return true;
                    case 3:
                        for (int i = 0; i < stride; i++)
                        {
                            int left = i >= bpp ? row[i - bpp] : 0;
                            row[i] = (byte)(raw[i + 1] + ((left + up[i]) >> 1));
                        }
                        return true;
                    case 4:
                        for (int i = 0; i < stride; i++)
                        {
                            int left = i >= bpp ? row[i - bpp] : 0, above = up[i], corner = i >= bpp ? up[i - bpp] : 0;
                            int p = left + above - corner;
                            int pa = Math.Abs(p - left), pb = Math.Abs(p - above), pc = Math.Abs(p - corner);
                            int pick = pa <= pb && pa <= pc ? left : pb <= pc ? above : corner;
                            row[i] = (byte)(raw[i + 1] + pick);
                        }
                        return true;
                    default:
                        return false;
                }
            }

            private static bool Is(byte[] head, string type)
            {
                return head[4] == type[0] && head[5] == type[1] && head[6] == type[2] && head[7] == type[3];
            }

            private static bool Fill(Stream from, byte[] into, int count)
            {
                int got = 0;
                while (got < count)
                {
                    int n = from.Read(into, got, count - got);
                    if (n <= 0)
                    {
                        return false;
                    }
                    got += n;
                }
                return true;
            }

            private static bool Skip(Stream from, int count)
            {
                var scratch = new byte[Math.Min(count, 4096)];
                while (count > 0)
                {
                    int n = from.Read(scratch, 0, Math.Min(count, scratch.Length));
                    if (n <= 0)
                    {
                        return false;
                    }
                    count -= n;
                }
                return true;
            }

            /// <summary>The image data: one IDAT chunk's bytes after another, as one stream (each chunk's check sum passed over).</summary>
            private sealed class ChunkData : Stream
            {
                private readonly Stream _file;
                private readonly byte[] _head = new byte[8];
                private int _left;
                private bool _ended;

                public ChunkData(Stream file, int first)
                {
                    _file = file;
                    _left = first;
                }

                public override int Read(byte[] buffer, int offset, int count)
                {
                    while (_left == 0 && !_ended)
                    {
                        // the chunk's check sum, then the next chunk: more image data, or the end of it
                        if (!Skip(_file, 4) || !Fill(_file, _head, 8))
                        {
                            _ended = true;
                            break;
                        }
                        _left = (_head[0] << 24) | (_head[1] << 16) | (_head[2] << 8) | _head[3];
                        if (!Is(_head, "IDAT") || _left < 0)
                        {
                            _left = 0;
                            _ended = true;
                        }
                    }
                    if (_left == 0)
                    {
                        return 0;
                    }
                    int n = _file.Read(buffer, offset, Math.Min(count, _left));
                    if (n <= 0)
                    {
                        _ended = true;
                        _left = 0;
                        return 0;
                    }
                    _left -= n;
                    return n;
                }

                public override int ReadByte()
                {
                    var one = new byte[1];
                    return Read(one, 0, 1) == 1 ? one[0] : -1;
                }

                public override bool CanRead => true;
                public override bool CanSeek => false;
                public override bool CanWrite => false;
                public override long Length => throw new NotSupportedException();
                public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
                public override void Flush() { }
                public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
                public override void SetLength(long value) => throw new NotSupportedException();
                public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
            }
        }
    }
}
