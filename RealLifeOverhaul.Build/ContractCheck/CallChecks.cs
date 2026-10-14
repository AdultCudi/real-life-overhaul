using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using RealLifeOverhaul;

// The game's calls into the mod past its entry points, read from the built mod (Core/GuardCalls):
//   the game's calls   every method of the mod's that the game calls through an interface or a base class of its own
//                      (an activity of its work panel, an overlay, a call to action, a pointer, a table and its cells,
//                      a mesh, a drag or a scroll, a row of the options page) is behind the guard: a try whose catch is
//                      the guard's, or a body that cannot fail, or one of the few listed by hand with the reason;
//   the callbacks      every delegate of the mod's own handed to a member of the game's or Unity's, straight, kept in a
//                      field first or passed on by a method of the mod's, is made by the guard (Guard.Wrap, Listener,
//                      Way, Asked, Maker), or its method is guarded itself (UiClone.Safe, the guard's catch), or it goes
//                      to a member the guard covers as a whole (a page of a dialog, a row of the options page), or the
//                      game calls it at once inside the mod's own call (a filter of a search);
//   the hand-overs     every page of a dialog the mod shows goes through Guard.Entry, every dialog the game makes for
//                      the mod through Guard.Maker, every options page the mod registers through Guard.Rows.
internal static partial class Program
{
    private const string ModAssembly = "RealLifeOverhaul";

    // Methods the game calls that are guarded otherwise, or not at all, and why.
    private static readonly Dictionary<string, string> CalledByHand = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        { "RealLifeOverhaul.Meta.OptionsLayout+RowTap.SpawnUi", "a row of the options page that draws nothing: one try round all of it, its catch the page's own log" },
        { "RealLifeOverhaul.ActivityAnimator+MaskedPoseJob.ProcessAnimation", "an animation job: the engine runs it on its animation thread, where the guard cannot run; it blends two poses" },
        { "RealLifeOverhaul.ActivityAnimator+MaskedPoseJob.ProcessRootMotion", "an animation job (as above): it does nothing" },
    };

    // Classes whose methods the game calls as a whole that are guarded otherwise, and why.
    private static readonly Dictionary<string, string> ClassesByHand = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        { "RealLifeOverhaul.RealLifeOverhaulInit", "the game's start of the mod: each of its steps runs in Guard.Step (Core/RealLifeOverhaulMod)" },
        { "RealLifeOverhaul.RealLifeOverhaulCity", "the game's start of the mod in the city: each of its steps runs in Guard.Step (Core/RealLifeOverhaulMod)" },
        { "RealLifeOverhaul.CharacterQualityCity", "the characters' own start, the owner's work: left as it is" },
        { "RealLifeOverhaul.CharacterQualityIntro", "the characters' own start, the owner's work: left as it is" },
        { "RealLifeOverhaul.CharacterLabEntry", "a development aid of the characters' work, the owner's: left as it is" },
        { "RealLifeOverhaul.Meta.Dev+GatedLogger", "the mod's own logger, which the guard itself writes with" },
    };

    // The Unity messages a game's base class declares as virtuals: called once each, by the engine, not by the game's code.
    private static readonly HashSet<string> OnceByUnity = new HashSet<string>(StringComparer.Ordinal)
    {
        "Awake", "Start", "OnEnable", "OnDisable", "OnDestroy", "OnValidate", "Reset", "Finalize", "ToString", "Equals", "GetHashCode",
    };

    // Members of the game's that take a delegate of the mod's and are covered as a whole, or call it at once.
    private static readonly Dictionary<string, string> CoveredMembers = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        { "Entities.DialogEntry::OnConfirm", "a page of a dialog: every page goes to the game through Guard.Entry" },
        { "Entities.DialogEntry::OnSecondOption", "a page of a dialog: every page goes to the game through Guard.Entry" },
        { "Entities.DialogEntry::OnCancel", "a page of a dialog: every page goes to the game through Guard.Entry" },
        { "Entities.DialogEntry::OnForcedCancel", "a page of a dialog: every page goes to the game through Guard.Entry" },
        { "Entities.DialogEntry::OnVisible", "a page of a dialog: every page goes to the game through Guard.Entry" },
        { "BigAmbitions.Mods.ModOptions::AddButton", "a row of the options page: every page goes to the game through Guard.Rows" },
        { "BigAmbitions.Mods.ModOptions::AddToggle", "a row of the options page: every page goes to the game through Guard.Rows" },
        { "BigAmbitions.Mods.ModOptions::AddSlider", "a row of the options page: every page goes to the game through Guard.Rows" },
        { "BigAmbitions.Mods.ModOptions::AddDropdown", "a row of the options page: every page goes to the game through Guard.Rows" },
        { "BigAmbitions.Mods.ButtonOption::.ctor", "a row of the options page: every page goes to the game through Guard.Rows" },
        { "BigAmbitions.Mods.ToggleOption::.ctor", "a row of the options page: every page goes to the game through Guard.Rows" },
        { "BigAmbitions.Mods.SliderOption::.ctor", "a row of the options page: every page goes to the game through Guard.Rows" },
        { "BigAmbitions.Mods.DropdownOption::.ctor", "a row of the options page: every page goes to the game through Guard.Rows" },
        { "GleyTrafficSystem.TrafficManager::GetClosestWaypoint", "a filter the traffic's search calls at once, inside the mod's own call" },
        { "Helpers.BuildingHelper::GetPlayerBuildingRegistrations", "a filter the game's search calls at once, inside the mod's own call" },
        { "ItemHelper::DeliverCargoToBuilding", "a filter the game's delivery calls at once, inside the mod's own call" },
    };

    // Classes whose hand-overs to the game are left as they are, and why.
    private static readonly Dictionary<string, string> ClassesHandedByHand = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        { "RealLifeOverhaul.CharacterQuality", "the characters' own, the owner's work: left as it is" },
        { "RealLifeOverhaul.PlayerDouble", "the characters' own (the player's detailed double), the owner's work: left as it is" },
    };

    // Where a delegate of the mod's goes to the game unguarded on purpose, and why.
    private static readonly Dictionary<string, string> HandedByHand = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        { "RealLifeOverhaul.FrameSteps.Install", "the frame's timestamps in the engine's own loop: a clock read and an addition, only while diagnostics are on" },
        { "RealLifeOverhaul.PerfLoop.Marker", "the benchmark's timestamps in the engine's own loop: a clock read and an addition, only while a benchmark runs" },
        { "RealLifeOverhaul.GameUi.WorldPanel.Watch", "the panel's step aside, once a frame while it is shown: one try round all of it (Core/GameUi/WorldPanel.Beside)" },
        { "RealLifeOverhaul.GameConfirm.Direct", "the question box's answers: its callers hand it answers made by Guard.Wrap (Core/GameConfirm)" },
        { "RealLifeOverhaul.GameConfirm.DirectData", "the question box's answers: its callers hand it answers made by Guard.Wrap (Core/GameConfirm)" },
        { "RealLifeOverhaul.Meta.MetaUi.Button", "the options page's own buttons: each click in a try of its own, its catch the page's own log (Meta/MetaUi), so the page works whatever is off" },
        { "RealLifeOverhaul.Meta.OptionsLayout.Organise", "the options page's own answer to the game's reset of every option: one try round all of it, its catch the page's own log" },
        { "RealLifeOverhaul.Meta.OptionsLayout.Dispose", "(the same answer taken off again)" },
        { "RealLifeOverhaul.Meta.OptionsLayout.SearchBox.Build", "the options page's search box: each of its three listeners one try round all of it, its catch the box's own (Meta/OptionsSearch)" },
    };

    // A type of a signature as 'assembly|full name' (the mod's own: '|full name', or '@delegate|full name' for a delegate type of its own).
    private sealed class TypeNames : ISignatureTypeProvider<string, object>
    {
        public string GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind)
        {
            EntityHandle b = reader.GetTypeDefinition(handle).BaseType;
            bool isDelegate = false;
            if (!b.IsNil && b.Kind == HandleKind.TypeReference)
            {
                GameFiles.Named(reader, (TypeReferenceHandle)b, out string ba, out string bf);
                isDelegate = bf == "System.MulticastDelegate";
            }
            return (isDelegate ? "@delegate" : string.Empty) + "|" + GameAssembly.FullName(reader, handle);
        }

        public string GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind)
        {
            GameFiles.Named(reader, handle, out string assembly, out string full);
            return (assembly ?? string.Empty) + "|" + full;
        }

        public string GetTypeFromSpecification(MetadataReader reader, object context, TypeSpecificationHandle handle, byte rawTypeKind)
        {
            return reader.GetTypeSpecification(handle).DecodeSignature(this, context);
        }

        public string GetPrimitiveType(PrimitiveTypeCode typeCode) => typeCode.ToString();
        public string GetSZArrayType(string elementType) => elementType + "[]";
        public string GetArrayType(string elementType, ArrayShape shape) => elementType + "[,]";
        public string GetByReferenceType(string elementType) => elementType + "&";
        public string GetPointerType(string elementType) => elementType + "*";
        public string GetPinnedType(string elementType) => elementType;
        public string GetModifiedType(string modifier, string unmodifiedType, bool isRequired) => unmodifiedType;
        public string GetGenericInstantiation(string genericType, ImmutableArray<string> typeArguments) => genericType;
        public string GetGenericTypeParameter(object genericContext, int index) => "!" + index;
        public string GetGenericMethodParameter(object genericContext, int index) => "!!" + index;
        public string GetFunctionPointerType(MethodSignature<string> signature) => "fnptr";
    }

    // What a call, a new object or a field store of the IL names: whose it is, its name, and whether it takes a delegate.
    private struct Named
    {
        public string Owner;      // the full name of its type
        public string Name;
        public string Assembly;   // null: the mod's own
        public bool TakesDelegate;
        public bool TakesGameDelegate;   // a delegate type of the game's own (one the game is to call: an engine's power modifier)
        public int Def;           // the mod's own method: its MethodDef token
    }

    private sealed class CallReader
    {
        private readonly MetadataReader _md;
        private readonly GameFiles _files;
        private readonly TypeNames _names = new TypeNames();
        private readonly Dictionary<string, bool> _delegates = new Dictionary<string, bool>(StringComparer.Ordinal);

        public CallReader(MetadataReader md, GameFiles files)
        {
            _md = md;
            _files = files;
        }

        public Named Of(int token)
        {
            EntityHandle h = MetadataTokens.EntityHandle(token);
            switch (h.Kind)
            {
                case HandleKind.MethodDefinition:
                {
                    MethodDefinition m = _md.GetMethodDefinition((MethodDefinitionHandle)h);
                    MethodSignature<string> sig = m.DecodeSignature(_names, null);
                    return new Named
                    {
                        Owner = GameAssembly.FullName(_md, m.GetDeclaringType()), Name = _md.GetString(m.Name), Assembly = null,
                        TakesDelegate = sig.ParameterTypes.Any(Delegate), Def = token,
                    };
                }
                case HandleKind.MethodSpecification:
                    return Of(MetadataTokens.GetToken(_md.GetMethodSpecification((MethodSpecificationHandle)h).Method));
                case HandleKind.FieldDefinition:
                {
                    FieldDefinition f = _md.GetFieldDefinition((FieldDefinitionHandle)h);
                    return new Named
                    {
                        Owner = GameAssembly.FullName(_md, f.GetDeclaringType()), Name = _md.GetString(f.Name), Assembly = null,
                        TakesDelegate = Delegate(f.DecodeSignature(_names, null)),
                    };
                }
                case HandleKind.MemberReference:
                {
                    MemberReference r = _md.GetMemberReference((MemberReferenceHandle)h);
                    string owner = OwnerOf(r.Parent, out string assembly);
                    string[] args = r.Parent.Kind == HandleKind.TypeSpecification ? ArgumentsOf((TypeSpecificationHandle)r.Parent) : null;
                    bool takes, game = false;
                    IEnumerable<string> types = r.GetKind() == MemberReferenceKind.Field
                        ? new[] { r.DecodeFieldSignature(_names, null) }
                        : (IEnumerable<string>)r.DecodeMethodSignature(_names, null).ParameterTypes;
                    var resolved = types.Select(p => p != null && args != null && p.StartsWith("!", StringComparison.Ordinal) && !p.StartsWith("!!", StringComparison.Ordinal)
                                                    && int.TryParse(p.Substring(1), out int k) && k < args.Length ? args[k] : p).ToList();
                    takes = resolved.Any(Delegate);
                    game = resolved.Any(GameDelegate);
                    return new Named { Owner = owner, Name = _md.GetString(r.Name), Assembly = assembly ?? "?", TakesDelegate = takes, TakesGameDelegate = game };
                }
                default:
                    return new Named { Owner = "?", Name = "?", Assembly = "?" };
            }
        }

        private string OwnerOf(EntityHandle parent, out string assembly)
        {
            assembly = null;
            switch (parent.Kind)
            {
                case HandleKind.TypeReference:
                    GameFiles.Named(_md, (TypeReferenceHandle)parent, out assembly, out string full);
                    return full;
                case HandleKind.TypeDefinition:
                    return GameAssembly.FullName(_md, (TypeDefinitionHandle)parent);
                case HandleKind.TypeSpecification:
                {
                    BlobReader b = _md.GetBlobReader(_md.GetTypeSpecification((TypeSpecificationHandle)parent).Signature);
                    if (b.ReadByte() == 0x15)
                    {
                        b.ReadByte();
                        return OwnerOf(b.ReadTypeHandle(), out assembly);
                    }
                    return "?";
                }
                case HandleKind.MethodDefinition:
                    return GameAssembly.FullName(_md, _md.GetMethodDefinition((MethodDefinitionHandle)parent).GetDeclaringType());
                default:
                    return "?";
            }
        }

        // The type arguments of a generic type's instance, as names.
        private string[] ArgumentsOf(TypeSpecificationHandle spec)
        {
            try
            {
                BlobReader b = _md.GetBlobReader(_md.GetTypeSpecification(spec).Signature);
                if (b.ReadByte() != 0x15)
                {
                    return null;
                }
                b.ReadByte();
                b.ReadTypeHandle();
                int count = b.ReadCompressedInteger();
                var decoder = new SignatureDecoder<string, object>(_names, _md, null);
                var args = new string[count];
                for (int i = 0; i < count; i++)
                {
                    args[i] = decoder.DecodeType(ref b);
                }
                return args;
            }
            catch (Exception)
            {
                return null;
            }
        }

        // The full name of a type of a signature, without its assembly.
        public static string Plain(string type)
        {
            return type == null ? null : type.Substring(type.IndexOf('|') + 1);
        }

        // A delegate type of the game's own, not the framework's or the engine's events: one the game is there to call.
        public bool GameDelegate(string type)
        {
            if (type == null || !Delegate(type))
            {
                return false;
            }
            string assembly = type.IndexOf('|') > 0 ? type.Substring(0, type.IndexOf('|')) : null;
            string name = Plain(type);
            return IsGame(assembly) && !name.StartsWith("System.", StringComparison.Ordinal) && !name.StartsWith("UnityEngine.Events.", StringComparison.Ordinal);
        }

        // A delegate type: the framework's and the engine's by their names, the game's own by its base type on disk, the
        // mod's own by its base type in the mod.
        public bool Delegate(string type)
        {
            if (type == null)
            {
                return false;
            }
            if (_delegates.TryGetValue(type, out bool known))
            {
                return known;
            }
            string assembly = type.IndexOf('|') > 0 ? type.Substring(0, type.IndexOf('|')) : null;
            string name = Plain(type);
            bool yes = assembly == "@delegate" || name.StartsWith("System.Action", StringComparison.Ordinal) || name.StartsWith("System.Func`", StringComparison.Ordinal)
                       || name.StartsWith("System.Predicate`", StringComparison.Ordinal) || name.StartsWith("System.Comparison`", StringComparison.Ordinal)
                       || name.StartsWith("System.EventHandler", StringComparison.Ordinal) || name.StartsWith("UnityEngine.Events.UnityAction", StringComparison.Ordinal)
                       || name == "System.Delegate" || name == "System.MulticastDelegate";
            if (!yes && assembly != null && assembly != "@delegate" && !name.StartsWith("System.", StringComparison.Ordinal))
            {
                FoundType t = _files.Find(assembly, name);
                if (t.Exists)
                {
                    EntityHandle b = t.Def.BaseType;
                    if (!b.IsNil && b.Kind == HandleKind.TypeReference)
                    {
                        GameFiles.Named(t.Assembly.Md, (TypeReferenceHandle)b, out string ba, out string bf);
                        yes = bf == "System.MulticastDelegate";
                    }
                }
            }
            _delegates[type] = yes;
            return yes;
        }
    }

    private static bool IsGame(string assembly)
    {
        return assembly != null && assembly != ModAssembly && assembly != "?" && ModScan.IsGameAssembly(assembly);
    }

    // The tokens of the guard's own methods of these names, and the generic instances of them.
    private static HashSet<int> GuardMethods(MetadataReader md, params string[] names)
    {
        var tokens = new HashSet<int>();
        var wanted = new HashSet<string>(names, StringComparer.Ordinal);
        foreach (TypeDefinitionHandle th in md.TypeDefinitions)
        {
            TypeDefinition t = md.GetTypeDefinition(th);
            if (md.GetString(t.Name) != "Guard" || md.GetString(t.Namespace) != "RealLifeOverhaul")
            {
                continue;
            }
            foreach (MethodDefinitionHandle mh in t.GetMethods())
            {
                if (wanted.Contains(md.GetString(md.GetMethodDefinition(mh).Name)))
                {
                    tokens.Add(MetadataTokens.GetToken(mh));
                }
            }
        }
        int specs = md.GetTableRowCount(TableIndex.MethodSpec);
        for (int row = 1; row <= specs; row++)
        {
            MethodSpecificationHandle sh = MetadataTokens.MethodSpecificationHandle(row);
            EntityHandle method = md.GetMethodSpecification(sh).Method;
            if (method.Kind == HandleKind.MethodDefinition && tokens.Contains(MetadataTokens.GetToken(method)))
            {
                tokens.Add(MetadataTokens.GetToken(sh));
            }
        }
        return tokens;
    }

    // True when a method of the mod's or a type of it is the guard's own (Guard, its nested classes).
    private static bool InGuard(MetadataReader md, TypeDefinitionHandle th)
    {
        for (int depth = 0; depth < 8 && !th.IsNil; depth++)
        {
            TypeDefinition t = md.GetTypeDefinition(th);
            if (md.GetString(t.Name) == "Guard" && md.GetString(t.Namespace) == "RealLifeOverhaul")
            {
                return true;
            }
            th = t.GetDeclaringType();
        }
        return false;
    }

    // ---------------------------------------------------------------- what a body is

    private static readonly HashSet<int> Plain = new HashSet<int>
    {
        0x00, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09, 0x0A, 0x0B, 0x0C, 0x0D, 0x0E, 0x0F, 0x10, 0x11, 0x12, 0x13, 0x14,
        0x15, 0x16, 0x17, 0x18, 0x19, 0x1A, 0x1B, 0x1C, 0x1D, 0x1E, 0x1F, 0x20, 0x21, 0x22, 0x23, 0x25, 0x26, 0x2A,
        0x58, 0x59, 0x5A, 0x5B, 0x5C, 0x5D, 0x5E, 0x5F, 0x60, 0x61, 0x62, 0x63, 0x64, 0x65, 0x66, 0x67, 0x68, 0x69, 0x6A, 0x6B, 0x6C, 0x6D, 0x6E,
        0x72, 0x76, 0x7E, 0x8C, 0xD1, 0xD2, 0xD3, 0xE0,
        0xFE01, 0xFE02, 0xFE03, 0xFE04, 0xFE05, 0xFE09, 0xFE0A, 0xFE0B, 0xFE0C, 0xFE0D, 0xFE0E, 0xFE15,
    };

    private static readonly string[] PlainCalls = { "System.Math", "UnityEngine.Mathf", "System.ValueTuple", "System.Nullable", "UnityEngine.Color", "UnityEngine.Vector2", "UnityEngine.Vector3" };

    // A body that cannot fail: constants, the object's own fields, plain arithmetic, comparisons and branches, Math.
    private static bool CannotFail(List<(int Offset, int Op, int Operand)> steps, CallReader names)
    {
        for (int i = 0; i < steps.Count; i++)
        {
            int op = steps[i].Op;
            if (Plain.Contains(op) || (op >= 0x2B && op <= 0x44))
            {
                continue;
            }
            if (op == 0x7B && i > 0 && steps[i - 1].Op == 0x02)
            {
                continue;   // ldfld of this
            }
            if (op == 0x7D && i > 1 && steps[i - 2].Op == 0x02)
            {
                continue;   // stfld of this, of a value loaded just before
            }
            if ((op == 0x28 || op == 0x73) && PlainCalls.Any(p => names.Of(steps[i].Operand).Owner.StartsWith(p, StringComparison.Ordinal)))
            {
                continue;
            }
            return false;
        }
        return true;
    }

    // A body behind the guard: a catch whose handler calls one of the guard's (Fail, Failed, FailMesh, Ended, Way's own),
    // or a body that hands its work to UiClone.Safe.
    private static bool Guarded(MethodBodyBlock body, List<(int Offset, int Op, int Operand)> steps, HashSet<int> guardCatch, HashSet<int> safe)
    {
        if (steps.Any(s => (s.Op == 0x28 || s.Op == 0x6F) && safe.Contains(s.Operand)))
        {
            return true;
        }
        foreach (ExceptionRegion r in body.ExceptionRegions)
        {
            if (r.Kind != ExceptionRegionKind.Catch)
            {
                continue;
            }
            int from = r.HandlerOffset, to = r.HandlerOffset + r.HandlerLength;
            if (steps.Any(s => s.Offset >= from && s.Offset < to && (s.Op == 0x28 || s.Op == 0x6F) && guardCatch.Contains(s.Operand)))
            {
                return true;
            }
        }
        return false;
    }

    private static int SafeToken(MetadataReader md)
    {
        foreach (TypeDefinitionHandle th in md.TypeDefinitions)
        {
            TypeDefinition t = md.GetTypeDefinition(th);
            if (md.GetString(t.Name) != "UiClone")
            {
                continue;
            }
            foreach (MethodDefinitionHandle mh in t.GetMethods())
            {
                if (md.GetString(md.GetMethodDefinition(mh).Name) == "Safe")
                {
                    return MetadataTokens.GetToken(mh);
                }
            }
        }
        return 0;
    }

    // ---------------------------------------------------------------- the game's calls

    private static void GameCalls(MetadataReader md, PEReader pe, GameFiles files, GameAssembly mod)
    {
        var names = new CallReader(md, files);
        HashSet<int> guardCatch = GuardMethods(md, "Fail", "Failed", "FailMesh", "Ended");
        var safe = new HashSet<int> { SafeToken(md) };
        Check(guardCatch.Count >= 4, "the game's calls: Guard.Fail, Failed, FailMesh and Ended are not all in the built mod");
        int called = 0, guarded = 0, plain = 0, byHand = 0, bare = 0;
        var kinds = new SortedDictionary<string, int>(StringComparer.Ordinal);
        foreach (TypeDefinitionHandle th in md.TypeDefinitions)
        {
            TypeDefinition t = md.GetTypeDefinition(th);
            string typeName = GameAssembly.FullName(md, th);
            if (typeName.IndexOf('<') >= 0 || InGuard(md, th))
            {
                continue;
            }
            // What the game asks of it: the methods of the game's interfaces it implements, the game's virtuals above it.
            var asked = new HashSet<string>(StringComparer.Ordinal);
            var kind = new List<string>();
            foreach (InterfaceImplementationHandle ih in t.GetInterfaceImplementations())
            {
                FoundType iface = files.Resolve(mod, md.GetInterfaceImplementation(ih).Interface, null);
                if (!iface.Exists || iface.Assembly == mod || !IsGame(iface.Assembly.Name))
                {
                    continue;
                }
                kind.Add(GameAssembly.FullName(iface.Assembly.Md, iface.Handle));
                foreach (MethodDefinitionHandle mh in iface.Def.GetMethods())
                {
                    asked.Add(iface.Assembly.Md.GetString(iface.Assembly.Md.GetMethodDefinition(mh).Name));
                }
            }
            bool first = true;
            foreach (FoundType b in files.Chain(new FoundType { Assembly = mod, Handle = th }))
            {
                if (first)
                {
                    first = false;
                    continue;
                }
                if (b.Assembly == mod || !IsGame(b.Assembly.Name))
                {
                    continue;
                }
                string baseName = GameAssembly.FullName(b.Assembly.Md, b.Handle);
                if (baseName == "UnityEngine.MonoBehaviour" || baseName == "UnityEngine.Behaviour" || baseName == "UnityEngine.Component" || baseName == "UnityEngine.Object"
                    || baseName == "UnityEngine.ScriptableObject")
                {
                    break;
                }
                kind.Add(baseName);
                foreach (MethodDefinitionHandle mh in b.Def.GetMethods())
                {
                    MethodDefinition m = b.Assembly.Md.GetMethodDefinition(mh);
                    if ((m.Attributes & MethodAttributes.Virtual) != 0)
                    {
                        asked.Add(b.Assembly.Md.GetString(m.Name));
                    }
                }
            }
            if (asked.Count == 0)
            {
                continue;
            }
            bool wholeByHand = ClassesByHand.ContainsKey(typeName);
            foreach (MethodDefinitionHandle mh in t.GetMethods())
            {
                MethodDefinition m = md.GetMethodDefinition(mh);
                string name = md.GetString(m.Name);
                string shortName = name.Substring(name.LastIndexOf('.') + 1);
                if ((m.Attributes & MethodAttributes.Virtual) == 0 || m.RelativeVirtualAddress == 0 || !asked.Contains(shortName) || OnceByUnity.Contains(shortName))
                {
                    continue;
                }
                called++;
                string full = typeName + "." + shortName;
                if (wholeByHand || CalledByHand.ContainsKey(full))
                {
                    byHand++;
                    continue;
                }
                MethodBodyBlock body = pe.GetMethodBody(m.RelativeVirtualAddress);
                var steps = Steps(body.GetILBytes()).ToList();
                if (Guarded(body, steps, guardCatch, safe))
                {
                    guarded++;
                }
                else if (CannotFail(steps, names))
                {
                    plain++;
                }
                else
                {
                    bare++;
                    Check(false, "the game's calls: " + full + " (" + string.Join(", ", kind) + ") is not behind the guard: its body is to be a try whose catch is the guard's (Core/GuardCalls: Guard.Failed)");
                    continue;
                }
                foreach (string k in kind)
                {
                    kinds[k] = kinds.TryGetValue(k, out int n) ? n + 1 : 1;
                }
            }
        }
        Check(called > 150, "the game's calls: fewer methods of the mod's that the game calls through its interfaces and base classes than the mod has (" + called + ")");
        Console.WriteLine("   " + called + " methods of the mod's that the game calls through its interfaces and base classes: " + guarded + " behind the guard, " + plain
                          + " that cannot fail, " + byHand + " by hand, " + bare + " not.");
        Console.WriteLine("   by what the game calls them as: " + string.Join("; ", kinds.Select(k => ShortType(k.Key) + " " + k.Value)) + ".");
        foreach (KeyValuePair<string, string> hand in ClassesByHand.Concat(CalledByHand))
        {
            Console.WriteLine("   by hand: " + hand.Key.Replace("RealLifeOverhaul.", string.Empty) + ": " + hand.Value + ".");
        }
    }

    private static string ShortType(string full)
    {
        return full.Substring(full.LastIndexOf('.') + 1).Replace("`1", string.Empty).Replace("`2", string.Empty);
    }

    // ---------------------------------------------------------------- the callbacks

    private enum Goes { Nowhere, Guard, Mod, Framework, Game, Field }

    // Where the value on top of the stack after step 'from' goes: the call or the store that takes it.
    private static Goes Taken(List<(int Offset, int Op, int Operand)> steps, int from, CallReader names, HashSet<int> wrappers, MetadataReader md, out Named to)
    {
        to = default;
        for (int j = from + 1; j < steps.Count && j <= from + 14; j++)
        {
            int op = steps[j].Op;
            if (op == 0x25)
            {
                continue;   // dup
            }
            if (op == 0x80 && steps[j - 1].Op == 0x25 && MetadataTokens.EntityHandle(steps[j].Operand).Kind == HandleKind.FieldDefinition
                && md.GetString(md.GetFieldDefinition((FieldDefinitionHandle)MetadataTokens.EntityHandle(steps[j].Operand)).Name).IndexOf('<') >= 0)
            {
                continue;   // the compiler's cache of a lambda or a method group: the value goes on
            }
            if (op == 0x28 || op == 0x6F || op == 0x73)
            {
                if (wrappers.Contains(steps[j].Operand))
                {
                    return Goes.Guard;
                }
                Named n = names.Of(steps[j].Operand);
                if (n.Assembly == null && n.Owner == "RealLifeOverhaul.EventHold" && n.Name == "Keep")
                {
                    to = n;
                    to.Owner = "event field, kept by EventHold";   // (Core/EventHold puts the handler on the game's own field; its type is generic)
                    return Goes.Game;
                }
                if (!n.TakesDelegate || (n.Owner == "System.Delegate" && (n.Name == "Combine" || n.Name == "Remove")))
                {
                    continue;   // (a call for another argument; or the delegate joined to an event's, which is stored next)
                }
                to = n;
                if (n.Assembly == null)
                {
                    return Goes.Mod;
                }
                return IsGame(n.Assembly) || n.TakesGameDelegate ? Goes.Game : Goes.Framework;   // (a list of the game's delegates is the game's)
            }
            if (op == 0x7D || op == 0x80)
            {
                Named n = names.Of(steps[j].Operand);
                if (!n.TakesDelegate)
                {
                    continue;   // (a store of another value)
                }
                to = n;
                return n.Assembly == null ? Goes.Field : IsGame(n.Assembly) || n.TakesGameDelegate ? Goes.Game : Goes.Framework;
            }
            if (op == 0x2A || (op >= 0x0A && op <= 0x0D) || op == 0x13 || op == 0xFE0E || op == 0x10 || op == 0xFE0B || op == 0x7A
                || (op >= 0x2B && op <= 0x44) || op == 0x45 || op == 0xDD || op == 0xDE)
            {
                return Goes.Nowhere;   // a return, a local, a branch: followed no further
            }
        }
        return Goes.Nowhere;
    }

    private static void Callbacks(MetadataReader md, PEReader pe, GameFiles files)
    {
        var names = new CallReader(md, files);
        HashSet<int> wrappers = GuardMethods(md, "Wrap", "Listener", "Way", "Asked", "Maker", "Entry", "Rows", "Routine", "Each", "Step");
        HashSet<int> guardCatch = GuardMethods(md, "Fail", "Failed", "FailMesh", "Ended");
        var safe = new HashSet<int> { SafeToken(md) };
        foreach (TypeDefinitionHandle th in md.TypeDefinitions)
        {
            TypeDefinition t = md.GetTypeDefinition(th);
            if (md.GetString(t.Name) == "UiClone")
            {
                foreach (MethodDefinitionHandle mh in t.GetMethods())
                {
                    wrappers.Add(MetadataTokens.GetToken(mh));   // (UiClone's clicks go through Safe)
                }
            }
        }
        var bodies = new Dictionary<MethodDefinitionHandle, List<(int Offset, int Op, int Operand)>>();
        foreach (MethodDefinitionHandle mh in md.MethodDefinitions)
        {
            MethodDefinition m = md.GetMethodDefinition(mh);
            if (m.RelativeVirtualAddress != 0)
            {
                bodies[mh] = Steps(pe.GetMethodBody(m.RelativeVirtualAddress).GetILBytes()).ToList();
            }
        }
        // A delegate's method guarded itself: its work handed to UiClone.Safe, or a catch that is the guard's.
        var guardedSelf = new Dictionary<int, bool>();
        bool SelfGuarded(int token)
        {
            if (guardedSelf.TryGetValue(token, out bool known))
            {
                return known;
            }
            EntityHandle h = MetadataTokens.EntityHandle(token);
            bool yes = false;
            if (h.Kind == HandleKind.MethodDefinition && bodies.TryGetValue((MethodDefinitionHandle)h, out var steps))
            {
                MethodDefinition m = md.GetMethodDefinition((MethodDefinitionHandle)h);
                yes = Guarded(pe.GetMethodBody(m.RelativeVirtualAddress), steps, guardCatch, safe) || CannotFail(steps, names);
            }
            guardedSelf[token] = yes;
            return yes;
        }

        int made = 0, toGuard = 0, covered = 0, self = 0, byHand = 0, bare = 0, kept = 0, passed = 0;
        var raw = new Dictionary<int, string>();   // a field of the mod's that keeps a delegate of the mod's not made by the guard
        void Judge(string where, string what, Named to)
        {
            string member = to.Owner + "::" + to.Name;
            if (CoveredMembers.ContainsKey(member))
            {
                covered++;
            }
            else if (HandedByHand.ContainsKey(where) || ClassesHandedByHand.Keys.Any(c => where.StartsWith(c + ".", StringComparison.Ordinal)))
            {
                byHand++;
            }
            else
            {
                bare++;
                Check(false, "callbacks: " + where.Replace("RealLifeOverhaul.", string.Empty) + " hands " + what + " to the game's " + member
                             + " outside the guard: Guard.Wrap, Listener, Way or Asked (Core/GuardCalls)");
            }
        }

        foreach (var kv in bodies)
        {
            MethodDefinition m = md.GetMethodDefinition(kv.Key);
            if (InGuard(md, m.GetDeclaringType()))
            {
                continue;
            }
            string where = GameAssembly.FullName(md, m.GetDeclaringType()).Replace('+', '.') + "." + md.GetString(m.Name);
            var steps = kv.Value;
            for (int i = 1; i < steps.Count; i++)
            {
                if (steps[i].Op != 0x73 || (steps[i - 1].Op != 0xFE06 && steps[i - 1].Op != 0xFE07)
                    || MetadataTokens.EntityHandle(steps[i - 1].Operand).Kind != HandleKind.MethodDefinition)
                {
                    continue;   // (only a delegate of a method of the mod's own)
                }
                int target = steps[i - 1].Operand;
                Goes goes = Taken(steps, i, names, wrappers, md, out Named to);
                if (goes == Goes.Guard)
                {
                    made++;
                    toGuard++;
                    continue;
                }
                if (goes != Goes.Game)
                {
                    continue;
                }
                made++;
                if (SelfGuarded(target))
                {
                    self++;
                    continue;
                }
                Judge(where, "a delegate of " + names.Of(target).Owner.Replace("RealLifeOverhaul.", string.Empty) + "." + names.Of(target).Name, to);
            }
        }

        // The fields that keep a delegate of the mod's not made by the guard, and where they are handed to the game.
        raw.Clear();
        foreach (var kv in bodies)
        {
            var steps = kv.Value;
            for (int i = 1; i < steps.Count; i++)
            {
                if (steps[i].Op != 0x73 || (steps[i - 1].Op != 0xFE06 && steps[i - 1].Op != 0xFE07)
                    || MetadataTokens.EntityHandle(steps[i - 1].Operand).Kind != HandleKind.MethodDefinition)
                {
                    continue;
                }
                int target = steps[i - 1].Operand;
                for (int j = i + 1; j < steps.Count && j <= i + 3; j++)
                {
                    int op = steps[j].Op;
                    if (op == 0x25)
                    {
                        continue;
                    }
                    if ((op == 0x7D || op == 0x80) && MetadataTokens.EntityHandle(steps[j].Operand).Kind == HandleKind.FieldDefinition)
                    {
                        FieldDefinition f = md.GetFieldDefinition((FieldDefinitionHandle)MetadataTokens.EntityHandle(steps[j].Operand));
                        if (md.GetString(f.Name).IndexOf('<') < 0 && !SelfGuarded(target))
                        {
                            raw[steps[j].Operand] = names.Of(target).Owner.Replace("RealLifeOverhaul.", string.Empty) + "." + names.Of(target).Name;
                        }
                    }
                    break;
                }
            }
        }
        foreach (var kv in bodies)
        {
            MethodDefinition m = md.GetMethodDefinition(kv.Key);
            if (InGuard(md, m.GetDeclaringType()))
            {
                continue;
            }
            string where = GameAssembly.FullName(md, m.GetDeclaringType()).Replace('+', '.') + "." + md.GetString(m.Name);
            var steps = kv.Value;
            for (int i = 0; i < steps.Count; i++)
            {
                if ((steps[i].Op != 0x7B && steps[i].Op != 0x7E) || !raw.TryGetValue(steps[i].Operand, out string target))
                {
                    continue;
                }
                if (Taken(steps, i, names, wrappers, md, out Named to) != Goes.Game)
                {
                    continue;
                }
                kept++;
                FieldDefinition f = md.GetFieldDefinition((FieldDefinitionHandle)MetadataTokens.EntityHandle(steps[i].Operand));
                Judge(where, "the field " + md.GetString(f.Name) + " (" + target + ")", to);
            }
            // A delegate the method was given, handed on to the game.
            MethodSignature<string> sig = m.DecodeSignature(new TypeNames(), null);
            bool instance = (m.Attributes & MethodAttributes.Static) == 0;
            for (int i = 0; i < steps.Count; i++)
            {
                int op = steps[i].Op;
                int arg = op >= 0x02 && op <= 0x05 ? op - 0x02 : op == 0x0E ? steps[i].Operand : -1;
                int index = instance ? arg - 1 : arg;
                if (arg < 0 || index < 0 || index >= sig.ParameterTypes.Length || !names.Delegate(sig.ParameterTypes[index]))
                {
                    continue;
                }
                if (Taken(steps, i, names, wrappers, md, out Named to) != Goes.Game)
                {
                    continue;
                }
                passed++;
                Judge(where, "its parameter " + (index + 1) + " (" + CallReader.Plain(sig.ParameterTypes[index]) + ")", to);
            }
        }
        Check(made > 100, "callbacks: fewer delegates of the mod's handed to the game than the mod hands it (" + made + ")");
        Console.WriteLine("   " + (made + kept + passed) + " delegates of the mod's handed to the game's or Unity's members (" + made + " where they are made, " + kept + " from a field, "
                          + passed + " passed on): " + toGuard + " made by the guard, " + self + " guarded themselves, " + covered + " to a member the guard covers as a whole, "
                          + byHand + " by hand, " + bare + " not.");
        foreach (KeyValuePair<string, string> hand in ClassesHandedByHand.Concat(HandedByHand))
        {
            Console.WriteLine("   by hand: " + hand.Key.Replace("RealLifeOverhaul.", string.Empty) + ": " + hand.Value + ".");
        }
    }

    // ---------------------------------------------------------------- the hand-overs

    private static void Handovers(MetadataReader md, PEReader pe, GameFiles files)
    {
        var names = new CallReader(md, files);
        var rules = new[]
        {
            ("DialogController", "ShowEntry", GuardMethods(md, "Entry"), "a page of a dialog", "Guard.Entry"),
            ("Dialogs.CallDialogFactory", "RegisterDialog", GuardMethods(md, "Maker"), "the maker of a dialog", "Guard.Maker"),
            ("BigAmbitions.Mods.OptionsService", "Register", GuardMethods(md, "Rows"), "an options page", "Guard.Rows"),
        };
        var counts = new int[rules.Length];
        foreach (MethodDefinitionHandle mh in md.MethodDefinitions)
        {
            MethodDefinition m = md.GetMethodDefinition(mh);
            if (m.RelativeVirtualAddress == 0 || InGuard(md, m.GetDeclaringType()))
            {
                continue;
            }
            var steps = Steps(pe.GetMethodBody(m.RelativeVirtualAddress).GetILBytes()).ToList();
            for (int i = 1; i < steps.Count; i++)
            {
                if (steps[i].Op != 0x28 && steps[i].Op != 0x6F)
                {
                    continue;
                }
                Named n = names.Of(steps[i].Operand);
                for (int r = 0; r < rules.Length; r++)
                {
                    if (n.Owner != rules[r].Item1 || n.Name != rules[r].Item2 || n.Assembly == null)
                    {
                        continue;
                    }
                    counts[r]++;
                    bool through = (steps[i - 1].Op == 0x28 || steps[i - 1].Op == 0x6F) && rules[r].Item3.Contains(steps[i - 1].Operand);
                    Check(through, "hand-overs: " + FullMethodName(md, mh).Replace("RealLifeOverhaul.", string.Empty) + " gives the game " + rules[r].Item4 + " not made by " + rules[r].Item5
                                   + " (Core/GuardCalls)");
                }
            }
        }
        Check(counts[0] > 40 && counts[1] > 30 && counts[2] >= 2, "hand-overs: fewer pages of dialogs, makers of dialogs or options pages than the mod gives the game ("
                                                              + counts[0] + ", " + counts[1] + ", " + counts[2] + ")");
        Console.WriteLine("   " + counts[0] + " pages of dialogs given to the game, each through Guard.Entry; " + counts[1] + " makers of dialogs, each through Guard.Maker; "
                          + counts[2] + " options pages, each through Guard.Rows.");
    }
}
