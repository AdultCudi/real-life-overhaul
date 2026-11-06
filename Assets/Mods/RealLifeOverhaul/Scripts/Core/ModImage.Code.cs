using System.Collections.Generic;
using System.IO;

namespace RealLifeOverhaul
{
    /// <summary>The mod's assembly read as a file: its methods' IL.</summary>
    public sealed partial class ModImage
    {
        private readonly List<int> _ops = new List<int>(256);
        private readonly List<int> _operands = new List<int>(256);

        /// <summary>Reads the whole file once, for the method bodies.</summary>
        public void LoadCode()
        {
            if (_file == null)
            {
                _file = File.ReadAllBytes(_path);
            }
        }

        public void DropCode()
        {
            _file = null;
        }

        /// <summary>
        /// Adds the tokens a method's IL names (not strings) and its catch types; <paramref name="locals"/> is its locals'
        /// signature, 0 for none. False when it has no readable body. <see cref="LoadCode"/> first.
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
        /// Every instruction of a stretch of IL in order: its opcode (a two-byte one as 0xFE00 plus its second byte) to
        /// <paramref name="ops"/>, its token or 0 to <paramref name="operands"/>.
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
                    operand = I32(il, i);   // ldstr's a string's, calli's a signature's, the rest a member's or a type's
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

        /// <summary>The bytes holding a method's IL, or null. <see cref="LoadCode"/> first.</summary>
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
    }
}
