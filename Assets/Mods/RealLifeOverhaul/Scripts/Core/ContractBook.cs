using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace RealLifeOverhaul
{
    /// <summary>What a member of the contract is: a type the mod looks for by name, or a field, property or method of one.</summary>
    public enum GameMemberKind
    {
        Type,
        Field,
        Property,
        Method
    }

    /// <summary>
    /// One part of the mod that stands on pieces of the game that are not the game's modding interface (a private
    /// field, a method read by name). When one of its pieces is missing or has changed after a game update, the part
    /// is off for the session and says so once; every other part runs. <see cref="Feature"/> is the id of its feature in
    /// Meta/FeatureList, for the options page; the name players read is the locale key <see cref="NameKey"/>.
    /// </summary>
    public sealed class GamePart
    {
        public const string KeyPrefix = "reallife" + "_contract_part_";   // (apart, so that the locale check does not take a prefix for a key)

        public readonly string Id;
        public readonly string Feature;
        public readonly string English;   // for Player.log and the owner's checker

        internal GamePart(string id, string feature, string english)
        {
            Id = id;
            Feature = feature;
            English = english;
        }

        public string NameKey => KeyPrefix + Id;

        private bool _off;
        private string _why;

        /// <summary>True when a piece of the game it needs is missing or has changed: the part does nothing this session.</summary>
        public bool Off
        {
            get
            {
                ContractBook.EnsureResolved();
                return _off;
            }
            internal set => _off = value;
        }

        /// <summary>What was missing or changed, in English, or null while the part is on.</summary>
        public string Why
        {
            get
            {
                ContractBook.EnsureResolved();
                return _why;
            }
            internal set => _why = value;
        }
    }

    /// <summary>
    /// One piece of the game the mod reaches by name. Resolved once, when the mod starts (<see cref="ContractBook.Resolve"/>);
    /// the code that uses it reads <see cref="Field"/>, <see cref="Property"/>, <see cref="Method"/> or <see cref="Type"/>,
    /// which are null when the piece is missing or not what the mod expects. Nothing is looked up again after that.
    /// </summary>
    public sealed class GameMember
    {
        public readonly GameMemberKind Kind;
        public readonly string Assembly;     // the game's assembly, by its simple name ("BigAmbitions")
        public readonly string TypeName;     // full name, a nested type after '+'
        public readonly string Name;         // the member's name (empty for a type)
        public readonly bool Static;
        public readonly string ValueType;    // a field's or property's type, or a method's return type, as ContractBook.TypeText writes it; null: any
        public readonly string[] Parameters; // a method's parameter types, in order, as TypeText writes them
        public readonly GamePart[] Parts;    // what stands on it

        internal bool NeedsSetter;

        /// <summary>The code has another way when it is gone: said in the log, nothing is switched off.</summary>
        public bool Optional { get; private set; }

        internal void MakeOptional()
        {
            Optional = true;
        }

        internal GameMember(GameMemberKind kind, string assembly, string typeName, string name, bool isStatic, string valueType, string[] parameters,
            bool optional, GamePart[] parts, bool register = true)
        {
            Kind = kind;
            Assembly = assembly;
            TypeName = typeName;
            Name = name ?? string.Empty;
            Static = isStatic;
            ValueType = valueType;
            Parameters = parameters;
            Optional = optional;   // (a line of the table makes it optional with MakeOptional)
            Parts = parts ?? new GamePart[0];
            if (register)
            {
                ContractBook.Add(this);   // (ContractCheck makes members of its own that are not in the table)
            }
        }

        private Type _type;
        private FieldInfo _field;
        private PropertyInfo _property;
        private MethodInfo _method;
        private MethodInfo _setter;
        private bool _ok;
        private string _problem;

        // Each is resolved before it is first read: a class that reads its piece in a static initialiser before the
        // mod's start has resolved the contract still gets it (one test of a flag after that).
        public Type Type
        {
            get
            {
                ContractBook.EnsureResolved();
                return _type;
            }
        }

        public FieldInfo Field
        {
            get
            {
                ContractBook.EnsureResolved();
                return _field;
            }
        }

        public PropertyInfo Property
        {
            get
            {
                ContractBook.EnsureResolved();
                return _property;
            }
        }

        public MethodInfo Method
        {
            get
            {
                ContractBook.EnsureResolved();
                return _method;
            }
        }

        /// <summary>The property's setter, private ones too (for a member made with NeedsSetter).</summary>
        public MethodInfo Setter
        {
            get
            {
                ContractBook.EnsureResolved();
                return _setter;
            }
        }

        /// <summary>True when it was found as expected.</summary>
        public bool Ok
        {
            get
            {
                ContractBook.EnsureResolved();
                return _ok;
            }
        }

        /// <summary>Missing, or not what the mod expects, in English; null when <see cref="Ok"/>.</summary>
        public string Problem
        {
            get
            {
                ContractBook.EnsureResolved();
                return _problem;
            }
        }

        /// <summary>"Type.Member", as the log and the checker name it.</summary>
        public string Label
        {
            get
            {
                int dot = TypeName.LastIndexOfAny(new[] { '.', '+' });
                string type = dot >= 0 ? TypeName.Substring(dot + 1) : TypeName;
                return Kind == GameMemberKind.Type ? type : type + "." + Name;
            }
        }

        internal void Set(Type type, MemberInfo info, string problem)
        {
            _problem = problem;
            _ok = problem == null;
            _type = _ok || Kind != GameMemberKind.Type ? type : null;   // (for a missing member the type may still be there)
            _field = _ok ? info as FieldInfo : null;
            _property = _ok ? info as PropertyInfo : null;
            _method = _ok ? info as MethodInfo : null;
            _setter = _property != null ? _property.GetSetMethod(true) : null;
        }
    }

    /// <summary>
    /// The register behind <see cref="GameContract"/>: every piece of the game the mod reaches by name, which parts
    /// stand on it, and the one resolution of all of them when the mod starts. A piece that is missing or has changed
    /// switches off only the parts that stand on it (<see cref="GamePart.Off"/>); each such part is named once in
    /// Player.log by the caller (Core/Guard), the rest of the mod runs.
    ///
    /// No Unity or game types: the owner's checker (RealLifeOverhaul.Build/ContractCheck) compiles this file and the
    /// table, checks every member against the game's assemblies on disk, and tests the switching off with types of
    /// its own. Cost: one pass over about seventy members at start; nothing after that.
    /// </summary>
    public static class ContractBook
    {
        /// <summary>The game's build this version of the mod was checked against with ContractCheck (GameVersion.buildNumber).</summary>
        public const int CheckedBuild = 3682;

        private static List<GameMember> _members;
        private static List<GamePart> _parts;
        private static bool _resolved;

        public static IReadOnlyList<GameMember> Members
        {
            get
            {
                GameContract.Touch();
                return _members;
            }
        }

        public static IReadOnlyList<GamePart> Parts
        {
            get
            {
                GameContract.Touch();
                return _parts;
            }
        }

        public static bool Resolved => _resolved;

        /// <summary>Resolves the contract among the loaded assemblies if nothing has yet (a piece read before the mod's start).</summary>
        public static void EnsureResolved()
        {
            if (!_resolved && !_resolving)
            {
                Resolve(FindLoaded);
            }
        }

        private static bool _resolving;

        internal static void Add(GameMember member)
        {
            (_members ?? (_members = new List<GameMember>(96))).Add(member);
        }

        internal static GamePart Part(string id, string feature, string english)
        {
            var part = new GamePart(id, feature, english);
            (_parts ?? (_parts = new List<GamePart>(48))).Add(part);
            return part;
        }

        public static GamePart FindPart(string id)
        {
            foreach (GamePart part in Parts)
            {
                if (part.Id == id)
                {
                    return part;
                }
            }
            return null;
        }

        /// <summary>
        /// Looks every member up once, with the given way of finding a type (an assembly's simple name and the type's
        /// full name; null when it is not there), and switches off the parts that stand on what is missing. Safe to call
        /// again: each call starts afresh.
        /// </summary>
        public static void Resolve(Func<string, string, Type> findType)
        {
            Apply(member =>
            {
                Type type = null;
                MemberInfo info = null;
                string problem;
                try
                {
                    type = findType(member.Assembly, member.TypeName);
                    problem = type == null ? "the type " + member.TypeName + " is not in " + member.Assembly : Check(member, type, out info);
                }
                catch (Exception e)
                {
                    problem = "it could not be read (" + e.GetType().Name + ": " + e.Message + ")";
                }
                member.Set(type, info, problem);
                return problem;
            });
        }

        /// <summary>
        /// The same switching off from problems found another way (ContractCheck reads the game's assemblies on disk):
        /// <paramref name="problemOf"/> answers null for a member that is there as expected. Nothing is resolved.
        /// </summary>
        public static void Mark(Func<GameMember, string> problemOf)
        {
            Apply(member =>
            {
                string problem = problemOf(member);
                member.Set(null, null, problem);
                return problem;
            });
        }

        // Every member's problem (null: there as expected) switches off the parts that stand on it, unless it is optional.
        private static void Apply(Func<GameMember, string> problemOf)
        {
            _resolving = true;
            try
            {
                ApplyAll(problemOf);
            }
            finally
            {
                _resolving = false;
            }
        }

        private static void ApplyAll(Func<GameMember, string> problemOf)
        {
            GameContract.Touch();
            foreach (GamePart part in _parts)
            {
                part.Off = false;
                part.Why = null;
            }
            foreach (GameMember member in _members)
            {
                string problem = problemOf(member);
                if (problem == null || member.Optional)
                {
                    continue;
                }
                foreach (GamePart part in member.Parts)
                {
                    if (!part.Off)
                    {
                        part.Off = true;
                        part.Why = member.Label + ": " + problem;
                    }
                }
            }
            _resolved = true;
        }

        /// <summary>The usual way of finding a type: among the assemblies loaded in this process.</summary>
        public static Type FindLoaded(string assembly, string typeName)
        {
            foreach (Assembly a in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (a.GetName().Name == assembly)
                {
                    Type t = a.GetType(typeName, false);
                    if (t != null)
                    {
                        return t;
                    }
                }
            }
            return null;
        }

        private const BindingFlags Declared = BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

        /// <summary>Null when the member is there on the given type as expected; otherwise what is wrong with it, in English.</summary>
        internal static string Check(GameMember m, Type type, out MemberInfo info)
        {
            info = null;
            switch (m.Kind)
            {
                case GameMemberKind.Type:
                    info = type;
                    return null;
                case GameMemberKind.Field:
                    for (Type t = type; t != null && info == null; t = t.BaseType)
                    {
                        info = t.GetField(m.Name, Declared);
                    }
                    if (info == null)
                    {
                        return "the field is missing";
                    }
                    var field = (FieldInfo)info;
                    if (field.IsStatic != m.Static)
                    {
                        return field.IsStatic ? "the field is now static" : "the field is no longer static";
                    }
                    return Same("the field's type", TypeText(field.FieldType), m.ValueType);
                case GameMemberKind.Property:
                    for (Type t = type; t != null && info == null; t = t.BaseType)
                    {
                        info = t.GetProperty(m.Name, Declared);
                    }
                    if (info == null)
                    {
                        return "the property is missing";
                    }
                    var property = (PropertyInfo)info;
                    MethodInfo get = property.GetGetMethod(true);
                    MethodInfo set = property.GetSetMethod(true);
                    MethodInfo either = get ?? set;
                    if (either == null || either.IsStatic != m.Static)
                    {
                        return either != null && either.IsStatic ? "the property is now static" : "the property is no longer static";
                    }
                    if (m.NeedsSetter && set == null)
                    {
                        return "the property can no longer be set";
                    }
                    return Same("the property's type", TypeText(property.PropertyType), m.ValueType);
                default:
                    MethodInfo found = null;
                    string near = null;
                    for (Type t = type; t != null && found == null; t = t.BaseType)
                    {
                        foreach (MethodInfo candidate in t.GetMethods(Declared))
                        {
                            if (candidate.Name != m.Name || candidate.IsStatic != m.Static)
                            {
                                continue;
                            }
                            string shape = Shape(candidate);
                            if (m.Parameters == null || shape == Join(m.Parameters))
                            {
                                found = candidate;
                                break;
                            }
                            near = near ?? shape;
                        }
                    }
                    if (found == null)
                    {
                        return near != null ? "the method now takes (" + near + "), the mod expects (" + Join(m.Parameters) + ")" : "the method is missing";
                    }
                    info = found;
                    return Same("the method's return type", TypeText(found.ReturnType), m.ValueType);
            }
        }

        private static string Same(string what, string actual, string expected)
        {
            return expected == null || actual == expected ? null : what + " is now " + actual + ", the mod expects " + expected;
        }

        private static string Shape(MethodInfo method)
        {
            ParameterInfo[] parameters = method.GetParameters();
            var types = new string[parameters.Length];
            for (int i = 0; i < parameters.Length; i++)
            {
                types[i] = TypeText(parameters[i].ParameterType);
            }
            return Join(types);
        }

        internal static string Join(string[] types)
        {
            return types == null ? string.Empty : string.Join(",", types);
        }

        /// <summary>
        /// A type as the contract writes it: its name without namespace or generic arity, a nested type after its outer
        /// one's name and a dot, generic arguments in angle brackets without spaces, "[]" for an array and "&amp;" for a
        /// reference ("Dictionary&lt;String,Toggle&gt;", "List&lt;SmartPhoneFrame.FrameData&gt;"). ContractCheck writes a
        /// type read from the game's files on disk the same way.
        /// </summary>
        public static string TypeText(Type t)
        {
            if (t == null)
            {
                return "?";
            }
            if (t.IsByRef)
            {
                return TypeText(t.GetElementType()) + "&";
            }
            if (t.IsPointer)
            {
                return TypeText(t.GetElementType()) + "*";
            }
            if (t.IsArray)
            {
                int rank = t.GetArrayRank();
                return TypeText(t.GetElementType()) + "[" + new string(',', rank - 1) + "]";
            }
            if (t.IsGenericParameter)
            {
                return t.Name;
            }
            var text = new StringBuilder(32);
            if (t.IsNested && t.DeclaringType != null)
            {
                text.Append(Bare(t.DeclaringType.Name)).Append('.');
            }
            text.Append(Bare(t.Name));
            if (t.IsGenericType)
            {
                Type[] args = t.GetGenericArguments();
                text.Append('<');
                for (int i = 0; i < args.Length; i++)
                {
                    text.Append(i > 0 ? "," : string.Empty).Append(TypeText(args[i]));
                }
                text.Append('>');
            }
            return text.ToString();
        }

        internal static string Bare(string name)
        {
            int tick = name.IndexOf('`');
            return tick >= 0 ? name.Substring(0, tick) : name;
        }

        /// <summary>The parts that are off, in the table's order.</summary>
        public static List<GamePart> OffParts()
        {
            var off = new List<GamePart>();
            foreach (GamePart part in Parts)
            {
                if (part.Off)
                {
                    off.Add(part);
                }
            }
            return off;
        }

        /// <summary>The members that were not found as expected, optional ones included.</summary>
        public static List<GameMember> Problems()
        {
            var bad = new List<GameMember>();
            foreach (GameMember member in Members)
            {
                if (!member.Ok)
                {
                    bad.Add(member);
                }
            }
            return bad;
        }

        /// <summary>The features (Meta/FeatureList ids) with at least one part off.</summary>
        public static HashSet<string> FeaturesHit()
        {
            var hit = new HashSet<string>(StringComparer.Ordinal);
            foreach (GamePart part in Parts)
            {
                if (part.Off && part.Feature != null)
                {
                    hit.Add(part.Feature);
                }
            }
            return hit;
        }
    }
}
