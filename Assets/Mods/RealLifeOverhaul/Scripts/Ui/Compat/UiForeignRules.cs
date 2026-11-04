using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text;

namespace RealLifeOverhaul.Ui
{
    /// <summary>Whose code an assembly holds, as the kit tells a fault's owner.</summary>
    public enum UiOrigin : byte
    {
        Framework = 0,   // the runtime, Unity, the text package, the mod API: never blamed
        Game = 1,        // Big Ambitions' own assemblies
        Ours = 2,        // this mod
        Other = 3        // another author's mod
    }

    /// <summary>What another mod's piece on a screen of the game's is.</summary>
    public enum UiPieceKind : byte
    {
        App = 0,       // a phone app
        Tab,           // a tab of a panel (BizMan's tab bar)
        Button,        // a button on a panel, a bar or a row
        Child,         // any other object a mod put on a screen
        Canvas,        // a canvas or a window of its own, opened over a screen
        Text,          // a game button's words or picture that a mod changed
        Notice,        // a notice or a question it posted
        Fault          // code of its that threw while the kit read or pressed its piece
    }

    /// <summary>
    /// The rules for another mod's pieces on the screens the kit replaces, game-free so they are checked offline
    /// (EconomyCheck, UiForeignChecks): who owns a fault, which of a screen's children the game did not make, the order a
    /// foreign window takes over the kit, who has the keys, and the once-only log. The game file (UiForeignGame) feeds them
    /// what it reads. Cost: a few comparisons, at a screen's open or a fault; nothing a frame.
    /// </summary>
    public static class UiForeignRules
    {
        /// <summary>Canvases of the kit's own stand this far over the covered screen's order, and a foreign window lifted over them this far over the top.</summary>
        public const int LiftOver = 2;

        /// <summary>
        /// The owner of a fault from the frames of its stack, innermost first, each with its assembly's name and origin:
        /// the first frame of another mod's before the first frame of the kit's. Null when it is the kit's or the game's.
        /// </summary>
        public static string Culprit(IList<string> names, IList<UiOrigin> origins, int count)
        {
            for (int i = 0; i < count; i++)
            {
                if (origins[i] == UiOrigin.Ours)
                {
                    return null;   // the kit's own frame comes first: its own fault
                }
                if (origins[i] == UiOrigin.Other)
                {
                    return names[i];
                }
            }
            return null;
        }

        /// <summary>
        /// The owner of a fault read off its own stack: each frame's method is given its origin and the name its mod goes by, and the first
        /// frame of another mod's before the kit's own is blamed. Null when there is none, or the fault is the kit's or the game's.
        /// </summary>
        public static string Blame(Exception e, Func<MethodBase, UiOrigin> origin, Func<MethodBase, string> nameOf)
        {
            if (e == null)
            {
                return null;
            }
            var trace = new StackTrace(e, false);
            for (int i = 0; i < trace.FrameCount; i++)
            {
                MethodBase method = trace.GetFrame(i).GetMethod();
                UiOrigin from = method != null ? origin(method) : UiOrigin.Framework;
                if (from == UiOrigin.Ours)
                {
                    return null;
                }
                if (from == UiOrigin.Other)
                {
                    return nameOf(method);
                }
            }
            return null;
        }

        /// <summary>The origin of an assembly by its name alone, when the game side cannot tell by where it was loaded from.</summary>
        public static UiOrigin OriginOf(string assembly, string own)
        {
            if (string.IsNullOrEmpty(assembly))
            {
                return UiOrigin.Framework;
            }
            if (string.Equals(assembly, own, StringComparison.Ordinal))
            {
                return UiOrigin.Ours;
            }
            if (assembly == "mscorlib" || assembly == "netstandard" || assembly == "System" || assembly.StartsWith("System.", StringComparison.Ordinal)
                || assembly.StartsWith("Mono.", StringComparison.Ordinal) || assembly == "Mono" || assembly.StartsWith("Unity", StringComparison.Ordinal)
                || assembly.StartsWith("TextMeshPro", StringComparison.Ordinal) || assembly == "BAModAPI" || assembly.StartsWith("Newtonsoft", StringComparison.Ordinal)
                || assembly.StartsWith("MonoMod", StringComparison.Ordinal) || char.IsDigit(assembly[0]))   // a name that starts with a digit is a patching library's
            {
                return UiOrigin.Framework;
            }
            if (assembly.StartsWith("BigAmbitions", StringComparison.Ordinal) || assembly == "Assembly-CSharp" || assembly == "ExternalPlugins" || assembly == "HGPlugins"
                || assembly == "Localizor")
            {
                return UiOrigin.Game;
            }
            return UiOrigin.Other;
        }

        /// <summary>
        /// The children of a container the game did not make: by its names for that container, a name the mod does not own
        /// either, and nothing else. Fills <paramref name="into"/> with their indexes; returns how many.
        /// </summary>
        public static int NotTheGames(IList<string> childNames, IList<bool> active, string[] gameNames, List<int> into)
        {
            into.Clear();
            for (int i = 0; i < childNames.Count; i++)
            {
                if ((active == null || active[i]) && !UiHostRules.Own(childNames[i], gameNames))
                {
                    into.Add(i);
                }
            }
            return into.Count;
        }

        /// <summary>
        /// The children added since a snapshot of a screen's top level, by name, for a screen the game's names are not known
        /// for: a child the snapshot lacks, active now. Fills <paramref name="into"/> with their indexes; returns how many.
        /// </summary>
        public static int NewSince(IList<string> childNames, IList<bool> active, ICollection<string> snapshot, List<int> into)
        {
            into.Clear();
            for (int i = 0; i < childNames.Count; i++)
            {
                if (active[i] && !snapshot.Contains(childNames[i]) && !UiHostRules.Own(childNames[i], null))
                {
                    into.Add(i);
                }
            }
            return into.Count;
        }

        /// <summary>
        /// The sort order a foreign window opened over the kit takes: its own when it is already over every canvas of the
        /// kit's, else just over them. A window that stands over the kit already is never moved.
        /// </summary>
        public static int Lifted(int windowOrder, int kitTop) => windowOrder > kitTop + 1 ? windowOrder : kitTop + LiftOver;

        /// <summary>
        /// Whether the kit reads the keys, with what stands over it: a foreign window first (Esc and every key are its own
        /// while it is up), then the game's boxes, then the kit's All apps. The kit never takes the game's Escape from a
        /// window above it.
        /// </summary>
        public static bool KitHasKeys(bool foreignWindow, bool gameBox, bool kitOver) => !foreignWindow && !gameBox && !kitOver;

        /// <summary>
        /// An overlay of the kit that answers keys (Fred's card) has them only while no menu of the game's (pause, options, blueprints),
        /// no full menu, no box and no other mod's window is up.
        /// </summary>
        public static bool KitHasOverlayKeys(bool pauseOrOptions, bool fullMenu, bool box, bool foreignWindow) => !pauseOrOptions && !fullMenu && !box && !foreignWindow;

        /// <summary>
        /// A canvas opened while a kit screen is up is a foreign window when it was not among those standing at the open,
        /// is not the kit's, and is not under the game's own interface.
        /// </summary>
        public static bool IsWindow(bool wasThere, bool kits, bool gameOwn, bool active, bool takesMouse) => active && takesMouse && !wasThere && !kits && !gameOwn;

        /// <summary>The words for what a mod's piece is, in English, for Player.log: "a tab", "a canvas".</summary>
        public static string Words(UiPieceKind kind)
        {
            switch (kind)
            {
                case UiPieceKind.App: return "an app";
                case UiPieceKind.Tab: return "a tab";
                case UiPieceKind.Button: return "a button";
                case UiPieceKind.Child: return "an object";
                case UiPieceKind.Canvas: return "a window of its own";
                case UiPieceKind.Text: return "a changed label or picture";
                case UiPieceKind.Notice: return "a notice";
                default: return "a fault";
            }
        }
    }

    /// <summary>
    /// What the kit found of other mods' pieces on its screens, and the lines that say it once: in Player.log as it is found
    /// (each piece once) and in the one self-check line at load. Game-free; the log goes through <see cref="Log"/>.
    /// </summary>
    public static class UiForeign
    {
        /// <summary>One piece found.</summary>
        public struct Piece
        {
            public string Screen;   // the screen's id in UiScreens ("bizman", "phone", "hud")
            public UiPieceKind Kind;
            public string Owner;    // the mod's name, or the object's when its mod is not known
            public string Where;    // where on the screen: a path or a part's name
            public string Handled;  // what the kit does with it, in English
        }

        private static readonly List<Piece> Found = new List<Piece>(16);
        private static readonly HashSet<string> Keys = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>Where the once-only lines go (Player.log); null offline.</summary>
        public static Action<string> Log;

        /// <summary>Where the lines about a fault go (Player.log as a warning); null offline.</summary>
        public static Action<string> Warn;

        /// <summary>The pieces found so far, in the order found.</summary>
        public static IReadOnlyList<Piece> All => Found;

        /// <summary>A mod's piece seen. True the first time (and logged); the same piece again costs a set look-up and says nothing.</summary>
        public static bool Note(string screen, UiPieceKind kind, string owner, string where, string handled)
        {
            string key = screen + "|" + (int)kind + "|" + owner + "|" + where;
            if (!Keys.Add(key))
            {
                return false;
            }
            Found.Add(new Piece { Screen = screen, Kind = kind, Owner = owner, Where = where, Handled = handled });
            if (kind == UiPieceKind.Fault)
            {
                Warn?.Invoke("UI: code of " + Name(owner) + " threw on the " + screen + " screen (" + where + "); " + handled + ". (Said once.)");
            }
            else
            {
                Log?.Invoke("UI: " + Name(owner) + " added " + UiForeignRules.Words(kind) + " to the " + screen + " screen (" + where + "); " + handled + ". (Said once.)");
            }
            return true;
        }

        /// <summary>True once this exact piece has been noted.</summary>
        public static bool Has(string screen, UiPieceKind kind, string owner, string where) => Keys.Contains(screen + "|" + (int)kind + "|" + owner + "|" + where);

        /// <summary>
        /// The load's self-check line: each screen with pieces of other mods and how it holds them, or that none were found.
        /// </summary>
        public static string Line()
        {
            if (Found.Count == 0)
            {
                return "UI: no other mod has added to a screen of the RLO interface.";
            }
            var sb = new StringBuilder(160);
            sb.Append("UI: other mods' pieces on the RLO interface (").Append(Found.Count).Append("): ");
            for (int i = 0; i < Found.Count; i++)
            {
                Piece p = Found[i];
                sb.Append(i > 0 ? "; " : string.Empty).Append(p.Screen).Append(": ").Append(Name(p.Owner)).Append(' ').Append(UiForeignRules.Words(p.Kind))
                  .Append(" at ").Append(p.Where).Append(" (").Append(p.Handled).Append(')');
            }
            return sb.Append('.').ToString();
        }

        /// <summary>The pieces found on one screen.</summary>
        public static int Count(string screen)
        {
            int n = 0;
            for (int i = 0; i < Found.Count; i++)
            {
                n += Found[i].Screen == screen ? 1 : 0;
            }
            return n;
        }

        /// <summary>The book emptied: a new session, and the checks.</summary>
        public static void Reset()
        {
            Found.Clear();
            Keys.Clear();
        }

        private static string Name(string owner) => string.IsNullOrEmpty(owner) ? "a mod" : "'" + owner + "'";
    }
}
