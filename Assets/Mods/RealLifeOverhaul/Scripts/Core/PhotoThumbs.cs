using System;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The 256-pixel copy of a player's photo that Flare shows, made once off the main thread when the photo is taken
    /// (or when Flare first wants it) and kept beside it in "Thumbs" as raw pixels. It is worked out as Graphics.Blit
    /// draws it: bilinear at each pixel's centre, in linear light, rows from the bottom, RGB24 (a graphics card may
    /// differ by one step in 255). The PNG is inflated a row at a time with two rows kept, so a 4K photo costs about
    /// 40 KB, not the 60 MB of a whole decode. The file is "RLOT", width, height (little-endian 32-bit), then the rows,
    /// written under its own name and moved into place. Game-free.
    /// </summary>
    public static class PhotoThumbs
    {
        public const int Width = 256;                 // pixels: the width Flare shows a picture at
        private const int WaitMs = 30000;             // milliseconds a new photo is waited for while the engine writes it
        private const int PollMs = 100;               // milliseconds between two looks at a photo being written
        private const int ReadBuffer = 65536;         // bytes: the file is read through a buffer this size
        private const string Magic = "RLOT";

        private static readonly float[] Linear = MakeLinear();

        public static string FileOf(string photo)
        {
            return Path.Combine(Path.GetDirectoryName(photo) ?? string.Empty, "Thumbs", Path.GetFileName(photo) + ".thumb");
        }

        /// <summary>
        /// Makes a new photo's copy on a worker thread once the engine has written it; a failure leaves it to be made
        /// when Flare first wants it.
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
                    // made when Flare first wants it
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
                    return;   // Flare asked for it meanwhile
                }
                byte[] small = FromPhoto(photo, out int width, out int height, out bool undecoded);
                if (small != null)
                {
                    Write(FileOf(photo), small, width, height);
                    return;
                }
                if (undecoded && File.Exists(photo) && Whole(photo))
                {
                    return;   // a PNG this does not read: the engine makes the copy when Flare wants it
                }
            }
        }

        // Whether a file ends with a PNG's IEND chunk: one that does not is still being written.
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
        /// A photo's small copy, read from its file or made and written. Null if the photo is missing or not whole yet,
        /// or (<paramref name="undecoded"/>) a PNG this does not read, left to the engine. Off the main thread.
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
                    return Small(stream, out width, out height, out undecoded);
                }
            }
            catch (IOException)
            {
                return null;   // being written
            }
            catch (UnauthorizedAccessException)
            {
                return null;
            }
        }

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

        /// <summary>Writes a small copy's file under a name of its own, then moves it into place.</summary>
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
                    File.Delete(file);   // one that was not whole
                }
                File.Move(temporary, file);
            }
            catch (IOException)
            {
                // another thread put one there meanwhile, or the disk refused: made again next time
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
                    // a stray part file is harmless
                }
            }
        }

        /// <summary>
        /// The small copy of a PNG, <see cref="Width"/> pixels wide and as tall as its shape gives (at least one). Null if
        /// the stream is not whole, or (<paramref name="undecoded"/>) a PNG this does not read or no wider than the copy.
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
                undecoded = true;   // a copy as wide as the photo samples at its edges: left to the engine
                return null;
            }
            int tw = Width, th = Math.Max(1, h * Width / w);
            // Each row of the copy (from the bottom) weighs texture row y0, the file's row h - 1 - y0, and the one above it.
            var lower = new int[th];
            var weight = new float[th];
            for (int y = 0; y < th; y++)
            {
                float ty = (y + 0.5f) / th * h - 0.5f;
                int y0 = (int)Math.Floor(ty);
                if (y0 < 0 || y0 + 1 >= h)
                {
                    undecoded = true;   // only a copy taller than its photo samples past the edge
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
                    undecoded = true;   // only a copy as wide as its photo samples at the edge
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
                    // 'row' is texture row y0, 'above' texture row y0 + 1
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

        /// <summary>
        /// A PNG read a row at a time: the header (<see cref="Open"/>), then each row as RGB from the top
        /// (<see cref="Next"/>); 8 bits a channel, not interlaced. Only the row and the one above it are held.
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

            /// <summary>Reads the signature and the header up to the first image data.</summary>
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

            /// <summary>The next row as RGB into <paramref name="rgb"/>; false if the data ends short or is damaged.</summary>
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

            // PNG's five filters: none, sub, up, average, Paeth.
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

            /// <summary>The IDAT chunks' bytes as one stream, their check sums skipped.</summary>
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
                        // the check sum, then the next chunk: more image data or the end of it
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
