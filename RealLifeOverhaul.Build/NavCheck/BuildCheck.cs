using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Text.RegularExpressions;
using RealLifeOverhaul;

// Checks for the build check and the guard against a game that lacks members (Core/GameBuild.cs as it ships, and the
// mod's sources read as text):
//   the rules        which build is older or newer, the line Player.log gets, the member named in Mono's own messages of a
//                    missing member (the very lines Build 3675 wrote)
//   the game         the installed game the mod is built against (the GameManaged folder the build uses, G:\Steam by
//                    default): its build, read from its resources as its main menu reads it, is GameBuild.MadeFor; every
//                    member of GameBuild.Isolated is in its assemblies, a method with exactly the parameters listed
//   the sources      the members the old game lacked are named only in Core/GameMembers.cs and Core/GameConfirm.cs, and
//                    every entry point that stands down on a missing member still calls GameMembers.StandsDown
// What is not here: whether a game without these members really runs the mod, which only an older game can show
// (the "Game build:" lines of Player.log).
internal static partial class Program
{
    private const string DefaultManaged = @"G:\Steam\steamapps\common\Big Ambitions\Big Ambitions_Data\Managed";

    private static void Builds()
    {
        BuildRules();
        MemberNames();
        string managed = Environment.GetEnvironmentVariable("GameManaged");
        if (string.IsNullOrEmpty(managed))
        {
            managed = DefaultManaged;
        }
        InstalledBuild(managed);
        InstalledMembers(managed);
        string scripts = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts"));
        NamedOnlyInTheGuard(scripts);
        EntryPointsGuarded(scripts);
    }

    // ---------------------------------------------------------------- the rules

    private static void BuildRules()
    {
        int made = GameBuild.MadeFor;
        Check(GameBuild.Of(made) == BuildKind.Same, "build: the build the mod is made for is the same build");
        Check(GameBuild.Of(3675) == BuildKind.Older && GameBuild.Of(made - 1) == BuildKind.Older, "build: Build 3675, and the one just before, are older");
        Check(GameBuild.Of(made + 1) == BuildKind.Newer, "build: the next build is newer");
        Check(GameBuild.Of(0) == BuildKind.Unknown && GameBuild.Of(-5) == BuildKind.Unknown, "build: a build that could not be read (0) is unknown, not older");

        Check(GameBuild.Line(made, "9.9.9") == null, "build: the build the mod is made for writes no warning (the diagnostics' check line says it)");
        string older = GameBuild.Line(3675, "9.9.9");
        Check(older != null && older.Contains("Build 3675") && older.Contains("Build " + made) && older.Contains("older") && older.Contains("9.9.9"),
            "build: an older game's line names both builds, says older and names the mod's version");
        Check(older != null && older.Contains("Betas set to None") && older.Contains("Verify integrity of game files"), "build: an older game's line says how to update it in Steam");
        string newer = GameBuild.Line(made + 8, "9.9.9");
        Check(newer != null && newer.Contains("newer") && !newer.Contains("Verify integrity"), "build: a newer game's line says newer, and nothing of updating");
        string unknown = GameBuild.Line(0, "9.9.9");
        Check(unknown != null && unknown.Contains("could not be read") && unknown.Contains("Build " + made), "build: a build that could not be read says so, with the build the mod is made for");
        foreach (string line in new[] { older, newer, unknown, GameBuild.Builds(3675, "9.9.9"), GameBuild.Builds(0, "9.9.9") })
        {
            Check(line != null && line.IndexOf('\u2014') < 0 && line.IndexOf('\u2013') < 0 && line.IndexOf(" - ", StringComparison.Ordinal) < 0 && line.IndexOf('!') < 0,
                "build: a line of the build check has no dash and no exclamation mark: " + line);
        }
        Check(GameBuild.Builds(3675, "9.9.9") == "This game is Build 3675; Real Life Overhaul 9.9.9 is made for Build " + made + ".", "build: the closing words name both builds");
    }

    private static void MemberNames()
    {
        // The messages exactly as Build 3675 wrote them to Player.log (Mono's), and .NET's, and a few edges.
        var cases = new (string Message, string Member)[]
        {
            ("Method not found: void .HudConfirm.Show(string,string,System.Action,System.Action,string,string,bool,bool)",
             "HudConfirm.Show(string,string,System.Action,System.Action,string,string,bool,bool)"),
            ("Method not found: EntityController UI.InGameUI.BuildingResume.get_TargetController()", "UI.InGameUI.BuildingResume.TargetController"),
            ("Method not found: string .SubwayStation.GetNameKey()", "SubwayStation.GetNameKey()"),
            ("Field not found: int Buildings.Building.locatedInsidePrivateFenceIndex Due to: Could not find field in class", "Buildings.Building.locatedInsidePrivateFenceIndex"),
            ("Could not resolve type with token 010002a5 from typeref (expected class 'Helpers.FuneralHelper' in assembly 'BigAmbitions, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null')",
             "Helpers.FuneralHelper"),
            ("Could not load type 'Helpers.FuneralHelper' from assembly 'BigAmbitions, Version=0.0.0.0'.", "Helpers.FuneralHelper"),
            ("Method not found: 'Void HudConfirm.Show(System.String, System.String)'.", "HudConfirm.Show(System.String, System.String)"),
            ("Method not found: void .Foo.set_Bar(bool)", "Foo.Bar"),
            ("Field not found: bool .HudConfirm.isOpen", "HudConfirm.isOpen"),
        };
        foreach (var c in cases)
        {
            string got = GameBuild.MemberOf(c.Message);
            Check(got == c.Member, "member: '" + c.Message + "' names '" + got + "', wanted '" + c.Member + "'");
        }
        Check(GameBuild.MemberOf(null) == "a member it does not name" && GameBuild.MemberOf("") == "a member it does not name", "member: no message names no member, without a fault");
        string other = GameBuild.MemberOf("Something else went wrong\non two lines");
        Check(other == "Something else went wrong on two lines", "member: any other message is given as it is, on one line");
        Check(GameBuild.MemberOf(new string('x', 400)).Length == 160, "member: a long message is cut to a line");
    }

    // ---------------------------------------------------------------- the installed game

    // The game's build as its main menu reads it: GameVersion.GetCurrent() is the resource Versioning/Current, a
    // ScriptableObject in resources.assets whose serialised fields after its name ("Current") are experimental (a bool,
    // padded to four bytes), buildNumber (an int) and latestBuildNumberForVersion (a count, then build and name pairs).
    internal static int ReadInstalledBuild(string dataFolder, out string how)
    {
        string path = Path.Combine(dataFolder, "resources.assets");
        how = path;
        if (!File.Exists(path))
        {
            how = path + " is not there";
            return 0;
        }
        byte[] data = File.ReadAllBytes(path);
        byte[] name = { 7, 0, 0, 0, (byte)'C', (byte)'u', (byte)'r', (byte)'r', (byte)'e', (byte)'n', (byte)'t' };
        var found = new List<int>();
        for (int at = IndexOf(data, name, 0); at >= 0; at = IndexOf(data, name, at + 1))
        {
            int field = (at + name.Length + 3) & ~3;
            if (field + 12 > data.Length || data[field] > 1 || data[field + 1] != 0 || data[field + 2] != 0 || data[field + 3] != 0)
            {
                continue;
            }
            int build = BitConverter.ToInt32(data, field + 4);
            int groups = BitConverter.ToInt32(data, field + 8);
            if (build < 1000 || build > 100000 || groups < 1 || groups > 200)
            {
                continue;
            }
            // The groups: an int and a name each, in order, the last running to 9999 or beyond.
            int read = field + 12, last = 0;
            bool fits = true;
            for (int g = 0; g < groups && fits; g++)
            {
                if (read + 8 > data.Length)
                {
                    fits = false;
                    break;
                }
                int upTo = BitConverter.ToInt32(data, read);
                int length = BitConverter.ToInt32(data, read + 4);
                fits = upTo >= last && length >= 0 && length < 64 && read + 8 + length <= data.Length;
                last = upTo;
                read = (read + 8 + length + 3) & ~3;
            }
            if (fits && last >= build)
            {
                found.Add(build);
            }
        }
        if (found.Count != 1)
        {
            how = path + ": " + (found.Count == 0 ? "the game's version object was not found" : found.Count + " version objects were found");
            return 0;
        }
        return found[0];
    }

    private static int IndexOf(byte[] data, byte[] what, int from)
    {
        for (int i = from; i <= data.Length - what.Length; i++)
        {
            if (data[i] != what[0])
            {
                continue;
            }
            int j = 1;
            while (j < what.Length && data[i + j] == what[j])
            {
                j++;
            }
            if (j == what.Length)
            {
                return i;
            }
        }
        return -1;
    }

    private static void InstalledBuild(string managed)
    {
        string dataFolder = Path.GetDirectoryName(managed.TrimEnd('\\', '/'));
        int build = dataFolder != null ? ReadInstalledBuild(dataFolder, out string how) : 0;
        if (build <= 0)
        {
            Check(false, "game build: the build of the game the mod is built against could not be read (" + (dataFolder ?? managed) + "); set GameManaged to the game's Managed folder");
            return;
        }
        Check(build == GameBuild.MadeFor, "game build: the game the mod is built against is Build " + build + ", and GameBuild.MadeFor says " + GameBuild.MadeFor
            + ". After a game update, build and check the mod against the new game, then set GameBuild.MadeFor (Core/GameBuild.cs) to its build");
        Console.WriteLine("Game build: the game the mod is built against is Build " + build + "; the mod is made for Build " + GameBuild.MadeFor + ".");
    }

    private static void InstalledMembers(string managed)
    {
        var assemblies = new Dictionary<string, MetadataReader>(StringComparer.OrdinalIgnoreCase);
        var readers = new List<PEReader>();
        try
        {
            int found = 0;
            foreach (GameNeed need in GameBuild.Isolated)
            {
                if (!assemblies.TryGetValue(need.Assembly, out MetadataReader md))
                {
                    string path = Path.Combine(managed, need.Assembly + ".dll");
                    if (File.Exists(path))
                    {
                        var pe = new PEReader(File.OpenRead(path));
                        readers.Add(pe);
                        md = pe.GetMetadataReader();
                    }
                    assemblies[need.Assembly] = md;
                }
                if (md == null)
                {
                    Check(false, "game members: " + need.Assembly + ".dll is not in " + managed + " (needed for " + need + ")");
                    continue;
                }
                string why = Find(md, need);
                Check(why == null, "game members: " + need + " in " + need.Assembly + ".dll: " + why);
                found += why == null ? 1 : 0;
            }
            Console.WriteLine("Game members: " + found + " of " + GameBuild.Isolated.Length + " that the mod reads in methods of their own are in the game at " + managed + ".");
        }
        finally
        {
            foreach (PEReader pe in readers)
            {
                pe.Dispose();
            }
        }
    }

    // Null when the member is there as listed, else what is wrong.
    private static string Find(MetadataReader md, GameNeed need)
    {
        TypeDefinitionHandle type = default;
        foreach (TypeDefinitionHandle h in md.TypeDefinitions)
        {
            if (FullName(md, h) == need.Type)
            {
                type = h;
                break;
            }
        }
        if (type.IsNil)
        {
            return "the type " + need.Type + " is not there";
        }
        TypeDefinition t = md.GetTypeDefinition(type);
        switch (need.Kind)
        {
            case GameNeedKind.Field:
                foreach (FieldDefinitionHandle f in t.GetFields())
                {
                    if (md.GetString(md.GetFieldDefinition(f).Name) == need.Name)
                    {
                        return null;
                    }
                }
                return "it has no field " + need.Name;
            case GameNeedKind.Property:
                foreach (PropertyDefinitionHandle p in t.GetProperties())
                {
                    if (md.GetString(md.GetPropertyDefinition(p).Name) == need.Name)
                    {
                        return null;
                    }
                }
                return "it has no property " + need.Name;
            default:
                var seen = new List<string>();
                foreach (MethodDefinitionHandle m in t.GetMethods())
                {
                    MethodDefinition method = md.GetMethodDefinition(m);
                    if (md.GetString(method.Name) != need.Name)
                    {
                        continue;
                    }
                    MethodSignature<string> signature = method.DecodeSignature(new TypeNames(), null);
                    string parameters = string.Join(",", signature.ParameterTypes);
                    if (need.Parameters == null || parameters == need.Parameters)
                    {
                        return null;
                    }
                    seen.Add("(" + parameters + ")");
                }
                return seen.Count == 0 ? "it has no method " + need.Name : "its " + need.Name + " takes " + string.Join(" or ", seen) + ", not (" + need.Parameters + ")";
        }
    }

    private static string FullName(MetadataReader md, TypeDefinitionHandle h)
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

    // A type in a signature as C# names it ("string", "bool") or by its full name ("System.Action").
    private sealed class TypeNames : ISignatureTypeProvider<string, object>
    {
        public string GetPrimitiveType(PrimitiveTypeCode code)
        {
            switch (code)
            {
                case PrimitiveTypeCode.Boolean: return "bool";
                case PrimitiveTypeCode.Char: return "char";
                case PrimitiveTypeCode.SByte: return "sbyte";
                case PrimitiveTypeCode.Byte: return "byte";
                case PrimitiveTypeCode.Int16: return "short";
                case PrimitiveTypeCode.UInt16: return "ushort";
                case PrimitiveTypeCode.Int32: return "int";
                case PrimitiveTypeCode.UInt32: return "uint";
                case PrimitiveTypeCode.Int64: return "long";
                case PrimitiveTypeCode.UInt64: return "ulong";
                case PrimitiveTypeCode.Single: return "float";
                case PrimitiveTypeCode.Double: return "double";
                case PrimitiveTypeCode.String: return "string";
                case PrimitiveTypeCode.Object: return "object";
                case PrimitiveTypeCode.Void: return "void";
                default: return code.ToString();
            }
        }

        public string GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind) => FullName(reader, handle);

        public string GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind)
        {
            TypeReference t = reader.GetTypeReference(handle);
            string name = reader.GetString(t.Name);
            if (t.ResolutionScope.Kind == HandleKind.TypeReference)
            {
                return GetTypeFromReference(reader, (TypeReferenceHandle)t.ResolutionScope, rawTypeKind) + "+" + name;
            }
            string ns = reader.GetString(t.Namespace);
            return ns.Length > 0 ? ns + "." + name : name;
        }

        public string GetTypeFromSpecification(MetadataReader reader, object context, TypeSpecificationHandle handle, byte rawTypeKind)
            => reader.GetTypeSpecification(handle).DecodeSignature(this, context);

        public string GetSZArrayType(string elementType) => elementType + "[]";
        public string GetArrayType(string elementType, ArrayShape shape) => elementType + "[" + new string(',', shape.Rank - 1) + "]";
        public string GetByReferenceType(string elementType) => elementType + "&";
        public string GetPointerType(string elementType) => elementType + "*";
        public string GetPinnedType(string elementType) => elementType;
        public string GetModifiedType(string modifier, string unmodifiedType, bool isRequired) => unmodifiedType;
        public string GetGenericInstantiation(string genericType, ImmutableArray<string> typeArguments) => genericType + "<" + string.Join(",", typeArguments) + ">";
        public string GetGenericTypeParameter(object genericContext, int index) => "!" + index;
        public string GetGenericMethodParameter(object genericContext, int index) => "!!" + index;
        public string GetFunctionPointerType(MethodSignature<string> signature) => "method pointer";
    }

    // ---------------------------------------------------------------- the sources

    // The guard's own files, which alone may name what the old game lacked.
    private static readonly string[] GuardFiles = { @"Core\GameMembers.cs", @"Core\GameConfirm.cs", @"Core\GameBuild.cs", @"Core\GameBuild.Notice.cs" };

    private static readonly (Regex Pattern, string What)[] Lacked =
    {
        (new Regex(@"\bFuneralHelper\b"), "Helpers.FuneralHelper (GameMembers.PlayerDead, GameMembers.FuneralActive)"),
        (new Regex(@"\blocatedInsidePrivateFenceIndex\b"), "Building.locatedInsidePrivateFenceIndex (GameMembers.Fenced)"),
        (new Regex(@"\.\s*TargetController\b"), "BuildingResume.TargetController (GameMembers.TargetOf)"),
        (new Regex(@"\.\s*GetNameKey\s*\("), "SubwayStation.GetNameKey (GameMembers.NameKey)"),
        (new Regex(@"\bHudConfirm\s*\.\s*Show\b"), "HudConfirm.Show (GameConfirm.Show)"),
    };

    private static void NamedOnlyInTheGuard(string scripts)
    {
        if (!Directory.Exists(scripts))
        {
            Check(false, "sources: the mod's scripts are not at " + scripts);
            return;
        }
        int files = 0, hits = 0;
        foreach (string file in Directory.GetFiles(scripts, "*.cs", SearchOption.AllDirectories))
        {
            string relative = Path.GetRelativePath(scripts, file);
            if (Array.IndexOf(GuardFiles, relative) >= 0)
            {
                continue;
            }
            files++;
            string[] lines = Code(File.ReadAllText(file)).Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                foreach (var lacked in Lacked)
                {
                    if (lacked.Pattern.IsMatch(lines[i]))
                    {
                        hits++;
                        Check(false, "sources: " + relative + ":" + (i + 1) + " names " + lacked.What + " directly: a game without it could not compile that method; use the guard's member");
                    }
                }
            }
        }
        Check(files > 500, "sources: the mod's scripts were read (" + files + " files)");
        Console.WriteLine("Sources: " + files + " files, the members an older game lacked named outside the guard " + hits + " times.");
        string members = File.ReadAllText(Path.Combine(scripts, @"Core\GameMembers.cs"));
        string confirm = File.ReadAllText(Path.Combine(scripts, @"Core\GameConfirm.cs"));
        foreach (var lacked in Lacked)
        {
            Check(lacked.Pattern.IsMatch(Code(members)) || lacked.Pattern.IsMatch(Code(confirm)), "sources: the guard itself still names " + lacked.What);
        }
    }

    // The text of a C# file with its comments blanked out (strings and characters kept, so that "//" in one is not a comment).
    private static string Code(string text)
    {
        var code = new StringBuilder(text.Length);
        int i = 0;
        while (i < text.Length)
        {
            char c = text[i];
            if (c == '/' && i + 1 < text.Length && text[i + 1] == '/')
            {
                while (i < text.Length && text[i] != '\n')
                {
                    i++;
                }
                continue;
            }
            if (c == '/' && i + 1 < text.Length && text[i + 1] == '*')
            {
                i += 2;
                while (i + 1 < text.Length && !(text[i] == '*' && text[i + 1] == '/'))
                {
                    code.Append(text[i] == '\n' ? '\n' : ' ');
                    i++;
                }
                i += 2;
                continue;
            }
            if (c == '"' || c == '\'')
            {
                bool verbatim = c == '"' && i > 0 && text[i - 1] == '@';
                code.Append(c);
                i++;
                while (i < text.Length && text[i] != c && (verbatim || text[i] != '\n'))
                {
                    if (!verbatim && text[i] == '\\' && i + 1 < text.Length)
                    {
                        code.Append(text[i]).Append(text[i + 1]);
                        i += 2;
                        continue;
                    }
                    if (verbatim && text[i] == '"' && i + 1 < text.Length && text[i + 1] == '"')
                    {
                        code.Append("\"\"");
                        i += 2;
                        continue;
                    }
                    code.Append(text[i]);
                    i++;
                }
                if (i < text.Length)
                {
                    code.Append(text[i]);
                    i++;
                }
                continue;
            }
            code.Append(c);
            i++;
        }
        return code.ToString();
    }

    // The entry points the engine calls whose catch stands the part down for the session on a missing member (instead
    // of failing every frame or tick), each as the file it is in. Others stand down on any fault already.
    private static readonly string[] Guarded =
    {
        @"Camera\ThirdPersonCamera.cs", @"Careers\CareersDirector.Prompt.cs", @"Core\MapPlaces.cs", @"Core\SpecialBuildings.cs",
        @"People\BeatPatrol.cs", @"People\PolicePresence.cs", @"Life\LifeEvents.cs", @"Traffic\TrafficAccidents.cs",
        @"Traffic\Reckless\RecklessDrivers.cs", @"Crime\CrimeDirector.cs", @"Law\LawDirector.cs", @"Ragdoll\PedestrianIncidents.cs",
    };

    private static void EntryPointsGuarded(string scripts)
    {
        foreach (string file in Guarded)
        {
            string path = Path.Combine(scripts, file);
            Check(File.Exists(path) && Code(File.ReadAllText(path)).Contains("GameMembers.StandsDown("), "guard: " + file + " still stands down on a missing member (GameMembers.StandsDown)");
        }
        string apps = Path.Combine(scripts, @"Core\GameUi\PhoneApps.cs");
        string open = File.Exists(apps) ? Code(File.ReadAllText(apps)) : string.Empty;
        Check(open.Contains("GameConfirm.TryShow(") && open.Contains("Unopened("), "guard: an app's opening asks through GameConfirm and says once why an app did not open");
    }
}
