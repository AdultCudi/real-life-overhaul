using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace RealLifeOverhaul
{
    /// <summary>How a signature blob is read (Core/ModImage.TypesIn).</summary>
    public enum BlobKind
    {
        Field,
        Method,
        Property,
        Locals,
        Instance,     // a generic method's instance (MethodSpec)
        Spec,         // a type specification (TypeSpec)
        Member,       // a member reference: a field's or a method's, by its first byte
        StandAlone    // a stand-alone signature: locals or calli's method, by its first byte
    }

    /// <summary>
    /// The mod's own assembly read as a file (ECMA-335): the metadata tables that say what of the game and Unity its
    /// code names, its own types and methods, and on demand their IL. Core/ModScan reads it at start to find what the
    /// running game lacks before anything of the mod runs; ContractCheck holds every answer to .NET's own reader.
    /// Only bytes are read, nothing loaded or resolved. COST: three reads at start (about 2.5 MB, let go of with the
    /// scan); the whole file only when something is missing. Game-free.
    /// </summary>
    public sealed partial class ModImage
    {
        // The tables read (ECMA-335 II.22).
        public const int TypeRef = 0x01;
        public const int TypeDef = 0x02;
        public const int Field = 0x04;
        public const int MethodDef = 0x06;
        public const int InterfaceImpl = 0x09;
        public const int MemberRef = 0x0A;
        public const int StandAloneSig = 0x11;
        public const int ModuleRef = 0x1A;
        public const int TypeSpec = 0x1B;
        public const int AssemblyRef = 0x23;
        public const int NestedClass = 0x29;
        public const int MethodSpec = 0x2B;

        private readonly string _path;
        private readonly int[] _rows = new int[64];
        private readonly int[] _size = new int[64];
        private readonly int[] _at = new int[64];
        private byte[] _tables, _strings, _blobs;
        private int _str, _guid, _blob;   // heap index sizes, 2 or 4
        private int[] _sectionVa, _sectionSize, _sectionRaw;
        private long _userStrings;        // file offset of #US
        private int _userStringsSize;
        private byte[] _file;             // the whole file, read when the IL is wanted

        private ModImage(string path)
        {
            _path = path;
        }

        /// <summary>The file read, or null with <paramref name="why"/> in English when it is not an assembly this can read.</summary>
        public static ModImage Read(string path, out string why)
        {
            why = null;
            try
            {
                var image = new ModImage(path);
                why = image.Load();
                return why == null ? image : null;
            }
            catch (Exception e)
            {
                why = e.GetType().Name + ": " + e.Message;
                return null;
            }
        }

        /// <summary>The rows of a table (0 when the assembly has none).</summary>
        public int Rows(int table)
        {
            return table >= 0 && table < 64 ? _rows[table] : 0;
        }

        private string Load()
        {
            using (var file = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            {
                byte[] head = ReadAt(file, 0, (int)Math.Min(4096, file.Length));
                if (head.Length < 0x40 || head[0] != 'M' || head[1] != 'Z')
                {
                    return "it is not a program file";
                }
                int pe = I32(head, 0x3C);
                if (pe < 0 || pe + 24 > head.Length || I32(head, pe) != 0x00004550)
                {
                    return "it has no PE header";
                }
                int sections = U16(head, pe + 6);
                int optional = U16(head, pe + 20);
                int opt = pe + 24;
                int magic = U16(head, opt);
                int directories = opt + (magic == 0x20B ? 112 : 96);
                int cliRva = I32(head, directories + 14 * 8);
                int table = opt + optional;
                if (sections <= 0 || sections > 96 || table + sections * 40 > head.Length || cliRva == 0)
                {
                    return "it is not a .NET assembly";
                }
                _sectionVa = new int[sections];
                _sectionSize = new int[sections];
                _sectionRaw = new int[sections];
                for (int i = 0; i < sections; i++)
                {
                    int s = table + i * 40;
                    int virtualSize = I32(head, s + 8);
                    _sectionVa[i] = I32(head, s + 12);
                    _sectionSize[i] = Math.Max(virtualSize, I32(head, s + 16));
                    _sectionRaw[i] = I32(head, s + 20);
                }
                byte[] cli = ReadAt(file, Offset(cliRva), 72);
                int mdRva = I32(cli, 8);
                long md = Offset(mdRva);
                byte[] root = ReadAt(file, md, 512);
                if (I32(root, 0) != 0x424A5342)
                {
                    return "its metadata has no signature";
                }
                int versionLength = I32(root, 12);
                int q = 16 + versionLength + 2;
                int streams = U16(root, q);
                q += 2;
                long tablesAt = -1, stringsAt = -1, blobsAt = -1;
                int tablesSize = 0, stringsSize = 0, blobsSize = 0;
                for (int i = 0; i < streams; i++)
                {
                    int offset = I32(root, q), size = I32(root, q + 4);
                    q += 8;
                    int end = q;
                    while (end < root.Length && root[end] != 0)
                    {
                        end++;
                    }
                    string name = Encoding.ASCII.GetString(root, q, end - q);
                    q = (end + 4) & ~3;
                    switch (name)
                    {
                        case "#~":
                            tablesAt = md + offset;
                            tablesSize = size;
                            break;
                        case "#-":
                            return "its metadata is not compressed";
                        case "#Strings":
                            stringsAt = md + offset;
                            stringsSize = size;
                            break;
                        case "#Blob":
                            blobsAt = md + offset;
                            blobsSize = size;
                            break;
                        case "#US":
                            _userStrings = md + offset;
                            _userStringsSize = size;
                            break;
                    }
                }
                if (tablesAt < 0 || stringsAt < 0 || blobsAt < 0)
                {
                    return "its metadata lacks a stream";
                }
                _tables = ReadAt(file, tablesAt, tablesSize);
                _strings = ReadAt(file, stringsAt, stringsSize);
                _blobs = ReadAt(file, blobsAt, blobsSize);
            }
            return Layout();
        }

        private static byte[] ReadAt(FileStream file, long at, int count)
        {
            if (at < 0 || count < 0 || at + count > file.Length)
            {
                throw new EndOfStreamException("a read past the end of the file");
            }
            var bytes = new byte[count];
            file.Position = at;
            int read = 0;
            while (read < count)
            {
                int n = file.Read(bytes, read, count - read);
                if (n <= 0)
                {
                    throw new EndOfStreamException("the file ended early");
                }
                read += n;
            }
            return bytes;
        }

        /// <summary>The file offset of a relative virtual address.</summary>
        public long Offset(int rva)
        {
            for (int i = 0; i < _sectionVa.Length; i++)
            {
                if (rva >= _sectionVa[i] && rva < _sectionVa[i] + _sectionSize[i])
                {
                    return (long)rva - _sectionVa[i] + _sectionRaw[i];
                }
            }
            return -1;
        }

        // The tables' row counts and sizes (ECMA-335 II.24.2.6, II.22).
        private string Layout()
        {
            byte heaps = _tables[6];
            _str = (heaps & 0x01) != 0 ? 4 : 2;
            _guid = (heaps & 0x02) != 0 ? 4 : 2;
            _blob = (heaps & 0x04) != 0 ? 4 : 2;
            ulong valid = U64(_tables, 8);
            int p = 24;
            for (int t = 0; t < 64; t++)
            {
                if ((valid & (1UL << t)) != 0)
                {
                    _rows[t] = I32(_tables, p);
                    p += 4;
                }
            }
            if ((heaps & 0x40) != 0)
            {
                p += 4;   // extra data, after the counts
            }
            if ((valid >> 0x2D) != 0)
            {
                return "its metadata has tables this reader does not know";
            }
            if (_rows[0x03] != 0 || _rows[0x05] != 0 || _rows[0x07] != 0 || _rows[0x13] != 0 || _rows[0x16] != 0)
            {
                return "its metadata has pointer tables";
            }
            int tdor = Coded(2, 0x02, 0x01, 0x1B);
            int hasConstant = Coded(2, 0x04, 0x08, 0x17);
            int hasCa = Coded(5, 0x06, 0x04, 0x01, 0x02, 0x08, 0x09, 0x0A, 0x00, 0x0E, 0x17, 0x14, 0x11, 0x1A, 0x1B, 0x20, 0x23, 0x26, 0x27, 0x28, 0x2A, 0x2C, 0x2B);
            int hasMarshal = Coded(1, 0x04, 0x08);
            int hasSecurity = Coded(2, 0x02, 0x06, 0x20);
            int mrp = Coded(3, 0x02, 0x01, 0x1A, 0x06, 0x1B);
            int hasSemantics = Coded(1, 0x14, 0x17);
            int mdor = Coded(1, 0x06, 0x0A);
            int forwarded = Coded(1, 0x04, 0x06);
            int implementation = Coded(2, 0x26, 0x23, 0x27);
            int caType = Coded(3, 0x06, 0x0A);
            int scope = Coded(2, 0x00, 0x1A, 0x23, 0x01);
            int typeOrMethod = Coded(1, 0x02, 0x06);
            _size[0x00] = 2 + _str + 3 * _guid;
            _size[0x01] = scope + 2 * _str;
            _size[0x02] = 4 + 2 * _str + tdor + Index(0x04) + Index(0x06);
            _size[0x04] = 2 + _str + _blob;
            _size[0x06] = 8 + _str + _blob + Index(0x08);
            _size[0x08] = 4 + _str;
            _size[0x09] = Index(0x02) + tdor;
            _size[0x0A] = mrp + _str + _blob;
            _size[0x0B] = 2 + hasConstant + _blob;
            _size[0x0C] = hasCa + caType + _blob;
            _size[0x0D] = hasMarshal + _blob;
            _size[0x0E] = 2 + hasSecurity + _blob;
            _size[0x0F] = 6 + Index(0x02);
            _size[0x10] = 4 + Index(0x04);
            _size[0x11] = _blob;
            _size[0x12] = Index(0x02) + Index(0x14);
            _size[0x14] = 2 + _str + tdor;
            _size[0x15] = Index(0x02) + Index(0x17);
            _size[0x17] = 2 + _str + _blob;
            _size[0x18] = 2 + Index(0x06) + hasSemantics;
            _size[0x19] = Index(0x02) + 2 * mdor;
            _size[0x1A] = _str;
            _size[0x1B] = _blob;
            _size[0x1C] = 2 + forwarded + _str + Index(0x1A);
            _size[0x1D] = 4 + Index(0x04);
            _size[0x1E] = 8;
            _size[0x1F] = 4;
            _size[0x20] = 16 + _blob + 2 * _str;
            _size[0x21] = 4;
            _size[0x22] = 12;
            _size[0x23] = 12 + 2 * _blob + 2 * _str;
            _size[0x24] = 4 + Index(0x23);
            _size[0x25] = 12 + Index(0x23);
            _size[0x26] = 4 + _str + _blob;
            _size[0x27] = 8 + 2 * _str + implementation;
            _size[0x28] = 8 + _str + implementation;
            _size[0x29] = 2 * Index(0x02);
            _size[0x2A] = 4 + typeOrMethod + _str;
            _size[0x2B] = mdor + _blob;
            _size[0x2C] = Index(0x2A) + tdor;
            for (int t = 0; t < 64; t++)
            {
                _at[t] = p;
                p += _rows[t] * _size[t];
            }
            _scopeSize = scope;
            _tdorSize = tdor;
            _mrpSize = mrp;
            _mdorSize = mdor;
            return p <= _tables.Length ? null : "its tables run past their stream";
        }

        private int _scopeSize, _tdorSize, _mrpSize, _mdorSize;

        private int Index(int table)
        {
            return _rows[table] < 0x10000 ? 2 : 4;
        }

        private int Coded(int bits, params int[] tables)
        {
            int most = 0;
            foreach (int t in tables)
            {
                most = Math.Max(most, _rows[t]);
            }
            return most < (1 << (16 - bits)) ? 2 : 4;
        }

        private int Row(int table, int row)
        {
            return _at[table] + (row - 1) * _size[table];
        }

        private int Read(int at, int size)
        {
            return size == 2 ? U16(_tables, at) : I32(_tables, at);
        }

        /// <summary>The metadata token of a row.</summary>
        public static int Token(int table, int row)
        {
            return (table << 24) | row;
        }

        public static int TableOf(int token)
        {
            return (token >> 24) & 0xFF;
        }

        public static int RowOf(int token)
        {
            return token & 0x00FFFFFF;
        }

        // A TypeDefOrRef coded index or a TypeDefOrRefOrSpecEncoded value as a token.
        private static int TypeToken(int coded)
        {
            int row = coded >> 2;
            switch (coded & 3)
            {
                case 0: return Token(TypeDef, row);
                case 1: return Token(TypeRef, row);
                case 2: return Token(TypeSpec, row);
                default: return 0;
            }
        }

        private static int U16(byte[] b, int at)
        {
            return b[at] | (b[at + 1] << 8);
        }

        private static int I32(byte[] b, int at)
        {
            return b[at] | (b[at + 1] << 8) | (b[at + 2] << 16) | (b[at + 3] << 24);
        }

        private static ulong U64(byte[] b, int at)
        {
            return (uint)I32(b, at) | ((ulong)(uint)I32(b, at + 4) << 32);
        }
    }
}
