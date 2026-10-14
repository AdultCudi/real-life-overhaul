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
    /// The mod's own assembly read as a file (ECMA-335): the few tables of its metadata that say what of the game and of
    /// Unity the mod's code names (type and member references, generic instances), its own types and methods, and on
    /// demand the IL of its methods. Core/ModScan reads it as the mod starts, to find what the running game lacks before
    /// anything of the mod runs; ContractCheck reads the built mod with it too and holds every answer to .NET's own reader.
    ///
    /// Nothing is loaded and nothing is resolved here: only bytes are read. At start the metadata's tables, names and
    /// signatures are read in three reads (about two and a half megabytes, let go of with the scan); the method bodies
    /// only when something is missing (Core/ModScan then reads the whole file once). No Unity or game types.
    /// </summary>
    public sealed class ModImage
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

        // ---------------------------------------------------------------- reading the file

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
                p += 4;   // (extra data, after the counts)
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

        // ---------------------------------------------------------------- tokens

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

        // ---------------------------------------------------------------- type references

        /// <summary>A type reference's resolution scope as a token: an assembly reference, the outer type reference of a nested one, a module (reference).</summary>
        public int TypeRefScope(int row)
        {
            int coded = Read(Row(TypeRef, row), _scopeSize);
            int r = coded >> 2;
            switch (coded & 3)
            {
                case 0: return Token(0x00, r);
                case 1: return Token(ModuleRef, r);
                case 2: return Token(AssemblyRef, r);
                default: return Token(TypeRef, r);
            }
        }

        public string TypeRefName(int row)
        {
            return String(Read(Row(TypeRef, row) + _scopeSize, _str));
        }

        public string TypeRefNamespace(int row)
        {
            return String(Read(Row(TypeRef, row) + _scopeSize + _str, _str));
        }

        /// <summary>A type reference's full name as reflection writes it: "Namespace.Name", a nested one "Outer+Inner".</summary>
        public string TypeRefFullName(int row)
        {
            return FullName(row, 0);
        }

        private string FullName(int row, int depth)
        {
            int scope = TypeRefScope(row);
            if (TableOf(scope) == TypeRef && depth < 16)
            {
                return FullName(RowOf(scope), depth + 1) + "+" + TypeRefName(row);
            }
            string ns = TypeRefNamespace(row);
            return ns.Length > 0 ? ns + "." + TypeRefName(row) : TypeRefName(row);
        }

        /// <summary>The simple name of the assembly a type reference is in (the outermost one's, for a nested one); null for the mod's own module.</summary>
        public string TypeRefAssembly(int row)
        {
            for (int depth = 0; depth < 16; depth++)
            {
                int scope = TypeRefScope(row);
                switch (TableOf(scope))
                {
                    case AssemblyRef:
                        return AssemblyRefName(RowOf(scope));
                    case TypeRef:
                        row = RowOf(scope);
                        continue;
                    default:
                        return null;
                }
            }
            return null;
        }

        public string AssemblyRefName(int row)
        {
            return String(Read(Row(AssemblyRef, row) + 12 + _blob, _str));
        }

        // ---------------------------------------------------------------- member references

        /// <summary>The parent of a member reference as a token: a type reference, a type specification (a generic instance), a type or method of the mod, a module reference.</summary>
        public int MemberRefParent(int row)
        {
            int coded = Read(Row(MemberRef, row), _mrpSize);
            int r = coded >> 3;
            switch (coded & 7)
            {
                case 0: return Token(TypeDef, r);
                case 1: return Token(TypeRef, r);
                case 2: return Token(ModuleRef, r);
                case 3: return Token(MethodDef, r);
                case 4: return Token(TypeSpec, r);
                default: return 0;
            }
        }

        public string MemberRefName(int row)
        {
            return String(Read(Row(MemberRef, row) + _mrpSize, _str));
        }

        public int MemberRefSignature(int row)
        {
            return Read(Row(MemberRef, row) + _mrpSize + _str, _blob);
        }

        /// <summary>True for a reference to a field (its signature begins FIELD), false for a method.</summary>
        public bool MemberRefIsField(int row)
        {
            int blob = MemberRefSignature(row);
            int at = BlobStart(blob, out int length);
            return length > 0 && _blobs[at] == 0x06;
        }

        // ---------------------------------------------------------------- specifications

        public int TypeSpecSignature(int row)
        {
            return Read(Row(TypeSpec, row), _blob);
        }

        /// <summary>The method a generic method instance is of, as a token (a method of the mod's or a member reference).</summary>
        public int MethodSpecMethod(int row)
        {
            int coded = Read(Row(MethodSpec, row), _mdorSize);
            return (coded & 1) == 0 ? Token(MethodDef, coded >> 1) : Token(MemberRef, coded >> 1);
        }

        public int MethodSpecInstantiation(int row)
        {
            return Read(Row(MethodSpec, row) + _mdorSize, _blob);
        }

        public int StandAloneSigSignature(int row)
        {
            return Read(Row(StandAloneSig, row), _blob);
        }

        // ---------------------------------------------------------------- the mod's own types and methods

        public string TypeDefName(int row)
        {
            return String(Read(Row(TypeDef, row) + 4, _str));
        }

        public string TypeDefNamespace(int row)
        {
            return String(Read(Row(TypeDef, row) + 4 + _str, _str));
        }

        /// <summary>The type a type of the mod derives from, as a token (0 for none).</summary>
        public int TypeDefExtends(int row)
        {
            int coded = Read(Row(TypeDef, row) + 4 + 2 * _str, _tdorSize);
            return coded == 0 ? 0 : TypeToken(coded);
        }

        private int TypeDefFieldList(int row)
        {
            return Read(Row(TypeDef, row) + 4 + 2 * _str + _tdorSize, Index(Field));
        }

        private int TypeDefMethodList(int row)
        {
            return Read(Row(TypeDef, row) + 4 + 2 * _str + _tdorSize + Index(Field), Index(MethodDef));
        }

        /// <summary>The first and one past the last field row of a type of the mod.</summary>
        public void FieldsOf(int row, out int first, out int end)
        {
            first = TypeDefFieldList(row);
            end = row < _rows[TypeDef] ? TypeDefFieldList(row + 1) : _rows[Field] + 1;
        }

        /// <summary>The first and one past the last method row of a type of the mod.</summary>
        public void MethodsOf(int row, out int first, out int end)
        {
            first = TypeDefMethodList(row);
            end = row < _rows[TypeDef] ? TypeDefMethodList(row + 1) : _rows[MethodDef] + 1;
        }

        public string FieldName(int row)
        {
            return String(Read(Row(Field, row) + 2, _str));
        }

        public int FieldSignature(int row)
        {
            return Read(Row(Field, row) + 2 + _str, _blob);
        }

        public bool FieldIsStatic(int row)
        {
            return (U16(_tables, Row(Field, row)) & 0x0010) != 0;
        }

        public int MethodRva(int row)
        {
            return I32(_tables, Row(MethodDef, row));
        }

        /// <summary>The method's implementation flags (MethodImplAttributes: 0x0008 NoInlining).</summary>
        public int MethodImplFlags(int row)
        {
            return U16(_tables, Row(MethodDef, row) + 4);
        }

        public bool MethodNoInlining(int row)
        {
            return (MethodImplFlags(row) & 0x0008) != 0;
        }

        public string MethodName(int row)
        {
            return String(Read(Row(MethodDef, row) + 8, _str));
        }

        public int MethodSignature(int row)
        {
            return Read(Row(MethodDef, row) + 8 + _str, _blob);
        }

        /// <summary>The type of the mod an interface implementation row is of.</summary>
        public int InterfaceImplClass(int row)
        {
            return Read(Row(InterfaceImpl, row), Index(TypeDef));
        }

        /// <summary>The interface of an interface implementation row, as a token.</summary>
        public int InterfaceImplInterface(int row)
        {
            return TypeToken(Read(Row(InterfaceImpl, row) + Index(TypeDef), _tdorSize));
        }

        /// <summary>The type of the mod each type of it is nested in (0: none), by row.</summary>
        public int[] Enclosing()
        {
            var outer = new int[_rows[TypeDef] + 1];
            for (int i = 1; i <= _rows[NestedClass]; i++)
            {
                int at = Row(NestedClass, i);
                int nested = Read(at, Index(TypeDef));
                int enclosing = Read(at + Index(TypeDef), Index(TypeDef));
                if (nested > 0 && nested < outer.Length)
                {
                    outer[nested] = enclosing;
                }
            }
            return outer;
        }

        /// <summary>The type of the mod each method row belongs to.</summary>
        public int[] MethodOwners()
        {
            var owner = new int[_rows[MethodDef] + 1];
            for (int t = 1; t <= _rows[TypeDef]; t++)
            {
                MethodsOf(t, out int first, out int end);
                for (int m = first; m < end && m < owner.Length; m++)
                {
                    owner[m] = t;
                }
            }
            return owner;
        }

        /// <summary>The type of the mod each field row belongs to.</summary>
        public int[] FieldOwners()
        {
            var owner = new int[_rows[Field] + 1];
            for (int t = 1; t <= _rows[TypeDef]; t++)
            {
                FieldsOf(t, out int first, out int end);
                for (int f = first; f < end && f < owner.Length; f++)
                {
                    owner[f] = t;
                }
            }
            return owner;
        }

        // ---------------------------------------------------------------- heaps

        private readonly Dictionary<int, string> _names = new Dictionary<int, string>();

        /// <summary>A string of #Strings by its index.</summary>
        public string String(int index)
        {
            if (index <= 0 || index >= _strings.Length)
            {
                return string.Empty;
            }
            if (_names.TryGetValue(index, out string known))
            {
                return known;
            }
            int end = index;
            while (end < _strings.Length && _strings[end] != 0)
            {
                end++;
            }
            string s = Encoding.UTF8.GetString(_strings, index, end - index);
            _names[index] = s;
            return s;
        }

        // The start of a blob's bytes, and its length.
        private int BlobStart(int index, out int length)
        {
            length = 0;
            if (index <= 0 || index >= _blobs.Length)
            {
                return index;
            }
            int at = index;
            length = Compressed(_blobs, ref at);
            if (length < 0 || at + length > _blobs.Length)
            {
                length = 0;
            }
            return at;
        }

        /// <summary>A string of #US (the user strings: ldstr's) by its token, read from the file; null when it cannot be.</summary>
        public string UserString(int token)
        {
            int index = RowOf(token);
            if (TableOf(token) != 0x70 || index <= 0 || index >= _userStringsSize)
            {
                return null;
            }
            try
            {
                byte[] bytes = _file;
                long at = _userStrings + index;
                if (bytes == null)
                {
                    using (var file = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                    {
                        byte[] head = ReadAt(file, at, (int)Math.Min(4096, file.Length - at));
                        int p = 0;
                        int length = Compressed(head, ref p);
                        return length > 1 && p + length <= head.Length ? Encoding.Unicode.GetString(head, p, length - 1) : string.Empty;
                    }
                }
                int q = (int)at;
                int count = Compressed(bytes, ref q);
                return count > 1 ? Encoding.Unicode.GetString(bytes, q, count - 1) : string.Empty;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Every type token a signature blob names (TypeDef, TypeRef and TypeSpec tokens, custom modifiers included), added
        /// to <paramref name="into"/>. The blob's kind says how it is read: a type specification and a generic method's
        /// instance are not told from a method's signature by their first byte.
        /// </summary>
        public void TypesIn(int blob, BlobKind kind, List<int> into)
        {
            int at = BlobStart(blob, out int length);
            if (length <= 0)
            {
                return;
            }
            int end = at + length;
            try
            {
                byte head = _blobs[at];
                switch (kind)
                {
                    case BlobKind.Member:      // a member reference: a field's, else a method's
                    case BlobKind.StandAlone:  // a stand-alone signature: a method's locals, else calli's method
                        kind = head == 0x06 ? BlobKind.Field : head == 0x07 ? BlobKind.Locals : BlobKind.Method;
                        break;
                }
                switch (kind)
                {
                    case BlobKind.Field:
                        at++;
                        Type(ref at, end, into);
                        return;
                    case BlobKind.Locals:
                        at++;
                        int locals = Compressed(_blobs, ref at);
                        for (int i = 0; i < locals && at < end; i++)
                        {
                            Type(ref at, end, into);
                        }
                        return;
                    case BlobKind.Property:
                        at++;
                        int index = Compressed(_blobs, ref at);
                        for (int i = 0; i <= index && at < end; i++)
                        {
                            Type(ref at, end, into);
                        }
                        return;
                    case BlobKind.Instance:
                        at++;
                        int arguments = Compressed(_blobs, ref at);
                        for (int i = 0; i < arguments && at < end; i++)
                        {
                            Type(ref at, end, into);
                        }
                        return;
                    case BlobKind.Method:
                        Method(ref at, end, into);
                        return;
                    default:   // a type specification: one type
                        Type(ref at, end, into);
                        return;
                }
            }
            catch (IndexOutOfRangeException)
            {
                // (a signature that runs past its blob names nothing more)
            }
        }

        // A method's signature (def, ref or stand-alone): calling convention, its generic count, its parameters' count, return and parameters.
        private void Method(ref int at, int end, List<int> into)
        {
            byte convention = _blobs[at++];
            if ((convention & 0x10) != 0)
            {
                Compressed(_blobs, ref at);   // (generic parameters)
            }
            int count = Compressed(_blobs, ref at);
            for (int i = 0; i <= count && at < end; i++)
            {
                if (_blobs[at] == 0x41)
                {
                    at++;   // (SENTINEL: the vararg part follows)
                }
                Type(ref at, end, into);
            }
        }

        // One type of a signature (ECMA-335 II.23.2.12), its custom modifiers and constraints included.
        private void Type(ref int at, int end, List<int> into)
        {
            for (int guard = 0; guard < 64 && at < end; guard++)
            {
                byte e = _blobs[at++];
                switch (e)
                {
                    case 0x1F:   // CMOD_REQD
                    case 0x20:   // CMOD_OPT
                        into.Add(TypeToken(Compressed(_blobs, ref at)));
                        continue;
                    case 0x45:   // PINNED
                    case 0x10:   // BYREF
                    case 0x0F:   // PTR
                    case 0x1D:   // SZARRAY
                        continue;
                    case 0x11:   // VALUETYPE
                    case 0x12:   // CLASS
                        into.Add(TypeToken(Compressed(_blobs, ref at)));
                        return;
                    case 0x13:   // VAR
                    case 0x1E:   // MVAR
                        Compressed(_blobs, ref at);
                        _open = true;
                        return;
                    case 0x14:   // ARRAY: the element, its rank, sizes and low bounds
                        Type(ref at, end, into);
                        Compressed(_blobs, ref at);
                        int sizes = Compressed(_blobs, ref at);
                        for (int i = 0; i < sizes; i++)
                        {
                            Compressed(_blobs, ref at);
                        }
                        int bounds = Compressed(_blobs, ref at);
                        for (int i = 0; i < bounds; i++)
                        {
                            Compressed(_blobs, ref at);
                        }
                        return;
                    case 0x15:   // GENERICINST: CLASS or VALUETYPE, the generic type, its arguments
                        at++;
                        into.Add(TypeToken(Compressed(_blobs, ref at)));
                        int arguments = Compressed(_blobs, ref at);
                        for (int i = 0; i < arguments; i++)
                        {
                            Type(ref at, end, into);
                        }
                        return;
                    case 0x1B:   // FNPTR: a method signature
                        Method(ref at, end, into);
                        return;
                    default:     // a primitive, object, string, typed reference, void
                        return;
                }
            }
        }

        /// <summary>The generic type a type specification is an instance of, as a token (0 when it is not a generic instance).</summary>
        public int TypeSpecGeneric(int row)
        {
            int at = BlobStart(TypeSpecSignature(row), out int length);
            if (length < 3 || _blobs[at] != 0x15)
            {
                return 0;
            }
            at += 2;
            return TypeToken(Compressed(_blobs, ref at));
        }

        /// <summary>True when a type specification names a generic parameter (VAR or MVAR): one of the mod's own generic code's, resolved only with its context.</summary>
        public bool TypeSpecIsOpen(int row)
        {
            _open = false;
            _scratch.Clear();
            TypesIn(TypeSpecSignature(row), BlobKind.Spec, _scratch);
            return _open;
        }

        private bool _open;   // set by Type when it meets VAR or MVAR
        private readonly List<int> _scratch = new List<int>(8);

        private static int Compressed(byte[] data, ref int at)
        {
            byte b = data[at];
            if ((b & 0x80) == 0)
            {
                at += 1;
                return b;
            }
            if ((b & 0xC0) == 0x80)
            {
                int v = ((b & 0x3F) << 8) | data[at + 1];
                at += 2;
                return v;
            }
            if ((b & 0xE0) == 0xC0)
            {
                int v = ((b & 0x1F) << 24) | (data[at + 1] << 16) | (data[at + 2] << 8) | data[at + 3];
                at += 4;
                return v;
            }
            at += 1;
            return -1;
        }

        // ---------------------------------------------------------------- method bodies

        private readonly List<int> _ops = new List<int>(256);
        private readonly List<int> _operands = new List<int>(256);

        /// <summary>Reads the whole file once, for the method bodies (only when something is missing: Core/ModScan).</summary>
        public void LoadCode()
        {
            if (_file == null)
            {
                _file = File.ReadAllBytes(_path);
            }
        }

        /// <summary>Lets go of the method bodies' bytes.</summary>
        public void DropCode()
        {
            _file = null;
        }

        /// <summary>
        /// The tokens a method's IL names (calls, fields, types, ldtoken, calli's signatures; not strings), its locals'
        /// signature token (0 for none) and its exception clauses' catch types, added to <paramref name="into"/>; false when it
        /// has no body or its body cannot be read. <see cref="LoadCode"/> first.
        /// </summary>
        public bool BodyTokens(int methodRow, List<int> into, out int locals)
        {
            locals = 0;
            int rva = MethodRva(methodRow);
            if (rva == 0 || _file == null)
            {
                return false;
            }
            long offset = Offset(rva);
            if (offset < 0 || offset >= _file.Length)
            {
                return false;
            }
            int at = (int)offset;
            byte head = _file[at];
            int code, size;
            bool more = false;
            if ((head & 3) == 2)
            {
                size = head >> 2;
                code = at + 1;
            }
            else if ((head & 3) == 3)
            {
                int flags = U16(_file, at);
                int headerSize = (flags >> 12) * 4;
                more = (flags & 0x08) != 0;
                size = I32(_file, at + 4);
                locals = I32(_file, at + 8);
                code = at + headerSize;
            }
            else
            {
                return false;
            }
            if (code + size > _file.Length)
            {
                return false;
            }
            _ops.Clear();
            _operands.Clear();
            Decode(_file, code, code + size, _ops, _operands);
            for (int i = 0; i < _ops.Count; i++)
            {
                if (_operands[i] != 0 && _ops[i] != 0x72)
                {
                    into.Add(_operands[i]);
                }
            }
            if (more)
            {
                Clauses(code + size, into);
            }
            return true;
        }

        /// <summary>
        /// Every instruction of a stretch of IL (ECMA-335 III, the operand of every opcode), in order: its opcode (a two-byte
        /// one as 0xFE00 and its second byte) added to <paramref name="ops"/> and, to <paramref name="operands"/>, its token
        /// (a member, a type, a signature or ldstr's string) or 0 for an instruction without one.
        /// </summary>
        public static void Decode(byte[] il, int start, int end, List<int> ops, List<int> operands)
        {
            int i = start;
            while (i < end)
            {
                int op = il[i++];
                int operand = 0;
                if (op == 0xFE)
                {
                    if (i >= end)
                    {
                        return;
                    }
                    int two = il[i++];
                    op = 0xFE00 | two;
                    switch (two)
                    {
                        case 0x06: case 0x07: case 0x15: case 0x16: case 0x1C:   // ldftn ldvirtftn initobj constrained. sizeof
                            operand = I32(il, i);
                            i += 4;
                            break;
                        case 0x09: case 0x0A: case 0x0B: case 0x0C: case 0x0D: case 0x0E:   // ldarg ldarga starg ldloc ldloca stloc
                            i += 2;
                            break;
                        case 0x12: case 0x19:   // unaligned. no.
                            i += 1;
                            break;
                    }
                }
                else if ((op >= 0x0E && op <= 0x13) || op == 0x1F || (op >= 0x2B && op <= 0x37) || op == 0xDE)
                {
                    i += 1;
                }
                else if (op == 0x20 || op == 0x22 || (op >= 0x38 && op <= 0x44) || op == 0xDD)
                {
                    i += 4;
                }
                else if (op == 0x21 || op == 0x23)
                {
                    i += 8;
                }
                else if (op == 0x45)   // switch
                {
                    int n = I32(il, i);
                    i += 4 + 4 * n;
                }
                else if (op == 0x72 || op == 0x29 || op == 0x27 || op == 0x28 || op == 0x6F || op == 0x73 || op == 0x70 || op == 0x71 || op == 0x74 || op == 0x75
                         || op == 0x79 || (op >= 0x7B && op <= 0x81) || op == 0x8C || op == 0x8D || op == 0x8F || op == 0xA3 || op == 0xA4 || op == 0xA5
                         || op == 0xC2 || op == 0xC6 || op == 0xD0)
                {
                    operand = I32(il, i);   // (ldstr's is a string's, calli's a signature's; the rest a member's or a type's)
                    i += 4;
                }
                ops.Add(op);
                operands.Add(operand);
            }
        }

        // The exception clauses after a fat body (ECMA-335 II.25.4.5): a typed catch's class token.
        private void Clauses(int after, List<int> into)
        {
            int at = (after + 3) & ~3;
            for (int sections = 0; sections < 16 && at + 4 <= _file.Length; sections++)
            {
                byte kind = _file[at];
                bool fat = (kind & 0x40) != 0;
                int size = fat ? (_file[at + 1] | (_file[at + 2] << 8) | (_file[at + 3] << 16)) : _file[at + 1];
                if ((kind & 0x01) != 0)
                {
                    int clause = fat ? 24 : 12;
                    int count = (size - 4) / clause;
                    for (int c = 0; c < count; c++)
                    {
                        int p = at + 4 + c * clause;
                        int flags = fat ? I32(_file, p) : U16(_file, p);
                        int token = fat ? I32(_file, p + 20) : I32(_file, p + 8);
                        if (flags == 0 && token != 0)
                        {
                            into.Add(token);
                        }
                    }
                }
                if ((kind & 0x80) == 0 || size <= 0)
                {
                    break;
                }
                at += size;
                at = (at + 3) & ~3;
            }
        }

        /// <summary>The bytes of a method's IL (for reading it instruction by instruction), or null. <see cref="LoadCode"/> first.</summary>
        public byte[] Code(int methodRow, out int start, out int end)
        {
            start = end = 0;
            int rva = MethodRva(methodRow);
            long offset = rva != 0 && _file != null ? Offset(rva) : -1;
            if (offset < 0 || offset >= _file.Length)
            {
                return null;
            }
            int at = (int)offset;
            byte head = _file[at];
            if ((head & 3) == 2)
            {
                start = at + 1;
                end = start + (head >> 2);
            }
            else if ((head & 3) == 3)
            {
                start = at + (U16(_file, at) >> 12) * 4;
                end = start + I32(_file, at + 4);
            }
            else
            {
                return null;
            }
            return end <= _file.Length ? _file : null;
        }

        // ---------------------------------------------------------------- little-endian

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
