using System;
using System.Collections.Generic;
using System.Reflection;

namespace RealLifeOverhaul
{
    /// <summary>The scan's part that finds who uses what is missing: the classes, methods and features that stand on it.</summary>
    public static partial class ModScan
    {
        private static void Users(ModImage image, Dictionary<int, MissingReference> broken)
        {
            // What else stands on a missing type: a generic instance naming one, a member of it, a stand-alone signature.
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

        // Who names whom in the mod's code: each method's and field's class, each class's outer one, each method's callers
        // and each outermost class's users. Read once from the method bodies, only when something is missing.
        private sealed class Graph
        {
            public readonly ModImage Image;
            public readonly int[] Outer, MethodOwner, FieldOwner;
            public readonly List<int>[] Callers;
            public readonly HashSet<int>[] UsedBy;
            public readonly Dictionary<int, string> Feature = new Dictionary<int, string>();   // class: its feature, "" for the machinery; absent while unknown
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

            // The methods of the mod a token names: the method, a generic instance's, or each of that name on a generic type.
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
            /// The features that stand on a method: its class's when known, else its callers', and so on up; a method nothing
            /// of the mod calls stands for its whole class.
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

            /// <summary>The features that stand on a class: its own, else those of the classes that use it.</summary>
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
                        machinery = true;   // used by nothing of the mod's and of no feature known
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

        // The methods naming a missing piece (code, locals or signature), the classes that cannot load without one (a
        // field, a base type, an interface), and the features they serve.
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
        /// For ContractCheck: the features that stand on some methods of a class, as the running mod would find them were
        /// they to fail. <paramref name="callers"/> names the calling classes; <paramref name="machinery"/>, whether the
        /// mod's own machinery calls one too.
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
        /// The feature of a class (a TypeDef row): its FrameGuard's ("" for the machinery), else Core/FeatureOwners'; null
        /// while not known.
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

        // The first argument of the Guard.For that sets FrameGuard in the static initialiser; found is false without one.
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
                    // The call's arguments read back: the owner (ldtoken and GetTypeFromHandle, or ldnull), the place (ldstr),
                    // the feature (ldstr, or ldnull for the machinery).
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

        /// <summary>A class's name as the log gives it: "Namespace.Class" without the mod's root namespace.</summary>
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
    }
}
