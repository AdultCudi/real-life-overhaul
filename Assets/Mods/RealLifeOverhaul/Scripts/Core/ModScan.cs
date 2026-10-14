using System;
using System.Collections.Generic;
using System.Reflection;

namespace RealLifeOverhaul
{
    /// <summary>What a game is asked about one reference of the mod's: null when it has it, else what is wrong, in English.</summary>
    public interface IGameAnswers
    {
        /// <summary>A type reference (a TypeRef token).</summary>
        string Type(int token);

        /// <summary>A member reference (a MemberRef token), a field's or a method's.</summary>
        string Member(int token, bool field);
    }

    /// <summary>One reference of the mod's compiled code that the game does not have as the mod was built against it.</summary>
    public sealed class MissingReference
    {
        public int Token;
        public string Assembly;   // the game's assembly the type (or the member's type) is in
        public string Name;       // "Helpers.FuneralHelper", "Buildings.Building.locatedInsidePrivateFenceIndex", "HudConfirm.Show"
        public string Kind;       // "type", "field" or "method"
        public string Problem;    // what the game said of it

        /// <summary>The mod's classes (outermost) whose code names it, sorted.</summary>
        public readonly List<string> Users = new List<string>();

        /// <summary>The mod's methods of their own that name it and whose callers catch its absence (never inlined: Core/GameMembers' kind).</summary>
        public readonly List<string> Isolated = new List<string>();

        /// <summary>The features of Meta/FeatureList that stand on it where it is not isolated, sorted.</summary>
        public readonly List<string> Features = new List<string>();

        /// <summary>True when some of it stands where no feature is known: the mod's own machinery.</summary>
        public bool Machinery;

        /// <summary>True when every method of the mod that names it is one of its own, whose callers do without it.</summary>
        public bool Contained => Users.Count == 0 && Isolated.Count > 0;

        public override string ToString()
        {
            return Kind + " " + Name + " (" + Assembly + ")";
        }
    }

    /// <summary>
    /// THE MOD'S COMPILED REFERENCES, CHECKED AS IT STARTS. Under Mono a method of the mod that names a type, field or
    /// method the running game does not have (an older build, or an update that took it away) cannot be compiled: it
    /// throws every time it is called, and everything else it does is lost. This reads the mod's own assembly
    /// (Core/ModImage) and asks the running game, once, for every type and member of the game's and Unity's assemblies
    /// the mod's code names, the way the code will ask for them (<see cref="Loaded"/>: by the mod's own metadata tokens,
    /// through ModuleHandle, so that no reflection object is made). What the game cannot give is found before anything
    /// that uses it runs, with the classes of the mod that use it and the features they belong to, so that Core/Guard
    /// switches off exactly those features and says each piece once.
    ///
    /// A method that names such a piece only in a method of its own that is never inlined, whose caller catches the
    /// failure and does without it (Core/GameMembers, Core/GameConfirm, an app's first steps in Core/GameUi/PhoneApps), is
    /// contained: nothing is switched off for it (ContractCheck holds every such method to being called inside a try).
    /// The feature of a class is its FrameGuard's (read from its static initialiser), else Core/FeatureOwners', else that
    /// of the classes that use it, found through the mod's code.
    ///
    /// Cost: once, at game start, about five thousand questions to the game (some twenty milliseconds); the classes and
    /// features of a missing piece are found only when one is missing (the whole file is then read once). No Unity or
    /// game types: ContractCheck runs it against the game's assemblies on disk, and against a game that lacks what a
    /// player's older build lacked.
    /// </summary>
    public static class ModScan
    {
        /// <summary>True for an assembly of the game's or Unity's: any the mod names but .NET's own.</summary>
        public static bool IsGameAssembly(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return false;
            }
            return !(name == "mscorlib" || name == "netstandard" || name == "System" || name.StartsWith("System.", StringComparison.Ordinal)
                     || name.StartsWith("Microsoft.", StringComparison.Ordinal));
        }

        /// <summary>
        /// Every reference of the mod to the game asked of it (<paramref name="asked"/> says how many); the missing ones,
        /// each with the classes and features that stand on it.
        /// </summary>
        public static List<MissingReference> Run(ModImage image, IGameAnswers game, out int asked)
        {
            var missing = new List<MissingReference>();
            var broken = new Dictionary<int, MissingReference>();
            asked = 0;

            // The game's types the mod names.
            var gameType = new bool[image.Rows(ModImage.TypeRef) + 1];
            for (int row = 1; row <= image.Rows(ModImage.TypeRef); row++)
            {
                string assembly = image.TypeRefAssembly(row);
                if (!IsGameAssembly(assembly))
                {
                    continue;
                }
                gameType[row] = true;
                asked++;
                int token = ModImage.Token(ModImage.TypeRef, row);
                string problem = game.Type(token);
                if (problem != null)
                {
                    var m = new MissingReference { Token = token, Assembly = assembly, Name = image.TypeRefFullName(row), Kind = "type", Problem = problem };
                    missing.Add(m);
                    broken[token] = m;
                }
            }

            // Their members (on a type of the game's, or on an instance of a generic type of the game's).
            for (int row = 1; row <= image.Rows(ModImage.MemberRef); row++)
            {
                int parent = image.MemberRefParent(row);
                int owner = 0;
                if (ModImage.TableOf(parent) == ModImage.TypeRef)
                {
                    owner = ModImage.RowOf(parent);
                }
                else if (ModImage.TableOf(parent) == ModImage.TypeSpec && !image.TypeSpecIsOpen(ModImage.RowOf(parent)))
                {
                    int generic = image.TypeSpecGeneric(ModImage.RowOf(parent));
                    owner = ModImage.TableOf(generic) == ModImage.TypeRef ? ModImage.RowOf(generic) : 0;
                }
                if (owner <= 0 || !gameType[owner] || broken.ContainsKey(ModImage.Token(ModImage.TypeRef, owner)))
                {
                    continue;   // (not the game's, or its type is missing already: said as the type)
                }
                bool field = image.MemberRefIsField(row);
                int token = ModImage.Token(ModImage.MemberRef, row);
                asked++;
                string problem = game.Member(token, field);
                if (problem != null)
                {
                    var m = new MissingReference
                    {
                        Token = token, Assembly = image.TypeRefAssembly(owner), Name = image.TypeRefFullName(owner) + "." + image.MemberRefName(row),
                        Kind = field ? "field" : "method", Problem = problem
                    };
                    missing.Add(m);
                    broken[token] = m;
                }
            }
            if (missing.Count == 0)
            {
                return missing;
            }
            Users(image, broken);
            return missing;
        }

        // ---------------------------------------------------------------- who uses what is missing

        private static void Users(ModImage image, Dictionary<int, MissingReference> broken)
        {
            // What else stands on a missing type: an instance of a generic type that names one, a generic method's instance,
            // a member of either, a stand-alone signature (a method's locals).
            var types = new List<int>(8);
            var derived = new Dictionary<int, MissingReference>();
            for (int pass = 0; pass < 4; pass++)
            {
                for (int row = 1; row <= image.Rows(ModImage.TypeSpec); row++)
                {
                    int token = ModImage.Token(ModImage.TypeSpec, row);
                    if (broken.ContainsKey(token) || derived.ContainsKey(token))
                    {
                        continue;
                    }
                    types.Clear();
                    image.TypesIn(image.TypeSpecSignature(row), BlobKind.Spec, types);
                    MissingReference by = Any(types, broken, derived);
                    if (by != null)
                    {
                        derived[token] = by;
                    }
                }
            }
            for (int row = 1; row <= image.Rows(ModImage.MemberRef); row++)
            {
                int token = ModImage.Token(ModImage.MemberRef, row);
                if (broken.ContainsKey(token))
                {
                    continue;
                }
                types.Clear();
                types.Add(image.MemberRefParent(row));
                image.TypesIn(image.MemberRefSignature(row), BlobKind.Member, types);
                MissingReference by = Any(types, broken, derived);
                if (by != null)
                {
                    derived[token] = by;
                }
            }
            for (int row = 1; row <= image.Rows(ModImage.MethodSpec); row++)
            {
                types.Clear();
                types.Add(image.MethodSpecMethod(row));
                image.TypesIn(image.MethodSpecInstantiation(row), BlobKind.Instance, types);
                MissingReference by = Any(types, broken, derived);
                if (by != null)
                {
                    derived[ModImage.Token(ModImage.MethodSpec, row)] = by;
                }
            }
            for (int row = 1; row <= image.Rows(ModImage.StandAloneSig); row++)
            {
                types.Clear();
                image.TypesIn(image.StandAloneSigSignature(row), BlobKind.StandAlone, types);
                MissingReference by = Any(types, broken, derived);
                if (by != null)
                {
                    derived[ModImage.Token(ModImage.StandAloneSig, row)] = by;
                }
            }

            image.LoadCode();
            try
            {
                Place(image, broken, derived);
            }
            finally
            {
                image.DropCode();
            }
        }

        private static MissingReference Any(List<int> tokens, Dictionary<int, MissingReference> broken, Dictionary<int, MissingReference> derived)
        {
            foreach (int t in tokens)
            {
                if (broken.TryGetValue(t, out MissingReference m) || derived.TryGetValue(t, out m))
                {
                    return m;
                }
            }
            return null;
        }

        // The mod's code and who calls whom: each method's class, each field's, the class each class is nested in, for
        // each method the methods whose code names it (a call, a delegate made of it, a generic instance of it), and for
        // each class the classes whose code names any of it (outermost classes, a lambda's or an iterator's being the
        // class it is written in). Read once, from the method bodies, only when something is missing.
        private sealed class Graph
        {
            public readonly ModImage Image;
            public readonly int[] Outer, MethodOwner, FieldOwner;
            public readonly List<int>[] Callers;
            public readonly HashSet<int>[] UsedBy;
            public readonly Dictionary<int, string> Feature = new Dictionary<int, string>();   // class: its feature; "" for the mod's machinery; absent while unknown
            private readonly List<int> _targets = new List<int>(4);

            public Graph(ModImage image)
            {
                Image = image;
                Outer = image.Enclosing();
                MethodOwner = image.MethodOwners();
                FieldOwner = image.FieldOwners();
                Callers = new List<int>[image.Rows(ModImage.MethodDef) + 1];
                UsedBy = new HashSet<int>[image.Rows(ModImage.TypeDef) + 1];
            }

            public int ClassOfMethod(int method)
            {
                return Outermost(Outer, MethodOwner[method]);
            }

            // Each method's body read once: its tokens to the visitor, the methods and classes they name to the graph.
            public void Read(Action<int, int, List<int>, int> visit)
            {
                var tokens = new List<int>(256);
                for (int m = 1; m <= Image.Rows(ModImage.MethodDef); m++)
                {
                    int cls = ClassOfMethod(m);
                    tokens.Clear();
                    bool body = Image.BodyTokens(m, tokens, out int locals);
                    visit?.Invoke(m, cls, tokens, body ? locals : 0);
                    foreach (int token in tokens)
                    {
                        int target = ClassOf(Image, token, Outer, MethodOwner, FieldOwner);
                        if (target > 0 && target != cls)
                        {
                            (UsedBy[target] ?? (UsedBy[target] = new HashSet<int>())).Add(cls);
                        }
                        _targets.Clear();
                        Methods(token, _targets);
                        foreach (int callee in _targets)
                        {
                            if (callee != m)
                            {
                                (Callers[callee] ?? (Callers[callee] = new List<int>(2))).Add(m);
                            }
                        }
                    }
                }
            }

            // The methods of the mod a token of its code names: the method itself, a generic instance's method, or a member
            // reference to a generic type of the mod's (each of its methods of that name).
            private void Methods(int token, List<int> into)
            {
                int row = ModImage.RowOf(token);
                switch (ModImage.TableOf(token))
                {
                    case ModImage.MethodDef:
                        if (row < MethodOwner.Length)
                        {
                            into.Add(row);
                        }
                        return;
                    case ModImage.MethodSpec:
                        if (row <= Image.Rows(ModImage.MethodSpec))
                        {
                            Methods(Image.MethodSpecMethod(row), into);
                        }
                        return;
                    case ModImage.MemberRef:
                        if (row > Image.Rows(ModImage.MemberRef) || Image.MemberRefIsField(row))
                        {
                            return;
                        }
                        int parent = Image.MemberRefParent(row);
                        int type = ModImage.TableOf(parent) == ModImage.TypeDef ? ModImage.RowOf(parent)
                            : ModImage.TableOf(parent) == ModImage.TypeSpec && ModImage.TableOf(Image.TypeSpecGeneric(ModImage.RowOf(parent))) == ModImage.TypeDef
                                ? ModImage.RowOf(Image.TypeSpecGeneric(ModImage.RowOf(parent))) : 0;
                        if (type <= 0)
                        {
                            return;
                        }
                        string name = Image.MemberRefName(row);
                        Image.MethodsOf(type, out int first, out int end);
                        for (int m = first; m < end; m++)
                        {
                            if (Image.MethodName(m) == name)
                            {
                                into.Add(m);
                            }
                        }
                        return;
                }
            }

            /// <summary>
            /// The features that stand on a method of the mod: its class's when that is known; else those of the methods
            /// that call it, and so on up; a method nothing of the mod calls (the engine's, the game's, a virtual or an
            /// interface's call) stands for its whole class, found through the classes that use it.
            /// </summary>
            public void ReachMethod(int start, HashSet<string> features, ref bool machinery)
            {
                var seen = new HashSet<int> { start };
                var methods = new Queue<int>();
                var classes = new List<int>();
                methods.Enqueue(start);
                while (methods.Count > 0 && seen.Count < 8192)
                {
                    int m = methods.Dequeue();
                    int cls = ClassOfMethod(m);
                    string known = FeatureOfClass(cls);
                    if (known != null)
                    {
                        Collect(known, features, ref machinery);
                        continue;
                    }
                    List<int> callers = Callers[m];
                    if (callers == null || callers.Count == 0)
                    {
                        if (!classes.Contains(cls))
                        {
                            classes.Add(cls);
                        }
                        continue;
                    }
                    foreach (int caller in callers)
                    {
                        if (seen.Add(caller))
                        {
                            methods.Enqueue(caller);
                        }
                    }
                }
                foreach (int cls in classes)
                {
                    ReachClass(cls, features, ref machinery);
                }
            }

            /// <summary>The features that stand on a class of the mod: its own, else those of the classes that use it, as far as one is known.</summary>
            public void ReachClass(int start, HashSet<string> features, ref bool machinery)
            {
                var seen = new HashSet<int> { start };
                var queue = new Queue<int>();
                queue.Enqueue(start);
                while (queue.Count > 0 && seen.Count < 4096)
                {
                    int cls = queue.Dequeue();
                    string known = FeatureOfClass(cls);
                    if (known != null)
                    {
                        Collect(known, features, ref machinery);
                        continue;
                    }
                    HashSet<int> users = UsedBy[cls];
                    if (users == null || users.Count == 0)
                    {
                        machinery = true;   // (used by nothing of the mod's: called by the game or the engine, and of no feature known)
                        continue;
                    }
                    foreach (int user in users)
                    {
                        if (seen.Add(user))
                        {
                            queue.Enqueue(user);
                        }
                    }
                }
            }

            private string FeatureOfClass(int cls)
            {
                if (!Feature.TryGetValue(cls, out string known))
                {
                    known = FeatureOf(Image, cls);
                    if (known != null)
                    {
                        Feature[cls] = known;
                    }
                }
                return known;
            }

            private static void Collect(string known, HashSet<string> features, ref bool machinery)
            {
                if (known.Length > 0)
                {
                    features.Add(known);
                }
                else
                {
                    machinery = true;
                }
            }
        }

        // Every method of the mod that names a missing piece (in its code, its locals or its own signature), every type of
        // the mod that cannot be loaded without one (a field of it, the type it derives from, an interface it implements),
        // and the features they serve.
        private static void Place(ModImage image, Dictionary<int, MissingReference> broken, Dictionary<int, MissingReference> derived)
        {
            var graph = new Graph(image);
            int[] outer = graph.Outer;
            var methods = new Dictionary<MissingReference, HashSet<int>>();      // the methods that name it (not isolated ones)
            var classes = new Dictionary<MissingReference, HashSet<int>>();      // the classes that cannot be loaded without it
            var isolated = new Dictionary<MissingReference, HashSet<int>>();     // the isolated methods that name it
            var signature = new List<int>(16);

            void Use(MissingReference m, int method)
            {
                Add(image.MethodNoInlining(method) ? isolated : methods, m, method);
            }

            for (int i = 1; i <= image.Rows(ModImage.InterfaceImpl); i++)
            {
                int interfaceToken = image.InterfaceImplInterface(i);
                if (broken.TryGetValue(interfaceToken, out MissingReference implemented) || derived.TryGetValue(interfaceToken, out implemented))
                {
                    Add(classes, implemented, Outermost(outer, image.InterfaceImplClass(i)));
                }
            }
            for (int t = 1; t <= image.Rows(ModImage.TypeDef); t++)
            {
                MissingReference by = null;
                int extends = image.TypeDefExtends(t);
                if (broken.TryGetValue(extends, out MissingReference e) || derived.TryGetValue(extends, out e))
                {
                    by = e;
                }
                image.FieldsOf(t, out int first, out int end);
                for (int f = first; f < end && by == null; f++)
                {
                    signature.Clear();
                    image.TypesIn(image.FieldSignature(f), BlobKind.Field, signature);
                    by = Any(signature, broken, derived);
                }
                if (by != null)
                {
                    Add(classes, by, Outermost(outer, t));
                }
            }

            graph.Read((m, cls, tokens, locals) =>
            {
                signature.Clear();
                image.TypesIn(image.MethodSignature(m), BlobKind.Method, signature);
                MissingReference by = Any(signature, broken, derived);
                if (by != null)
                {
                    Use(by, m);
                }
                if (locals != 0 && derived.TryGetValue(locals, out MissingReference l))
                {
                    Use(l, m);
                }
                foreach (int token in tokens)
                {
                    if (broken.TryGetValue(token, out MissingReference b) || derived.TryGetValue(token, out b))
                    {
                        Use(b, m);
                    }
                }
            });

            foreach (MissingReference m in broken.Values)
            {
                if (isolated.TryGetValue(m, out HashSet<int> own))
                {
                    foreach (int method in own)
                    {
                        m.Isolated.Add(TypeName(image, graph.ClassOfMethod(method)) + "." + image.MethodName(method));
                    }
                    m.Isolated.Sort(StringComparer.Ordinal);
                }
                var features = new HashSet<string>(StringComparer.Ordinal);
                var users = new HashSet<string>(StringComparer.Ordinal);
                if (methods.TryGetValue(m, out HashSet<int> named))
                {
                    foreach (int method in named)
                    {
                        users.Add(TypeName(image, graph.ClassOfMethod(method)));
                        graph.ReachMethod(method, features, ref m.Machinery);
                    }
                }
                if (classes.TryGetValue(m, out HashSet<int> lost))
                {
                    foreach (int cls in lost)
                    {
                        users.Add(TypeName(image, cls));
                        graph.ReachClass(cls, features, ref m.Machinery);
                    }
                }
                m.Users.AddRange(users);
                m.Users.Sort(StringComparer.Ordinal);
                m.Features.AddRange(features);
                m.Features.Sort(StringComparer.Ordinal);
            }
        }

        /// <summary>
        /// The features that stand on some methods of a class of the mod (by namespace, class and method names), as the
        /// running mod would find them were those methods to fail: those of the methods that call them, and so on up.
        /// <paramref name="callers"/> names the calling classes; <paramref name="machinery"/> says whether the mod's own
        /// machinery calls one too. For ContractCheck: the features a member read only in Core/GameMembers touches.
        /// </summary>
        public static List<string> FeaturesUsing(ModImage image, string ns, string type, string[] names, out List<string> callers, out bool machinery)
        {
            machinery = false;
            callers = new List<string>();
            var features = new List<string>();
            var targets = new List<int>();
            for (int t = 1; t <= image.Rows(ModImage.TypeDef); t++)
            {
                if (image.TypeDefName(t) == type && image.TypeDefNamespace(t) == ns)
                {
                    image.MethodsOf(t, out int first, out int end);
                    for (int m = first; m < end; m++)
                    {
                        if (Array.IndexOf(names, image.MethodName(m)) >= 0)
                        {
                            targets.Add(m);
                        }
                    }
                }
            }
            if (targets.Count == 0)
            {
                return features;
            }
            image.LoadCode();
            try
            {
                var graph = new Graph(image);
                graph.Read(null);
                var found = new HashSet<string>(StringComparer.Ordinal);
                var calling = new HashSet<string>(StringComparer.Ordinal);
                foreach (int target in targets)
                {
                    List<int> direct = graph.Callers[target];
                    if (direct != null)
                    {
                        foreach (int caller in direct)
                        {
                            calling.Add(TypeName(image, graph.ClassOfMethod(caller)));
                            graph.ReachMethod(caller, found, ref machinery);
                        }
                    }
                }
                callers.AddRange(calling);
                callers.Sort(StringComparer.Ordinal);
                features.AddRange(found);
                features.Sort(StringComparer.Ordinal);
            }
            finally
            {
                image.DropCode();
            }
            return features;
        }

        private static void Add(Dictionary<MissingReference, HashSet<int>> into, MissingReference m, int value)
        {
            if (!into.TryGetValue(m, out HashSet<int> set))
            {
                into[m] = set = new HashSet<int>();
            }
            set.Add(value);
        }

        private static int Outermost(int[] outer, int type)
        {
            for (int depth = 0; depth < 32 && type > 0 && outer[type] > 0; depth++)
            {
                type = outer[type];
            }
            return type;
        }

        // The class of the mod (outermost) a token of its code names, or 0.
        private static int ClassOf(ModImage image, int token, int[] outer, int[] methodOwner, int[] fieldOwner)
        {
            int row = ModImage.RowOf(token);
            switch (ModImage.TableOf(token))
            {
                case ModImage.MethodDef:
                    return row < methodOwner.Length ? Outermost(outer, methodOwner[row]) : 0;
                case ModImage.Field:
                    return row < fieldOwner.Length ? Outermost(outer, fieldOwner[row]) : 0;
                case ModImage.TypeDef:
                    return row <= image.Rows(ModImage.TypeDef) ? Outermost(outer, row) : 0;
                case ModImage.TypeSpec:
                    return row <= image.Rows(ModImage.TypeSpec) ? ClassOf(image, image.TypeSpecGeneric(row), outer, methodOwner, fieldOwner) : 0;
                case ModImage.MemberRef:
                    if (row > image.Rows(ModImage.MemberRef))
                    {
                        return 0;
                    }
                    int parent = image.MemberRefParent(row);
                    return ModImage.TableOf(parent) == ModImage.TypeDef || ModImage.TableOf(parent) == ModImage.TypeSpec ? ClassOf(image, parent, outer, methodOwner, fieldOwner) : 0;
                case ModImage.MethodSpec:
                    return row <= image.Rows(ModImage.MethodSpec) ? ClassOf(image, image.MethodSpecMethod(row), outer, methodOwner, fieldOwner) : 0;
                default:
                    return 0;
            }
        }

        /// <summary>
        /// The feature of a class of the mod (a TypeDef row): its FrameGuard's, as its static initialiser makes it
        /// (Guard.For("feature", ...), "" for null: the mod's machinery), else Core/FeatureOwners'; null while not known.
        /// </summary>
        public static string FeatureOf(ModImage image, int typeRow)
        {
            string guarded = GuardFeature(image, typeRow, out bool found);
            if (found)
            {
                return guarded ?? string.Empty;
            }
            string feature = FeatureOwners.Of(image.TypeDefName(typeRow), image.TypeDefNamespace(typeRow), out bool known);
            return known ? feature ?? string.Empty : null;
        }

        // The feature a class's FrameGuard is made with: the first argument of Guard.For in the instruction before the
        // field is set, in its static initialiser. Found is false when it has no FrameGuard, or it is not made that way.
        private static string GuardFeature(ModImage image, int typeRow, out bool found)
        {
            found = false;
            image.FieldsOf(typeRow, out int first, out int end);
            int guard = 0;
            for (int f = first; f < end; f++)
            {
                if (image.FieldIsStatic(f) && image.FieldName(f) == "FrameGuard")
                {
                    guard = f;
                    break;
                }
            }
            if (guard == 0)
            {
                return null;
            }
            image.MethodsOf(typeRow, out int mFirst, out int mEnd);
            for (int m = mFirst; m < mEnd; m++)
            {
                if (image.MethodName(m) != ".cctor")
                {
                    continue;
                }
                byte[] il = image.Code(m, out int start, out int stop);
                if (il == null)
                {
                    return null;
                }
                var ops = new List<int>(64);
                var operands = new List<int>(64);
                ModImage.Decode(il, start, stop, ops, operands);
                int set = ModImage.Token(ModImage.Field, guard);
                for (int i = 0; i < ops.Count; i++)
                {
                    if (ops[i] != 0x80 || operands[i] != set || i == 0 || ops[i - 1] != 0x28)   // stsfld FrameGuard, after a call
                    {
                        continue;
                    }
                    // The call's three arguments, read back from it: the owner (ldtoken, GetTypeFromHandle; or ldnull), the
                    // place (ldstr), the feature (ldstr, or ldnull for the mod's machinery).
                    int k = i - 2;
                    if (k >= 1 && ops[k] == 0x28 && ops[k - 1] == 0xD0)
                    {
                        k -= 2;
                    }
                    else if (k >= 0 && ops[k] == 0x14)
                    {
                        k -= 1;
                    }
                    if (k < 1 || ops[k] != 0x72)
                    {
                        return null;
                    }
                    k -= 1;
                    if (ops[k] == 0x14)
                    {
                        found = true;
                        return null;
                    }
                    if (ops[k] == 0x72)
                    {
                        string text = image.UserString(operands[k]);
                        found = text != null;
                        return text;
                    }
                    return null;
                }
            }
            return null;
        }

        /// <summary>A class of the mod's name as the log gives it: "Namespace.Class" without the mod's own root namespace.</summary>
        public static string TypeName(ModImage image, int typeRow)
        {
            string ns = image.TypeDefNamespace(typeRow);
            string name = image.TypeDefName(typeRow);
            if (ns == "RealLifeOverhaul")
            {
                return name;
            }
            if (ns.StartsWith("RealLifeOverhaul.", StringComparison.Ordinal))
            {
                ns = ns.Substring("RealLifeOverhaul.".Length);
            }
            return ns.Length > 0 ? ns + "." + name : name;
        }

        // ---------------------------------------------------------------- asking the running game

        /// <summary>
        /// The running game's answers: each reference resolved by the mod's own metadata token, as the code that names it
        /// will be compiled (ModuleHandle: no reflection object is made). Null when the game has it, else the runtime's
        /// own message (Mono names the missing type or member in it).
        /// </summary>
        public sealed class Loaded : IGameAnswers
        {
            private readonly ModuleHandle _module;

            public Loaded(Module module)
            {
                _module = module.ModuleHandle;
            }

            public string Type(int token)
            {
                try
                {
                    _module.ResolveTypeHandle(token);
                    return null;
                }
                catch (Exception e)
                {
                    return Said(e);
                }
            }

            public string Member(int token, bool field)
            {
                try
                {
                    if (field)
                    {
                        _module.ResolveFieldHandle(token);
                    }
                    else
                    {
                        _module.ResolveMethodHandle(token);
                    }
                    return null;
                }
                catch (Exception e)
                {
                    return Said(e);
                }
            }

            private static string Said(Exception e)
            {
                string message = e.Message ?? e.GetType().Name;
                message = message.Replace('\r', ' ').Replace('\n', ' ');
                return message.Length > 200 ? message.Substring(0, 200) : message;
            }
        }
    }
}
