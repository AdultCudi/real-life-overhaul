using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The game side of the kit's care for other mods' pieces (docs/reference/kit-for-other-mods.md): whose code a fault came
    /// from, which pieces of a screen a mod added, and the names the log gives them. Everything here runs on a fault or on a
    /// screen's open, never in a frame; the rules it feeds are game-free (UiForeignRules, UiBreaker, UiForeign). A fault of
    /// another mod's code that the kit meets reading or pressing its piece is caught by name, logged once, and opens that
    /// screen's breaker so it is the game's own for the session; it never reaches the game's loop and never counts against
    /// the interface's own faults.
    /// </summary>
    public static class UiForeignGame
    {
        private static readonly string OwnName = typeof(UiForeignGame).Assembly.GetName().Name;
        private static readonly Dictionary<Assembly, UiOrigin> Origins = new Dictionary<Assembly, UiOrigin>(32);
        private static readonly List<MonoBehaviour> Scripts = new List<MonoBehaviour>(16);
        private static readonly List<Button> Buttons = new List<Button>(8);
        private static string _managed;
        private static FieldInfo _calls, _runtime, _delegate;
        private static bool _callsTried;

        /// <summary>Wires the once-only lines to Player.log; once, when the interface starts.</summary>
        public static void Start(Action<string> info, Action<string> warn)
        {
            UiForeign.Log = info;
            UiForeign.Warn = warn;
        }

        // ------------------------------------------------------------------------------------------------- whose code

        /// <summary>Whose code an assembly holds. Cached: an assembly is asked once.</summary>
        public static UiOrigin Origin(Assembly assembly)
        {
            if (assembly == null)
            {
                return UiOrigin.Framework;
            }
            if (Origins.TryGetValue(assembly, out UiOrigin known))
            {
                return known;
            }
            UiOrigin origin;
            try
            {
                origin = assembly.IsDynamic ? UiOrigin.Framework : UiForeignRules.OriginOf(assembly.GetName().Name, OwnName);
                if (origin == UiOrigin.Other && InGameFolder(assembly))
                {
                    origin = UiOrigin.Game;   // a library the game ships that no name above lists
                }
            }
            catch (Exception)
            {
                origin = UiOrigin.Framework;
            }
            Origins[assembly] = origin;
            return origin;
        }

        private static bool InGameFolder(Assembly assembly)
        {
            if (_managed == null)
            {
                string unity = typeof(UnityEngine.Object).Assembly.Location;
                _managed = string.IsNullOrEmpty(unity) ? string.Empty : System.IO.Path.GetDirectoryName(unity) ?? string.Empty;
            }
            string at = assembly.Location;
            return _managed.Length > 0 && !string.IsNullOrEmpty(at) && at.StartsWith(_managed, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>The name another mod goes by in the log: its entry in the list of mods the mod knows, else its assembly's.</summary>
        public static string Friendly(string assembly)
        {
            for (int i = 0; i < OtherMods.All.Length; i++)
            {
                if (OtherModName.Is(assembly, OtherMods.All[i].AssemblyPrefix))
                {
                    return OtherMods.All[i].Name;
                }
            }
            return assembly;
        }

        /// <summary>The mod whose code a fault came from (the first frame of another mod's before the kit's own), or null when it is not another mod's.</summary>
        public static string Blame(Exception e)
        {
            if (e == null)
            {
                return null;
            }
            try
            {
                string who = UiForeignRules.Blame(e, m => Origin(m.DeclaringType != null ? m.DeclaringType.Assembly : null), m => m.DeclaringType.Assembly.GetName().Name);
                return who != null ? Friendly(who) : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// For an exception filter round a kit screen's reads and presses: true when the fault is another mod's code. It is then
        /// logged once with the mod's name, and the screen's breaker is open (the screen is the game's own for the session, and
        /// the rest of the interface goes on). False for the kit's own and the game's faults, which go the guard's way.
        /// </summary>
        public static bool Caught(Exception e, string screenId, string screenName)
        {
            string who = Blame(e);
            if (who == null)
            {
                return false;
            }
            string where = e.GetType().Name + " in " + (e.TargetSite != null ? e.TargetSite.DeclaringType?.Name + "." + e.TargetSite.Name : "?");
            UiForeign.Note(screenId ?? screenName, UiPieceKind.Fault, who, where, "the screen is the game's own for the session");
            UiBreaker.Trip(screenId);
            return true;
        }

        /// <summary>
        /// For an exception filter round a read of one piece of another mod's (a label, a colour): true when the fault is another mod's code, said
        /// once with its mod and the object; the caller leaves that one value empty and goes on. False for the kit's and the game's own faults.
        /// </summary>
        public static bool Spoilt(Exception e, Component piece)
        {
            string who = Blame(e);
            if (who == null)
            {
                return false;
            }
            UiForeign.Note("reads", UiPieceKind.Fault, who, e.GetType().Name + " reading " + (piece != null ? piece.name : "a piece"), "that one label is left empty and the screen goes on");
            return true;
        }

        // ------------------------------------------------------------------------------------------------- whose piece

        /// <summary>
        /// The mod a piece of a screen belongs to: the assembly of a script of another mod's on it or under it, else of a
        /// listener of another mod's on a button under it. Null when nothing ties it to another mod. At a screen's open.
        /// </summary>
        public static string OwnerOf(Transform piece)
        {
            if (piece == null)
            {
                return null;
            }
            Scripts.Clear();
            piece.GetComponentsInChildren(true, Scripts);
            string owner = null;
            for (int i = 0; i < Scripts.Count && owner == null; i++)
            {
                if (Scripts[i] == null)
                {
                    continue;
                }
                Assembly assembly = Scripts[i].GetType().Assembly;
                if (Origin(assembly) == UiOrigin.Other)
                {
                    owner = Friendly(assembly.GetName().Name);
                }
            }
            Scripts.Clear();
            if (owner != null)
            {
                return owner;
            }
            Buttons.Clear();
            piece.GetComponentsInChildren(true, Buttons);
            for (int i = 0; i < Buttons.Count && owner == null; i++)
            {
                owner = ListenerOwner(Buttons[i]);
            }
            Buttons.Clear();
            return owner;
        }

        /// <summary>The mod a button's runtime listener belongs to (read from Unity's call list, which is private: null when it cannot be read).</summary>
        public static string ListenerOwner(Button button) => Listeners(button, out bool _);

        /// <summary>
        /// The mod that made a button: a script of another mod's on the button itself, else a runtime listener of another
        /// mod's with none of the game's or this mod's beside it (a mod's copy of a game button takes the game's listeners
        /// off and puts its own on; a game button a mod only listens to keeps the game's and stays the game's). Null for the
        /// game's and the mod's own buttons, and when the listeners cannot be read. Asked when a panel's objects changed.
        /// </summary>
        public static string ButtonOwner(Button button)
        {
            if (button == null)
            {
                return null;
            }
            Scripts.Clear();
            button.GetComponents(Scripts);
            string owner = null;
            for (int i = 0; i < Scripts.Count && owner == null; i++)
            {
                Assembly assembly = Scripts[i] != null ? Scripts[i].GetType().Assembly : null;
                if (assembly != null && Origin(assembly) == UiOrigin.Other)
                {
                    owner = Friendly(assembly.GetName().Name);
                }
            }
            Scripts.Clear();
            if (owner != null)
            {
                return owner;
            }
            owner = Listeners(button, out bool ownToo);
            return ownToo ? null : owner;
        }

        // The first mod among a button's runtime listeners, and whether code of the game's or this mod's listens too.
        private static string Listeners(Button button, out bool ownToo)
        {
            ownToo = false;
            if (button == null || !FindCalls())
            {
                return null;
            }
            string other = null;
            try
            {
                object calls = _calls.GetValue(button.onClick);
                var list = calls != null ? _runtime.GetValue(calls) as System.Collections.IList : null;
                for (int i = 0; list != null && i < list.Count; i++)
                {
                    object call = list[i];
                    Delegate d = call != null && _delegate != null && _delegate.DeclaringType.IsInstanceOfType(call) ? _delegate.GetValue(call) as Delegate : null;
                    if (d == null || d.Method == null || d.Method.DeclaringType == null)
                    {
                        continue;
                    }
                    UiOrigin from = Origin(d.Method.DeclaringType.Assembly);
                    if (from == UiOrigin.Other)
                    {
                        other = other ?? Friendly(d.Method.DeclaringType.Assembly.GetName().Name);
                    }
                    else if (from == UiOrigin.Game || from == UiOrigin.Ours)
                    {
                        ownToo = true;
                    }
                }
            }
            catch (Exception)
            {
                _calls = null;   // not as this Unity has it: the listeners are not read again
                _callsTried = true;
                return null;
            }
            return other;
        }

        private static bool FindCalls()
        {
            if (_callsTried)
            {
                return _calls != null;
            }
            _callsTried = true;
            try
            {
                const BindingFlags all = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
                _calls = typeof(UnityEventBase).GetField("m_Calls", all);
                Type list = _calls != null ? _calls.FieldType : null;
                _runtime = list != null ? list.GetField("m_RuntimeCalls", all) : null;
                Type invokable = typeof(UnityEventBase).Assembly.GetType("UnityEngine.Events.InvokableCall");
                _delegate = invokable != null ? invokable.GetField("Delegate", all) : null;
                if (_runtime == null || _delegate == null)
                {
                    _calls = null;
                }
            }
            catch (Exception)
            {
                _calls = null;
            }
            return _calls != null;
        }

        /// <summary>The path of a piece under its screen, for the log: "Business/Menu/TabX".</summary>
        public static string PathOf(Transform piece, Transform from)
        {
            if (piece == null)
            {
                return "?";
            }
            string path = piece.name;
            int guard = 0;
            for (Transform t = piece.parent; t != null && t != from && guard++ < 6; t = t.parent)
            {
                path = t.name + "/" + path;
            }
            return path;
        }

        /// <summary>The kind of piece an object is, for the log: a tab by a toggle or a button on a bar, a button, else an object.</summary>
        public static UiPieceKind KindOf(Transform piece)
        {
            if (piece.GetComponent<Button>() != null || piece.GetComponent<Toggle>() != null)
            {
                return UiPieceKind.Button;
            }
            return UiPieceKind.Child;
        }

        // ------------------------------------------------------------------------------------------------- a screen's top level

        /// <summary>
        /// A screen's own children by name, taken when the kit binds over it, to tell what another mod added since. A child the
        /// snapshot lacks, active, that belongs to another mod (a script or a listener of its on it) is its addition.
        /// </summary>
        public sealed class Top
        {
            private readonly HashSet<string> _names = new HashSet<string>(StringComparer.Ordinal);

            /// <summary>Takes the names of a container's children as they stand.</summary>
            public void Take(Transform container)
            {
                _names.Clear();
                for (int i = 0; container != null && i < container.childCount; i++)
                {
                    _names.Add(container.GetChild(i).name);
                }
            }

            /// <summary>
            /// Another mod's children added to the container since, noted once each; returns how many are shown now. With a list of the
            /// game's names for the container, any active child neither the game's nor the mod's is another mod's, owner or no.
            /// </summary>
            public int Added(Transform container, string screenId, string[] gameNames, string handled)
            {
                int shown = 0;
                for (int i = 0; container != null && i < container.childCount; i++)
                {
                    Transform child = container.GetChild(i);
                    if (!child.gameObject.activeSelf || UiHostRules.Own(child.name, gameNames))
                    {
                        continue;
                    }
                    if (gameNames == null && _names.Contains(child.name))
                    {
                        continue;
                    }
                    string owner = OwnerOf(child);
                    if (owner == null && gameNames == null)
                    {
                        continue;   // not tied to a mod: the game's own piece it made after the bind (a pooled popup)
                    }
                    shown++;
                    UiForeign.Note(screenId, KindOf(child), owner ?? child.name, PathOf(child, container), handled);
                }
                return shown;
            }
        }
    }
}
