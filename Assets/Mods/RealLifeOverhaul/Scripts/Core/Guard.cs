using System;
using System.Collections.Generic;
using System.Text;
using BAModAPI;
using BigAmbitions.Mods;
using Localizor;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// What keeps one feature's trouble from becoming the game's, and what tells the player about it.
    ///
    /// THE CONTRACT. <see cref="Start"/>, once at game start, resolves <see cref="GameContract"/> (every piece of the
    /// game the mod reaches by name) and reads the game's build. Each part switched off by a missing or changed piece
    /// gets one line in Player.log; one notice names them all when the first city is loaded, gentler when the game is
    /// newer than the build this version was checked against; the options page says it under Diagnostics and after
    /// the name of each feature hit (<see cref="Note"/>).
    ///
    /// FAULTS. Every entry point the engine or the game calls (Update, LateUpdate, FixedUpdate, a handler on one of
    /// the game's events, a step of the start) runs inside a try with a <see cref="FeatureGuard"/> of its own. The first
    /// fault is written to Player.log in full and the feature goes on; a run of them in a short time
    /// (<see cref="FeatureGuard.Most"/> in <see cref="FeatureGuard.Seconds"/> s) switches the whole feature off for the
    /// session: its components stop, what it has in the street is taken away (its Cleanup in Meta/FeatureList), its
    /// save line is left as it last wrote it (Core/SaveHook skips its writer), and one line and one notice say so.
    ///
    /// SAVES. A line a store could not read is kept as it was (Core/SaveLines), with one line and one notice per save.
    ///
    /// Cost: nothing per frame beyond the try around each entry point; the notices' component is switched off while
    /// it has nothing to say.
    /// </summary>
    public static class Guard
    {
        private const int MostDetailed = 3;   // faults of one entry point written in full; after that one short line each up to MostShort
        private const int MostShort = 12;

        public static IModLogger Logger;

        private static readonly Queue<string> Pending = new Queue<string>(4);
        private static readonly Dictionary<Type, FeatureGuard> ByOwner = new Dictionary<Type, FeatureGuard>(96);
        private static bool _started;
        private static bool _contractTold;
        private static int _build = -1;
        private static HashSet<string> _hit;   // the features with a part off, once resolved
        private static int _offParts;

        /// <summary>True when any part is off after a game update, or any feature was switched off after its faults.</summary>
        public static bool AnyOff => _offParts > 0 || FeatureGuards.AnyOff;

        /// <summary>The running game's build (GameVersion.buildNumber), or 0 when it could not be read.</summary>
        public static int GameBuild => _build < 0 ? 0 : _build;

        /// <summary>True when the running game is newer than the build this version of the mod was checked against.</summary>
        public static bool GameNewer => GameBuild > ContractBook.CheckedBuild;

        /// <summary>Made by Core/RealLifeOverhaulMod for each city; shows what is waiting once the HUD is there.</summary>
        public static GuardNotices Notices;

        // ---------------------------------------------------------------- the contract

        /// <summary>Once, at game start, before any feature: resolves the contract and reads the game's build.</summary>
        public static void Start(IModLogger logger)
        {
            Logger = logger;
            if (_started)
            {
                return;
            }
            _started = true;
            SaveLines.Told = Kept;
            try
            {
                ContractBook.Resolve(ContractBook.FindLoaded);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
            _build = ReadBuild();
            try
            {
                _hit = ContractBook.FeaturesHit();
                _offParts = ContractBook.OffParts().Count;
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
            try
            {
                List<GamePart> off = ContractBook.OffParts();
                foreach (GameMember member in ContractBook.Problems())
                {
                    if (member.Optional)
                    {
                        Logger?.Info("Game contract: " + member.Label + ": " + member.Problem + "; the mod does without it.");
                    }
                }
                foreach (GamePart part in off)
                {
                    Logger?.Warn("Game contract: " + part.English + " is switched off until the mod is updated. " + part.Why + ".");
                }
                string build = GameBuild > 0 ? "build " + GameBuild : "a build that could not be read";
                if (GameNewer)
                {
                    Logger?.Warn("Game contract: the game is " + build + ", newer than build " + ContractBook.CheckedBuild + " this version of the mod was checked against; "
                                 + (off.Count == 0 ? "every piece of the game it uses is there as expected." : off.Count + " part(s) switched off (above), everything else runs."));
                }
                else
                {
                    Logger?.Info("Game contract: " + ContractBook.Members.Count + " pieces of the game checked, " + off.Count + " part(s) off; the game is " + build
                                 + ", this version of the mod was checked against build " + ContractBook.CheckedBuild + ".");
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        // Read apart, so that a change to the game's version class costs only this.
        private static int ReadBuild()
        {
            try
            {
                return BuildOfGame();
            }
            catch (Exception)
            {
                return 0;
            }
        }

        private static int BuildOfGame()
        {
            GameVersion version = GameVersion.GetCurrent();
            return version != null ? version.buildNumber : 0;
        }

        /// <summary>The first time a city has loaded: one notice if any part is off.</summary>
        public static void CityLoaded()
        {
            if (_contractTold)
            {
                return;
            }
            _contractTold = true;
            try
            {
                List<GamePart> off = ContractBook.OffParts();
                if (off.Count > 0)
                {
                    Tell((GameNewer ? "reallife_guard_notice_update" : "reallife_guard_notice_parts").GetLocalization().Replace("{parts}", PartNames(off)));
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
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
            if (FeatureGuards.FeatureOff(feature))
            {
                return "reallife_guard_note_faults".GetLocalization();
            }
            return _hit != null && _hit.Contains(feature) ? "reallife_guard_note_parts".GetLocalization() : null;
        }

        /// <summary>For the options page's button under Diagnostics: what is off, and the game's build against the checked one.</summary>
        public static string Status()
        {
            List<GamePart> off = ContractBook.OffParts();
            string text = (off.Count == 0 ? "reallife_guard_status_ok" : "reallife_guard_status_off").GetLocalization()
                .Replace("{parts}", PartNames(off))
                .Replace("{build}", GameBuild > 0 ? GameBuild.ToString() : "?")
                .Replace("{checked}", ContractBook.CheckedBuild.ToString());
            var faults = new StringBuilder();
            foreach (string feature in FeatureGuards.OffList)
            {
                faults.Append(faults.Length > 0 ? "; " : string.Empty).Append(FeatureName(feature));
            }
            if (faults.Length > 0)
            {
                text += "\n\n" + "reallife_guard_status_faults".GetLocalization().Replace("{features}", faults.ToString());
            }
            return text;
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
            lock (ByOwner)
            {
                return ByOwner.TryGetValue(owner, out FeatureGuard guard) ? guard : null;
            }
        }

        /// <summary>
        /// An entry point threw. Written to Player.log (the first ones in full); a run of them switches the feature off
        /// for the session and stops the component that threw. Never throws.
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
            string name = feature != null ? FeatureName(feature) : guard.Where;
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

        // ---------------------------------------------------------------- the game's events

        /// <summary>A handler for one of the game's events that can never throw into the game's own code. Made once and kept: event holders compare it.</summary>
        public static Action Wrap(FeatureGuard guard, Action handler)
        {
            return () =>
            {
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
            return value =>
            {
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
            return (a, b) =>
            {
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

        /// <summary>One step of the mod's start: a step that throws is written to Player.log and the others still run.</summary>
        public static void Step(string what, Action step)
        {
            try
            {
                step();
            }
            catch (Exception e)
            {
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
