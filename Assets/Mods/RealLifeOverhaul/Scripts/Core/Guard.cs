using System;
using System.Collections.Generic;
using System.Text;
using BAModAPI;
using Localizor;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// What keeps one feature's trouble from becoming the game's, and tells the player about it.
    /// THE CONTRACT: <see cref="Start"/>, once before any feature, resolves <see cref="GameContract"/>, reads the game's
    /// build and checks the mod's compiled code against the running game (Core/ModScan). What the game lacks is logged
    /// once with what stands on it, and those features are switched off before they run; one notice says so
    /// (Core/GameBuild.Notice), and the options page (<see cref="Note"/>).
    /// FAULTS: every entry point runs in a try with a <see cref="FeatureGuard"/>. A missing member (<see cref="Missing"/>)
    /// stops its class at once; any other fault is logged and the feature goes on, until a run of them switches the
    /// feature off for the session (its Cleanup runs, its save line is left as last written). <see cref="StandsDown"/>
    /// and <see cref="DoesWithout(Exception, string)"/> serve parts inside an entry point; the game's other calls in are in Core/GuardCalls.
    /// SAVES: a line a store could not read is kept as it was (Core/SaveLines), with one notice.
    /// COST: at game start the contract and the code check (about twenty milliseconds); per frame only the try round
    /// each entry point.
    /// </summary>
    public static partial class Guard
    {
        private const int MostDetailed = 3;   // faults of one entry point logged in full; then a short line each up to MostShort
        private const int MostShort = 12;
        private const int MostNamed = 6;      // the mod's classes a log line names for one piece the game lacks

        public static IModLogger Logger;

        private static readonly Queue<string> Pending = new Queue<string>(4);
        private static readonly Dictionary<Type, FeatureGuard> ByOwner = new Dictionary<Type, FeatureGuard>(96);
        private static readonly HashSet<string> Told = new HashSet<string>(StringComparer.Ordinal);
        private static readonly HashSet<string> Lacking = new HashSet<string>(StringComparer.Ordinal);   // features off from the start: the game lacks what they stand on
        private static readonly List<string> StoodDown = new List<string>(4);                              // parts stopped when first used, in English
        private static bool _started;
        private static HashSet<string> _hit;   // the features with a part off, once resolved
        private static int _offParts;

        private static readonly FeatureGuard TestGuard = FeatureGuards.For(null, "the guard's test (options page, test tools)");
        private static readonly Dictionary<System.Reflection.MethodInfo, FeatureGuard> ByMethod = new Dictionary<System.Reflection.MethodInfo, FeatureGuard>(64);
        private static readonly Dictionary<Type, int> RoutineSlots = new Dictionary<Type, int>(96);
        private static readonly Dictionary<System.Reflection.MethodInfo, int> HandlerSlots = new Dictionary<System.Reflection.MethodInfo, int>(128);

        /// <summary>True when any part is off after a game update, or any feature was switched off after its faults.</summary>
        public static bool AnyOff => _offParts > 0 || Lacking.Count > 0 || StoodDown.Count > 0 || FeatureGuards.AnyOff;

        /// <summary>True when a part of the mod is off because this game lacks what it stands on.</summary>
        public static bool PartsOff => _offParts > 0 || Lacking.Count > 0;

        /// <summary>Made by Core/RealLifeOverhaulMod for each city.</summary>
        public static GuardNotices Notices;

        /// <summary>Once at game start, before any feature: the contract, the build, the mod's code against this game.</summary>
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

        // Each type or member this game lacks that the contract does not name: logged once, its features switched off now.
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
                        continue;   // said above, with its part
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
                                 + (m.Machinery ? (titles.Count > 0 ? "; the mod's own machinery names it too, and stops only where it meets it"
                                                                    : ", and they are the mod's own machinery, which stops only where it meets it") : string.Empty)
                                 + ". " + Builds);
                }
                string summary = "Game contract check (the mod's code): " + asked + " types and members of the game's and Unity's assemblies asked of this game in "
                             + clock.ElapsedMilliseconds + " ms; " + missing.Count + " missing"
                             + (told < missing.Count ? " (" + (missing.Count - told) + " of them pieces of the contract, above)" : string.Empty)
                             + (Lacking.Count > 0 ? "; " + Lacking.Count + " feature(s) switched off: " + string.Join(", ", Lacking) : string.Empty)
                             + ": " + (missing.Count == 0 ? "PASS." : "the mod runs without them.");
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

        // True for a line of the contract: its type, or a member by name (a property by its accessors).
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
        /// The parts the notice names, in the player's language: those off, and with <paramref name="doneWithout"/> (an
        /// older game) those that work without what it lacks.
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

        /// <summary>The names of the parts the pieces stand on, in the player's language, for the notice's test.</summary>
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

        /// <summary>For the options page: a note after a feature's name while a part of it is off; null while all of it runs.</summary>
        public static string Note(string feature)
        {
            if (feature == null)
            {
                return AnyOff ? "reallife_guard_note_parts".GetLocalization() : null;   // the page's first group: something is off
            }
            if (FeatureGuards.FeatureOff(feature) && !Lacking.Contains(feature))
            {
                return "reallife_guard_note_faults".GetLocalization();
            }
            return Lacking.Contains(feature) || (_hit != null && _hit.Contains(feature)) ? "reallife_guard_note_parts".GetLocalization() : null;
        }

        /// <summary>For the options page's Diagnostics button: what is off, and the game's build against the mod's.</summary>
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

        /// <summary>For a bug report: the state of the guard in English, whatever the player's language. On a button press only.</summary>
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

        /// <summary>A notice for the player, shown once the game's HUD can show it.</summary>
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

    /// <summary>Shows <see cref="Guard"/>'s notices once the HUD is there, one every few seconds. COST: switched off while idle.</summary>
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
