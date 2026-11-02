using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The code of a job that runs in play compiled behind the loading screen, so its first run after a load does not pay
    /// Mono's compiler in a frame of play (Core/CodeWarm has the why). The owner's log of 2 Nov 2026 had first runs at many
    /// times what the same work costs once compiled: the city drivers' first look 7.2 ms and the traffic's own 6.9 ms for one
    /// car (some 0.1 ms a look after), the bare ground check's first step 3.4 ms, the office count 8.5 ms and the far
    /// squares' survey's slowest tick 3.9 ms. A feature names the types its job lives in, and may name a few methods of a
    /// larger type: every method of those types (and of the types nested in them, their closures and coroutines) is a root,
    /// read for what it calls down to <see cref="Deepest"/> calls, so a path the job takes rarely (a hold line's first raise, a
    /// self-check's first line) is compiled too. Compiled code lasts the whole run: the walk is kept for the session, and a
    /// later load compiles nothing met before. Nothing a job does changes, nor its order or its numbers: only when its code is
    /// compiled.
    /// COST: behind the loading screen, once a session, the methods not compiled yet (a tenth of a millisecond or so each:
    /// the five jobs handed over take in some 1,200 methods, 640 of them the mod's own read for their calls, counted over
    /// the built mod on 2 Nov 2026); nothing at a later load. Handed over in play (a feature switched on again), a slice of
    /// <see cref="SliceMs"/> a frame behind the black screen of a door. Nothing per frame.
    /// </summary>
    public static class LoadWarm
    {
        internal const int Deepest = 3;           // calls from a job's own methods read further: its helpers, and theirs
        internal const int MostMethods = 12000;   // methods taken in at most over the session (the walk keeps what it has seen)
        internal const double SliceMs = 1.0;      // ms a frame behind a black screen in play may take (never behind the loading screen)

        private static CodeWarm _warm;   // kept across loads: compiled code is not lost with a city
        private static readonly HashSet<string> Rooted = new HashSet<string>(StringComparer.Ordinal);   // pieces whose roots are in

        /// <summary>
        /// Hands over a job's code as a piece of the loading screen's one-off work (<see cref="ScenePrewarm"/>): every method of
        /// <paramref name="types"/>, and the methods of <paramref name="host"/> named in <paramref name="methods"/> (for a job
        /// that lives in a few methods of a large type). <paramref name="what"/> is a constant text.
        /// </summary>
        public static void Add(string what, Type[] types, Type host = null, params string[] methods)
        {
            ScenePrewarm.Add(what, () => Compile(what, types, host, methods));
        }

        /// <summary>
        /// The same, from a piece already running behind the loading screen (a tweak's own prewarm): compiled at once there;
        /// anywhere else handed over as <see cref="Add"/> does, for the next black screen.
        /// </summary>
        public static void Now(string what, Type[] types, Type host = null, params string[] methods)
        {
            if (UI.Load.LoadScene.isLoading || ScenePrewarm.InLate)
            {
                Compile(what, types, host, methods);
            }
            else
            {
                Add(what, types, host, methods);
            }
        }

        // The piece's roots put in once a session (they are the same at every load), then the walk to its end.
        private static bool Compile(string what, Type[] types, Type host, string[] methods)
        {
            bool loading = UI.Load.LoadScene.isLoading || ScenePrewarm.InLate;
            long began = Stopwatch.GetTimestamp();
            int compiled = 0, read = 0, refused = 0;
            try
            {
                _warm = _warm ?? new CodeWarm(typeof(LoadWarm).Module, MostMethods, Deepest);
                compiled = _warm.Compiled;
                read = _warm.Read;
                refused = _warm.Refused;
                if (Rooted.Add(what))
                {
                    for (int i = 0; types != null && i < types.Length; i++)
                    {
                        _warm.RootAll(types[i]);
                    }
                    RootNamed(host, methods);
                }
                if (loading)
                {
                    WorkSlice whole = WorkSlice.Whole;
                    while (!_warm.Run(ref whole))
                    {
                        whole = WorkSlice.Whole;   // a whole slice still stops before a long method that is not its first
                    }
                }
                else
                {
                    WorkSlice slice = WorkSlice.Of(SliceMs, 1);
                    if (!_warm.Run(ref slice))
                    {
                        ScenePrewarm.NextFrame();   // the rest in the next frame while the screen is black
                        return false;
                    }
                }
            }
            catch (Exception e)
            {
                SessionWatch.Logger?.Warn("Code warm (" + what + "): left to be compiled when first called, as before (" + e.GetType().Name + ": " + e.Message + ").");
                return true;
            }
            if (Meta.Dev.Verbose && _warm.Compiled > compiled)
            {
                CultureInfo c = CultureInfo.InvariantCulture;
                double ms = (Stopwatch.GetTimestamp() - began) * 1000.0 / Stopwatch.Frequency;
                SessionWatch.Logger?.Info("Code warm (" + what + "): " + (_warm.Compiled - compiled) + " method(s) compiled, " + (_warm.Read - read)
                                          + " of the mod's read for their calls, in " + ms.ToString("F1", c) + " ms "
                                          + (loading ? "behind the loading screen" : "behind a black screen, a slice a frame") + "; " + (_warm.Refused - refused)
                                          + " refused (members this build of the game lacks: compiled when first called, as before).");
            }
            return true;
        }

        // The named methods of a type, every overload of each.
        private static void RootNamed(Type host, string[] methods)
        {
            if (host == null || methods == null || methods.Length == 0)
            {
                return;
            }
            MethodInfo[] all = host.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            for (int i = 0; i < all.Length; i++)
            {
                if (Array.IndexOf(methods, all[i].Name) >= 0)
                {
                    _warm.Root(all[i]);
                }
            }
        }
    }
}
