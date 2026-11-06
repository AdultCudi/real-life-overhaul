namespace RealLifeOverhaul
{
    /// <summary>The mod's assembly read as a file: its type and member references and its own types, methods and fields.</summary>
    public sealed partial class ModImage
    {
        /// <summary>A type reference's resolution scope as a token: an assembly reference, a nested one's outer type, a module.</summary>
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

        /// <summary>The simple name of the assembly a type reference is in (a nested one's outermost); null for the mod's own module.</summary>
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

        /// <summary>The parent of a member reference as a token: a type reference or specification, a type or method of the mod, a module.</summary>
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
    }
}
