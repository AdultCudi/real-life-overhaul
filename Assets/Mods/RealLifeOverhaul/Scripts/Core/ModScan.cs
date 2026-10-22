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

        /// <summary>The mod's own never-inlined methods that name it, whose callers catch its absence (as Core/GameMembers').</summary>
        public readonly List<string> Isolated = new List<string>();

        /// <summary>The Meta/FeatureList features that stand on it where it is not isolated, sorted.</summary>
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
    /// The mod's compiled references, checked as it starts. Under Mono a method naming a type or member the running game
    /// lacks throws every time it is called. This reads the mod's assembly (Core/ModImage) and asks the game once for
    /// every game and Unity type and member its code names, by metadata token as the code will (<see cref="Loaded"/>), so
    /// Core/Guard can switch off exactly the features that use what is missing before they run. A piece named only in a
    /// never-inlined method whose caller does without it (Core/GameMembers) is contained: nothing is switched off. A
    /// class's feature is its FrameGuard's, else Core/FeatureOwners', else that of the classes that use it.
    /// COST: once at game start, about five thousand questions (some twenty milliseconds); users are traced only when
    /// something is missing. Game-free: ContractCheck runs it against the game on disk and against an older build.
    /// </summary>
    public static partial class ModScan
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

        /// <summary>Asks the game for every reference of the mod's (<paramref name="asked"/> of them); the missing ones, with their users.</summary>
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
                    continue;   // not the game's, or its type is missing already
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

        /// <summary>
        /// The running game's answers, each reference resolved by the mod's metadata token as its code will be compiled,
        /// with no reflection object made. Null when the game has it, else the runtime's message, which names it.
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
