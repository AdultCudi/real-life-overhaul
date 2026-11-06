using System;

namespace RealLifeOverhaul.DistrictSave
{
    /// <summary>
    /// A deflate writer of the mod's own, for the district save's payload in pieces (DistrictSaveZip). The game's runtime
    /// cannot compress a payload in pieces: Mono's DeflateStream has no sync flush (its Flush leaves the data in zlib), and
    /// its GZipStream reads only the first member of a gzip, so a whole 3.6 MB payload went through one zlib stream on the
    /// main thread (42 ms of a 70 ms save, measured under the game's Mono). Here each piece is one block with codes made
    /// for it, over its own bytes alone (no match reaches back past its start), closed by an empty stored block so that the
    /// next begins on a byte: pieces written on any number of processors, laid end to end with an empty last block and the
    /// gzip frame, are one ordinary deflate stream that every inflater reads, the game's own zlib as DistrictSaveCodec.Decode
    /// does. The same bytes always give the same output. COST: at a save only, on the save's worker threads.
    /// </summary>
    public static class DistrictSaveDeflate
    {
        public const int Window = 32768;      // bytes: the farthest a match may reach back, deflate's own limit
        private const int HashBits = 15;      // a 32,768-entry table of where each three bytes were last seen
        private const int MaxChain = 32;      // earlier places tried along one hash's chain: zlib's middle levels try 32 to 128
        private const int Nice = 128;         // bytes: a match this long is taken at once, without looking for a longer one
        private const int MinMatch = 3;       // bytes: deflate's shortest match
        private const int MaxMatch = 258;     // bytes: deflate's longest match
        private const int FarForThree = 4096; // bytes: a three-byte match farther back than this costs more bits than its literals
        private const int Literals = 286;     // the literal and length symbols deflate has
        private const int Distances = 30;     // the distance symbols
        private const int LengthSymbols = 19;      // the symbols the code lengths themselves are written with
        private const int LongestCode = 15;   // bits: deflate's longest code for a literal, length or distance
        private const int LongestLengthCode = 7;   // bits: and for a code length's symbol
        private const int End = 256;          // the symbol that ends a block

        /// <summary>The gzip frame's first ten bytes, as the game's own GZipStream (Mono's zlib) writes them.</summary>
        public static readonly byte[] GzipHeader = { 0x1F, 0x8B, 0x08, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x0A };

        /// <summary>The stream's last block: empty, final, of the fixed codes (what zlib writes when it finishes after a flush).</summary>
        public static readonly byte[] LastBlock = { 0x03, 0x00 };

        private static readonly byte[] LenSym = new byte[MaxMatch + 1];   // a match length's symbol, less 257
        private static readonly byte[] DistSym = new byte[Window + 1];
        private static readonly int[] LenBase = { 3, 4, 5, 6, 7, 8, 9, 10, 11, 13, 15, 17, 19, 23, 27, 31, 35, 43, 51, 59, 67, 83, 99, 115, 131, 163, 195, 227, 258 };
        private static readonly int[] LenExtra = { 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 2, 2, 2, 2, 3, 3, 3, 3, 4, 4, 4, 4, 5, 5, 5, 5, 0 };
        private static readonly int[] DistBase =
        {
            1, 2, 3, 4, 5, 7, 9, 13, 17, 25, 33, 49, 65, 97, 129, 193, 257, 385, 513, 769, 1025, 1537, 2049, 3073, 4097, 6145, 8193, 12289, 16385, 24577
        };
        private static readonly int[] DistExtra = { 0, 0, 0, 0, 1, 1, 2, 2, 3, 3, 4, 4, 5, 5, 6, 6, 7, 7, 8, 8, 9, 9, 10, 10, 11, 11, 12, 12, 13, 13 };
        private static readonly int[] LengthExtra = { 2, 3, 7 };   // the repeat symbols 16, 17 and 18's extra bits
        private static readonly byte[] LengthOrder = { 16, 17, 18, 0, 8, 7, 9, 6, 10, 5, 11, 4, 12, 3, 13, 2, 14, 1, 15 };

        static DistrictSaveDeflate()
        {
            for (int s = 0; s < LenBase.Length; s++)
            {
                int top = s + 1 < LenBase.Length ? LenBase[s + 1] : MaxMatch + 1;   // 258 is the last symbol's alone, never the one before's with 31
                for (int len = LenBase[s]; len < top; len++)
                {
                    LenSym[len] = (byte)s;
                }
            }
            for (int s = 0; s < Distances; s++)
            {
                int top = s + 1 < Distances ? DistBase[s + 1] : Window + 1;
                for (int d = DistBase[s]; d < top; d++)
                {
                    DistSym[d] = (byte)s;
                }
            }
        }

        /// <summary>
        /// The most bytes a piece of <paramref name="count"/> bytes can become: every byte a nine-bit literal (a block's own
        /// codes never cost more than the fixed ones), its codes' table, its end and the closing stored block.
        /// </summary>
        public static int Bound(int count)
        {
            return count + (count >> 3) + 1024;
        }

        /// <summary>A worker's tables, kept between pieces and saves so that a piece allocates nothing.</summary>
        public sealed class Scratch
        {
            public readonly int[] Head = new int[1 << HashBits];
            public ushort[] Back = new ushort[0];   // for each place in the piece and before it, how far back the last place with its hash was, less one (none: the largest)
            public ushort[] Tokens = new ushort[0]; // a literal (under 256), or a match: its length plus 256, then its distance (never more than one a byte)
            public byte[] Out;                      // a piece's gzip of its own, for its read back (DistrictSaveZip)
            public byte[] Read;                     // the read back's buffer (DistrictSaveZip)
            public readonly int[] LitFreq = new int[Literals];
            public readonly int[] DistFreq = new int[Distances];
            public readonly int[] LengthFreq = new int[LengthSymbols];
            public readonly byte[] LitLen = new byte[Literals];
            public readonly byte[] DistLen = new byte[Distances];
            public readonly byte[] LengthLen = new byte[LengthSymbols];
            public readonly uint[] LitCode = new uint[Literals];
            public readonly uint[] DistCode = new uint[Distances];
            public readonly uint[] LengthCode = new uint[LengthSymbols];
            public readonly byte[] All = new byte[Literals + Distances];   // the code lengths of both, as the block's table lists them
            public readonly int[] Runs = new int[Literals + Distances];    // the table's symbols: the symbol, and its extra bits shifted up 8
            public readonly int[] Syms = new int[Literals];
            public readonly int[] Parent = new int[2 * Literals];
            public readonly int[] Depth = new int[2 * Literals];
            public readonly long[] Weight = new long[2 * Literals];
            public readonly int[] Count = new int[LongestCode + 1];
            public readonly int[] Next = new int[LongestCode + 1];
        }

        // The LSB-first bit stream deflate is written in.
        private struct Bits
        {
            public byte[] Out;
            public int At;
            public ulong Held;
            public int Count;

            public void Put(uint value, int bits)
            {
                Held |= (ulong)value << Count;
                Count += bits;
                if (Count >= 32)
                {
                    Out[At] = (byte)Held;
                    Out[At + 1] = (byte)(Held >> 8);
                    Out[At + 2] = (byte)(Held >> 16);
                    Out[At + 3] = (byte)(Held >> 24);
                    At += 4;
                    Held >>= 32;
                    Count -= 32;
                }
            }

            public void ToByte()
            {
                while (Count > 0)
                {
                    Out[At++] = (byte)Held;
                    Held >>= 8;
                    Count -= 8;
                }
                Held = 0;
                Count = 0;
            }
        }

        /// <summary>
        /// Writes <paramref name="count"/> bytes of <paramref name="src"/> from <paramref name="offset"/> as one piece of a
        /// deflate stream into <paramref name="dst"/> at <paramref name="at"/> (room for <see cref="Bound"/> bytes): a block
        /// with its own codes, not the last, then an empty stored block. The bytes before <paramref name="offset"/> are what
        /// the stream holds just before the piece (the end of the piece before it, at most <see cref="Window"/>): its
        /// matches may reach back into them, as an inflater reading the whole stream still has them. Returns how many bytes
        /// it wrote.
        /// </summary>
        public static int Piece(byte[] src, int offset, int count, byte[] dst, int at, Scratch s)
        {
            int tokens = Match(src, offset, count, s);
            Codes(s, out int lits, out int dists, out int runs, out int table);
            var bits = new Bits { Out = dst, At = at };
            bits.Put(4, 3);   // not the last block; codes of its own
            bits.Put((uint)(lits - 257), 5);
            bits.Put((uint)(dists - 1), 5);
            bits.Put((uint)(table - 4), 4);
            for (int i = 0; i < table; i++)
            {
                bits.Put(s.LengthLen[LengthOrder[i]], 3);
            }
            for (int i = 0; i < runs; i++)
            {
                int sym = s.Runs[i] & 0xFF;
                bits.Put(s.LengthCode[sym], s.LengthLen[sym]);
                if (sym >= 16)
                {
                    bits.Put((uint)(s.Runs[i] >> 8), LengthExtra[sym - 16]);
                }
            }
            uint[] litCode = s.LitCode;
            byte[] litLen = s.LitLen;
            ushort[] tok = s.Tokens;
            for (int i = 0; i < tokens; i++)
            {
                int t = tok[i];
                if (t < 256)
                {
                    bits.Put(litCode[t], litLen[t]);
                    continue;
                }
                int len = t - 256;
                int dist = tok[++i];
                int ls = LenSym[len];
                bits.Put(litCode[257 + ls], litLen[257 + ls]);
                if (LenExtra[ls] > 0)
                {
                    bits.Put((uint)(len - LenBase[ls]), LenExtra[ls]);
                }
                int ds = DistSym[dist];
                bits.Put(s.DistCode[ds], s.DistLen[ds]);
                if (DistExtra[ds] > 0)
                {
                    bits.Put((uint)(dist - DistBase[ds]), DistExtra[ds]);
                }
            }
            bits.Put(litCode[End], litLen[End]);
            bits.Put(0, 3);   // an empty stored block, not the last: the next piece starts on a byte
            bits.ToByte();
            dst[bits.At] = 0x00;
            dst[bits.At + 1] = 0x00;
            dst[bits.At + 2] = 0xFF;
            dst[bits.At + 3] = 0xFF;
            return bits.At + 4 - at;
        }

        // The piece as literals and matches (greedy, along hash chains through the bytes before it too), counting how often
        // each symbol comes.
        private static int Match(byte[] src, int offset, int count, Scratch s)
        {
            int[] head = s.Head;
            for (int i = 0; i < head.Length; i++)
            {
                head[i] = -1;
            }
            if (s.Back.Length < offset + count)
            {
                s.Back = new ushort[offset + count];
            }
            if (s.Tokens.Length < count)
            {
                s.Tokens = new ushort[count];
            }
            Array.Clear(s.LitFreq, 0, Literals);
            Array.Clear(s.DistFreq, 0, Distances);
            ushort[] back = s.Back;
            ushort[] tokens = s.Tokens;
            int[] litFreq = s.LitFreq;
            int n = 0;
            int end = offset + count;
            for (int q = 0; q < offset && q + MinMatch <= end; q++)
            {
                int h = Hash(src, q);
                back[q] = head[h] >= 0 && q - head[h] <= Window ? (ushort)(q - head[h] - 1) : ushort.MaxValue;
                head[h] = q;
            }
            int p = offset;
            while (p < end)
            {
                int best = 0;
                int dist = 0;
                if (p + MinMatch <= end)
                {
                    int h = Hash(src, p);
                    int cand = head[h];
                    back[p] = cand >= 0 && p - cand <= Window ? (ushort)(p - cand - 1) : ushort.MaxValue;
                    head[h] = p;
                    int limit = end - p < MaxMatch ? end - p : MaxMatch;
                    int tries = MaxChain;
                    while (cand >= 0 && p - cand <= Window && tries-- > 0)
                    {
                        if (src[cand + best] == src[p + best])
                        {
                            int len = 0;
                            while (len < limit && src[cand + len] == src[p + len])
                            {
                                len++;
                            }
                            if (len > best)
                            {
                                best = len;
                                dist = p - cand;
                                if (len >= Nice || len == limit)
                                {
                                    break;
                                }
                            }
                        }
                        ushort step = back[cand];
                        cand = step == ushort.MaxValue ? -1 : cand - step - 1;
                    }
                }
                if (best > MinMatch || (best == MinMatch && dist <= FarForThree))
                {
                    tokens[n++] = (ushort)(256 + best);
                    tokens[n++] = (ushort)dist;
                    litFreq[257 + LenSym[best]]++;
                    s.DistFreq[DistSym[dist]]++;
                    int stop = p + best;
                    for (p++; p < stop; p++)
                    {
                        if (p + MinMatch <= end)
                        {
                            int h = Hash(src, p);
                            int cand = head[h];
                            back[p] = cand >= 0 && p - cand <= Window ? (ushort)(p - cand - 1) : ushort.MaxValue;
                            head[h] = p;
                        }
                    }
                    continue;
                }
                tokens[n++] = src[p];
                litFreq[src[p]]++;
                p++;
            }
            litFreq[End] = 1;
            return n;
        }

        private static int Hash(byte[] src, int p)
        {
            uint v = (uint)(src[p] | (src[p + 1] << 8) | (src[p + 2] << 16));
            return (int)((v * 2654435761u) >> (32 - HashBits));
        }

        // The block's codes from the counts, and its table: the code lengths of both codes as one list, run-length coded,
        // written with a third code. Every code has at least two symbols: an inflater may refuse a code of one.
        private static void Codes(Scratch s, out int lits, out int dists, out int runs, out int table)
        {
            AtLeastTwo(s.LitFreq, Literals);
            AtLeastTwo(s.DistFreq, Distances);
            Lengths(s.LitFreq, Literals, LongestCode, s.LitLen, s);
            Lengths(s.DistFreq, Distances, LongestCode, s.DistLen, s);
            Canonical(s.LitLen, Literals, s.LitCode, s);
            Canonical(s.DistLen, Distances, s.DistCode, s);
            lits = Literals;
            while (lits > 257 && s.LitLen[lits - 1] == 0)
            {
                lits--;
            }
            dists = Distances;
            while (dists > 1 && s.DistLen[dists - 1] == 0)
            {
                dists--;
            }
            byte[] all = s.All;
            Buffer.BlockCopy(s.LitLen, 0, all, 0, lits);
            Buffer.BlockCopy(s.DistLen, 0, all, lits, dists);
            int total = lits + dists;
            Array.Clear(s.LengthFreq, 0, LengthSymbols);
            runs = 0;
            for (int i = 0; i < total;)
            {
                byte len = all[i];
                int run = 1;
                while (i + run < total && all[i + run] == len)
                {
                    run++;
                }
                i += run;
                if (len == 0)
                {
                    while (run >= 11)
                    {
                        int r = run < 138 ? run : 138;
                        Run(s, ref runs, 18, r - 11);
                        run -= r;
                    }
                    if (run >= 3)
                    {
                        Run(s, ref runs, 17, run - 3);
                        run = 0;
                    }
                }
                else
                {
                    Run(s, ref runs, len, 0);
                    run--;
                    while (run >= 3)
                    {
                        int r = run < 6 ? run : 6;
                        Run(s, ref runs, 16, r - 3);
                        run -= r;
                    }
                }
                for (; run > 0; run--)
                {
                    Run(s, ref runs, len, 0);
                }
            }
            AtLeastTwo(s.LengthFreq, LengthSymbols);
            Lengths(s.LengthFreq, LengthSymbols, LongestLengthCode, s.LengthLen, s);
            Canonical(s.LengthLen, LengthSymbols, s.LengthCode, s);
            table = LengthSymbols;
            while (table > 4 && s.LengthLen[LengthOrder[table - 1]] == 0)
            {
                table--;
            }
        }

        private static void Run(Scratch s, ref int runs, int symbol, int extra)
        {
            s.Runs[runs++] = symbol | (extra << 8);
            s.LengthFreq[symbol]++;
        }

        // A code of one symbol, or none, is given a second (or two) that are never written: the code is then complete.
        private static void AtLeastTwo(int[] freq, int n)
        {
            int used = 0;
            for (int i = 0; i < n && used < 2; i++)
            {
                if (freq[i] > 0)
                {
                    used++;
                }
            }
            for (int i = 0; i < n && used < 2; i++)
            {
                if (freq[i] == 0)
                {
                    freq[i] = 1;
                    used++;
                }
            }
        }

        // Huffman code lengths for the counted symbols, none longer than limit (over it, the counts are halved and the code
        // made again: rare, and never far from the best); a symbol not used has none. The order of equal counts is the
        // symbols', so the same counts always give the same code.
        private static void Lengths(int[] freq, int n, int limit, byte[] len, Scratch s)
        {
            int[] syms = s.Syms;
            int[] parent = s.Parent;
            int[] depth = s.Depth;
            long[] weight = s.Weight;
            while (true)
            {
                int m = 0;
                for (int i = 0; i < n; i++)
                {
                    len[i] = 0;
                    if (freq[i] > 0)
                    {
                        syms[m] = i;
                        m++;
                    }
                }
                for (int i = 1; i < m; i++)   // by count, then symbol: insertion, as the symbols come in order
                {
                    int sym = syms[i];
                    int j = i - 1;
                    while (j >= 0 && (freq[syms[j]] > freq[sym] || (freq[syms[j]] == freq[sym] && syms[j] > sym)))
                    {
                        syms[j + 1] = syms[j];
                        j--;
                    }
                    syms[j + 1] = sym;
                }
                for (int i = 0; i < m; i++)
                {
                    weight[i] = freq[syms[i]];
                }
                int leaf = 0;
                int inner = m;
                int made = m;
                for (int k = 0; k < m - 1; k++)   // two queues: the sorted leaves, and the joined nodes in the order made
                {
                    int a = leaf < m && (inner >= made || weight[leaf] <= weight[inner]) ? leaf++ : inner++;
                    int b = leaf < m && (inner >= made || weight[leaf] <= weight[inner]) ? leaf++ : inner++;
                    weight[made] = weight[a] + weight[b];
                    parent[a] = made;
                    parent[b] = made;
                    made++;
                }
                depth[made - 1] = 0;
                for (int node = made - 2; node >= 0; node--)
                {
                    depth[node] = depth[parent[node]] + 1;   // a node's parent is made after it
                }
                int longest = 0;
                for (int i = 0; i < m; i++)
                {
                    len[syms[i]] = (byte)depth[i];
                    if (depth[i] > longest)
                    {
                        longest = depth[i];
                    }
                }
                if (longest <= limit)
                {
                    return;
                }
                for (int i = 0; i < n; i++)
                {
                    if (freq[i] > 0)
                    {
                        freq[i] = (freq[i] + 1) >> 1;
                    }
                }
            }
        }

        // The canonical codes for the lengths (RFC 1951, 3.2.2), bit-reversed for the LSB-first stream.
        private static void Canonical(byte[] len, int n, uint[] code, Scratch s)
        {
            int[] count = s.Count;
            int[] next = s.Next;
            Array.Clear(count, 0, count.Length);
            for (int i = 0; i < n; i++)
            {
                count[len[i]]++;
            }
            count[0] = 0;
            int c = 0;
            for (int bits = 1; bits <= LongestCode; bits++)
            {
                c = (c + count[bits - 1]) << 1;
                next[bits] = c;
            }
            for (int i = 0; i < n; i++)
            {
                code[i] = len[i] != 0 ? Reverse((uint)next[len[i]]++, len[i]) : 0u;
            }
        }

        private static uint Reverse(uint code, int bits)
        {
            uint r = 0;
            for (int i = 0; i < bits; i++)
            {
                r = (r << 1) | ((code >> i) & 1u);
            }
            return r;
        }

        // ---------------------------------------------------------------- the gzip frame's check value

        private static readonly uint[] Crc8 = CrcTables();

        private static uint[] CrcTables()
        {
            var t = new uint[8 * 256];
            for (uint n = 0; n < 256; n++)
            {
                uint c = n;
                for (int k = 0; k < 8; k++)
                {
                    c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
                }
                t[n] = c;
            }
            for (int n = 0; n < 256; n++)
            {
                for (int k = 1; k < 8; k++)
                {
                    uint prev = t[(k - 1) * 256 + n];
                    t[k * 256 + n] = (prev >> 8) ^ t[prev & 0xFF];
                }
            }
            return t;
        }

        /// <summary>The CRC-32 of the gzip frame (IEEE, as zlib's crc32), carried on from <paramref name="crc"/> (0 to start), eight bytes a step.</summary>
        public static uint Crc(uint crc, byte[] b, int offset, int count)
        {
            uint[] t = Crc8;
            uint c = ~crc;
            int i = offset;
            int end = offset + count;
            for (; i + 8 <= end; i += 8)
            {
                uint one = c ^ (uint)(b[i] | (b[i + 1] << 8) | (b[i + 2] << 16) | (b[i + 3] << 24));
                uint two = (uint)(b[i + 4] | (b[i + 5] << 8) | (b[i + 6] << 16) | (b[i + 7] << 24));
                c = t[7 * 256 + (one & 0xFF)] ^ t[6 * 256 + ((one >> 8) & 0xFF)] ^ t[5 * 256 + ((one >> 16) & 0xFF)] ^ t[4 * 256 + (one >> 24)]
                    ^ t[3 * 256 + (two & 0xFF)] ^ t[2 * 256 + ((two >> 8) & 0xFF)] ^ t[256 + ((two >> 16) & 0xFF)] ^ t[two >> 24];
            }
            for (; i < end; i++)
            {
                c = t[(c ^ b[i]) & 0xFF] ^ (c >> 8);
            }
            return ~c;
        }

        /// <summary>
        /// Into <paramref name="op"/> (32 words), what carrying a CRC-32 across <paramref name="length"/> more bytes does to
        /// it, for <see cref="Across"/>: made once for a length that comes again and again (a full piece's), it joins two
        /// CRCs with 32 steps instead of <see cref="Combine"/>'s hundreds.
        /// </summary>
        public static void Shift(long length, uint[] op, uint[] even, uint[] odd)
        {
            for (int n = 0; n < 32; n++)
            {
                op[n] = 1u << n;
            }
            if (length <= 0)
            {
                return;
            }
            odd[0] = 0xEDB88320u;
            uint row = 1;
            for (int n = 1; n < 32; n++)
            {
                odd[n] = row;
                row <<= 1;
            }
            Square(even, odd);
            Square(odd, even);
            while (true)
            {
                Square(even, odd);   // one byte the first time, then each power of two
                if ((length & 1) != 0)
                {
                    Apply(op, even);
                }
                length >>= 1;
                if (length == 0)
                {
                    return;
                }
                Square(odd, even);
                if ((length & 1) != 0)
                {
                    Apply(op, odd);
                }
                length >>= 1;
                if (length == 0)
                {
                    return;
                }
            }
        }

        /// <summary>The CRC-32 of two runs of bytes end to end, from each one's own and <paramref name="op"/> made by <see cref="Shift"/> for the second's length.</summary>
        public static uint Across(uint crc1, uint crc2, uint[] op)
        {
            return Times(op, crc1) ^ crc2;
        }

        // op followed by m, in place.
        private static void Apply(uint[] op, uint[] m)
        {
            for (int n = 0; n < 32; n++)
            {
                op[n] = Times(m, op[n]);
            }
        }

        /// <summary>
        /// The CRC-32 of two runs of bytes end to end from each one's own (zlib's crc32_combine): each piece's is made on
        /// its worker, and the whole stream's from them in order. <paramref name="even"/> and <paramref name="odd"/>: 32
        /// words each of room, reused.
        /// </summary>
        public static uint Combine(uint crc1, uint crc2, long length2, uint[] even, uint[] odd)
        {
            if (length2 <= 0)
            {
                return crc1;
            }
            odd[0] = 0xEDB88320u;   // the operator for one zero bit
            uint row = 1;
            for (int n = 1; n < 32; n++)
            {
                odd[n] = row;
                row <<= 1;
            }
            Square(even, odd);   // two zero bits
            Square(odd, even);   // four
            do
            {
                Square(even, odd);
                if ((length2 & 1) != 0)
                {
                    crc1 = Times(even, crc1);
                }
                length2 >>= 1;
                if (length2 == 0)
                {
                    break;
                }
                Square(odd, even);
                if ((length2 & 1) != 0)
                {
                    crc1 = Times(odd, crc1);
                }
                length2 >>= 1;
            }
            while (length2 != 0);
            return crc1 ^ crc2;
        }

        private static uint Times(uint[] matrix, uint vector)
        {
            uint sum = 0;
            for (int i = 0; vector != 0; i++, vector >>= 1)
            {
                if ((vector & 1) != 0)
                {
                    sum ^= matrix[i];
                }
            }
            return sum;
        }

        private static void Square(uint[] square, uint[] matrix)
        {
            for (int n = 0; n < 32; n++)
            {
                square[n] = Times(matrix, matrix[n]);
            }
        }
    }
}
