using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The mod's code for moments of play that come a few times a session and are each held to 2 ms a frame (a car got
    /// into or its options changed, the street chess player set up, a director's first load in the start line), compiled
    /// behind the loading screen, so that the first of them does not pay Mono's compiler in play (Core/CodeWarm has the
    /// why). The owner's log of 2 Nov 2026: the first car's handling put on in 4.20 ms where the next car's took under 0.74,
    /// all its sums 20.7 ms against 7.5, the chess opponent seated in 4.84 ms, and the City Ledger's first load 11.4 ms with
    /// the screen clear. Features name the types of a moment in their Awake (<see cref="Add"/>), whose
    /// callers are the game's (a frame, an engine's callback); one piece of Core/ScenePrewarm reads and compiles all they
    /// reach, whole. Compiled code lasts the run, so the walk is kept across loads and a later load compiles only what is
    /// new. Nothing the code does changes, nor its order or its numbers: only when it is compiled. COST: behind the loading
    /// screen only (some tens of milliseconds the first load, next to nothing after); nothing per frame.
    /// </summary>
    public static class MomentWarm
    {
        private const int MostMethods = 12000;   // methods taken in at most, over the run
        private const int Deepest = 3;           // calls from a named type's own methods read further: every method of the types is a root already
        private const int MostSlices = 100000;   // a whole slice stops before a long method that is not its first: slices at most

        private const string What = "the code of moments of play compiled ahead (a car got into, the street chess set up, a first load after the save)";

        private static readonly List<Type> Named = new List<Type>(16);
        private static readonly List<string> Moments = new List<string>(4);
        private static CodeWarm _warm;          // kept across loads: compiled code is not lost with a city
        private static int _handedIn = -1;      // the frame the piece was handed to ScenePrewarm in: the features' Awake of one load share it

        /// <summary>
        /// From a feature's Awake, before the loading screen's pieces run: the types whose code <paramref name="moment"/> (a
        /// constant text) runs, compiled behind this load's screen with everything they call.
        /// </summary>
        public static void Add(string moment, params Type[] types)
        {
            if (types == null)
            {
                return;
            }
            for (int i = 0; i < types.Length; i++)
            {
                if (types[i] != null && !Named.Contains(types[i]))
                {
                    Named.Add(types[i]);
                }
            }
            if (moment != null && !Moments.Contains(moment))
            {
                Moments.Add(moment);
            }
            if (_handedIn != UnityEngine.Time.frameCount)
            {
                _handedIn = UnityEngine.Time.frameCount;   // once a load (a city that went first took its pieces with it)
                ScenePrewarm.Add(What, Run);
            }
        }

        // The piece behind the loading screen: every named type's code and what it calls, read and compiled whole.
        private static bool Run()
        {
            if (Named.Count == 0)
            {
                return true;   // done already this load
            }
            long began = Stopwatch.GetTimestamp();
            _warm ??= new CodeWarm(typeof(MomentWarm).Module, MostMethods, Deepest);
            int read = _warm.Read, compiled = _warm.Compiled, refused = _warm.Refused, skipped = _warm.Skipped;
            for (int i = 0; i < Named.Count; i++)
            {
                _warm.RootDeclared(Named[i]);
            }
            int slices = 0;
            WorkSlice whole = WorkSlice.Whole;
            while (!_warm.Run(ref whole) && ++slices < MostSlices)
            {
                whole = WorkSlice.Whole;   // a whole slice still stops before a long method that is not its first
            }
            if (Meta.Dev.Verbose)
            {
                CultureInfo c = CultureInfo.InvariantCulture;
                bool hidden = UI.Load.LoadScene.isLoading || ScenePrewarm.InLate;
                SessionWatch.Logger?.Info("Moment warm check (behind the loading screen): the code of " + Moments.Count + " moment(s) of play compiled ahead ("
                                          + string.Join("; ", Moments) + "), " + Named.Count + " type(s) named: " + (_warm.Read - read) + " of the mod's method(s) read, "
                                          + (_warm.Compiled - compiled) + " compiled, " + (_warm.Refused - refused)
                                          + " refused (members this build of the game lacks: compiled when first called, as before), " + (_warm.Skipped - skipped)
                                          + " with no code of their own, in " + ((Stopwatch.GetTimestamp() - began) * 1000.0 / Stopwatch.Frequency).ToString("F0", c)
                                          + " ms (wanted: behind the loading screen, never in play): " + (hidden ? "PASS" : "FAIL, in play") + ".");
            }
            Named.Clear();
            Moments.Clear();
            return true;
        }
    }
}
