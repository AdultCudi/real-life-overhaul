using System;
using Localizor;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>The guard's part for faults: a missing member, a run of faults, the guards of entry points.</summary>
    public static partial class Guard
    {
        /// <summary>True when the exception says the game lacks a type or member the mod was compiled against, or made it inaccessible.</summary>
        public static bool Missing(Exception e)
        {
            return e is TypeLoadException || e is MemberAccessException
                   || (e is TypeInitializationException && e.InnerException != null && Missing(e.InnerException));
        }

        /// <summary>
        /// For the catch round a part that can stop alone: true when the game lacks a member it names, said once; the
        /// caller then stops the part for the session. False for any other fault.
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

        /// <summary>For a step done without on a game that lacks what it needs: true when the exception says so, said once.</summary>
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

        // Once a session per key: a game that lacks something lacks it for good. True the first time.
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

        /// <summary>A guard for an entry point, a static of the class it guards; the owner's save writer is skipped once it is off.</summary>
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
                // Runs the class's initialiser, so its own FrameGuard is the one found.
                System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(owner.TypeHandle);
            }
            catch (Exception)
            {
                // a class that cannot be initialised has no guard of its own: one is made for it
            }
            lock (ByOwner)
            {
                return ByOwner.TryGetValue(owner, out FeatureGuard guard) ? guard : null;
            }
        }

        /// <summary>
        /// Adds a component of the city's start, unless its own FrameGuard's feature was switched off at start because the
        /// game lacks what it stands on, or stands on New York's data in another city (Core/CityCheck). A component guarded
        /// by hand (Life/LifeEvents) is always added.
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
            if (guard != null && guard.Away)
            {
                CityCheck.LeftOut(guard.Feature);
                Logger?.Info("City check: " + type.Name + " is not started: its feature '" + guard.Feature + "' stands on New York's data.");
                return null;
            }
            return root.AddComponent<T>();
        }

        /// <summary>
        /// An entry point threw: a missing member stops its class at once; any other fault is logged, and a run of them
        /// switches the feature off and stops the component. Never throws.
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
                    // It would fail at every call: stopped now, not after a run of faults.
                    guard.SwitchOff();
                    StandsDown(e, guard.Where + (guard.Feature != null ? " (" + guard.Feature + ")" : string.Empty));
                    if (component != null)
                    {
                        component.enabled = false;
                    }
                    LetGo(guard.Where);   // a class of the mod's own machinery that stops lets go of what it held (a feature's holds go with the feature)
                    return;
                }
                bool now = guard.Count(Time.realtimeSinceStartup);
                // Each line carries the fault's kind, message and first methods: a player who copies only the mod's own lines sends them.
                if (guard.Faults <= MostDetailed)
                {
                    Logger?.Warn("Guard: " + guard.Where + " failed (" + guard.Faults + "): " + FaultWords.Of(e) + "; the feature goes on.");
                    Logger?.Error(e);
                }
                else if (guard.Faults <= MostShort)
                {
                    Logger?.Warn("Guard: " + guard.Where + " failed again (" + guard.Faults + "): " + FaultWords.Of(e)
                                 + (guard.Faults == MostShort ? " (Said no more this session.)" : string.Empty));
                }
                if (component != null && guard.Off)
                {
                    component.enabled = false;
                }
                if (now)
                {
                    SwitchedOff(guard, e);
                }
            }
            catch (Exception)
            {
                // the guard itself must never be the next fault
            }
        }

        private static void SwitchedOff(FeatureGuard guard, Exception last)
        {
            string feature = guard.Feature;
            if (feature != null && !FeatureGuards.SwitchOff(feature))
            {
                return;   // said already for the feature
            }
            string name = feature != null ? FeatureName(feature) : "reallife_guard_unnamed".GetLocalization();
            Logger?.Warn("Guard: " + guard.Where + " failed " + FeatureGuard.Most + " times within " + FeatureGuard.Seconds + " seconds; "
                         + (feature != null ? "the feature '" + feature + "' is" : "it is") + " switched off until the game is started again. Its saved data is left as it was. "
                         + "The last fault: " + FaultWords.Of(last) + ".");
            if (feature != null)
            {
                try
                {
                    Meta.Features.Find(feature)?.CleanupAction?.Invoke();   // as the options page does when it is switched off
                }
                catch (Exception e)
                {
                    Logger?.Error(e);
                }
            }
            LetGo(feature ?? guard.Where);
            string what = Place(guard.Where);
            Tell(what != null
                ? "reallife_guard_notice_fault_at".GetLocalization().Replace("{feature}", name).Replace("{what}", what)
                : "reallife_guard_notice_fault".GetLocalization().Replace("{feature}", name));
        }

        // Everything the part still holds over the screen let go (Core/ScreenHolds), after its own clean-up: what would answer a card
        // or give the keys back is off now, so nothing else would. One line says what was held.
        private static void LetGo(string part)
        {
            try
            {
                var released = new System.Collections.Generic.List<string>(4);
                int asked = ScreenHolds.Release(part, released, (what, e) =>
                {
                    Logger?.Warn("Guard: " + what + " could not be let go of (" + FaultWords.Of(e) + ").");
                });
                if (asked > 0)
                {
                    Logger?.Warn("Guard: '" + part + "' is off, so what it could hold over the screen was let go of: "
                                 + (released.Count > 0 ? string.Join("; ", released) : "nothing was held") + ".");
                }
            }
            catch (Exception)
            {
                // the guard itself must never be the next fault
            }
        }

        private static readonly System.Collections.Generic.Dictionary<string, Func<string>> Places =
            new System.Collections.Generic.Dictionary<string, Func<string>>(StringComparer.Ordinal);

        /// <summary>
        /// Gives a guard's place its plain words for the notice, in the player's language ("the status bar over the city" for the
        /// HUD's guard): asked only when that guard switches its feature off. A place nobody named leaves the notice without them.
        /// </summary>
        public static void Named(string where, Func<string> words)
        {
            if (string.IsNullOrEmpty(where) || words == null)
            {
                return;
            }
            lock (Places)
            {
                Places[where] = words;
            }
        }

        // The plain words of a guard's place, or null: none given, none to give now, or they could not be made.
        private static string Place(string where)
        {
            Func<string> words;
            lock (Places)
            {
                if (where == null || !Places.TryGetValue(where, out words))
                {
                    return null;
                }
            }
            try
            {
                string said = words();
                return string.IsNullOrEmpty(said) ? null : said;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>The options page's test: five faults of an entry point of its own, to see the log lines and the notice.</summary>
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
    }
}
