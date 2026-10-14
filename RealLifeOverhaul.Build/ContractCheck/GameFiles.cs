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
    /// <summary>One assembly read from disk: its types by full name (a nested one after '+') and the types it forwards elsewhere.</summary>
    internal sealed class GameAssembly
    {
        public readonly string Name;
        public readonly PEReader Pe;
        public readonly MetadataReader Md;
        public readonly Dictionary<string, TypeDefinitionHandle> Types = new Dictionary<string, TypeDefinitionHandle>(StringComparer.Ordinal);
        public readonly Dictionary<string, string> Forwards = new Dictionary<string, string>(StringComparer.Ordinal);

        public GameAssembly(string name, string path)
        {
            Name = name;
            Pe = new PEReader(File.OpenRead(path));
            Md = Pe.GetMetadataReader();
            foreach (TypeDefinitionHandle h in Md.TypeDefinitions)
            {
                Types[FullName(Md, h)] = h;
            }
            foreach (ExportedTypeHandle h in Md.ExportedTypes)
            {
                ExportedType e = Md.GetExportedType(h);
                string full = ExportedName(Md, e);
                string target = TargetOf(Md, e);
                if (full != null && target != null)
                {
                    Forwards[full] = target;
                }
            }
        }

        public static string FullName(MetadataReader md, TypeDefinitionHandle h)
        {
            TypeDefinition t = md.GetTypeDefinition(h);
            string name = md.GetString(t.Name);
            TypeDefinitionHandle outer = t.GetDeclaringType();
            if (!outer.IsNil)
            {
                return FullName(md, outer) + "+" + name;
            }
            string ns = md.GetString(t.Namespace);
            return ns.Length > 0 ? ns + "." + name : name;
        }

        private static string ExportedName(MetadataReader md, ExportedType e)
        {
            string name = md.GetString(e.Name);
            if (e.Implementation.Kind == HandleKind.ExportedType)
            {
                string outer = ExportedName(md, md.GetExportedType((ExportedTypeHandle)e.Implementation));
                return outer != null ? outer + "+" + name : null;
            }
            string ns = md.GetString(e.Namespace);
            return ns.Length > 0 ? ns + "." + name : name;
        }

        private static string TargetOf(MetadataReader md, ExportedType e)
        {
            if (e.Implementation.Kind == HandleKind.AssemblyReference)
            {
                return md.GetString(md.GetAssemblyReference((AssemblyReferenceHandle)e.Implementation).Name);
            }
            if (e.Implementation.Kind == HandleKind.ExportedType)
            {
                return TargetOf(md, md.GetExportedType((ExportedTypeHandle)e.Implementation));
            }
            return null;
        }
    }

    /// <summary>A type found in an assembly on disk, with the generic arguments it was reached with (a base type's), if any.</summary>
    internal struct FoundType
    {
        public GameAssembly Assembly;
        public TypeDefinitionHandle Handle;
        public string[] Args;   // short texts of the generic arguments, null for none or unknown

        public bool Exists => Assembly != null;
        public TypeDefinition Def => Assembly.Md.GetTypeDefinition(Handle);
    }

    /// <summary>
    /// The game's Managed folder, read as files: nothing of it is loaded to run. Finds types by assembly and full name
    /// (following type forwarders), walks base types, and writes a type of a signature as ContractBook.TypeText writes
    /// a runtime type, so that the contract's expected types can be compared.
    /// </summary>
    internal sealed class GameFiles
    {
        private readonly string _folder;
        private readonly Dictionary<string, GameAssembly> _loaded = new Dictionary<string, GameAssembly>(StringComparer.OrdinalIgnoreCase);

        public GameFiles(string folder)
        {
            _folder = folder;
        }

        public string Folder => _folder;

        public bool Has(string assembly)
        {
            return Get(assembly) != null;
        }

        public GameAssembly Get(string assembly)
        {
            if (assembly == null)
            {
                return null;
            }
            if (_loaded.TryGetValue(assembly, out GameAssembly a))
            {
                return a;
            }
            string path = Path.Combine(_folder, assembly + ".dll");
            a = File.Exists(path) ? new GameAssembly(assembly, path) : null;
            _loaded[assembly] = a;
            return a;
        }

        public FoundType Find(string assembly, string fullName)
        {
            for (int hop = 0; hop < 8 && assembly != null; hop++)
            {
                GameAssembly a = Get(assembly);
                if (a == null)
                {
                    break;
                }
                if (a.Types.TryGetValue(fullName, out TypeDefinitionHandle h))
                {
                    return new FoundType { Assembly = a, Handle = h };
                }
                if (!a.Forwards.TryGetValue(fullName, out assembly))
                {
                    break;
                }
            }
            return default;
        }

        /// <summary>The assembly and full name a type reference points at (a nested one after '+').</summary>
        public static void Named(MetadataReader md, TypeReferenceHandle h, out string assembly, out string fullName)
        {
            TypeReference r = md.GetTypeReference(h);
            string name = md.GetString(r.Name);
            switch (r.ResolutionScope.Kind)
            {
                case HandleKind.TypeReference:
                    Named(md, (TypeReferenceHandle)r.ResolutionScope, out assembly, out string outer);
                    fullName = outer + "+" + name;
                    return;
                case HandleKind.AssemblyReference:
                    assembly = md.GetString(md.GetAssemblyReference((AssemblyReferenceHandle)r.ResolutionScope).Name);
                    break;
                default:
                    assembly = null;
                    break;
            }
            string ns = md.GetString(r.Namespace);
            fullName = ns.Length > 0 ? ns + "." + name : name;
        }

        /// <summary>The type a handle of one assembly's metadata stands for: its own definition, or a reference resolved on disk.</summary>
        public FoundType Resolve(GameAssembly within, EntityHandle handle, string[] context)
        {
            switch (handle.Kind)
            {
                case HandleKind.TypeDefinition:
                    return new FoundType { Assembly = within, Handle = (TypeDefinitionHandle)handle };
                case HandleKind.TypeReference:
                    Named(within.Md, (TypeReferenceHandle)handle, out string assembly, out string full);
                    return Find(assembly, full);
                case HandleKind.TypeSpecification:
                    TypeSpecification spec = within.Md.GetTypeSpecification((TypeSpecificationHandle)handle);
                    var instance = spec.DecodeSignature(new Instance(this, within), new Ctx(within.Md, default, context));
                    return instance;
                default:
                    return default;
            }
        }

        /// <summary>The type and the base types above it, each with the generic arguments it is reached with.</summary>
        public IEnumerable<FoundType> Chain(FoundType start)
        {
            FoundType t = start;
            for (int depth = 0; depth < 32 && t.Exists; depth++)
            {
                yield return t;
                EntityHandle b = t.Def.BaseType;
                if (b.IsNil)
                {
                    yield break;
                }
                t = ResolveWithArgs(t.Assembly, b, t.Args);
            }
        }

        /// <summary>Short type text of a signature blob (field, property, method), as ContractBook.TypeText writes a runtime type.</summary>
        public static ShortText Short = new ShortText();

        /// <summary>What a decoded type in a signature is reached through: a type's own generic parameters' names, and its arguments if known.</summary>
        internal readonly struct Ctx
        {
            public readonly MetadataReader Md;
            public readonly TypeDefinitionHandle Owner;
            public readonly string[] Args;
            public readonly MethodDefinitionHandle Method;

            public Ctx(MetadataReader md, TypeDefinitionHandle owner, string[] args, MethodDefinitionHandle method = default)
            {
                Md = md;
                Owner = owner;
                Args = args;
                Method = method;
            }
        }

        /// <summary>ContractBook.TypeText's way of writing a type, from metadata.</summary>
        internal sealed class ShortText : ISignatureTypeProvider<string, Ctx>
        {
            public string GetPrimitiveType(PrimitiveTypeCode c)
            {
                switch (c)
                {
                    case PrimitiveTypeCode.Boolean: return "Boolean";
                    case PrimitiveTypeCode.Char: return "Char";
                    case PrimitiveTypeCode.SByte: return "SByte";
                    case PrimitiveTypeCode.Byte: return "Byte";
                    case PrimitiveTypeCode.Int16: return "Int16";
                    case PrimitiveTypeCode.UInt16: return "UInt16";
                    case PrimitiveTypeCode.Int32: return "Int32";
                    case PrimitiveTypeCode.UInt32: return "UInt32";
                    case PrimitiveTypeCode.Int64: return "Int64";
                    case PrimitiveTypeCode.UInt64: return "UInt64";
                    case PrimitiveTypeCode.Single: return "Single";
                    case PrimitiveTypeCode.Double: return "Double";
                    case PrimitiveTypeCode.String: return "String";
                    case PrimitiveTypeCode.Object: return "Object";
                    case PrimitiveTypeCode.IntPtr: return "IntPtr";
                    case PrimitiveTypeCode.UIntPtr: return "UIntPtr";
                    case PrimitiveTypeCode.TypedReference: return "TypedReference";
                    default: return "Void";
                }
            }

            public string GetTypeFromDefinition(MetadataReader md, TypeDefinitionHandle h, byte rawTypeKind)
            {
                TypeDefinition t = md.GetTypeDefinition(h);
                string name = ContractBook.Bare(md.GetString(t.Name));
                TypeDefinitionHandle outer = t.GetDeclaringType();
                return outer.IsNil ? name : ContractBook.Bare(md.GetString(md.GetTypeDefinition(outer).Name)) + "." + name;
            }

            public string GetTypeFromReference(MetadataReader md, TypeReferenceHandle h, byte rawTypeKind)
            {
                TypeReference r = md.GetTypeReference(h);
                string name = ContractBook.Bare(md.GetString(r.Name));
                if (r.ResolutionScope.Kind == HandleKind.TypeReference)
                {
                    TypeReference outer = md.GetTypeReference((TypeReferenceHandle)r.ResolutionScope);
                    return ContractBook.Bare(md.GetString(outer.Name)) + "." + name;
                }
                return name;
            }

            public string GetTypeFromSpecification(MetadataReader md, Ctx ctx, TypeSpecificationHandle h, byte rawTypeKind)
            {
                return md.GetTypeSpecification(h).DecodeSignature(this, ctx);
            }

            public string GetSZArrayType(string element) => element + "[]";
            public string GetArrayType(string element, ArrayShape shape) => element + "[" + new string(',', Math.Max(0, shape.Rank - 1)) + "]";
            public string GetByReferenceType(string element) => element + "&";
            public string GetPointerType(string element) => element + "*";
            public string GetPinnedType(string element) => element;
            public string GetModifiedType(string modifier, string unmodified, bool isRequired) => unmodified;
            public string GetFunctionPointerType(MethodSignature<string> signature) => "IntPtr";

            public string GetGenericInstantiation(string generic, ImmutableArray<string> args)
            {
                return generic + "<" + string.Join(",", args) + ">";
            }

            public string GetGenericTypeParameter(Ctx ctx, int index)
            {
                if (ctx.Args != null && index < ctx.Args.Length)
                {
                    return ctx.Args[index];
                }
                if (!ctx.Owner.IsNil)
                {
                    GenericParameterHandleCollection ps = ctx.Md.GetTypeDefinition(ctx.Owner).GetGenericParameters();
                    if (index < ps.Count)
                    {
                        return ctx.Md.GetString(ctx.Md.GetGenericParameter(ps[index]).Name);
                    }
                }
                return "!" + index;
            }

            public string GetGenericMethodParameter(Ctx ctx, int index)
            {
                if (!ctx.Method.IsNil)
                {
                    GenericParameterHandleCollection ps = ctx.Md.GetMethodDefinition(ctx.Method).GetGenericParameters();
                    if (index < ps.Count)
                    {
                        return ctx.Md.GetString(ctx.Md.GetGenericParameter(ps[index]).Name);
                    }
                }
                return "!!" + index;
            }
        }

        // A type specification read as "which type, with which arguments": a base type such as BaTable<EmployeeCellView,EmployeeModel>.
        private sealed class Instance : ISignatureTypeProvider<FoundType, Ctx>
        {
            private readonly GameFiles _files;
            private readonly GameAssembly _within;

            public Instance(GameFiles files, GameAssembly within)
            {
                _files = files;
                _within = within;
            }

            public FoundType GetGenericInstantiation(FoundType generic, ImmutableArray<FoundType> typeArguments)
            {
                return generic;   // (replaced below: the arguments are wanted as text)
            }

            public FoundType GetTypeFromDefinition(MetadataReader md, TypeDefinitionHandle h, byte k) => new FoundType { Assembly = _within, Handle = h };

            public FoundType GetTypeFromReference(MetadataReader md, TypeReferenceHandle h, byte k)
            {
                Named(md, h, out string assembly, out string full);
                return _files.Find(assembly, full);
            }

            public FoundType GetTypeFromSpecification(MetadataReader md, Ctx ctx, TypeSpecificationHandle h, byte k) => default;
            public FoundType GetPrimitiveType(PrimitiveTypeCode c) => default;
            public FoundType GetSZArrayType(FoundType e) => default;
            public FoundType GetArrayType(FoundType e, ArrayShape s) => default;
            public FoundType GetByReferenceType(FoundType e) => default;
            public FoundType GetPointerType(FoundType e) => default;
            public FoundType GetPinnedType(FoundType e) => e;
            public FoundType GetModifiedType(FoundType m, FoundType u, bool r) => u;
            public FoundType GetFunctionPointerType(MethodSignature<FoundType> s) => default;
            public FoundType GetGenericTypeParameter(Ctx ctx, int i) => default;
            public FoundType GetGenericMethodParameter(Ctx ctx, int i) => default;
        }

        /// <summary>The base type of a type spec with its arguments as short text (BaTable&lt;EmployeeCellView,EmployeeModel&gt;).</summary>
        public FoundType ResolveWithArgs(GameAssembly within, EntityHandle handle, string[] context)
        {
            FoundType t = Resolve(within, handle, context);
            if (!t.Exists || handle.Kind != HandleKind.TypeSpecification)
            {
                return t;
            }
            BlobReader blob = within.Md.GetBlobReader(within.Md.GetTypeSpecification((TypeSpecificationHandle)handle).Signature);
            var decoder = new System.Reflection.Metadata.Ecma335.SignatureDecoder<string, Ctx>(Short, within.Md, new Ctx(within.Md, default, context));
            if (blob.ReadByte() != 0x15)   // ELEMENT_TYPE_GENERICINST
            {
                return t;
            }
            blob.ReadByte();   // class or value type
            blob.ReadTypeHandle();
            int count = blob.ReadCompressedInteger();
            var args = new string[count];
            for (int i = 0; i < count; i++)
            {
                args[i] = decoder.DecodeType(ref blob);
            }
            t.Args = args;
            return t;
        }
    }
}
