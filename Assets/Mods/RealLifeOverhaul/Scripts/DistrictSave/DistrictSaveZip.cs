using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Threading;

namespace RealLifeOverhaul.DistrictSave
{
    /// <summary>
    /// The district save's payload compressed on several processors while the game's serializer is still writing it: the
    /// stream the serializer writes into cuts its bytes into pieces of <see cref="PieceBytes"/>, each carrying the last
    /// <see cref="DistrictSaveDeflate.Window"/> bytes of the one before (which its matches may reach into, as the reader
    /// of the whole stream has them), and each full piece is compressed at once on a worker (DistrictSaveDeflate) and read
    /// back there by the runtime's own inflater (the game's zlib, the one every load reads the payload with), behind a
    /// stored block of the bytes before it, so that a piece that would not read back the same is found before anything is
    /// written. The caller's thread takes the pieces no worker has taken. The pieces in order, framed as one gzip, become
    /// the store's line in one string. The pieces are cut by byte count alone, so the same records always give the same
    /// line, whatever the machine. Its buffers are kept for the next save. COST: nothing per frame; at a save of a game with
    /// a district, the copy of the serializer's bytes and the frame on the caller's thread, the rest on workers; every
    /// worker is done before <see cref="Finish"/> returns.
    /// </summary>
    public sealed class DistrictSaveZip : Stream
    {
        public const int PieceBytes = 128 * 1024;   // bytes: 29 pieces for the owner's 3.6 MB district, a millisecond or two of one worker each
        private const int Before = DistrictSaveDeflate.Window;   // bytes of the piece before carried at the front of each piece
        private const int Header = 10;              // the gzip frame's head
        private const int Stored = 5;               // a stored block's head: its first byte, and its length and the length's complement
        private const int Tail = 10;                // the last block and the frame's check value and length
        private const int MostWorkers = 6;          // the save's workers at most beside the caller's thread: with four, pieces were still queued when the serializer ended

        private sealed class Part
        {
            public byte[] Raw;                     // the bytes before, then the piece's own; given back once the piece is compressed
            public int From;                       // where the piece's own bytes start in Raw (0 for the first)
            public int Length;                     // the piece's own bytes
            public byte[] Packed = new byte[0];    // its deflate bytes, kept for the next save
            public int Size;
            public uint Crc;                       // of its own bytes
        }

        private readonly List<Part> _parts = new List<Part>(32);
        private readonly Queue<Part> _ready = new Queue<Part>(32);
        private readonly Stack<byte[]> _free = new Stack<byte[]>(8);
        private readonly Stack<DistrictSaveDeflate.Scratch> _spare = new Stack<DistrictSaveDeflate.Scratch>(MostWorkers + 1);
        private readonly object _gate = new object();
        private readonly CountdownEvent _left = new CountdownEvent(1);
        private readonly WaitCallback _run;
        private readonly int _workers;
        private readonly uint[] _even = new uint[32];
        private readonly uint[] _odd = new uint[32];
        private readonly uint[] _full = new uint[32];   // carries a CRC across a full piece
        private byte[] _gzip = new byte[0];
        private char[] _chars = new char[0];
        private int _used;        // parts in use for this payload
        private long _total;      // bytes written in
        private int _running;     // workers posted and not yet done
        private string _fault;

        /// <param name="workers">Workers beside the caller's thread; 0 does every piece on the caller's thread (the same line).</param>
        public DistrictSaveZip(int workers)
        {
            _workers = Math.Max(0, Math.Min(MostWorkers, workers));
            _run = Run;
            DistrictSaveDeflate.Shift(PieceBytes, _full, _even, _odd);
        }

        /// <summary>Workers for this machine: one fewer than its processors (the main thread is one), at most <see cref="MostWorkers"/>.</summary>
        public static int WorkersHere => Math.Max(0, Math.Min(MostWorkers, Environment.ProcessorCount - 1));

        /// <summary>The workers this one posts at most, beside the caller's thread.</summary>
        public int Workers => _workers;

        /// <summary>Why the last payload could not be made this way (a piece that did not read back the same, a worker's fault); null when it was.</summary>
        public string Fault => _fault;

        /// <summary>The bytes the serializer wrote in for the last payload.</summary>
        public long Written => _total;

        /// <summary>The last payload's gzip bytes, before base64.</summary>
        public int Packed { get; private set; }

        /// <summary>Starts a payload: what the last left is forgotten.</summary>
        public void Begin()
        {
            _used = 0;
            _total = 0;
            _fault = null;
            Packed = 0;
            _ready.Clear();
            _left.Reset(1);
            Next(null);
        }

        /// <summary>
        /// Waits for every piece, and returns <paramref name="head"/> followed by the payload's base64 as one new string;
        /// null when a piece failed (<see cref="Fault"/> says why), and the caller writes the payload the old way.
        /// </summary>
        public string Finish(string head)
        {
            Ready(_parts[_used - 1]);   // never empty unless nothing was written: a part is taken only for a byte that needs it
            Help(null);
            _left.Signal();
            _left.Wait();
            if (_fault != null)
            {
                return null;
            }
            int size = Header;
            for (int i = 0; i < _used; i++)
            {
                size += _parts[i].Size;
            }
            size += Tail;
            if (_gzip.Length < size)
            {
                _gzip = new byte[size + (size >> 3)];
            }
            Buffer.BlockCopy(DistrictSaveDeflate.GzipHeader, 0, _gzip, 0, Header);
            int at = Header;
            uint crc = 0;
            for (int i = 0; i < _used; i++)
            {
                Part p = _parts[i];
                Buffer.BlockCopy(p.Packed, 0, _gzip, at, p.Size);
                at += p.Size;
                crc = p.Length == PieceBytes ? DistrictSaveDeflate.Across(crc, p.Crc, _full) : DistrictSaveDeflate.Combine(crc, p.Crc, p.Length, _even, _odd);
            }
            Frame(_gzip, at, crc, (uint)_total);
            Packed = size;
            int chars = head.Length + (size + 2) / 3 * 4;
            if (_chars.Length < chars)
            {
                _chars = new char[chars + (chars >> 3)];
            }
            head.CopyTo(0, _chars, 0, head.Length);
            Convert.ToBase64CharArray(_gzip, 0, size, _chars, head.Length);
            return new string(_chars, 0, chars);
        }

        // The last block, the check value and the length (little-endian, the length modulo 2^32), at the end of a gzip frame.
        private static void Frame(byte[] b, int at, uint crc, uint length)
        {
            b[at] = DistrictSaveDeflate.LastBlock[0];
            b[at + 1] = DistrictSaveDeflate.LastBlock[1];
            for (int i = 0; i < 4; i++)
            {
                b[at + 2 + i] = (byte)(crc >> (8 * i));
                b[at + 6 + i] = (byte)(length >> (8 * i));
            }
        }

        // ---------------------------------------------------------------- the stream the serializer writes into

        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => _total;

        public override long Position
        {
            get => _total;
            set => throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            while (count > 0)
            {
                Part p = _parts[_used - 1];
                int room = PieceBytes - p.Length;
                if (room == 0)
                {
                    Next(p);
                    Ready(p);
                    continue;
                }
                int n = count < room ? count : room;
                Buffer.BlockCopy(buffer, offset, p.Raw, p.From + p.Length, n);
                p.Length += n;
                offset += n;
                count -= n;
                _total += n;
            }
        }

        public override void WriteByte(byte value)
        {
            Part p = _parts[_used - 1];
            if (p.Length == PieceBytes)
            {
                Next(p);
                Ready(p);
                p = _parts[_used - 1];
            }
            p.Raw[p.From + p.Length++] = value;
            _total++;
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        // The next part to fill, with the end of the full one before it at its front; taken before that one is queued, as
        // its bytes go back to be reused once a worker has compressed it.
        private void Next(Part full)
        {
            if (_used == _parts.Count)
            {
                _parts.Add(new Part());
            }
            Part p = _parts[_used];
            lock (_gate)
            {
                p.Raw = _free.Count > 0 ? _free.Pop() : null;
            }
            if (p.Raw == null)
            {
                p.Raw = new byte[Before + PieceBytes];
            }
            p.From = 0;
            if (full != null)
            {
                p.From = Before;
                Buffer.BlockCopy(full.Raw, full.From + full.Length - Before, p.Raw, 0, Before);   // a full piece is longer than the window
            }
            p.Length = 0;
            p.Size = 0;
            _used++;
        }

        // ---------------------------------------------------------------- the workers

        // A full part: queued, and a worker posted while there are fewer than the most.
        private void Ready(Part p)
        {
            _left.AddCount();
            bool post = false;
            lock (_gate)
            {
                _ready.Enqueue(p);
                if (_running < _workers)
                {
                    _running++;
                    post = true;
                }
            }
            if (post && !ThreadPool.UnsafeQueueUserWorkItem(_run, null))
            {
                lock (_gate)
                {
                    _running--;   // no worker: the caller's thread takes the piece at the finish
                }
            }
        }

        private void Run(object _)
        {
            try
            {
                Help(this);
            }
            catch (Exception e)
            {
                _fault = "a worker failed (" + e.GetType().Name + ": " + e.Message + ")";   // nothing may leave a pool thread
            }
        }

        // Takes queued parts until none is left; a worker (<paramref name="worker"/> set) then counts itself out under the
        // same lock a new part is queued under, so that no part is queued while every worker has just gone.
        private void Help(object worker)
        {
            DistrictSaveDeflate.Scratch s = null;
            while (true)
            {
                Part p;
                lock (_gate)
                {
                    if (_ready.Count == 0)
                    {
                        if (worker != null)
                        {
                            _running--;
                        }
                        if (s != null)
                        {
                            _spare.Push(s);
                        }
                        return;
                    }
                    p = _ready.Dequeue();
                    if (s == null && _spare.Count > 0)
                    {
                        s = _spare.Pop();
                    }
                }
                try
                {
                    if (s == null)
                    {
                        s = new DistrictSaveDeflate.Scratch();
                    }
                    Pack(p, s);
                }
                catch (Exception e)
                {
                    _fault = "a piece could not be compressed (" + e.GetType().Name + ": " + e.Message + ")";
                }
                finally
                {
                    lock (_gate)
                    {
                        _free.Push(p.Raw);   // the next part to fill takes it
                    }
                    p.Raw = null;
                    _left.Signal();   // whatever happened, the finish does not wait for this piece again
                }
            }
        }

        // One part compressed, and read back by the runtime's own inflater as a gzip of its own: the bytes before it in a
        // stored block, then the piece, so that its matches into them read as in the whole stream.
        private static void Pack(Part p, DistrictSaveDeflate.Scratch s)
        {
            int need = Header + Stored + Before + DistrictSaveDeflate.Bound(PieceBytes) + Tail;
            if (s.Out == null || s.Out.Length < need)
            {
                s.Out = new byte[need];
            }
            byte[] o = s.Out;
            Buffer.BlockCopy(DistrictSaveDeflate.GzipHeader, 0, o, 0, Header);
            int at = Header;
            if (p.From > 0)
            {
                o[at] = 0x00;   // a stored block, not the last
                o[at + 1] = (byte)p.From;
                o[at + 2] = (byte)(p.From >> 8);
                o[at + 3] = (byte)~p.From;
                o[at + 4] = (byte)(~p.From >> 8);
                Buffer.BlockCopy(p.Raw, 0, o, at + Stored, p.From);
                at += Stored + p.From;
            }
            p.Size = DistrictSaveDeflate.Piece(p.Raw, p.From, p.Length, o, at, s);
            p.Crc = DistrictSaveDeflate.Crc(0, p.Raw, p.From, p.Length);
            uint all = p.From > 0 ? DistrictSaveDeflate.Crc(0, p.Raw, 0, p.From + p.Length) : p.Crc;
            Frame(o, at + p.Size, all, (uint)(p.From + p.Length));
            if (!ReadsBack(o, at + p.Size + Tail, p.Raw, p.From + p.Length, s))
            {
                throw new InvalidDataException("a piece of " + p.Length + " bytes did not read back the same");
            }
            if (p.Packed.Length < p.Size)
            {
                p.Packed = new byte[p.Size + (p.Size >> 2)];
            }
            Buffer.BlockCopy(o, at, p.Packed, 0, p.Size);
        }

        /// <summary>
        /// True when the gzip in <paramref name="gzip"/> reads back, by the runtime's own GZipStream, as exactly
        /// <paramref name="count"/> bytes of <paramref name="raw"/>; false for one it cannot read.
        /// </summary>
        internal static bool ReadsBack(byte[] gzip, int length, byte[] raw, int count, DistrictSaveDeflate.Scratch s)
        {
            if (s.Read == null)
            {
                s.Read = new byte[16384];
            }
            byte[] got = s.Read;
            int at = 0;
            try
            {
                using (var z = new GZipStream(new MemoryStream(gzip, 0, length, false), CompressionMode.Decompress))
                {
                    while (true)
                    {
                        int n = z.Read(got, 0, got.Length);
                        if (n <= 0)
                        {
                            break;
                        }
                        if (at + n > count)
                        {
                            return false;
                        }
                        for (int i = 0; i < n; i++)
                        {
                            if (got[i] != raw[at + i])
                            {
                                return false;
                            }
                        }
                        at += n;
                    }
                }
            }
            catch (InvalidDataException)
            {
                return false;   // not deflate at all, or its check value wrong
            }
            return at == count;
        }
    }
}
