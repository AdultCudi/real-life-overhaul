using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace RealLifeOverhaul
{
    /// <summary>The mod's assembly read as a file: its heaps, and the types its signatures name.</summary>
    public sealed partial class ModImage
    {
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
        /// Adds every type token a signature blob names, custom modifiers included. <paramref name="kind"/> says how to
        /// read it: a type specification or a method instance is not told from a method's signature by its first byte.
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
                // a signature that runs past its blob names nothing more
            }
        }

        // A method's signature: calling convention, generic count, parameter count, return and parameters.
        private void Method(ref int at, int end, List<int> into)
        {
            byte convention = _blobs[at++];
            if ((convention & 0x10) != 0)
            {
                Compressed(_blobs, ref at);   // generic parameters
            }
            int count = Compressed(_blobs, ref at);
            for (int i = 0; i <= count && at < end; i++)
            {
                if (_blobs[at] == 0x41)
                {
                    at++;   // SENTINEL: the vararg part follows
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

        /// <summary>True when a type specification names a generic parameter (VAR or MVAR), resolved only with its context.</summary>
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
    }
}
