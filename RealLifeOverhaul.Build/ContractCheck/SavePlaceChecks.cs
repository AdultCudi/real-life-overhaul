using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Text.RegularExpressions;
using RealLifeOverhaul;
using RealLifeOverhaul.DistrictSave;

// The district save's guard against what a game update or a DLC adds to the save (Scripts/DistrictSave,
// docs/features/district-save.md). Against the game on disk: every field the game's save writes, walked from GameInstance
// through every type it can reach (a list, a dictionary, a tuple, every subclass of a field's type in the game's
// assemblies), by the game's own rule of what is written (public fields and those marked to be kept; obsolete ones are
// left out of a binary save and written by a JSON one, and listed as such); each field that can hold an address, a
// street and its number, a district, or an id of someone or a plan that leaves with a business, is a path. The paths
// must be exactly SavePlaces.txt, where each says what the district save does with it; a new one fails here by name.
// Then that the game never reads or writes GameInstance.modData (the mod's section), but to make it. Without the game:
// every path the list says is handled is handled in the district save's code ("save path:" comments), and every save goes
// through it (SaveHook's last step, the save tools leave its key, nothing else writes a save).
internal static partial class Program
{
    /// <summary>Fields that hold a place by what they mean, not by their name or type: the census cannot tell them otherwise.</summary>
    private static readonly (string Type, string Field, string Holds)[] ByMeaning =
    {
        ("BigAmbitions.Neighborhoods.NeighbourhoodStats", "name", "district"),
        ("GameInstance", "LastPlayerPosition", "position"),
        ("VehicleInstance", "position", "position"),
    };

    private static readonly Regex SavePathComment = new Regex(@"//\s*save path:\s*(GameInstance\S*)", RegexOptions.Compiled);

    private static void SavePlaces(GameFiles files, string scripts)
    {
        Section("the save's fields that can name a place of the mod's (the district save), against " + files.Folder);
        List<string> found = SaveCensus(files, out int types);
        string listPath = Path.Combine(AppContext.BaseDirectory, "SavePlaces.txt");
        var listed = new Dictionary<string, (string Holds, string Handled)>(StringComparer.Ordinal);
        foreach (string raw in File.ReadAllLines(listPath))
        {
            string line = raw.Trim();
            if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal))
            {
                continue;
            }
            string[] f = line.Split('|');
            Check(f.Length == 3, "SavePlaces.txt: a line is not 'path | holds | handled': " + line);
            if (f.Length == 3)
            {
                Check(listed.TryAdd(f[0].Trim(), (f[1].Trim(), f[2].Trim())), "SavePlaces.txt: a path is listed twice: " + f[0].Trim());
            }
        }
        var census = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string line in found)
        {
            int at = line.IndexOf(" : ", StringComparison.Ordinal);
            census[line.Substring(0, at)] = line.Substring(at + 3);
        }
        foreach (KeyValuePair<string, string> path in census)
        {
            if (!listed.TryGetValue(path.Key, out (string Holds, string Handled) entry))
            {
                Check(false, "the game's save has a field the district save does not know: " + path.Key + " (" + path.Value + "). Read what it holds, handle it in "
                             + "Scripts/DistrictSave (a kind's code), and list it in ContractCheck/SavePlaces.txt (docs/features/district-save.md, 'After a game update')");
                continue;
            }
            Check(entry.Holds == path.Value, "SavePlaces.txt: " + path.Key + " holds " + path.Value + " in the game now, the list says " + entry.Holds);
        }
        foreach (KeyValuePair<string, (string Holds, string Handled)> path in listed)
        {
            Check(census.ContainsKey(path.Key), "SavePlaces.txt lists " + path.Key + ", which the game's save no longer has: take it out of the list and of the district save");
            string handled = path.Value.Handled;
            if (handled == "obsolete" || handled.StartsWith("display:", StringComparison.Ordinal))
            {
                Check(handled != "obsolete" || path.Value.Holds.EndsWith("(obsolete)", StringComparison.Ordinal),
                    "SavePlaces.txt: " + path.Key + " is said to be obsolete, but the game writes it to every save");
                continue;
            }
            foreach (string code in handled.Split('+'))
            {
                Check(DistrictSaveRules.KindOf(code.Trim(), out SaveKind _), "SavePlaces.txt: " + path.Key + " names " + code.Trim() + ", which is no kind of the district save's");
            }
        }
        Check(census.Count > 100 && types > 120, "the census of the save read fewer fields or types than the game has (" + census.Count + " paths, " + types + " types): the reading is broken");
        Console.WriteLine("   " + census.Count + " paths that can name a place, through " + types + " of the game's types; " + listed.Count + " listed in SavePlaces.txt.");

        ModDataUntouched(files);
        SavePathsHandled(scripts, listed);
    }

    // ---------------------------------------------------------------- the census of the save's fields

    private static List<string> SaveCensus(GameFiles files, out int typeCount)
    {
        var census = new SaveCensusWalk(files);
        census.Run();
        typeCount = census.TypesSeen.Count;
        return census.Found.Distinct().ToList();
    }

    /// <summary>A type of a field's signature, as the census follows it.</summary>
    private sealed class SaveType
    {
        public enum K { Prim, Str, Obj, Named, Array, GParam, Other }
        public K Kind;
        public string Prim;
        public string Asm;
        public string Full;
        public SaveType[] Args = Array.Empty<SaveType>();
        public bool ValueType;

        public override string ToString()
        {
            switch (Kind)
            {
                case K.Prim: return Prim;
                case K.Str: return "string";
                case K.Obj: return "object";
                case K.Array: return Args[0] + "[]";
                case K.GParam: return "!" + Prim;
                case K.Named: return Full + (Args.Length > 0 ? "<" + string.Join(",", Args.Select(a => a.ToString())) + ">" : string.Empty);
                default: return "?";
            }
        }
    }

    private readonly struct SaveCtx
    {
        public readonly SaveType[] Args;

        public SaveCtx(SaveType[] args)
        {
            Args = args;
        }
    }

    private sealed class SaveShape : ISignatureTypeProvider<SaveType, SaveCtx>
    {
        private readonly GameAssembly _within;

        public SaveShape(GameAssembly within)
        {
            _within = within;
        }

        public SaveType GetPrimitiveType(PrimitiveTypeCode c)
        {
            if (c == PrimitiveTypeCode.String)
            {
                return new SaveType { Kind = SaveType.K.Str };
            }
            if (c == PrimitiveTypeCode.Object)
            {
                return new SaveType { Kind = SaveType.K.Obj };
            }
            return new SaveType { Kind = SaveType.K.Prim, Prim = c.ToString() };
        }

        public SaveType GetTypeFromDefinition(MetadataReader md, TypeDefinitionHandle h, byte k) =>
            new SaveType { Kind = SaveType.K.Named, Asm = _within.Name, Full = GameAssembly.FullName(md, h), ValueType = k == 0x11 };

        public SaveType GetTypeFromReference(MetadataReader md, TypeReferenceHandle h, byte k)
        {
            GameFiles.Named(md, h, out string asm, out string full);
            return new SaveType { Kind = SaveType.K.Named, Asm = asm, Full = full, ValueType = k == 0x11 };
        }

        public SaveType GetTypeFromSpecification(MetadataReader md, SaveCtx ctx, TypeSpecificationHandle h, byte k) => md.GetTypeSpecification(h).DecodeSignature(this, ctx);
        public SaveType GetSZArrayType(SaveType e) => new SaveType { Kind = SaveType.K.Array, Args = new[] { e } };
        public SaveType GetArrayType(SaveType e, ArrayShape s) => new SaveType { Kind = SaveType.K.Array, Args = new[] { e } };
        public SaveType GetByReferenceType(SaveType e) => e;
        public SaveType GetPointerType(SaveType e) => new SaveType { Kind = SaveType.K.Other };
        public SaveType GetPinnedType(SaveType e) => e;
        public SaveType GetModifiedType(SaveType m, SaveType u, bool r) => u;
        public SaveType GetFunctionPointerType(MethodSignature<SaveType> s) => new SaveType { Kind = SaveType.K.Other };

        public SaveType GetGenericInstantiation(SaveType g, ImmutableArray<SaveType> args) =>
            new SaveType { Kind = SaveType.K.Named, Asm = g.Asm, Full = g.Full, Args = args.ToArray(), ValueType = g.ValueType };

        public SaveType GetGenericTypeParameter(SaveCtx ctx, int index) =>
            ctx.Args != null && index < ctx.Args.Length ? ctx.Args[index] : new SaveType { Kind = SaveType.K.GParam, Prim = index.ToString() };

        public SaveType GetGenericMethodParameter(SaveCtx ctx, int index) => new SaveType { Kind = SaveType.K.GParam, Prim = "m" + index };
    }

    /// <summary>The walk from GameInstance over every field the game's save writes, as Scripts/DistrictSave/DistrictSaveScan walks the save itself.</summary>
    private sealed class SaveCensusWalk
    {
        private static readonly HashSet<string> ListLike = new HashSet<string>(StringComparer.Ordinal)
        {
            "System.Collections.Generic.List`1", "System.Collections.Generic.Queue`1", "System.Collections.Generic.Stack`1",
            "System.Collections.Generic.HashSet`1", "System.Collections.Generic.LinkedList`1", "System.Collections.Generic.SortedSet`1",
            "System.Collections.Generic.IList`1", "System.Collections.Generic.ICollection`1", "System.Collections.Generic.IEnumerable`1",
            "System.Collections.Generic.IReadOnlyList`1", "System.Collections.ObjectModel.Collection`1", "System.Collections.ObjectModel.ReadOnlyCollection`1",
        };

        private static readonly HashSet<string> MapLike = new HashSet<string>(StringComparer.Ordinal)
        {
            "System.Collections.Generic.Dictionary`2", "System.Collections.Generic.SortedDictionary`2", "System.Collections.Generic.IDictionary`2",
            "System.Collections.Generic.SortedList`2", "System.Collections.Generic.KeyValuePair`2", "System.Collections.Generic.IReadOnlyDictionary`2",
        };

        private readonly GameFiles _files;
        private readonly Dictionary<string, List<FoundType>> _derived = new Dictionary<string, List<FoundType>>(StringComparer.Ordinal);
        public readonly List<string> Found = new List<string>();
        public readonly HashSet<string> TypesSeen = new HashSet<string>(StringComparer.Ordinal);

        public SaveCensusWalk(GameFiles files)
        {
            _files = files;
        }

        public void Run()
        {
            foreach (string path in Directory.GetFiles(_files.Folder, "*.dll"))
            {
                string name = Path.GetFileNameWithoutExtension(path);
                GameAssembly a = Game(name) ? _files.Get(name) : null;
                if (a == null)
                {
                    continue;
                }
                foreach (KeyValuePair<string, TypeDefinitionHandle> kv in a.Types)
                {
                    TypeDefinition t = a.Md.GetTypeDefinition(kv.Value);
                    string baseFull = t.BaseType.IsNil ? null : BaseFull(a, t.BaseType);
                    if (baseFull == null)
                    {
                        continue;
                    }
                    if (!_derived.TryGetValue(baseFull, out List<FoundType> list))
                    {
                        _derived[baseFull] = list = new List<FoundType>();
                    }
                    list.Add(new FoundType { Assembly = a, Handle = kv.Value });
                }
            }
            Named(new SaveType { Kind = SaveType.K.Named, Asm = "BigAmbitions", Full = "GameInstance" }, "GameInstance", new HashSet<string>(StringComparer.Ordinal), false);
        }

        // An assembly of the game's own (not the framework's, the engine's or a library's): its types are walked into.
        private static bool Game(string name)
        {
            if (name == null || name.StartsWith("System", StringComparison.Ordinal) || name.StartsWith("Unity", StringComparison.Ordinal)
                || name.StartsWith("Mono.", StringComparison.Ordinal) || name == "mscorlib" || name == "netstandard")
            {
                return false;
            }
            return !name.StartsWith("Newtonsoft", StringComparison.Ordinal) && !name.StartsWith("OdinSerializer", StringComparison.Ordinal)
                   && !name.StartsWith("Google.", StringComparison.Ordinal) && !name.StartsWith("Facepunch", StringComparison.Ordinal)
                   && !name.StartsWith("DOTween", StringComparison.Ordinal) && !name.StartsWith("Cinemachine", StringComparison.Ordinal);
        }

        private static string BaseFull(GameAssembly a, EntityHandle h)
        {
            switch (h.Kind)
            {
                case HandleKind.TypeDefinition:
                    return GameAssembly.FullName(a.Md, (TypeDefinitionHandle)h);
                case HandleKind.TypeReference:
                    GameFiles.Named(a.Md, (TypeReferenceHandle)h, out string _, out string full);
                    return full;
                case HandleKind.TypeSpecification:
                    return a.Md.GetTypeSpecification((TypeSpecificationHandle)h).DecodeSignature(new SaveShape(a), new SaveCtx(null)).Full;
                default:
                    return null;
            }
        }

        private void Visit(SaveType t, string path, HashSet<string> onPath, string owner, string field, bool obsolete)
        {
            string tag = obsolete ? " (obsolete)" : string.Empty;
            if (owner != null && field != null)
            {
                foreach ((string Type, string Field, string Holds) m in ByMeaning)
                {
                    if (m.Type == owner && m.Field == field)
                    {
                        Found.Add(path + " : " + m.Holds + tag);
                        return;
                    }
                }
            }
            switch (t.Kind)
            {
                case SaveType.K.Str:
                    string kind = TextKind(field);
                    if (kind != null)
                    {
                        Found.Add(path + " : " + kind + tag);
                    }
                    return;
                case SaveType.K.Prim:
                    if (field != null && field.EndsWith("streetnumber", StringComparison.OrdinalIgnoreCase))
                    {
                        Found.Add(path + " : number" + tag);
                    }
                    return;
                case SaveType.K.Obj:
                    Found.Add(path + " : untyped" + tag);
                    return;
                case SaveType.K.GParam:
                    Found.Add(path + " : open generic" + tag);
                    return;
                case SaveType.K.Array:
                    Visit(t.Args[0], path + "[]", onPath, null, field, obsolete);
                    return;
            }
            if (t.Kind != SaveType.K.Named)
            {
                return;
            }
            if (ListLike.Contains(t.Full))
            {
                Visit(t.Args[0], path + "[]", onPath, null, field, obsolete);
                return;
            }
            if (MapLike.Contains(t.Full))
            {
                Visit(t.Args[0], path + "{key}", onPath, null, field, obsolete);
                Visit(t.Args[1], path + "{}", onPath, null, field, obsolete);
                return;
            }
            if (t.Full == "System.Nullable`1")
            {
                Visit(t.Args[0], path, onPath, owner, field, obsolete);
                return;
            }
            if (t.Full.StartsWith("System.Tuple`", StringComparison.Ordinal) || t.Full.StartsWith("System.ValueTuple`", StringComparison.Ordinal))
            {
                for (int i = 0; i < t.Args.Length; i++)
                {
                    Visit(t.Args[i], path + ".Item" + (i + 1), onPath, null, "Item" + (i + 1), obsolete);
                }
                return;
            }
            if (t.Full == "Address")
            {
                Found.Add(path + " : address" + tag);
                return;
            }
            if (t.Full == "Buildings.Building")
            {
                Found.Add(path + " : building" + tag);
                return;
            }
            if (t.Asm == null || !Game(t.Asm))
            {
                if (t.Full.StartsWith("System.Collections", StringComparison.Ordinal) || t.Full == "System.Array")
                {
                    Found.Add(path + " : untyped collection " + t + tag);
                }
                return;
            }
            Named(t, path, onPath, obsolete);
        }

        // A text field by its name: a street's id, a district's, an address written out, or an id of someone or a plan.
        private static string TextKind(string field)
        {
            if (field == null)
            {
                return null;
            }
            string n = field.ToLowerInvariant();
            if (n.EndsWith("streetname", StringComparison.Ordinal) || n == "street" || n.EndsWith("street", StringComparison.Ordinal))
            {
                return "street";
            }
            if (n.Contains("neighbourhood") || n.Contains("neighborhood") || n.Contains("district"))
            {
                return "district";
            }
            if (n.Contains("address"))
            {
                return "address text";
            }
            bool idName = n.EndsWith("id", StringComparison.Ordinal) || n.EndsWith("ids", StringComparison.Ordinal) || n.EndsWith("employees", StringComparison.Ordinal)
                          || n.EndsWith("plans", StringComparison.Ordinal);
            bool leaving = n.Contains("employee") || n.Contains("candidate") || n.Contains("plan") || n.Contains("negotiation") || n.Contains("offer")
                           || n.Contains("headhunter") || n.Contains("manager") || n.Contains("driver");
            return idName && leaving ? "id" : null;
        }

        private void Named(SaveType t, string path, HashSet<string> onPath, bool obsolete)
        {
            FoundType ft = _files.Find(t.Asm, t.Full);
            if (!ft.Exists)
            {
                Found.Add(path + " : unresolved " + t);
                return;
            }
            TypesSeen.Add(t.Full);
            if (IsEnumOrDelegate(ft))
            {
                return;
            }
            string key = t.ToString();
            if (!onPath.Add(key))
            {
                return;   // the type within itself: walked once, above
            }
            try
            {
                foreach ((FieldDefinition f, SaveType type, bool obs, GameAssembly a) in Fields(ft, t.Args))
                {
                    string name = a.Md.GetString(f.Name);
                    Visit(type, path + "." + name, onPath, t.Full, name, obsolete || obs);
                }
                if (!t.ValueType && (ft.Def.Attributes & TypeAttributes.Sealed) == 0 && _derived.TryGetValue(t.Full, out List<FoundType> subs))
                {
                    foreach (FoundType sub in subs)
                    {
                        Derived(sub, path + "<" + GameAssembly.FullName(sub.Assembly.Md, sub.Handle) + ">", onPath, obsolete);
                    }
                }
            }
            finally
            {
                onPath.Remove(key);
            }
        }

        // A subclass the field may hold: its own fields, and its subclasses'.
        private void Derived(FoundType sub, string path, HashSet<string> onPath, bool obsolete)
        {
            string full = GameAssembly.FullName(sub.Assembly.Md, sub.Handle);
            string key = "<" + full + ">";
            if (!onPath.Add(key) || IsEnumOrDelegate(sub))
            {
                onPath.Remove(key);
                return;
            }
            try
            {
                TypesSeen.Add(full);
                foreach (FieldDefinitionHandle fh in sub.Def.GetFields())
                {
                    FieldDefinition f = sub.Assembly.Md.GetFieldDefinition(fh);
                    if (!Written(sub.Assembly.Md, f, out bool obs))
                    {
                        continue;
                    }
                    SaveType type = f.DecodeSignature(new SaveShape(sub.Assembly), new SaveCtx(null));
                    string name = sub.Assembly.Md.GetString(f.Name);
                    Visit(type, path + "." + name, onPath, full, name, obsolete || obs);
                }
                if ((sub.Def.Attributes & TypeAttributes.Sealed) == 0 && _derived.TryGetValue(full, out List<FoundType> subs))
                {
                    foreach (FoundType s2 in subs)
                    {
                        Derived(s2, path + "<" + GameAssembly.FullName(s2.Assembly.Md, s2.Handle) + ">", onPath, obsolete);
                    }
                }
            }
            finally
            {
                onPath.Remove(key);
            }
        }

        // The written fields of a type and its bases, with the generic arguments put in.
        private IEnumerable<(FieldDefinition, SaveType, bool, GameAssembly)> Fields(FoundType ft, SaveType[] args)
        {
            FoundType cur = ft;
            SaveType[] curArgs = args;
            for (int depth = 0; depth < 16 && cur.Exists; depth++)
            {
                MetadataReader md = cur.Assembly.Md;
                TypeDefinition def = cur.Def;
                foreach (FieldDefinitionHandle fh in def.GetFields())
                {
                    FieldDefinition f = md.GetFieldDefinition(fh);
                    if (Written(md, f, out bool obs))
                    {
                        yield return (f, f.DecodeSignature(new SaveShape(cur.Assembly), new SaveCtx(curArgs)), obs, cur.Assembly);
                    }
                }
                if (def.BaseType.IsNil)
                {
                    yield break;
                }
                SaveType bt;
                EntityHandle b = def.BaseType;
                switch (b.Kind)
                {
                    case HandleKind.TypeDefinition:
                        bt = new SaveType { Kind = SaveType.K.Named, Asm = cur.Assembly.Name, Full = GameAssembly.FullName(md, (TypeDefinitionHandle)b) };
                        break;
                    case HandleKind.TypeReference:
                        GameFiles.Named(md, (TypeReferenceHandle)b, out string asm, out string full);
                        bt = new SaveType { Kind = SaveType.K.Named, Asm = asm, Full = full };
                        break;
                    case HandleKind.TypeSpecification:
                        bt = md.GetTypeSpecification((TypeSpecificationHandle)b).DecodeSignature(new SaveShape(cur.Assembly), new SaveCtx(curArgs));
                        break;
                    default:
                        yield break;
                }
                if (bt.Asm == null || !Game(bt.Asm))
                {
                    yield break;
                }
                cur = _files.Find(bt.Asm, bt.Full);
                curArgs = bt.Args;
            }
        }

        // The game's save policy (Player.SaveSystem.SaveGameSerializationPolicy over Odin's Unity rule): a public field not
        // marked unserialized, or one marked to be kept; obsolete ones are left out of a binary save and kept, so named.
        private static bool Written(MetadataReader md, FieldDefinition f, out bool obsolete)
        {
            obsolete = false;
            FieldAttributes a = f.Attributes;
            if ((a & FieldAttributes.Static) != 0 || (a & FieldAttributes.Literal) != 0)
            {
                return false;
            }
            bool isPublic = (a & FieldAttributes.FieldAccessMask) == FieldAttributes.Public;
            bool notSerialized = (a & FieldAttributes.NotSerialized) != 0;
            bool kept = false, odin = false;
            foreach (CustomAttributeHandle ch in f.GetCustomAttributes())
            {
                string n = AttributeName(md, md.GetCustomAttribute(ch));
                if (n == "System.NonSerializedAttribute")
                {
                    notSerialized = true;
                }
                else if (n == "UnityEngine.SerializeField" || n == "UnityEngine.SerializeReference")
                {
                    kept = true;
                }
                else if (n == "OdinSerializer.OdinSerializeAttribute")
                {
                    odin = true;
                }
                else if (n == "System.ObsoleteAttribute")
                {
                    obsolete = true;
                }
            }
            if (notSerialized && !odin)
            {
                return false;
            }
            return isPublic || kept || odin;
        }

        private static string AttributeName(MetadataReader md, CustomAttribute ca)
        {
            EntityHandle owner;
            if (ca.Constructor.Kind == HandleKind.MemberReference)
            {
                owner = md.GetMemberReference((MemberReferenceHandle)ca.Constructor).Parent;
            }
            else if (ca.Constructor.Kind == HandleKind.MethodDefinition)
            {
                owner = md.GetMethodDefinition((MethodDefinitionHandle)ca.Constructor).GetDeclaringType();
            }
            else
            {
                return null;
            }
            if (owner.Kind == HandleKind.TypeReference)
            {
                GameFiles.Named(md, (TypeReferenceHandle)owner, out string _, out string full);
                return full;
            }
            return owner.Kind == HandleKind.TypeDefinition ? GameAssembly.FullName(md, (TypeDefinitionHandle)owner) : null;
        }

        private static bool IsEnumOrDelegate(FoundType ft)
        {
            EntityHandle b = ft.Def.BaseType;
            if (b.IsNil || b.Kind != HandleKind.TypeReference)
            {
                return false;
            }
            GameFiles.Named(ft.Assembly.Md, (TypeReferenceHandle)b, out string _, out string full);
            return full == "System.Enum" || full == "System.MulticastDelegate" || full == "System.Delegate";
        }
    }

    // ---------------------------------------------------------------- the mod's section is the mod's

    // The plain game only declares GameInstance.modData and makes it: nothing of the game's reads or writes it, so a save made
    // with the mod keeps the district's records through a game played without it.
    private static void ModDataUntouched(GameFiles files)
    {
        GameAssembly game = files.Get("BigAmbitions");
        FoundType gi = files.Find("BigAmbitions", "GameInstance");
        if (game == null || !gi.Exists)
        {
            Check(false, "GameInstance is not in BigAmbitions.dll: the census of the save cannot be read");
            return;
        }
        FieldDefinitionHandle modData = default;
        foreach (FieldDefinitionHandle fh in gi.Def.GetFields())
        {
            if (game.Md.GetString(game.Md.GetFieldDefinition(fh).Name) == "modData")
            {
                modData = fh;
            }
        }
        Check(!modData.IsNil, "GameInstance has no modData field any more: the mod's section of the save (the district's records) has nowhere to go");
        if (modData.IsNil)
        {
            return;
        }
        int modDataToken = MetadataTokens.GetToken(modData);
        var touched = new List<string>();
        int methods = 0;
        foreach (string path in Directory.GetFiles(files.Folder, "*.dll"))
        {
            string name = Path.GetFileNameWithoutExtension(path);
            if (!name.StartsWith("BigAmbitions", StringComparison.Ordinal) && name != "HGExtensions" && name != "HGPlugins" && name != "DayNightCycle" && name != "ExternalPlugins")
            {
                continue;
            }
            GameAssembly a = files.Get(name);
            if (a == null)
            {
                continue;
            }
            foreach (TypeDefinitionHandle th in a.Md.TypeDefinitions)
            {
                TypeDefinition t = a.Md.GetTypeDefinition(th);
                string typeName = GameAssembly.FullName(a.Md, th);
                foreach (MethodDefinitionHandle mh in t.GetMethods())
                {
                    MethodDefinition m = a.Md.GetMethodDefinition(mh);
                    if (m.RelativeVirtualAddress == 0)
                    {
                        continue;
                    }
                    methods++;
                    foreach (int token in IlTokens.Read(a.Pe.GetMethodBody(m.RelativeVirtualAddress).GetILContent()))
                    {
                        if (!IsModData(a, token, ReferenceEquals(a, game) ? modDataToken : 0))
                        {
                            continue;
                        }
                        string where = typeName + "." + a.Md.GetString(m.Name);
                        if (where != "GameInstance..ctor")   // the field's own initializer
                        {
                            touched.Add(where);
                        }
                    }
                }
            }
        }
        Check(methods > 10000, "the game's methods read for modData are fewer than the game has (" + methods + "): the reading is broken");
        Check(touched.Count == 0, "the game now reads or writes GameInstance.modData in " + string.Join(", ", touched.Distinct()) + ": the district's records are kept there "
                                  + "for a game played without the mod; read what the game does with it before this build is the mod's (docs/features/district-save.md)");
        Console.WriteLine("   GameInstance.modData: made by its own initializer and touched by none of the game's " + methods + " methods: " + (touched.Count == 0 ? "the mod's alone" : "TOUCHED"));
    }

    private static bool IsModData(GameAssembly a, int token, int ownToken)
    {
        if (ownToken != 0 && token == ownToken)
        {
            return true;
        }
        EntityHandle h = MetadataTokens.EntityHandle(token);
        if (h.Kind != HandleKind.MemberReference)
        {
            return false;
        }
        MemberReference r = a.Md.GetMemberReference((MemberReferenceHandle)h);
        if (r.GetKind() != MemberReferenceKind.Field || a.Md.GetString(r.Name) != "modData" || r.Parent.Kind != HandleKind.TypeReference)
        {
            return false;
        }
        GameFiles.Named(a.Md, (TypeReferenceHandle)r.Parent, out string _, out string full);
        return full == "GameInstance";
    }

    // ---------------------------------------------------------------- every save goes through the district save

    private static void SavePathsHandled(string scripts, Dictionary<string, (string Holds, string Handled)> listed)
    {
        string folder = Path.Combine(scripts, "DistrictSave");
        var commented = new HashSet<string>(StringComparer.Ordinal);
        foreach (string file in Directory.EnumerateFiles(folder, "*.cs"))
        {
            foreach (string line in File.ReadAllLines(file))
            {
                Match m = SavePathComment.Match(line);
                if (m.Success)
                {
                    commented.Add(m.Groups[1].Value);
                    Check(listed.ContainsKey(m.Groups[1].Value), Path.GetFileName(file) + " handles " + m.Groups[1].Value + ", which SavePlaces.txt does not list");
                }
            }
        }
        foreach (KeyValuePair<string, (string Holds, string Handled)> path in listed)
        {
            bool aside = path.Value.Handled == "obsolete" || path.Value.Handled.StartsWith("display:", StringComparison.Ordinal);
            Check(aside || commented.Contains(path.Key), "SavePlaces.txt says the district save handles " + path.Key + " (" + path.Value.Handled + "), but no 'save path:' line in "
                                                         + "Scripts/DistrictSave names it");
        }

        string hook = File.ReadAllText(Path.Combine(scripts, "Core", "SaveHook.cs"));
        int companion = hook.IndexOf("CompanionGame.Save(SaveGameManager.Current);", StringComparison.Ordinal);
        int district = hook.IndexOf("DistrictSave.DistrictSaveDirector.BeforeWrite();", StringComparison.Ordinal);
        Check(companion >= 0 && district > companion, "SaveHook.Run's last step is not the district save's (DistrictSaveDirector.BeforeWrite after every feature has written)");
        Check(hook.Contains("Handlers.Count == 0 && !DistrictSave.DistrictSaveDirector.Holds"), "SaveHook.Drop no longer keeps the save hook while the district save holds anything");
        string steps = File.ReadAllText(Path.Combine(scripts, "Meta", "SaveTools.Steps.cs"));
        Check(steps.Contains("!DistrictSave.DistrictSaveDirector.Keeps(key)"), "the save tools' clean of the mod's data no longer leaves the district's records (DistrictSaveDirector.Keeps)");
        string city = File.ReadAllText(Path.Combine(scripts, "Core", "RealLifeOverhaulMod.cs"));
        Check(city.Contains("DistrictSave.DistrictSaveDirector.City(_root)"), "the city load no longer runs the district save's step");
        string entry = File.ReadAllText(Path.Combine(folder, "DistrictSaveEntry.cs"));
        Check(entry.Contains("[ModEntryMainMenu]") && entry.Contains("DistrictSaveDirector.Early"), "the main menu's entry no longer puts the district's records back at its unload");

        // Nothing of the mod's writes a save but the game's own SaveGameManager.Save, whose save event the district save is the last step of.
        var writers = new List<string>();
        foreach (string file in Directory.EnumerateFiles(scripts, "*.cs", SearchOption.AllDirectories))
        {
            string rel = file.Substring(scripts.Length + 1).Replace('\\', '/');
            string text = File.ReadAllText(file);
            bool writes = text.Contains("SaveGameSerializationHelper.Serialize") || text.Contains("CreateSaveSnapshot") || text.Contains("SerializationUtility.SerializeValue")
                          || text.Contains("SerializationUtility.CreateCopy");
            if (writes && rel != "DistrictSave/DistrictSaveCodec.cs")
            {
                writers.Add(rel);
            }
        }
        Check(writers.Count == 0, "a save is written past the game's SaveGameManager.Save (and so past the district save) in " + string.Join(", ", writers));
        Console.WriteLine("   " + commented.Count + " paths handled in Scripts/DistrictSave; every save goes through its step: " + (writers.Count == 0 ? "yes" : "no"));
    }
}
