using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Every type, field and method of the game's and Unity's assemblies the built mod names in its compiled code,
    /// looked for in the assemblies on disk. One that is not there (or has another signature) would stop each method of
    /// the mod that uses it the first time it runs; the mod's guards keep that to its feature, and this says it before
    /// a player does. For each one it names the mod's types whose code uses it.
    /// </summary>
    internal sealed class ModReferences
    {
        private readonly GameFiles _files;
        private readonly GameAssembly _mod;
        private readonly Dictionary<int, HashSet<string>> _users = new Dictionary<int, HashSet<string>>();

        public int TypesChecked { get; private set; }
        public int MembersChecked { get; private set; }
        public readonly List<string> Missing = new List<string>();

        public ModReferences(GameFiles files, string modPath)
        {
            _files = files;
            _mod = new GameAssembly(Path.GetFileNameWithoutExtension(modPath), modPath);
        }

        public void Run()
        {
            ReadUsers();
            MetadataReader md = _mod.Md;
            foreach (TypeReferenceHandle h in md.TypeReferences)
            {
                GameFiles.Named(md, h, out string assembly, out string full);
                if (!_files.Has(assembly))
                {
                    continue;   // the mod's own references to what is not the game's (none expected)
                }
                TypesChecked++;
                if (!_files.Find(assembly, full).Exists)
                {
                    Missing.Add("type " + full + " (" + assembly + ")" + UsedBy(MetadataTokens(h)));
                }
            }
            foreach (MemberReferenceHandle h in md.MemberReferences)
            {
                MemberReference r = md.GetMemberReference(h);
                FoundType owner;
                string ownerName;
                if (!Owner(r.Parent, out owner, out ownerName))
                {
                    continue;
                }
                MembersChecked++;
                if (!owner.Exists)
                {
                    continue;   // (said as a missing type above)
                }
                string name = md.GetString(r.Name);
                string problem = r.GetKind() == MemberReferenceKind.Field ? FieldProblem(owner, name, r) : MethodProblem(owner, name, r);
                if (problem != null)
                {
                    Missing.Add(ownerName + "." + name + ": " + problem + UsedBy(MetadataTokens(h)));
                }
            }
        }

        /// <summary>The game on disk asked about one type reference of the mod's, as Core/ModScan asks the running game: null when it is there.</summary>
        public string TypeProblem(int token)
        {
            var h = (TypeReferenceHandle)System.Reflection.Metadata.Ecma335.MetadataTokens.EntityHandle(token);
            GameFiles.Named(_mod.Md, h, out string assembly, out string full);
            if (!_files.Has(assembly))
            {
                return null;   // (an assembly not in the game's folder is not the game's to answer for)
            }
            return _files.Find(assembly, full).Exists ? null : "the type " + full + " is not in " + assembly;
        }

        /// <summary>The game on disk asked about one member reference of the mod's: null when it is there with the signature the mod was built against.</summary>
        public string MemberProblem(int token)
        {
            var h = (MemberReferenceHandle)System.Reflection.Metadata.Ecma335.MetadataTokens.EntityHandle(token);
            MemberReference r = _mod.Md.GetMemberReference(h);
            if (!Owner(r.Parent, out FoundType owner, out string ownerName))
            {
                return null;
            }
            if (!owner.Exists)
            {
                return "its type " + ownerName + " is not in the game";
            }
            string name = _mod.Md.GetString(r.Name);
            return r.GetKind() == MemberReferenceKind.Field ? FieldProblem(owner, name, r) : MethodProblem(owner, name, r);
        }

        /// <summary>The parameters' types of a member reference of the mod's that is a method, by their full names; null for a field.</summary>
        public string[] Parameters(int token)
        {
            var h = (MemberReferenceHandle)System.Reflection.Metadata.Ecma335.MetadataTokens.EntityHandle(token);
            MemberReference r = _mod.Md.GetMemberReference(h);
            if (r.GetKind() != MemberReferenceKind.Method)
            {
                return null;
            }
            MethodSignature<string> s = r.DecodeMethodSignature(Full, new FullCtx(_mod.Md));
            var types = new string[s.ParameterTypes.Length];
            s.ParameterTypes.CopyTo(types);
            return types;
        }

        private static int MetadataTokens(EntityHandle h)
        {
            return System.Reflection.Metadata.Ecma335.MetadataTokens.GetToken(h);
        }

        // The game's type a member reference is on; false for the mod's own and for what is not in the game's folder.
        private bool Owner(EntityHandle parent, out FoundType owner, out string name)
        {
            owner = default;
            name = null;
            MetadataReader md = _mod.Md;
            if (parent.Kind == HandleKind.TypeReference)
            {
                GameFiles.Named(md, (TypeReferenceHandle)parent, out string assembly, out string full);
                if (!_files.Has(assembly))
                {
                    return false;
                }
                owner = _files.Find(assembly, full);
                name = full;
                return true;
            }
            if (parent.Kind == HandleKind.TypeSpecification)
            {
                TypeSpecification spec = md.GetTypeSpecification((TypeSpecificationHandle)parent);
                BlobReader blob = md.GetBlobReader(spec.Signature);
                if (blob.ReadByte() != 0x15)
                {
                    return false;   // an array's own methods (Get, Set, Address)
                }
                blob.ReadByte();
                EntityHandle generic = blob.ReadTypeHandle();
                if (generic.Kind != HandleKind.TypeReference)
                {
                    return false;
                }
                return Owner(generic, out owner, out name);
            }
            return false;
        }

        private string FieldProblem(FoundType owner, string name, MemberReference r)
        {
            string wanted = r.DecodeFieldSignature(Full, new FullCtx(_mod.Md));
            foreach (FoundType t in _files.Chain(owner))
            {
                MetadataReader md = t.Assembly.Md;
                foreach (FieldDefinitionHandle h in t.Def.GetFields())
                {
                    FieldDefinition f = md.GetFieldDefinition(h);
                    if (md.GetString(f.Name) == name)
                    {
                        string actual = f.DecodeSignature(Full, new FullCtx(md));
                        return actual == wanted ? null : "the field's type is now " + actual + ", the mod was built against " + wanted;
                    }
                }
            }
            return "the field is missing";
        }

        private string MethodProblem(FoundType owner, string name, MemberReference r)
        {
            MethodSignature<string> wanted = r.DecodeMethodSignature(Full, new FullCtx(_mod.Md));
            string want = Text(wanted);
            string near = null;
            foreach (FoundType t in _files.Chain(owner))
            {
                MetadataReader md = t.Assembly.Md;
                foreach (MethodDefinitionHandle h in t.Def.GetMethods())
                {
                    MethodDefinition d = md.GetMethodDefinition(h);
                    if (md.GetString(d.Name) != name)
                    {
                        continue;
                    }
                    string have = Text(d.DecodeSignature(Full, new FullCtx(md)));
                    if (have == want)
                    {
                        return null;
                    }
                    near = near ?? have;
                }
                if (IsInterface(t))
                {
                    // An interface's members are its own; a method of System.Object is reached through any of them.
                    FoundType root = _files.Find("mscorlib", "System.Object");
                    if (root.Exists && !SameType(root, t))
                    {
                        string found = InType(root, name, want);
                        if (found == null)
                        {
                            return null;
                        }
                    }
                    foreach (InterfaceImplementationHandle ih in t.Def.GetInterfaceImplementations())
                    {
                        FoundType inner = _files.Resolve(t.Assembly, md.GetInterfaceImplementation(ih).Interface, null);
                        if (inner.Exists && InType(inner, name, want) == null)
                        {
                            return null;
                        }
                    }
                }
            }
            return near != null ? "its signature is now " + near + ", the mod was built against " + want : "the method is missing";
        }

        private string InType(FoundType t, string name, string want)
        {
            MetadataReader md = t.Assembly.Md;
            foreach (MethodDefinitionHandle h in t.Def.GetMethods())
            {
                MethodDefinition d = md.GetMethodDefinition(h);
                if (md.GetString(d.Name) == name && Text(d.DecodeSignature(Full, new FullCtx(md))) == want)
                {
                    return null;
                }
            }
            return "no";
        }

        private static bool IsInterface(FoundType t)
        {
            return (t.Def.Attributes & TypeAttributes.Interface) != 0;
        }

        private static bool SameType(FoundType a, FoundType b)
        {
            return a.Assembly == b.Assembly && a.Handle == b.Handle;
        }

        private static string Text(MethodSignature<string> s)
        {
            return (s.Header.IsInstance ? "instance " : string.Empty) + s.ReturnType + " <" + s.GenericParameterCount + ">(" + string.Join(",", s.ParameterTypes) + ")";
        }

        // ---------------------------------------------------------------- who uses what: the tokens in the mod's code

        private void ReadUsers()
        {
            MetadataReader md = _mod.Md;
            foreach (TypeDefinitionHandle th in md.TypeDefinitions)
            {
                string user = Outermost(md, th);
                foreach (MethodDefinitionHandle mh in md.GetTypeDefinition(th).GetMethods())
                {
                    MethodDefinition m = md.GetMethodDefinition(mh);
                    if (m.RelativeVirtualAddress == 0)
                    {
                        continue;
                    }
                    ImmutableArray<byte> il = _mod.Pe.GetMethodBody(m.RelativeVirtualAddress).GetILContent();
                    foreach (int token in IlTokens.Read(il))
                    {
                        Note(token, user);
                        if ((token >> 24) == 0x2B)   // a generic method's instance: the method it is of
                        {
                            MethodSpecification spec = md.GetMethodSpecification((MethodSpecificationHandle)System.Reflection.Metadata.Ecma335.MetadataTokens.EntityHandle(token));
                            Note(System.Reflection.Metadata.Ecma335.MetadataTokens.GetToken(spec.Method), user);
                        }
                    }
                }
            }
        }

        private void Note(int token, string user)
        {
            if (!_users.TryGetValue(token, out HashSet<string> set))
            {
                _users[token] = set = new HashSet<string>(StringComparer.Ordinal);
            }
            set.Add(user);
        }

        private static string Outermost(MetadataReader md, TypeDefinitionHandle h)
        {
            TypeDefinition t = md.GetTypeDefinition(h);
            while (!t.GetDeclaringType().IsNil)
            {
                h = t.GetDeclaringType();
                t = md.GetTypeDefinition(h);
            }
            return GameAssembly.FullName(md, h);
        }

        private string UsedBy(int token)
        {
            if (!_users.TryGetValue(token, out HashSet<string> set) || set.Count == 0)
            {
                return "; in a signature of the mod's (no code names it)";
            }
            var list = new List<string>(set);
            list.Sort(StringComparer.Ordinal);
            return "; used by " + string.Join(", ", list.Count > 6 ? list.GetRange(0, 6) : list) + (list.Count > 6 ? " and " + (list.Count - 6) + " more" : string.Empty);
        }

        // ---------------------------------------------------------------- full names, for comparing signatures exactly

        internal readonly struct FullCtx
        {
            public readonly MetadataReader Md;

            public FullCtx(MetadataReader md)
            {
                Md = md;
            }
        }

        private static readonly FullText Full = new FullText();

        private sealed class FullText : ISignatureTypeProvider<string, FullCtx>
        {
            public string GetPrimitiveType(PrimitiveTypeCode c) => "System." + c;

            public string GetTypeFromDefinition(MetadataReader md, TypeDefinitionHandle h, byte k) => GameAssembly.FullName(md, h);

            public string GetTypeFromReference(MetadataReader md, TypeReferenceHandle h, byte k)
            {
                GameFiles.Named(md, h, out string assembly, out string full);
                return full;
            }

            public string GetTypeFromSpecification(MetadataReader md, FullCtx ctx, TypeSpecificationHandle h, byte k) => md.GetTypeSpecification(h).DecodeSignature(this, ctx);
            public string GetSZArrayType(string e) => e + "[]";
            public string GetArrayType(string e, ArrayShape s) => e + "[" + new string(',', Math.Max(0, s.Rank - 1)) + "]";
            public string GetByReferenceType(string e) => e + "&";
            public string GetPointerType(string e) => e + "*";
            public string GetPinnedType(string e) => e;
            public string GetModifiedType(string m, string u, bool r) => u;
            public string GetFunctionPointerType(MethodSignature<string> s) => "fnptr";
            public string GetGenericInstantiation(string g, ImmutableArray<string> a) => g + "<" + string.Join(",", a) + ">";
            public string GetGenericTypeParameter(FullCtx ctx, int i) => "!" + i;
            public string GetGenericMethodParameter(FullCtx ctx, int i) => "!!" + i;
        }
    }

    /// <summary>The metadata tokens in a method's IL (calls, fields, types, ldtoken), by the operand table of ECMA-335.</summary>
    internal static class IlTokens
    {
        public static IEnumerable<int> Read(ImmutableArray<byte> il)
        {
            int i = 0;
            while (i < il.Length)
            {
                int op = il[i++];
                if (op == 0xFE)
                {
                    if (i >= il.Length)
                    {
                        yield break;
                    }
                    int two = il[i++];
                    switch (two)
                    {
                        case 0x06: case 0x07: case 0x15: case 0x16: case 0x1C:   // ldftn ldvirtftn initobj constrained. sizeof
                            yield return Int(il, i);
                            i += 4;
                            break;
                        case 0x09: case 0x0A: case 0x0B: case 0x0C: case 0x0D: case 0x0E:   // ldarg ldarga starg ldloc ldloca stloc
                            i += 2;
                            break;
                        case 0x12: case 0x19:   // unaligned. no.
                            i += 1;
                            break;
                    }
                    continue;
                }
                if ((op >= 0x0E && op <= 0x13) || op == 0x1F || (op >= 0x2B && op <= 0x37) || op == 0xDE)
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
                else if (op == 0x45)
                {
                    int n = Int(il, i);
                    i += 4 + 4 * n;
                }
                else if (op == 0x72 || op == 0x29)   // ldstr, calli: a string or a signature, not a member
                {
                    i += 4;
                }
                else if (op == 0x27 || op == 0x28 || op == 0x6F || op == 0x73 || op == 0x70 || op == 0x71 || op == 0x74 || op == 0x75 || op == 0x79
                         || (op >= 0x7B && op <= 0x81) || op == 0x8C || op == 0x8D || op == 0x8F || op == 0xA3 || op == 0xA4 || op == 0xA5
                         || op == 0xC2 || op == 0xC6 || op == 0xD0)
                {
                    yield return Int(il, i);
                    i += 4;
                }
            }
        }

        private static int Int(ImmutableArray<byte> il, int at)
        {
            return il[at] | (il[at + 1] << 8) | (il[at + 2] << 16) | (il[at + 3] << 24);
        }
    }
}
