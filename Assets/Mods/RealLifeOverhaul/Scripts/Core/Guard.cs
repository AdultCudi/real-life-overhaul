using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using BAModAPI;
using BigAmbitions.Mods;
using Localizor;
using UI.Notification;
using UnityEngine;
using UnityEngine.Events;

namespace RealLifeOverhaul
{
    /// <summary>
    /// What keeps one feature's trouble from becoming the game's, and what tells the player about it: the one mechanism
    /// of the mod against a game that is not the one it was made for.
    ///
    /// THE CONTRACT. <see cref="Start"/>, once at game start and before any feature, resolves <see cref="GameContract"/>
    /// (every piece of the game the mod reaches by name, and the members it names in its code where an older game lacks
    /// them), so that Core/GameMembers and Core/GameConfirm take their answers without them from the first question;
    /// reads the game's build (Core/GameBuild); and checks every type and member of the game's and Unity's assemblies the
    /// mod's compiled code names against the running game (Core/ModScan). Each piece the game lacks is said once in
    /// Player.log with what stands on it; the features that stand on one are switched off before they run (their
    /// components are not even added, Add); a piece only named in a method of the mod's own whose caller does without
    /// it switches nothing off. One notice, the game's own question box, says it (Core/GameBuild.Notice: in the main
    /// menu for an older game, once a session in the city); the options page says it under Diagnostics and after the
    /// name of each feature hit (<see cref="Note"/>).
    ///
    /// FAULTS. Every entry point the engine or the game calls (Update, LateUpdate, FixedUpdate, a coroutine, a handler
    /// on one of the game's events, a click, a step of the start) runs inside a try with a <see cref="FeatureGuard"/>.
    /// A fault that says the running game lacks a member the code names (<see cref="Missing"/>) stops that entry point's
    /// class at once, said once: it would fail at every call. Any other fault is written to Player.log in full and the
    /// feature goes on; a run of them in a short time (<see cref="FeatureGuard.Most"/> in <see cref="FeatureGuard.Seconds"/>
    /// s) switches the whole feature off for the session: its components stop, what it has in the street is taken away
    /// (its Cleanup in Meta/FeatureList), its save line is left as it last wrote it (Core/SaveHook skips its writer), and
    /// one line and one notice say so. A part inside an entry point that can stop alone stands down with
    /// <see cref="StandsDown"/>, a step done without with <see cref="DoesWithout"/>. The game's own calls into the mod
    /// past its entry points (what it asks through its interfaces and base classes, the delegates it keeps, the pages of
    /// the mod's dialogs and the options page) are behind the same guard: Core/GuardCalls.
    ///
    /// SAVES. A line a store could not read is kept as it was (Core/SaveLines), with one line and one notice per save.
    ///
    /// Cost: once at game start, the contract (some 140 lookups) and the check of the mod's code (some 4 000 questions to
    /// the game, about twenty milliseconds); nothing per frame beyond the try round each entry point; the notices'
    /// component is switched off while it has nothing to say.
    /// </summary>
    public static partial class Guard
    {
        private const int MostDetailed = 3;   // faults of one entry point written in full; after that one short line each up to MostShort
        private const int MostShort = 12;
        private const int MostNamed = 6;      // the mod's classes a line of Player.log names for one piece the game lacks

        public static IModLogger Logger;

        private static readonly Queue<string> Pending = new Queue<string>(4);
        private static readonly Dictionary<Type, FeatureGuard> ByOwner = new Dictionary<Type, FeatureGuard>(96);
        private static readonly HashSet<string> Told = new HashSet<string>(StringComparer.Ordinal);
        private static readonly HashSet<string> Lacking = new HashSet<string>(StringComparer.Ordinal);   // features off as the mod started: the game lacks what they stand on
        private static readonly List<string> StoodDown = new List<string>(4);                              // parts stopped when first used, in English
        private static bool _started;
        private static HashSet<string> _hit;   // the features with a part off, once resolved
        private static int _offParts;

        /// <summary>True when any part is off after a game update, or any feature was switched off after its faults.</summary>
        public static bool AnyOff => _offParts > 0 || Lacking.Count > 0 || StoodDown.Count > 0 || FeatureGuards.AnyOff;

        /// <summary>True when a part of the mod is off because this game lacks what it stands on (the notice names them).</summary>
        public static bool PartsOff => _offParts > 0 || Lacking.Count > 0;

        /// <summary>Made by Core/RealLifeOverhaulMod for each city; shows what is waiting once the HUD is there.</summary>
        public static GuardNotices Notices;

        // ---------------------------------------------------------------- the contract

        /// <summary>Once, at game start, before any feature: the contract, the build, the mod's code against this game.</summary>
        public static void Start(IModLogger logger)
        {
            Logger = logger;
            if (_started)
            {
                return;
            }
            _started = true;
            SaveLines.Told = Kept;
            SaveLines.Later = (key, versions) => Logger?.Info("Save: the line '" + key + "' was written by a later version of the mod (" + versions
                                                             + "); it is read as far as this version can, and what this version does not know of it is not kept.");
            try
            {
                ContractBook.Resolve(ContractBook.FindLoaded);
                GameMembers.Known();
                GameConfirm.Known();
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
            GameBuild.Start(logger);
            CheckLayers();
            try
            {
                _hit = ContractBook.FeaturesHit();
                _offParts = ContractBook.OffParts().Count;
                TellContract();
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
            CheckCode();
        }

        private static string Builds => GameBuild.Builds(GameBuild.Running, Meta.Changelog.Version);

        // Each piece of the contract this game lacks or has changed, once: a part switched off, or done without.
        private static void TellContract()
        {
            List<GamePart> off = ContractBook.OffParts();
            foreach (GameMember member in ContractBook.Problems())
            {
                if (member.Optional)
                {
                    Logger?.Warn("Game contract: " + member.Label + ": " + member.Problem + "; the mod does without it (" + PartsOf(member) + "). " + Builds);
                }
            }
            foreach (GamePart part in off)
            {
                Logger?.Warn("Game contract: switched off until " + Until + ": " + part.English + " (" + part.Why + "). " + Builds);
            }
            if (GameBuild.Of(GameBuild.Running) == BuildKind.Newer)
            {
                Logger?.Warn("Game contract: " + ContractBook.Members.Count + " pieces of the game checked on a newer game; "
                             + (off.Count == 0 ? "every one the mod relies on is there as expected." : off.Count + " part(s) switched off (above), everything else runs."));
            }
            else
            {
                Logger?.Info("Game contract: " + ContractBook.Members.Count + " pieces of the game checked, " + off.Count + " part(s) off. " + Builds);
            }
        }

        private static string PartsOf(GameMember member)
        {
            var text = new StringBuilder(48);
            foreach (GamePart part in member.Parts)
            {
                text.Append(text.Length > 0 ? "; " : string.Empty).Append(part.English);
            }
            return text.ToString();
        }

        // What brings a part back: an older game updated in Steam, else a version of the mod made for this game.
        private static string Until => GameBuild.Of(GameBuild.Running) == BuildKind.Older ? "the game is updated in Steam" : "the mod is updated";

        // The game's layers the mod finds things by: one that is gone is named once (GameContract.Layers).
        private static void CheckLayers()
        {
            try
            {
                var gone = new List<string>();
                foreach (string layer in GameContract.Layers)
                {
                    if (LayerMask.NameToLayer(layer) < 0)
                    {
                        gone.Add(layer);
                    }
                }
                if (gone.Count > 0)
                {
                    Logger?.Warn("Game contract: the game has no layer " + string.Join(", ", gone) + " any more; what the mod looks for on "
                                 + (gone.Count == 1 ? "it" : "them") + " (the ground, roads, buildings, vehicles, people) is not found until the mod is updated.");
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        // The mod's compiled code against this game (Core/ModScan): each type or member it lacks that the contract does
        // not already name, once, with what stands on it; the features that stand on one are switched off now.
        private static void CheckCode()
        {
            try
            {
                string path = typeof(Guard).Assembly.Location;
                if (string.IsNullOrEmpty(path))
                {
                    Logger?.Info("Game contract: the mod's own file is not known, so what this game lacks of what the mod's code names is found as it is first used.");
                    return;
                }
                var clock = System.Diagnostics.Stopwatch.StartNew();
                ModImage image = ModImage.Read(path, out string why);
                if (image == null)
                {
                    Logger?.Warn("Game contract: the mod's own file could not be read (" + why + "), so what this game lacks of what the mod's code names is found as it is first used.");
                    return;
                }
                List<MissingReference> missing = ModScan.Run(image, new ModScan.Loaded(typeof(Guard).Module), out int asked);
                int told = 0;
                foreach (MissingReference m in missing)
                {
                    if (InContract(m))
                    {
                        continue;   // (said above, with the part it is for)
                    }
                    told++;
                    if (m.Contained)
                    {
                        Logger?.Warn("Game contract: this game lacks the " + m.Kind + " " + m.Name + " (" + m.Assembly + "); the mod names it only where it does without it ("
                                     + Named(m.Isolated) + "). " + Builds);
                        continue;
                    }
                    var titles = new List<string>(m.Features.Count);
                    foreach (string feature in m.Features)
                    {
                        Lacking.Add(feature);
                        FeatureGuards.SwitchOff(feature);
                        titles.Add(feature);
                    }
                    Logger?.Warn("Game contract: this game lacks the " + m.Kind + " " + m.Name + " (" + m.Assembly + ": " + m.Problem + "). The mod's " + Named(m.Users)
                                 + (titles.Count > 0 ? " name it, so " + (titles.Count == 1 ? "the feature '" : "the features '") + string.Join("', '", titles) + (titles.Count == 1 ? "' is" : "' are")
                                                       + " switched off until " + Until : " name it")
                                 + (m.Machinery ? (titles.Count > 0 ? "; the mod's own machinery names it too, and stops only where it meets it" : ", and they are the mod's own machinery, which stops only where it meets it") : string.Empty)
                                 + ". " + Builds);
                }
                string summary = "Game contract check (the mod's code): " + asked + " types and members of the game's and Unity's assemblies asked of this game in "
                             + clock.ElapsedMilliseconds + " ms; " + missing.Count + " missing" + (told < missing.Count ? " (" + (missing.Count - told) + " of them pieces of the contract, above)" : string.Empty)
                             + (Lacking.Count > 0 ? "; " + Lacking.Count + " feature(s) switched off: " + string.Join(", ", Lacking) : string.Empty) + ": " + (missing.Count == 0 ? "PASS." : "the mod runs without them.");
                if (missing.Count == 0)
                {
                    Logger?.Info(summary);
                }
                else
                {
                    Logger?.Warn(summary);
                }
            }
            catch (Exception e)
            {
                Logger?.Warn("Game contract: the mod's code could not be checked against this game; what it lacks is found as it is first used.");
                Logger?.Error(e);
            }
        }

        // A missing reference that is a line of the contract (its type, or a member of it by name: a property by its accessors).
        private static bool InContract(MissingReference m)
        {
            foreach (GameMember member in ContractBook.Members)
            {
                if (member.Problem == null)
                {
                    continue;
                }
                string type = member.TypeName;
                if (m.Kind == "type" ? m.Name == type
                    : m.Name == type + "." + member.Name || m.Name == type + ".get_" + member.Name || m.Name == type + ".set_" + member.Name)
                {
                    return true;
                }
            }
            return false;
        }

        private static string Named(List<string> names)
        {
            if (names.Count <= MostNamed)
            {
                return string.Join(", ", names);
            }
            return string.Join(", ", names.GetRange(0, MostNamed)) + " and " + (names.Count - MostNamed) + " more";
        }

        /// <summary>
        /// The parts named in the notice, in the player's language: the contract's parts that are off, the features off
        /// because this game lacks what they stand on, and, for an older game (<paramref name="doneWithout"/>), the parts
        /// that work without what it lacks.
        /// </summary>
        public static string PartsNamed(bool doneWithout)
        {
            var names = new List<string>(8);
            foreach (GamePart part in ContractBook.OffParts())
            {
                Name(names, part.NameKey.GetLocalization());
            }
            if (doneWithout)
            {
                foreach (GameMember member in ContractBook.Problems())
                {
                    if (member.Optional && member.Missing)
                    {
                        foreach (GamePart part in member.Parts)
                        {
                            Name(names, part.NameKey.GetLocalization());
                        }
                    }
                }
            }
            foreach (string feature in Lacking)
            {
                Name(names, FeatureName(feature));
            }
            return string.Join("; ", names);
        }

        /// <summary>The names of the parts the given pieces stand on, in the player's language, joined for a sentence (the test of the notice).</summary>
        public static string PartNames(GameMember[] members)
        {
            var names = new List<string>(8);
            foreach (GameMember member in members)
            {
                foreach (GamePart part in member.Parts)
                {
                    Name(names, part.NameKey.GetLocalization());
                }
            }
            return string.Join("; ", names);
        }

        private static void Name(List<string> names, string name)
        {
            if (!string.IsNullOrEmpty(name) && !names.Contains(name))
            {
                names.Add(name);
            }
        }

        /// <summary>The parts' names in the player's language, joined for a sentence.</summary>
        public static string PartNames(List<GamePart> parts)
        {
            var text = new StringBuilder(64);
            for (int i = 0; i < parts.Count; i++)
            {
                text.Append(i > 0 ? "; " : string.Empty).Append(parts[i].NameKey.GetLocalization());
            }
            return text.ToString();
        }

        /// <summary>
        /// For the options page: a short note after a feature's name while a part of it is off after a game update, or
        /// it was switched off this session after its faults; null while all of it runs.
        /// </summary>
        public static string Note(string feature)
        {
            if (feature == null)
            {
                return AnyOff ? "reallife_guard_note_parts".GetLocalization() : null;   // (the page's first group: something somewhere is off)
            }
            if (FeatureGuards.FeatureOff(feature) && !Lacking.Contains(feature))
            {
                return "reallife_guard_note_faults".GetLocalization();
            }
            return Lacking.Contains(feature) || (_hit != null && _hit.Contains(feature)) ? "reallife_guard_note_parts".GetLocalization() : null;
        }

        /// <summary>For the options page's button under Diagnostics: what is off, and the game's build against the one the mod is made for.</summary>
        public static string Status()
        {
            string parts = PartsNamed(GameBuild.Of(GameBuild.Running) == BuildKind.Older);
            string text = (parts.Length == 0 ? "reallife_guard_status_ok" : "reallife_guard_status_off").GetLocalization()
                .Replace("{parts}", parts)
                .Replace("{build}", GameBuild.Running > 0 ? GameBuild.Running.ToString() : "?")
                .Replace("{checked}", GameBuild.MadeFor.ToString());
            var faults = new StringBuilder();
            foreach (string feature in FeatureGuards.OffList)
            {
                if (!Lacking.Contains(feature))
                {
                    faults.Append(faults.Length > 0 ? "; " : string.Empty).Append(FeatureName(feature));
                }
            }
            if (faults.Length > 0)
            {
                text += "\n\n" + "reallife_guard_status_faults".GetLocalization().Replace("{features}", faults.ToString());
            }
            if (StoodDown.Count > 0)
            {
                text += "\n\n" + "reallife_guard_status_stood".GetLocalization().Replace("{count}", StoodDown.Count.ToString());
            }
            return text;
        }

        /// <summary>
        /// For a bug report (Meta/BugReport): the state of the game contract in English, for the developer whatever the
        /// player's language: the game's build against the one the mod is made for, the parts off because this game lacks
        /// what they stand on, the features switched off after their faults, the parts stood down when first used, and
        /// whether the notice of it all was due and shown. On a button press only.
        /// </summary>
        public static string Report()
        {
            BuildKind kind = GameBuild.Of(GameBuild.Running);
            var text = new StringBuilder(256);
            text.Append("this game is ").Append(kind == BuildKind.Same ? "the build the mod is made for" : kind == BuildKind.Older ? "older than the build the mod is made for"
                : kind == BuildKind.Newer ? "newer than the build the mod is made for" : "of a build that could not be read");
            var parts = new List<string>(8);
            foreach (GamePart part in ContractBook.OffParts())
            {
                if (!parts.Contains(part.English))
                {
                    parts.Add(part.English);
                }
            }
            foreach (string feature in Lacking)
            {
                parts.Add(feature);
            }
            text.Append("; parts off: ").Append(parts.Count > 0 ? string.Join(", ", parts) : "none");
            var faults = new List<string>(4);
            foreach (string feature in FeatureGuards.OffList)
            {
                if (!Lacking.Contains(feature))
                {
                    faults.Add(feature);
                }
            }
            text.Append("; features off after faults: ").Append(faults.Count > 0 ? string.Join(", ", faults) : "none");
            text.Append("; parts stood down when first used: ").Append(StoodDown.Count > 0 ? string.Join("; ", StoodDown) : "none");
            bool due = kind == BuildKind.Older || PartsOff;
            text.Append("; the notice of it: ").Append(!due ? "not needed" : GameBuild.NoticeShown ? "shown" : "due, not shown yet");
            return text.ToString();
        }

        // ---------------------------------------------------------------- what the running game lacks, found as it is used

        /// <summary>True when the exception says the running game lacks a type, field or method the mod was compiled against (or has made it inaccessible).</summary>
        public static bool Missing(Exception e)
        {
            return e is TypeLoadException || e is MemberAccessException
                   || (e is TypeInitializationException && e.InnerException != null && Missing(e.InnerException));
        }

        /// <summary>
        /// For the catch round a part of an entry point that can stop alone: true when the running game lacks a member it
        /// names, said once for that part ("... is off for the rest of this session: this game lacks ..."), which the caller
        /// then stops for the session. False for any other fault, which the caller handles as it always has.
        /// </summary>
        public static bool StandsDown(Exception e, string part)
        {
            if (!Missing(e))
            {
                return false;
            }
            if (Say(part, "Game contract: " + part + " is off for the rest of this session: this game lacks " + GameBuild.MemberOf(e.Message) + ". " + Builds))
            {
                lock (StoodDown)
                {
                    StoodDown.Add(part);
                }
            }
            return true;
        }

        /// <summary>For a step that is done without on a game that lacks what it needs: true (and said once for <paramref name="what"/>) when the exception says so.</summary>
        public static bool DoesWithout(Exception e, string what)
        {
            return DoesWithout(e, what, "the mod does without it");
        }

        /// <summary>As <see cref="DoesWithout(Exception, string)"/>, with what is done instead.</summary>
        public static bool DoesWithout(Exception e, string what, string answer)
        {
            if (!Missing(e))
            {
                return false;
            }
            Say(what, "Game contract: this game lacks " + GameBuild.MemberOf(e != null ? e.Message : null) + ". " + char.ToUpperInvariant(what[0]) + what.Substring(1)
                      + ": " + answer + ". " + Builds);
            return true;
        }

        // Once a session for each part or member: a game that lacks something lacks it for good. True the first time.
        private static bool Say(string key, string line)
        {
            lock (Told)
            {
                if (!Told.Add(key))
                {
                    return false;
                }
            }
            if (Logger != null)
            {
                Logger.Warn(line);
            }
            else
            {
                Debug.LogWarning("[Mod:Real Life Overhaul] " + line);
            }
            return true;
        }

        // ---------------------------------------------------------------- faults

        /// <summary>A guard for an entry point of a feature (a static of the class it guards; the owner's save writer is skipped once it is off).</summary>
        public static FeatureGuard For(string feature, string where, Type owner = null)
        {
            FeatureGuard guard = FeatureGuards.For(feature, where);
            if (owner != null)
            {
                lock (ByOwner)
                {
                    if (!ByOwner.ContainsKey(owner))
                    {
                        ByOwner[owner] = guard;
                    }
                }
            }
            return guard;
        }

        /// <summary>The guard of the class a handler is a method of, or null.</summary>
        public static FeatureGuard OfOwner(Type owner)
        {
            if (owner == null)
            {
                return null;
            }
            try
            {
                // The class's own FrameGuard is a static of it: made now, if it has not been, so that it is the one found.
                System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(owner.TypeHandle);
            }
            catch (Exception)
            {
                // (a class that cannot be initialised has no guard of its own: one is made for it)
            }
            lock (ByOwner)
            {
                return ByOwner.TryGetValue(owner, out FeatureGuard guard) ? guard : null;
            }
        }

        /// <summary>
        /// A component of the city's start (Core/RealLifeOverhaulMod), added unless its feature was switched off as the mod
        /// started because this game lacks what it stands on: then it is not added at all, so that nothing of it runs. Only
        /// a component with a guard of its own (FrameGuard) is left out; one guarded by hand (Life/LifeEvents, which keeps
        /// the mod's moods out of the save whatever is off) is always added.
        /// </summary>
        public static T Add<T>(GameObject root) where T : Component
        {
            Type type = typeof(T);
            FeatureGuard guard = OfOwner(type);
            if (guard != null && guard.Off && guard.Feature != null && Lacking.Contains(guard.Feature))
            {
                Logger?.Info("Game contract: " + type.Name + " is not started: its feature '" + guard.Feature + "' is switched off until " + Until + ".");
                return null;
            }
            return root.AddComponent<T>();
        }

        /// <summary>
        /// An entry point threw. A member the running game lacks stops it at once (its class's guard, said once); any
        /// other fault is written to Player.log (the first ones in full), and a run of them switches the feature off for
        /// the session and stops the component that threw. Never throws.
        /// </summary>
        public static void Fail(FeatureGuard guard, Exception e, Behaviour component = null)
        {
            try
            {
                if (guard == null)
                {
                    Logger?.Error(e);
                    return;
                }
                if (Missing(e))
                {
                    // It would fail at every call: the entry point's class stops now, not after a run of faults.
                    guard.SwitchOff();
                    StandsDown(e, guard.Where + (guard.Feature != null ? " (" + guard.Feature + ")" : string.Empty));
                    if (component != null)
                    {
                        component.enabled = false;
                    }
                    return;
                }
                bool now = guard.Count(Time.realtimeSinceStartup);
                if (guard.Faults <= MostDetailed)
                {
                    Logger?.Warn("Guard: " + guard.Where + " failed (" + guard.Faults + "); the feature goes on.");
                    Logger?.Error(e);
                }
                else if (guard.Faults <= MostShort)
                {
                    Logger?.Warn("Guard: " + guard.Where + " failed again (" + guard.Faults + "): " + e.GetType().Name + ": " + e.Message
                                 + (guard.Faults == MostShort ? " (Said no more this session.)" : string.Empty));
                }
                if (component != null && guard.Off)
                {
                    component.enabled = false;
                }
                if (now)
                {
                    SwitchedOff(guard);
                }
            }
            catch (Exception)
            {
                // (the guard itself must never be the next fault)
            }
        }

        private static void SwitchedOff(FeatureGuard guard)
        {
            string feature = guard.Feature;
            if (feature != null && !FeatureGuards.SwitchOff(feature))
            {
                return;   // (said already for the feature)
            }
            string name = feature != null ? FeatureName(feature) : "reallife_guard_unnamed".GetLocalization();
            Logger?.Warn("Guard: " + guard.Where + " failed " + FeatureGuard.Most + " times within " + FeatureGuard.Seconds + " seconds; "
                         + (feature != null ? "the feature '" + feature + "' is" : "it is") + " switched off until the game is loaded again. Its saved data is left as it was.");
            if (feature != null)
            {
                try
                {
                    Meta.Features.Find(feature)?.CleanupAction?.Invoke();   // off the street, as the options page does when it is switched off
                }
                catch (Exception e)
                {
                    Logger?.Error(e);
                }
            }
            Tell("reallife_guard_notice_fault".GetLocalization().Replace("{feature}", name));
        }

        private static readonly FeatureGuard TestGuard = FeatureGuards.For(null, "the guard's test (options page, test tools)");

        /// <summary>
        /// The options page's test: five faults of an entry point of its own, as a feature broken by a game update would
        /// make them, so that the lines in Player.log and the one notice can be seen. Nothing of the mod is switched off.
        /// </summary>
        public static void TestFault()
        {
            for (int i = 0; i < FeatureGuard.Most; i++)
            {
                try
                {
                    throw new InvalidOperationException("a test fault of the guard (options page, test tools), " + (i + 1) + " of " + FeatureGuard.Most);
                }
                catch (Exception e)
                {
                    Fail(TestGuard, e);
                }
            }
            if (TestGuard.Faults > FeatureGuard.Most)
            {
                Logger?.Info("Guard: the test was run before this session; its entry point is off already, so nothing more is said.");
            }
        }

        /// <summary>The feature's title in the player's language (its id when it has none).</summary>
        public static string FeatureName(string feature)
        {
            try
            {
                Meta.Feature found = Meta.Features.Find(feature);
                return found != null ? found.TitleKey.GetLocalization() : feature;
            }
            catch (Exception)
            {
                return feature;
            }
        }

        // ---------------------------------------------------------------- whose a handler is

        private static readonly Dictionary<System.Reflection.MethodInfo, FeatureGuard> ByMethod = new Dictionary<System.Reflection.MethodInfo, FeatureGuard>(64);

        /// <summary>The feature of Meta/FeatureList a class of the mod belongs to (Core/FeatureOwners), or null for the mod's own machinery.</summary>
        public static string FeatureOf(Type owner)
        {
            return owner == null ? null : FeatureOwners.Of(owner.Name, owner.Namespace);
        }

        /// <summary>
        /// The guard of the class a handler is a method of (a lambda's: the class it is written in), made the first time
        /// it is asked for. Its faults count for that class, and a feature switched off skips it.
        /// </summary>
        public static FeatureGuard OfHandler(Delegate handler)
        {
            if (handler == null)
            {
                return null;
            }
            System.Reflection.MethodInfo method = handler.Method;
            lock (ByMethod)
            {
                if (ByMethod.TryGetValue(method, out FeatureGuard known))
                {
                    return known;
                }
            }
            FeatureGuard guard = OfType(method.DeclaringType);
            lock (ByMethod)
            {
                ByMethod[method] = guard;
            }
            return guard;
        }

        // The guard of a class of the mod (a nested one's: its outermost class's), made the first time it is asked for.
        private static FeatureGuard OfType(Type owner)
        {
            while (owner != null && owner.DeclaringType != null && owner.Name.IndexOf('<') >= 0)
            {
                owner = owner.DeclaringType;   // (a lambda's class is the compiler's, inside the class it is written in)
            }
            return OfOwner(owner) ?? For(FeatureOf(owner), owner != null ? owner.Name : "a handler", owner);
        }

        /// <summary>
        /// Calls each handler of one of the mod's own events in turn, each in a try of its own: a handler that throws is
        /// written to Player.log against its class's guard and the others still run; a handler whose feature was
        /// switched off is skipped. For the economy's hour and day, which a dozen features share (its second goes to each in the start line's order: Economy/Banking/EconomyDirector.Follow).
        /// </summary>
        public static void Each(Action handlers)
        {
            if (handlers == null)
            {
                return;
            }
            Delegate[] list = handlers.GetInvocationList();
            for (int i = 0; i < list.Length; i++)
            {
                var handler = (Action)list[i];
                FeatureGuard guard = OfHandler(handler);
                if (guard != null && guard.Off)
                {
                    continue;
                }
                using var hotSpot = HotSpots.Time(HandlerSlot(handler, "Event"));
                try
                {
                    handler();
                }
                catch (Exception e)
                {
                    Fail(guard, e);
                }
            }
        }

        // ---------------------------------------------------------------- coroutines

        /// <summary>
        /// A coroutine of the mod's run inside the guard of the class its code is written in (whichever component hosts
        /// it): <c>StartCoroutine(Guard.Routine(Tick()))</c>. A fault ends it and is counted as an entry point's is (a member
        /// the game lacks stops the class's entry points at once, said once), instead of reaching Unity's own log. A
        /// coroutine under way is never cut short between its steps, whatever is switched off meanwhile: one may be half
        /// way through a fade or a move of the player that only its own end puts right. A coroutine it waits for (an
        /// IEnumerator it yields) runs inside the same guard. Each step is timed under the coroutine's own name
        /// ("Coroutine: Class.Method", <see cref="RoutineSlot"/>), so that the time the engine counts under "coroutines and
        /// timers" has the mod's names too. Cost: one small object for each coroutine started, nothing per step beyond one
        /// call and one flag test.
        /// </summary>
        public static IEnumerator Routine(IEnumerator routine)
        {
            return routine == null ? null : new GuardedRoutine(OfType(routine.GetType()), routine);
        }

        private sealed class GuardedRoutine : IEnumerator
        {
            private readonly FeatureGuard _guard;
            private readonly IEnumerator _inner;
            private readonly int _slot;
            private object _current;

            public GuardedRoutine(FeatureGuard guard, IEnumerator inner)
            {
                _guard = guard;
                _inner = inner;
                _slot = RoutineSlot(inner.GetType());
            }

            public object Current => _current;

            public bool MoveNext()
            {
                using var hotSpot = HotSpots.Time(_slot);
                try
                {
                    if (!_inner.MoveNext())
                    {
                        return false;
                    }
                    object yielded = _inner.Current;
                    _current = yielded is IEnumerator waited && !(yielded is GuardedRoutine) ? new GuardedRoutine(_guard, waited) : yielded;
                    return true;
                }
                catch (Exception e)
                {
                    Fail(_guard, e);
                    return false;
                }
            }

            public void Reset()
            {
                _inner.Reset();
            }
        }

        // ---------------------------------------------------------------- timing (Core/HotSpots)

        private static readonly Dictionary<Type, int> RoutineSlots = new Dictionary<Type, int>(96);
        private static readonly Dictionary<System.Reflection.MethodInfo, int> HandlerSlots = new Dictionary<System.Reflection.MethodInfo, int>(128);

        /// <summary>
        /// The HotSpots slot of a coroutine, by the class the compiler made of it ("&lt;Tick&gt;d__12" inside TransitDirector:
        /// "Coroutine: TransitDirector.Tick"); a coroutine of the game's the mod waits on is named by the game's class and
        /// method. Made once per coroutine method, when the first of it starts.
        /// </summary>
        public static int RoutineSlot(Type routine)
        {
            lock (RoutineSlots)
            {
                if (RoutineSlots.TryGetValue(routine, out int known))
                {
                    return known;
                }
                int slot = HotSpots.Slot("Coroutine: " + HotSpots.Written(routine, null));
                RoutineSlots[routine] = slot;
                return slot;
            }
        }

        /// <summary>
        /// The HotSpots slot of a handler the guard wraps ("Event: CareersDirector.OnHour", "Listener: StocksApp.Awake"
        /// for a lambda written in Awake). Made once per handler method, when it is wrapped.
        /// </summary>
        public static int HandlerSlot(Delegate handler, string kind)
        {
            if (handler == null)
            {
                return -1;
            }
            System.Reflection.MethodInfo method = handler.Method;
            lock (HandlerSlots)
            {
                if (HandlerSlots.TryGetValue(method, out int known))
                {
                    return known;
                }
                int slot = HotSpots.Slot(kind + ": " + HotSpots.Written(method.DeclaringType, method.Name));
                HandlerSlots[method] = slot;
                return slot;
            }
        }

        // ---------------------------------------------------------------- the game's events

        /// <summary>As <see cref="Wrap(FeatureGuard, Action)"/>, with the guard of the handler's class.</summary>
        public static Action Wrap(Action handler)
        {
            return handler == null ? null : Wrap(OfHandler(handler), handler);
        }

        public static Action<T> Wrap<T>(Action<T> handler)
        {
            return handler == null ? null : Wrap(OfHandler(handler), handler);
        }

        public static Action<T1, T2> Wrap<T1, T2>(Action<T1, T2> handler)
        {
            return handler == null ? null : Wrap(OfHandler(handler), handler);
        }

        /// <summary>As <see cref="Wrap(Action)"/>, for the game's notification event (Notifications.onShow, eight arguments).</summary>
        public static Action<T1, T2, T3, T4, T5, T6, T7, T8> Wrap<T1, T2, T3, T4, T5, T6, T7, T8>(Action<T1, T2, T3, T4, T5, T6, T7, T8> handler)
        {
            FeatureGuard guard = OfHandler(handler);
            int slot = HandlerSlot(handler, "Event");
            return (a, b, c, d, e, f, g, h) =>
            {
                using var hotSpot = HotSpots.Time(slot);
                if (guard.Off)
                {
                    return;
                }
                try
                {
                    handler(a, b, c, d, e, f, g, h);
                }
                catch (Exception fault)
                {
                    Fail(guard, fault);
                }
            };
        }

        /// <summary>A handler for one of the game's events that can never throw into the game's own code. Made once and kept: event holders compare it.</summary>
        public static Action Wrap(FeatureGuard guard, Action handler)
        {
            int slot = HandlerSlot(handler, "Event");
            return () =>
            {
                using var hotSpot = HotSpots.Time(slot);
                if (guard.Off)
                {
                    return;
                }
                try
                {
                    handler();
                }
                catch (Exception e)
                {
                    Fail(guard, e);
                }
            };
        }

        public static Action<T> Wrap<T>(FeatureGuard guard, Action<T> handler)
        {
            int slot = HandlerSlot(handler, "Event");
            return value =>
            {
                using var hotSpot = HotSpots.Time(slot);
                if (guard.Off)
                {
                    return;
                }
                try
                {
                    handler(value);
                }
                catch (Exception e)
                {
                    Fail(guard, e);
                }
            };
        }

        public static Action<T1, T2> Wrap<T1, T2>(FeatureGuard guard, Action<T1, T2> handler)
        {
            int slot = HandlerSlot(handler, "Event");
            return (a, b) =>
            {
                using var hotSpot = HotSpots.Time(slot);
                if (guard.Off)
                {
                    return;
                }
                try
                {
                    handler(a, b);
                }
                catch (Exception e)
                {
                    Fail(guard, e);
                }
            };
        }

        /// <summary>
        /// A listener for one of the engine's or the game's UnityEvents (a button's click, a toggle's change, a car's
        /// collision, the vehicle's input each physics step) that can never throw into the event's other listeners, with the
        /// guard of the class it is written in. Made once and kept where it is taken off again: the event compares it.
        /// </summary>
        public static UnityAction Listener(UnityAction handler)
        {
            if (handler == null)
            {
                return null;
            }
            FeatureGuard guard = OfHandler(handler);
            int slot = HandlerSlot(handler, "Listener");
            return () =>
            {
                using var hotSpot = HotSpots.Time(slot);
                if (guard.Off)
                {
                    return;
                }
                try
                {
                    handler();
                }
                catch (Exception e)
                {
                    Fail(guard, e);
                }
            };
        }

        public static UnityAction<T> Listener<T>(UnityAction<T> handler)
        {
            if (handler == null)
            {
                return null;
            }
            FeatureGuard guard = OfHandler(handler);
            int slot = HandlerSlot(handler, "Listener");
            return value =>
            {
                using var hotSpot = HotSpots.Time(slot);
                if (guard.Off)
                {
                    return;
                }
                try
                {
                    handler(value);
                }
                catch (Exception e)
                {
                    Fail(guard, e);
                }
            };
        }

        /// <summary>One step of the mod's start: a step that throws is written to Player.log and the others still run.</summary>
        public static void Step(string what, Action step)
        {
            try
            {
                step();
            }
            catch (Exception e)
            {
                if (StandsDown(e, "the start step '" + what + "'"))
                {
                    return;
                }
                Logger?.Warn("Guard: the start step '" + what + "' failed; the rest of the mod starts without it.");
                Logger?.Error(e);
            }
        }

        /// <summary>Rows of one feature added to the options page: a feature whose rows cannot be made costs the page only its rows.</summary>
        public static ModOptions Guarded(this ModOptions options, Func<ModOptions, ModOptions> add)
        {
            try
            {
                return add(options) ?? options;
            }
            catch (Exception e)
            {
                Logger?.Warn("Guard: a feature's rows could not be added to the options page; the page is shown without them.");
                Logger?.Error(e);
                return options;
            }
        }

        /// <summary>The options page put in order (Meta/OptionsLayout); the page as it was added if that fails.</summary>
        public static ModOptions Organised(ModContext context, ModOptions options)
        {
            try
            {
                return Meta.OptionsLayout.Organise(context, options);
            }
            catch (Exception e)
            {
                Logger?.Warn("Guard: the options page could not be put in order; it is shown as the features added it.");
                Logger?.Error(e);
                return options;
            }
        }

        // ---------------------------------------------------------------- the save

        // A store could not read its line: it is kept as it was (Core/SaveLines), said once per line.
        private static void Kept(string key, string why)
        {
            Logger?.Warn("Save: the line '" + key + "' could not be read (" + why + "). It is kept in the save exactly as it was, for a later version of the mod; "
                         + "the feature starts afresh this session and its line is not written over.");
            string feature = null;
            try
            {
                Meta.Feature owner = Meta.Features.DeclaredData(key);
                feature = owner != null ? owner.TitleKey.GetLocalization() : null;
            }
            catch (Exception)
            {
                feature = null;
            }
            Tell("reallife_guard_notice_kept".GetLocalization().Replace("{feature}", feature ?? key));
        }

        // ---------------------------------------------------------------- notices

        /// <summary>A notice for the player, shown once the game's HUD can show it (at once if it can now).</summary>
        public static void Tell(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }
            Pending.Enqueue(text);
            if (Notices != null)
            {
                Notices.enabled = true;
            }
        }

        internal static bool ShowNext()
        {
            if (Pending.Count == 0)
            {
                return false;
            }
            if (Notifications.onShow == null)
            {
                return true;   // no HUD yet: asked again in a moment
            }
            string text = Pending.Dequeue();
            try
            {
                var data = new Dictionary<string, string>(1) { { "text", text } };
                Notifications.Show(NotificationType.Warning, "reallife_opt_text", data, 16f, null, null, true, false);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
            return Pending.Count > 0;
        }
    }

    /// <summary>Shows <see cref="Guard"/>'s notices once the HUD is there, one every few seconds; switched off while there are none.</summary>
    public sealed class GuardNotices : MonoBehaviour
    {
        private const float Gap = 3f;   // seconds between two notices, and between two looks for the HUD

        private float _next;

        private void Awake()
        {
            Guard.Notices = this;
        }

        private void Update()
        {
            if (Time.unscaledTime < _next)
            {
                return;
            }
            _next = Time.unscaledTime + Gap;
            try
            {
                if (!Guard.ShowNext())
                {
                    enabled = false;
                }
            }
            catch (Exception)
            {
                enabled = false;
            }
        }

        private void OnDestroy()
        {
            if (Guard.Notices == this)
            {
                Guard.Notices = null;
            }
        }
    }
}
