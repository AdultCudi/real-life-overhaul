using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using RealLifeOverhaul;

// The running mod's own check of its compiled code against the game (Core/ModImage, Core/ModScan as they ship), run here
// on the built mod against the game's assemblies on disk:
//   the reader     every row, name, signature and method body Core/ModImage reads is what .NET's own reader reads
//   this game      what the running mod would find missing on this game (nothing, on the build it is made for), and
//                  for each piece missing the classes and the features it would switch off
//   Build 3675     a game that lacks what a player's Build 3675 lacked (Mono's own words from their Player.log): the
//                  five pieces found, each named only where the mod does without it, so nothing is switched off; each
//                  a line of the contract; and the features each touches through the method that reads it
//   the code       every method of the mod's own that names a game member and is never inlined is called only inside
//                  a try with a catch; every Update, LateUpdate, FixedUpdate and OnGUI is guarded; every coroutine of
//                  the mod's runs inside the guard (Guard.Routine)
internal static partial class Program
{
    private static void ScanChecks(GameFiles files, string mod)
    {
        if (!File.Exists(mod))
        {
            return;   // (said under the compiled references)
        }
        ModImage image = ModImage.Read(mod, out string why);
        Section("the mod's own reader of its assembly (Core/ModImage), against .NET's");
        Check(image != null, "the built mod could not be read by Core/ModImage: " + why);
        if (image == null)
        {
            return;
        }
        var refs = new ModReferences(files, mod);
        using (var pe = new PEReader(File.OpenRead(mod)))
        {
            MetadataReader md = pe.GetMetadataReader();
            Reader(image, md, pe);
            Section("the mod's code against this game, as the running mod checks it as it starts (Core/ModScan)");
            ThisGame(image, refs);
            Section("the mod's code against a game that lacks what a player's Build 3675 lacked (their Player.log, 14 Oct 2026)");
            Older(image, refs, md);
            Section("the mod's code: isolated members, guarded entry points, guarded coroutines");
            Isolation(image, md, pe);
            EntryPoints(image, md, pe);
            Coroutines(image, md, pe);
            Section("the game's calls into the mod: what it asks through its interfaces and base classes, the delegates it is given, the dialogs and the options page (Core/GuardCalls)");
            var self = new GameAssembly("RealLifeOverhaul", mod);
            GameCalls(md, pe, files, self);
            Callbacks(md, pe, files);
            Handovers(md, pe, files);
        }
    }

    // ---------------------------------------------------------------- the reader

    private sealed class Tokens : ISignatureTypeProvider<int, object>
    {
        public readonly List<int> Seen = new List<int>();

        public int GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind)
        {
            Seen.Add(MetadataTokens.GetToken(handle));
            return 0;
        }

        public int GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind)
        {
            Seen.Add(MetadataTokens.GetToken(handle));
            return 0;
        }

        public int GetTypeFromSpecification(MetadataReader reader, object context, TypeSpecificationHandle handle, byte rawTypeKind)
        {
            Seen.Add(MetadataTokens.GetToken(handle));
            return 0;
        }

        public int GetPrimitiveType(PrimitiveTypeCode typeCode) => 0;
        public int GetSZArrayType(int elementType) => 0;
        public int GetArrayType(int elementType, ArrayShape shape) => 0;
        public int GetByReferenceType(int elementType) => 0;
        public int GetPointerType(int elementType) => 0;
        public int GetPinnedType(int elementType) => 0;
        public int GetModifiedType(int modifier, int unmodifiedType, bool isRequired) => 0;
        public int GetGenericInstantiation(int genericType, ImmutableArray<int> typeArguments) => 0;
        public int GetGenericTypeParameter(object genericContext, int index) => 0;
        public int GetGenericMethodParameter(object genericContext, int index) => 0;
        public int GetFunctionPointerType(MethodSignature<int> signature) => 0;
    }

    private static string Sorted(List<int> tokens)
    {
        var copy = new List<int>(tokens);
        copy.Sort();
        return string.Join(",", copy.Select(t => t.ToString("x8")));
    }

    private static void Reader(ModImage image, MetadataReader md, PEReader pe)
    {
        foreach (var (table, index) in new[] { (ModImage.TypeRef, TableIndex.TypeRef), (ModImage.TypeDef, TableIndex.TypeDef), (ModImage.Field, TableIndex.Field),
                                               (ModImage.MethodDef, TableIndex.MethodDef), (ModImage.MemberRef, TableIndex.MemberRef), (ModImage.TypeSpec, TableIndex.TypeSpec),
                                               (ModImage.MethodSpec, TableIndex.MethodSpec), (ModImage.AssemblyRef, TableIndex.AssemblyRef), (ModImage.NestedClass, TableIndex.NestedClass),
                                               (ModImage.InterfaceImpl, TableIndex.InterfaceImpl), (ModImage.StandAloneSig, TableIndex.StandAloneSig) })
        {
            Check(image.Rows(table) == md.GetTableRowCount(index), "reader: " + index + " has " + md.GetTableRowCount(index) + " rows, Core/ModImage reads " + image.Rows(table));
        }
        int bad = 0;
        void Same(bool ok, string what)
        {
            if (!ok && bad++ < 8)
            {
                Check(false, "reader: " + what);
            }
        }

        foreach (TypeReferenceHandle h in md.TypeReferences)
        {
            int row = MetadataTokens.GetRowNumber(h);
            GameFiles.Named(md, h, out string assembly, out string full);
            Same(image.TypeRefFullName(row) == full, "the type reference " + full + " is read as " + image.TypeRefFullName(row));
            Same(image.TypeRefAssembly(row) == assembly, "the type reference " + full + " is in " + assembly + ", read as " + image.TypeRefAssembly(row));
        }
        var tokens = new Tokens();
        var mine = new List<int>();
        foreach (MemberReferenceHandle h in md.MemberReferences)
        {
            int row = MetadataTokens.GetRowNumber(h);
            MemberReference r = md.GetMemberReference(h);
            string name = md.GetString(r.Name);
            Same(image.MemberRefName(row) == name, "the member reference " + name + " is read as " + image.MemberRefName(row));
            Same(image.MemberRefParent(row) == MetadataTokens.GetToken(r.Parent), "the member reference " + name + "'s parent");
            bool field = r.GetKind() == MemberReferenceKind.Field;
            Same(image.MemberRefIsField(row) == field, "the member reference " + name + " is a " + (field ? "field" : "method"));
            tokens.Seen.Clear();
            if (field)
            {
                r.DecodeFieldSignature(tokens, null);
            }
            else
            {
                r.DecodeMethodSignature(tokens, null);
            }
            mine.Clear();
            image.TypesIn(image.MemberRefSignature(row), BlobKind.Member, mine);
            Same(Sorted(mine) == Sorted(tokens.Seen), "the member reference " + name + "'s signature names " + Sorted(tokens.Seen) + ", read as " + Sorted(mine));
        }
        foreach (TypeSpecificationHandle h in Enumerable.Range(1, md.GetTableRowCount(TableIndex.TypeSpec)).Select(MetadataTokens.TypeSpecificationHandle))
        {
            int row = MetadataTokens.GetRowNumber(h);
            tokens.Seen.Clear();
            md.GetTypeSpecification(h).DecodeSignature(tokens, null);
            mine.Clear();
            image.TypesIn(image.TypeSpecSignature(row), BlobKind.Spec, mine);
            Same(Sorted(mine) == Sorted(tokens.Seen), "the type specification " + row + " names " + Sorted(tokens.Seen) + ", read as " + Sorted(mine));
            BlobReader blob = md.GetBlobReader(md.GetTypeSpecification(h).Signature);
            int generic = 0;
            if (blob.ReadByte() == 0x15)
            {
                blob.ReadByte();
                generic = MetadataTokens.GetToken(blob.ReadTypeHandle());
            }
            Same(image.TypeSpecGeneric(row) == generic, "the type specification " + row + "'s generic type");
        }
        foreach (MethodSpecificationHandle h in Enumerable.Range(1, md.GetTableRowCount(TableIndex.MethodSpec)).Select(MetadataTokens.MethodSpecificationHandle))
        {
            int row = MetadataTokens.GetRowNumber(h);
            MethodSpecification spec = md.GetMethodSpecification(h);
            Same(image.MethodSpecMethod(row) == MetadataTokens.GetToken(spec.Method), "the generic method instance " + row + "'s method");
            tokens.Seen.Clear();
            spec.DecodeSignature(tokens, null);
            mine.Clear();
            image.TypesIn(image.MethodSpecInstantiation(row), BlobKind.Instance, mine);
            Same(Sorted(mine) == Sorted(tokens.Seen), "the generic method instance " + row + "'s arguments");
        }
        foreach (int row in Enumerable.Range(1, md.GetTableRowCount(TableIndex.StandAloneSig)))
        {
            StandaloneSignature sig = md.GetStandaloneSignature(MetadataTokens.StandaloneSignatureHandle(row));
            tokens.Seen.Clear();
            if (sig.GetKind() == StandaloneSignatureKind.LocalVariables)
            {
                sig.DecodeLocalSignature(tokens, null);
            }
            else
            {
                sig.DecodeMethodSignature(tokens, null);
            }
            mine.Clear();
            image.TypesIn(image.StandAloneSigSignature(row), BlobKind.StandAlone, mine);
            Same(Sorted(mine) == Sorted(tokens.Seen), "the stand-alone signature " + row);
        }
        foreach (InterfaceImplementationHandle h in Enumerable.Range(1, md.GetTableRowCount(TableIndex.InterfaceImpl)).Select(MetadataTokens.InterfaceImplementationHandle))
        {
            int row = MetadataTokens.GetRowNumber(h);
            Same(image.InterfaceImplInterface(row) == MetadataTokens.GetToken(md.GetInterfaceImplementation(h).Interface), "the interface implementation " + row);
        }

        int[] outer = image.Enclosing();
        int[] methodOwner = image.MethodOwners();
        image.LoadCode();
        int bodies = 0;
        foreach (TypeDefinitionHandle th in md.TypeDefinitions)
        {
            int t = MetadataTokens.GetRowNumber(th);
            TypeDefinition type = md.GetTypeDefinition(th);
            Same(image.TypeDefName(t) == md.GetString(type.Name) && image.TypeDefNamespace(t) == md.GetString(type.Namespace), "the type " + md.GetString(type.Name));
            Same(outer[t] == (type.GetDeclaringType().IsNil ? 0 : MetadataTokens.GetRowNumber(type.GetDeclaringType())), "the type " + md.GetString(type.Name) + "'s enclosing type");
            Same(image.TypeDefExtends(t) == (type.BaseType.IsNil ? 0 : MetadataTokens.GetToken(type.BaseType)), "the type " + md.GetString(type.Name) + "'s base");
            image.FieldsOf(t, out int fFirst, out int fEnd);
            Same(fEnd - fFirst == type.GetFields().Count, "the type " + md.GetString(type.Name) + "'s fields");
            foreach (FieldDefinitionHandle fh in type.GetFields())
            {
                int f = MetadataTokens.GetRowNumber(fh);
                FieldDefinition field = md.GetFieldDefinition(fh);
                Same(f >= fFirst && f < fEnd && image.FieldName(f) == md.GetString(field.Name) && image.FieldIsStatic(f) == ((field.Attributes & FieldAttributes.Static) != 0), "the field " + md.GetString(field.Name));
                tokens.Seen.Clear();
                field.DecodeSignature(tokens, null);
                mine.Clear();
                image.TypesIn(image.FieldSignature(f), BlobKind.Field, mine);
                Same(Sorted(mine) == Sorted(tokens.Seen), "the field " + md.GetString(field.Name) + "'s type");
            }
            image.MethodsOf(t, out int mFirst, out int mEnd);
            Same(mEnd - mFirst == type.GetMethods().Count, "the type " + md.GetString(type.Name) + "'s methods");
            foreach (MethodDefinitionHandle mh in type.GetMethods())
            {
                int m = MetadataTokens.GetRowNumber(mh);
                MethodDefinition method = md.GetMethodDefinition(mh);
                string name = md.GetString(method.Name);
                Same(methodOwner[m] == t && image.MethodName(m) == name && image.MethodRva(m) == method.RelativeVirtualAddress
                     && image.MethodNoInlining(m) == ((method.ImplAttributes & MethodImplAttributes.NoInlining) != 0), "the method " + md.GetString(type.Name) + "." + name);
                tokens.Seen.Clear();
                method.DecodeSignature(tokens, null);
                mine.Clear();
                image.TypesIn(image.MethodSignature(m), BlobKind.Method, mine);
                Same(Sorted(mine) == Sorted(tokens.Seen), "the method " + name + "'s signature");
                if (method.RelativeVirtualAddress == 0)
                {
                    continue;
                }
                MethodBodyBlock body = pe.GetMethodBody(method.RelativeVirtualAddress);
                var expected = new List<int>(IlTokens.Read(body.GetILContent()));
                foreach (ExceptionRegion region in body.ExceptionRegions)
                {
                    if (region.Kind == ExceptionRegionKind.Catch && !region.CatchType.IsNil)
                    {
                        expected.Add(MetadataTokens.GetToken(region.CatchType));
                    }
                }
                mine.Clear();
                Same(image.BodyTokens(m, mine, out int locals), "the method " + name + "'s body is read");
                mine.RemoveAll(token => ModImage.TableOf(token) == ModImage.StandAloneSig);   // (calli's signature, which .NET's reader here leaves out)
                Same(string.Join(",", mine) == string.Join(",", expected), "the method " + md.GetString(type.Name) + "." + name + "'s code names " + expected.Count + " tokens, read as " + mine.Count);
                Same(locals == (body.LocalSignature.IsNil ? 0 : MetadataTokens.GetToken(body.LocalSignature)), "the method " + name + "'s locals");
                bodies++;
            }
        }
        image.DropCode();
        Check(bad == 0, "reader: " + bad + " differences from .NET's reader (the first eight above)");
        Check(bodies > 10000, "reader: the method bodies were read (" + bodies + ")");
        Console.WriteLine("   " + image.Rows(ModImage.TypeRef) + " type references, " + image.Rows(ModImage.MemberRef) + " member references, " + image.Rows(ModImage.TypeDef)
                          + " types and " + bodies + " method bodies read as .NET reads them.");
    }

    // ---------------------------------------------------------------- this game, and an older one

    private sealed class DiskAnswers : IGameAnswers
    {
        private readonly ModReferences _refs;

        public DiskAnswers(ModReferences refs)
        {
            _refs = refs;
        }

        public string Type(int token) => _refs.TypeProblem(token);

        public string Member(int token, bool field) => _refs.MemberProblem(token);
    }

    private static void ThisGame(ModImage image, ModReferences refs)
    {
        var clock = System.Diagnostics.Stopwatch.StartNew();
        List<MissingReference> missing = ModScan.Run(image, new DiskAnswers(refs), out int asked);
        foreach (MissingReference m in missing)
        {
            Check(false, "this game lacks the " + m.Kind + " " + m.Name + " (" + m.Assembly + ": " + m.Problem + "); "
                         + (m.Contained ? "the mod does without it in " + string.Join(", ", m.Isolated)
                             : "named by " + string.Join(", ", m.Users.Take(8)) + (m.Users.Count > 8 ? " and " + (m.Users.Count - 8) + " more" : string.Empty)
                               + "; the running mod switches off " + (m.Features.Count == 0 ? "no feature" : string.Join(", ", m.Features)) + (m.Machinery ? ", and stops the mod's own machinery where it meets it" : string.Empty)));
        }
        Check(asked > 3000, "the scan asked fewer of the game than the mod names (" + asked + "): it is broken");
        Console.WriteLine("   " + asked + " types and members of the game's and Unity's assemblies asked of the game on disk; " + missing.Count + " missing ("
                          + clock.ElapsedMilliseconds + " ms).");
    }

    // A game that lacks what a player's Build 3675 lacked, in Mono's own words from their Player.log.
    private sealed class OlderGame : IGameAnswers
    {
        private readonly IGameAnswers _game;
        private readonly ModImage _image;
        private readonly ModReferences _refs;

        public OlderGame(IGameAnswers game, ModImage image, ModReferences refs)
        {
            _game = game;
            _image = image;
            _refs = refs;
        }

        public string Type(int token)
        {
            if (_image.TypeRefFullName(ModImage.RowOf(token)) == "Helpers.FuneralHelper")
            {
                return "Could not resolve type with token " + token.ToString("x8") + " from typeref (expected class 'Helpers.FuneralHelper' in assembly 'BigAmbitions, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null')";
            }
            return _game.Type(token);
        }

        public string Member(int token, bool field)
        {
            int row = ModImage.RowOf(token);
            int parent = _image.MemberRefParent(row);
            string type = ModImage.TableOf(parent) == ModImage.TypeRef ? _image.TypeRefFullName(ModImage.RowOf(parent)) : null;
            string name = _image.MemberRefName(row);
            if (type == "Buildings.Building" && name == "locatedInsidePrivateFenceIndex")
            {
                return "Field not found: int Buildings.Building.locatedInsidePrivateFenceIndex Due to: Could not find field in class";
            }
            if (type == "UI.InGameUI.BuildingResume" && name == "get_TargetController")
            {
                return "Method not found: EntityController UI.InGameUI.BuildingResume.get_TargetController()";
            }
            if (type == "SubwayStation" && name == "GetNameKey")
            {
                return "Method not found: string .SubwayStation.GetNameKey()";
            }
            if (type == "HudConfirm" && name == "Show")
            {
                string[] parameters = _refs.Parameters(token);
                if (parameters != null && parameters.Length == 8 && parameters[0] == "System.String")
                {
                    return "Method not found: void .HudConfirm.Show(string,string,System.Action,System.Action,string,string,bool,bool)";
                }
            }
            return _game.Member(token, field);
        }
    }

    private static void Older(ModImage image, ModReferences refs, MetadataReader md)
    {
        List<MissingReference> missing = ModScan.Run(image, new OlderGame(new DiskAnswers(refs), image, refs), out _);
        var wanted = new[]
        {
            ("type", "Helpers.FuneralHelper", "GameMembers", new[] { "get_PlayerDead", "set_PlayerDead", "get_FuneralActive" }, new[] { "camera", "careers_study", "jobs" }),
            ("field", "Buildings.Building.locatedInsidePrivateFenceIndex", "GameMembers", new[] { "Fenced" }, new[] { "careers_study", "civic", "justice", "law", "travel", "utilities" }),
            ("method", "UI.InGameUI.BuildingResume.get_TargetController", "GameMembers", new[] { "TargetOf" }, new[] { "careers_study" }),
            ("method", "SubwayStation.GetNameKey", "GameMembers", new[] { "NameKey" }, new[] { "transport" }),
            ("method", "HudConfirm.Show", "GameConfirm", new[] { "Show", "TryShow" }, new[] { "economy", "markets", "planner" }),
        };
        Check(missing.Count == wanted.Length, "Build 3675: " + missing.Count + " pieces missing, wanted the " + wanted.Length + " its Player.log named: " + string.Join("; ", missing));
        foreach (var (kind, name, holder, accessors, touched) in wanted)
        {
            MissingReference m = missing.FirstOrDefault(x => x.Kind == kind && x.Name == name);
            Check(m != null, "Build 3675: the " + kind + " " + name + " is found missing");
            if (m == null)
            {
                continue;
            }
            Check(m.Contained && m.Isolated.All(x => x.StartsWith(holder + ".", StringComparison.Ordinal)),
                "Build 3675: " + name + " is named only where the mod does without it (Core/" + holder + "): users " + string.Join(", ", m.Users) + "; isolated " + string.Join(", ", m.Isolated));
            List<string> features = ModScan.FeaturesUsing(image, "RealLifeOverhaul", holder, accessors, out List<string> callers, out bool machinery);
            foreach (string feature in touched)
            {
                Check(features.Contains(feature), "Build 3675: " + name + " touches the feature " + feature + " through " + holder + " (it touches " + string.Join(", ", features) + ")");
            }
            Console.WriteLine("   " + name + " (" + m.Problem + "): read only in " + string.Join(", ", m.Isolated) + "; it touches, through " + holder + "."
                              + string.Join(" and ", accessors) + ", " + callers.Count + " classes of " + features.Count + " features: " + string.Join(", ", features)
                              + (machinery ? ", and the mod's own machinery" : string.Empty) + ".");
        }

        // The contract: the five are lines of it, each said and done without as the running mod starts on such a game.
        var lacked = new HashSet<GameMember>(GameContract.LackedBy3675);
        foreach (var (kind, name, holder, accessors, touched) in wanted)
        {
            string type = kind == "type" ? name : name.Substring(0, name.LastIndexOf('.'));
            string member = kind == "type" ? null : name.Substring(name.LastIndexOf('.') + 1).Replace("get_", string.Empty);
            Check(GameContract.LackedBy3675.Any(g => g.TypeName == type && (member == null || g.Name == member)), "Build 3675: " + name + " is a line of the game contract (GameContract.LackedBy3675)");
        }
        ContractBook.Mark(g => lacked.Contains(g) ? "not in Build 3675" : null);
        List<GamePart> off = ContractBook.OffParts();
        Check(off.Count == 1 && off[0] == GameContract.CardTarget, "Build 3675: only the mod's lines on the building card are off (the rest is done without): " + string.Join(", ", off.Select(p => p.Id)));
        Check(GameContract.LackedBy3675.All(g => g.Missing), "Build 3675: each of the five is known to be missing from the start, so the methods that read it are never called");
        ContractBook.Mark(g => null);
    }

    // ---------------------------------------------------------------- the code

    // An instruction of IL with its offset (ECMA-335 III): the same table as Core/ModImage.Decode, read apart here.
    private static IEnumerable<(int Offset, int Op, int Operand)> Steps(byte[] il)
    {
        var ops = new List<int>();
        var operands = new List<int>();
        int i = 0;
        while (i < il.Length)
        {
            int at = i;
            int op = il[i++];
            int operand = 0;
            if (op == 0xFE)
            {
                int two = il[i++];
                op = 0xFE00 | two;
                if (two == 0x06 || two == 0x07 || two == 0x15 || two == 0x16 || two == 0x1C)
                {
                    operand = BitConverter.ToInt32(il, i);
                    i += 4;
                }
                else if (two >= 0x09 && two <= 0x0E)
                {
                    i += 2;
                }
                else if (two == 0x12 || two == 0x19)
                {
                    i += 1;
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
            else if (op == 0x45)
            {
                int n = BitConverter.ToInt32(il, i);
                i += 4 + 4 * n;
            }
            else if (op == 0x72 || op == 0x29 || op == 0x27 || op == 0x28 || op == 0x6F || op == 0x73 || op == 0x70 || op == 0x71 || op == 0x74 || op == 0x75 || op == 0x79
                     || (op >= 0x7B && op <= 0x81) || op == 0x8C || op == 0x8D || op == 0x8F || op == 0xA3 || op == 0xA4 || op == 0xA5 || op == 0xC2 || op == 0xC6 || op == 0xD0)
            {
                operand = BitConverter.ToInt32(il, i);
                i += 4;
            }
            yield return (at, op, operand);
        }
    }

    private static int MethodToken(MetadataReader md, string type, string method)
    {
        foreach (TypeDefinitionHandle th in md.TypeDefinitions)
        {
            TypeDefinition t = md.GetTypeDefinition(th);
            if (md.GetString(t.Name) != type || md.GetString(t.Namespace) != "RealLifeOverhaul")
            {
                continue;
            }
            foreach (MethodDefinitionHandle mh in t.GetMethods())
            {
                if (md.GetString(md.GetMethodDefinition(mh).Name) == method)
                {
                    return MetadataTokens.GetToken(mh);
                }
            }
        }
        return 0;
    }

    private static string FullMethodName(MetadataReader md, MethodDefinitionHandle mh)
    {
        MethodDefinition m = md.GetMethodDefinition(mh);
        return GameAssembly.FullName(md, m.GetDeclaringType()) + "." + md.GetString(m.Name);
    }

    // A game member named in IL: a reference into an assembly of the game's or Unity's.
    private static bool Game(ModImage image, int token)
    {
        int row = ModImage.RowOf(token);
        switch (ModImage.TableOf(token))
        {
            case ModImage.TypeRef:
                return ModScan.IsGameAssembly(image.TypeRefAssembly(row));
            case ModImage.MemberRef:
                int parent = image.MemberRefParent(row);
                if (ModImage.TableOf(parent) == ModImage.TypeRef)
                {
                    return ModScan.IsGameAssembly(image.TypeRefAssembly(ModImage.RowOf(parent)));
                }
                int generic = ModImage.TableOf(parent) == ModImage.TypeSpec ? image.TypeSpecGeneric(ModImage.RowOf(parent)) : 0;
                return ModImage.TableOf(generic) == ModImage.TypeRef && ModScan.IsGameAssembly(image.TypeRefAssembly(ModImage.RowOf(generic)));
            default:
                return false;
        }
    }

    // Every method of the mod's own that names a game member and is never inlined (the way Core/GameMembers reads what an
    // older game lacks) is called only inside a try with a catch: its failure on such a game is always caught.
    private static void Isolation(ModImage image, MetadataReader md, PEReader pe)
    {
        var isolated = new HashSet<int>();
        foreach (MethodDefinitionHandle mh in md.MethodDefinitions)
        {
            MethodDefinition m = md.GetMethodDefinition(mh);
            if ((m.ImplAttributes & MethodImplAttributes.NoInlining) == 0 || m.RelativeVirtualAddress == 0)
            {
                continue;
            }
            byte[] il = pe.GetMethodBody(m.RelativeVirtualAddress).GetILBytes();
            if (Steps(il).Any(s => s.Operand != 0 && s.Op != 0x72 && Game(image, s.Operand)))
            {
                isolated.Add(MetadataTokens.GetToken(mh));
            }
        }
        int calls = 0, bare = 0;
        foreach (MethodDefinitionHandle mh in md.MethodDefinitions)
        {
            MethodDefinition m = md.GetMethodDefinition(mh);
            if (m.RelativeVirtualAddress == 0)
            {
                continue;
            }
            MethodBodyBlock body = pe.GetMethodBody(m.RelativeVirtualAddress);
            byte[] il = body.GetILBytes();
            foreach (var step in Steps(il))
            {
                if ((step.Op != 0x28 && step.Op != 0x6F) || !isolated.Contains(step.Operand))
                {
                    continue;
                }
                calls++;
                bool caught = body.ExceptionRegions.Any(r => (r.Kind == ExceptionRegionKind.Catch || r.Kind == ExceptionRegionKind.Filter)
                                                              && step.Offset >= r.TryOffset && step.Offset < r.TryOffset + r.TryLength);
                if (!caught)
                {
                    bare++;
                    Check(false, "isolated: " + FullMethodName(md, mh) + " calls " + FullMethodName(md, (MethodDefinitionHandle)MetadataTokens.EntityHandle(step.Operand))
                                 + ", which names a game member and is never inlined, outside a try: on a game without that member the caller would fail too");
                }
            }
        }
        Check(isolated.Count >= 12, "isolated: fewer methods of the mod's own that name what an older game lacks than Core/GameMembers, Core/GameConfirm and the rest have (" + isolated.Count + ")");
        Console.WriteLine("   " + isolated.Count + " methods of the mod's own, never inlined, name a game member; their " + calls + " calls are inside a try with a catch"
                          + (bare > 0 ? ", but " + bare + " are not" : string.Empty) + ".");
    }

    // The mod's MonoBehaviours, and the entry points the engine calls each frame: each one guarded.
    private static readonly HashSet<string> GuardedByHand = new HashSet<string>(StringComparer.Ordinal)
    {
        "RealLifeOverhaul.GuardNotices.Update",   // the guard's own notices: its catch switches it off
        "RealLifeOverhaul.LifeEvents.Update",     // the save hook must run whatever is off: each part that can fail is in a try of its own (Life/LifeEvents.cs)
    };

    private static bool Behaviour(MetadataReader md, TypeDefinitionHandle th, Dictionary<TypeDefinitionHandle, bool> known, int depth = 0)
    {
        if (known.TryGetValue(th, out bool yes))
        {
            return yes;
        }
        EntityHandle b = md.GetTypeDefinition(th).BaseType;
        bool result = false;
        if (!b.IsNil && depth < 16)
        {
            if (b.Kind == HandleKind.TypeReference)
            {
                GameFiles.Named(md, (TypeReferenceHandle)b, out string assembly, out string full);
                result = full == "UnityEngine.MonoBehaviour";
            }
            else if (b.Kind == HandleKind.TypeDefinition)
            {
                result = Behaviour(md, (TypeDefinitionHandle)b, known, depth + 1);
            }
        }
        known[th] = result;
        return result;
    }

    private static void EntryPoints(ModImage image, MetadataReader md, PEReader pe)
    {
        int fail = MethodToken(md, "Guard", "Fail");
        Check(fail != 0, "entry points: Guard.Fail is not in the built mod");
        var known = new Dictionary<TypeDefinitionHandle, bool>();
        int entries = 0, unguarded = 0;
        foreach (TypeDefinitionHandle th in md.TypeDefinitions)
        {
            if (!Behaviour(md, th, known))
            {
                continue;
            }
            foreach (MethodDefinitionHandle mh in md.GetTypeDefinition(th).GetMethods())
            {
                MethodDefinition m = md.GetMethodDefinition(mh);
                string name = md.GetString(m.Name);
                if ((name != "Update" && name != "LateUpdate" && name != "FixedUpdate" && name != "OnGUI") || m.RelativeVirtualAddress == 0
                    || m.DecodeSignature(new Tokens(), null).ParameterTypes.Length != 0)
                {
                    continue;
                }
                entries++;
                string full = FullMethodName(md, mh);
                if (GuardedByHand.Contains(full))
                {
                    continue;
                }
                MethodBodyBlock body = pe.GetMethodBody(m.RelativeVirtualAddress);
                bool caught = body.ExceptionRegions.Any(r => r.Kind == ExceptionRegionKind.Catch);
                bool failed = Steps(body.GetILBytes()).Any(s => s.Op == 0x28 && s.Operand == fail);
                if (!caught || !failed)
                {
                    unguarded++;
                    Check(false, "entry points: " + full + " is not guarded: its body is to be a try round the method it was, its catch Guard.Fail (Core/Guard: FrameGuard)");
                }
            }
        }
        Check(entries > 100, "entry points: fewer Update, LateUpdate, FixedUpdate and OnGUI methods than the mod has (" + entries + ")");
        Console.WriteLine("   " + entries + " Update, LateUpdate, FixedUpdate and OnGUI methods of the mod's components; " + (entries - unguarded - GuardedByHand.Count)
                          + " behind the guard, " + GuardedByHand.Count + " guarded by hand, " + unguarded + " not.");
    }

    // Every coroutine the mod starts runs inside the guard (Guard.Routine), but the game's own (a fade, an emoji, a
    // turn of a character), in which no code of the mod's runs, and the one started by hand below.
    private static readonly HashSet<string> StartedByHand = new HashSet<string>(StringComparer.Ordinal)
    {
        "RealLifeOverhaul.LifeEvents.OnSaveGame",   // the mod's moods put back after a save: every feature's, whatever is off (Life/LifeEvents.cs)
    };

    private static void Coroutines(ModImage image, MetadataReader md, PEReader pe)
    {
        int routine = MethodToken(md, "Guard", "Routine");
        Check(routine != 0, "coroutines: Guard.Routine is not in the built mod");
        var start = new HashSet<int>();
        foreach (MemberReferenceHandle h in md.MemberReferences)
        {
            MemberReference r = md.GetMemberReference(h);
            if (md.GetString(r.Name) != "StartCoroutine" || r.Parent.Kind != HandleKind.TypeReference)
            {
                continue;
            }
            GameFiles.Named(md, (TypeReferenceHandle)r.Parent, out string assembly, out string full);
            var parameters = new Tokens();
            MethodSignature<int> sig = r.DecodeMethodSignature(parameters, null);
            if (full == "UnityEngine.MonoBehaviour" && sig.ParameterTypes.Length == 1 && parameters.Seen.Count == 2)
            {
                GameFiles.Named(md, (TypeReferenceHandle)MetadataTokens.EntityHandle(parameters.Seen[1]), out string a2, out string first);
                if (first == "System.Collections.IEnumerator")
                {
                    start.Add(MetadataTokens.GetToken(h));
                }
            }
        }
        int calls = 0, bare = 0;
        foreach (MethodDefinitionHandle mh in md.MethodDefinitions)
        {
            MethodDefinition m = md.GetMethodDefinition(mh);
            if (m.RelativeVirtualAddress == 0)
            {
                continue;
            }
            var steps = Steps(pe.GetMethodBody(m.RelativeVirtualAddress).GetILBytes()).ToList();
            for (int i = 1; i < steps.Count; i++)
            {
                if ((steps[i].Op != 0x28 && steps[i].Op != 0x6F) || !start.Contains(steps[i].Operand))
                {
                    continue;
                }
                calls++;
                var before = steps[i - 1];
                bool guarded = before.Op == 0x28 && before.Operand == routine;
                bool games = (before.Op == 0x28 || before.Op == 0x6F) && Game(image, before.Operand);
                if (!guarded && !games && !StartedByHand.Contains(FullMethodName(md, mh)))
                {
                    bare++;
                    Check(false, "coroutines: " + FullMethodName(md, mh) + " starts a coroutine of the mod's outside the guard: StartCoroutine(Guard.Routine(...)) (Core/Guard)");
                }
            }
        }
        Check(calls > 50, "coroutines: fewer coroutines started than the mod starts (" + calls + ")");
        Console.WriteLine("   " + calls + " coroutines started; " + (calls - bare) + " inside the guard or the game's own, " + bare + " not.");
    }
}
